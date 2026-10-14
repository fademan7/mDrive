# Android release signing

Official Android releases use one permanent RSA-4096 signing key, alias
`mdrive-release`, with a 100-year certificate. The APK is a non-debuggable
release build. APK signing is separate from Windows Authenticode: the Windows
installer is still unsigned.

Public certificate SHA-256:

```
802297e5be0a38fa84db19cea49f664c9be8895efb1b879f69d72ac447e7dcd5
```

The pin is also in `android/signing/release-certificate.sha256`. A certificate
fingerprint identifies the signer; it is not the APK file's checksum.

## Key custody

On the maintainer's Windows account, the vault is
`%LOCALAPPDATA%\mDriveSigning`, **outside the repository**. It contains:

- `mdrive-release.p12`: encrypted permanent private key.
- `password.dpapi`: password protected with Windows current-user DPAPI.
- `release-certificate.der`: public certificate, not a private key.

The vault directory has an explicit current-user-only ACL. No passwords are
written in plaintext or printed by the build scripts. Do not upload the vault,
keystore, encrypted password, test APKs, or private-key backups to GitHub.

**DPAPI is not a portable backup.** Losing the Windows account/system can make
its password file unusable. Before relying on this signing identity, export a
portable encrypted backup and keep its password separately in a password manager:

```powershell
$env:JAVA_HOME = 'C:\path\to\jdk-17'
.\tools\export-android-signing-backup.ps1 -Destination 'E:\private-backups\mdrive-signing'
```

The destination must not exist and must be outside the repository. The command
prompts privately for a new backup password, re-encrypts a copy of the same key,
and verifies its public certificate. It does not change the original key.
Maintain at least one independent offline backup. The scripts do not claim that
an off-device backup exists until the owner has actually created one.

To restore on another PC, use the backed-up PKCS12 key with its backup password
and the same alias. Gradle accepts `MDRIVE_KEYSTORE`, `MDRIVE_STORE_PASSWORD`,
`MDRIVE_KEY_ALIAS`, and `MDRIVE_KEY_PASSWORD` from the process environment. Load
secrets from a trusted secret manager or secure prompt, not command-line literals
or a tracked file. A restored Windows vault needs a new current-user DPAPI
password file; copying the old machine's DPAPI file is not sufficient.

Never run key generation as an ordinary build step. The initialization script
refuses to overwrite an existing vault. Losing or replacing this key without a
valid rotation plan can prevent future updates.

## Official build

Set JDK 17 / Android SDK paths, then run:

```powershell
.\tools\build-android-release.ps1
```

The script loads the local encrypted credential, runs release unit tests/build/
lint, signs with the permanent key and public lineage, verifies the pinned
certificate and non-debuggable flag, and produces
`release/android/mDrive-<version>.apk`. This is the distribution artifact, not
the intermediate Gradle APK. It never falls back to the debug key. Gradle release
builds fail if signing credentials are absent; debug builds remain available.
Do not enable Gradle configuration-cache/build-scan/debug logging when handling
signing secrets. The official script disables configuration caching and uses a
single-use daemon. Password environment variables are restored after the build.

Forks should create and securely manage their own signing identity and package
name; they must not claim to produce official updates or obtain our private key.
They can build from source with their own credentials. The official pin/lineage
guard is specific to official mDrive distribution.

## Transition from 0.5.16 debug signing

`android/signing/debug-to-release.lineage` is a **public proof of rotation**, not
a private key. It links the original distributed debug certificate to the new
release certificate using Android APK Signature Scheme v3. It permits installed
data migration and signature-permission continuity but does not allow rollback
to the old signing key. Permission continuity is needed for AndroidX's existing
`dev.phonewheel.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`: disabling it caused
the device to reject an update with `INSTALL_FAILED_DUPLICATE_PERMISSION`.
The maintainer explicitly approved retaining this trust for compatibility.
Shared-UID and authenticator trust in the old key are disabled. The old private
key must still be protected because permission continuity trusts its signer;
this is not a key-compromise recovery mechanism.

The supported minimum is Android 13 (API 33). The final APK is signed only with
the permanent key using v3; v1/v2 signatures from the old key are not included.
The old private key is not needed for subsequent release builds. Keep the public
lineage in every subsequent official APK so delayed updaters can migrate too.

An APK signed with a different developer's debug key does not match this lineage.
Do not uninstall or clear data to force an update. Device-specific update tests
and their limits are recorded in the release validation report.

Official references: [Android app signing](https://developer.android.com/studio/publish/app-signing),
[apksigner rotation](https://developer.android.com/tools/apksigner),
[v3 proof of rotation](https://source.android.com/docs/security/features/apksigning/v3).
