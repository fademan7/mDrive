"""Authorized device lifecycle test. Null output, no microphone or game commands."""
import argparse
import base64
from pathlib import Path
import secrets
import subprocess
import threading
import time
from collections import deque

root = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser()
p.add_argument('--host', required=True)
p.add_argument('--serial', required=True)
p.add_argument('--receiver', default='release/distribution-0514/PhoneWheel.Receiver.exe')
p.add_argument('--held-recovery', action='store_true')
a = p.parse_args()
key = base64.b64encode(secrets.token_bytes(32)).decode()
session = f'{secrets.randbits(64) or 1:X}'
health = deque(maxlen=15)
process = subprocess.Popen([str(root / a.receiver), '--wifi', '--monitor', '--backend', 'null',
    '--bind', a.host, '--port', '26760', '--session-hex', session, '--key-base64', key, '--run-seconds', '90'],
    stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
def read():
    for raw in process.stdout:
        line = raw.decode('utf-8', errors='replace').strip()
        if line.startswith(('Stops ', 'Wi-Fi · ')):
            health.append(line)
threading.Thread(target=read, daemon=True).start()
try:
    time.sleep(1)
    if process.poll() is not None:
        raise RuntimeError('Null receiver did not start; no test performed')
    adb = str(root / '.tools/android-sdk/platform-tools/adb.exe')
    instrument = subprocess.Popen([adb, '-s', a.serial,
        'shell', 'am', 'instrument', '-w', '-e', 'nullBackend', 'true', '-e', 'resumeOnly', 'true',
        '-e', 'heldRecovery', str(a.held_recovery).lower(),
        '-e', 'host', a.host, '-e', 'session', session, '-e', 'key', key,
        'dev.phonewheel.test/dev.phonewheel.ControllerIsolationInstrumentation'],
        stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding='utf-8', errors='replace')
    timer=threading.Timer(80,instrument.kill)
    timer.start()
    lines=[]
    try:
        for line in instrument.stdout:
            lines.append(line)
            if 'INSTRUMENTATION_STATUS: hostResumeRequested=true' in line:
                launch=subprocess.run([adb,'-s',a.serial,'shell','am','start','-f','0x10020000','-n','dev.phonewheel/.MainActivity'],
                    capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=10)
                print('Explicit app resume exit:',launch.returncode,flush=True)
        instrument.wait(timeout=5)
    finally:
        timer.cancel()
        if instrument.poll() is None:
            instrument.kill()
    output=''.join(lines)
    print(output)
    print('Null receiver health:\n' + '\n'.join(health))
    if 'PASS actual Wi-Fi' not in output or 'failure=' in output:
        raise RuntimeError('Lifecycle test failed; see result above')
finally:
    if process.poll() is None:
        process.terminate()
    process.wait(timeout=5)
