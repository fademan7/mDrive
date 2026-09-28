# mDrive 0.5.5 — Wi-Fi-only and conversational racing requests

2026-09-27. Source/build verification, not a live F1 or speech-recognition accuracy
certification. Prior 0.5.4 dropout changes and recorder are preserved; this update
does not claim to resolve the previously measured radio-path gaps.

## Implemented

- Removed automatic ADB detection, reverse tunnels, USB TCP controller transport,
  Android USB pairing intents/QRs, USB status UI and `START_USB.cmd`.
- Receiver EXE, `START_RECEIVER.cmd` and `START_WIFI.cmd` now all use Wi-Fi.
  Retired `--usb` / `--usb-test` flags fail with a clear message, not a silent
  switch. APK installation may still use USB debugging; it is not a game mode.
- Added `WendyNaturalLanguage`: bounded request normalization plus topic,
  direction, wheel and handling-feedback slots before the existing CPU model.
  "Tell me the time gap" now explicitly maps to `GET_GAP_AHEAD`.
  Polite prefixes/suffixes, US/UK tyre spelling, hyphenated/reversed wheel names
  and direction-specific gaps are handled without model startup.
- Distinguishes temperatures, pressures, wear, age, compound; fuel, ERS/mode;
  gaps ahead/behind/leader; damage versus setup; laps, sectors and remaining laps;
  pit status/advice/count/limiter; weather/forecast, DRS, penalties and coaching.
- Short social replies, race summary, "say again" and wheel follow-ups. Read-only
  context is one query for 30 seconds, reset by session change/reconnection.
  Repetition reads fresh telemetry instead of replaying old values. No settings,
  pit plans or approvals are repeated. Reconnection also clears pending approval.
- Existing optional CPU-only classifier remains for other phrasing; GPU, Ollama,
  OpenAI API, capture and OCR were not added. Small talk responses are templates,
  not free-form model-generated facts. No new model download is required.

## Safety and scope

Controller protocol, axis mapping, gyro/pedals, safety deadlines and independent
controller workers are unchanged apart from retiring the USB branch. Race query
processing remains on the Wendy service, not a controller thread. Known phrases
need no inference process. This is not a measured zero-overhead game benchmark.

Ambiguous multi-topic questions ask for a single topic. Negative/uncertain pit
requests do not become plans; numbers are not guessed. Actual game-menu setting
changes remain unsupported. `Box box` is still a reminder, not a game pit request.
Missing/stale telemetry is reported, not filled with invented values. The existing
first-clean-lap reference, lap/corner comparisons and proactive alerts are retained.
Optimal strategy, arbitrary general knowledge/chat, all possible English phrases,
and live game setting automation are not claimed as complete.

## Verification

- Windows **37/37 groups passed**, including **255 phrasing variants** (51 known
  requests × 5 wrappers), ambiguous/negative requests, polite commands, small talk,
  fresh follow-up values, expiry/session/reconnect reset and no command repetition.
- Fast-path test uses a nonexistent model bundle: all known corpus requests
  classify as `Race rules`, and no model process starts.
- Authenticated Wendy TCP round trip checks full sentence queries, wheel follow-up,
  short chat and radio check; response sizes stay within the Android wire limit.
- Android **43 tests passed**, zero failures/errors. Three USB-only tests were
  removed with the transport; retired USB pairing now has a rejection test.
- APK and instrumentation APK built. Android lint **0 errors / 73 warnings**;
  SDK analytics also warned about the sandbox's unwritable analytics directory.
- Self-contained Windows x64 Receiver published to `release/receiver-055`.
- Real loopback UDP regression passed: endpoint protection, stale endpoint
  authentication, held throttle rejected on reconnect, neutral re-arm.
- USB CLI retirement checked: clear rejection, no USB transport startup.
- No connected ADB device was available for this update. No new phone install,
  live microphone/TTS check, physical driving or F1 run was performed.

## Main files

- `windows/src/PhoneWheel.Core/WendyNaturalLanguage.cs`, `WendyIntent.cs`,
  `WendyEngineer.cs`; Host `CpuIntentModel.cs`, `WendyService.cs`.
- Host `Program.cs`, `ReceiverTransport.cs`, `PairingWindow.cs`, project version;
  deleted `UsbConnector.cs`.
- Android `MainActivity.kt`, `PairingDetails.kt`, `PacketTransport.kt`,
  `UdpControllerClient.kt`, `WendyClient.kt`, version 0.5.5/code 17.
- Tests, launch script, README, conversation reference and protocol retirement note.
- Removed `tools/test_usb_transport.py` and USB-only Android transport cases;
  retained authenticated reconnect safety tests under transport-neutral names.

Removed tracked source/scripts remain recoverable from Git history. No user
preferences, voice data, model bundle or existing diagnostic captures were deleted.

## Build identifiers

- APK SHA-256: `25AB6795DC21CC2596BD76720420E778FCD9C8711229EB8D8BE23B374C8EBE04`
- Receiver SHA-256: `7AEC8B40C89408D35793FE4AD5FF7B090454C2B394D64CB3703B34A473FB7975`
- The inactive default `release/receiver/PhoneWheel.Receiver.exe` was updated after
  backing up 0.5.4 to `release/backups/PhoneWheel.Receiver-before-055.exe`.
  Its existing optional `wendy` bundle was preserved. Receiver was not restarted;
  no active game connection was interrupted. Phone installation awaits an ADB device.

## Subsequent device deployment

After the user enabled/authorized USB debugging, `adb install -r` succeeded on
the Fold5. Package inspection confirmed versionName **0.5.5**, versionCode **17**.
The main app was launched and its process was present. The normal Wi-Fi Receiver
was started for new QR pairing. This confirms installation/startup only; live
microphone recognition and F1 driving on 0.5.5 still require user testing.
