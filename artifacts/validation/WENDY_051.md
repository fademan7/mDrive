# Wendy 0.5.1 — 2026-09-19

## Scope / diagnosis

The user reported tyre temperature not understood, hands-free not working, and controller interruptions during speech/settings. Code inspection confirmed that old modal Options/Wendy settings called `releaseInputs`, stole window focus, and restarted the Wendy link on focus changes. It did not prove the radio itself disconnected. The old automatic mode forced on-device recognition while working PTT used the system provider; it also did not enable Wendy when only automatic mode was selected.

Phone configuration showed GoogleTTSRecognitionService as the PTT provider. No current disconnection/STT-error logs were available. A historical Sep 12 crash was unrelated to this build and was not used as its diagnosis. On-device service availability was subsequently tested through Android APIs rather than inferred from a null secure setting.

## Changes

- Options/Wendy are in-window panels, preserving controller client, touch ownership, armed state and calibration epoch. Real background/permission/focus loss and steering/layout edits retain release protections.
- Wi-Fi latency lock for connected foreground Wi-Fi only; higher-priority controller I/O threads; catch/count scheduled sender exceptions so one error cannot stop all subsequent sends. ACK/send gap counters. No freshness/watchdog relaxation or steering/pedal mapping changes.
- Shared UI/sensor state visibility made explicit with volatile fields.
- System/on-device automatic English recognition options, explicit network-provider disclosure, auto-enable Wendy, permission-result retry, foreground suspension recovery, Retry button and metadata-only diagnostics. No raw audio capture/storage by mDrive; the selected system provider may use its own online service. Actual repeated microphone recognition remains a user voice test.
- English UI throughout app and receiver, including app-specific locale for QR UI. Existing saved control layout/response preferences preserved.
- GET_TYRE_TEMPERATURE, GET_TYRE_PRESSURE, GET_TYRE_AGE, GET_BRAKE_TEMPERATURE, GET_ENGINE_TEMPERATURE, GET_LAPS_REMAINING. Specific wheels/units, current versus setup pressure and stale-field checks. Game commands remain unsupported; no unsafe menu automation.

## Verification so far

- .NET: 23/23 groups PASS. Release self-contained Windows publish succeeded.
- CPU classifier: 43/43 representative phrase tests PASS, including existing damage/gap queries. Not a general-English or microphone-accuracy measurement.
- Android: 42 unit tests pass, including scheduled sender survival after a synthetic callback exception. App and development instrumentation APKs build successfully. Initial compilation failed on a nested Button receiver used as Context; fixed explicitly with `this@MainActivity` before deployment.
- Actual Fold5 Wi-Fi first trial: 12 Options/Wendy panel cycles and a TTS request, same controller client, 494 ACKs, max send gap 16ms, zero sender failures. Null-backend receiver only; no gamepad output or microphone recording.
- Synthetic USB: fragmented/malformed/independent pedal/watchdog/reconnect/rearm PASS, watchdog 152.892ms.
- Synthetic Wi-Fi: active endpoint protection/stale reauthentication/held-input block/neutral rearm PASS.
- Wendy + controller integration: OFF/ON, ten queries, plans/unsupported mutations, authentication/freshness/reconnection/AUTO alert arbitration, actual CPU inference with controller active PASS.
- 6-second synthetic metrics: OFF Receiver 7.53% of one core / 37.56MiB / ACK p95 9.04ms; ON before model 9.28% / 44.56MiB / 8.95ms; inference-inclusive ACK p95 9.04ms. Build/test activity was concurrent; not an isolated performance benchmark. CPU classifier trial 193.4% of one core, idle 4.69%, working set 957.2MiB (16 logical CPUs).

## Deployment

### Final physical-device verification

On the final build, the stronger Fold5 test also asserted armed state/calibration epoch preservation before and after each panel cycle and speech, and observed actual `SPEAKING` state. **PASS**: 12 panel cycles, 492 ACKs, max sender gap **11ms**, max received ACK gap **58ms**, zero sender failures, same controller client, armed throughout. Null output only: no gamepad actuation, microphone recording or game driving. These bounded maxima are not a guarantee for all future radio conditions.

Android `checkRecognitionSupport(en-US)` returned **onDeviceEnglishInstalled=false**, while `isOnDeviceRecognitionAvailable=true` and system recognition availability=true. This explains the old on-device-only mode failing despite PTT working. **Always Listening: system** now explicitly selects the working PTT provider; use it unless an English on-device pack is installed. Actual hands-free user speech is still not exercised by the no-recording test.

Final Android build/lint succeeded: **42 tests**, **0 errors / 43 warnings** (including existing/development UI hardcoded-string warnings). Static scan found no Hangul in app-owned Android runtime UI or Receiver source. App locale is English; system-wide language is unchanged. The separate `dev.phonewheel.test` APK is removed after verification and can be recreated from androidTest sources.

Android 0.5.1 versionCode 13. Windows default executable replaced with the published result; SHA256 `D7286D8F10422C5E6ECFF4733BF2597397249EE6DCF1C3DFC54023DD2988DF2A`. Old binary backed up to `release/backups/PhoneWheel.Receiver-before-051.exe`. CPU bundle unchanged. The user approved app installation and receiver restart with game paused.

Final normal receiver PID 139844: `PhoneWheel · Wi-Fi QR connection`, Responding=true; 172.30.1.77 UDP26760/TCP26762 and loopback UDP20777 confirmed. Phone main app PID 30457, versionCode13/versionName0.5.1 confirmed. Test APK uninstall returned Success; only the temporary development package was removed. Scan the newly generated QR; the previous pairing is no longer current.

## Changed files

Android: MainActivity.kt, WendyVoice.kt, UdpControllerClient.kt, PedalTouchView.kt, new ControllerWifiLease.kt and PhoneWheelApplication.kt, AndroidManifest.xml, build.gradle.kts. Tests: UdpControllerClientTest.kt; new androidTest ControllerIsolationInstrumentation.kt (separate development APK, no microphone recording).

Core: F1RaceState.cs, WendyIntent.cs, WendyEngineer.cs. Host: CpuIntentModel.cs, PairingWindow.cs, Program.cs, UsbConnector.cs. Tests: WendyNextTests.cs, WendyModelTests.cs, Program.cs. Tools: new test_phone_isolation.py. README/WENDY docs and this report.

## Sources / limitations

- [Android Wi-Fi lock API](https://developer.android.com/reference/android/net/wifi/WifiManager.WifiLock): OS request, not a guarantee of continuous radio delivery.
- [Android speech recognizer API](https://developer.android.com/reference/android/speech/SpeechRecognizer): system provider may stream to its servers; continuous recognition has battery/bandwidth implications. User selects the mode explicitly; no silent cloud fallback from on-device.
- [EA F1 25 UDP v3](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf): telemetry car record offsets 22 brake temperatures, 30 surface, 34 inner, 38 engine, 40 pressures (60 bytes/car).

No claim of never-disconnecting Wi-Fi, long-term speech-provider uptime, F1 FPS, or physical input-to-game latency. Real speech recognition during driving and extended wireless use still need user testing. First/second lap coaching remains the conservative reference comparison from 0.5.0, not a complete strategy/vehicle-dynamics model.
