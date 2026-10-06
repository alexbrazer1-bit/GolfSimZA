# SimLink.ps1  -  controller for the MAIN PC.
# Talks to SimLink-Agent on the simulator PC. Compatible with Windows PowerShell 5.1 and PowerShell 7+.
#
#   .\SimLink.ps1 setup [-Token SIMLINK1:ip:port:token]          (or -Address/-Port + token; stored encrypted)
#   .\SimLink.ps1 ping | info
#   .\SimLink.ps1 exec -Run "Get-Date" [-TimeoutSec 120] [-WorkDir C:\Shared]
#   .\SimLink.ps1 push -Local .\file.exe -Remote C:\Shared\file.exe [-Overwrite]
#   .\SimLink.ps1 push -Local .\BuildFolder -Remote C:\Games\GolfSimZA [-Clean]   (folders are zipped and unpacked)
#   .\SimLink.ps1 pull -Remote C:\Shared\x.log -Local .\x.log [-Overwrite]
#   .\SimLink.ps1 ls -Remote C:\Shared
#   .\SimLink.ps1 mkdir -Remote C:\Shared\New
#   .\SimLink.ps1 delete -Remote C:\Shared\Old [-Recurse]
#   .\SimLink.ps1 tail -Remote "C:\Users\me\AppData\LocalLow\Co\GolfSimZA\Player.log" [-Lines 200]
#   .\SimLink.ps1 ps [-Name GolfSimZA]
#   .\SimLink.ps1 start -Path C:\Games\GolfSimZA\GolfSimZA.exe [-Arguments "-screen-fullscreen 1"] [-Wait] [-TimeoutSec 600]
#   .\SimLink.ps1 stop -Name GolfSimZA | -Id 1234 [-Graceful]
#   .\SimLink.ps1 screenshot [-Local .\shot.png] [-Format png|jpg]
#   .\SimLink.ps1 deploy -Local "C:\Shared\Unity Programs\GolfSimZA\Build" -Remote C:\Games\GolfSimZA `
#                        -StopName GolfSimZA,gspro-r10 -Launch GolfSimZA.exe [-LaunchArgs ".."] [-Clean] [-ScreenshotAfter 15]
#   .\SimLink.ps1 gsz-status                               GolfSimZA version / running / R10 bridge / Player.log on the sim PC
#   .\SimLink.ps1 gsz-releases                             published versions in C:\Shared\GolfSimZA-Updates
#   .\SimLink.ps1 gsz-update [-Version 1.8.1] [-ScreenshotAfter 20]   copy + silently install a published version
#   .\SimLink.ps1 gsz-start | gsz-stop [-Graceful]
#   .\SimLink.ps1 gsz-log [-Lines 300]
#   .\SimLink.ps1 gsz-screens | gsz-swap-screens               show the sim monitors / swap game and swing-data screens
#   .\SimLink.ps1 selftest                                 run the full automated test against the sim PC
#   .\SimLink.ps1 panel                                    open the SimLink Control Panel
#   .\SimLink.ps1 rotate-token
#   .\SimLink.ps1 watch [-JobsDir C:\SimLinkBridge]       (job-folder bridge so Claude can drive SimLink)
#   .\SimLink.ps1 help

[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Command = 'help',
    [string]$Address,
    [int]$Port = 0,
    [string]$Token,
    [string]$Run,
    [int]$TimeoutSec = 0,
    [string]$WorkDir,
    [string]$Local,
    [string]$Remote,
    [string]$Path,
    [string]$Arguments,
    [string]$Name,
    [int]$Id = 0,
    [int]$Lines = 100,
    [string]$Format = 'png',
    [string]$StopName,
    [string]$Launch,
    [string]$LaunchArgs,
    [int]$ScreenshotAfter = 0,
    [string]$JobsDir,
    [string]$ConfigDir,
    [switch]$Overwrite,
    [switch]$Clean,
    [switch]$Recurse,
    [switch]$Wait,
    [switch]$Graceful,
    [string]$Version,
    [string]$UpdateFolder,
    [string]$RemoteFolder,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$script:SLConfigDir = $ConfigDir
. (Join-Path $PSScriptRoot 'SimLink.Core.ps1')

function Show-SLHelp {
    Get-Content -LiteralPath $PSCommandPath -TotalCount 32 | Select-Object -Skip 2 | Where-Object { $_ -like '#*' } | ForEach-Object { $_ -replace '^#\s?', '' }
}

function Write-SLResult {
    param([string]$Cmd, $R)
    if ($Json) { $R | ConvertTo-Json -Depth 12; return }
    switch ($Cmd) {
        'exec' {
            if ($R.stdout) { Write-Output $R.stdout.TrimEnd() }
            if ($R.stderr) { Write-Host $R.stderr.TrimEnd() -ForegroundColor Red }
            $msg = "exit code: $($R.exitCode)"
            if ($R.timedOut) { $msg += ' (TIMED OUT - process killed)' }
            if ($R.truncated) { $msg += ' (output truncated)' }
            Write-Host $msg -ForegroundColor DarkGray
        }
        { $_ -in 'tail', 'gsz-log' } { Write-Output $R.text }
        'ls' { $R.items | Format-Table name, type, size, modified -AutoSize | Out-String -Width 250 | Write-Output }
        'ps' { $R.processes | Sort-Object name | Format-Table id, name, memMB, cpuSec, title -AutoSize | Out-String -Width 250 | Write-Output }
        'gsz-releases' { $R.releases | Format-Table version, exists, publishedUtc, installer -AutoSize | Out-String -Width 250 | Write-Output }
        default { $R | ConvertTo-Json -Depth 12 }
    }
}

$cmdName = $Command.ToLowerInvariant()
$exitCode = 0
try {
    switch ($cmdName) {
        { $_ -in 'help', '-h', '/?', '--help' } { Show-SLHelp }
        'setup' { $r = Invoke-SLSetup -Address $Address -Port $Port -Token $Token; if ($Json) { $r | ConvertTo-Json } }
        'watch' { Invoke-SLWatch -JobsDir $JobsDir }
        'panel' {
            $panel = Join-Path $PSScriptRoot 'SimLink-Panel.ps1'
            Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-WindowStyle', 'Hidden', '-File', "`"$panel`"") -WindowStyle Hidden
        }
        'selftest' {
            $t = Join-Path $PSScriptRoot 'Test-SimLink.ps1'
            $a = @{}
            if ($ConfigDir) { $a['ConfigDir'] = $ConfigDir }
            & $t @a
            $exitCode = $LASTEXITCODE
        }
        default {
            $p = @{}
            foreach ($k in $PSBoundParameters.Keys) { if ($k -ne 'Command' -and $k -ne 'Json' -and $k -ne 'ConfigDir') { $p[$k] = $PSBoundParameters[$k] } }
            foreach ($sw in 'Overwrite', 'Clean', 'Recurse', 'Wait', 'Graceful') { if ($p.ContainsKey($sw)) { $p[$sw] = [bool]$p[$sw] } }
            $r = Invoke-SLCommand -Cmd $cmdName -P $p
            Write-SLResult -Cmd $cmdName -R $r
            if ($cmdName -eq 'exec' -and $r.exitCode -ne 0) { $exitCode = 1 }
        }
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 2
}
exit $exitCode
