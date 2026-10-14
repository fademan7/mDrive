param([string]$Bundle='release\distribution-0513')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$exe=Join-Path (Join-Path $root $Bundle) 'PhoneWheel.Receiver.exe'
$model=Join-Path (Join-Path $root $Bundle) 'speech\vosk-model-small-en-us-0.15'
$info=[System.Diagnostics.ProcessStartInfo]::new($exe)
$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$info.RedirectStandardInput=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$info.ArgumentList.Add('--vosk-worker');$info.ArgumentList.Add($model)
$info.Environment['OPENBLAS_NUM_THREADS']='1';$info.Environment['OMP_NUM_THREADS']='1'
$child=[System.Diagnostics.Process]::Start($info)
$drain=$child.StandardError.ReadToEndAsync()
try {
    # Never enable capture. Verify real model loading, epoch changes and EOF exit.
    foreach($epoch in @(1,2)) {
        $child.StandardInput.WriteLine((@{Listen=$false;Epoch=$epoch}|ConvertTo-Json -Compress));$child.StandardInput.Flush()
        $matched=$false
        for($attempt=0;$attempt -lt 20;$attempt++) {
            $read=$child.StandardOutput.ReadLineAsync()
            if(!$read.Wait(10000)){throw 'Speech worker response timeout'}
            if(!$read.Result){throw 'Speech worker exited unexpectedly'}
            $message=$read.Result|ConvertFrom-Json
            if($message.kind -eq 'status' -and $message.state -eq 'ERROR'){throw $message.health}
            if($message.kind -eq 'status' -and $message.epoch -eq $epoch -and $message.state -eq 'IDLE'){$matched=$true;break}
        }
        if(!$matched){throw 'Epoch acknowledgement missing'}
    }
    $child.StandardInput.Close()
    if(!$child.WaitForExit(5000)){throw 'Speech worker did not exit after parent EOF'}
    if($child.ExitCode -ne 0){throw "Speech worker exit $($child.ExitCode)"}
    Write-Host 'PASS real Vosk load, idle epochs, no microphone capture, clean EOF exit'
}finally{if(!$child.HasExited){$child.Kill()};$child.Dispose()}
