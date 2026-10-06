# Uninstall-Agent.ps1
# Run on the SIMULATOR PC in an elevated PowerShell to completely remove the SimLink agent.
#   .\Uninstall-Agent.ps1            (removes everything)
#   .\Uninstall-Agent.ps1 -KeepLogs  (keeps C:\SimLink\logs for reference)

[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\SimLink',
    [switch]$KeepLogs
)
$ErrorActionPreference = 'Stop'

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run from an elevated PowerShell (Run as administrator).'
}

$port = 8765
$cfgPath = Join-Path $InstallDir 'agent.config.json'
if (Test-Path -LiteralPath $cfgPath) {
    try { $port = [int]((Get-Content -LiteralPath $cfgPath -Raw | ConvertFrom-Json).Port) } catch { }
}

Write-Host '==> Removing startup task'
try { Stop-ScheduledTask -TaskName 'SimLink Agent' -ErrorAction SilentlyContinue } catch { }
Unregister-ScheduledTask -TaskName 'SimLink Agent' -Confirm:$false -ErrorAction SilentlyContinue

Write-Host '==> Stopping agent process'
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like '*SimLink-Agent.ps1*' -and $_.ProcessId -ne $PID } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1

Write-Host '==> Removing firewall rule'
Get-NetFirewallRule -DisplayName 'SimLink Agent' -ErrorAction SilentlyContinue | Remove-NetFirewallRule

Write-Host "==> Removing URL reservation http://+:$port/sl/"
& netsh.exe http delete urlacl url="http://+:$port/sl/" 2>&1 | Out-Null

Write-Host "==> Removing $InstallDir"
if (Test-Path -LiteralPath $InstallDir) {
    Get-ChildItem -LiteralPath $InstallDir -Force | Where-Object { -not ($KeepLogs -and $_.Name -eq 'logs') } |
        Remove-Item -Recurse -Force
    if (-not $KeepLogs) { Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction SilentlyContinue }
}
Write-Host 'SimLink agent removed.' -ForegroundColor Green
