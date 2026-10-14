param([string]$Vault = (Join-Path $env:LOCALAPPDATA 'mDriveSigning'))
$ErrorActionPreference = 'Stop'
if (!$IsWindows -and $PSVersionTable.PSEdition -eq 'Core') { throw 'This key vault uses Windows DPAPI.' }
if (!$env:JAVA_HOME) { throw 'Set JAVA_HOME to JDK 17.' }
$keytool = Join-Path $env:JAVA_HOME 'bin\keytool.exe'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$vaultPath = [IO.Path]::GetFullPath($Vault)
if ($vaultPath.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $vaultPath -eq $root) {
    throw 'Keep the permanent signing vault outside the repository.'
}
if (Test-Path -LiteralPath $vaultPath) { throw 'Vault already exists. Never overwrite or automatically regenerate a signing key.' }
New-Item -ItemType Directory -Path $vaultPath | Out-Null
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$acl.SetOwner($sid)
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $vaultPath -AclObject $acl
$random = New-Object byte[] 48
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($random); $rng.Dispose()
$password = [Convert]::ToBase64String($random)
$secure = ConvertTo-SecureString $password -AsPlainText -Force
# Generated encrypted credential artifact, never plaintext and never in Git.
$encrypted = ConvertFrom-SecureString $secure
[IO.File]::WriteAllText((Join-Path $vaultPath 'password.dpapi'), $encrypted)
$previous = $env:MDRIVE_NEW_KEY_PASSWORD
try {
    $env:MDRIVE_NEW_KEY_PASSWORD = $password
    & $keytool -genkeypair -keystore (Join-Path $vaultPath 'mdrive-release.p12') -storetype PKCS12 -alias mdrive-release -keyalg RSA -keysize 4096 -sigalg SHA256withRSA -validity 36500 -dname 'CN=fademan7, OU=mDrive, O=mDrive' -storepass:env MDRIVE_NEW_KEY_PASSWORD -keypass:env MDRIVE_NEW_KEY_PASSWORD
    if ($LASTEXITCODE -ne 0) { throw 'Key generation failed; inspect the vault manually. Do not regenerate blindly.' }
    & $keytool -exportcert -keystore (Join-Path $vaultPath 'mdrive-release.p12') -alias mdrive-release -storepass:env MDRIVE_NEW_KEY_PASSWORD -file (Join-Path $vaultPath 'release-certificate.der')
    if ($LASTEXITCODE -ne 0) { throw 'Certificate export failed.' }
    $fingerprint = (Get-FileHash (Join-Path $vaultPath 'release-certificate.der') -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Output "Created permanent signing vault: $vaultPath"
    Write-Output "Public certificate SHA-256: $fingerprint"
    Write-Output 'Back up the keystore AND a recoverable password outside this PC. DPAPI alone is not a portable backup.'
} finally {
    $env:MDRIVE_NEW_KEY_PASSWORD = $previous
    $password = $null; $secure.Dispose(); [Array]::Clear($random, 0, $random.Length)
}
