"""Authenticated PWR1 mock phone for local Host integration testing."""
import argparse
import base64
import hashlib
import hmac
import socket
import struct
import time

HEADER = struct.Struct("<4sBBHQIQI")
CONTROL = struct.Struct("<fffHHI")
SESSION_DEFAULT = 0x0102030405060708
KEY_DEFAULT = bytes(range(32))


def encode(kind, session, sequence, ack, payload, key):
    body = HEADER.pack(b"PWR1", 1, kind, len(payload), session, sequence,
                       time.monotonic_ns() // 1000, ack) + payload
    return body + hmac.new(key, body, hashlib.sha256).digest()[:16]


def decode_status(data, session, key):
    if len(data) != 52:
        raise ValueError("bad status size")
    body, tag = data[:-16], data[-16:]
    if not hmac.compare_digest(tag, hmac.new(key, body, hashlib.sha256).digest()[:16]):
        raise ValueError("bad status MAC")
    magic, version, kind, length, actual_session, sequence, sent, ack = HEADER.unpack_from(body)
    if (magic, version, kind, length, actual_session) != (b"PWR1", 1, 3, 4, session):
        raise ValueError("bad status header")
    state, reason, reserved = struct.unpack_from("<BBH", body, 32)
    if reserved or state not in (0, 1) or reason > 9:
        raise ValueError("bad status payload")
    return sequence, ack, state, reason


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=26760)
    parser.add_argument("--session-hex", default=f"{SESSION_DEFAULT:016X}")
    parser.add_argument("--key-base64", default=base64.b64encode(KEY_DEFAULT).decode())
    args = parser.parse_args()
    session, key = int(args.session_hex, 16), base64.b64decode(args.key_base64, validate=True)
    if session == 0 or len(key) != 32:
        parser.error("nonzero session and 32-byte key required")
    destination = (args.host, args.port)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.settimeout(0.5)
    sequence, peer = 1, 0
    sock.sendto(encode(0, session, sequence, peer, b"", key), destination)
    peer, ack, state, reason = decode_status(sock.recv(68), session, key)
    print(f"hello accepted: status_seq={peer} state={state} reason={reason}")

    def control(steer=0.0, throttle=0.0, brake=0.0, arm=False):
        nonlocal sequence, peer
        sequence = (sequence + 1) & 0xFFFFFFFF
        flags = 0xE | int(arm)
        payload = CONTROL.pack(steer, throttle, brake, 0, flags, 1)
        sock.sendto(encode(1, session, sequence, peer, payload, key), destination)
        sent_ns = time.perf_counter_ns()
        sock.settimeout(0.012)
        try:
            while True:
                peer, _, _, _ = decode_status(sock.recv(68), session, key)
        except socket.timeout:
            pass
        finally:
            sock.settimeout(0.5)
        return sent_ns

    start = time.monotonic()
    while time.monotonic() - start < 0.36:
        control()
        time.sleep(0.018)
    control(arm=True)
    print("armed rising edge sent after neutral dwell")
    last_control_ns = 0
    for _ in range(10):
        last_control_ns = control(throttle=1.0, brake=1.0, arm=True)
        time.sleep(0.006)
    print(f"simultaneous pedals sent; last_control_monotonic_ns={last_control_ns}; now silent for watchdog")
    time.sleep(0.30)
    sock.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
