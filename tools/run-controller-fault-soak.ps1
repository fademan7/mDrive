param(
    [Parameter(Mandatory=$true)][string]$PcAddress,
    [Parameter(Mandatory=$true)][string]$PhoneAddress,
    [Parameter(Mandatory=$true)][string]$Serial,
    [Parameter(Mandatory=$true)][string]$Python,
    [int]$Seconds = 600,
    [string]$Receiver = 'release/receiver/PhoneWheel.Receiver.exe',
    [switch]$BriefOutages
)
# Requires explicit permission to adjust the test executable's UDP firewall rule.
# Never changes profiles, TCP rules, the Receiver's rules, or the global firewall.
$ErrorActionPreference = 'Stop'
$Python = (Resolve-Path -LiteralPath $Python).Path
$ruleName = 'mDrive-Controller-Soak-Temporary-' + [guid]::NewGuid().ToString('N')
$blocked = @(Get-NetFirewallApplicationFilter | Where-Object { $_.Program -eq $Python } |
    Get-NetFirewallRule | Where-Object { $_.Enabled -eq 'True' -and $_.Action -eq 'Block' -and $_.Direction -eq 'Inbound' } |
    Where-Object { ($_ | Get-NetFirewallPortFilter).Protocol -eq 'UDP' })
$disabled = @()
$created = $false
try {
    New-NetFirewallRule -Name $ruleName -DisplayName $ruleName -Direction Inbound -Action Allow -Profile Public -Program $Python -Protocol UDP -LocalAddress $PcAddress -LocalPort 26760 -RemoteAddress $PhoneAddress | Out-Null
    $created = $true
    foreach ($rule in $blocked) {
        Disable-NetFirewallRule -Name $rule.Name | Out-Null
        $disabled += $rule.Name
    }
    Write-Output 'Temporary UDP allowance active for one phone and port 26760.'
    $env:PYTHONUTF8 = '1'
    $soakArgs = @('--host', $PcAddress, '--serial', $Serial, '--seconds', $Seconds, '--faults', '--receiver', $Receiver)
    if ($BriefOutages) { $soakArgs += '--brief-outages' }
    & $Python (Join-Path $PSScriptRoot 'test_controller_soak.py') @soakArgs
    $testExit = $LASTEXITCODE
} finally {
    # Attempt every restoration even if another restoration operation fails.
    $restoreErrors = @()
    foreach ($name in $disabled) {
        try { Enable-NetFirewallRule -Name $name | Out-Null } catch { $restoreErrors += $_ }
    }
    if ($created) {
        try { Remove-NetFirewallRule -Name $ruleName } catch { $restoreErrors += $_ }
    }
    if ($restoreErrors.Count -gt 0) { throw "Firewall restoration requires attention: $($restoreErrors -join '; ')" }
    Write-Output 'Firewall restored: original UDP blocks enabled; temporary allowance removed.'
}
exit $testExit
