# 0.2.3 — USB reconnect and steering gain

Date: 2026-09-13. User authorized installation and receiver restart; game comparison is reserved for user.

## Changes

- Android USB IOException/1 s read timeout now discards the stream and retries after 500 ms, retaining authenticated session sequence numbers. Send errors close the stream to unblock receive. The UI disarms on disconnect.
- Hello/Control header and body use one handshake-state snapshot to avoid a concurrent state change producing mismatched packets and cancelling the scheduled sender.
- Receiver accepts a newer authenticated USB Hello on a replacement TCP connection, clears old challenges and releases output. Replay/invalid authentication cannot take over. Wi-Fi endpoint policy is unchanged.
- Same-device ADB reverse mapping is checked every 2 s, with no-rebind and no repeated app launches. Socket disposal errors no longer terminate the receive loop.
- Transport recovery never resumes held throttle automatically. User presses Resume and completes neutral dwell. The 150 ms watchdog / 100 ms ACK freshness / 300 ms host neutral dwell are unchanged.
- Steering preset: half-range 116 degrees, curve 1, deadzone 0, smoothing 0. 90 degrees -> 0.775862 normalized stick; clamp at 116 degrees. Both directions tested; pedals unchanged. This is not proof of game cockpit animation matching.

## Executed

- Android offline `testDebugUnitTest assembleDebug lintDebug`: 28 tests, zero failures/errors; build/lint passed (existing deprecation/environment warnings).
- C# test runner: 12/12 groups passed, including reconnect release/replay/neutral rearm.
- `tools/test_usb_transport.py`: real loopback TCP receiver + CSV output, invalid length, fragmented Hello, independent full triggers, partial-frame watchdog **155.569 ms**, new-source-port reconnect, held ARM+throttle blocked, neutral rearm passed. This is not physical USB or game latency.
- APK installed with `adb install -r`, receiver republished and launched in USB mode. Android and PC both reported authenticated USB input.
- App UI preset applied. Device preferences read back: `halfRange180=116`, `curve=1`, `deadzone=0`, `smoothing=0`; game rumble remains off.
- Removed only the observed `tcp:26761 -> tcp:26761` ADB reverse rule; the running receiver restored it within the subsequent 5 s observation. Existing stream stayed active, so this proves mapping recovery, not stream-break recovery.
- Targeted `adb -s <device-serial> reconnect` test (personal device identifier omitted): active stream closed, receiver stayed alive/listening and released output. ADB device did **not** automatically reappear, even though Windows PnP still listed Samsung USB Composite/Modem/ADB Interface. Asked user to unplug/replug cable. Do not label physical reconnect test PASS.

## Follow-up: final installation and user cable replug

- User unplugged/replugged the cable. Same receiver PID 80856 remained running; ADB returned (transport 11), reverse mapping was present, a new TCP connection was established (remote port 56239), and the phone reported `USB 연결됨 · 정지 · 재개로 준비`. This confirms same-session recovery without restarting the receiver after user cable replug, while keeping output disarmed. It does not prove that an absent ADB device recovers without user replugging.
- Installed the final 0.2.3 APK including the handshake-state snapshot correction using `adb install -r` (Success). Package readback: versionCode 5, versionName 0.2.3, lastUpdateTime 2026-09-13 21:34:13.
- After APK replacement, gracefully closed/restarted receiver for a fresh session, as required when the Android process restarts. Final receiver PID 75532 had an established loopback TCP connection and phone reported `USB 연결됨 · 운전 활성`.
- Settings readback after final install: halfRange180=116, curve=1, deadzone=0, smoothing=0, gameRumble=false. Pedal/layout code and preferences were not changed. No further disconnect injection performed.

## Remaining

- Final source APK installation and user cable-replug recovery are complete as recorded above. Autonomous recovery of an absent ADB device is still not demonstrated.
- Cause of spontaneous USB/ADB disappearance is not established. An explicit debug reconnect reproduces absent ADB, but is not proof of a bad cable, driver, or original gameplay failure cause.
- Long gameplay stability and exact cockpit-wheel angle matching in F1 25 + 2026 pack / Assetto Corsa are not verified. No game configuration changed, no security settings changed, no physical steering or pedal injected during this task.
- Killing/restarting the phone app resets its sequence; restarting the receiver for a fresh session is still required. USB/Wi-Fi are selected at receiver startup, not switched live.
