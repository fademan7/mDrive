# mDrive 0.5.18 Preview — Android steering update

This release updates **Android only**. Keep using the [Windows 0.5.17 installer](https://github.com/fademan7/mDrive/releases/download/v0.5.17/mDrive-0.5.17-Setup-x64.exe). Existing PC installations do not need an update or restart.

## Install

Download [mDrive-0.5.18.apk](https://github.com/fademan7/mDrive/releases/download/v0.5.18/mDrive-0.5.18.apk) on your Android 13+ phone and open it to update mDrive. Pause the game first; installation restarts the app. Use Android's install permission for your chosen browser/file manager if prompted, and review system security warnings. Do not uninstall the existing app or clear its data.

The APK is non-debuggable and uses the same permanent release signing key as 0.5.17, including the existing signing-key rotation proof for earlier official builds. A data-preserving update and cold launch were verified on the connected test phone.

## Steering changes

Phone rotation maps to **virtual Xbox Left Stick X**, not the car's steering-wheel angle:

| Phone rotation | Xbox LX |
|---|---|
| 0° | 0% |
| ±45° | ±50% |
| ±90° | ±100% |
| ±120° | ±100% |
| ±180° | ±100% |

Quaternion tracking continues independently through ±180° and beyond. Only controller output saturates. The first launch applies the linear F1 preset once: full stick at 90°, response curve 1, deadzone 0, smoothing 0. Other settings remain unchanged, and later steering customizations are retained.

The driving display shows accumulated **Phone** angle and normalized **LX** (-1.00 to +1.00). There is no speed/telemetry steering correction. F1's own gamepad assistance still determines cockpit-wheel animation and vehicle steering.

Controller packets, Wi-Fi, safety/recovery, and PC ViGEm output are unchanged.

## Verification and limits

- Android release tests: 61 passed.
- PC regression: 49/49 groups passed.
- Release build and certificate verification passed; lint: 0 errors, 81 warnings.
- ±190° round-trip, hemisphere flips, threshold jitter, saturation and packet preservation tested synthetically.
- APK update installation and cold launch verified. Physical steering accuracy and live F1 driving for this build are **not yet verified**.

See [detailed validation](STEERING_90_VALIDATION.md). This remains a preview.

## Integrity

APK SHA-256: `f1d7527b8f789c0c0aad06e7d87d9d224f3d0a4603e8d782c4efc86dcf2a7c82`

Release certificate SHA-256: `802297e5be0a38fa84db19cea49f664c9be8895efb1b879f69d72ac447e7dcd5`

[Download SHA256SUMS.txt](https://github.com/fademan7/mDrive/releases/download/v0.5.18/SHA256SUMS.txt). Source is licensed GPL-3.0-only.
