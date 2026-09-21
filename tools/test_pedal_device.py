"""Bounded ADB-injected pedal sweep on a connected PhoneWheel, checked via XInput.

Run only after observing the app's landscape bounds and verifying its XInput slot.
This is an injected touch test, not a human ergonomics or physical steering test.
"""
import argparse
import ctypes
import os
import subprocess
import time
from xinput_probe import State


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--adb", required=True)
    p.add_argument("--serial", required=True)
    p.add_argument("--height", type=int, required=True)
    p.add_argument("--throttle-x", type=int, required=True)
    p.add_argument("--brake-x", type=int, required=True)
    p.add_argument("--slot", type=int, choices=range(4), required=True)
    a = p.parse_args()
    dll = ctypes.WinDLL(os.path.join(os.environ["SystemRoot"], "System32", "xinput1_4.dll"))
    read = dll.XInputGetState
    read.argtypes = [ctypes.c_uint32, ctypes.POINTER(State)]
    read.restype = ctypes.c_uint32

    def touch(kind, x, y):
        subprocess.run([a.adb, "-s", a.serial, "shell", "input", "motionevent", kind, str(x), str(y)], check=True, timeout=5)

    def expect(brake, throttle):
        deadline = time.monotonic() + .5
        while True:
            s = State()
            result = read(a.slot, ctypes.byref(s))
            if result == 0 and abs(s.gamepad.lt - brake) <= 1 and abs(s.gamepad.rt - throttle) <= 1:
                return s.gamepad.lt, s.gamepad.rt
            if time.monotonic() >= deadline:
                raise AssertionError(f"Expected LT/RT {brake}/{throttle}, got status={result} {s.gamepad.lt}/{s.gamepad.rt}")
            time.sleep(.01)

    print("pedal,percent,y,lt,rt")
    for pedal, x in [("throttle", a.throttle_x), ("brake", a.brake_x)]:
        # Start at the requested 0% line, not inside Android's bottom gesture zone.
        y = round(a.height * .9)
        try:
            touch("DOWN", x, y)
            for percent in [0, 25, 50, 75, 100, 50, 0]:
                y = round(a.height * (.9 - .8 * percent / 100))
                touch("MOVE", x, y)
                expected = round(255 * percent / 100)
                lt, rt = expect(expected if pedal == "brake" else 0, expected if pedal == "throttle" else 0)
                print(f"{pedal},{percent},{y},{lt},{rt}")
        finally:
            touch("UP", x, y)
        expect(0, 0)
    print("PASS: both independent sweeps and release; no calibration/arm button injected")


if __name__ == "__main__":
    main()
