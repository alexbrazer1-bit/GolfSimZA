# Uninstall-Controller.ps1 - run on the MAIN PC (elevated) to remove the SimLink controller and bridge.
#   .\Uninstall-Controller.ps1              (keeps C:\SimLinkBridge contents)
#   .\Uninstall-Controller.ps1 -RemoveBridgeFolder
[CmdletBinding()]
param([string]$JobsDir = 'C:\SimLinkBridge', [switch]$RemoveBridgeFolder)
$ErrorActionPreference = 'Stop'

Write-Host '==> Removing SimLink Bridge task'
try { Stop-ScheduledTask -TaskName 'SimLink Bridge' -ErrorAction SilentlyContinue } catch { }
Unregister-ScheduledTask -TaskName 'SimLink Bridge' -Confirm:$false -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { ($_.CommandLine -like '*SimLink.ps1*watch*' -or $_.CommandLine -like '*SimLink-Panel.ps1*') -and $_.ProcessId -ne $PID } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

$base = Join-Path $env:LOCALAPPDATA 'SimLink'
$appDir = Join-Path $base 'app'
Write-Host "==> Removing $base and stored token ($env:APPDATA\SimLink)"
Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $env:APPDATA 'SimLink') -Recurse -Force -ErrorAction SilentlyContinue

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($userPath) {
    $new = (($userPath -split ';') | Where-Object { $_ -and $_ -ne $appDir }) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $new, 'User')
}
if ($RemoveBridgeFolder -and (Test-Path -LiteralPath $JobsDir)) {
    Write-Host "==> Removing $JobsDir"
    Remove-Item -LiteralPath $JobsDir -Recurse -Force
}
Write-Host 'SimLink controller removed.' -ForegroundColor Green
