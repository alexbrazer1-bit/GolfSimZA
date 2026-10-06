# Test-SimLink.ps1 - automated end-to-end test of SimLink against the real simulator PC.
# Run on the MAIN PC:   simlink selftest      (or: .\Test-SimLink.ps1 [-ReportPath report.json] [-SkipVisible])
# Everything it creates on the sim PC lives in C:\Shared\SimLinkSelfTest-<time> and is deleted at the end.
# -SkipVisible skips the tests that briefly open a window on the sim PC (start/stop program).

[CmdletBinding()]
param(
    [string]$ConfigDir,
    [string]$ReportPath,
    [string]$RemoteBase = 'C:\Shared',
    [switch]$SkipVisible
)

$ErrorActionPreference = 'Stop'
$script:SLConfigDir = $ConfigDir
. (Join-Path $PSScriptRoot 'SimLink.Core.ps1')

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$sep = '\'
if ($RemoteBase.Contains('/')) { $sep = '/' }
$remoteDir = $RemoteBase.TrimEnd('\', '/') + $sep + "SimLinkSelfTest-$stamp"
$localDir = Join-Path ([IO.Path]::GetTempPath()) "SimLinkSelfTest-$stamp"
New-Item -ItemType Directory -Path $localDir -Force | Out-Null
if (-not $ReportPath) { $ReportPath = Join-Path (Get-SLConfigDir) 'selftest-report.json' }

$results = New-Object System.Collections.ArrayList
$script:failCount = 0

function Test-Step {
    param([string]$Name, [scriptblock]$Body)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $entry = [ordered]@{ test = $Name; ok = $false; ms = 0; detail = $null }
    try {
        $d = & $Body
        $entry.ok = $true
        $entry.detail = [string]$d
        Write-Host ("  PASS  {0,-38} {1}" -f $Name, $d) -ForegroundColor Green
    } catch {
        $entry.detail = $_.Exception.Message
        $script:failCount++
        Write-Host ("  FAIL  {0,-38} {1}" -f $Name, $_.Exception.Message) -ForegroundColor Red
    }
    $entry.ms = $sw.ElapsedMilliseconds
    [void]$results.Add([pscustomobject]$entry)
}
function Assert-True([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw $Msg } }
function J([string]$a, [string]$b) { return (Join-SLRemotePath $a $b) }

Write-Host "SimLink self-test  (controller $($script:SLControllerVersion))" -ForegroundColor Cyan

$info = $null
Test-Step 'connect + ping' {
    $r = Invoke-SLCommand -Cmd 'ping' -P @{}
    Assert-True ([bool]$r.pong) 'no pong'
    "host=$($r.host) latency=$($r.latencyMs)ms agent=$($r.agentVersion)"
}
Test-Step 'info' {
    $script:info = Invoke-SLCommand -Cmd 'info' -P @{}
    Assert-True ($script:info.allowedRoots.Count -gt 0) 'no allowed roots'
    "os=$($script:info.os) ps=$($script:info.psVersion) screens=$(@($script:info.screens).Count) exec=$($script:info.allowExec)"
}
Test-Step 'exec: output + exit code' {
    $r = Invoke-SLCommand -Cmd 'exec' -P @{ Run = 'Write-Output "simlink-ok"; [Console]::Error.WriteLine("to-stderr"); exit 7'; TimeoutSec = 60 }
    Assert-True ($r.stdout -match 'simlink-ok') "stdout was: $($r.stdout)"
    Assert-True ($r.stderr -match 'to-stderr') "stderr was: $($r.stderr)"
    Assert-True ($r.exitCode -eq 7) "exit code was $($r.exitCode)"
    'stdout, stderr and exit code 7 returned'
}
Test-Step 'exec: timeout kills command' {
    $r = Invoke-SLCommand -Cmd 'exec' -P @{ Run = 'Start-Sleep -Seconds 60'; TimeoutSec = 3 }
    Assert-True ([bool]$r.timedOut) 'did not time out'
    'killed after 3 s'
}
Test-Step 'mkdir' {
    $r = Invoke-SLCommand -Cmd 'mkdir' -P @{ Remote = $remoteDir }
    $r.path
}

$big = Join-Path $localDir 'big.bin'
Test-Step 'push 12 MB file (chunked + SHA-256)' {
    $bytes = New-Object byte[] (12MB + 12345)
    (New-Object Random 42).NextBytes($bytes)
    [IO.File]::WriteAllBytes($big, $bytes)
    $r = Invoke-SLCommand -Cmd 'push' -P @{ Local = $big; Remote = (J $remoteDir 'big.bin') }
    $lh = (Get-FileHash -LiteralPath $big -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-True ($r.sha256 -eq $lh) 'hash mismatch'
    "size=$($r.size)"
}
Test-Step 'push refuses overwrite without -Overwrite' {
    $failed = $false
    try { [void](Invoke-SLCommand -Cmd 'push' -P @{ Local = $big; Remote = (J $remoteDir 'big.bin') }) } catch { $failed = $_.Exception.Message -like '*exists*' }
    Assert-True $failed 'overwrite was not refused'
    'refused as expected'
}
Test-Step 'stat with hash' {
    $r = Invoke-SLCommand -Cmd 'stat' -P @{ Remote = (J $remoteDir 'big.bin') }
    Assert-True ($r.sha256 -eq (Get-FileHash -LiteralPath $big -Algorithm SHA256).Hash.ToLowerInvariant()) 'remote hash differs'
    'remote hash matches'
}
Test-Step 'pull file back' {
    $back = Join-Path $localDir 'back.bin'
    [void](Invoke-SLCommand -Cmd 'pull' -P @{ Remote = (J $remoteDir 'big.bin'); Local = $back })
    Assert-True ((Get-FileHash $back).Hash -eq (Get-FileHash $big).Hash) 'downloaded file differs'
    'identical'
}
Test-Step 'push folder (zip + extract)' {
    $src = Join-Path $localDir 'folder'
    New-Item -ItemType Directory -Path (Join-Path $src 'Data\Sub') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $src 'readme.txt') -Value ((1..300 | ForEach-Object { "line $_" }) -join "`r`n")
    Set-Content -LiteralPath (Join-Path $src 'Data\a.txt') -Value 'a'
    Set-Content -LiteralPath (Join-Path $src 'Data\Sub\b.txt') -Value 'b'
    $r = Invoke-SLCommand -Cmd 'push' -P @{ Local = $src; Remote = (J $remoteDir 'folder'); Clean = $true }
    Assert-True ($r.files -eq 3) "extracted $($r.files) files"
    '3 files extracted'
}
Test-Step 'ls' {
    $r = Invoke-SLCommand -Cmd 'ls' -P @{ Remote = (J $remoteDir 'folder') }
    $names = @($r.items | ForEach-Object { $_.name })
    Assert-True (($names -contains 'Data') -and ($names -contains 'readme.txt')) "got: $($names -join ',')"
    "items: $($names -join ', ')"
}
Test-Step 'tail' {
    $r = Invoke-SLCommand -Cmd 'tail' -P @{ Remote = (J (J $remoteDir 'folder') 'readme.txt'); Lines = 2 }
    $l = @($r.text -split "`n")
    Assert-True ($l.Count -eq 2 -and $l[1].Trim() -eq 'line 300') "got: $($r.text)"
    'last 2 lines correct'
}
Test-Step 'deploy (folder, clean, no launch)' {
    $r = Invoke-SLCommand -Cmd 'deploy' -P @{ Local = (Join-Path $localDir 'folder'); Remote = (J $remoteDir 'deploy'); Clean = $true }
    Assert-True ($r.steps.push.files -eq 3) 'deploy push failed'
    'deployed 3 files'
}
Test-Step 'ps' {
    $r = Invoke-SLCommand -Cmd 'ps' -P @{ Name = 'powershell' }
    Assert-True (@($r.processes).Count -gt 0) 'agent process not listed'
    "$(@($r.processes).Count) powershell processes"
}
if (-not $SkipVisible) {
    Test-Step 'start -Wait (exit code)' {
        $r = Invoke-SLCommand -Cmd 'start' -P @{ Path = 'C:\Windows\System32\cmd.exe'; Arguments = '/c exit 5'; Wait = $true; TimeoutSec = 60 }
        Assert-True ($r.exitCode -eq 5) "exit code $($r.exitCode)"
        'cmd exited with 5'
    }
    Test-Step 'start + stop by id' {
        $r = Invoke-SLCommand -Cmd 'start' -P @{ Path = 'C:\Windows\System32\PING.EXE'; Arguments = '-n 120 127.0.0.1' }
        Start-Sleep -Seconds 1
        $s = Invoke-SLCommand -Cmd 'stop' -P @{ Id = [int]$r.pid }
        Assert-True ($s.count -eq 1) 'process not stopped'
        "pid $($r.pid) stopped"
    }
}
Test-Step 'screenshot' {
    $out = Join-Path $localDir 'shot.png'
    $r = Invoke-SLCommand -Cmd 'screenshot' -P @{ Local = $out }
    Assert-True ((Test-Path $out) -and (Get-Item $out).Length -gt 1000) 'no image'
    $b = [IO.File]::ReadAllBytes($out)
    Assert-True ($b[0] -eq 0x89 -and $b[1] -eq 0x50) 'not a PNG'
    "$($r.width)x$($r.height), $([Math]::Round($r.size/1KB)) KB"
}
Test-Step 'GolfSimZA status' {
    $r = Invoke-SLCommand -Cmd 'gsz-status' -P @{}
    "installed=$($r.installed) version=$($r.version) running=$($r.running) r10=$($r.r10BridgeRunning)"
}
Test-Step 'GolfSimZA releases (main PC)' {
    $r = Invoke-SLCommand -Cmd 'gsz-releases' -P @{}
    "latest=$($r.latestVersion) published=$(@($r.releases).Count)"
}
Test-Step 'GolfSimZA Player.log' {
    $r = Invoke-SLCommand -Cmd 'gsz-log' -P @{ Lines = 5 }
    "$($r.path)"
}
Test-Step 'security: path outside allowed folders' {
    $blocked = $false
    try { [void](Invoke-SLCommand -Cmd 'ls' -P @{ Remote = 'C:\Windows' }) } catch { $blocked = $_.Exception.Message -like '*outside the allowed*' }
    Assert-True $blocked 'C:\Windows was not blocked'
    'C:\Windows blocked'
}
Test-Step 'security: ..\ escape' {
    $blocked = $false
    try { [void](Invoke-SLCommand -Cmd 'ls' -P @{ Remote = (J $remoteDir '..\..\Windows') }) } catch { $blocked = $_.Exception.Message -like '*outside the allowed*' }
    Assert-True $blocked 'escape was not blocked'
    'blocked'
}
Test-Step 'security: wrong token rejected' {
    $good = Get-SLConn
    $script:Conn = @{ Address = $good.Address; Port = $good.Port; Keys = (New-SLKeys -Token (New-SLToken)) }
    $rejected = $false
    try { [void](Invoke-SLRemote -Action ping -Timeout 10) } catch { $rejected = $_.Exception.Message -like '*rejected authentication*' }
    $script:Conn = $null
    Assert-True $rejected 'wrong token was accepted!'
    'HTTP 401 as expected (1 of 5 allowed failures used)'
}
Test-Step 'delete test folder' {
    $r = Invoke-SLCommand -Cmd 'delete' -P @{ Remote = $remoteDir; Recurse = $true }
    Assert-True ([bool]$r.deleted) 'not deleted'
    $st = Invoke-SLCommand -Cmd 'stat' -P @{ Remote = $remoteDir }
    Assert-True (-not $st.exists) 'still exists'
    'removed'
}
Test-Step 'bridge heartbeat (main PC)' {
    $status = 'C:\SimLinkBridge\status.json'
    try {
        $k = Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SimLinkMainPC' -ErrorAction Stop
        if ($k.PSObject.Properties['BridgeDir'] -and $k.BridgeDir) { $status = Join-Path $k.BridgeDir 'status.json' }
    } catch { }
    if (-not (Test-Path -LiteralPath $status)) { throw "bridge not installed ($status missing)" }
    $s = Get-Content -LiteralPath $status -Raw | ConvertFrom-Json
    $age = ((Get-Date) - [datetime]$s.time).TotalSeconds
    Assert-True ($age -lt 60) "heartbeat is $([int]$age)s old - bridge not running"
    "alive, simReachable=$($s.simReachable)"
}

Remove-Item -LiteralPath $localDir -Recurse -Force -ErrorAction SilentlyContinue
$report = [pscustomobject]@{
    time = (Get-Date).ToString('o'); controllerVersion = $script:SLControllerVersion
    passed = @($results | Where-Object { $_.ok }).Count; failed = $script:failCount; tests = $results
}
$rd = Split-Path -Parent $ReportPath
if ($rd -and -not (Test-Path -LiteralPath $rd)) { New-Item -ItemType Directory -Path $rd -Force | Out-Null }
[IO.File]::WriteAllText($ReportPath, ($report | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
Write-Host ''
if ($script:failCount -eq 0) { Write-Host "ALL $($report.passed) TESTS PASSED" -ForegroundColor Green }
else { Write-Host "$($script:failCount) TEST(S) FAILED, $($report.passed) passed" -ForegroundColor Red }
Write-Host "Report: $ReportPath"
if ($script:failCount -gt 0) { exit 1 } else { exit 0 }
