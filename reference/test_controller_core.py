"""Requirements-based tests with fixed expected values and adversarial inputs."""
import hashlib
import hmac
import math
import random
import struct
import unittest

from reference.controller_core import (
    ARM, READY, CONTROL, HEADER, Controls, ControlFrame, HapticArbiter,
    InvalidInput, Pedal, SafetyGate, decode_control, encode_control, low_pass,
    newer_u32, parse_f1_2025_motion_ex, qmul, quantize_stick, quantize_trigger,
    relative_twist_deg, steering_from_angle,
)

KEY = bytes(range(32))  # Public test key. Never use this key with a real device.
SESSION = 0x0102030405060708


def rotation(axis, degrees):
    half = math.radians(degrees) / 2
    result = [math.cos(half), 0.0, 0.0, 0.0]
    result[axis + 1] = math.sin(half)
    return tuple(result)


def resign(body):
    return bytes(body) + hmac.new(KEY, body, hashlib.sha256).digest()[:16]


class GeometryTests(unittest.TestCase):
    def test_known_angles_including_clockwise_sign(self):
        for angle in (-120, -90, -45, 0, 45, 90, 120):
            with self.subTest(angle=angle):
                result = relative_twist_deg((1, 0, 0, 0), rotation(2, angle))
                self.assertAlmostEqual(result, angle)
        self.assertEqual(steering_from_angle(-90), 1)
        self.assertEqual(steering_from_angle(90), -1)

    def test_noncommuting_center_uses_correct_multiplication_order(self):
        center = qmul(rotation(0, 35), rotation(1, 60))
        current = qmul(center, rotation(2, -45))
        self.assertAlmostEqual(relative_twist_deg(center, current), -45)

    def test_invariant_under_common_world_rotation(self):
        rng = random.Random(20260907)
        for _ in range(300):
            world = qmul(rotation(0, rng.uniform(-180, 180)),
                         rotation(1, rng.uniform(-180, 180)))
            angle = rng.uniform(-130, 130)
            self.assertAlmostEqual(relative_twist_deg(world, qmul(world, rotation(2, angle))), angle)

    def test_quaternion_sign_and_scale_do_not_change_orientation(self):
        q = rotation(2, 87)
        self.assertAlmostEqual(relative_twist_deg((1, 0, 0, 0), q), 87)
        self.assertAlmostEqual(relative_twist_deg((-2, 0, 0, 0), tuple(-3*v for v in q)), 87)

    def test_zero_and_singular_quaternions_rejected(self):
        for q in [(0, 0, 0, 0), (0, 1, 0, 0), (math.nan, 0, 0, 0)]:
            with self.subTest(q=q), self.assertRaises(InvalidInput):
                relative_twist_deg((1, 0, 0, 0), q)

    def test_deadzone_continuity_and_full_scale(self):
        self.assertEqual(steering_from_angle(0.5), 0)
        self.assertAlmostEqual(steering_from_angle(0.50001), -0.00001/89.5)
        self.assertEqual(steering_from_angle(120), -1)

    def test_filter_step_has_expected_time_constant(self):
        expected = 1 - math.exp(-1)
        self.assertAlmostEqual(low_pass(0, 1, 8, 8), expected)
        split = low_pass(low_pass(0, 1, 3, 8), 1, 5, 8)
        self.assertAlmostEqual(split, expected)
        self.assertEqual(low_pass(0, 1, 8, 0), 1)

    def test_filter_rejects_invalid_timestamps(self):
        for dt in (0, -1, 101, math.nan):
            with self.subTest(dt=dt), self.assertRaises(InvalidInput):
                low_pass(0, 1, dt)


class AnalogTests(unittest.TestCase):
    def test_fixed_xinput_mapping_and_simultaneous_pedals(self):
        self.assertEqual(Controls(-1, 1, 0.5).xinput(), (-32768, 128, 255, 0))
        self.assertEqual(Controls(1, 1, 1).xinput(), (32767, 255, 255, 0))
        self.assertEqual(Controls().xinput(), (0, 0, 0, 0))
        self.assertEqual(quantize_stick(0.5), 16384)
        self.assertEqual(quantize_stick(-0.5), -16384)

    def test_trigger_all_codes_and_error_bound(self):
        observed = set()
        for i in range(4097):
            value = i/4096
            code = quantize_trigger(value)
            observed.add(code)
            self.assertLessEqual(abs(code/255-value), 0.5/255 + 1e-12)
        self.assertEqual(observed, set(range(256)))

    def test_stick_monotonicity_and_bounds(self):
        codes = [quantize_stick(i/2048) for i in range(-2048, 2049)]
        self.assertEqual(codes, sorted(codes))
        self.assertEqual((min(codes), max(codes)), (-32768, 32767))

    def test_nonfinite_controls_rejected(self):
        for v in (math.nan, math.inf, -math.inf):
            with self.subTest(v=v), self.assertRaises(InvalidInput):
                Controls(v, 0, 0).validate()

    def test_touch_down_starts_at_zero_then_release_is_immediate(self):
        pedal = Pedal(100)
        pedal.down(7, 200)
        self.assertEqual(pedal.value, 0)
        self.assertEqual(pedal.move(7, 150), 0.5)
        self.assertEqual(pedal.move(7, 50), 1)
        pedal.up(7)
        self.assertEqual(pedal.value, 0)

    def test_pointer_reorder_cannot_steal_pedal(self):
        left, right = Pedal(100), Pedal(100)
        left.down(8, 200)
        right.down(3, 200)
        left.move(8, 100)
        right.move(3, 150)
        self.assertFalse(left.down(3, 0))
        left.up(3)
        self.assertEqual((left.value, right.value), (1, 0.5))
        left.cancel()
        self.assertEqual((left.value, right.value), (0, 0.5))


class ProtocolTests(unittest.TestCase):
    def frame(self):
        return ControlFrame(SESSION, 0x01020304, 0x0102030405060708,
                            0x0A0B0C0D, Controls(-0.5, 1, 0.5, 0x1000))

    def test_fixed_layout_and_byte_order(self):
        packet = encode_control(self.frame(), KEY)
        expected = bytes.fromhex(
            "50 57 52 31 01 01 14 00 08 07 06 05 04 03 02 01 "
            "04 03 02 01 08 07 06 05 04 03 02 01 0d 0c 0b 0a "
            "00 00 00 bf 00 00 80 3f 00 00 00 3f 00 10 0e 00 01 00 00 00")
        self.assertEqual((HEADER.size, CONTROL.size, len(packet)), (32, 20, 68))
        self.assertEqual(packet[:-16], expected)
        self.assertEqual(decode_control(packet, KEY, SESSION), self.frame())

    def test_every_single_bit_modification_rejected(self):
        original = encode_control(self.frame(), KEY)
        for bit in range(len(original)*8):
            changed = bytearray(original)
            changed[bit//8] ^= 1 << (bit % 8)
            with self.subTest(bit=bit), self.assertRaises(InvalidInput):
                decode_control(changed, KEY, SESSION)

    def test_all_truncations_and_trailing_data_rejected(self):
        original = encode_control(self.frame(), KEY)
        for length in range(68):
            with self.subTest(length=length), self.assertRaises(InvalidInput):
                decode_control(original[:length], KEY, SESSION)
        with self.assertRaises(InvalidInput):
            decode_control(original+b"\0", KEY, SESSION)

    def test_wrong_key_or_session_rejected(self):
        original = encode_control(self.frame(), KEY)
        for key, session in [(b"x"*32, SESSION), (KEY, SESSION+1)]:
            with self.subTest(session=session), self.assertRaises(InvalidInput):
                decode_control(original, key, session)

    def test_authenticated_but_semantically_invalid_packet_rejected(self):
        original = encode_control(self.frame(), KEY)
        changes = [(4, b"\x02"), (5, b"\x02"), (6, b"\x13"),
                   (32, struct.pack("<f", math.nan)),
                   (36, struct.pack("<f", 1.01)), (46, b"\x10")]
        for offset, value in changes:
            body = bytearray(original[:-16])
            body[offset:offset+len(value)] = value
            with self.subTest(offset=offset), self.assertRaises(InvalidInput):
                decode_control(resign(body), KEY, SESSION)

    def test_sequence_wrap_duplicates_and_half_range(self):
        self.assertTrue(newer_u32(0, 0xFFFFFFFF))
        self.assertFalse(newer_u32(9, 9))
        self.assertFalse(newer_u32(8, 9))
        self.assertFalse(newer_u32(0x80000000, 0))


class SafetyTests(unittest.TestCase):
    def setUp(self):
        self.gate = SafetyGate(SESSION, KEY)
        self.seq = 0

    def send(self, now, controls=Controls(), arm=False, ready=True, epoch=1):
        self.seq += 1
        self.gate.note_server_send(self.seq, now-1)
        frame = ControlFrame(SESSION, self.seq, 900_000_000_000+self.seq,
                             self.seq, controls, (READY if ready else 0) | (ARM if arm else 0), epoch)
        packet = encode_control(frame, KEY)
        accepted = self.gate.ingest(packet, now)
        return accepted, packet

    def arm(self, start=0):
        for t in range(start, start+301, 50):
            self.send(t)
        self.send(start+310, arm=True)
        self.assertTrue(self.gate.armed)

    def test_requires_neutral_dwell_and_rising_arm_edge(self):
        self.send(0, arm=True)
        self.assertFalse(self.gate.armed)
        self.send(10)
        self.send(100)
        self.send(200, arm=True)
        self.assertFalse(self.gate.armed)
        self.arm(250)

    def test_nonzero_throttle_cannot_arm(self):
        for t in range(0, 301, 50):
            self.send(t)
        self.send(310, Controls(0, 1, 0), arm=True)
        self.assertFalse(self.gate.armed)
        self.assertEqual(self.gate.output, Controls())

    def test_disconnect_timeout_and_no_automatic_rearm(self):
        self.arm()
        self.send(320, Controls(0.5, 1, 1), arm=True)
        self.assertEqual(self.gate.tick(469).throttle, 1)
        self.assertEqual(self.gate.tick(470), Controls())
        self.send(480, Controls(0, 1, 0), arm=True)
        self.assertFalse(self.gate.armed)
        self.arm(500)

    def test_replayed_packet_does_not_refresh_watchdog(self):
        self.arm()
        _, packet = self.send(320, Controls(0, 1, 0), arm=True)
        self.assertFalse(self.gate.ingest(packet, 400))
        self.assertEqual(self.gate.tick(470), Controls())

    def test_delayed_new_packet_rejected_by_receiver_clock_challenge(self):
        self.arm()
        self.gate.note_server_send(999, 315)
        frame = ControlFrame(SESSION, 100, 123, 999, Controls(0, 1, 0), READY | ARM)
        self.assertFalse(self.gate.ingest(encode_control(frame, KEY), 416))

    def test_unknown_acknowledgement_rejected(self):
        frame = ControlFrame(SESSION, 1, 123, 999, Controls())
        self.assertFalse(self.gate.ingest(encode_control(frame, KEY), 0))

    def test_background_and_recalibration_disarm_immediately(self):
        self.arm()
        self.send(320, Controls(0, 1, 0), arm=True, ready=False)
        self.assertEqual(self.gate.output, Controls())
        self.arm(400)
        self.send(720, Controls(0, 1, 0), arm=True, epoch=2)
        self.assertFalse(self.gate.armed)

    def test_phone_clock_offset_is_not_used_as_network_latency(self):
        self.arm()
        self.send(320, Controls(0, 0.5, 0.75), arm=True)
        self.assertEqual(self.gate.output.xinput(), (0, 191, 128, 0))


class HapticTests(unittest.TestCase):
    def test_engine_and_shift_disabled_by_default(self):
        a = HapticArbiter()
        self.assertEqual(a.offer(a.ENGINE, 3, 0), "dropped")
        self.assertEqual(a.offer(a.SHIFT, 3, 0), "dropped")

    def test_lock_preempts_curb_and_no_old_curb_is_replayed(self):
        a = HapticArbiter(enabled={1, 2, 5})
        a.offer(a.CURB, 1, 0)
        self.assertEqual(a.offer(a.LOCK, 3, 10), "started")
        self.assertEqual(a.offer(a.CURB, 1, 20), "dropped")
        self.assertEqual(a.tick(110), a.NONE)

    def test_refresh_does_not_restart_pattern(self):
        a = HapticArbiter()
        a.offer(a.SPIN, 1, 0)
        self.assertEqual(a.offer(a.SPIN, 2, 30), "refreshed")
        self.assertEqual(a.pattern_started, 0)
        self.assertEqual(a.tick(129), a.SPIN)
        self.assertEqual(a.tick(130), a.NONE)

    def test_stale_event_and_excessive_lease_are_dropped(self):
        a = HapticArbiter()
        self.assertEqual(a.offer(a.LOCK, 3, 200, source_age_ms=101), "dropped")
        self.assertEqual(a.offer(a.LOCK, 3, 200, lease_ms=151), "dropped")

    def test_explicit_stop_cancels_active_warning(self):
        a = HapticArbiter()
        a.offer(a.LOCK, 3, 0)
        self.assertEqual(a.offer(a.NONE, 0, 1), "stopped")
        self.assertEqual(a.tick(1), a.NONE)


class F1LayoutTests(unittest.TestCase):
    def fixture(self):
        # Literal header and manually placed diagnostic values, not a parser round trip.
        data = bytearray(273)
        data[:29] = bytes.fromhex(
            "e9 07 19 01 00 01 0d 01 00 00 00 00 00 00 00 "
            "00 00 80 3f 02 00 00 00 03 00 00 00 00 ff")
        struct.pack_into("<4f", data, 77, 11, 22, 33, 44)
        struct.pack_into("<4f", data, 93, -0.5, 0.25, -1, 0)
        return data

    def test_official_2025_offsets_and_wheel_order(self):
        parsed = parse_f1_2025_motion_ex(self.fixture())
        self.assertEqual(parsed["slip_ratio_raw"], {"RL": -0.5, "RR": 0.25, "FL": -1, "FR": 0})
        self.assertEqual(parsed["wheel_speed_raw"]["FL"], 33)
        self.assertEqual((parsed["session"], parsed["frame"], parsed["overall_frame"]), (1, 2, 3))

    def test_unknown_2026_format_cannot_silently_use_2025_parser(self):
        data = self.fixture()
        struct.pack_into("<H", data, 0, 2026)
        with self.assertRaises(InvalidInput):
            parse_f1_2025_motion_ex(data)

    def test_f1_bad_length_id_version_and_player_rejected(self):
        original = self.fixture()
        invalid = [original[:-1], original+b"\0"]
        for offset, value in [(5, 2), (6, 6), (27, 22)]:
            packet = bytearray(original)
            packet[offset] = value
            invalid.append(packet)
        for packet in invalid:
            with self.subTest(length=len(packet)), self.assertRaises(InvalidInput):
                parse_f1_2025_motion_ex(packet)

    def test_nonfinite_telemetry_rejected(self):
        data = self.fixture()
        struct.pack_into("<f", data, 93, math.nan)
        with self.assertRaises(InvalidInput):
            parse_f1_2025_motion_ex(data)


if __name__ == "__main__":
    unittest.main(verbosity=2)
