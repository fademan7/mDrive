"""Read-only Windows XInput CSV probe. Does not create a virtual controller."""
import argparse
import csv
import ctypes
import math
import os
import sys
import time


class Gamepad(ctypes.Structure):
    _fields_ = [("buttons", ctypes.c_uint16), ("lt", ctypes.c_uint8),
                ("rt", ctypes.c_uint8), ("lx", ctypes.c_int16),
                ("ly", ctypes.c_int16), ("rx", ctypes.c_int16),
                ("ry", ctypes.c_int16)]


class State(ctypes.Structure):
    _fields_ = [("packet", ctypes.c_uint32), ("gamepad", Gamepad)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seconds", type=float, default=30)
    parser.add_argument("--interval-ms", type=float, default=8)
    parser.add_argument("--slot", type=int, choices=range(4))
    args = parser.parse_args()
    if (not math.isfinite(args.seconds) or not 0 < args.seconds <= 3600
            or not math.isfinite(args.interval_ms) or not 1 <= args.interval_ms <= 1000):
        parser.error("seconds must be 0..3600; interval-ms must be 1..1000")
    if os.name != "nt":
        print("NOT RUN: XInput requires Windows. No device test was performed.", file=sys.stderr)
        return 2
    assert ctypes.sizeof(Gamepad) == 12 and ctypes.sizeof(State) == 16
    # System DLL only. No downloads or writes to a controller.
    dll_path = os.path.join(os.environ["SystemRoot"], "System32", "xinput1_4.dll")
    dll = ctypes.WinDLL(dll_path)
    read = dll.XInputGetState
    read.argtypes = [ctypes.c_uint32, ctypes.POINTER(State)]
    read.restype = ctypes.c_uint32
    writer = csv.writer(sys.stdout, lineterminator="\n")
    writer.writerow(["pc_monotonic_ns", "slot", "status", "packet", "lx", "lt", "rt", "buttons"])
    slots = [args.slot] if args.slot is not None else range(4)
    start = time.perf_counter()
    try:
        while time.perf_counter() - start < args.seconds:
            for slot in slots:
                state = State()
                result = read(slot, ctypes.byref(state))
                now = time.perf_counter_ns()
                if result == 0:
                    p = state.gamepad
                    writer.writerow([now, slot, result, state.packet, p.lx, p.lt, p.rt, p.buttons])
                else:
                    writer.writerow([now, slot, result, "", "", "", "", ""])
            time.sleep(args.interval_ms / 1000)
    except KeyboardInterrupt:
        pass
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
