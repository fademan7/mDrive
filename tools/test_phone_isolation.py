"""Explicit Fold5/Wi-Fi integration test against a null-output receiver only.

Requires the development instrumentation APK. Stops only its own child receiver.
Microphone use is opt-in; uses one English TTS phrase and UI option cycles.
"""
import argparse
import base64
from pathlib import Path
import secrets
import socket
import subprocess
import time
import threading
from collections import deque

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--host", required=True)
parser.add_argument("--serial", required=True, help="Authorized test phone serial from adb devices")
parser.add_argument("--microphone", action="store_true", help="Explicitly authorized live microphone start/stop test")
args = parser.parse_args()
adb = str(root / ".tools/android-sdk/platform-tools/adb.exe")
receiver = str(root / "release/receiver/PhoneWheel.Receiver.exe")
key = base64.b64encode(secrets.token_bytes(32)).decode()
session = f"{secrets.randbits(64) or 1:X}"
process = subprocess.Popen([receiver, "--wifi", "--monitor", "--bind", args.host, "--port", "26760", "--backend", "null",
                            "--engineer", "--session-hex", session, "--key-base64", key, "--run-seconds", "100"],
                           stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
# Keep only health metadata, never pairing keys or speech transcripts.
health = deque(maxlen=12)
def read_health():
    for raw in process.stdout:
        line = raw.decode("utf-8", errors="replace").strip()
        if line.startswith(("Stops ", "Wi-Fi · ")):
            health.append(line)
reader = threading.Thread(target=read_health, daemon=True)
reader.start()
try:
    until = time.monotonic() + 5
    while True:
        try:
            with socket.create_connection((args.host, 26762), timeout=.5):
                break
        except OSError:
            if process.poll() is not None or time.monotonic() > until:
                raise RuntimeError("Null test receiver did not start; check occupied ports")
            time.sleep(.1)
    completed = subprocess.run([adb, "-s", args.serial, "shell", "am", "instrument", "-w",
                                "-e", "nullBackend", "true", "-e", "microphone", str(args.microphone).lower(), "-e", "host", args.host,
                                "-e", "session", session, "-e", "key", key,
                                "dev.phonewheel.test/dev.phonewheel.ControllerIsolationInstrumentation"],
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", errors="replace", timeout=85)
    print(completed.stdout)
    time.sleep(.3)
    print("Receiver health:\n" + "\n".join(health))
    if "PASS Wi-Fi ACKs" not in completed.stdout or "failure=" in completed.stdout:
        raise RuntimeError("Phone isolation test failed; see instrumentation result")
finally:
    if process.poll() is None:
        process.terminate()
    process.wait(timeout=5)
