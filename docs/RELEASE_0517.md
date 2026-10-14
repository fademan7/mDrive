# mDrive 0.5.17 Preview

## Release signing and open-source license

- Android uses a permanent RSA-4096 release key and a non-debuggable release build, not the development key.
- A public Android v3 proof-of-rotation links the earlier official debug certificate to the release certificate. Future updates retain the same release identity and lineage; the old private key is not required to build them.
- Release packaging verifies the expected signer and rejects debug/unsigned fallbacks. Gradle's Windows launcher now propagates build failures correctly.
- mDrive original code and documentation are licensed **GPL-3.0-only**. LICENSE/COPYRIGHT are included in the APK and Windows installation; upstream components keep their own licenses.
- No controller timing, safety gate, recovery algorithm, Wendy intent behavior, or game output was changed.

## Downloads

- `mDrive-0.5.17.apk`: Android 13+, permanent release signature.
- `mDrive-0.5.17-Setup-x64.exe`: Windows receiver, Vosk model and official driver prerequisite. Windows Authenticode signing is still unavailable; the installer remains unsigned.
- `SHA256SUMS.txt`: asset integrity hashes.
- Matching source/build scripts: the `v0.5.17` tag and GitHub-generated source archives on this release.

Update the APK over the existing official app; do not uninstall or clear data.
Unrelated debug certificates cannot use this migration. See the [signing guide](ANDROID_SIGNING.md) and [validation report](../artifacts/validation/RELEASE_SIGNING_0517.md).

Verified on the connected SM-S942N: update from 0.5.16 succeeded without uninstalling; both saved settings and button-layout file hashes were unchanged. The installed APK's permanent signer and non-debuggable flag were verified. Android 57 release tests/build/lint and Windows 49 regression groups passed. Other devices and new F1 driving sessions were not retested.

Migration retains the old certificate's signature-permission compatibility with the maintainer's explicit approval; old-key rollback remains disabled. This is not a remedy for an exposed old key. Private signing material is never included in release assets.

The app remains a preview. A release signature does not prove dropout-free races, game compatibility, or speech accuracy. Windows security/anti-cheat protections should not be disabled.
