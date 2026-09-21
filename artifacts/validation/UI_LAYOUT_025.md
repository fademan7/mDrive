# 0.2.5 UI layout preview — 2026-09-15

## Scope

- User requested layout review before adding voice pit-crew functionality.
- D-pad and ABXY groups moved slightly apart, preserving their circular/square-sized touch controls.
- Added bottom-center local analog-stick preview. One pointer owns it; radial clamping, release/cancel recentering, and non-finite coordinate rejection are tested. Existing pedal/button targets take precedence over the preview. Preview never adds RX/RY to ControllerSnapshot or PWR1.
- Added reserved `PIT CREW · INFO / 음성 기능 미연결` area between Center and Options. No voice implementation or microphone permission.
- Existing pedal widths, steering gain/response, independent pedal mapping, transport, receiver, neutral re-arm and haptic behavior unchanged.
- One-time layout migration backs up saved layout to `layout_before_look_v5`; only the eight D-pad/ABXY positions and sizes are updated. Preview stick is fixed in this iteration; existing buttons retain their move/resize editor.

## Local validation

Executed from `android` with the workspace JDK 17, Android SDK and Gradle cache:

```powershell
$env:JAVA_HOME='C:\paper\mdrive\.tools\jdk17\jdk-17.0.20.1+1'
$env:ANDROID_HOME='C:\paper\mdrive\.tools\android-sdk'
$env:ANDROID_USER_HOME='C:\paper\mdrive\.tools\android-user'
$env:GRADLE_USER_HOME='C:\paper\mdrive\.tools\gradle-home'
.\gradlew.bat --offline testDebugUnitTest assembleDebug lintDebug
```

- BUILD SUCCESSFUL, including a final repeat after the last geometry change.
- JUnit: 32 tests, 0 failures/errors (AutoDrive 8, Core 14, LookStickPreview 2, PairingDetails 3, UdpControllerClient 5).
- Android lint: 0 errors, 20 warnings. Resource-string/localization and existing deprecated QR API warnings remain.
- APK: `android/app/build/outputs/apk/debug/app-debug.apk`, versionName 0.2.5 / versionCode 7.
- Conversation preview is an independent HTML layout approximation, not a device screenshot. At narrow conversation widths its groups stack for readability; native app remains landscape.

## Not run / installation blocker

ADB access was rejected because automatic approval review hit its usage limit. No alternate device-access route was attempted. 0.2.5 is **built, not installed**; no new phone screenshot, physical multi-touch or game test is claimed. Previously tested driving version was 0.2.4.

Once authorized device access is available, install the APK as an update, inspect the landscape layout, confirm pedal widths and saved driving settings, and test two pedals plus the preview stick without false game-button input. Restart the receiver for a fresh session if the app process restarted. Do not remove app data. Actual look-axis transmission and voice pit crew remain a later implementation step after layout feedback.
