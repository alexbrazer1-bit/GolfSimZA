# Install-Agent.ps1
# Run ONCE on the SIMULATOR PC in an elevated PowerShell (Run as Administrator):
#
#   Set-ExecutionPolicy -Scope Process Bypass -Force
#   .\Install-Agent.ps1 -ControllerIP 192.168.1.10
#
# -ControllerIP : IP address(es) of your MAIN PC (the only machine(s) allowed to connect)
# -Port         : TCP port the agent listens on (default 8765)
# -AllowedRoots : folders the controller may read/write (default C:\Shared, C:\Games, user's AppData\LocalLow)
# -DisableExec  : turn off remote PowerShell commands / arbitrary program start
# -NoElevation  : run the agent without admin rights (installers needing admin will then show a UAC prompt on the sim PC)
# -KeepAwake    : stop the sim PC from sleeping while on AC power so it stays reachable
# -Token        : supply your own token instead of generating one
# -NewToken     : force a brand-new token even if one already exists (main PC must then pair again)
# -KeepSettings : repair/upgrade - reuse the existing main PC IP, port, folders, exec and admin settings
#   On a repair/upgrade the existing token is ALWAYS kept (unless -Token/-NewToken), so the main PC stays paired.
# -PairingFile  : also write the pairing code (SIMLINK1:<simIP>:<port>:<token>) to this file (used by the setup EXE)

[CmdletBinding()]
param(
    [string[]]$ControllerIP,
    [ValidateRange(0, 65535)][int]$Port = 0,
    [string]$InstallDir = 'C:\SimLink',
    [string[]]$AllowedRoots,
    [string]$RunAsUser,
    [switch]$DisableExec,
    [switch]$NoElevation,
    [switch]$KeepAwake,
    [string]$Token,
    [switch]$NewToken,
    [switch]$KeepSettings,
    [string]$PairingFile
)

$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
. (Join-Path $src 'SimLink.Common.ps1')

function Write-Step([string]$m) { Write-Host "==> $m" -ForegroundColor Cyan }

# --- checks ---------------------------------------------------------------
if (-not (Test-SLWindows)) { throw 'This installer is for Windows.' }
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this installer from an elevated PowerShell (right-click > Run as administrator).'
}
# --- existing installation (repair / upgrade) -------------------------------
$existingCfg = $null
$cfgPathOld = Join-Path $InstallDir 'agent.config.json'
if (Test-Path -LiteralPath $cfgPathOld) {
    try { $existingCfg = Get-Content -LiteralPath $cfgPathOld -Raw | ConvertFrom-Json; Write-Step 'Existing SimLink agent found - upgrading/repairing it' } catch { $existingCfg = $null }
}
if (-not $ControllerIP -or @($ControllerIP | Where-Object { $_ }).Count -eq 0) {
    if ($existingCfg) { $ControllerIP = @($existingCfg.AllowedIPs) } else { throw 'Specify -ControllerIP (the IP address of your main PC).' }
}
$ControllerIP = @($ControllerIP | ForEach-Object { $_ -split '[;, ]' } | Where-Object { $_ })
if ($Port -eq 0) {
    if ($existingCfg -and $existingCfg.Port) { $Port = [int]$existingCfg.Port } else { $Port = 8765 }
}
if ($Port -lt 1024) { throw 'Port must be between 1024 and 65535.' }
if ($KeepSettings -and $existingCfg) {
    if (-not $AllowedRoots -or @($AllowedRoots | Where-Object { $_ }).Count -eq 0) { $AllowedRoots = @($existingCfg.AllowedRoots) }
    if (-not $existingCfg.AllowExec) { $DisableExec = [switch]$true }
    try {
        $oldTask = Get-ScheduledTask -TaskName 'SimLink Agent' -ErrorAction Stop
        if ([string]$oldTask.Principal.RunLevel -eq 'Limited') { $NoElevation = [switch]$true }
    } catch { }
}
foreach ($ip in $ControllerIP) {
    $parsed = $null
    if (-not [System.Net.IPAddress]::TryParse($ip, [ref]$parsed)) { throw "Not a valid IP address: $ip" }
}
foreach ($f in 'SimLink-Agent.ps1', 'SimLink.Common.ps1', 'Uninstall-Agent.ps1') {
    if (-not (Test-Path -LiteralPath (Join-Path $src $f))) { throw "Missing file next to installer: $f" }
}

if (-not $RunAsUser) {
    $RunAsUser = (Get-CimInstance Win32_ComputerSystem).UserName   # interactive (logged-on) user
    if (-not $RunAsUser) { $RunAsUser = $id.Name }
}
Write-Step "Agent will run as: $RunAsUser"

$userProfile = $env:USERPROFILE
try {
    $sid = (New-Object Security.Principal.NTAccount($RunAsUser)).Translate([Security.Principal.SecurityIdentifier]).Value
    $pp = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$sid" -ErrorAction Stop).ProfileImagePath
    if ($pp) { $userProfile = $pp }
} catch { }

# accept 'C:\A;C:\B' as one string too (setup EXE passes it that way)
$AllowedRoots = @($AllowedRoots | Where-Object { $_ } | ForEach-Object { $_ -split ';' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if (-not $AllowedRoots -or $AllowedRoots.Count -eq 0) {
    $AllowedRoots = @('C:\Shared', 'C:\Games', (Join-Path $userProfile 'AppData\LocalLow'))
}
$unityLogs = Join-Path $userProfile 'AppData\LocalLow'   # Unity Player.log lives here - always allowed
if (-not ($AllowedRoots | Where-Object { $_.TrimEnd('\') -ieq $unityLogs })) { $AllowedRoots += $unityLogs }
$AllowedRoots = @($AllowedRoots | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\') } | Select-Object -Unique)
foreach ($r in $AllowedRoots) {
    if ($r.Length -le 3) { throw "Refusing to allow a whole drive as a root: $r" }
    if (-not (Test-Path -LiteralPath $r)) { New-Item -ItemType Directory -Path $r -Force | Out-Null; Write-Step "Created allowed folder $r" }
}

# --- stop any running copy ------------------------------------------------
Write-Step 'Stopping previous agent (if any)'
try { Stop-ScheduledTask -TaskName 'SimLink Agent' -ErrorAction SilentlyContinue } catch { }
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like '*SimLink-Agent.ps1*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1

# --- files ----------------------------------------------------------------
Write-Step "Installing files to $InstallDir"
foreach ($d in @($InstallDir, (Join-Path $InstallDir 'staging'), (Join-Path $InstallDir 'logs'))) {
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}
foreach ($f in 'SimLink-Agent.ps1', 'SimLink.Common.ps1', 'Uninstall-Agent.ps1') {
    Copy-Item -LiteralPath (Join-Path $src $f) -Destination (Join-Path $InstallDir $f) -Force
}

# --- token ----------------------------------------------------------------
$tokenFile = Join-Path $InstallDir 'token.dat'
if ($Token) {
    if (-not (Test-SLTokenFormat $Token)) { throw 'Supplied token is invalid (32-256 chars of A-Z a-z 0-9 _ -).' }
} elseif (-not $NewToken -and (Test-Path -LiteralPath $tokenFile)) {
    try {
        $Token = Unprotect-SLSecret -Stored (Get-Content -LiteralPath $tokenFile -Raw) -Scope LocalMachine
        Write-Step 'Keeping the existing access token (the main PC stays paired)'
    } catch {
        Write-Step 'Existing token could not be read - generating a new one (pair the main PC again)'
        $Token = New-SLToken
    }
} else {
    $Token = New-SLToken
}
[IO.File]::WriteAllText($tokenFile, (Protect-SLSecret -Secret $Token -Scope LocalMachine))

# --- config ---------------------------------------------------------------
$cfg = [ordered]@{
    Port            = $Port
    Prefix          = "http://+:$Port/sl/"
    AllowedIPs      = @($ControllerIP)
    AllowedRoots    = @($AllowedRoots)
    AllowExec       = (-not $DisableExec)
    InstallDir      = $InstallDir
    StagingDir      = (Join-Path $InstallDir 'staging')
    LogDir          = (Join-Path $InstallDir 'logs')
    TokenFile       = $tokenFile
    ReplayWindowSec = 120
    MaxAuthFailures = 5
    LockoutMinutes  = 15
    MaxChunkBytes   = 8388608
    MaxOutputChars  = 400000
}
$cfg | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $InstallDir 'agent.config.json') -Encoding UTF8

# --- ACLs: only admins/SYSTEM can change the agent; the run-as user can read it and write logs/staging ---
Write-Step 'Locking down permissions'
& icacls.exe $InstallDir /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${RunAsUser}:(OI)(CI)RX" | Out-Null
& icacls.exe (Join-Path $InstallDir 'staging') /grant "${RunAsUser}:(OI)(CI)M" | Out-Null
& icacls.exe (Join-Path $InstallDir 'logs') /grant "${RunAsUser}:(OI)(CI)M" | Out-Null
& icacls.exe $tokenFile /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' "${RunAsUser}:R" | Out-Null

# --- URL reservation (lets the non-admin/elevated task listen on the LAN) ---
Write-Step "Reserving http://+:$Port/sl/"
& netsh.exe http delete urlacl url="http://+:$Port/sl/" 2>&1 | Out-Null
$out = & netsh.exe http add urlacl url="http://+:$Port/sl/" user="$RunAsUser" 2>&1
if ($LASTEXITCODE -ne 0) { throw "netsh urlacl failed: $out" }

# --- firewall: only the controller IP(s) may reach the port ----------------
Write-Step "Firewall rule: TCP $Port inbound from $($ControllerIP -join ', ') only"
Get-NetFirewallRule -DisplayName 'SimLink Agent' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName 'SimLink Agent' -Direction Inbound -Protocol TCP -LocalPort $Port `
    -RemoteAddress $ControllerIP -Action Allow -Profile Any -Description 'SimLink: main PC controller access' | Out-Null

# --- scheduled task: start at logon in the interactive session (needed for screenshots / launching the game) ---
Write-Step 'Registering startup task'
$psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$action = New-ScheduledTaskAction -Execute $psExe -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\SimLink-Agent.ps1`"" -WorkingDirectory $InstallDir
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $RunAsUser
$runLevel = 'Highest'
if ($NoElevation) { $runLevel = 'Limited' }
$principal = New-ScheduledTaskPrincipal -UserId $RunAsUser -LogonType Interactive -RunLevel $runLevel
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'SimLink Agent' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null

if ($KeepAwake) {
    Write-Step 'Disabling sleep on AC power'
    & powercfg.exe /change standby-timeout-ac 0 | Out-Null
    & powercfg.exe /change hibernate-timeout-ac 0 | Out-Null
}

Write-Step 'Starting agent'
Start-ScheduledTask -TaskName 'SimLink Agent'
$listening = $false
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Seconds 1
    if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) { $listening = $true; break }
}

$myIPs = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | Select-Object -ExpandProperty IPAddress)

# pick the sim PC address on the same subnet (/24) as the main PC
$pairIP = $myIPs | Select-Object -First 1
$ctlPrefix = ($ControllerIP[0] -split '\.')[0..2] -join '.'
$same = @($myIPs | Where-Object { (($_ -split '\.')[0..2] -join '.') -eq $ctlPrefix })
if ($same.Count -gt 0) { $pairIP = $same[0] }
$pairing = 'SIMLINK1:{0}:{1}:{2}' -f $pairIP, $Port, $Token

if ($PairingFile) {
    $pf = [IO.Path]::GetFullPath($PairingFile)
    $pdir = Split-Path -Parent $pf
    if (-not (Test-Path -LiteralPath $pdir)) { New-Item -ItemType Directory -Path $pdir -Force | Out-Null }
    [IO.File]::WriteAllText($pf, $pairing)
}

Write-Host ''
if ($listening) { Write-Host "SimLink agent is RUNNING on port $Port." -ForegroundColor Green }
else { Write-Host "Agent did not start listening yet. Check $InstallDir\logs (it starts automatically when $RunAsUser is logged on)." -ForegroundColor Yellow }
Write-Host ''
Write-Host 'This simulator PC IP address(es):' ($myIPs -join ', ')
Write-Host ''
Write-Host '================ PAIRING CODE (keep secret) ================' -ForegroundColor Yellow
Write-Host $pairing -ForegroundColor Yellow
Write-Host '============================================================' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Enter this pairing code in SimLink-MainPC-Setup.exe on your main PC'
Write-Host '(or run: simlink setup -Address <ip> -Port <port> and paste only the last part as the token).'
if (-not $listening) { exit 3 }
