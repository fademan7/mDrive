param([switch]$IncludeComparisonModel,[string]$OutputDirectory = 'release\distribution-0517')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$cache=Join-Path $root '.tools\downloads'
$bundle=Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $cache,(Join-Path $bundle 'speech'),(Join-Path $root 'release\prerequisites') | Out-Null
$assets=@(
    @{Name='vosk-model-small-en-us-0.15.zip';Url='https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip';Hash='30F26242C4EB449F948E42CB302DD7A686CB29A3423A8367F99FF41780942498'},
    @{Name='ViGEmBus_1.22.0_x64_x86_arm64.exe';Url='https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe';Hash='89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A'}
)
if($IncludeComparisonModel){$assets+=@{Name='vosk-model-en-us-0.22-lgraph.zip';Url='https://alphacephei.com/vosk/models/vosk-model-en-us-0.22-lgraph.zip';Hash='D9838B4AAA82A75C4A17F5ACA300EACA129AAAB2A7CBF951BAFBB500EB9C4334'}}
foreach($asset in $assets){
    $path=Join-Path $cache $asset.Name
    if(!(Test-Path -LiteralPath $path)){Invoke-WebRequest -Uri $asset.Url -OutFile $path}
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $asset.Hash){throw "Asset integrity mismatch: $($asset.Name). No file was executed/extracted."}
    if($asset.Name.EndsWith('.zip')){
        Expand-Archive -LiteralPath $path -DestinationPath (Join-Path $bundle 'speech') -Force
    }else{
        $signature=Get-AuthenticodeSignature -LiteralPath $path
        if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Nefarius Software Solutions*'){throw 'Official driver signature could not be verified.'}
        Copy-Item -LiteralPath $path -Destination (Join-Path $root 'release\prerequisites') -Force
    }
}
Write-Host 'Verified speech model and signed driver installer prepared. No driver installed.'
