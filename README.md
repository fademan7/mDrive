# mDrive

Turn your Android phone into a wireless controller for Windows racing games. Rotate the phone to steer, use independent touch pedals for braking and acceleration, and feel standard Xbox rumble on your phone.

**Wendy F1 Engineer** adds English voice questions, telemetry-based race updates, and driving guidance for **F1 25 / 2026 Season Pack**. Wendy is optional: the controller works without it.

**Current version: 0.5.17 Preview** · Windows x64 + Android 13 or later · Wi-Fi only · **GPL-3.0-only**

[Download](https://github.com/fademan7/mDrive/releases/tag/v0.5.17) · [한국어 안내](README.ko.md) · [Release notes](docs/RELEASE_0517.md) · [License](LICENSE)

## Features

- Linear motion steering: **90° phone rotation reaches full Xbox Left Stick X** (Android 0.5.18).
- Full-height left brake and right throttle surfaces with independent multitouch input.
- D-pad, ABXY, LB/RB, Back/Start, and a right analog stick for camera control.
- Adjustable button positions and sizes, steering response, and pedal settings.
- QR pairing over a local Wi-Fi network; no USB connection or ADB needed for driving.
- Game-generated Xbox vibration relayed to the phone, independent of F1 telemetry or Wendy.
- English race assistance with offline PC speech recognition and text-to-speech.
- Independent PC/Android microphone and voice-output selection, Wendy volume, and available PC voice selection.

mDrive appears as a **virtual Xbox 360 gamepad**, not a dedicated force-feedback racing wheel. Gamepad support, steering assistance, and vibration behavior depend on the game.

## Download and install

Use the release's recommended PC receiver and Android app versions. The Android-only 0.5.18 steering update is compatible with the existing 0.5.17 receiver; it does not change the protocol or require a receiver restart.

| Download | Contents |
|---|---|
| [Windows x64 installer](https://github.com/fademan7/mDrive/releases/download/v0.5.17/mDrive-0.5.17-Setup-x64.exe) | Receiver, offline English Vosk model, and official ViGEmBus prerequisite installer |
| [Android APK](https://github.com/fademan7/mDrive/releases/download/v0.5.17/mDrive-0.5.17.apk) | Non-debuggable mDrive app, signed with the permanent release key |
| [SHA-256 checksums](https://github.com/fademan7/mDrive/releases/download/v0.5.17/SHA256SUMS.txt) | Integrity checks for both downloads |

1. Exit the game and close any running mDrive receiver before installing the Windows package. Administrator approval may be required. Follow any driver restart request.
2. Install the APK on your phone. An update must use the same signing key or a valid signing-key rotation proof; if Android reports a signature mismatch, do not erase the app's data just to retry.
3. Launch **mDrive** on the PC and phone, then follow the pairing instructions below.

The Android APK uses a **permanent release signing key**, not a debug key. The Windows installer is still **not Authenticode-signed**. This remains a preview; signing does not guarantee stability or eliminate platform security checks. Do not bypass OS security warnings or disable security/anti-cheat protections.

Official APKs include Android's proof of signing-key rotation from the previously published debug certificate. Do not uninstall the old app to update: installation should preserve its data on supported Android versions. Independently debug-signed builds do not share this identity. See [signing, migration, and key custody](docs/ANDROID_SIGNING.md) for the public certificate fingerprint and verification limits.

### Requirements

- **PC:** Windows x64 with ViGEmBus for virtual Xbox output. Development and device testing used Windows 11 x64. The installer includes the official driver installer and keeps an existing installation. Uninstalling mDrive does not remove this shared driver.
- **Phone:** Android 13 (API 33) or later with a suitable rotation sensor. Device testing has included Galaxy Z Fold5 and SM-S942N; this does not guarantee compatibility with every phone.
- **Network:** PC and phone on the same local network with device-to-device communication allowed.
- **Optional Wendy audio:** A microphone and an English TTS voice on the selected device. PC recognition includes its own offline model; a Windows speech-recognition language pack is not required. PC speech output still needs an English Windows TTS voice available to the app.

**ViGEmBus is end-of-life.** Future Windows and anti-cheat compatibility is not guaranteed. See the [third-party notices](installer/THIRD-PARTY.txt).

## Connect and start driving

1. Run **one** PC receiver. The installed app opens a QR window; source builds also provide `START_RECEIVER.cmd` and `START_WIFI.cmd`.
2. Connect the phone and PC to the same local network. Guest Wi-Fi, AP isolation, or VPN routing can prevent pairing.
3. If Windows Firewall asks, allow the receiver on your trusted **private network**. Do not disable the firewall or expose these ports to the internet.
4. On the phone, open **mDrive → QR connection** and scan the PC's QR. Grant camera/local-network permissions if requested.
5. Hold the phone in a landscape driving position with its long edge level. Release the pedals and let automatic centering finish. Wait for **Driving active** before driving.
6. Start the game, select the Xbox controller, and verify steering, brake, and throttle in its calibration screen.

Restarting the receiver creates a new session: **scan the new QR**. If the PC's network address changes, restart the receiver and pair again. Keep QR codes and pairing keys private.

| Purpose | Default endpoint |
|---|---|
| Controller input and feedback | PC LAN address, UDP **26760** |
| Wendy requests and responses | TCP **26762** |
| F1 telemetry | PC loopback, UDP **20777** |

USB is not a game-control transport. USB debugging is only useful for developer installation and device tests.

## Controls and calibration

| Phone control | Xbox output |
|---|---|
| Phone rotation | Left Stick X — steering |
| Left pedal | LT — brake |
| Right pedal | RT — throttle |
| Lower-center analog stick | Right Stick — assign camera/look controls in the game |
| D-pad, ABXY, LB/RB, Back/Start | Corresponding gamepad buttons |

Android 0.5.18 defaults to linear Xbox Left Stick X input: **0° = 0%, 45° = 50%, 90° = 100%**, mirrored in the opposite direction. At 120° and 180° the stick remains at 100%. Quaternion sensor tracking continues through ±180° independently; only the output is saturated. Phone rotation does **not** specify the car's steering-wheel angle. No speed- or telemetry-based correction is applied by mDrive.

The first 0.5.18 launch applies this preset once (curve 1, deadzone 0, smoothing 0), without changing pairing, pedals, layout, or Wendy settings. Later custom steering settings are retained. Use **mDrive → Steering / pedals → F1 Linear 90°** to restore it. The driving display shows accumulated **Phone** angle and normalized **LX** (-1.00 to +1.00) together. Published Android 0.5.17 uses the previous 120° default.

For the pedals, the point 10% above the bottom is 0%, and the point 10% below the top is 100%. The usable middle section is linear, giving fingers room at both ends. Brake and throttle can be held together.

Use **Control layout** to adjust button positions and sizes. The controller layout remains visible without a Race/Menu switch. Pause the game before editing settings or layouts. Automatic centering runs at startup; use **Center** only when you need to correct the neutral position.

Check axis direction, full range, and simultaneous pedal input in the game. Enable automatic shifting/clutch assists in the game if needed. A game's speed-dependent gamepad steering assistance can make the cockpit wheel animation differ from the phone's angle, even when mDrive's output is linear.

### Vibration

Enable controller vibration in the game. mDrive forwards **standard Xbox motor rumble** through the receiver to the phone; Wendy can remain off, and no F1 telemetry is required.

The receiver's **Rumble: Xbox events → phone packets** counters help distinguish missing game feedback from transport problems. Dedicated wheel force feedback and impulse-trigger effects are not interpreted. Not every game or device produces the same sensation.

### Connection recovery and safety

Controller sending, receiving, gamepad output, and Wendy processing are separated. Inputs older than the **150 ms freshness limit** are neutralized; the **100 ms authentication freshness check** remains in place.

A recoverable network interruption within the same active driving session can resume from **new authenticated input** without a manual neutral dwell. Old throttle or steering is not replayed. Sensor, focus, calibration, or output faults still require safe reactivation.

Switching to another phone app releases controls rather than driving in the background. When you return, mDrive checks the connection and can reopen an unresponsive socket; release the pedals and hold the phone level if safe re-arming is requested. Receiver restarts still require a new QR scan. Instant recovery from every failure and dropout-free long races are **not guaranteed**.

## Wendy F1 Engineer

Wendy supports **F1 25 / 2026 Season Pack telemetry**, not other games' telemetry. The ordinary Xbox controller can still be used in other compatible games.

### Configure F1 telemetry

Run F1 and the receiver on the same PC. In F1's Telemetry Settings, use:

| Setting | Value |
|---|---|
| UDP Telemetry | **On** |
| UDP Broadcast Mode | **Off** |
| UDP IP Address | **127.0.0.1** |
| UDP Port | **20777** |
| UDP Send Rate | **30 or 60 Hz** |
| UDP Format | **2025** |

Use the **2025** format for the 2026 Season Pack as well; there is no separate 2026-format parser. Enable **Wendy · F1 2025 / 2026** in the receiver and enter a session. Check for **Telemetry: Connected**. Wendy is off by default when the receiver starts; controller operation does not depend on it.

### Set up voice

1. Pair the phone, then select **Audio check** in the PC QR window.
2. Choose **Microphone** and **Voice output** independently: PC or Android. Both default to **PC**, making a headset convenient for game audio and Wendy together.
3. Set the headset as the Windows default microphone/playback device when using PC audio. Select an installed English voice under **PC voice**. British male or American female voices are selectable only if installed and exposed to the app; no additional character voices are bundled.
4. Adjust **Wendy volume** from 0–100%; the default is **100%**. Balance the game's volume in Windows if needed. There is no automatic game-audio ducking or amplification above 100%.
5. Enable Wendy and say **“Radio check.”** It should respond even without a running game. Live car-data questions require fresh telemetry.

PC recognition uses the bundled `vosk-model-small-en-us-0.15` in a separate low-priority CPU process. It does not record audio to files, upload audio, use GPU inference, or require an online speech service. If you copy a standalone receiver build, keep its `speech` folder with it.

Android input uses the system's English speech-recognition service, which may use its provider's online service. Grant microphone permission when requested; **mDrive → Wendy → Retry voice** restarts that voice service, not the controller session. Android output also depends on the phone's media volume and TTS configuration.

Audio routing, volume, and PC voice preferences are stored in `%LOCALAPPDATA%\mDrive\audio.json`. Raw audio is not streamed between the PC and phone; recognized requests and responses are exchanged. Wendy pauses recognition while speaking to avoid hearing itself.

### What you can ask

| Topic | Examples |
|---|---|
| Connection and short conversation | “Radio check.” “Audio check.” “Hello.” “Thanks.” |
| Gaps and race state | “Tell me the time gap.” “What's the gap behind?” “Race update.” “What's my position?” |
| Tyres | “How are my tyres?” “Tyre temperature.” “What about the rear left?” |
| Fuel and energy | “How much fuel do I have?” “What's my ERS?” |
| Damage and pit advice | “Any damage?” “Front wing damage.” “Do I need to pit?” |
| Weather and flags | “Is it going to rain?” “What flag is out?” |
| Laps and guidance | “What lap am I on?” “How was my last lap?” “Coach me.” |

Answers are intended to be short and relevant. “Say again” refreshes the previous information query rather than repeating a setting command. Follow-up context expires after 30 seconds or a session/reconnection change.

Wendy gives important flag, safety-car, damage, wear, and fuel alerts with cooldowns. During suitable quiet driving sections it can offer varied race briefings. Without fresh active telemetry, it does not prompt you for racing questions. Off-track conversation is limited to supported short exchanges; Wendy is **not a general-purpose conversational LLM**.

The HUD can show flags and useful summaries such as pit reminders, damage, rain forecasts, and estimated tyre life. Tyre life uses recent clean-lap wear trends and displays a range only when confidence is sufficient. It is a planning estimate, not a puncture prediction or optimal strategy. Lap coaching starts after a valid reference lap; setup guidance uses repeated feedback and conservative conditions, not a single event.

See the [conversation reference](docs/WENDY_CONVERSATIONS.md) for more phrases. Historical design documents may describe experiments that are not enabled in the current release; the support limits below take precedence.

### Game radio and actual commands

**Wendy does not currently operate F1's pit request, next-stop tyres, front wing, brake bias, or differential settings.** “Box box” records a reminder; it does not send a pit-request command. Setup suggestions are advice, not automatic changes.

For actual radio commands, configure **LB** as the radio-menu button in F1 and use the game's own voice recognition with your PC microphone. Wendy stops listening/speaking while LB is held and for **8 seconds after release**. This is a timed reservation, not game-menu detection; hold or press LB again if you need more time.

LB still sends its normal gamepad input. It does not start Wendy PTT, inject keyboard T, or stream the phone microphone into F1. OCR, screen capture, blind menu macros, and production BB/Diff keyboard automation are not used.

### Optional developer intent model

The standard installer works with rules and the bundled speech recognizer; no extra language model is required. Known requests use fast rule-based matching.

For source-build experiments, `tools/prepare-wendy-model.ps1` can download a hash-checked llama.cpp CPU runtime and Qwen3 0.6B Q8_0 intent classifier into `release/receiver/wendy`. This is an **optional local CPU model**, not unrestricted chat, and is not included in the standard installer. Keep its notices with it. It does not use Ollama, GPU inference, or the OpenAI API.

## Troubleshooting

| Symptom | Check |
|---|---|
| No Wi-Fi reply | Same LAN, current QR, running receiver, private-network firewall permission, AP isolation/VPN routing |
| Phone input appears but the game does not respond | ViGEmBus installation, game's Xbox controller selection, duplicate receivers/remappers, receiver Game output / Stops / Pad errors |
| Controls release during a race | Pause the game; inspect phone Controller diagnostics and the receiver's release reason. Do not increase timeouts or preserve stale throttle to hide faults. |
| Telemetry: Waiting | Active F1 session, format 2025, loopback address and port 20777, another program holding the port |
| Wendy cannot hear or speak | Wendy enabled, Audio check, selected audio route, default headset, Vosk model, English TTS, volume and permissions |
| Android AUTO repeatedly errors | System English recognition availability, microphone permission, provider connectivity if needed, then Retry voice |
| No vibration | Game's Xbox vibration setting, receiver rumble counters, active phone connection, device vibration support |
| Update APK will not install | Android version and signing-key compatibility; avoid deleting settings before identifying the cause |

Keep pairing secrets out of screenshots and shared logs. See [controller diagnostics and recovery](docs/CONTROLLER_DROPOUT.md) for incident recording and troubleshooting.

## Build from source

End users can use the downloads above without development tools. For builds, install **.NET SDK 10.0.400**, **JDK 17**, **Android SDK API 37 / Build Tools 37.0.0**, and Platform-Tools for optional USB deployment. The repository pins Gradle **9.5.0** and Android Gradle Plugin **9.3.2**.

```powershell
git clone https://github.com/fademan7/mDrive.git
cd mDrive
```

### Windows receiver and installer

From the repository root:

```powershell
.\tools\build-app-icon.ps1
.\tools\publish-receiver.ps1 -OutputDirectory release/distribution-0517
.\tools\prepare-speech-assets.ps1 -OutputDirectory release/distribution-0517
.\tools\prepare-distribution-notices.ps1 -OutputDirectory release/distribution-0517
.\tools\build-installer.ps1 -Compiler 'C:\path\to\Inno Setup 6\ISCC.exe'
```

The receiver is self-contained; end users do not need the .NET SDK. The installer output is `release/installer/mDrive-0.5.17-Setup-x64.exe`. Packaging was tested with Inno Setup 6.7.3. Check the compiler's license for your distribution/use. The preparation scripts verify downloaded model/driver hashes and the official driver's signature without installing the driver.

For just the receiver, run `tools/publish-receiver.ps1`; its default output is `release/receiver/PhoneWheel.Receiver.exe`. That EXE alone does not include the speech-model folder or install ViGEmBus.

### Android APK

Set paths for your own environment:

```powershell
$env:JAVA_HOME = 'C:\path\to\jdk-17'
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
cd android
.\gradlew.bat assembleDebug
cd ..
```

Output: `android/app/build/outputs/apk/debug/app-debug.apk`, for local development only, **not public distribution**. Preserve your signing key for updates; a fresh debug key cannot update an app signed by a different key. Never commit signing keys.

Official release builds use `tools/build-android-release.ps1` and the maintainer's external signing vault. This runs release tests/lint, signs with the permanent key and migration lineage, and verifies the certificate pin and non-debuggable flag. Only `release/android/mDrive-<version>.apk` is distributed. Missing signing credentials fail the release build instead of falling back to debug signing. Forks can use their own signing identity and package name; see [release signing](docs/ANDROID_SIGNING.md).

Optional developer deployment, with USB debugging enabled and authorized:

```powershell
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices
& "$env:ANDROID_HOME\platform-tools\adb.exe" install -r .\android\app\build\outputs\apk\debug\app-debug.apk
```

### Tests and validation

```powershell
dotnet run --project windows/tests/PhoneWheel.Tests/PhoneWheel.Tests.csproj -c Release
cd android
.\gradlew.bat testDebugUnitTest assembleDebug lintDebug
cd ..
python verification/run_verification.py
```

Recorded validation for 0.5.16: **49 Windows regression groups**, **57 Android unit tests**, Android build/lint, and successful installation on the development devices. Actual Xbox → receiver → Wi-Fi → phone rumble was tested, and the user confirmed all three intensity levels. These results are **not proof of dropout-free long F1 races**, universal game compatibility, or recognition accuracy for every accent/headset.

Hardware scripts require an explicitly prepared device and a stopped game; they are not part of normal installation. See the [rumble validation](artifacts/validation/XINPUT_RUMBLE_0516.md), [recovery validation](artifacts/validation/WENDY_RECOVERY_0515.md), [hardware test plan](docs/HARDWARE_TEST_PLAN.md), and [protocol](docs/PROTOCOL.md).

## Credits

Created by **fademan7 / neojshin**.

[Website](https://fademan7.github.io/) · [Email](mailto:neojshin@gmail.com) · [Third-party notices](installer/THIRD-PARTY.txt)

## License

mDrive is free and open-source software under the **GNU General Public License version 3 only (GPL-3.0-only)**. See [LICENSE](LICENSE) and [COPYRIGHT](COPYRIGHT). Redistribution of covered modified versions must comply with the GPL, including its corresponding-source requirements. The software is provided without warranty.

Third-party software and models retain their own licenses and notices; this license does not replace theirs. Release binaries live in GitHub Releases alongside access to the matching tagged source and build scripts. Android APKs include the project license in assets, and Windows installations include it in the notices directory.
