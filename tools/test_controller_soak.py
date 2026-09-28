"""Explicitly authorized real-phone Wi-Fi/controller soak through a UDP fault proxy.
No microphone, Wendy, telemetry or live gamepad. Receiver is always --backend null.
No credentials, addresses or control values are saved in the report.
"""
import argparse
import base64
import heapq
import json
import re
from pathlib import Path
import secrets
import select
import socket
import subprocess
import threading
import time
from collections import deque

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--host', required=True)
p.add_argument('--serial', required=True)
p.add_argument('--seconds', type=int, default=600)
p.add_argument('--faults', action='store_true')
p.add_argument('--brief-outages', action='store_true', help='Also inject 160ms two-way loss every 10s in the jitter phase')
p.add_argument('--direct', action='store_true', help='Normal phone-to-Receiver path, no Python UDP proxy')
p.add_argument('--receiver', default='release/receiver-054/PhoneWheel.Receiver.exe')
a = p.parse_args()
if a.direct and a.faults: p.error('--direct cannot inject network faults')
if a.brief_outages and not a.faults: p.error('--brief-outages requires --faults')
root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/dropout' / time.strftime('%Y%m%d-%H%M%S')
out.mkdir(parents=True, exist_ok=True)
key = base64.b64encode(secrets.token_bytes(32)).decode()
session = f'{secrets.randbits(64) or 1:X}'
stop = threading.Event()
front = back = None
if not a.direct:
    front = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    front.bind((a.host, 26760))
    back = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    back.connect(('127.0.0.1', 26770))
stats = dict(control=0, status=0, droppedControl=0, delayedControl=0, delayedStatus=0, duplicates=0, interruptions=0)
phase_events = []
health = deque(maxlen=20)
health_events = []
started_at = time.monotonic()
receiver = subprocess.Popen([str(root / a.receiver), '--wifi', '--monitor', '--bind', a.host if a.direct else '127.0.0.1', '--port', '26760' if a.direct else '26770',
    '--backend', 'null', '--session-hex', session, '--key-base64', key, '--diagnostics-dir', str(out / 'pc'),
    '--run-seconds', str(a.seconds + 80)], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)

def read_health():
    previous_stops = None
    for raw in receiver.stdout:
        text = raw.decode('utf-8', errors='replace').strip()
        if text.startswith(('Stops ', 'RX max ', 'Wi-Fi · ')):
            health.append(text)
        if text.startswith('Stops ') and text.split(' / ', 1)[0] != previous_stops:
            previous_stops = text.split(' / ', 1)[0]
            health_events.append(dict(seconds=round(time.monotonic() - started_at, 3), health=text,
                phase=phase_events[-1]['phase'] if phase_events else 'direct'))

def proxy():
    peer = None
    started = None
    queue = []
    counter = 0
    last_phase = None
    while not stop.is_set():
        now = time.monotonic()
        while queue and queue[0][0] <= now:
            _, _, direction, packet = heapq.heappop(queue)
            if direction == 0:
                back.send(packet)
            elif peer:
                front.sendto(packet, peer)
        ready, _, _ = select.select([front, back], [], [], .002)
        for source in ready:
            try:
                packet, address = source.recvfrom(256)
            except ConnectionResetError:
                continue
            direction = 0 if source is front else 1
            if direction == 0:
                peer = address
                if started is None: started = now
            elapsed = now - (started or now)
            # First 20s always clean. Repeated 120s cycle: bounded jitter/loss,
            # delayed ACK, duplicate/reorder, then a deliberate real 1.5s outage.
            phase = 'normal'
            slot = (elapsed - 20) % 120
            if a.faults and elapsed >= 20:
                phase = 'jitter' if slot < 25 else 'loss' if slot < 50 else 'ack-delay' if slot < 75 else 'duplicate-reorder' if slot < 100 else 'interruption' if slot < 101.5 else 'recovery'
            if phase != last_phase:
                phase_events.append(dict(seconds=round(elapsed, 3), phase=phase))
                if phase == 'interruption': stats['interruptions'] += 1
                last_phase = phase
            if phase == 'interruption': continue
            if a.brief_outages and phase == 'jitter' and 2 <= slot % 10 < 2.160:
                continue
            delay = 0
            if direction == 0 and len(packet) > 5 and packet[5] in (1, 4):
                stats['control'] += 1
                n = stats['control']
                if phase == 'loss' and n % 30 in (0, 1): stats['droppedControl'] += 1; continue
                if phase == 'jitter' and n % 25 == 0: delay = .060; stats['delayedControl'] += 1
                if phase == 'duplicate-reorder' and n % 25 == 0:
                    counter += 1; heapq.heappush(queue, (now + .025, counter, direction, packet)); stats['duplicates'] += 1
            elif direction == 1 and len(packet) > 5 and packet[5] == 3:
                stats['status'] += 1
                if phase == 'ack-delay' and stats['status'] % 15 == 0: delay = .080; stats['delayedStatus'] += 1
            counter += 1; heapq.heappush(queue, (now + delay, counter, direction, packet))

threads = [threading.Thread(target=read_health, daemon=True)]
if not a.direct: threads.append(threading.Thread(target=proxy, daemon=True))
for t in threads: t.start()
try:
    adb = str(root / '.tools/android-sdk/platform-tools/adb.exe')
    instrument = subprocess.Popen([adb, '-s', a.serial, 'shell', 'am', 'instrument', '-w', '-e', 'nullBackend', 'true',
        '-e', 'soakSeconds', str(a.seconds), '-e', 'host', a.host, '-e', 'session', session, '-e', 'key', key,
        'dev.phonewheel.test/dev.phonewheel.ControllerIsolationInstrumentation'], stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        text=True, encoding='utf-8', errors='replace')
    instrument_lines = []
    def read_instrument():
        for line in instrument.stdout:
            instrument_lines.append(line)
            print(line.rstrip(), flush=True)
    reader = threading.Thread(target=read_instrument, daemon=True)
    reader.start()
    try:
        instrument.wait(timeout=a.seconds + 90)
    except subprocess.TimeoutExpired:
        instrument.kill()
        instrument.wait(5)
        raise RuntimeError('Instrumentation timed out; command credentials omitted') from None
    finally:
        reader.join(5)
    completed = subprocess.CompletedProcess([], instrument.returncode, ''.join(instrument_lines))
    print('\n'.join(health))
    print(json.dumps(stats))
    (out / 'summary.json').write_text(json.dumps(dict(mode='direct' if a.direct else 'fault-proxy' if a.faults else 'proxy', stats=stats, phases=phase_events, health=list(health), healthEvents=health_events, instrumentation=completed.stdout), indent=2), encoding='utf-8')
    if 'SOAK COMPLETE' not in completed.stdout or 'failure=' in completed.stdout:
        raise RuntimeError('Soak failed; inspect diagnostic files')
    print('Report:', out)
    disarms = re.search(r'INSTRUMENTATION_RESULT: disarms=(\d+)', completed.stdout)
    if a.direct and disarms and int(disarms.group(1)) > 0:
        raise RuntimeError('Direct soak completed but unexpected disarms remain; NOT a reliability pass')
finally:
    # Let triggered recorder post-windows finish before ending this null receiver.
    time.sleep(3.2)
    stop.set()
    for t in threads[1:]: t.join(2)
    if front: front.close()
    if back: back.close()
    if receiver.poll() is None: receiver.terminate()
    receiver.wait(5)
