# Xbox rumble relay 0.5.16 — 2026-10-14

## Scope and confirmed faults

Game Xbox vibration -> ViGEm callback -> RumbleMailbox -> authenticated UDP HAPTIC -> Android vibration. No F1 telemetry dependency, added AI, game input macro, firewall or driver settings change.

Native XInput test on this PC reproduced two faults before the change:

- A single sustained motor request: level 3 initially, level 0 at 800 ms, callback count unchanged (5). The old 500 ms expiry incorrectly treated changed-state feedback as a heartbeat.
- A 15 ms motor pulse followed by stop before the next read: level 0, lost entirely.

After the change: sustained request remains level 3 at 800 ms; large and small motors both received; explicit stop level 0; short pulse level 3 once then 0. No stored pulse older than 100 ms is replayed. The mailbox is one peak, not an event queue.

## Safety / isolation

Motor state persists until the game stops it, but disarm clears current state and pending peak. The phone still requires authenticated packets with <=100 ms ACK age and a short renewed vibration lease. App background cancels vibration. Steering/pedal safety gate and freshness thresholds were not changed. Haptic transmission interval changed from 50 to 20 ms (same ACK loop, no new blocking calls); native callback only updates bounded state/counter.

## Verification

- Windows 49/49 regression groups passed, including control recovery and rumble lifecycle/peak tests.
- Android 57 unit tests, APK/test APK build and lint passed.
- Real ViGEm/XInput native probe passed (no game running, own test pad only).
- First real Wi-Fi relay run: 118 HAPTIC accepted, 0 rejected, 115 nonzero frames played, level bitmask 14 (all three), max control send gap 15 ms, 0 controller releases/output errors.
- Repeat requested by user: 119 HAPTIC accepted, 0 rejected, 116 nonzero frames played, mask 14, max control send gap 13 ms, 0 controller releases/output errors. User explicitly confirmed all three strengths felt on the phone.
- Installer 0.5.16 built successfully; SHA256 `6136C02C6838D4B4C37E44FEF3276806D447B69EF3EF75212EC27F6381A7A64E`.
- PC installer exited 0 and installed file version is 0.5.16.0. Final Android APK installation returned Success; app was reopened. No firmware/driver/security settings changed.

## Reproduction

Game must be stopped. These tests create a new virtual pad and only send rumble to its own slot; no driver/security changes or persisted pairing secrets.

```powershell
.tools/dotnet/dotnet.exe run --project windows/src/PhoneWheel.Probe -c Release -- --rumble-only
python tools/test_rumble_relay.py --host <PC-WIFI-IP> --serial <ADB-SERIAL>
```

Relay requires the development instrumentation APK and a level, untouched phone for initial ARM. No simulated steering/pedal touches are used.

## Limits / references

This verifies actual Xbox feedback transport and phone sensation, not a new F1 driving session or every game's own rumble settings. Wheel-only force feedback/impulse-trigger effects are not interpreted. A game must send standard Xbox motor vibration. Source diagnostics help separate absence of game feedback from packet rejection.

- [Microsoft XInputSetState](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputsetstate)
- [ViGEm.NET Xbox360 callback implementation](https://github.com/nefarius/ViGEm.NET/blob/master/ViGEmClient/Targets/Xbox360Controller.cs)
