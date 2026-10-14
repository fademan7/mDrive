param(
    [Parameter(Mandatory=$true)][string]$Destination,
    [string]$Vault = (Join-Path $env:LOCALAPPDATA 'mDriveSigning')
)
$ErrorActionPreference = 'Stop'
if (!$env:JAVA_HOME) { throw 'Set JAVA_HOME to JDK 17.' }
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$backupPath = [IO.Path]::GetFullPath($Destination)
if ($backupPath -eq $root -or $backupPath.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Choose a backup directory outside the repository.' }
if (Test-Path -LiteralPath $backupPath) { throw 'Backup destination must not already exist. No overwrite is allowed.' }
$backupSecret = Read-Host 'New portable backup password (save it in your password manager)' -AsSecureString
$confirmation = Read-Host 'Confirm backup password' -AsSecureString
$sourceSecret = Get-Content (Join-Path $Vault 'password.dpapi') -Raw | ConvertTo-SecureString
$previousSource = $env:MDRIVE_SOURCE_PASSWORD
$previousBackup = $env:MDRIVE_BACKUP_PASSWORD
try {
    $env:MDRIVE_BACKUP_PASSWORD = ([PSCredential]::new('backup',$backupSecret)).GetNetworkCredential().Password
    $confirmed = ([PSCredential]::new('backup',$confirmation)).GetNetworkCredential().Password
    if ($env:MDRIVE_BACKUP_PASSWORD.Length -lt 16 -or $env:MDRIVE_BACKUP_PASSWORD -cne $confirmed) { throw 'Passwords must match and contain at least 16 characters.' }
    $env:MDRIVE_SOURCE_PASSWORD = ([PSCredential]::new('source',$sourceSecret)).GetNetworkCredential().Password
    New-Item -ItemType Directory -Path $backupPath | Out-Null
    & (Join-Path $env:JAVA_HOME 'bin\keytool.exe') -importkeystore -srckeystore (Join-Path $Vault 'mdrive-release.p12') -srcstoretype PKCS12 -srcalias mdrive-release -srcstorepass:env MDRIVE_SOURCE_PASSWORD -srckeypass:env MDRIVE_SOURCE_PASSWORD -destkeystore (Join-Path $backupPath 'mdrive-release.p12') -deststoretype PKCS12 -destalias mdrive-release -deststorepass:env MDRIVE_BACKUP_PASSWORD -destkeypass:env MDRIVE_BACKUP_PASSWORD -noprompt
    if ($LASTEXITCODE -ne 0) { throw 'Portable backup export failed. Original key is unchanged.' }
    & (Join-Path $env:JAVA_HOME 'bin\keytool.exe') -exportcert -keystore (Join-Path $backupPath 'mdrive-release.p12') -alias mdrive-release -storepass:env MDRIVE_BACKUP_PASSWORD -file (Join-Path $backupPath 'release-certificate.der')
    if ($LASTEXITCODE -ne 0) { throw 'Backup verification failed.' }
    $pin = (Get-Content (Join-Path $root 'android\signing\release-certificate.sha256') -Raw).Trim()
    if ((Get-FileHash (Join-Path $backupPath 'release-certificate.der') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pin) { throw 'Backup does not match the official release certificate.' }
    Copy-Item -LiteralPath (Join-Path $root 'android\signing\debug-to-release.lineage') -Destination $backupPath
    Copy-Item -LiteralPath (Join-Path $root 'android\signing\release-certificate.sha256') -Destination $backupPath
    Write-Output 'Portable encrypted keystore exported and certificate verified. Keep the backup and password separately, outside Git. Original vault was not changed.'
} finally {
    $env:MDRIVE_SOURCE_PASSWORD = $previousSource
    $env:MDRIVE_BACKUP_PASSWORD = $previousBackup
    $confirmed = $null; $backupSecret.Dispose(); $confirmation.Dispose(); $sourceSecret.Dispose()
}
