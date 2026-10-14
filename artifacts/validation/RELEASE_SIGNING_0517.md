# Release signing and GPL 0.5.17 — 2026-10-14

## Scope

Permanent Android release signing, debug-to-release migration proof, GPL-3.0-only
licensing, release packaging, and documentation. Controller timing, safety gate,
recovery, game input, and Wendy behavior were not modified.

## Build and signature verification

- RSA-4096 permanent key outside Git, current-user-only ACL, DPAPI-protected password.
- Public certificate SHA-256: `802297e5be0a38fa84db19cea49f664c9be8895efb1b879f69d72ac447e7dcd5`.
- Android 0.5.17 / versionCode 28: 57 release unit tests, release build/lint, and release instrumentation build passed.
- Debug unit tests also passed without release credentials.
- Missing release credentials produced the expected Gradle failure, with no debug-signing fallback. Fixed the Windows Gradle launcher's failure exit-code propagation.
- `apksigner verify --verbose --print-certs --min-sdk-version 33` passed: one RSA-4096 signer, v3 signature, matching permanent pin. No debug/v1/v2 fallback signature.
- Windows 49/49 regression groups passed; receiver and installer 0.5.17 built. The running PC receiver was not restarted or replaced.
- GPL LICENSE/COPYRIGHT confirmed in APK assets and Windows notices.

## Actual-device migration

The owner authorized testing on the connected SM-S942N. No main-app uninstall or data clearing occurred.

1. Stopped the existing 0.5.16 debug app and hashed its settings XML and layout XML. No preference contents or pairing secrets were printed/published.
2. Initial proof disabled old signature-permission trust. Android rejected the update with `INSTALL_FAILED_DUPLICATE_PERMISSION` for AndroidX's `dev.phonewheel.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`. Existing app/data remained intact.
3. Explained the retained-trust security tradeoff and obtained explicit owner approval. Final proof permits installed-data and signature-permission continuity; old-key rollback, shared-UID and authenticator trust are false.
4. `adb install -r` of the final release APK succeeded.
5. Read-only release-signed instrumentation verified the installed permanent signer, non-debuggable flag, and exact unchanged hashes for BOTH saved settings and button layout: **PASS**. No controller Activity, microphone or game input was started by this test.
6. Removed only the development test APK, then reopened the main app. Installed version 0.5.17 / 28 confirmed.

## Distribution hashes

- APK: `1ccf96b0d4ae7e5522841ef8c6f4bc5360ca0e61c8183abe507c85a70b939ab0`
- Windows installer: `0c535000860ab20028b5e97b59f851c8a97affcc0db39fd2520537b648951085`

## Limits

Migration is verified on one actual device, not every Android 13+ device or third-party debug build. Unrelated debug keys are not covered. Protect the old private key because signature-permission compatibility retains trust in it; this is not a compromised-key recovery procedure.

Windows Authenticode signing remains unavailable. No new full F1 driving/latency test was performed for this packaging-only release. Portable/off-device key backup remains the owner's action: DPAPI alone is not portable. See `docs/ANDROID_SIGNING.md` for the backup procedure and official references. No private keys, passwords or test APKs are distributed.
