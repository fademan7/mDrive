"""Explicit game-paused test: own new Xbox slot -> real Receiver -> phone haptics.
No simulated controller input, no voice, no screen capture, no stored pairing key.
"""
import argparse, base64, ctypes, os, secrets, subprocess, threading, time
from pathlib import Path
from collections import deque
from xinput_probe import State

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--host',required=True);p.add_argument('--serial',required=True)
p.add_argument('--receiver',default='release/receiver/PhoneWheel.Receiver.exe')
a=p.parse_args();root=Path(__file__).resolve().parents[1]
dll=ctypes.WinDLL(os.path.join(os.environ['SystemRoot'],'System32','xinput1_4.dll'))
class Motors(ctypes.Structure):
    _fields_=[('large',ctypes.c_uint16),('small',ctypes.c_uint16)]
dll.XInputGetState.argtypes=[ctypes.c_uint32,ctypes.POINTER(State)]
dll.XInputSetState.argtypes=[ctypes.c_uint32,ctypes.POINTER(Motors)]
def slots():
    return {i for i in range(4) if dll.XInputGetState(i,ctypes.byref(State()))==0}
before=slots();slot=None;history=deque(maxlen=8)
key=base64.b64encode(secrets.token_bytes(32)).decode();session=f'{secrets.randbits(64) or 1:X}'
proc=subprocess.Popen([str(root/a.receiver),'--wifi','--monitor','--backend','vigem','--bind',a.host,
    '--session-hex',session,'--key-base64',key,'--run-seconds','60'],stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
def read():
    for raw in proc.stdout:
        text=raw.decode('utf-8',errors='replace').strip()
        if text.startswith(('Rumble:', 'Stops ')):history.append(text)
threading.Thread(target=read,daemon=True).start()
instrument=None
def motor(large,small):
    rc=dll.XInputSetState(slot,ctypes.byref(Motors(large,small)))
    if rc:raise RuntimeError(f'Own-slot XInput write failed: {rc}')
try:
    for _ in range(100):
        new=slots()-before
        if len(new)==1:slot=new.pop();break
        time.sleep(.05)
    if slot is None:raise RuntimeError('Unique new Xbox slot unavailable; no rumble sent')
    instrument=subprocess.Popen([str(root/'.tools/android-sdk/platform-tools/adb.exe'),'-s',a.serial,'shell','am','instrument','-w',
        '-e','resumeOnly','true','-e','rumbleRelay','true','-e','host',a.host,'-e','session',session,'-e','key',key,
        'dev.phonewheel.test/dev.phonewheel.ControllerIsolationInstrumentation'],stdout=subprocess.PIPE,text=True,encoding='utf-8',errors='replace')
    timeout=threading.Timer(35,instrument.kill);timeout.start();lines=[]
    try:
        for line in instrument.stdout:
            lines.append(line)
            if 'rumbleReady=true' in line:
                print('Own Xbox slot ready. Weak, medium, strong rumble, then stop.',flush=True)
                for large,small in [(18000,0),(0,35000),(65535,65535)]:
                    motor(large,small);time.sleep(1.2);motor(0,0);time.sleep(.35)
        instrument.wait(timeout=5)
    finally:timeout.cancel()
    text=''.join(lines);print(text);print('\n'.join(history))
    if 'PASS actual Xbox feedback' not in text or 'failure=' in text:raise RuntimeError('End-to-end rumble test failed')
finally:
    if slot is not None:motor(0,0)
    if instrument and instrument.poll() is None:instrument.kill()
    if proc.poll() is None:proc.terminate()
    proc.wait(timeout=5)
