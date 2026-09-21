"""Executable design reference, not an Android app or Windows driver.

All time arguments use the caller's monotonic milliseconds. No network socket,
thread, device, game, or real-time scheduling guarantee is implemented here.
"""
from dataclasses import dataclass
import hashlib
import hmac
import math
import struct


class InvalidInput(ValueError):
    pass


def finite(*values):
    if not all(math.isfinite(v) for v in values):
        raise InvalidInput("non-finite value")


def clamp(x, low, high):
    finite(x, low, high)
    return max(low, min(high, x))


def normalized(q):
    if len(q) != 4:
        raise InvalidInput("quaternion must be w,x,y,z")
    finite(*q)
    length = math.hypot(*q)
    if length < 1e-8 or not math.isfinite(length):
        raise InvalidInput("invalid quaternion norm")
    return tuple(v / length for v in q)


def qmul(a, b):
    w, x, y, z = a
    v, i, j, k = b
    return (w*v-x*i-y*j-z*k, w*i+x*v+y*k-z*j,
            w*j-x*k+y*v+z*i, w*k+x*j-y*i+z*v)


def relative_twist_deg(center, current):
    """Device-to-world active quaternions; positive is CCW viewed at screen.

    Extract twist about the calibrated device's +Z, using inverse(q0)*q.
    Production must separately enforce the allowed holding/swing envelope.
    """
    w, x, y, z = normalized(center)
    relative = normalized(qmul((w, -x, -y, -z), normalized(current)))
    tw, tz = relative[0], relative[3]
    if math.hypot(tw, tz) < 1e-6:
        raise InvalidInput("twist undefined at a 180-degree off-axis swing")
    return (math.degrees(2 * math.atan2(tz, tw)) + 180) % 360 - 180


def steering_from_angle(angle_deg, half_range_deg=90.0, sign=-1.0,
                        deadzone_deg=0.5):
    finite(angle_deg, half_range_deg, sign, deadzone_deg)
    if half_range_deg <= deadzone_deg or deadzone_deg < 0 or sign not in (-1, 1):
        raise InvalidInput("invalid steering calibration")
    magnitude = max(0.0, abs(angle_deg) - deadzone_deg)
    value = min(1.0, magnitude / (half_range_deg - deadzone_deg))
    return sign * math.copysign(value, angle_deg)


def low_pass(previous, current, dt_ms, tau_ms=8.0):
    finite(previous, current, dt_ms, tau_ms)
    if dt_ms <= 0 or dt_ms > 100 or tau_ms < 0:
        raise InvalidInput("invalid sample interval or time constant")
    if tau_ms == 0:
        return current
    return previous + (-math.expm1(-dt_ms / tau_ms)) * (current - previous)


def quantize_stick(value):
    value = clamp(value, -1.0, 1.0)
    scale = 32768 if value < 0 else 32767
    magnitude = math.floor(abs(value) * scale + 0.5)
    return -magnitude if value < 0 else magnitude


def quantize_trigger(value):
    return math.floor(clamp(value, 0.0, 1.0) * 255 + 0.5)


@dataclass(frozen=True)
class Controls:
    steer: float = 0.0
    throttle: float = 0.0
    brake: float = 0.0
    buttons: int = 0

    def validate(self):
        finite(self.steer, self.throttle, self.brake)
        if not (-1 <= self.steer <= 1 and 0 <= self.throttle <= 1
                and 0 <= self.brake <= 1):
            raise InvalidInput("control outside normalized range")
        if not isinstance(self.buttons, int) or self.buttons < 0 or self.buttons & ~0xF3FF:
            raise InvalidInput("reserved XInput button bits")
        return self

    def is_neutral(self):
        return (abs(self.steer) <= 0.05 and self.throttle == 0
                and self.brake == 0 and self.buttons == 0)

    def xinput(self):
        self.validate()
        # LX, LT (brake), RT (throttle), buttons. Other axes are always zero.
        return (quantize_stick(self.steer), quantize_trigger(self.brake),
                quantize_trigger(self.throttle), self.buttons)


class Pedal:
    """One pointer owns one pedal until release/cancel; moving up increases it."""
    def __init__(self, travel_px=72.0):
        finite(travel_px)
        if travel_px <= 0:
            raise InvalidInput("travel must be positive")
        self.travel = travel_px
        self.pointer = None
        self.anchor_y = 0.0
        self.value = 0.0

    def down(self, pointer_id, y):
        finite(y)
        if self.pointer is not None:
            return False
        self.pointer, self.anchor_y, self.value = pointer_id, y, 0.0
        return True

    def move(self, pointer_id, y):
        finite(y)
        if pointer_id == self.pointer and self.pointer is not None:
            self.value = clamp((self.anchor_y - y) / self.travel, 0.0, 1.0)
        return self.value

    def up(self, pointer_id):
        if pointer_id == self.pointer:
            self.cancel()

    def cancel(self):
        self.pointer, self.value = None, 0.0


# PWR1, little endian. Payload length excludes the 32-byte header and MAC.
HEADER = struct.Struct("<4sBBHQIQI")
CONTROL = struct.Struct("<fffHHI")
TAG_SIZE = 16
ARM = 0x1
READY = 0xE  # sensor valid, activity foreground, touch subsystem ready


@dataclass(frozen=True)
class ControlFrame:
    session: int
    seq: int
    sent_us: int
    ack_seq: int
    controls: Controls
    flags: int = READY
    calibration_epoch: int = 1


def encode_control(frame, key):
    frame.controls.validate()
    if len(key) != 32 or frame.flags & ~0xF:
        raise InvalidInput("bad key length or flags")
    c = frame.controls
    payload = CONTROL.pack(c.steer, c.throttle, c.brake, c.buttons,
                           frame.flags, frame.calibration_epoch)
    data = HEADER.pack(b"PWR1", 1, 1, len(payload), frame.session,
                       frame.seq, frame.sent_us, frame.ack_seq) + payload
    return data + hmac.new(key, data, hashlib.sha256).digest()[:TAG_SIZE]


def decode_control(data, key, expected_session):
    if len(key) != 32 or len(data) != HEADER.size + CONTROL.size + TAG_SIZE:
        raise InvalidInput("bad key or datagram size")
    body, tag = data[:-TAG_SIZE], data[-TAG_SIZE:]
    expected = hmac.new(key, body, hashlib.sha256).digest()[:TAG_SIZE]
    if not hmac.compare_digest(tag, expected):
        raise InvalidInput("authentication failed")
    magic, version, kind, length, session, seq, sent, ack = HEADER.unpack_from(body)
    if (magic, version, kind, length) != (b"PWR1", 1, 1, CONTROL.size):
        raise InvalidInput("unknown protocol or payload")
    if session != expected_session:
        raise InvalidInput("wrong session")
    steer, throttle, brake, buttons, flags, epoch = CONTROL.unpack_from(body, HEADER.size)
    if flags & ~0xF:
        raise InvalidInput("reserved flags")
    controls = Controls(steer, throttle, brake, buttons).validate()
    return ControlFrame(session, seq, sent, ack, controls, flags, epoch)


def newer_u32(candidate, previous):
    if not (0 <= candidate <= 0xFFFFFFFF and 0 <= previous <= 0xFFFFFFFF):
        raise InvalidInput("sequence outside uint32")
    distance = (candidate - previous) & 0xFFFFFFFF
    return 0 < distance < 0x80000000


class SafetyGate:
    """Reference state machine, with receiver-clock challenge freshness.

    note_server_send() records authenticated PC status/haptic sequence numbers.
    Phone echoes the most recent received PC sequence in control.ack_seq.
    tick() MUST also run during total input silence in the production worker.
    """
    def __init__(self, session, key, timeout_ms=150.0, challenge_ms=100.0):
        if len(key) != 32 or min(timeout_ms, challenge_ms) <= 0:
            raise InvalidInput("invalid safety configuration")
        self.session, self.key = session, key
        self.timeout_ms, self.challenge_ms = timeout_ms, challenge_ms
        self.sent_history = {}
        self.last_seq = self.last_rx = self.epoch = None
        self.armed = False
        self.previous_arm = False
        self.neutral_since = None
        self.output = Controls()

    def note_server_send(self, seq, now_ms):
        finite(now_ms)
        self.sent_history = {s: t for s, t in self.sent_history.items()
                             if 0 <= now_ms-t <= self.challenge_ms}
        self.sent_history[seq] = now_ms

    def disarm(self):
        self.armed = self.previous_arm = False
        self.neutral_since = None
        self.output = Controls()

    def tick(self, now_ms):
        finite(now_ms)
        if self.last_rx is None or now_ms - self.last_rx >= self.timeout_ms:
            self.disarm()
        return self.output

    def ingest(self, data, now_ms):
        self.tick(now_ms)
        try:
            f = decode_control(data, self.key, self.session)
        except (InvalidInput, struct.error):
            return False
        if self.last_seq is not None and not newer_u32(f.seq, self.last_seq):
            return False
        challenge_sent = self.sent_history.get(f.ack_seq)
        if challenge_sent is None or not 0 <= now_ms-challenge_sent <= self.challenge_ms:
            return False
        self.last_seq, self.last_rx = f.seq, now_ms
        if f.calibration_epoch != self.epoch:
            self.disarm()
            self.epoch = f.calibration_epoch
        if f.flags & READY != READY:
            self.disarm()
            return True
        intent = bool(f.flags & ARM)
        neutral = f.controls.is_neutral()
        if not intent:
            self.armed = False
            if neutral:
                if self.neutral_since is None:
                    self.neutral_since = now_ms
            else:
                self.neutral_since = None
        elif not self.previous_arm:
            self.armed = (neutral and self.neutral_since is not None
                          and now_ms-self.neutral_since >= 300.0)
            self.neutral_since = None
        self.previous_arm = intent
        self.output = f.controls if self.armed else Controls()
        return True


class HapticArbiter:
    # Each event is an observation, not an assertion of predicted grip loss.
    NONE, LOCK, SPIN, GRIP, COLLISION, CURB, SHIFT, ENGINE = range(8)
    PRIORITY = {LOCK: 100, SPIN: 90, GRIP: 80, COLLISION: 60,
                CURB: 30, SHIFT: 10, ENGINE: 0}

    def __init__(self, enabled=None):
        self.enabled = {self.LOCK, self.SPIN} if enabled is None else set(enabled)
        self.event = self.NONE
        self.level = 0
        self.deadline = 0.0
        self.pattern_started = None

    def stop(self):
        self.event, self.level, self.pattern_started = self.NONE, 0, None

    def tick(self, now_ms):
        finite(now_ms)
        if now_ms >= self.deadline:
            self.stop()
        return self.event

    def offer(self, event, level, now_ms, source_age_ms=0.0, lease_ms=100.0):
        finite(now_ms, source_age_ms, lease_ms)
        self.tick(now_ms)
        if not 0 <= source_age_ms <= 100 or not 0 < lease_ms <= 150:
            return "dropped"
        if event == self.NONE:
            self.stop()
            return "stopped"
        if event not in self.enabled or event not in self.PRIORITY or level not in (1, 2, 3):
            return "dropped"
        if self.event and self.PRIORITY[event] < self.PRIORITY[self.event]:
            return "dropped"  # No queue to replay after a critical event.
        same_event = event == self.event
        self.event, self.level, self.deadline = event, level, now_ms + lease_ms
        if not same_event:
            self.pattern_started = now_ms
        return "refreshed" if same_event else "started"


F1_HEADER = struct.Struct("<H5BQf2I2B")


def parse_f1_2025_motion_ex(data):
    """Small reader for official F1 25 v3 Motion Ex, not the 2026 parser.

    The fixture used by tests is synthetic. Session handling, live/replay state,
    inter-packet alignment and slip interpretation belong in the game adapter.
    """
    if len(data) != 273:
        raise InvalidInput("unexpected F1 2025 Motion Ex length")
    fields = F1_HEADER.unpack_from(data)
    fmt, year, major, minor, version, kind, session, seconds, frame, overall, player, secondary = fields
    if (fmt, version, kind) != (2025, 1, 13) or not 0 <= player < 22:
        raise InvalidInput("unsupported F1 format/version/id/player")
    if secondary != 255 and not 0 <= secondary < 22:
        raise InvalidInput("invalid secondary player")
    floats = struct.unpack_from("<61f", data, F1_HEADER.size)
    finite(seconds, *floats)
    if seconds < 0:
        raise InvalidInput("invalid session time")
    # 3 preceding suspension arrays, then wheel speed, slip ratio, slip angle.
    wheels = ("RL", "RR", "FL", "FR")
    return {"session": session, "frame": frame, "overall_frame": overall,
            "player": player, "game_year": year,
            "wheel_speed_raw": dict(zip(wheels, floats[12:16])),
            "slip_ratio_raw": dict(zip(wheels, floats[16:20]))}
