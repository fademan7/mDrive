# Android 0.5.9 linear 120-degree steering — 2026-10-05

User request: physical phone 0..120 degrees should produce the input previously
associated with 0..180 degrees, symmetrically left/right, with zero at center.

## Changes

- Default estimator and Activity half-range: 120 degrees. Existing formula,
  sign, angle unwrapping, gravity centering and sensor freshness checks unchanged.
- Once per installation upgrade, before connecting: set halfRange=120,
  deadzone=0, smoothing=0, curve=1. No pairing/layout/pedal/Wendy preferences reset.
  Later custom settings remain saved; options include a `Linear 120°` preset.
- Normalized output: 0°=0, 30°=.25, 60°=.5, 90°=.75, 120°=1, clipped beyond 120°.
  This equals a 1.5x mapping against a 180-degree reference, not a guarantee of
  the game's cockpit animation: game steering assists and calibration still apply.
- Version code 21 / version name 0.5.9. No Receiver update is needed for steering.

## Verification

Executed in `android` using bundled JDK17/SDK/Gradle cache:
`gradlew.bat --offline testDebugUnitTest assembleDebug assembleDebugAndroidTest lintDebug`

**BUILD SUCCESSFUL**, 48 unit tests, 0 failures, 0 errors. APK and test APK built;
lint passed. QR scanner deprecation and Android analytics-directory warnings
remain. `git diff --check` passed (line-ending warnings only).

Default tests cover left/right linearity, saturation, return to zero, arbitrary
center poses and gravity centering across display rotations/tilts. Explicit
180-degree boundary/sign and custom 116-degree tests remain intact. Existing
controller dropout and AutoDrive regression tests passed in the same run.

No new live F1 test or sensor measurement was performed. Initial ADB scan had
no device; final scan found model SM-S942N with mDrive installed, different from
the prior Fold5 device. APK installation awaits confirmation of this target.
Current game/receiver processes were not restarted.

Separately, the user's radio-menu confirmation (direction keys then Enter/Right)
was recorded in Wendy trial documentation; no unverified menu recipe was enabled.
