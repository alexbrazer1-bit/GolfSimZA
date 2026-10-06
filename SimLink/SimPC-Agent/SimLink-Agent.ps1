# SimLink-Agent.ps1
# Runs on the SIMULATOR PC. Listens on the LAN and executes authenticated, encrypted requests
# from the main PC's SimLink controller. Installed and started by Install-Agent.ps1.
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.

param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'agent.config.json')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SimLink.Common.ps1')

# ------------------------------------------------------------------ config

if (-not (Test-Path -LiteralPath $ConfigPath)) { throw "Config not found: $ConfigPath" }
$script:Cfg = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json

function Get-SLCfg {
    param([string]$Name, $Default)
    $p = $script:Cfg.PSObject.Properties[$Name]
    if ($null -eq $p -or $null -eq $p.Value) { return $Default }
    return $p.Value
}

$script:Port          = [int](Get-SLCfg 'Port' 8765)
$script:Prefix        = [string](Get-SLCfg 'Prefix' "http://+:$($script:Port)/sl/")
$script:AllowedIPs    = @(Get-SLCfg 'AllowedIPs' @())
$script:AllowedRoots  = @(Get-SLCfg 'AllowedRoots' @())
$script:AllowExec     = [bool](Get-SLCfg 'AllowExec' $true)
$script:InstallDir    = [string](Get-SLCfg 'InstallDir' $PSScriptRoot)
$script:StagingDir    = [string](Get-SLCfg 'StagingDir' (Join-Path $script:InstallDir 'staging'))
$script:LogDir        = [string](Get-SLCfg 'LogDir' (Join-Path $script:InstallDir 'logs'))
$script:TokenFile     = [string](Get-SLCfg 'TokenFile' (Join-Path $script:InstallDir 'token.dat'))
$script:WindowSec     = [int](Get-SLCfg 'ReplayWindowSec' 120)
$script:MaxFailures   = [int](Get-SLCfg 'MaxAuthFailures' 5)
$script:LockoutMin    = [int](Get-SLCfg 'LockoutMinutes' 15)
$script:MaxChunk      = [int](Get-SLCfg 'MaxChunkBytes' 8388608)
$script:MaxOutput     = [int](Get-SLCfg 'MaxOutputChars' 400000)

if ($script:AllowedIPs.Count -eq 0) { throw 'AllowedIPs is empty in config - refusing to start.' }
if ($script:AllowedRoots.Count -eq 0) { throw 'AllowedRoots is empty in config - refusing to start.' }

foreach ($d in @($script:StagingDir, $script:LogDir)) {
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

# ------------------------------------------------------------------ logging (audit)

function Write-SLLog {
    param([string]$Level, [string]$Message)
    $line = '{0} [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
    try {
        $f = Join-Path $script:LogDir ('agent-{0}.log' -f (Get-Date -Format 'yyyyMMdd'))
        Add-Content -LiteralPath $f -Value $line -Encoding UTF8
    } catch { }
    Write-Host $line
}

function Remove-SLOldLogs {
    try {
        Get-ChildItem -LiteralPath $script:LogDir -Filter 'agent-*.log' -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item -Force -ErrorAction SilentlyContinue
    } catch { }
}

# ------------------------------------------------------------------ token

function Get-SLAgentKeys {
    if (-not (Test-Path -LiteralPath $script:TokenFile)) { throw "Token file missing: $($script:TokenFile)" }
    $stored = Get-Content -LiteralPath $script:TokenFile -Raw
    $tok = Unprotect-SLSecret -Stored $stored -Scope LocalMachine
    return (New-SLKeys -Token $tok)
}

# ------------------------------------------------------------------ security state

$script:Nonces   = @{}   # nonce -> unix ts (replay cache)
$script:Failures = @{}   # ip -> list of failure timestamps
$script:Lockouts = @{}   # ip -> DateTime until
$script:Uploads  = @{}   # uploadId -> session

function Get-SLArg {
    param($Obj, [string]$Name, $Default = $null)
    if ($null -eq $Obj) { return $Default }
    $p = $Obj.PSObject.Properties[$Name]
    if ($null -eq $p -or $null -eq $p.Value) { return $Default }
    return $p.Value
}

function Get-SLRequiredArg {
    param($Obj, [string]$Name)
    $v = Get-SLArg $Obj $Name
    if ($null -eq $v -or ([string]$v).Length -eq 0) { throw "Missing argument '$Name'" }
    return $v
}

function Get-SLIntArg {
    param($Obj, [string]$Name, [int64]$Default, [int64]$Min, [int64]$Max)
    $v = Get-SLArg $Obj $Name $Default
    $n = 0L
    if (-not [int64]::TryParse([string]$v, [ref]$n)) { throw "Argument '$Name' must be a number" }
    if ($n -lt $Min -or $n -gt $Max) { throw "Argument '$Name' must be between $Min and $Max" }
    return $n
}

function Test-SLIPAllowed {
    param([string]$Ip)
    foreach ($a in $script:AllowedIPs) { if ([string]$a -eq $Ip) { return $true } }
    return $false
}

function Test-SLLockedOut {
    param([string]$Ip)
    if ($script:Lockouts.ContainsKey($Ip)) {
        if ((Get-Date) -lt $script:Lockouts[$Ip]) { return $true }
        $script:Lockouts.Remove($Ip)
    }
    return $false
}

function Register-SLFailure {
    param([string]$Ip)
    $now = Get-Date
    if (-not $script:Failures.ContainsKey($Ip)) { $script:Failures[$Ip] = New-Object System.Collections.ArrayList }
    [void]$script:Failures[$Ip].Add($now)
    $recent = @($script:Failures[$Ip] | Where-Object { $_ -gt $now.AddMinutes(-5) })
    $script:Failures[$Ip] = New-Object System.Collections.ArrayList
    foreach ($r in $recent) { [void]$script:Failures[$Ip].Add($r) }
    if ($recent.Count -ge $script:MaxFailures) {
        $script:Lockouts[$Ip] = $now.AddMinutes($script:LockoutMin)
        $script:Failures.Remove($Ip)
        Write-SLLog 'SECURITY' "IP $Ip locked out for $($script:LockoutMin) min after $($script:MaxFailures) failed auth attempts"
    }
}

function Test-SLNonceFresh {
    param([string]$Nonce, [int64]$TS)
    $now = Get-SLUnixTime
    if ($script:Nonces.Count -gt 500) {
        foreach ($k in @($script:Nonces.Keys)) {
            if (($now - $script:Nonces[$k]) -gt ($script:WindowSec * 2)) { $script:Nonces.Remove($k) }
        }
    }
    if ($script:Nonces.ContainsKey($Nonce)) { return $false }
    $script:Nonces[$Nonce] = $TS
    return $true
}

# ------------------------------------------------------------------ path policy

function Get-SLSep { return [string][IO.Path]::DirectorySeparatorChar }

function Resolve-SLPath {
    # Validates the path is inside an allowed root (or staging). Returns full normalized path.
    param([string]$Path, [switch]$AllowRoot)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Path is empty' }
    if ($Path.IndexOfAny([IO.Path]::GetInvalidPathChars()) -ge 0) { throw 'Path contains invalid characters' }
    if ($Path.StartsWith('\\') -or $Path.StartsWith('//')) { throw 'Network (UNC) paths are not allowed' }
    if (-not [IO.Path]::IsPathRooted($Path)) { throw 'Path must be absolute' }
    $full = [IO.Path]::GetFullPath($Path)
    $sep = Get-SLSep
    $roots = @($script:AllowedRoots) + @($script:StagingDir)
    foreach ($r in $roots) {
        $rf = [IO.Path]::GetFullPath([string]$r).TrimEnd('\', '/')
        if ($full.TrimEnd('\', '/').Equals($rf, [StringComparison]::OrdinalIgnoreCase)) {
            if ($AllowRoot) { return $rf }
            throw "Operation not allowed on an allowed-root folder itself: $rf"
        }
        if ($full.StartsWith($rf + $sep, [StringComparison]::OrdinalIgnoreCase)) { return $full }
    }
    throw "Path is outside the allowed folders: $full"
}

# ------------------------------------------------------------------ actions

function Invoke-SLInfo {
    $os = [Environment]::OSVersion.VersionString
    $uptime = $null
    try { $uptime = [int]([TimeSpan]::FromMilliseconds([Environment]::TickCount64).TotalMinutes) } catch {
        try { $uptime = [int]([TimeSpan]::FromMilliseconds([Environment]::TickCount).TotalMinutes) } catch { }
    }
    $drives = @()
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        try {
            if ($d.IsReady -and $d.DriveType -eq 'Fixed') {
                $drives += [pscustomobject]@{ name = $d.Name; freeGB = [Math]::Round($d.AvailableFreeSpace / 1GB, 1); totalGB = [Math]::Round($d.TotalSize / 1GB, 1) }
            }
        } catch { }
    }
    $screens = @()
    if (Test-SLWindows) {
        try {
            Add-Type -AssemblyName System.Windows.Forms
            foreach ($s in ([type]'System.Windows.Forms.Screen')::AllScreens) {
                $screens += [pscustomobject]@{ name = $s.DeviceName; primary = $s.Primary; width = $s.Bounds.Width; height = $s.Bounds.Height }
            }
        } catch { }
    }
    return [pscustomobject]@{
        ok = $true; host = [Environment]::MachineName; user = [Environment]::UserName; os = $os
        uptimeMinutes = $uptime; psVersion = $PSVersionTable.PSVersion.ToString(); agentVersion = $script:SLAgentVersion
        allowedRoots = $script:AllowedRoots; stagingDir = $script:StagingDir; allowExec = $script:AllowExec
        drives = $drives; screens = $screens; time = (Get-Date).ToString('o')
    }
}

function Invoke-SLExec {
    param($A)
    if (-not $script:AllowExec) { throw 'Remote command execution is disabled on this agent (AllowExec=false)' }
    $cmd = [string](Get-SLRequiredArg $A 'command')
    if ($cmd.Length -gt 100000) { throw 'Command too long' }
    $timeout = Get-SLIntArg $A 'timeoutSec' 120 1 3600
    $workDir = Get-SLArg $A 'workDir'
    # Errors are written to stderr as plain text (Windows PowerShell 5.1 would otherwise send them as CLIXML).
    $wrapped = "`$ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; `$OutputEncoding=[Text.Encoding]::UTF8; if (`$PSVersionTable.PSVersion.Major -ge 7) { `$PSStyle.OutputRendering='PlainText' };`n" +
        "& {`n" + $cmd + "`n} 2>&1 | ForEach-Object { if (`$_ -is [System.Management.Automation.ErrorRecord]) { [Console]::Error.WriteLine((`$_ | Out-String -Width 250).TrimEnd()) } else { `$_ } } | Out-String -Stream -Width 250`n" +
        "if (`$global:LASTEXITCODE) { exit `$global:LASTEXITCODE }"
    $enc = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($wrapped))

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = (Get-Process -Id $PID).Path
    $psi.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand $enc"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    if ($workDir) { $psi.WorkingDirectory = (Resolve-SLPath ([string]$workDir) -AllowRoot) }

    $p = [System.Diagnostics.Process]::Start($psi)
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    $timedOut = $false
    if (-not $p.WaitForExit([int]($timeout * 1000))) {
        $timedOut = $true
        try {
            if (Test-SLWindows) { & taskkill.exe /PID $p.Id /T /F 2>&1 | Out-Null } else { $p.Kill() }
        } catch { }
        [void]$p.WaitForExit(5000)
    }
    [void]$outTask.Wait(5000); [void]$errTask.Wait(5000)
    $out = ''; $err = ''
    if ($outTask.IsCompleted) { $out = $outTask.Result }
    if ($errTask.IsCompleted) { $err = $errTask.Result }
    $ansi = [char]27 + '\[[0-9;?]*[A-Za-z]'
    $out = $out -replace $ansi, ''
    $err = $err -replace $ansi, ''
    $trunc = $false
    if ($out.Length -gt $script:MaxOutput) { $out = $out.Substring($out.Length - $script:MaxOutput); $trunc = $true }
    if ($err.Length -gt $script:MaxOutput) { $err = $err.Substring($err.Length - $script:MaxOutput); $trunc = $true }
    $code = $null
    if ($p.HasExited) { $code = $p.ExitCode }
    return [pscustomobject]@{ ok = $true; exitCode = $code; stdout = $out; stderr = $err; timedOut = $timedOut; truncated = $trunc }
}

function Invoke-SLPs {
    param($A)
    $name = Get-SLArg $A 'name'
    if ($name -and ([string]$name -notmatch '^[\w\.\-\* ]{1,100}$')) { throw 'Invalid process name filter' }
    $procs = if ($name) { @(Get-Process -Name ([string]$name) -ErrorAction SilentlyContinue) } else { @(Get-Process) }
    $list = foreach ($p in $procs) {
        $path = $null; $title = $null; $cpu = $null
        try { $path = $p.Path } catch { }
        try { $title = $p.MainWindowTitle } catch { }
        try { $cpu = [Math]::Round($p.CPU, 1) } catch { }
        [pscustomobject]@{ id = $p.Id; name = $p.ProcessName; memMB = [Math]::Round($p.WorkingSet64 / 1MB, 1); cpuSec = $cpu; title = $title; path = $path }
    }
    return [pscustomobject]@{ ok = $true; processes = @($list) }
}

function Invoke-SLStart {
    param($A)
    $path = [string](Get-SLRequiredArg $A 'path')
    if ($script:AllowExec) {
        if (-not [IO.Path]::IsPathRooted($path)) { throw 'Path must be absolute' }
        $full = [IO.Path]::GetFullPath($path)
    } else {
        $full = Resolve-SLPath $path
    }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Program not found: $full" }
    $argsStr = [string](Get-SLArg $A 'args' '')
    $workDir = Get-SLArg $A 'workDir'
    if ($workDir) { $wd = Resolve-SLPath ([string]$workDir) -AllowRoot } else { $wd = Split-Path -Parent $full }
    $wait = [bool](Get-SLArg $A 'wait' $false)
    $timeout = Get-SLIntArg $A 'timeoutSec' 600 1 7200
    $sp = @{ FilePath = $full; WorkingDirectory = $wd; PassThru = $true }
    if ($argsStr.Length -gt 0) { $sp['ArgumentList'] = $argsStr }
    $p = Start-Process @sp
    $res = [pscustomobject]@{ ok = $true; pid = $p.Id; path = $full; exited = $false; exitCode = $null; timedOut = $false }
    if ($wait) {
        if ($p.WaitForExit([int]($timeout * 1000))) {
            $res.exited = $true; $res.exitCode = $p.ExitCode
        } else { $res.timedOut = $true }
    }
    return $res
}

function Invoke-SLStop {
    param($A)
    $id = Get-SLArg $A 'id'
    $name = Get-SLArg $A 'name'
    $force = [bool](Get-SLArg $A 'force' $true)
    $targets = @()
    if ($null -ne $id) {
        $n = Get-SLIntArg $A 'id' 0 1 2147483647
        $targets = @(Get-Process -Id ([int]$n) -ErrorAction SilentlyContinue)
    } elseif ($name) {
        if ([string]$name -notmatch '^[\w\.\- ]{1,100}$') { throw 'Invalid process name' }
        $targets = @(Get-Process -Name ([string]$name) -ErrorAction SilentlyContinue)
    } else { throw "Provide 'id' or 'name'" }
    if ($targets | Where-Object { $_.Id -eq $PID }) { throw 'Refusing to stop the SimLink agent itself' }
    $stopped = @()
    foreach ($t in $targets) {
        if ($force) { Stop-Process -Id $t.Id -Force -ErrorAction SilentlyContinue }
        else { try { [void]$t.CloseMainWindow() } catch { } }
        $stopped += [pscustomobject]@{ id = $t.Id; name = $t.ProcessName }
    }
    return [pscustomobject]@{ ok = $true; stopped = $stopped; count = $stopped.Count }
}

function Invoke-SLLs {
    param($A)
    $full = Resolve-SLPath ([string](Get-SLRequiredArg $A 'path')) -AllowRoot
    if (-not (Test-Path -LiteralPath $full -PathType Container)) { throw "Folder not found: $full" }
    $items = foreach ($i in (Get-ChildItem -LiteralPath $full -Force -ErrorAction SilentlyContinue)) {
        $isDir = $i.PSIsContainer
        $size = $null
        if (-not $isDir) { $size = $i.Length }
        [pscustomobject]@{ name = $i.Name; type = $(if ($isDir) { 'dir' } else { 'file' }); size = $size; modified = $i.LastWriteTime.ToString('o') }
    }
    return [pscustomobject]@{ ok = $true; path = $full; items = @($items) }
}

function Invoke-SLStat {
    param($A)
    $full = Resolve-SLPath ([string](Get-SLRequiredArg $A 'path')) -AllowRoot
    $withHash = [bool](Get-SLArg $A 'hash' $false)
    if (-not (Test-Path -LiteralPath $full)) { return [pscustomobject]@{ ok = $true; path = $full; exists = $false } }
    $i = Get-Item -LiteralPath $full -Force
    $res = [pscustomobject]@{ ok = $true; path = $full; exists = $true; type = $(if ($i.PSIsContainer) { 'dir' } else { 'file' }); size = $null; modified = $i.LastWriteTime.ToString('o'); sha256 = $null }
    if (-not $i.PSIsContainer) {
        $res.size = $i.Length
        if ($withHash) { $res.sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    return $res
}

function Invoke-SLRead {
    param($A)
    $full = Resolve-SLPath ([string](Get-SLRequiredArg $A 'path'))
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "File not found: $full" }
    $offset = Get-SLIntArg $A 'offset' 0 0 ([int64]::MaxValue)
    $length = Get-SLIntArg $A 'length' $script:MaxChunk 1 $script:MaxChunk
    $fs = New-Object System.IO.FileStream($full, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $size = $fs.Length
        if ($offset -gt $size) { throw 'Offset beyond end of file' }
        $toRead = [int][Math]::Min([int64]$length, $size - $offset)
        $buf = New-Object byte[] $toRead
        [void]$fs.Seek($offset, [IO.SeekOrigin]::Begin)
        $got = 0
        while ($got -lt $toRead) {
            $n = $fs.Read($buf, $got, $toRead - $got)
            if ($n -le 0) { break }
            $got += $n
        }
        if ($got -ne $toRead) { $b2 = New-Object byte[] $got; [Array]::Copy($buf, $b2, $got); $buf = $b2 }
        return @{ Result = [pscustomobject]@{ ok = $true; offset = $offset; length = $got; size = $size; eof = (($offset + $got) -ge $size) }; Data = $buf }
    } finally { $fs.Dispose() }
}

function Invoke-SLUploadBegin {
    param($A)
    $dest = Resolve-SLPath ([string](Get-SLRequiredArg $A 'dest'))
    $size = Get-SLIntArg $A 'size' 0 0 ([int64]1TB)
    $sha = ([string](Get-SLRequiredArg $A 'sha256')).ToLowerInvariant()
    if ($sha -notmatch '^[0-9a-f]{64}$') { throw 'Invalid sha256' }
    $overwrite = [bool](Get-SLArg $A 'overwrite' $false)
    if ((Test-Path -LiteralPath $dest -PathType Container)) { throw "Destination is a folder: $dest" }
    if ((Test-Path -LiteralPath $dest) -and -not $overwrite) { throw "Destination exists (use overwrite): $dest" }
    $drive = [IO.Path]::GetPathRoot($script:StagingDir)
    try {
        $di = New-Object IO.DriveInfo($drive)
        if ($di.AvailableFreeSpace -lt ($size * 2 + 100MB)) { throw "Not enough free disk space on $drive" }
    } catch [System.ArgumentException] { }
    $id = [guid]::NewGuid().ToString('N')
    $part = Join-Path $script:StagingDir "$id.part"
    [IO.File]::WriteAllBytes($part, (New-Object byte[] 0))
    $script:Uploads[$id] = @{ Dest = $dest; Size = $size; Sha = $sha; Part = $part; Overwrite = $overwrite; Created = Get-Date }
    return [pscustomobject]@{ ok = $true; id = $id; dest = $dest }
}

function Get-SLUpload {
    param($A)
    $id = [string](Get-SLRequiredArg $A 'id')
    if ($id -notmatch '^[0-9a-f]{32}$' -or -not $script:Uploads.ContainsKey($id)) { throw 'Unknown upload id' }
    return $script:Uploads[$id]
}

function Invoke-SLUploadChunk {
    param($A, [byte[]]$Data)
    $u = Get-SLUpload $A
    $offset = Get-SLIntArg $A 'offset' 0 0 ([int64]1TB)
    if ($null -eq $Data -or $Data.Length -eq 0) { throw 'Empty chunk' }
    if ($Data.Length -gt $script:MaxChunk) { throw 'Chunk too large' }
    $fs = New-Object System.IO.FileStream($u.Part, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        if ($fs.Length -ne $offset) { throw "Offset mismatch (have $($fs.Length), got $offset)" }
        if (($fs.Length + $Data.Length) -gt $u.Size) { throw 'Upload exceeds declared size' }
        $fs.Write($Data, 0, $Data.Length)
        $received = $fs.Length
    } finally { $fs.Dispose() }
    return [pscustomobject]@{ ok = $true; received = $received }
}

function Invoke-SLUploadEnd {
    param($A)
    $u = Get-SLUpload $A
    $id = [string](Get-SLArg $A 'id')
    try {
        $len = (Get-Item -LiteralPath $u.Part).Length
        if ($len -ne $u.Size) { throw "Size mismatch (expected $($u.Size), got $len)" }
        $h = (Get-FileHash -LiteralPath $u.Part -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($h -ne $u.Sha) { throw 'SHA-256 mismatch - file corrupted in transfer' }
        $parent = Split-Path -Parent $u.Dest
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        if (Test-Path -LiteralPath $u.Dest) {
            if (-not $u.Overwrite) { throw 'Destination appeared during upload' }
            Remove-Item -LiteralPath $u.Dest -Force
        }
        Move-Item -LiteralPath $u.Part -Destination $u.Dest
        return [pscustomobject]@{ ok = $true; path = $u.Dest; size = $len; sha256 = $h }
    } catch {
        Remove-Item -LiteralPath $u.Part -Force -ErrorAction SilentlyContinue
        throw
    } finally { $script:Uploads.Remove($id) }
}

function Invoke-SLUploadAbort {
    param($A)
    $u = Get-SLUpload $A
    Remove-Item -LiteralPath $u.Part -Force -ErrorAction SilentlyContinue
    $script:Uploads.Remove([string](Get-SLArg $A 'id'))
    return [pscustomobject]@{ ok = $true }
}

function Invoke-SLMkdir {
    param($A)
    $full = Resolve-SLPath ([string](Get-SLRequiredArg $A 'path'))
    New-Item -ItemType Directory -Path $full -Force | Out-Null
    return [pscustomobject]@{ ok = $true; path = $full }
}

function Invoke-SLDelete {
    param($A)
    $full = Resolve-SLPath ([string](Get-SLRequiredArg $A 'path'))
    $recurse = [bool](Get-SLArg $A 'recurse' $false)
    if (-not (Test-Path -LiteralPath $full)) { return [pscustomobject]@{ ok = $true; path = $full; deleted = $false } }
    if ((Test-Path -LiteralPath $full -PathType Container) -and -not $recurse) {
        if (@(Get-ChildItem -LiteralPath $full -Force).Count -gt 0) { throw 'Folder is not empty (use recurse)' }
    }
    Remove-Item -LiteralPath $full -Recurse:$recurse -Force
    return [pscustomobject]@{ ok = $true; path = $full; deleted = $true }
}

function Invoke-SLExtract {
    param($A)
    $zip = Resolve-SLPath ([string](Get-SLRequiredArg $A 'zip'))
    $dest = Resolve-SLPath ([string](Get-SLRequiredArg $A 'dest'))
    $clean = [bool](Get-SLArg $A 'clean' $false)
    $deleteZip = [bool](Get-SLArg $A 'deleteZip' $true)
    if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) { throw "Zip not found: $zip" }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if ($clean -and (Test-Path -LiteralPath $dest)) { Remove-Item -LiteralPath $dest -Recurse -Force }
    if (-not (Test-Path -LiteralPath $dest)) { New-Item -ItemType Directory -Path $dest -Force | Out-Null }
    $destFull = [IO.Path]::GetFullPath($dest).TrimEnd('\', '/') + (Get-SLSep)
    $count = 0
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($e in $archive.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $destFull $e.FullName))
            if (-not $target.StartsWith($destFull, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe path in zip: $($e.FullName)" }
            if ($e.FullName.EndsWith('/') -or $e.FullName.EndsWith('\')) {
                New-Item -ItemType Directory -Path $target -Force | Out-Null
                continue
            }
            $dir = Split-Path -Parent $target
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $target, $true)
            $count++
        }
    } finally { $archive.Dispose() }
    if ($deleteZip) { Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue }
    return [pscustomobject]@{ ok = $true; dest = $dest; files = $count }
}

function Invoke-SLTail {
    param($A)
    $full = Resolve-SLPath ([string](Get-SLRequiredArg $A 'path'))
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "File not found: $full" }
    $lines = Get-SLIntArg $A 'lines' 100 1 5000
    $fs = New-Object System.IO.FileStream($full, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $take = [int64][Math]::Min($fs.Length, [int64]($lines * 1000 + 4096))
        [void]$fs.Seek(-$take, [IO.SeekOrigin]::End)
        $buf = New-Object byte[] $take
        $got = 0
        while ($got -lt $take) { $n = $fs.Read($buf, $got, [int]($take - $got)); if ($n -le 0) { break }; $got += $n }
        $text = [Text.Encoding]::UTF8.GetString($buf, 0, $got)
    } finally { $fs.Dispose() }
    $all = $text.TrimEnd("`r", "`n") -split "`r?`n"
    if ($all.Count -gt $lines) { $all = $all[($all.Count - $lines)..($all.Count - 1)] }
    return [pscustomobject]@{ ok = $true; path = $full; text = ($all -join "`n") }
}

$script:DpiSet = $false
function Invoke-SLScreenshot {
    param($A)
    if (-not (Test-SLWindows)) { throw 'Screenshots are only supported on Windows' }
    $fmt = ([string](Get-SLArg $A 'format' 'png')).ToLowerInvariant()
    if ($fmt -notin @('png', 'jpg')) { throw "format must be 'png' or 'jpg'" }
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    if (-not $script:DpiSet) {
        try {
            Add-Type -Namespace SimLink -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();' -ErrorAction SilentlyContinue
            [void](([type]'SimLink.Dpi')::SetProcessDPIAware())
        } catch { }
        $script:DpiSet = $true
    }
    $b = ([type]'System.Windows.Forms.SystemInformation')::VirtualScreen
    $bmp = New-Object -TypeName 'System.Drawing.Bitmap' -ArgumentList $b.Width, $b.Height
    try {
        $g = ([type]'System.Drawing.Graphics')::FromImage($bmp)
        try { $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size) } finally { $g.Dispose() }
        $file = Join-Path $script:StagingDir ('screenshot-{0}.{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), $fmt)
        if ($fmt -eq 'png') { $bmp.Save($file, ([type]'System.Drawing.Imaging.ImageFormat')::Png) }
        else { $bmp.Save($file, ([type]'System.Drawing.Imaging.ImageFormat')::Jpeg) }
    } finally { $bmp.Dispose() }
    $i = Get-Item -LiteralPath $file
    return [pscustomobject]@{ ok = $true; path = $file; size = $i.Length; width = $b.Width; height = $b.Height; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
}

$script:PendingToken = $null
function Invoke-SLRotateToken {
    param($A)
    $new = [string](Get-SLRequiredArg $A 'newToken')
    if (-not (Test-SLTokenFormat $new)) { throw 'New token format invalid' }
    [void](New-SLKeys -Token $new)
    $script:PendingToken = $new
    return [pscustomobject]@{ ok = $true; rotated = $true }
}

function Save-SLPendingToken {
    if ($null -eq $script:PendingToken) { return }
    $stored = Protect-SLSecret -Secret $script:PendingToken -Scope LocalMachine
    # keep existing file ACL: overwrite content in place
    [IO.File]::WriteAllText($script:TokenFile, $stored)
    $script:Keys = New-SLKeys -Token $script:PendingToken
    $script:PendingToken = $null
    $script:Nonces = @{}
    Write-SLLog 'SECURITY' 'Token rotated by controller'
}

function Remove-SLStaleStaging {
    try {
        Get-ChildItem -LiteralPath $script:StagingDir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt (Get-Date).AddHours(-24) } | Remove-Item -Force -ErrorAction SilentlyContinue
        foreach ($k in @($script:Uploads.Keys)) {
            if ($script:Uploads[$k].Created -lt (Get-Date).AddHours(-24)) {
                Remove-Item -LiteralPath $script:Uploads[$k].Part -Force -ErrorAction SilentlyContinue
                $script:Uploads.Remove($k)
            }
        }
    } catch { }
}

function Invoke-SLAction {
    param([string]$Action, $A, [byte[]]$Data)
    switch ($Action) {
        'ping'          { return @{ Result = [pscustomobject]@{ ok = $true; pong = $true; host = [Environment]::MachineName; time = (Get-Date).ToString('o'); agentVersion = $script:SLAgentVersion } } }
        'info'          { return @{ Result = (Invoke-SLInfo) } }
        'exec'          { return @{ Result = (Invoke-SLExec $A) } }
        'ps'            { return @{ Result = (Invoke-SLPs $A) } }
        'start'         { return @{ Result = (Invoke-SLStart $A) } }
        'stop'          { return @{ Result = (Invoke-SLStop $A) } }
        'ls'            { return @{ Result = (Invoke-SLLs $A) } }
        'stat'          { return @{ Result = (Invoke-SLStat $A) } }
        'read'          { return (Invoke-SLRead $A) }
        'upload_begin'  { return @{ Result = (Invoke-SLUploadBegin $A) } }
        'upload_chunk'  { return @{ Result = (Invoke-SLUploadChunk $A $Data) } }
        'upload_end'    { return @{ Result = (Invoke-SLUploadEnd $A) } }
        'upload_abort'  { return @{ Result = (Invoke-SLUploadAbort $A) } }
        'mkdir'         { return @{ Result = (Invoke-SLMkdir $A) } }
        'delete'        { return @{ Result = (Invoke-SLDelete $A) } }
        'extract'       { return @{ Result = (Invoke-SLExtract $A) } }
        'tail'          { return @{ Result = (Invoke-SLTail $A) } }
        'screenshot'    { return @{ Result = (Invoke-SLScreenshot $A) } }
        'rotate_token'  { return @{ Result = (Invoke-SLRotateToken $A) } }
        default         { throw "Unknown action '$Action'" }
    }
}

function Get-SLAuditSummary {
    param([string]$Action, $A)
    $parts = @()
    foreach ($k in 'path', 'dest', 'zip', 'name', 'id', 'offset') {
        $v = Get-SLArg $A $k
        if ($null -ne $v) { $parts += "$k=$v" }
    }
    if ($Action -eq 'exec') {
        $c = [string](Get-SLArg $A 'command' '')
        $c = $c -replace '\s+', ' '
        if ($c.Length -gt 200) { $c = $c.Substring(0, 200) + '...' }
        $parts += "command=$c"
    }
    return ($parts -join ' ')
}

# ------------------------------------------------------------------ HTTP

function Send-SLStatus {
    param($Ctx, [int]$Code)
    try {
        $Ctx.Response.StatusCode = $Code
        $Ctx.Response.ContentLength64 = 0
    } catch { }
}

function Invoke-SLRequest {
    param($Ctx)
    $req = $Ctx.Request
    $ep = $req.RemoteEndPoint
    $addr = $ep.Address
    if ($addr.IsIPv4MappedToIPv6) { $addr = $addr.MapToIPv4() }
    $ip = $addr.ToString()

    if (-not (Test-SLIPAllowed $ip)) {
        Write-SLLog 'SECURITY' "Rejected connection from non-allowed IP $ip"
        Send-SLStatus $Ctx 403; return
    }
    if (Test-SLLockedOut $ip) { Send-SLStatus $Ctx 429; return }
    if ($req.HttpMethod -ne 'POST' -or $req.Url.AbsolutePath.TrimEnd('/') -ne '/sl') { Send-SLStatus $Ctx 404; return }
    $maxBody = [int64]$script:MaxChunk + 1048576
    if ($req.ContentLength64 -gt $maxBody) { Send-SLStatus $Ctx 413; return }

    $body = $null
    try { $body = Get-SLStreamBytes -Stream $req.InputStream -MaxBytes $maxBody } catch { Send-SLStatus $Ctx 413; return }

    $hdrs = @{}
    foreach ($h in 'X-SL-V', 'X-SL-Dir', 'X-SL-TS', 'X-SL-Nonce', 'X-SL-Ref', 'X-SL-IV', 'X-SL-MAC') { $hdrs[$h] = $req.Headers[$h] }

    $frame = $null
    try {
        $frame = Read-SLFrame -Keys $script:Keys -ExpectDir req -Headers $hdrs -Body $body -WindowSec $script:WindowSec
        if (-not (Test-SLNonceFresh -Nonce $frame.Nonce -TS $frame.TS)) { throw 'AUTH:replayed request' }
    } catch {
        Register-SLFailure $ip
        Write-SLLog 'SECURITY' "Auth failure from ${ip}: $($_.Exception.Message)"
        Send-SLStatus $Ctx 401; return
    }

    $action = [string](Get-SLArg $frame.Object 'action' '')
    $a = Get-SLArg $frame.Object 'args'
    $outData = $null
    $started = Get-Date
    try {
        if ($action -notmatch '^[a-z_]{2,32}$') { throw 'Invalid action' }
        $r = Invoke-SLAction -Action $action -A $a -Data $frame.Data
        $result = $r.Result
        if ($r.ContainsKey('Data')) { $outData = $r.Data }
        if ($action -notin @('upload_chunk', 'read', 'ping')) {
            Write-SLLog 'AUDIT' ("{0} {1} OK ({2} ms) {3}" -f $ip, $action, [int]((Get-Date) - $started).TotalMilliseconds, (Get-SLAuditSummary $action $a))
        }
    } catch {
        $result = [pscustomobject]@{ ok = $false; error = $_.Exception.Message }
        Write-SLLog 'AUDIT' ("{0} {1} FAILED: {2} {3}" -f $ip, $action, $_.Exception.Message, (Get-SLAuditSummary $action $a))
    }

    $resp = New-SLFrame -Keys $script:Keys -Dir resp -Ref $frame.Nonce -Object $result -Data $outData
    $Ctx.Response.StatusCode = 200
    $Ctx.Response.ContentType = 'application/octet-stream'
    foreach ($k in $resp.Headers.Keys) { $Ctx.Response.Headers[$k] = $resp.Headers[$k] }
    $Ctx.Response.ContentLength64 = $resp.Body.Length
    $Ctx.Response.OutputStream.Write($resp.Body, 0, $resp.Body.Length)

    if ($action -eq 'rotate_token' -and $result.ok) { $script:RotateAfterResponse = $true }
}

# ------------------------------------------------------------------ main

$mutex = New-Object System.Threading.Mutex($false, 'Global\SimLinkAgent')
if (-not $mutex.WaitOne(0)) { Write-Host 'SimLink agent is already running.'; exit 0 }

try {
    Write-SLLog 'INFO' "SimLink agent $($script:SLAgentVersion) starting on $($script:Prefix) (allowed IPs: $($script:AllowedIPs -join ', '))"
    Remove-SLOldLogs
    while ($true) {
        $listener = $null
        try {
            $script:Keys = Get-SLAgentKeys
            Remove-SLStaleStaging
            $listener = New-Object System.Net.HttpListener
            $listener.Prefixes.Add($script:Prefix)
            $listener.Start()
            Write-SLLog 'INFO' 'Listening.'
            $lastClean = Get-Date
            while ($listener.IsListening) {
                $ctx = $listener.GetContext()
                $script:RotateAfterResponse = $false
                try { Invoke-SLRequest $ctx }
                catch { Write-SLLog 'ERROR' "Request handling error: $($_.Exception.Message)"; Send-SLStatus $ctx 500 }
                finally { try { $ctx.Response.Close() } catch { } }
                if ($script:RotateAfterResponse) {
                    try { Save-SLPendingToken } catch { Write-SLLog 'ERROR' "Token save failed: $($_.Exception.Message)" }
                }
                if (((Get-Date) - $lastClean).TotalHours -ge 1) { Remove-SLStaleStaging; $lastClean = Get-Date }
            }
        } catch {
            Write-SLLog 'ERROR' "Listener error: $($_.Exception.Message) - restarting in 10s"
            Start-Sleep -Seconds 10
        } finally {
            if ($null -ne $listener) { try { $listener.Close() } catch { } }
        }
    }
} finally {
    $mutex.ReleaseMutex()
}
