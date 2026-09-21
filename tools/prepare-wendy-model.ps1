param([string]$Destination = (Join-Path (Split-Path -Parent $PSScriptRoot) 'release\receiver\wendy'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
function Fetch-Verified($Url, $Path, $Hash) {
    if (!(Test-Path -LiteralPath $Path) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) {
        Invoke-WebRequest $Url -OutFile $Path
    }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) { throw "Checksum failed: $Path" }
}
$archive = Join-Path $Destination 'llama-b10964-bin-win-cpu-x64.zip'
Fetch-Verified 'https://github.com/ggml-org/llama.cpp/releases/download/b10964/llama-b10964-bin-win-cpu-x64.zip' $archive '917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7'
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $Destination 'cpu') -Force
$revision = '23749fefcc72300e3a2ad315e1317431b06b590a'
Fetch-Verified "https://huggingface.co/Qwen/Qwen3-0.6B-GGUF/resolve/$revision/Qwen3-0.6B-Q8_0.gguf" (Join-Path $Destination 'Qwen3-0.6B-Q8_0.gguf') '9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031'
Invoke-WebRequest "https://huggingface.co/Qwen/Qwen3-0.6B-GGUF/resolve/$revision/LICENSE" -OutFile (Join-Path $Destination 'LICENSE-Qwen.txt')
Invoke-WebRequest 'https://raw.githubusercontent.com/ggml-org/llama.cpp/b10964/LICENSE' -OutFile (Join-Path $Destination 'LICENSE-llama.txt')
Write-Host "Verified CPU-only Wendy bundle: $Destination"
