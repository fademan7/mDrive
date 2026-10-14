param([string]$OutputDirectory = 'release\receiver')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
& $dotnetCommand publish (Join-Path $projectRoot 'windows\src\PhoneWheel.Host\PhoneWheel.Host.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $projectRoot $OutputDirectory)
if ($LASTEXITCODE -ne 0) { throw 'Receiver publish failed.' }
Write-Host "Ready: $projectRoot\$OutputDirectory\PhoneWheel.Receiver.exe"
