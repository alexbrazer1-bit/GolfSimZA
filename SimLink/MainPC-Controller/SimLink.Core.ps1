# SimLink.Core.ps1 - all controller logic (transport, file transfer, commands, GolfSimZA helpers, bridge).
# Dot-sourced by SimLink.ps1 (command line), SimLink-Panel.ps1 (control panel), the bridge and Test-SimLink.ps1.
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.
# Optional before dot-sourcing: $script:SLConfigDir = '<folder>' (default %APPDATA%\SimLink).

$script:SLCoreDir = $PSScriptRoot
. (Join-Path $PSScriptRoot 'SimLink.Common.ps1')
if (-not (Get-Variable -Name SLConfigDir -Scope Script -ErrorAction SilentlyContinue)) { $script:SLConfigDir = $null }
$script:SLControllerVersion = '1.1.1'

$script:ChunkBytes = 4194304   # 4 MB per request (agent max is 8 MB)

# ------------------------------------------------------------------ config / token

function Get-SLConfigDir {
    if ($script:SLConfigDir) { return $script:SLConfigDir }
    if (Test-SLWindows) { return (Join-Path $env:APPDATA 'SimLink') }
    return (Join-Path $HOME '.simlink')
}

function Get-SLControllerConfig {
    $dir = Get-SLConfigDir
    $cfgFile = Join-Path $dir 'controller.json'
    $tokFile = Join-Path $dir 'token.dat'
    if (-not (Test-Path -LiteralPath $cfgFile) -or -not (Test-Path -LiteralPath $tokFile)) {
        throw 'Not paired yet. Open SimLink Control Panel > Settings and enter the pairing code (or run: simlink setup).'
    }
    $cfg = Get-Content -LiteralPath $cfgFile -Raw | ConvertFrom-Json
    $tok = Unprotect-SLSecret -Stored (Get-Content -LiteralPath $tokFile -Raw) -Scope CurrentUser
    return @{ Address = [string]$cfg.Address; Port = [int]$cfg.Port; Keys = (New-SLKeys -Token $tok) }
}

function Save-SLControllerToken {
    param([string]$Tok)
    $dir = Get-SLConfigDir
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $tokFile = Join-Path $dir 'token.dat'
    [IO.File]::WriteAllText($tokFile, (Protect-SLSecret -Secret $Tok -Scope CurrentUser))
    if (Test-SLWindows) {
        $me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        & icacls.exe $tokFile /inheritance:r /grant:r "${me}:F" '*S-1-5-18:F' | Out-Null
    } else {
        & chmod 600 $tokFile 2>$null
    }
}

function Resolve-SLLocalPath {
    param([string]$P)
    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($P)
}

# ------------------------------------------------------------------ transport

$script:Conn = $null
function Get-SLConn {
    if ($null -eq $script:Conn) { $script:Conn = Get-SLControllerConfig }
    return $script:Conn
}

function Invoke-SLRemote {
    # Returns @{ Result = pscustomobject; Data = byte[] }. Throws on transport/auth/remote errors.
    param([Parameter(Mandatory)][string]$Action, $ArgsObj = @{}, [byte[]]$Data, [int]$Timeout = 60)
    $c = Get-SLConn
    $frame = New-SLFrame -Keys $c.Keys -Dir req -Object @{ action = $Action; args = $ArgsObj } -Data $Data
    $url = "http://$($c.Address):$($c.Port)/sl/"
    $req = [System.Net.HttpWebRequest]::Create($url)
    $req.Method = 'POST'
    $req.Proxy = $null
    $req.KeepAlive = $false
    $req.Timeout = $Timeout * 1000
    $req.ReadWriteTimeout = $Timeout * 1000
    $req.ContentType = 'application/octet-stream'
    foreach ($k in $frame.Headers.Keys) { $req.Headers[$k] = $frame.Headers[$k] }
    $req.ContentLength = $frame.Body.Length
    $resp = $null
    try {
        $rs = $req.GetRequestStream()
        try { $rs.Write($frame.Body, 0, $frame.Body.Length) } finally { $rs.Dispose() }
        $resp = $req.GetResponse()
    } catch [System.Net.WebException] {
        $we = $_.Exception
        if ($null -ne $we.Response) {
            $code = [int]$we.Response.StatusCode
            $we.Response.Dispose()
            switch ($code) {
                401 { throw 'Sim PC rejected authentication (wrong token, or PC clocks differ by more than 2 minutes).' }
                403 { throw 'Sim PC refused this IP address. Re-run Install-Agent.ps1 on the sim PC with this PC''s IP in -ControllerIP.' }
                429 { throw 'This PC is temporarily locked out after repeated failed logins (15 min).' }
                413 { throw 'Request too large for the agent.' }
                default { throw "Sim PC returned HTTP $code." }
            }
        }
        throw "Cannot reach sim PC at $($c.Address):$($c.Port) - is it on, logged in and on the network? ($($we.Message))"
    }
    try {
        $body = Get-SLStreamBytes -Stream $resp.GetResponseStream() -MaxBytes 0
        $hdrs = @{}
        foreach ($h in 'X-SL-V', 'X-SL-Dir', 'X-SL-TS', 'X-SL-Nonce', 'X-SL-Ref', 'X-SL-IV', 'X-SL-MAC') { $hdrs[$h] = $resp.Headers[$h] }
    } finally { $resp.Dispose() }
    try {
        $f = Read-SLFrame -Keys $c.Keys -ExpectDir resp -Headers $hdrs -Body $body -ExpectRef $frame.Nonce -WindowSec 300
    } catch {
        throw "Response from sim PC failed verification: $($_.Exception.Message)"
    }
    $r = $f.Object
    $okProp = $r.PSObject.Properties['ok']
    if ($null -eq $okProp -or -not $okProp.Value) {
        $e = $r.PSObject.Properties['error']
        $msg = 'unknown error'
        if ($null -ne $e) { $msg = $e.Value }
        throw "Sim PC: $msg"
    }
    return @{ Result = $r; Data = $f.Data }
}

# ------------------------------------------------------------------ file transfer helpers

function Send-SLFile {
    param([string]$LocalFile, [string]$RemoteFile, [bool]$Force)
    $fi = Get-Item -LiteralPath $LocalFile
    $sha = (Get-FileHash -LiteralPath $LocalFile -Algorithm SHA256).Hash.ToLowerInvariant()
    $b = Invoke-SLRemote -Action upload_begin -ArgsObj @{ dest = $RemoteFile; size = $fi.Length; sha256 = $sha; overwrite = $Force }
    $uid = $b.Result.id
    $fs = [IO.File]::OpenRead($fi.FullName)
    try {
        $buf = New-Object byte[] $script:ChunkBytes
        $offset = [int64]0
        $sw = [Diagnostics.Stopwatch]::StartNew()
        while ($offset -lt $fi.Length) {
            $n = $fs.Read($buf, 0, $buf.Length)
            if ($n -le 0) { break }
            $chunk = $buf
            if ($n -ne $buf.Length) { $chunk = New-Object byte[] $n; [Array]::Copy($buf, $chunk, $n) }
            $attempt = 0
            while ($true) {
                try {
                    [void](Invoke-SLRemote -Action upload_chunk -ArgsObj @{ id = $uid; offset = $offset } -Data $chunk -Timeout 120)
                    break
                } catch {
                    $attempt++
                    if ($attempt -ge 3 -or $_.Exception.Message -like 'Sim PC:*') { throw }
                    Start-Sleep -Seconds 2
                }
            }
            $offset += $n
            if ($fi.Length -gt 0) {
                $pct = [int](100 * $offset / $fi.Length)
                $mbps = 0
                if ($sw.Elapsed.TotalSeconds -gt 0) { $mbps = [Math]::Round(($offset / 1MB) / $sw.Elapsed.TotalSeconds, 1) }
                Write-Progress -Activity "Uploading $($fi.Name)" -Status "$pct% ($mbps MB/s)" -PercentComplete $pct
            }
        }
    } catch {
        try { [void](Invoke-SLRemote -Action upload_abort -ArgsObj @{ id = $uid }) } catch { }
        throw
    } finally {
        $fs.Dispose()
        Write-Progress -Activity "Uploading $($fi.Name)" -Completed
    }
    $e = Invoke-SLRemote -Action upload_end -ArgsObj @{ id = $uid } -Timeout 600
    return $e.Result
}

function Receive-SLFile {
    param([string]$RemoteFile, [string]$LocalFile, [bool]$Force)
    if ((Test-Path -LiteralPath $LocalFile) -and -not $Force) { throw "Local file exists (use -Overwrite): $LocalFile" }
    $st = (Invoke-SLRemote -Action stat -ArgsObj @{ path = $RemoteFile; hash = $true } -Timeout 600).Result
    if (-not $st.exists) { throw "Remote file not found: $RemoteFile" }
    if ($st.type -ne 'file') { throw "Remote path is a folder: $RemoteFile" }
    $parent = Split-Path -Parent $LocalFile
    if ($parent -and -not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $tmp = "$LocalFile.simlink-part"
    $fs = [IO.File]::Create($tmp)
    try {
        $offset = [int64]0
        while ($offset -lt [int64]$st.size) {
            $r = Invoke-SLRemote -Action read -ArgsObj @{ path = $RemoteFile; offset = $offset; length = $script:ChunkBytes } -Timeout 120
            if ($r.Data.Length -eq 0) { break }
            $fs.Write($r.Data, 0, $r.Data.Length)
            $offset += $r.Data.Length
            $pct = [int](100 * $offset / [Math]::Max(1, [int64]$st.size))
            Write-Progress -Activity "Downloading $(Split-Path -Leaf $RemoteFile)" -PercentComplete $pct
        }
    } finally {
        $fs.Dispose()
        Write-Progress -Activity 'Downloading' -Completed
    }
    $h = (Get-FileHash -LiteralPath $tmp -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($h -ne $st.sha256) {
        Remove-Item -LiteralPath $tmp -Force
        throw 'SHA-256 mismatch after download (file changed on the sim PC while downloading?). Try again.'
    }
    if (Test-Path -LiteralPath $LocalFile) { Remove-Item -LiteralPath $LocalFile -Force }
    Move-Item -LiteralPath $tmp -Destination $LocalFile
    return [pscustomobject]@{ ok = $true; local = $LocalFile; remote = $RemoteFile; size = $st.size; sha256 = $h }
}

function Send-SLFolder {
    param([string]$LocalDir, [string]$RemoteDir, [bool]$DoClean)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $info = (Invoke-SLRemote -Action info).Result
    $zipName = 'push-{0}.zip' -f ([guid]::NewGuid().ToString('N'))
    $tmpZip = Join-Path ([IO.Path]::GetTempPath()) $zipName
    try {
        Write-Host "Zipping $LocalDir ..."
        [IO.Compression.ZipFile]::CreateFromDirectory($LocalDir, $tmpZip, [IO.Compression.CompressionLevel]::Fastest, $false)
        $sep = '\'
        if (([string]$info.stagingDir).Contains('/')) { $sep = '/' }
        $remoteZip = ([string]$info.stagingDir).TrimEnd('\', '/') + $sep + $zipName
        $up = Send-SLFile -LocalFile $tmpZip -RemoteFile $remoteZip -Force $false
        $x = (Invoke-SLRemote -Action extract -ArgsObj @{ zip = $remoteZip; dest = $RemoteDir; clean = $DoClean; deleteZip = $true } -Timeout 1800).Result
        return [pscustomobject]@{ ok = $true; local = $LocalDir; remote = $RemoteDir; files = $x.files; zipBytes = $up.size; cleaned = $DoClean }
    } finally {
        Remove-Item -LiteralPath $tmpZip -Force -ErrorAction SilentlyContinue
    }
}

function Join-SLRemotePath {
    param([string]$Base, [string]$Child)
    $sep = '\'
    if ($Base.Contains('/') -and -not $Base.Contains('\')) { $sep = '/' }
    return $Base.TrimEnd('\', '/') + $sep + $Child.TrimStart('\', '/')
}

# ------------------------------------------------------------------ commands

function Invoke-SLCommand {
    param([string]$Cmd, [hashtable]$P)

    function Need([string]$k) {
        if (-not $P.ContainsKey($k) -or $null -eq $P[$k] -or ([string]$P[$k]).Length -eq 0) { throw "Missing -$k for '$Cmd'" }
        return $P[$k]
    }
    function Opt([string]$k, $def) { if ($P.ContainsKey($k) -and $null -ne $P[$k] -and ([string]$P[$k]).Length -gt 0) { return $P[$k] } return $def }

    switch ($Cmd.ToLowerInvariant()) {
        'ping' {
            $sw = [Diagnostics.Stopwatch]::StartNew()
            $r = (Invoke-SLRemote -Action ping -Timeout 10).Result
            $r | Add-Member -NotePropertyName latencyMs -NotePropertyValue $sw.ElapsedMilliseconds
            return $r
        }
        'info' { return (Invoke-SLRemote -Action info).Result }
        'exec' {
            $t = [int](Opt 'TimeoutSec' 120)
            if ($t -le 0) { $t = 120 }
            $a = @{ command = [string](Need 'Run'); timeoutSec = $t }
            $wd = Opt 'WorkDir' $null
            if ($wd) { $a['workDir'] = $wd }
            return (Invoke-SLRemote -Action exec -ArgsObj $a -Timeout ($t + 30)).Result
        }
        'ls'     { return (Invoke-SLRemote -Action ls -ArgsObj @{ path = [string](Need 'Remote') }).Result }
        'stat'   { return (Invoke-SLRemote -Action stat -ArgsObj @{ path = [string](Need 'Remote'); hash = $true } -Timeout 600).Result }
        'mkdir'  { return (Invoke-SLRemote -Action mkdir -ArgsObj @{ path = [string](Need 'Remote') }).Result }
        'delete' { return (Invoke-SLRemote -Action delete -ArgsObj @{ path = [string](Need 'Remote'); recurse = [bool](Opt 'Recurse' $false) } -Timeout 600).Result }
        'tail'   { return (Invoke-SLRemote -Action tail -ArgsObj @{ path = [string](Need 'Remote'); lines = [int](Opt 'Lines' 100) }).Result }
        'ps' {
            $a = @{}
            $n = Opt 'Name' $null
            if ($n) { $a['name'] = $n }
            return (Invoke-SLRemote -Action ps -ArgsObj $a).Result
        }
        'start' {
            $t = [int](Opt 'TimeoutSec' 600)
            if ($t -le 0) { $t = 600 }
            $a = @{ path = [string](Need 'Path'); wait = [bool](Opt 'Wait' $false); timeoutSec = $t }
            $ar = Opt 'Arguments' $null
            if ($ar) { $a['args'] = $ar }
            $wd = Opt 'WorkDir' $null
            if ($wd) { $a['workDir'] = $wd }
            $to = 60
            if ($a.wait) { $to = $t + 30 }
            return (Invoke-SLRemote -Action start -ArgsObj $a -Timeout $to).Result
        }
        'stop' {
            $a = @{ force = -not [bool](Opt 'Graceful' $false) }
            $i = [int](Opt 'Id' 0)
            if ($i -gt 0) { $a['id'] = $i } else { $a['name'] = [string](Need 'Name') }
            return (Invoke-SLRemote -Action stop -ArgsObj $a).Result
        }
        'push' {
            $l = Resolve-SLLocalPath ([string](Need 'Local'))
            $r = [string](Need 'Remote')
            if (Test-Path -LiteralPath $l -PathType Container) { return (Send-SLFolder -LocalDir $l -RemoteDir $r -DoClean ([bool](Opt 'Clean' $false))) }
            if (-not (Test-Path -LiteralPath $l -PathType Leaf)) { throw "Local path not found: $l" }
            return (Send-SLFile -LocalFile $l -RemoteFile $r -Force ([bool](Opt 'Overwrite' $false)))
        }
        'pull' {
            $r = [string](Need 'Remote')
            $lp = Opt 'Local' $null
            if (-not $lp) { $lp = Join-Path (Get-Location) (Split-Path -Leaf $r) }
            return (Receive-SLFile -RemoteFile $r -LocalFile (Resolve-SLLocalPath ([string]$lp)) -Force ([bool](Opt 'Overwrite' $false)))
        }
        'screenshot' {
            $fmt = [string](Opt 'Format' 'png')
            $s = (Invoke-SLRemote -Action screenshot -ArgsObj @{ format = $fmt } -Timeout 60).Result
            $lp = Opt 'Local' $null
            if (-not $lp) { $lp = Join-Path (Get-Location) ('sim-screenshot-{0}.{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $fmt) }
            $dl = Receive-SLFile -RemoteFile $s.path -LocalFile (Resolve-SLLocalPath ([string]$lp)) -Force $true
            try { [void](Invoke-SLRemote -Action delete -ArgsObj @{ path = $s.path }) } catch { }
            return [pscustomobject]@{ ok = $true; local = $dl.local; width = $s.width; height = $s.height; size = $dl.size }
        }
        'deploy' {
            $l = Resolve-SLLocalPath ([string](Need 'Local'))
            $r = [string](Need 'Remote')
            if (-not (Test-Path -LiteralPath $l -PathType Container)) { throw "Build folder not found: $l" }
            $steps = [ordered]@{}
            $sn = Opt 'StopName' $null
            if ($sn) {
                # several programs can be given: 'GolfSimZA,gspro-r10' (the R10 bridge locks files in the game folder)
                $stopped = 0
                foreach ($n in ([string]$sn -split '[,;]')) {
                    $n = $n.Trim()
                    if ($n) { $stopped += [int](Invoke-SLRemote -Action stop -ArgsObj @{ name = $n; force = $true }).Result.count }
                }
                $steps['stopped'] = $stopped
                Start-Sleep -Seconds 2
            }
            $steps['push'] = Send-SLFolder -LocalDir $l -RemoteDir $r -DoClean ([bool](Opt 'Clean' $false))
            $ln = Opt 'Launch' $null
            if ($ln) {
                $exe = [string]$ln
                if (-not ($exe -match '^[A-Za-z]:\\' -or $exe.StartsWith('/'))) { $exe = Join-SLRemotePath $r $exe }
                $a = @{ path = $exe }
                $la = Opt 'LaunchArgs' $null
                if ($la) { $a['args'] = $la }
                $steps['launched'] = (Invoke-SLRemote -Action start -ArgsObj $a).Result
            }
            $sa = [int](Opt 'ScreenshotAfter' 0)
            if ($sa -gt 0) {
                Start-Sleep -Seconds $sa
                $sp = @{ Format = 'png' }
                $lp = Opt 'Local_Screenshot' $null
                if ($lp) { $sp['Local'] = $lp }
                $steps['screenshot'] = Invoke-SLCommand -Cmd 'screenshot' -P $sp
            }
            return [pscustomobject]@{ ok = $true; steps = [pscustomobject]$steps }
        }
        'rotate-token' {
            $new = New-SLToken
            [void](Invoke-SLRemote -Action rotate_token -ArgsObj @{ newToken = $new })
            Save-SLControllerToken -Tok $new
            $script:Conn = $null
            $chk = (Invoke-SLRemote -Action ping -Timeout 10).Result
            return [pscustomobject]@{ ok = $true; rotated = $true; verified = [bool]$chk.pong }
        }
        'gsz-status'   { return (Get-GSZStatus) }
        'gsz-releases' { return (Get-GSZReleases -UpdateFolder ([string](Opt 'UpdateFolder' $script:GSZDefaults.UpdateFolder))) }
        'gsz-update'   {
            return (Invoke-GSZUpdate -Version ([string](Opt 'Version' '')) -UpdateFolder ([string](Opt 'UpdateFolder' $script:GSZDefaults.UpdateFolder)) `
                -RemoteFolder ([string](Opt 'RemoteFolder' $script:GSZDefaults.RemoteUpdateFolder)) -ScreenshotAfter ([int](Opt 'ScreenshotAfter' 0)) `
                -ScreenshotPath ([string](Opt 'Local' '')))
        }
        'gsz-start'    { return (Start-GSZGame) }
        'gsz-stop'     {
            $r = (Invoke-SLRemote -Action stop -ArgsObj @{ name = 'GolfSimZA'; force = -not [bool](Opt 'Graceful' $false) }).Result
            try { [void](Invoke-SLRemote -Action stop -ArgsObj @{ name = 'gspro-r10'; force = $true }) } catch { }
            return $r
        }
        'gsz-log'      { return (Get-GSZLog -Lines ([int](Opt 'Lines' 200))) }
        'gsz-swap-screens' { return (Invoke-GSZSwapScreens) }
        'gsz-screens'  { return (Get-GSZScreens) }
        default { throw "Unknown command '$Cmd'. Run: simlink help" }
    }
}

# ------------------------------------------------------------------ GolfSimZA helpers

$script:GSZDefaults = @{
    UpdateFolder       = 'C:\Shared\GolfSimZA-Updates'    # on this (main) PC: latest.json + installers\
    RemoteUpdateFolder = 'C:\Shared\GolfSimZA-Updates'    # on the sim PC: where installers are copied to
    AppId              = '{8F3C2B7A-5D1E-4C9A-9E2B-6A7D0C1F4B52}_is1'
}

function Invoke-SLRemoteJson {
    # Runs a PowerShell snippet on the sim PC that writes ONE line of JSON, returns it as an object.
    param([string]$Script, [int]$TimeoutSec = 60)
    $r = (Invoke-SLRemote -Action exec -ArgsObj @{ command = $Script; timeoutSec = $TimeoutSec } -Timeout ($TimeoutSec + 30)).Result
    if ($r.exitCode -ne 0) { throw "Sim PC script failed (exit $($r.exitCode)): $($r.stderr)" }
    $line = @(($r.stdout -split "`r?`n") | Where-Object { $_.Trim().StartsWith('{') }) | Select-Object -Last 1
    if (-not $line) { throw "Unexpected output from sim PC: $($r.stdout) $($r.stderr)" }
    return ($line | ConvertFrom-Json)
}

function Get-GSZStatus {
    $appId = $script:GSZDefaults.AppId
    $s = @"
`$k = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appId' -ErrorAction SilentlyContinue
if (-not `$k) { `$k = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue | Where-Object { `$_.PSObject.Properties['DisplayName'] -and `$_.DisplayName -eq 'GolfSimZA' } | Select-Object -First 1 }
`$g = Get-Process -Name GolfSimZA -ErrorAction SilentlyContinue | Select-Object -First 1
`$r = Get-Process -Name gspro-r10 -ErrorAction SilentlyContinue | Select-Object -First 1
`$log = Get-ChildItem "`$env:USERPROFILE\AppData\LocalLow\*\GolfSimZA\Player.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
`$o = [ordered]@{ installed = [bool]`$k; version = `$null; installDir = `$null; exe = `$null; running = [bool]`$g; pid = `$null; startedAt = `$null; r10BridgeRunning = [bool]`$r; playerLog = `$null; playerLogUpdated = `$null }
if (`$k) { `$o.version = `$k.DisplayVersion; `$o.installDir = ([string]`$k.InstallLocation).TrimEnd('\'); `$o.exe = Join-Path `$o.installDir 'GolfSimZA.exe' }
if (`$g) { `$o.pid = `$g.Id; try { `$o.startedAt = `$g.StartTime.ToString('o') } catch { } }
if (`$log) { `$o.playerLog = `$log.FullName; `$o.playerLogUpdated = `$log.LastWriteTime.ToString('o') }
[pscustomobject]`$o | ConvertTo-Json -Compress
"@
    $o = Invoke-SLRemoteJson -Script $s
    $o | Add-Member -NotePropertyName ok -NotePropertyValue $true -Force
    return $o
}

function Get-GSZReleases {
    param([string]$UpdateFolder)
    $lj = Join-Path $UpdateFolder 'latest.json'
    if (-not (Test-Path -LiteralPath $lj)) { throw "No published GolfSimZA updates found ($lj missing)." }
    $j = Get-Content -LiteralPath $lj -Raw -Encoding UTF8 | ConvertFrom-Json
    $rel = @($j.releases | ForEach-Object {
        $file = Join-Path $UpdateFolder (([string]$_.installer) -replace '/', '\')
        [pscustomobject]@{ version = [string]$_.version; installer = $file; exists = (Test-Path -LiteralPath $file); sha256 = ([string]$_.sha256).ToLowerInvariant(); publishedUtc = $_.publishedUtc; notes = $_.notes }
    })
    return [pscustomobject]@{ ok = $true; latestVersion = [string]$j.latestVersion; releases = $rel }
}

function Invoke-GSZUpdate {
    # Copies a published GolfSimZA installer to the sim PC (if not already there) and installs it silently.
    param([string]$Version, [string]$UpdateFolder, [string]$RemoteFolder, [int]$ScreenshotAfter = 0, [string]$ScreenshotPath)
    $rels = Get-GSZReleases -UpdateFolder $UpdateFolder
    if (-not $Version) { $Version = $rels.latestVersion }
    $rel = $rels.releases | Where-Object { $_.version -eq $Version } | Select-Object -First 1
    if (-not $rel) { throw "GolfSimZA $Version is not in latest.json" }
    if (-not $rel.exists) { throw "Installer missing on this PC: $($rel.installer)" }
    Write-Host "Checking installer $([IO.Path]::GetFileName($rel.installer)) ..."
    $localSha = (Get-FileHash -LiteralPath $rel.installer -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($rel.sha256 -and $localSha -ne $rel.sha256) { throw "Local installer hash does not match latest.json - republish GolfSimZA $Version." }

    $before = Get-GSZStatus
    $remote = Join-SLRemotePath (Join-SLRemotePath $RemoteFolder 'installers') ([IO.Path]::GetFileName($rel.installer))
    $st = (Invoke-SLRemote -Action stat -ArgsObj @{ path = $remote; hash = $true } -Timeout 600).Result
    $copied = $false
    if (-not $st.exists -or $st.sha256 -ne $localSha) {
        Write-Host "Copying installer to the sim PC ..."
        [void](Send-SLFile -LocalFile $rel.installer -RemoteFile $remote -Force $true)
        $copied = $true
    } else { Write-Host 'Installer already on the sim PC (hash verified).' }

    if ($before.running -or $before.r10BridgeRunning) {
        Write-Host 'Closing GolfSimZA and its R10 bridge on the sim PC ...'
        [void](Invoke-SLRemote -Action stop -ArgsObj @{ name = 'GolfSimZA'; force = $true })
        try { [void](Invoke-SLRemote -Action stop -ArgsObj @{ name = 'gspro-r10'; force = $true }) } catch { }
        Start-Sleep -Seconds 2
    }
    Write-Host "Installing GolfSimZA $Version silently ..."
    $inst = (Invoke-SLRemote -Action start -ArgsObj @{ path = $remote; args = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'; wait = $true; timeoutSec = 900 } -Timeout 960).Result
    if ($inst.timedOut) { throw 'Installer did not finish within 15 minutes.' }
    if ($inst.exitCode -ne 0) { throw "Installer exited with code $($inst.exitCode)." }
    Start-Sleep -Seconds 3
    $after = Get-GSZStatus
    if ($after.version -ne $Version) { throw "Install finished but the sim PC reports version '$($after.version)' instead of $Version." }
    $res = [ordered]@{ ok = $true; version = $Version; previousVersion = $before.version; installerCopied = $copied; running = $after.running; installDir = $after.installDir }
    if ($ScreenshotAfter -gt 0) {
        Start-Sleep -Seconds $ScreenshotAfter
        $sp = @{ Format = 'png' }
        if ($ScreenshotPath) { $sp['Local'] = $ScreenshotPath }
        $res['screenshot'] = Invoke-SLCommand -Cmd 'screenshot' -P $sp
    }
    return [pscustomobject]$res
}

function Start-GSZGame {
    $s = Get-GSZStatus
    if (-not $s.installed) { throw 'GolfSimZA is not installed on the sim PC.' }
    if ($s.running) { return [pscustomobject]@{ ok = $true; alreadyRunning = $true; pid = $s.pid } }
    $r = (Invoke-SLRemote -Action start -ArgsObj @{ path = $s.exe }).Result
    return [pscustomobject]@{ ok = $true; alreadyRunning = $false; pid = $r.pid; exe = $s.exe }
}

function Get-SLWindowsDisplaysScript {
    # PowerShell prefix that loads GolfSimZA's WindowsDisplays helper (the same C# file the game uses) on the
    # sim PC. The file is uploaded once to the agent's staging folder (too big to send on a command line).
    $cs = Join-Path $script:SLCoreDir 'WindowsDisplays.cs'
    if (-not (Test-Path -LiteralPath $cs)) { throw "WindowsDisplays.cs is missing next to SimLink.Core.ps1 ($cs)." }
    $sha = (Get-FileHash -LiteralPath $cs -Algorithm SHA256).Hash.ToLowerInvariant()
    $info = (Invoke-SLRemote -Action info).Result
    $remote = Join-SLRemotePath ([string]$info.stagingDir) ('WindowsDisplays-' + $sha.Substring(0, 12) + '.cs')
    $st = (Invoke-SLRemote -Action stat -ArgsObj @{ path = $remote; hash = $true }).Result
    if (-not $st.exists -or $st.sha256 -ne $sha) { [void](Send-SLFile -LocalFile $cs -RemoteFile $remote -Force $true) }
    $q = $remote.Replace("'", "''")
    return "if (-not ('GolfSimZA.Platform.WindowsDisplays' -as [type])) { Add-Type -Path '$q' }`n"
}

function Get-GSZScreens {
    $s = (Get-SLWindowsDisplaysScript) + @'
$m = @([GolfSimZA.Platform.WindowsDisplays]::GetMonitors() | ForEach-Object { [pscustomobject]@{ device = $_.Device; name = $_.FriendlyName; x = $_.X; y = $_.Y; width = $_.Width; height = $_.Height; main = $_.Primary } })
[pscustomobject]@{ monitors = $m; layout = [GolfSimZA.Platform.WindowsDisplays]::Describe() } | ConvertTo-Json -Compress -Depth 4
'@
    $o = Invoke-SLRemoteJson -Script $s
    $o | Add-Member -NotePropertyName ok -NotePropertyValue $true -Force
    return $o
}

function Invoke-GSZSwapScreens {
    # Swaps the game and the swing-data screen on the sim PC: the other monitor becomes the Windows
    # main display. GolfSimZA 1.8.2+ restarts itself when that happens; older versions are restarted here.
    $before = Get-GSZStatus
    Write-Host 'Swapping the Windows main display on the sim PC ...'
    $s = (Get-SLWindowsDisplaysScript) + @'
$b = [GolfSimZA.Platform.WindowsDisplays]::Describe()
$e = [GolfSimZA.Platform.WindowsDisplays]::SwapPrimaryOfTwo()
Start-Sleep -Seconds 2
[pscustomobject]@{ error = $e; before = $b; after = [GolfSimZA.Platform.WindowsDisplays]::Describe() } | ConvertTo-Json -Compress
'@
    $sw = Invoke-SLRemoteJson -Script $s
    if ($sw.error) { throw "Windows refused the swap: $($sw.error)" }
    $restartedBy = 'not running'
    if ($before.running) {
        Write-Host 'Waiting for GolfSimZA to restart on the new main display ...'
        $deadline = (Get-Date).AddSeconds(30)
        $restartedBy = $null
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 3
            $st = Get-GSZStatus
            if ($st.running -and $st.pid -ne $before.pid) { $restartedBy = 'game'; break }
        }
        if (-not $restartedBy) {
            Write-Host 'Restarting GolfSimZA ...'
            [void](Invoke-SLRemote -Action stop -ArgsObj @{ name = 'GolfSimZA'; force = $true })
            Start-Sleep -Seconds 3
            [void](Start-GSZGame)
            $restartedBy = 'simlink'
        }
    }
    return [pscustomobject]@{ ok = $true; before = $sw.before; after = $sw.after; gameRestartedBy = $restartedBy }
}

function Get-GSZLog {
    param([int]$Lines = 200)
    $s = Get-GSZStatus
    if (-not $s.playerLog) { throw 'No GolfSimZA Player.log found on the sim PC yet (start the game once).' }
    $t = (Invoke-SLRemote -Action tail -ArgsObj @{ path = $s.playerLog; lines = $Lines }).Result
    return [pscustomobject]@{ ok = $true; path = $s.playerLog; updated = $s.playerLogUpdated; text = $t.text }
}


# ------------------------------------------------------------------ setup

function ConvertFrom-SLPairingCode {
    # 'SIMLINK1:<address>:<port>:<token>' -> @{ Address; Port; Token }  (returns $null if not a pairing code)
    param([string]$Code)
    if (-not $Code) { return $null }
    $c = $Code.Trim()
    if (-not $c.StartsWith('SIMLINK1:')) { return $null }
    $parts = $c.Split(':')
    if ($parts.Count -ne 4) { throw 'Pairing code is malformed (expected SIMLINK1:<ip>:<port>:<token>).' }
    $pp = 0
    if (-not [int]::TryParse($parts[2], [ref]$pp) -or $pp -lt 1 -or $pp -gt 65535) { throw 'Pairing code has an invalid port.' }
    return @{ Address = $parts[1]; Port = $pp; Token = $parts[3] }
}

function Invoke-SLSetup {
    param([string]$Address, [int]$Port = 0, [string]$Token)
    $tok = $Token
    if (-not $tok -and $env:SIMLINK_TOKEN) { $tok = $env:SIMLINK_TOKEN }
    $addr = $Address
    $p = $Port
    if (-not $tok) {
        $sec = Read-Host -Prompt 'Paste the pairing code (or token) shown on the simulator PC' -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        try { $tok = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }
    $tok = $tok.Trim()
    $pc = ConvertFrom-SLPairingCode $tok
    if ($null -ne $pc) {
        if (-not $addr) { $addr = $pc.Address }
        if ($p -le 0) { $p = $pc.Port }
        $tok = $pc.Token
    }
    if (-not $addr) { throw 'setup needs -Address <sim PC IP> (or a full pairing code)' }
    $parsed = $null
    if (-not [System.Net.IPAddress]::TryParse($addr, [ref]$parsed)) {
        if ($addr -notmatch '^[A-Za-z0-9\.\-]{1,253}$') { throw "Invalid address: $addr" }
    }
    if ($p -le 0) { $p = 8765 }
    if ($p -lt 1 -or $p -gt 65535) { throw 'Invalid port' }
    if (-not (Test-SLTokenFormat $tok)) { throw 'That token/pairing code does not look right (copy it exactly from the simulator PC).' }
    $dir = Get-SLConfigDir
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [pscustomobject]@{ Address = $addr; Port = $p } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dir 'controller.json') -Encoding UTF8
    Save-SLControllerToken -Tok $tok
    $script:Conn = $null
    Write-Host "Saved. Testing connection to ${addr}:$p ..."
    $r = Invoke-SLCommand -Cmd 'ping' -P @{}
    Write-Host ("Connected to {0} in {1} ms (agent {2})." -f $r.host, $r.latencyMs, $r.agentVersion) -ForegroundColor Green
    return $r
}

# ------------------------------------------------------------------ bridge (job folder) for Claude

$script:BridgeCommands = @('ping', 'info', 'exec', 'ls', 'stat', 'mkdir', 'delete', 'tail', 'ps', 'start', 'stop', 'push', 'pull', 'screenshot', 'deploy',
    'gsz-status', 'gsz-update', 'gsz-start', 'gsz-stop', 'gsz-log', 'gsz-releases', 'gsz-swap-screens', 'gsz-screens')

function Write-SLJsonFile {
    param([string]$File, $Obj)
    $tmp = "$File.tmp"
    [IO.File]::WriteAllText($tmp, ($Obj | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
    if (Test-Path -LiteralPath $File) { Remove-Item -LiteralPath $File -Force }
    Move-Item -LiteralPath $tmp -Destination $File
}

function ConvertTo-SLHashtable {
    param($Obj)
    $h = @{}
    if ($null -eq $Obj) { return $h }
    foreach ($p in $Obj.PSObject.Properties) { $h[$p.Name] = $p.Value }
    return $h
}

function Invoke-SLWatch {
    param([string]$JobsDir)
    $root = $JobsDir
    if (-not $root) { $root = 'C:\SimLinkBridge' }
    $root = Resolve-SLLocalPath $root
    $inbox = Join-Path $root 'inbox'; $results = Join-Path $root 'results'; $done = Join-Path $root 'done'; $files = Join-Path $root 'files'
    foreach ($d in @($root, $inbox, $results, $done, $files)) { if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null } }
    $logFile = Join-Path $root 'bridge.log'
    function BLog([string]$m) { $l = '{0} {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $m; Add-Content -LiteralPath $logFile -Value $l -Encoding UTF8; Write-Host $l }

    $mutex = New-Object System.Threading.Mutex($false, 'Global\SimLinkBridge')
    if (-not $mutex.WaitOne(0)) { Write-Host 'Bridge already running.'; return }
    BLog "Bridge watching $inbox"
    $lastBeat = [DateTime]::MinValue
    $lastPing = [DateTime]::MinValue
    $simOk = $null; $simErr = $null
    try {
        while ($true) {
            $now = Get-Date
            if (($now - $lastPing).TotalSeconds -ge 30) {
                try { [void](Invoke-SLCommand -Cmd 'ping' -P @{}); $simOk = $true; $simErr = $null } catch { $simOk = $false; $simErr = $_.Exception.Message }
                $lastPing = $now
            }
            if (($now - $lastBeat).TotalSeconds -ge 5) {
                Write-SLJsonFile -File (Join-Path $root 'status.json') -Obj ([pscustomobject]@{ bridgeAlive = $true; time = $now.ToString('o'); simReachable = $simOk; simError = $simErr; lastPing = $lastPing.ToString('o') })
                $lastBeat = $now
            }
            $jobs = @(Get-ChildItem -LiteralPath $inbox -Filter '*.json' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime)
            foreach ($j in $jobs) {
                $base = [IO.Path]::GetFileNameWithoutExtension($j.Name)
                if ($base -notmatch '^[A-Za-z0-9_\-\.]{1,100}$') { BLog "Skipping job with unsafe name: $($j.Name)"; Move-Item -LiteralPath $j.FullName -Destination (Join-Path $done ($j.Name + '.rejected')) -Force; continue }
                $started = Get-Date
                $out = [ordered]@{ job = $base; ok = $false; command = $null; startedAt = $started.ToString('o') }
                try {
                    $spec = Get-Content -LiteralPath $j.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
                    $cmd = [string]$spec.command
                    $out.command = $cmd
                    if ($script:BridgeCommands -notcontains $cmd) { throw "Command '$cmd' not allowed via bridge. Allowed: $($script:BridgeCommands -join ', ')" }
                    $prm = ConvertTo-SLHashtable $spec.params
                    if ($cmd -eq 'screenshot' -and -not $prm.ContainsKey('Local')) { $prm['Local'] = Join-Path $files "$base.png" }
                    if ($cmd -eq 'deploy' -and [int]($prm['ScreenshotAfter']) -gt 0 -and -not $prm.ContainsKey('Local_Screenshot')) { $prm['Local_Screenshot'] = Join-Path $files "$base.png" }
                    if ($cmd -eq 'gsz-update' -and [int]($prm['ScreenshotAfter']) -gt 0 -and -not $prm.ContainsKey('Local')) { $prm['Local'] = Join-Path $files "$base.png" }
                    if ($cmd -eq 'pull' -and -not $prm.ContainsKey('Local')) { $prm['Local'] = Join-Path $files ("$base-" + (Split-Path -Leaf ([string]$prm['Remote']))) }
                    BLog "Job $base : $cmd"
                    $res = Invoke-SLCommand -Cmd $cmd -P $prm
                    $out.ok = $true
                    $out['result'] = $res
                } catch {
                    $out['error'] = $_.Exception.Message
                    BLog "Job $base failed: $($_.Exception.Message)"
                }
                $out['finishedAt'] = (Get-Date).ToString('o')
                Write-SLJsonFile -File (Join-Path $results "$base.result.json") -Obj ([pscustomobject]$out)
                Move-Item -LiteralPath $j.FullName -Destination (Join-Path $done $j.Name) -Force
            }
            # housekeeping: keep done/ and results/ from growing forever
            if ($now.Minute -eq 0 -and $now.Second -lt 2) {
                Get-ChildItem -LiteralPath $done, $results, $files -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.LastWriteTime -lt $now.AddDays(-7) } | Remove-Item -Force -ErrorAction SilentlyContinue
            }
            Start-Sleep -Milliseconds 700
        }
    } finally { $mutex.ReleaseMutex() }
}

