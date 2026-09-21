"""Local synthetic Wendy + controller concurrency checks. No phone, game or driver.

Measures process CPU time/working set and PWR1 ACK RTT, not input-to-game latency.
"""
import argparse
import base64
import ctypes
from ctypes import wintypes
import hashlib
import hmac
import json
from pathlib import Path
import secrets
import socket
import statistics
import struct
import subprocess
import threading
import time
from pwr1_mock_phone import encode, decode_status, CONTROL


def exact(sock, count):
    data = b""
    while len(data) < count:
        part = sock.recv(count - len(data))
        if not part:
            raise EOFError()
        data += part
    return data


def connect(port):
    until = time.monotonic() + 5
    while True:
        try:
            return socket.create_connection(("127.0.0.1", port), timeout=1)
        except OSError:
            if time.monotonic() >= until:
                raise
            time.sleep(.05)


def packet(kind, frame):
    sizes = {1: 753, 2: 1285, 7: 1239, 10: 1041}
    data = bytearray(sizes[kind])
    struct.pack_into("<HBBBBBQfIIBB", data, 0, 2025, 25, 1, 0, 1, kind, 1234, 10 + frame / 60, frame, frame, 2, 255)
    if kind == 1:
        data[29] = 4
    elif kind == 2:
        for car, position in [(0, 1), (1, 3), (2, 2)]:
            offset = 29 + car * 57
            data[offset + 32] = position
            data[offset + 33] = 6
            data[offset + 45] = 2
            struct.pack_into("<H", data, offset + 14, 1800 if car == 2 else 2500)
    elif kind == 7:
        offset = 29 + 2 * 55
        data[offset + 3] = 54
        data[offset + 28] = 3
        struct.pack_into("<fff", data, offset + 5, 20, 100, 1.5)
        struct.pack_into("<f", data, offset + 37, 2_000_000)
    else:
        offset = 29 + 2 * 46
        struct.pack_into("<ffff", data, offset, 22, 23, 38, 25)
        data[offset + 28] = 8
    return data


class ProcessMetrics:
    class Counters(ctypes.Structure):
        _fields_ = [("cb", wintypes.DWORD), ("faults", wintypes.DWORD)] + [(name, ctypes.c_size_t) for name in ["peak_ws", "ws", "peak_pool", "pool", "peak_nonpaged", "nonpaged", "pagefile", "peak_pagefile"]]

    def __init__(self, pid):
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel.OpenProcess.restype = wintypes.HANDLE
        self.kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        self.kernel.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
        self.kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        self.psapi = ctypes.WinDLL("psapi")
        self.psapi.GetProcessMemoryInfo.argtypes = [wintypes.HANDLE, ctypes.POINTER(self.Counters), wintypes.DWORD]
        self.handle = self.kernel.OpenProcess(0x0410, False, pid)
        assert self.handle

    def read(self):
        fields = [wintypes.FILETIME() for _ in range(4)]
        assert self.kernel.GetProcessTimes(self.handle, *[ctypes.byref(f) for f in fields])
        cpu = sum((f.dwHighDateTime << 32) + f.dwLowDateTime for f in fields[2:]) / 1e7
        info = self.Counters()
        info.cb = ctypes.sizeof(info)
        assert self.psapi.GetProcessMemoryInfo(self.handle, ctypes.byref(info), info.cb)
        return cpu, info.ws / 1024**2

    def close(self):
        self.kernel.CloseHandle(self.handle)


class Channel:
    def __init__(self, port, key):
        self.sock = connect(port)
        self.sock.settimeout(3)
        self.nonce = exact(self.sock, 32)
        self.key = key
        self.sequence = 0

    def send(self, text="", state="PROCESSING", tamper=False, allow_alerts=False):
        self.sequence += 1
        request = json.dumps({"state": state, "text": text, "confidence": .99, "allowAlerts": allow_alerts}, separators=(",", ":")).encode()
        body = struct.pack(">Q", self.sequence) + request
        tag = hmac.new(self.key, b"WDY1" + self.nonce + b"\0" + body, hashlib.sha256).digest()
        if tamper:
            tag = bytes([tag[0] ^ 1]) + tag[1:]
        self.sock.sendall(struct.pack(">I", len(body) + 32) + body + tag)
        length, = struct.unpack(">I", exact(self.sock, 4))
        assert 42 <= length <= 2088
        reply = exact(self.sock, length)
        assert hmac.compare_digest(reply[-32:], hmac.new(self.key, b"WDY1" + self.nonce + b"\1" + reply[:-32], hashlib.sha256).digest())
        assert struct.unpack_from(">Q", reply)[0] == self.sequence
        return json.loads(reply[8:-32])

    def close(self):
        self.sock.close()

    def ask(self, text):
        result = self.send(text)
        deadline = time.monotonic() + 19
        while not result["text"] and time.monotonic() < deadline:
            result = self.send()
        assert result["text"], ("voice response timeout", text)
        return result


def run(receiver, enabled):
    key = secrets.token_bytes(32)
    session = secrets.randbits(64) or 1
    control_port, engineer_port, telemetry_port = 26864, 26865, 20878
    args = [str(receiver), "--usb", "--usb-test", "--port", str(control_port), "--backend", "null",
            "--session-hex", f"{session:X}", "--key-base64", base64.b64encode(key).decode(),
            "--engineer-port", str(engineer_port), "--telemetry-port", str(telemetry_port), "--run-seconds", "100"]
    if enabled:
        args.append("--engineer")
    process = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    stop = threading.Event()
    telemetry_stop = threading.Event()
    errors, rtts, states = [], [], []
    shared = {"peer": 0, "arm": False}
    sent = {}
    sock = channel = metrics = None
    threads = []
    try:
        sock = connect(control_port)
        sock.settimeout(1)
        sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        hello = encode(0, session, 1, 0, b"", key)
        sock.sendall(struct.pack(">H", len(hello)) + hello)
        peer, _, _, _ = decode_status(exact(sock, struct.unpack(">H", exact(sock, 2))[0]), session, key)
        shared["peer"] = peer

        def receive():
            try:
                while not stop.is_set():
                    peer, ack, state, _ = decode_status(exact(sock, struct.unpack(">H", exact(sock, 2))[0]), session, key)
                    shared["peer"] = peer
                    states.append(state)
                    stamp = sent.get(ack)
                    if stamp is not None:
                        rtts.append((time.perf_counter() - stamp) * 1000)
            except (OSError, EOFError):
                if not stop.is_set():
                    errors.append("controller receive failed")

        def transmit():
            sequence = 1
            start = time.monotonic()
            try:
                while not stop.is_set():
                    sequence += 1
                    armed = time.monotonic() - start > .5
                    driving = time.monotonic() - start > .8
                    payload = CONTROL.pack(.2 if driving else 0, .3 if driving else 0, .4 if driving else 0, 0, 0xE | int(armed), 1)
                    kind = 4 if driving else 1
                    if driving:
                        payload += struct.pack("<ff", .5, -.25)
                    framed = encode(kind, session, sequence, shared["peer"], payload, key)
                    sent[sequence] = time.perf_counter()
                    sent.pop(sequence - 512, None)
                    sock.sendall(struct.pack(">H", len(framed)) + framed)
                    time.sleep(1/120)
            except OSError:
                if not stop.is_set():
                    errors.append("controller send failed")

        def telemetry():
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
                frame = 0
                while not telemetry_stop.is_set():
                    frame += 1
                    for kind in (1, 2, 7, 10):  # Stress: every kind at 60 Hz, higher than game's session/damage rate.
                        udp.sendto(packet(kind, frame), ("127.0.0.1", telemetry_port))
                    time.sleep(1/60)

        for fn in (receive, transmit):
            thread = threading.Thread(target=fn, daemon=True); thread.start(); threads.append(thread)
        if enabled:
            thread = threading.Thread(target=telemetry, daemon=True); thread.start(); threads.append(thread)
            channel = Channel(engineer_port, key)
        else:
            try:
                check = socket.create_connection(("127.0.0.1", engineer_port), timeout=.1)
                check.close()
                raise AssertionError("OFF must not expose Wendy listener")
            except (ConnectionRefusedError, TimeoutError):
                pass
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
                udp.bind(("127.0.0.1", telemetry_port))  # OFF must not bind telemetry.
        time.sleep(1.2)
        metrics = ProcessMetrics(process.pid)
        initial_cpu, _ = metrics.read()
        start = time.monotonic()
        memory = []
        while time.monotonic() - start < 6:
            if channel:
                response = channel.send()
                assert response["telemetry"] and response["flag"] == "YELLOW", response
            else:
                time.sleep(.2)
            memory.append(metrics.read()[1])
        elapsed = time.monotonic() - start
        final_cpu, _ = metrics.read()
        assert not errors, errors
        assert states and all(state == 1 for state in states[-100:]), "controller disarmed under load"
        result = {"engineer": enabled, "duration_s": round(elapsed, 2), "cpu_one_core_percent": round((final_cpu - initial_cpu) / elapsed * 100, 2),
                  "working_set_MiB": round(statistics.mean(memory), 2), "ack_rtt_p95_ms": round(sorted(rtts)[int(len(rtts)*.95)], 2), "status_count": len(states)}
        if channel:
            queries = {"How are my tyres?": "38 percent", "What's the gap ahead?": "1.8 seconds", "What's the gap behind?": "2.5 seconds",
                       "How much fuel do I have?": "20.0 kilograms", "What's my ERS?": "2.0 megajoules", "Any damage?": "8 percent",
                       "What's the weather?": "heavy rain", "What flag is out?": "Yellow flag", "What lap am I on?": "lap 6", "What's my position?": "P 2"}
            for query, expected in queries.items():
                response = channel.ask(query)
                assert expected in response["text"], (query, response)
            assert channel.ask("Set brake bias to 54")["kind"] == "unsupported"
            assert channel.ask("Set differential to 55")["kind"] == "unsupported"
            planned = channel.ask("Box this lap")
            assert planned["kind"] == "plan" and "cannot send" in planned["text"]
            assert channel.ask("Stay out")["kind"] == "plan"
            assert "pit" in channel.ask("Do I need pit in?")["text"].lower()
            assert channel.ask("The rear feels loose when I get back on the power")["text"].startswith("Feedback noted")
            assert not errors and all(state == 1 for state in states[-100:]), "controller disarmed during CPU inference"
            result["ack_rtt_p95_including_inference_ms"] = round(sorted(rtts)[int(len(rtts) * .95)], 2)
            assert channel.send(state="LISTENING")["text"] == "", "PTT must not be interrupted"
            alert = channel.send(state="LISTENING", allow_alerts=True)
            assert alert["kind"] == "alert" and alert["text"] == "Yellow flag."
            assert channel.send(state="IDLE")["text"] == "", "duplicate alert"
            try:
                channel.send(tamper=True)
                raise AssertionError("accepted invalid MAC")
            except (EOFError, ConnectionResetError):
                pass
            old_nonce = channel.nonce
            channel.close(); channel = Channel(engineer_port, key)
            assert channel.nonce != old_nonce, "reconnect nonce reused"
            assert channel.ask("What lap am I on?")["text"] == "You are on lap 6."
            telemetry_stop.set()
            until = time.monotonic() + 3.2
            while time.monotonic() < until:
                channel.send()  # Keep voice heartbeat alive while telemetry expires.
            response = channel.ask("What's the weather?")
            assert not response["telemetry"] and response["flag"] == "UNKNOWN" and "unavailable" in response["text"], response
        print(json.dumps(result))
        return result
    finally:
        stop.set(); telemetry_stop.set()
        if channel:
            channel.close()
        if sock:
            sock.close()
        for thread in threads:
            thread.join(timeout=2)
        if metrics:
            metrics.close()
        if process.poll() is None:
            process.terminate()
        _, stderr = process.communicate(timeout=5)
        if stderr:
            print(stderr.decode(errors="replace"))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--receiver", type=Path, default=Path(__file__).resolve().parents[1] / "release/receiver/PhoneWheel.Receiver.exe")
    args = parser.parse_args()
    run(args.receiver, False)
    run(args.receiver, True)
    print("PASS Wendy: all 10 queries, command exclusions, proactive cooldown, HMAC rejection, reconnect, stale data, simultaneous controller; synthetic loopback only")
