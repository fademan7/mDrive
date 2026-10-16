# Android 0.5.18: phone rotation to Xbox Left Stick X

Validated on 2026-10-16. This is an Android-only mapping change, not a vehicle steering-angle calibration.

## Contract

Default output is `clamp(clockwisePhoneDegrees / 90, -1, 1)` with response curve 1, deadzone 0 and smoothing 0. Both directions are symmetric: 45 degrees gives half stick, and 90, 120 and 180 degrees give full stick. The car's wheel animation and speed-dependent steering assistance remain entirely game-controlled.

The existing relative-quaternion twist, shortest-delta accumulation, quaternion hemisphere handling and sensor continuity protections are unchanged. Accumulated angle is never limited by the configurable output full-scale angle. Curves, deadzone and optional smoothing remain available.

The first upgraded launch applies the 90-degree linear preset before creating a controller session. Later steering customizations persist. Pairing, pedal, layout and Wendy preferences are untouched. The driving HUD displays clockwise accumulated phone angle and normalized LX together, before receiver safety gating.

Controller packets, sender scheduling, Wi-Fi transport, ACK checks, SafetyGate, recovery, GamepadWorker and ViGEm output are unchanged. The receiver still quantizes normalized steer directly to Xbox LeftThumbX. No telemetry or speed input is introduced.

## Verification

- Android release unit tests: 61 passed, 0 failures/errors.
- New tests: requested mapping through both existing packet kinds with held pedals; dense ±190-degree round trip with alternating quaternion hemispheres; 60 simulated seconds per direction held around 180 degrees plus 90-degree threshold jitter; independent output ranges and response curves.
- Existing arbitrary-center-pose and gravity-centering tests updated only for the default output scale; sensor continuity and controller recovery tests retained.
- Windows regression: 49/49 groups passed, including jitter/loss, fresh-input recovery, stale input rejection and output-error handling. These are synthetic tests, not F1 driving results.
- Signed release build succeeded; permanent release certificate verified. Lint: 0 errors, 81 warnings.
- Build command: set the repository's JDK/Android SDK/Gradle environment paths, then run `tools/build-android-release.ps1 -BuildMigrationTest`. This selects the release test variant; the helper's default invocation did not expose `testReleaseUnitTest` with the current Gradle configuration. The optional test APK was built but not installed.
- Android 0.5.18 (versionCode 29) installed successfully using `adb install -r`, without uninstall/data reset. Cold launch returned `Status: ok`; installed version verified.

Actual physical rotation accuracy, on-device HUD appearance and live F1/XInput behavior have **not yet been verified** for this build. During a safe calibration-screen check, compare Phone 45/90/120/180 degrees with LX 0.50/1.00/1.00/1.00, then return to zero and repeat left. Check the game's input-axis display, not cockpit-wheel animation. No receiver restart, live game input injection or public release upload was performed.
