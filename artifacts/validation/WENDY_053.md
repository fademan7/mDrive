# mDrive 0.5.3 — 2026-09-20

## Diagnosis and priority

The user reports that steering/pedals stop while Wendy continues answering. This isolates neither the radio nor the precise fault: voice uses separate TCP, while controls use UDP and a safety gate/ViGEm output worker. Android was not listed by ADB on read-only checks this turn. The live Receiver was not stopped or changed; no incident-time phone/driver trace was available.

Code inspection found a reproducible permanent-stop defect: old `GamepadWorker.RunAsync` caught one output exception outside its read loop, then exited forever. The receiver, producer and Wendy could continue; controller frames were queued with no consumer. Fault injection reproduces this failure mode. It is **not proof** that this was the user's particular incident. Sensor/focus/ACK timeout and native-driver faults remain distinguishable causes.

## Controller changes

- Dedicated above-normal-priority native-output thread with a latest-only mailbox. All output calls, neutralization and disposal remain serialized; no virtual device recreation/slot change on a transient error.
- Failed writes disarm the safety gate, discard buffered controls and retry neutral reports at 250ms intervals. A successful neutral write restores the worker; stale throttle is never replayed. A broken diagnostic observer cannot terminate recovery. Normal fresh input and existing neutral re-arm are still required.
- Independent producer freshness expiry at 150ms in the output worker. Native output completion age is monitored; driver stalls appear as output faults rather than “Driving active.” A permanently blocked native call cannot be safely cancelled by this managed code and is not claimed fixed.
- Dedicated ACK/haptic and safety-clock threads replace shared thread-pool timer continuations. Voice/model/UI work does not run on these threads. Transport authentication, 100ms ACK challenge, 150ms gate timeout, controller mappings and neutral dwell are not relaxed.
- Safety diagnostics retain stop count and last release cause even after current reason changes; count rejected ACK challenges. Receiver shows stop cause, pad error count and ACK rejects. Android has an in-window Controller diagnostics snapshot with sensor/ACK ages, focus, armed state, max gaps, sender failures and last release context. No credentials/transcripts in these diagnostics.
- Real background/focus loss still releases input. No promise to continue driving on stale packets or survive driver/radio failure without interruption.

## Other additions

- Android Options → About / Creator and Windows Receiver → About show **Created by fademan7 / neojshin**, **https://fademan7.github.io/** and **neojshin@gmail.com**. Links open only on user click; no external messages or publication. Android warns to pause the game before opening external apps.
- `GET_WEATHER_FORECAST`: current-session EA forecast samples only, minute offset, rain probability, approximate label; invalid/unavailable samples excluded. `GET_GAP_LEADER`: same-lap leader gap, with explicit unavailable status for lapped/missing data. Current weather queries unchanged. [Conversation list](../../docs/WENDY_CONVERSATIONS.md).
- Game menu commands remain unsupported: no reliable verified command channel or binding/menu state is available. Pit requests remain explicit reminders, not falsely confirmed game actions. A complete strategy/physics/driver-name conversation engine is not claimed.

## Verification

- Windows **28/28 groups PASS**, including permanent output-failure/recovery injection, exceptions in diagnostic callbacks, stale mailbox neutralization, single native-output thread, dedicated clocks, safety output-error held-throttle rejection and fresh neutral re-arm, retained stop cause, forecast/session filtering and same-lap leader gap. Release publish succeeds.
- Android debug APK/test APK builds and **42 unit tests PASS**. Final lint: **0 errors / 66 warnings**, including hardcoded English diagnostic/creator text. Steering/pedal/layout settings are preserved.
- Actual optional CPU classifier **51/51 representative phrases PASS**, including future rain routed to forecast. CPU workload 192.9% of one core, short idle 0.52%, 955.8MiB working set on 16 logical CPUs. No GPU model added.
- Synthetic concurrent Wendy/control test PASS: queries, unsupported mutations, cooldown, authentication, reconnect, stale telemetry and inference with ongoing controller frames. Six-second samples: OFF **3.38% of one core / 36.91MiB / ACK p95 9.25ms**; ON before model **5.46% / 45.17MiB / 9.03ms**; inference-inclusive ACK p95 **9.06ms**. Shared development workload, not an isolated FPS benchmark. These measurements preceded the final additional native-write-age fault guard.
- Synthetic USB PASS: malformed/fragmented frames, both triggers, **153.412ms** partial-frame watchdog, reconnect and held-input protection. Synthetic Wi-Fi endpoint protection, reauthentication and neutral re-arm PASS. Tests created null/CSV outputs only, not a live gamepad.
- Physical gamepad/phone and prolonged in-game verification **not run this turn**: phone absent from ADB; paused-game installation/restart confirmation requested but not received at report preparation. Do not present synthetic tests as proof that the reported race incident cannot recur.

Final published binary rerun (including native-write-age guard): concurrent Wendy/control, USB and Wi-Fi tests all PASS. OFF **2.60% of one core / 36.04MiB / ACK p95 9.28ms**; ON **6.22% / 45.20MiB / 9.06ms**, inference-inclusive p95 **9.07ms**. USB partial-frame watchdog **153.103ms**. Default executable hash remained the 0.5.2 `E9EF897E...` hash, confirming no live deployment/restart occurred.

## Artifacts and deployment

New Receiver: `release/receiver-053/PhoneWheel.Receiver.exe`. Local `wendy` junction points to the existing CPU bundle in `release/receiver/wendy` solely for tests; copy the EXE into the standard receiver directory at approved deployment, preserving the model bundle. New APK: `android/app/build/outputs/apk/debug/app-debug.apk`, version 0.5.3 / code15.

Receiver SHA256: `C96E90626D047C136A05E570BF4CEBB8D8B522FE63D851798B44469C8075BCA4`.

**Initial preparation status (superseded by the deployment follow-up below):** no ADB phone was available, so installation/restart was deferred.

## Changed files

Core: `GamepadOutput.cs`, new `CriticalLoop.cs`, `SafetyGate.cs`, `F1RaceState.cs`, `WendyIntent.cs`, `WendyDataQueries.cs`.
Host: `Program.cs`, `PairingWindow.cs`, `CpuIntentModel.cs`.
Tests: new `OutputRecoveryTests.cs`, `Program.cs`, `Wendy052Tests.cs`, `WendyModelTests.cs`.
Android: `MainActivity.kt`, `app/build.gradle.kts`.
Docs: README, WENDY, WENDY_CONVERSATIONS and this report.

Forecast offsets/types checked against [EA F1 25 UDP v3](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf). No inferred 2026 packet layout.

## Connected-device follow-up — 2026-09-20

ADB returned with the user's USB reconnection. Both APKs installed successfully; the default Receiver EXE was replaced with 0.5.3, preserving the CPU model bundle and backing up the old EXE in `release/backups/PhoneWheel.Receiver-before-053.exe`. No normal receiver was running when deployment began. PC Wi-Fi address is now `172.30.1.15`, not the previous address.

The first real microphone/settings isolation run **failed**: the phone disarmed during repeated Options/Wendy panel cycles after 8 microphone starts/ready callbacks with no non-silence voice errors. Focus and sensor were fresh at the logged release, ACK age 8ms, current PC reason User (5). The original PC stop cause was not captured in that run, so this does not prove which fault caused it. Two unchanged-code repeats passed, confirming intermittency rather than proving a fix.

Inspection then found another independently reproducible defect: the sender sampled `now` before reading the sensor snapshot. A sensor publication between those reads could give a negative age and clear the READY bit for one packet, causing a safety release. The UI freshness check used the same unsafe ordering. Both now capture the sensor timestamp/snapshot before sampling the clock. The 100ms freshness limit, rejection of genuinely future/stale data, 150ms receiver expiry and neutral recovery are unchanged. A deterministic concurrency regression failed on the old code and passes on the fix.

Rebuilt Android main/test APKs and lint successfully; **43 unit tests, 0 failures**, Windows **28/28 groups** rerun PASS. Updated main APK installed successfully. SHA256: `FB0392E4F082C08BF126D40E94E24FF34187F2956EC554FE26B8BE90C7229496`. Receiver hash remains as listed above.

First post-fix physical Fold5 null-backend Wi-Fi run: **PASS**, 2,294 ACKs during the measured phase, max sender scheduling gap **16ms**, max received-status gap **64ms**, 8 AUTO microphone starts / 8 ready callbacks / 0 non-silence errors; silence caused automatic restarts. Four AUTO/PTT switches, 12 settings panel cycles and English TTS were observed; same controller client and calibration epoch, armed throughout checks, PC stop/pad-error/ACK-reject counters **0 during operation**. The final Timeout after instrumentation exits is the expected release after stopping the test app, not an in-test failure. On-device recognition exists but English is **not installed**; use AUTO system on this phone.

The harness now retains only PC health lines and failure metrics, never pairing keys or audio/transcripts. Follow-up changed files: `UdpControllerClient.kt`, `MainActivity.kt`, `UdpControllerClientTest.kt`, `ControllerIsolationInstrumentation.kt`, `tools/test_phone_isolation.py`, README and this report. These short real-device tests are not evidence of indefinite Wi-Fi reliability, human speech accuracy or an uninterrupted full F1 race. The initial intermittent failure's exact historical cause remains unproven.

Second post-fix run added an injected actual pedal-view pointer at **50% throttle**, held across AUTO, mode switches, settings panels and TTS. **PASS**: pointer and 50% value maintained, then 0% on release; 2,384 measured-phase ACKs, max send gap **16ms**, max ACK gap **74ms**, 8 starts/8 ready/0 voice errors. PC had **0 in-test releases / 0 pad errors**, and one stale ACK challenge was safely rejected without a release. The receiver was null-output, so no simulated test pedal value reached a live gamepad. After tests, `dev.phonewheel.test` was uninstalled and the normal updated Android app opened. Normal Wi-Fi Receiver was launched with `--wifi --qr --engineer`; scan its new QR before driving, then enable AUTO system in Android Wendy options if desired. No prolonged in-game driving test was performed.
