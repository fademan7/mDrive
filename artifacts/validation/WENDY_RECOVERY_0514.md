# mDrive 0.5.14 — Wendy conversation and Android resume

Date: 2026-10-09 (Asia/Seoul).

## Installation and real-device follow-up (same day)

The user paused the game and authorized installation/testing. Both the Android app and `C:\Program Files\mDrive` are now 0.5.14. Installer exit 0, shared ViGEmBus retained, no reboot. The workspace's regular receiver path was also updated with an old-EXE backup in ignored artifacts. No firewall rules were modified.

The first APK used a different default developer keystore and Android refused the update. The existing signing certificate was matched to `.tools/android-user/debug.keystore`; rebuilding with `ANDROID_USER_HOME` set to that directory allowed `adb install -r`. No app uninstall or user-data clearing was performed.

Initial Wi-Fi test launch from the new staging path was blocked by existing path-specific firewall rules. Testing from the existing permitted receiver path received actual Wi-Fi ACKs. A separate test-harness issue initially inspected a destroyed intermediate Activity rather than the final screen; the harness now follows ActivityMonitor during repeated recreation.

The recreation path also exposed a concrete protocol problem: constructing `UdpControllerClient` restarted its sequence at zero, while the PC correctly retained its replay window. Android now generates increasing boot-millisecond-based packet sequences (with an increment fallback for repeated/older clock samples), including after constructing a new client. PC HMAC, replay checks and safety thresholds are unchanged. Phone reboot/very long modular sequence gaps still require a fresh pairing session. MainActivity is singleTask to avoid duplicate task entry; Android saved-state restoration retains pairing only.

Final **real device + real Wi-Fi + null-output receiver** test passed:

- Background Activity recreation observed; foreground authenticated ACK recovery: **1,060 ms**.
- Explicitly closed UDP socket recovery: **2,361 ms**.
- Max send gap during this run: **14 ms**.
- Max ACK gap including the deliberately closed socket: **2,049 ms**.
- 110 expected send exceptions while the injected socket remained closed; the sender continued and recovered.
- Held pedal cleared on background; no old pedal restored on foreground or explicit Activity recreation.
- Pairing/ACKs restored after explicit Activity recreation.
- Initial/final ARM was not observed in the successful test. This is proof of connection/lifecycle recovery, **not a successful driving re-arm or F1 race test**.
- Null receiver reported zero gamepad output errors; this does not exercise ViGEm.
- Final Windows regressions **49/49**; Android unit tests **57/57**. APK/test APK builds passed. Lint passed after singleTask change; it was not rerun after the small packet-sequence helper was added.

Final continuation: `lintDebug` was rerun including the sequence helper and passed. The background-launched normal receiver had its UDP port but no targetable QR window. The verified installed process was restarted through the Windows app launcher. Computer-use accessibility inspection confirmed a visible `mDrive · Wi-Fi & Wendy` window, waiting controller connection, zero output errors, Wendy F1 2025/2026 OFF, PC/PC audio routing, 100% volume and the PC voice selector. No microphone was enabled. Scan this newly launched receiver's QR; prior test/hidden-instance credentials are obsolete.

Normal installed receiver was then launched (QR window) with gamepad output available and Wendy OFF by default. Test session credentials were discarded; user must scan the new normal receiver QR. No microphone capture, voice model additions or real F1 commands were performed. Existing Windows voice architecture is retained at the user's request. Long-race reliability and actual driving re-arm remain unverified.

## Changes

- No unsolicited race invitations when telemetry is absent/stale or the session is paused. Existing telemetry freshness checks remain. Unrecognized incidental speech no longer produces a list of race questions; explicit unknown requests have a cooldown.
- Deterministic radio-check and short English social replies work without telemetry and bypass the optional classification process off track. Supported social topics include greetings, wellbeing, thanks, encouragement, a joke, identity and PC local time. This is bounded rule-based conversation, not an open-domain generative chatbot. Live-session casual requests are redirected briefly to racing.
- Quiet straight-line briefings rotate through gaps, wear, fuel, weather/track temperature, vehicle condition, tyre temperatures and available forecasts. Existing urgent warnings retain priority. Per-topic/global cooldowns and current telemetry checks remain. Repeated close-behind alert cooldown increased from 45 to 120 seconds.
- Wendy activation explicitly identifies F1 2025 / 2026 Season Pack support. No new game telemetry adapter added.
- Removed the Keys dialog and production `WendyShortcutOutput` keyboard injection implementation. No production BB/Diff key mapping remains. Query/advice stays read-only; approvals save manual recommendations when no action output exists. Legacy action-state-machine tests remain as safety regression coverage, not a claim of shipped keyboard functionality.
- Added installed-English PC TTS voice selection, culture/gender labels and persisted choice. No voice cloning or language-pack installation. PC routing and voice volume remain independent of Android routing. Android output uses the phone's TTS settings.
- Default volume 100%; migrate legacy unversioned 85% preferences to 100%, preserve other levels. Future saved settings are versioned.
- Added original vector steering-wheel/m icon to Android, Windows executable/window and installer; deterministic ICO build script included.

## Android recovery

The reported incident was phone app switching, not PC Alt+Tab. Code inspection found that an unresponsive existing UDP socket was retried indefinitely; receive-loop reconstruction only occurred if the socket reference was null. This is a plausible persistence mechanism, **not a confirmed diagnosis of the user's observed incident**.

- Background transition still releases input, stops sensor use and pauses phone voice. No stale throttle or steering is held.
- Foreground resume requests socket replacement when the link has been silent for at least one second. Sustained silence otherwise schedules replacement after two seconds, bounded by the recovery policy.
- Replacement runs on the receiver I/O worker, not the UI/sensor thread. It preserves authentication/session and monotonic sequences, performs HELLO again, and does not bypass the existing neutral/ARM procedure.
- Short jitter does not replace the socket. PC freshness remains 150 ms, ACK checks and neutral/re-arm requirements are unchanged.
- New foreground sensor registration starts without a stale sensor timestamp/pose.
- Activity recreation restores only pairing details from Android's saved state, not ARM, pedals or calibration. A restarted PC receiver with new session credentials still needs QR pairing.
- This provides automatic safe recovery, not a guarantee of instantaneous or interruption-free background driving. Actual Wi-Fi availability and Android scheduling still matter.

## Verification performed

- Windows Release regression: **48/48 groups passed**.
- Android unit tests: **55 tests, zero failures/errors**.
- Android `assembleDebug` and `lintDebug`: passed. Existing deprecated ZXing IntentIntegrator warnings remain.
- New real loopback UDP socket test: held steering/throttle/brake, suppress replies to original endpoint, trigger resume replacement, authenticate on new endpoint, maintain monotonic sequence, background neutral output and no automatic stale ARM — passed. Runtime approximately 1.3 seconds in the recorded run; this is not a real Wi-Fi or F1 measurement.
- Existing simulated controller jitter test: max receive gap 100 ms, zero releases, one ACK rejection. Existing 170 ms outage: neutral at 150 ms, fresh-input recovery at 170 ms. No safety threshold relaxation.
- New conversation tests: radio-check variants without telemetry, off-track social replies, ten simulated minutes without spontaneous prompts, incidental-speech suppression and disabled game setting writes — passed.
- New briefing tests: multiple fresh topics, race/chat transitions and stale suppression — passed.
- Four PC/Android audio routes and LB game-radio priority regressions — passed.
- Self-contained receiver publish and Inno installer compile — passed.
- Published real Vosk worker: model load, idle epoch changes and EOF shutdown passed without opening microphone.
- Published voice probe: bundled model installed; one English output voice available on this PC. Both requested accent/gender choices cannot currently be offered together without additional installed voices.
- `git diff --check`: no whitespace errors (line-ending notices only).

## Artifacts

- PC installer: `release/installer/mDrive-0.5.14-Setup-x64.exe`
  - SHA256 `091BC44B7539C4894DAAF6FE62EAA3801F907E8CCE907E980351742FF1C107DD`
- Android APK: `android/app/build/outputs/apk/debug/app-debug.apk`
  - Final installed APK SHA256 `6CB84F53D5DC5AAEFCABC1F268047CFA3248C3639F9A5D2ECE930D77B8E06BFF`

## Outstanding at the earlier build-only checkpoint (superseded by follow-up above)

No APK installation, current receiver replacement, microphone recording, Windows voice installation or real F1 input was performed in this pass. Installation/app-switch testing was requested but no confirmation had arrived at report time. Real-device Activity recreation, app switching and long-run F1 recovery remain **not yet confirmed**. The new voice selector's appearance/actual playback selection was not visually or audibly tested. Installer build succeeded; install/uninstall smoke testing for this version was not repeated (0.5.13 had passed that test). Application and installer remain unsigned previews.

No commit, push or publication was performed. Existing unrelated work was preserved.
