param([string]$Version = '0.4.0')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$bundle = Join-Path $projectRoot 'release\receiver-040'
if (!(Test-Path -LiteralPath (Join-Path $bundle 'PhoneWheel.Receiver.exe')) -or !(Test-Path -LiteralPath (Join-Path $bundle 'wendy\Qwen3-0.6B-Q8_0.gguf'))) { throw 'Publish receiver-040 and prepare the verified Wendy model first.' }
$zip = Join-Path $projectRoot "release\mDrive-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zip -CompressionLevel Optimal -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
Get-FileHash -LiteralPath $zip -Algorithm SHA256
