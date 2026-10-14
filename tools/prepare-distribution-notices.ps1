param([string]$OutputDirectory='release\distribution-0517')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$destination=Join-Path (Join-Path $root $OutputDirectory) 'notices'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'installer\THIRD-PARTY.txt') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $destination 'mDrive-GPL-3.0.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'COPYRIGHT') -Destination $destination -Force
$sources=@{
    'Vosk-Apache-2.0.txt'='https://raw.githubusercontent.com/alphacep/vosk-api/master/COPYING'
    'ViGEmBus-BSD-3-Clause.txt'='https://raw.githubusercontent.com/nefarius/ViGEmBus/master/LICENSE'
    'ViGEmClient-MIT.txt'='https://raw.githubusercontent.com/nefarius/ViGEmClient/master/LICENSE'
    'ViGEm.NET-MIT.txt'='https://raw.githubusercontent.com/nefarius/ViGEm.NET/master/LICENSE'
    'NAudio-MIT.txt'='https://raw.githubusercontent.com/naudio/NAudio/v2.2.1/license.txt'
    'QRCoder-MIT.txt'='https://raw.githubusercontent.com/codebude/QRCoder/v1.6.0/LICENSE.txt'
    'GCC-GPL-3.0.txt'='https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-12.2.0/COPYING3'
    'GCC-Runtime-Exception.txt'='https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-12.2.0/COPYING.RUNTIME'
    'winpthreads-COPYING.txt'='https://raw.githubusercontent.com/mingw-w64/mingw-w64/v12.0.0/mingw-w64-libraries/winpthreads/COPYING'
    'Kaldi-Apache-2.0.txt'='https://raw.githubusercontent.com/kaldi-asr/kaldi/master/COPYING'
    'OpenBLAS-BSD.txt'='https://raw.githubusercontent.com/OpenMathLib/OpenBLAS/v0.3.20/LICENSE'
    'DotNet-MIT.txt'='https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/LICENSE.TXT'
    'DotNet-ThirdParty.txt'='https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/THIRD-PARTY-NOTICES.TXT'
    'WindowsDesktop-MIT.txt'='https://raw.githubusercontent.com/dotnet/wpf/v10.0.0/LICENSE.TXT'
    'WindowsDesktop-ThirdParty.txt'='https://raw.githubusercontent.com/dotnet/wpf/v10.0.0/THIRD-PARTY-NOTICES.TXT'
}
foreach($name in $sources.Keys){
    $path=Join-Path $destination $name
    if(!(Test-Path -LiteralPath $path)){Invoke-WebRequest -Uri $sources[$name] -OutFile $path}
    if((Get-Item -LiteralPath $path).Length -lt 100){throw "Invalid license text: $name"}
}
Write-Host 'Upstream license files prepared.'
