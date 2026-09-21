"""Loopback-only USB framing/safety integration test (no physical device)."""
import base64
import argparse
import csv
from pathlib import Path
import socket
import struct
import subprocess
import time
from pwr1_mock_phone import encode, decode_status, CONTROL, KEY_DEFAULT, SESSION_DEFAULT


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser()
    parser.add_argument("--receiver", type=Path, default=root / "release/receiver/PhoneWheel.Receiver.exe")
    parser.add_argument("--port", type=int, default=26866)
    options = parser.parse_args()
    output = root / "artifacts/validation/usb-tcp-watchdog.csv"
    args = [str(options.receiver), "--usb", "--usb-test",
            "--backend", "csv", "--csv", str(output), "--port", str(options.port), "--run-seconds", "8",
            "--session-hex", f"{SESSION_DEFAULT:016X}", "--key-base64", base64.b64encode(KEY_DEFAULT).decode()]
    process = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    try:
        until = time.monotonic() + 3
        while True:
            try:
                sock = socket.create_connection(("127.0.0.1", options.port), timeout=.5)
                break
            except OSError:
                if time.monotonic() > until:
                    raise
                time.sleep(.02)
        with sock:
            # Malformed first frame must close without authenticating a peer.
            sock.sendall(struct.pack(">H", 77))
            assert sock.recv(1) == b""
        with socket.create_connection(("127.0.0.1", options.port), timeout=1) as sock:
            sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

            def read_exact(size):
                data = b""
                while len(data) < size:
                    part = sock.recv(size - len(data))
                    if not part:
                        raise EOFError("USB stream closed")
                    data += part
                return data

            def status():
                length = struct.unpack(">H", read_exact(2))[0]
                return decode_status(read_exact(length), SESSION_DEFAULT, KEY_DEFAULT)

            def send(packet):
                sock.sendall(struct.pack(">H", len(packet)) + packet)

            hello = encode(0, SESSION_DEFAULT, 1, 0, b"", KEY_DEFAULT)
            framed = struct.pack(">H", len(hello)) + hello
            for byte in framed:  # Fragmented prefix and packet body.
                sock.sendall(bytes([byte]))
            peer, _, _, _ = status()
            sequence = 1

            def control(arm=False, throttle=0.0, brake=0.0):
                nonlocal sequence, peer
                sequence += 1
                send(encode(1, SESSION_DEFAULT, sequence, peer,
                            CONTROL.pack(0.0, throttle, brake, 0, 0xE | int(arm), 1), KEY_DEFAULT))
                sent_ns = time.perf_counter_ns()
                peer, _, state, _ = status()
                return sent_ns, state

            for _ in range(24):
                control()
            _, armed = control(True)
            assert armed == 1, "did not arm after neutral dwell"
            for _ in range(3):
                last_ns, armed = control(True, 1, 1)
                assert armed == 1
            # Leave a partial frame outstanding: read waits, watchdog must not.
            sock.sendall(struct.pack(">H", 68) + b"PWR1")
            time.sleep(.3)
        # New TCP source port, same authenticated session with increasing sequence.
        with socket.create_connection(("127.0.0.1", options.port), timeout=1) as sock:
            sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            sequence += 1
            send(encode(0, SESSION_DEFAULT, sequence, 0, b"", KEY_DEFAULT))
            peer, _, state, _ = status()
            assert state == 0, "reconnect must release output"
            _, state = control(True, 1, 1)
            assert state == 0, "held throttle/ARM must not resume after reconnect"
            for _ in range(24):
                control()
            _, state = control(True)
            assert state == 1, "reconnect did not rearm after neutral dwell"
        _, error = process.communicate(timeout=7)
        assert process.returncode == 0, error.decode(errors="replace")
        with output.open(newline="", encoding="utf-8-sig") as file:
            rows = list(csv.DictReader(file))
        assert any(int(row["lt"]) == 255 and int(row["rt"]) == 255 for row in rows)
        neutral = next(row for row in rows if int(row["pc_monotonic_ns"]) >= last_ns
                       and int(row["lt"]) == 0 and int(row["rt"]) == 0)
        elapsed_ms = (int(neutral["pc_monotonic_ns"]) - last_ns) / 1e6
        assert 140 <= elapsed_ms <= 170, f"watchdog outside measured tolerance: {elapsed_ms:.3f} ms"
        print(f"PASS USB TCP: invalid length, fragmented Hello, both triggers, partial-frame watchdog {elapsed_ms:.3f} ms, new-socket reconnect, held-input blocked, neutral rearm")
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=5)


if __name__ == "__main__":
    main()
