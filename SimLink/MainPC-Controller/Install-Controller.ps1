# Install-Controller.ps1
# Run ONCE on your MAIN PC (the one Claude runs on) in an elevated PowerShell:
#
#   Set-ExecutionPolicy -Scope Process Bypass -Force
#   .\Install-Controller.ps1 -PairingCode SIMLINK1:192.168.1.50:8765:xxxxxxxx
#   (or -Address 192.168.1.50 and paste the token when asked)
#
# What it does:
#   1. Copies the SimLink app (Control Panel, command line, self-test) to %LOCALAPPDATA%\SimLink\app
#   2. Adds a 'simlink' shortcut command (simlink.cmd) to that folder and your user PATH
#   3. Runs 'setup' (pairing code from the sim PC) unless -SkipSetup, or -KeepPairing and already paired
#   4. Registers the 'SimLink Bridge' startup task that watches C:\SimLinkBridge for jobs from Claude
#
# Afterwards, connect the folder C:\SimLinkBridge to Claude (Add folder in the Claude desktop app)
# so Claude can drive the simulator PC.

[CmdletBinding()]
param(
    [string]$PairingCode,
    [string]$PairingFile,
    [string]$Address,
    [ValidateRange(1, 65535)][int]$Port = 8765,
    [string]$JobsDir = 'C:\SimLinkBridge',
    [switch]$SkipSetup,
    [switch]$KeepPairing,
    [switch]$NoBridge
)
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
. (Join-Path $src 'SimLink.Common.ps1')
function Write-Step([string]$m) { Write-Host "==> $m" -ForegroundColor Cyan }

if (-not (Test-SLWindows)) { throw 'This installer is for Windows.' }
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run from an elevated PowerShell (right-click > Run as administrator).'
}
$appFiles = @('SimLink.ps1', 'SimLink.Core.ps1', 'SimLink.Common.ps1', 'SimLink-Panel.ps1', 'Test-SimLink.ps1', 'SimLink.exe', 'simlink.ico', 'WindowsDisplays.cs')
foreach ($f in $appFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $src $f))) { throw "Missing file next to installer: $f" }
}

# stop a running bridge / control panel so their files can be replaced
Write-Step 'Stopping running SimLink bridge / control panel (if any)'
try { Stop-ScheduledTask -TaskName 'SimLink Bridge' -ErrorAction SilentlyContinue } catch { }
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessId -ne $PID -and ($_.CommandLine -like '*SimLink.ps1*watch*' -or $_.CommandLine -like '*SimLink-Panel.ps1*') } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1

$appDir = Join-Path $env:LOCALAPPDATA 'SimLink\app'
Write-Step "Installing SimLink app to $appDir"
if (-not (Test-Path -LiteralPath $appDir)) { New-Item -ItemType Directory -Path $appDir -Force | Out-Null }
foreach ($f in $appFiles) { Copy-Item -LiteralPath (Join-Path $src $f) -Destination (Join-Path $appDir $f) -Force }
$cmdFile = Join-Path $appDir 'simlink.cmd'
Set-Content -LiteralPath $cmdFile -Encoding ASCII -Value "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0SimLink.ps1`" %*"

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (-not $userPath) { $userPath = '' }
if (($userPath -split ';') -notcontains $appDir) {
    Write-Step 'Adding simlink to your PATH (open a new terminal to use it)'
    [Environment]::SetEnvironmentVariable('Path', ($userPath.TrimEnd(';') + ';' + $appDir).TrimStart(';'), 'User')
}

$ctl = Join-Path $appDir 'SimLink.ps1'
$cfgDir = Join-Path $env:APPDATA 'SimLink'
$alreadyPaired = (Test-Path -LiteralPath (Join-Path $cfgDir 'controller.json')) -and (Test-Path -LiteralPath (Join-Path $cfgDir 'token.dat'))
if ($KeepPairing -and $alreadyPaired -and -not $PairingCode -and -not $PairingFile) {
    Write-Step 'Keeping the existing pairing'
    $SkipSetup = [switch]$true
}
if (-not $SkipSetup) {
    if (-not $PairingCode -and $PairingFile) {
        if (-not (Test-Path -LiteralPath $PairingFile)) { throw "Pairing file not found: $PairingFile" }
        $PairingCode = (Get-Content -LiteralPath $PairingFile -Raw).Trim()
    }
    Write-Step 'Pairing with the simulator PC'
    if ($PairingCode) {
        # token goes through an environment variable, never the command line
        $env:SIMLINK_TOKEN = $PairingCode.Trim()
        try { & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ctl setup }
        finally { Remove-Item Env:\SIMLINK_TOKEN -ErrorAction SilentlyContinue }
    } else {
        if (-not $Address) { $Address = Read-Host 'Simulator PC IP address' }
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $ctl setup -Address $Address -Port $Port
    }
    if ($LASTEXITCODE -ne 0) { throw 'Pairing failed - check the pairing code and that the sim PC is on, logged in and the agent is running.' }
}

if (-not $NoBridge) {
    Write-Step "Creating bridge folder $JobsDir"
    foreach ($d in @($JobsDir, (Join-Path $JobsDir 'inbox'), (Join-Path $JobsDir 'results'), (Join-Path $JobsDir 'done'), (Join-Path $JobsDir 'files'))) {
        if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    }
    # only you, admins and SYSTEM can use the bridge folder (it is a command channel)
    & icacls.exe $JobsDir /inheritance:r /grant:r "$($id.Name):(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null

    Write-Step 'Registering SimLink Bridge startup task'
    $psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $action = New-ScheduledTaskAction -Execute $psExe -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$ctl`" watch -JobsDir `"$JobsDir`"" -WorkingDirectory $appDir
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $id.Name
    $principal = New-ScheduledTaskPrincipal -UserId $id.Name -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
        -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName 'SimLink Bridge' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
    Start-ScheduledTask -TaskName 'SimLink Bridge'
    Start-Sleep -Seconds 4
    $status = Join-Path $JobsDir 'status.json'
    if (Test-Path -LiteralPath $status) { Write-Host "Bridge running. Status: $(Get-Content -LiteralPath $status -Raw)" -ForegroundColor Green }
    else { Write-Host 'Bridge task registered; status file not written yet - check again in a few seconds.' -ForegroundColor Yellow }
}

Write-Host ''
Write-Host 'Done. Next: in the Claude desktop app click "Add folder" and add' $JobsDir -ForegroundColor Green
Write-Host 'Open "SimLink Control Panel" from the Start menu or desktop.  Command line: simlink help'
