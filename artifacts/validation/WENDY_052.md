# Wendy 0.5.2 — 2026-09-19

## Scope and evidence

The user reported AUTO failure/ERROR↔IDLE, a controller interruption during AUTO→PTT, wanted PTT hidden in AUTO, varied race greetings and broader telemetry conversation coverage. Preserve existing wheel/pedal/right-stick mapping and all safety deadlines.

Real phone logs contained both controller reply losses/timeouts and Wendy IDLE/ERROR without microphone-start entries. This does **not** identify a single proven cause for the reported race incident. The PC Receiver was not running at inspection and its current Wi-Fi address had changed to 172.30.1.58. Android support probing confirms the system recognizer works while an English on-device model is absent. Do not equate service availability with a downloaded language.

## Changes

- AUTO hides PTT; PTT mode restores it without moving the centered flag. English settings recommend the working system provider. An asynchronous on-device English support check prevents knowingly selecting a missing model and explains the remedy. No silent cloud fallback.
- Transient speech errors use 3/6/12/24/30-second retry backoff instead of permanently suspending after three errors. Silence retries after 1.2 seconds. A 1-second supervisor restores a missed IDLE restart, preserving backoff/fatal-error suspension. Recognizer reuse reduces service churn; retry/provider changes dispose it when required.
- Speech-output failures no longer permanently disable AUTO input. Voice focus changes pause recording without rebuilding the Wendy TCP connection. Real Activity background/focus loss still releases game controls for safety.
- Controller disconnect UI callbacks check for a newer ACK before clearing status/disarming: a delayed old callback cannot erase a recovered link. Controller socket/scheduler/mapping and ACK freshness/watchdog remain unchanged. No promise of lossless Wi-Fi.
- Metadata diagnostics show engineer connection, mic starts/readiness/error counts, installed-model status and last fault. Logs contain state/error class, not raw audio, transcripts or pairing secrets. Speech recognizer is the selected Android provider; system mode may use its online service.
- Twenty race greetings with no immediate text repeat, once per session UID over Receiver lifetime (bounded history). Requires current live race data; flags and significant warnings take priority. Same-session reconnect/flashback does not greet again. Greeting confirms downlink/TTS, not microphone reception; “Radio check” tests the latter.
- Expanded read-only intents: radio check/help, speed, gear, RPM, DRS, compound, track/air temperature, session time, pit limiter/speed limit, pit stop count, wing setup, lap validity, last-lap time, sectors and ERS mode. Named wing/floor/diffuser/sidepod/gearbox/engine damage. Parsed packed offsets checked against EA F1 25 UDP v3; stale/unknown fields are not invented.
- Existing questions, conservative pit reminders, first-clean-lap coaching and proactive alerts retained. No game menu mutations added. Conversation examples/limits in `docs/WENDY_CONVERSATIONS.md`.

## Verification

- Windows: **25/25 groups PASS**, including new read-only values/freshness and race greeting session/flashback/pause/SC tests. Self-contained Release EXE published successfully.
- Android: **42 tests PASS**; debug app and development instrumentation APK builds succeed. Lint: **0 errors / 43 warnings** (includes existing hardcoded English text and deprecated QR API). Final app versionName 0.5.2, versionCode 14.
- Actual CPU classifier: initial extended-prompt run 50/51 (diff readback misclassified). Added deterministic readback phrasing; rerun **51/51 representative sentences PASS**, not general language accuracy. Measured model CPU 193.2% of one core while classifying, 0.00% in a short idle sample, 955.7 MiB working set, 16 logical CPUs. CPU-only optional model unchanged; GPU inference not used.
- Synthetic simultaneous Wendy/controller test PASS: ten queries, unsupported mutations, alerts/cooldowns, MAC rejection, reconnect, stale telemetry and classification with controller active. Six-second samples: OFF Receiver **5.45% of one core / 34.72 MiB / ACK RTT p95 9.13ms**; ON before model **9.09% / 45.25 MiB / 9.08ms**; inference-inclusive p95 **9.18ms**. Concurrent development workload; not a game FPS or physical input latency benchmark.
- Synthetic USB PASS: malformed length, fragmented handshake, independent triggers, partial-frame watchdog **150.386ms**, new-socket recovery, held-input block and neutral rearm. Wi-Fi endpoint protection/reauthentication/neutral rearm PASS.
- Physical Fold5, intermediate 0.5.2: **AUTO microphone started/ready 4/4, errors 0**. Four AUTO→PTT transitions, 12 panel cycles and English TTS passed with same controller client and same Wendy TCP object, armed/calibration preserved at checkpoints. **1201 ACKs, max sender gap 12ms, max ACK gap 91ms, sender failures 0**. Null-output Receiver only: no virtual gamepad and no racing. This verifies microphone service readiness, not recognition of a spoken human request or long-session radio reliability.
- Initial physical test harness captured a stale Activity after Fold orientation recreation, causing “Controller not created”; fixed by waiting for the resumed Activity. This was a harness failure, not evidence of a new controller regression.
- Final app APK installation returned **Success**. USB debugging disappeared while installing the updated test APK for the longer automatic re-listen test. That longer test, final package readback and development-test-APK removal remain pending reconnection; earlier 4/4 readiness result must not be represented as that test. Several subsequent authorized ADB listings were empty, including after the user's reconnect acknowledgement.

## Deployment / limitations

New EXE copied to `release/receiver/PhoneWheel.Receiver.exe`; SHA256 **E9EF897E7508AF709FDA0EB2EF608AFB794937B9215A2857432B779E78D81E7A**. Previous executable preserved at `release/backups/PhoneWheel.Receiver-before-052.exe`. Existing CPU bundle retained. User approved installation, microphone tests and Receiver restart with the game paused.

Normal Receiver launched successfully as PID **168440**, Responding=true, title **PhoneWheel · Wi-Fi QR connection**. Current bind **172.30.1.58**, UDP26760 controller, TCP26762 Wendy, loopback UDP20777 F1 telemetry verified. Scan the new QR; previous pairing is obsolete. No further interruption was made after starting this final normal receiver. Phone-side long re-listen verification must use null output again with a paused game, not this gamepad-enabled receiver.

Not all UDP packets/conversations or game operations are supported. No weather forecasting, automatic pit/menu controls, optimal strategy model, guaranteed never-disconnecting Wi-Fi, or continuous background microphone. Long sessions and actual spoken questions in the game still need user verification. Radio check should be tried before racing. Use AUTO system on this device unless English is installed for the on-device recognizer.

## Changed files

- Android runtime: `MainActivity.kt`, `WendyVoice.kt`, `WendyClient.kt`, `AlwaysListenPolicy.kt`, `app/build.gradle.kts`.
- Android tests: `AlwaysListenPolicyTest.kt`, `ControllerIsolationInstrumentation.kt`.
- Core: `F1RaceState.cs`, `WendyIntent.cs`, `WendyEngineer.cs`; new `WendyDataQueries.cs`, `WendyGreetings.cs`.
- Host: `CpuIntentModel.cs`.
- Windows tests: `Program.cs`, `WendyModelTests.cs`; new `Wendy052Tests.cs`.
- Tools/docs: `tools/test_phone_isolation.py`, `README.md`, `docs/WENDY.md`, new `docs/WENDY_CONVERSATIONS.md`, this report.

## Primary references

- [EA F1 25 UDP v3](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf): session types 15–17 are races; status/telemetry packed fields and units. Format 2026 is not guessed.
- [Android SpeechRecognizer](https://developer.android.com/reference/android/speech/SpeechRecognizer): provider availability/support, recognition callbacks and lifecycle; repeated recognition has provider, battery and bandwidth limitations.
