"""Send one bounded rumble pulse to an explicitly selected, verified PhoneWheel slot."""
import argparse
import ctypes
import os
import time


class Vibration(ctypes.Structure):
    _fields_ = [("large", ctypes.c_uint16), ("small", ctypes.c_uint16)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--slot", type=int, choices=range(4), required=True)
    args = parser.parse_args()
    dll = ctypes.WinDLL(os.path.join(os.environ["SystemRoot"], "System32", "xinput1_4.dll"))
    write = dll.XInputSetState
    write.argtypes = [ctypes.c_uint32, ctypes.POINTER(Vibration)]
    write.restype = ctypes.c_uint32
    try:
        result = write(args.slot, ctypes.byref(Vibration(65535, 32767)))
        if result != 0:
            raise RuntimeError(f"XInputSetState failed: {result}")
        time.sleep(.3)
    finally:
        write(args.slot, ctypes.byref(Vibration(0, 0)))
    print(f"PASS XInput rumble pulse accepted on slot {args.slot}; stop sent (physical feel not verified)")


if __name__ == "__main__":
    main()
