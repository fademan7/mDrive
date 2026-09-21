"""Bounded loopback UDP reconnect test; not a radio stability measurement."""
import base64
import argparse
import secrets
import socket
import subprocess
import time
from pathlib import Path
from pwr1_mock_phone import encode, decode_status, CONTROL


def main():
    root = Path(__file__).resolve().parents[1]
    key = secrets.token_bytes(32)
    session = secrets.randbits(64) or 1
    host = ("127.0.0.1", 26763)
    parser = argparse.ArgumentParser()
    parser.add_argument("--receiver", type=Path, default=root / "release/receiver/PhoneWheel.Receiver.exe")
    options = parser.parse_args()
    args = [str(options.receiver), "--wifi",
            "--bind", host[0], "--port", str(host[1]), "--backend", "null",
            "--session-hex", f"{session:X}", "--key-base64", base64.b64encode(key).decode(),
            "--run-seconds", "7"]
    process = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    seq = 0
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as first, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as second:
            for sock in (first, second):
                sock.bind((host[0], 0)); sock.settimeout(.2)

            def hello(sock):
                nonlocal seq
                seq += 1
                sock.sendto(encode(0, session, seq, 0, b"", key), host)

            def status(sock, ack=None):
                until = time.monotonic() + 1
                while time.monotonic() < until:
                    header_seq, echoed, state, reason = decode_status(sock.recv(128), session, key)
                    if ack is None or echoed == ack:
                        return header_seq, state
                raise AssertionError("no matching status ACK")

            def control(sock, peer, arm=False, throttle=0):
                nonlocal seq
                seq += 1
                sock.sendto(encode(1, session, seq, peer, CONTROL.pack(0, throttle, 0, 0, 14 | int(arm), 1), key), host)
                return status(sock, seq)

            until = time.monotonic() + 3
            while True:
                hello(first)
                try:
                    peer, _ = status(first); break
                except (socket.timeout, ConnectionResetError):
                    if time.monotonic() > until:
                        raise
                    time.sleep(.05)
            for _ in range(20):
                peer, _ = control(first, peer)
            peer, state = control(first, peer, True)
            assert state == 1
            peer, state = control(first, peer, True, 1)
            assert state == 1
            # A different source port must not steal a currently fresh endpoint.
            second.settimeout(.06)
            hello(second)
            try:
                second.recv(128)
                raise AssertionError("active endpoint replaced")
            except socket.timeout:
                pass
            # No input for longer than the unchanged watchdog deadline.
            time.sleep(.2)
            second.settimeout(.5)
            hello(second)
            peer, state = status(second)
            assert state == 0
            peer, state = control(second, peer, True, 1)
            assert state == 0, "held throttle resumed"
            for _ in range(20):
                peer, _ = control(second, peer)
            _, state = control(second, peer, True)
            assert state == 1
        _, error = process.communicate(timeout=9)
        assert process.returncode == 0, error.decode(errors="replace")
        print("PASS UDP: active endpoint protected, stale endpoint reauthenticated, held throttle blocked, neutral rearm")
    finally:
        if process.poll() is None:
            process.terminate(); process.wait(timeout=5)


if __name__ == "__main__":
    main()
