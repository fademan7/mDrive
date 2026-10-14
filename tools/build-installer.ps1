param([string]$Compiler,[string]$OutputDirectory = 'release\distribution-0516')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$Compiler){$Compiler=Join-Path $root '.tools\inno\ISCC.exe'}
if(!(Test-Path -LiteralPath $Compiler)){throw 'Install the official Inno Setup compiler, or pass -Compiler. See installer/README.txt.'}
& (Join-Path $PSScriptRoot 'prepare-speech-assets.ps1') -OutputDirectory $OutputDirectory
$bundle=Join-Path $root $OutputDirectory
if(!(Test-Path -LiteralPath (Join-Path $bundle 'notices\THIRD-PARTY.txt'))){throw 'Run tools/prepare-distribution-notices.ps1 before packaging.'}
$receiver=Join-Path $bundle 'PhoneWheel.Receiver.exe'
if(!(Test-Path -LiteralPath $receiver) -or (Get-Item $receiver).VersionInfo.FileVersion -ne '0.5.16.0'){throw 'Publish receiver 0.5.16 before packaging.'}
& $Compiler "/DBundleRoot=$bundle" (Join-Path $root 'installer\mdrive.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compile failed.'}
Get-FileHash -LiteralPath (Join-Path $root 'release\installer\mDrive-0.5.16-Setup-x64.exe') -Algorithm SHA256
