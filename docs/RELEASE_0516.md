# mDrive 0.5.16 preview

## Downloads / 설치

- `mDrive-0.5.16-Setup-x64.exe`: Windows x64 receiver, offline English Vosk recognition model, and official ViGEmBus prerequisite installer.
- `mDrive-0.5.16.apk`: Android 13+ application, development/debug signed.
- `SHA256SUMS.txt`: checksums for both downloads.

Update both PC and phone. Exit the game and receiver before installing. Connect both devices to the same Wi-Fi, launch mDrive on PC, then scan its QR from the Android menu. USB is not used for game control. See the repository README for game mapping, firewall, English TTS, and F1 UDP configuration.

This is an unsigned Windows / debug-signed Android preview, not a production-signed distribution. Do not disable security or anti-cheat protections. ViGEmBus is end-of-life; clean-PC and game compatibility still need verification.

## Included changes

- Standard Xbox rumble forwarded over Wi-Fi, independently of Wendy/F1 telemetry. Corrected premature expiry of sustained vibration and missed short pulses; added receiver rumble diagnostics.
- Wi-Fi recovery uses fresh authenticated inputs without replaying old throttle/steering. Existing 150 ms input expiry and 100 ms authentication limits remain; sensor/focus/output faults still require safe recovery.
- Wendy PC microphone/output defaults, bundled offline English recognition, output volume and installed English voice selection; limited conversational intents and telemetry-based race assistance.
- Wi-Fi-only controller, updated layout and default linear steering range of +/-120 degrees. LB reserves the game's own radio input and temporarily pauses Wendy.

Wendy is for F1 2025 / 2026 Season Pack telemetry. It is not an unrestricted chat LLM. Automatic Box/tyre/wing actions, BB/Diff keyboard automation, and OCR are not supported in normal operation; use the game's radio commands.

## Verification and limits

- Windows: 49 regression groups passed.
- Android: 57 unit tests, APK build and lint passed.
- Actual Xbox -> PC -> Wi-Fi -> Android rumble: all three levels felt and confirmed by the user. Repeat test: 119 accepted haptic packets, zero rejected, max control send gap 13 ms, zero controller releases/output errors.
- Installer and APK installed successfully on the development devices.

These results do not establish that every game's rumble works or that long F1 races are dropout-free. See `artifacts/validation/XINPUT_RUMBLE_0516.md` and the recovery validation reports for measured results and remaining limitations.
