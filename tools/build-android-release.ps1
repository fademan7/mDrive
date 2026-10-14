param([string]$Vault = (Join-Path $env:LOCALAPPDATA 'mDriveSigning'), [switch]$BuildMigrationTest)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$env:JAVA_HOME -or !$env:ANDROID_HOME) { throw 'Set JAVA_HOME and ANDROID_HOME first.' }
$signer = Join-Path $env:ANDROID_HOME 'build-tools\37.0.0\apksigner.bat'
$aapt = Join-Path $env:ANDROID_HOME 'build-tools\37.0.0\aapt.exe'
$pinPath = Join-Path $root 'android\signing\release-certificate.sha256'
$lineage = Join-Path $root 'android\signing\debug-to-release.lineage'
if (!(Test-Path $pinPath) -or !(Test-Path $lineage)) { throw 'Public certificate pin and migration lineage must be present.' }
$pin = (Get-Content $pinPath -Raw).Trim().ToLowerInvariant()
if ($pin -notmatch '^[0-9a-f]{64}$') { throw 'Invalid certificate pin.' }
$keystore = Join-Path $Vault 'mdrive-release.p12'
$secure = Get-Content (Join-Path $Vault 'password.dpapi') -Raw | ConvertTo-SecureString
$credential = [PSCredential]::new('mdrive-release', $secure)
$names = @('MDRIVE_KEYSTORE','MDRIVE_STORE_PASSWORD','MDRIVE_KEY_ALIAS','MDRIVE_KEY_PASSWORD')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:MDRIVE_KEYSTORE = $keystore
    $env:MDRIVE_STORE_PASSWORD = $credential.GetNetworkCredential().Password
    $env:MDRIVE_KEY_ALIAS = 'mdrive-release'
    $env:MDRIVE_KEY_PASSWORD = $env:MDRIVE_STORE_PASSWORD
    Push-Location (Join-Path $root 'android')
    try {
        $tasks = @(':app:testReleaseUnitTest', ':app:assembleRelease', ':app:lintRelease')
        if ($BuildMigrationTest) { $tasks += @('-PinstrumentationBuildType=release', ':app:assembleReleaseAndroidTest') }
        & .\gradlew.bat @tasks --no-daemon --no-configuration-cache
        if ($LASTEXITCODE -ne 0) { throw 'Android release build/tests/lint failed.' }
    } finally { Pop-Location }
    $intermediate = Join-Path $root 'android\app\build\outputs\apk\release\app-release.apk'
    $metadata = Get-Content (Join-Path (Split-Path $intermediate) 'output-metadata.json') -Raw | ConvertFrom-Json
    $version = $metadata.elements[0].versionName
    $destination = Join-Path $root "release\android\mDrive-$version.apk"
    New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
    # Android 13+ only: sign solely with the permanent key. The old key is NOT
    # required for future builds; the public lineage proves the one-time rotation.
    & $signer sign --ks $keystore --ks-key-alias mdrive-release --ks-pass env:MDRIVE_STORE_PASSWORD --key-pass env:MDRIVE_KEY_PASSWORD --lineage $lineage --rotation-min-sdk-version 28 --v1-signing-enabled false --v2-signing-enabled false --v3-signing-enabled true --v4-signing-enabled false --out $destination $intermediate
    if ($LASTEXITCODE -ne 0) { throw 'Release signing with lineage failed.' }
    $verification = & $signer verify --verbose --print-certs --min-sdk-version 33 $destination
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
    $actual = @($verification | Select-String '^(?:Signer #\d+|V[\d.]+ Signer):? certificate SHA-256 digest: (.+)$')
    if ($actual.Count -ne 1 -or $actual[0].Matches[0].Groups[1].Value.Trim().ToLowerInvariant() -ne $pin) { throw 'APK certificate does not match the permanent public pin. Do not distribute it.' }
    $badging = & $aapt dump badging $destination
    if ($LASTEXITCODE -ne 0 -or $badging -match '^application-debuggable') { throw 'APK must not be debuggable.' }
    if (!($badging -match "package: name='dev.phonewheel'")) { throw 'Unexpected package identity.' }
    Write-Output "Verified release APK: $destination"
    Write-Output "Permanent signing certificate SHA-256: $pin"
    Get-FileHash $destination -Algorithm SHA256
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    $credential = $null; $secure.Dispose()
}
