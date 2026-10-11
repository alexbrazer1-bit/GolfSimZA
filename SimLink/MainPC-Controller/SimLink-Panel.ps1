# SimLink-Panel.ps1 - SimLink Control Panel (Windows Forms) for the MAIN PC.
# Every SimLink command as buttons: GolfSimZA status/update/start/stop/log, live screenshots, file manager,
# processes, remote PowerShell, logs, pairing, token rotation, bridge control and the self-test.
# Started by SimLink.exe (or: powershell -STA -File SimLink-Panel.ps1). Windows PowerShell 5.1.

param([string]$ConfigDir)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic
[System.Windows.Forms.Application]::EnableVisualStyles()

$script:AppDir = $PSScriptRoot
$script:CorePath = Join-Path $PSScriptRoot 'SimLink.Core.ps1'
$script:SLConfigDir = $ConfigDir
. $script:CorePath

# ======================================================================= settings

$script:PanelSettingsFile = Join-Path (Get-SLConfigDir) 'panel.json'
$script:Settings = [ordered]@{
    GolfSimZAProject   = 'C:\Shared\Unity Programs\GolfSimZA'
    UpdateFolder       = $script:GSZDefaults.UpdateFolder
    RemoteUpdateFolder = $script:GSZDefaults.RemoteUpdateFolder
    DevBuildRemote     = 'C:\Games\GolfSimZA-Dev'
    AutoPingSeconds    = 15
}
if (Test-Path -LiteralPath $script:PanelSettingsFile) {
    try {
        $saved = Get-Content -LiteralPath $script:PanelSettingsFile -Raw | ConvertFrom-Json
        foreach ($k in @($script:Settings.Keys)) {
            $p = $saved.PSObject.Properties[$k]
            if ($null -ne $p -and $null -ne $p.Value -and [string]$p.Value -ne '') { $script:Settings[$k] = $p.Value }
        }
    } catch { }
}
function Save-PanelSettings {
    $d = Split-Path -Parent $script:PanelSettingsFile
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    [IO.File]::WriteAllText($script:PanelSettingsFile, ([pscustomobject]$script:Settings | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
}

function Get-BridgeDir {
    try {
        $k = Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SimLinkMainPC' -ErrorAction Stop
        if ($k.PSObject.Properties['BridgeDir'] -and $k.BridgeDir) { return [string]$k.BridgeDir }
    } catch { }
    return 'C:\SimLinkBridge'
}
function Test-Paired {
    $d = Get-SLConfigDir
    return ((Test-Path -LiteralPath (Join-Path $d 'controller.json')) -and (Test-Path -LiteralPath (Join-Path $d 'token.dat')))
}

# ======================================================================= background worker

$script:RS = [runspacefactory]::CreateRunspace()
$script:RS.ApartmentState = 'STA'
$script:RS.Open()
$init = [powershell]::Create()
$init.Runspace = $script:RS
[void]$init.AddScript('param($cd, $core) $ErrorActionPreference = "Stop"; $global:SLConfigDir = $cd; . $core').AddParameter('cd', $ConfigDir).AddParameter('core', $script:CorePath)
[void]$init.Invoke()
$init.Dispose()

$script:Op = $null
$script:Queue = New-Object System.Collections.ArrayList
$script:LogBox = $null
$script:BusyLabel = $null
$script:Progress = $null
$script:TempDir = Join-Path ([IO.Path]::GetTempPath()) 'SimLinkPanel'
if (-not (Test-Path -LiteralPath $script:TempDir)) { New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null }

function Write-Log {
    param([string]$Text)
    if (-not $script:LogBox) { return }
    $line = '{0}  {1}' -f (Get-Date -Format 'HH:mm:ss'), $Text
    $script:LogBox.AppendText($line + [Environment]::NewLine)
}

function Get-InnerMessage {
    param($Err)
    $e = $Err.Exception
    while ($null -ne $e.InnerException) { $e = $e.InnerException }
    return $e.Message
}

function Start-Op {
    # Runs $Script (a string with a param() block) in the background runspace.
    param([string]$Title, [string]$Script, [hashtable]$Params, [scriptblock]$OnDone, [scriptblock]$OnError, [switch]$Quiet)
    if ($null -ne $script:Op) {
        if ($Quiet) { return $false }   # background refreshes are simply skipped while busy
        [void]$script:Queue.Add(@{ Title = $Title; Script = $Script; Params = $Params; OnDone = $OnDone; OnError = $OnError })
        if (-not $script:Op.Quiet) { Write-Log "Queued '$Title' (waiting for '$($script:Op.Title)')" }
        return $true
    }
    $ps = [powershell]::Create()
    $ps.Runspace = $script:RS
    [void]$ps.AddScript($Script)
    if ($Params) { [void]$ps.AddParameters($Params) }
    $handle = $ps.BeginInvoke()
    $script:Op = @{ PS = $ps; Handle = $handle; Title = $Title; OnDone = $OnDone; OnError = $OnError; Quiet = [bool]$Quiet; Started = (Get-Date); InfoSeen = 0 }
    if (-not $Quiet) {
        Write-Log "> $Title"
        $script:BusyLabel.Text = "Working: $Title ..."
        $script:Progress.Style = 'Marquee'
        $script:Progress.Visible = $true
    }
    return $true
}

function Start-Cmd {
    # Shortcut: run one SimLink command (same names as the command line) in the background.
    param([string]$Title, [string]$Cmd, [hashtable]$P = @{}, [scriptblock]$OnDone, [switch]$Quiet)
    return (Start-Op -Title $Title -Script 'param($Cmd, $P) Invoke-SLCommand -Cmd $Cmd -P $P' -Params @{ Cmd = $Cmd; P = $P } -OnDone $OnDone -Quiet:$Quiet)
}

function Update-Op {
    if ($null -eq $script:Op) { return }
    $op = $script:Op
    $ps = $op.PS
    if (-not $op.Quiet) {
        $inf = $ps.Streams.Information
        while ($op.InfoSeen -lt $inf.Count) {
            Write-Log ('   ' + [string]$inf[$op.InfoSeen].MessageData)
            $op.InfoSeen++
        }
        $pc = $ps.Streams.Progress.Count
        if ($pc -gt 0) {
            $pr = $ps.Streams.Progress[$pc - 1]
            if ($pr.RecordType -eq 'Completed' -or $pr.PercentComplete -lt 0) { $script:Progress.Style = 'Marquee' }
            else {
                $script:Progress.Style = 'Continuous'
                $script:Progress.Value = [Math]::Max(0, [Math]::Min(100, $pr.PercentComplete))
                $script:BusyLabel.Text = "Working: $($op.Title) - $($pr.Activity) $($pr.StatusDescription)"
            }
        }
    }
    if (-not $op.Handle.IsCompleted) { return }
    $script:Op = $null
    $secs = [int]((Get-Date) - $op.Started).TotalSeconds
    try {
        $out = $ps.EndInvoke($op.Handle)
        if (-not $op.Quiet) {
            $inf = $ps.Streams.Information
            while ($op.InfoSeen -lt $inf.Count) { Write-Log ('   ' + [string]$inf[$op.InfoSeen].MessageData); $op.InfoSeen++ }
        }
        $res = $null
        if ($out.Count -gt 0) { $res = $out[$out.Count - 1] }
        if (-not $op.Quiet) { Write-Log "  done: $($op.Title) ($secs s)" }
        if ($op.OnDone) { & $op.OnDone $res }
    } catch {
        $msg = Get-InnerMessage $_
        if ($op.OnError) { & $op.OnError $msg }
        if (-not $op.Quiet) {
            Write-Log "  FAILED: $($op.Title): $msg"
            [void][System.Windows.Forms.MessageBox]::Show($script:Form, $msg, "SimLink - $($op.Title)", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
        }
    } finally {
        $ps.Dispose()
        if (-not $op.Quiet) {
            $script:Progress.Visible = $false
            $script:BusyLabel.Text = ''
        }
    }
    if ($script:Queue.Count -gt 0) {
        $n = $script:Queue[0]
        $script:Queue.RemoveAt(0)
        [void](Start-Op -Title $n.Title -Script $n.Script -Params $n.Params -OnDone $n.OnDone -OnError $n.OnError)
    }
}

# ======================================================================= UI helpers

$script:FontUI = New-Object System.Drawing.Font('Segoe UI', 9)
$script:FontMono = New-Object System.Drawing.Font('Consolas', 9)
$script:FontBig = New-Object System.Drawing.Font('Segoe UI Semibold', 12)

function New-Btn {
    param([string]$Text, [int]$X, [int]$Y, [int]$W = 150, [int]$H = 30, [scriptblock]$OnClick)
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $Text; $b.Location = New-Object System.Drawing.Point($X, $Y); $b.Size = New-Object System.Drawing.Size($W, $H)
    if ($OnClick) { $b.Add_Click($OnClick) }
    return $b
}
function New-Lbl {
    param([string]$Text, [int]$X, [int]$Y, [int]$W = 200, [int]$H = 20)
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $Text; $l.Location = New-Object System.Drawing.Point($X, $Y); $l.Size = New-Object System.Drawing.Size($W, $H)
    $l.AutoEllipsis = $true
    return $l
}
function New-Txt {
    param([string]$Text, [int]$X, [int]$Y, [int]$W = 300, [int]$H = 23, [switch]$Multi, [switch]$ReadOnly)
    $t = New-Object System.Windows.Forms.TextBox
    $t.Text = $Text; $t.Location = New-Object System.Drawing.Point($X, $Y); $t.Size = New-Object System.Drawing.Size($W, $H)
    if ($Multi) { $t.Multiline = $true; $t.ScrollBars = 'Both'; $t.WordWrap = $false; $t.Font = $script:FontMono }
    if ($ReadOnly) { $t.ReadOnly = $true; $t.BackColor = [System.Drawing.SystemColors]::Window }
    return $t
}
function New-Grp {
    param([string]$Text, [int]$X, [int]$Y, [int]$W, [int]$H)
    $g = New-Object System.Windows.Forms.GroupBox
    $g.Text = $Text; $g.Location = New-Object System.Drawing.Point($X, $Y); $g.Size = New-Object System.Drawing.Size($W, $H)
    return $g
}
function New-Chk {
    param([string]$Text, [int]$X, [int]$Y, [int]$W = 250, [bool]$Checked = $false)
    $c = New-Object System.Windows.Forms.CheckBox
    $c.Text = $Text; $c.Location = New-Object System.Drawing.Point($X, $Y); $c.Size = New-Object System.Drawing.Size($W, 22); $c.Checked = $Checked
    return $c
}
function New-Num {
    param([int]$Value, [int]$Min, [int]$Max, [int]$X, [int]$Y, [int]$W = 70)
    $n = New-Object System.Windows.Forms.NumericUpDown
    $n.Minimum = $Min; $n.Maximum = $Max; $n.Value = $Value
    $n.Location = New-Object System.Drawing.Point($X, $Y); $n.Size = New-Object System.Drawing.Size($W, 23)
    return $n
}
function New-LV {
    param([string[]]$Columns, [int[]]$Widths)
    $lv = New-Object System.Windows.Forms.ListView
    $lv.View = 'Details'; $lv.FullRowSelect = $true; $lv.GridLines = $true; $lv.HideSelection = $false
    for ($i = 0; $i -lt $Columns.Count; $i++) { [void]$lv.Columns.Add($Columns[$i], $Widths[$i]) }
    return $lv
}
function Confirm-Action {
    param([string]$Text)
    return ([System.Windows.Forms.MessageBox]::Show($script:Form, $Text, 'SimLink', [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question) -eq [System.Windows.Forms.DialogResult]::Yes)
}
function Format-Size {
    param($Bytes)
    if ($null -eq $Bytes) { return '' }
    $b = [double]$Bytes
    if ($b -ge 1GB) { return ('{0:N2} GB' -f ($b / 1GB)) }
    if ($b -ge 1MB) { return ('{0:N1} MB' -f ($b / 1MB)) }
    if ($b -ge 1KB) { return ('{0:N0} KB' -f ($b / 1KB)) }
    return ('{0} B' -f $b)
}
function Set-Text {
    param($Box, [string]$Text)
    $Box.Text = ($Text -replace "(?<!`r)`n", "`r`n")
    $Box.SelectionStart = $Box.TextLength
    $Box.ScrollToCaret()
}

# ======================================================================= main form

$script:Form = New-Object System.Windows.Forms.Form
$script:Form.Text = "SimLink Control Panel  $($script:SLControllerVersion)"
$script:Form.Size = New-Object System.Drawing.Size(1180, 910)
$script:Form.MinimumSize = New-Object System.Drawing.Size(1000, 760)
$script:Form.StartPosition = 'CenterScreen'
$script:Form.Font = $script:FontUI
$icoPath = Join-Path $script:AppDir 'simlink.ico'
if (Test-Path -LiteralPath $icoPath) { try { $script:Form.Icon = New-Object System.Drawing.Icon($icoPath) } catch { } }

# --- top status bar
$top = New-Object System.Windows.Forms.Panel
$top.Dock = 'Top'; $top.Height = 62; $top.BackColor = [System.Drawing.Color]::FromArgb(24, 52, 40)
$script:StatusDot = New-Lbl ([string][char]0x25CF) 12 12 26 34
$script:StatusDot.Font = New-Object System.Drawing.Font('Segoe UI', 18)
$script:StatusDot.ForeColor = [System.Drawing.Color]::Gray
$script:StatusMain = New-Lbl 'Connecting to the simulator PC ...' 44 8 700 26
$script:StatusMain.Font = $script:FontBig; $script:StatusMain.ForeColor = [System.Drawing.Color]::White
$script:StatusSub = New-Lbl '' 46 36 760 20
$script:StatusSub.ForeColor = [System.Drawing.Color]::FromArgb(190, 220, 200)
$top.Width = 1164
$btnRefreshAll = New-Btn 'Refresh' 934 14 100 32 { Invoke-RefreshAll }
$btnSelfTest = New-Btn 'Run self-test' 1042 14 110 32 { Invoke-SelfTest }
foreach ($hb in @($btnRefreshAll, $btnSelfTest)) {
    $hb.Anchor = 'Top,Right'
    $hb.BackColor = [System.Drawing.SystemColors]::Control
    $hb.ForeColor = [System.Drawing.SystemColors]::ControlText
    $hb.UseVisualStyleBackColor = $true
}
$top.Controls.AddRange(@($script:StatusDot, $script:StatusMain, $script:StatusSub, $btnRefreshAll, $btnSelfTest))

# --- bottom activity log
$bottom = New-Object System.Windows.Forms.Panel
$bottom.Dock = 'Bottom'; $bottom.Height = 165
$script:BusyLabel = New-Lbl '' 6 2 900 18
$script:BusyLabel.Dock = 'Top'
$script:Progress = New-Object System.Windows.Forms.ProgressBar
$script:Progress.Dock = 'Top'; $script:Progress.Height = 14; $script:Progress.Visible = $false; $script:Progress.MarqueeAnimationSpeed = 30
$script:LogBox = New-Txt '' 0 0 100 100 -Multi -ReadOnly
$script:LogBox.Dock = 'Fill'; $script:LogBox.WordWrap = $true; $script:LogBox.ScrollBars = 'Vertical'
$bottom.Controls.Add($script:LogBox); $bottom.Controls.Add($script:Progress); $bottom.Controls.Add($script:BusyLabel)

$tabs = New-Object System.Windows.Forms.TabControl
$tabs.Size = New-Object System.Drawing.Size(1164, 640)
$tabs.Dock = 'Fill'
$tabs.Padding = New-Object System.Drawing.Point(14, 5)
# Controls are laid out for a 1156x610 page; anchors are relative to that size, so set it BEFORE adding controls.
function New-Tab([string]$Name) { $t = New-Object System.Windows.Forms.TabPage; $t.Text = $Name; $t.Size = New-Object System.Drawing.Size(1156, 610); $t.Padding = New-Object System.Windows.Forms.Padding(8); [void]$tabs.TabPages.Add($t); return $t }

# ======================================================================= tab: GolfSimZA

$tG = New-Tab 'GolfSimZA'
$gSim = New-Grp 'GolfSimZA on the simulator PC' 10 8 540 200
$script:GVer = New-Lbl 'Installed version: -' 14 26 360
$script:GRun = New-Lbl 'Running: -' 14 50 360
$script:GR10 = New-Lbl 'R10 bridge (gspro-r10): -' 14 74 510
$script:GDir = New-Lbl 'Install folder: -' 14 98 510
$script:GLog = New-Lbl 'Player.log updated: -' 14 122 510
$gSim.Controls.AddRange(@($script:GVer, $script:GRun, $script:GR10, $script:GDir, $script:GLog,
    (New-Btn 'Swap screens' 384 22 144 32 { Invoke-SwapScreens }),
    (New-Btn 'Refresh status' 14 152 120 30 { Invoke-GszStatus }),
    (New-Btn 'Start game' 140 152 95 30 { Start-Cmd 'Start GolfSimZA' 'gsz-start' @{} { param($r) Write-Log "  GolfSimZA running (pid $($r.pid))"; Invoke-GszStatus -Quiet } | Out-Null }),
    (New-Btn 'Stop game' 241 152 95 30 { if (Confirm-Action 'Close GolfSimZA on the simulator PC?') { Start-Cmd 'Stop GolfSimZA' 'gsz-stop' @{} { param($r) Write-Log "  stopped $($r.count) process(es)"; Invoke-GszStatus -Quiet } | Out-Null } }),
    (New-Btn 'Restart game' 342 152 95 30 { Invoke-GszRestart }),
    (New-Btn 'Screenshot' 443 152 85 30 { $tabs.SelectedTab = $tS; Invoke-Screenshot })))

$gUpd = New-Grp 'Published updates (on this PC)' 560 8 580 200
$script:ULatest = New-Lbl 'Latest published: -' 14 24 550
$script:UCombo = New-Object System.Windows.Forms.ComboBox
$script:UCombo.DropDownStyle = 'DropDownList'; $script:UCombo.Location = New-Object System.Drawing.Point(14, 48); $script:UCombo.Size = New-Object System.Drawing.Size(150, 24)
$script:UNotes = New-Txt '' 172 48 392 94 -Multi -ReadOnly
$script:UNotes.WordWrap = $true; $script:UNotes.ScrollBars = 'Vertical'; $script:UNotes.Font = $script:FontUI
$script:UShot = New-Chk 'Screenshot 20 s after installing' 14 80 155 $true
$script:UShot.Height = 40
$script:UCombo.Add_SelectedIndexChanged({ Show-ReleaseNotes })
$gUpd.Controls.AddRange(@($script:ULatest, $script:UCombo, $script:UNotes, $script:UShot,
    (New-Btn 'Reload list' 14 152 100 30 { Import-Releases }),
    (New-Btn 'Install selected version on sim PC' 120 152 230 30 { Invoke-GszUpdate ([string]$script:UCombo.SelectedItem) }),
    (New-Btn 'Install LATEST' 356 152 140 30 { Invoke-GszUpdate '' })))

$gDev = New-Grp 'Deploy a Unity build folder (quick dev test, no installer)' 10 214 1130 92
$script:DLocal = New-Txt (Join-Path $script:Settings.GolfSimZAProject 'Builds\GolfSimZA') 110 24 520
$script:DRemote = New-Txt $script:Settings.DevBuildRemote 110 56 300
$script:DClean = New-Chk 'Clean target first' 420 56 140 $true
$script:DLaunch = New-Chk 'Launch after deploy' 565 56 150 $true
$gDev.Controls.AddRange(@((New-Lbl 'Build folder:' 14 27 95), $script:DLocal,
    (New-Btn 'Browse...' 636 22 80 27 { $d = New-Object System.Windows.Forms.FolderBrowserDialog; $d.SelectedPath = $script:DLocal.Text; if ($d.ShowDialog() -eq 'OK') { $script:DLocal.Text = $d.SelectedPath } }),
    (New-Lbl 'Sim PC folder:' 14 59 95), $script:DRemote, $script:DClean, $script:DLaunch,
    (New-Btn 'Deploy build to sim PC' 760 22 200 60 { Invoke-DevDeploy })))

$gPL = New-Grp 'GolfSimZA Player.log (sim PC)' 10 312 1130 300
$gPL.Anchor = 'Top,Left,Right,Bottom'
$script:PLBox = New-Txt '' 14 56 1100 230 -Multi -ReadOnly
$script:PLBox.Anchor = 'Top,Left,Right,Bottom'
$script:PLLines = New-Num 300 20 5000 290 22 80
$gPL.Controls.AddRange(@((New-Btn 'Load Player.log' 14 20 140 30 { Invoke-PlayerLog }),
    (New-Lbl 'Last lines:' 210 26 80), $script:PLLines, $script:PLBox))
$tG.Controls.AddRange(@($gSim, $gUpd, $gDev, $gPL))

function Invoke-GszStatus {
    param([switch]$Quiet)
    [void](Start-Cmd 'GolfSimZA status' 'gsz-status' @{} -Quiet:$Quiet -OnDone { param($r) Show-GszStatus $r })
}
function Show-GszStatus {
    param($r)
    if ($r.installed) { $script:GVer.Text = "Installed version: $($r.version)" } else { $script:GVer.Text = 'Installed version: NOT INSTALLED' }
    if ($r.running) { $script:GRun.Text = "Running: YES (pid $($r.pid), since $(([datetime]$r.startedAt).ToString('HH:mm:ss')))"; $script:GRun.ForeColor = [System.Drawing.Color]::DarkGreen }
    else { $script:GRun.Text = 'Running: no'; $script:GRun.ForeColor = [System.Drawing.Color]::DarkRed }
    if ($r.r10BridgeRunning) { $script:GR10.Text = 'R10 bridge (gspro-r10): running' } else { $script:GR10.Text = 'R10 bridge (gspro-r10): not running' }
    $script:GDir.Text = "Install folder: $($r.installDir)"
    if ($r.playerLogUpdated) { $script:GLog.Text = "Player.log updated: $(([datetime]$r.playerLogUpdated).ToString('yyyy-MM-dd HH:mm:ss'))" } else { $script:GLog.Text = 'Player.log: none yet' }
    $script:LastGszStatus = $r
    Update-ReleaseHighlight
}

function Invoke-SwapScreens {
    if (-not (Confirm-Action "Swap the game and the swing-data screen on the simulator PC?`r`n`r`nThe other monitor becomes the Windows main display and GolfSimZA restarts (about 10-20 s).")) { return }
    [void](Start-Cmd 'Swap screens' 'gsz-swap-screens' @{} -OnDone {
        param($r)
        Write-Log "  before: $($r.before)"
        Write-Log "  after:  $($r.after)"
        Write-Log "  game restarted by: $($r.gameRestartedBy)"
        Invoke-GszStatus -Quiet
    })
}

function Invoke-GszRestart {
    [void](Start-Op 'Restart GolfSimZA' 'Invoke-SLCommand -Cmd gsz-stop -P @{} | Out-Null; Start-Sleep -Seconds 3; Invoke-SLCommand -Cmd gsz-start -P @{}' -OnDone { param($r) Write-Log "  GolfSimZA restarted (pid $($r.pid))"; Invoke-GszStatus -Quiet })
}

$script:Releases = $null
$script:LastGszStatus = $null
function Import-Releases {
    try {
        $script:Releases = Get-GSZReleases -UpdateFolder $script:Settings.UpdateFolder
        $script:UCombo.Items.Clear()
        foreach ($r in $script:Releases.releases) { [void]$script:UCombo.Items.Add($r.version) }
        if ($script:UCombo.Items.Count -gt 0) { $script:UCombo.SelectedIndex = 0 }
        Update-ReleaseHighlight
    } catch {
        $script:ULatest.Text = "No published updates found in $($script:Settings.UpdateFolder)"
    }
}
function Update-ReleaseHighlight {
    if (-not $script:Releases) { return }
    $t = "Latest published: $($script:Releases.latestVersion)"
    if ($script:LastGszStatus -and $script:LastGszStatus.installed) {
        if ($script:LastGszStatus.version -eq $script:Releases.latestVersion) { $t += '   (sim PC is up to date)'; $script:ULatest.ForeColor = [System.Drawing.Color]::DarkGreen }
        else { $t += "   (sim PC has $($script:LastGszStatus.version) - update available)"; $script:ULatest.ForeColor = [System.Drawing.Color]::DarkOrange }
    }
    $script:ULatest.Text = $t
}
function Show-ReleaseNotes {
    if (-not $script:Releases) { return }
    $v = [string]$script:UCombo.SelectedItem
    $r = $script:Releases.releases | Where-Object { $_.version -eq $v } | Select-Object -First 1
    if ($r) {
        $state = 'installer present'
        if (-not $r.exists) { $state = 'INSTALLER FILE MISSING' }
        $script:UNotes.Text = ("{0}  ({1})`r`n{2}" -f $r.publishedUtc, $state, $r.notes)
        $script:UNotes.SelectionStart = 0; $script:UNotes.ScrollToCaret()
    }
}
function Invoke-GszUpdate {
    param([string]$Version)
    $label = $Version
    if (-not $label) { $label = 'the latest version' }
    if (-not (Confirm-Action "Install GolfSimZA $label on the simulator PC?`r`n`r`nThe game will be closed, updated silently and started again.")) { return }
    $p = @{ UpdateFolder = $script:Settings.UpdateFolder; RemoteFolder = $script:Settings.RemoteUpdateFolder }
    if ($Version) { $p['Version'] = $Version }
    if ($script:UShot.Checked) { $p['ScreenshotAfter'] = 20; $p['Local'] = (Join-Path $script:TempDir 'after-update.png') }
    [void](Start-Cmd "Install GolfSimZA $label" 'gsz-update' $p -OnDone {
        param($r)
        Write-Log "  GolfSimZA $($r.previousVersion) -> $($r.version) installed (running: $($r.running))"
        if ($r.PSObject.Properties['screenshot'] -and $r.screenshot) { Show-Image $r.screenshot.local; $tabs.SelectedTab = $tS }
        Invoke-GszStatus -Quiet
    })
}
function Invoke-DevDeploy {
    $local = $script:DLocal.Text.Trim(); $remote = $script:DRemote.Text.Trim()
    if (-not (Test-Path -LiteralPath (Join-Path $local 'GolfSimZA.exe'))) { [void][System.Windows.Forms.MessageBox]::Show($script:Form, "GolfSimZA.exe not found in`r`n$local", 'SimLink', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning); return }
    if (-not (Confirm-Action "Copy`r`n$local`r`nto the sim PC folder`r`n$remote ?")) { return }
    $script:Settings.DevBuildRemote = $remote; Save-PanelSettings
    $p = @{ Local = $local; Remote = $remote; StopName = 'GolfSimZA,gspro-r10'; Clean = $script:DClean.Checked }
    if ($script:DLaunch.Checked) { $p['Launch'] = 'GolfSimZA.exe' }
    [void](Start-Cmd 'Deploy dev build' 'deploy' $p -OnDone { param($r) Write-Log "  deployed $($r.steps.push.files) files to $($r.steps.push.remote)" })
}
function Invoke-PlayerLog {
    [void](Start-Cmd 'Load Player.log' 'gsz-log' @{ Lines = [int]$script:PLLines.Value } -OnDone { param($r) Set-Text $script:PLBox $r.text; $gPL.Text = "GolfSimZA Player.log (sim PC)  -  $($r.path)" })
}

# ======================================================================= tab: Screen

$tS = New-Tab 'Screen'
$script:Pic = New-Object System.Windows.Forms.PictureBox
$script:Pic.SizeMode = 'Zoom'; $script:Pic.BackColor = [System.Drawing.Color]::Black
$script:Pic.Location = New-Object System.Drawing.Point(10, 50); $script:Pic.Size = New-Object System.Drawing.Size(1120, 560)
$script:Pic.Anchor = 'Top,Left,Right,Bottom'
$script:AutoShot = New-Chk 'Auto refresh every' 330 14 130
$script:AutoSec = New-Num 10 3 300 462 13 60
$script:ShotInfo = New-Lbl '' 600 17 520
$script:LastShot = $null
$tS.Controls.AddRange(@((New-Btn 'Take screenshot' 10 10 150 30 { Invoke-Screenshot }),
    (New-Btn 'Save as...' 168 10 150 30 { Save-Shot }), $script:AutoShot, $script:AutoSec, (New-Lbl 's' 525 17 20), $script:ShotInfo, $script:Pic))
$script:ShotTimer = New-Object System.Windows.Forms.Timer
$script:ShotTimer.Interval = 10000
$script:ShotTimer.Add_Tick({ if ($null -eq $script:Op) { Invoke-Screenshot -Quiet } })
$script:AutoShot.Add_CheckedChanged({ $script:ShotTimer.Interval = [int]$script:AutoSec.Value * 1000; $script:ShotTimer.Enabled = $script:AutoShot.Checked })
$script:AutoSec.Add_ValueChanged({ $script:ShotTimer.Interval = [int]$script:AutoSec.Value * 1000 })

function Show-Image {
    param([string]$File)
    if (-not $File -or -not (Test-Path -LiteralPath $File)) { return }
    $bytes = [IO.File]::ReadAllBytes($File)
    $ms = New-Object IO.MemoryStream(, $bytes)
    $img = [System.Drawing.Image]::FromStream($ms)
    $old = $script:Pic.Image
    $script:Pic.Image = $img
    if ($old) { $old.Dispose() }
    $script:LastShot = $File
    $script:ShotInfo.Text = "{0}x{1}  taken {2}" -f $img.Width, $img.Height, (Get-Date -Format 'HH:mm:ss')
}
function Invoke-Screenshot {
    param([switch]$Quiet)
    $file = Join-Path $script:TempDir ('shot-{0}.png' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Get-ChildItem -LiteralPath $script:TempDir -Filter 'shot-*.png' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -Skip 5 | Remove-Item -Force -ErrorAction SilentlyContinue
    [void](Start-Cmd 'Screenshot' 'screenshot' @{ Local = $file } -Quiet:$Quiet -OnDone { param($r) Show-Image $r.local })
}
function Save-Shot {
    if (-not $script:LastShot) { return }
    $d = New-Object System.Windows.Forms.SaveFileDialog
    $d.Filter = 'PNG image|*.png'; $d.FileName = [IO.Path]::GetFileName($script:LastShot)
    if ($d.ShowDialog() -eq 'OK') { Copy-Item -LiteralPath $script:LastShot -Destination $d.FileName -Force; Write-Log "Saved $($d.FileName)" }
}

# ======================================================================= tab: Files

$tF = New-Tab 'Files'
$script:FPath = New-Object System.Windows.Forms.ComboBox
$script:FPath.Location = New-Object System.Drawing.Point(10, 12); $script:FPath.Size = New-Object System.Drawing.Size(640, 24)
$script:FPath.Text = 'C:\Shared'
$script:FPath.Add_KeyDown({ if ($_.KeyCode -eq 'Enter') { Invoke-List } })
$script:FPath.Add_SelectedIndexChanged({ Invoke-List })
$script:FList = New-LV @('Name', 'Type', 'Size', 'Modified') @(520, 80, 110, 170)
$script:FList.Location = New-Object System.Drawing.Point(10, 46); $script:FList.Size = New-Object System.Drawing.Size(900, 560)
$script:FList.Anchor = 'Top,Left,Right,Bottom'
$script:FList.Add_DoubleClick({
    $s = $script:FList.SelectedItems
    if ($s.Count -eq 1 -and $s[0].SubItems[1].Text -eq 'dir') { $script:FPath.Text = Join-SLRemotePath $script:FPath.Text $s[0].Text; Invoke-List }
})
$script:FOverwrite = New-Chk 'Overwrite existing files' 0 0 200 $false
$fBtns = New-Object System.Windows.Forms.FlowLayoutPanel
$fBtns.FlowDirection = 'TopDown'; $fBtns.WrapContents = $false
$fBtns.Location = New-Object System.Drawing.Point(920, 46); $fBtns.Size = New-Object System.Drawing.Size(215, 400); $fBtns.Anchor = 'Top,Right'
foreach ($b in @(
        (New-Btn 'Upload file(s)...' 0 0 200 32 { Invoke-UploadFiles }),
        (New-Btn 'Upload folder...' 0 0 200 32 { Invoke-UploadFolder }),
        (New-Btn 'Download selected...' 0 0 200 32 { Invoke-Download }),
        (New-Btn 'New folder...' 0 0 200 32 { Invoke-NewFolder }),
        (New-Btn 'Delete selected' 0 0 200 32 { Invoke-DeleteSelected }),
        (New-Btn 'Show file hash (SHA-256)' 0 0 200 32 { Invoke-HashSelected }),
        (New-Btn 'Copy path' 0 0 200 32 { Copy-SelectedPath }))) { $fBtns.Controls.Add($b) }
$fBtns.Controls.Add($script:FOverwrite)
$tF.Controls.AddRange(@($script:FPath, (New-Btn 'Go' 656 10 50 28 { Invoke-List }), (New-Btn 'Up' 710 10 50 28 { Invoke-Up }),
    (New-Btn 'Refresh' 764 10 80 28 { Invoke-List }), $script:FList, $fBtns))

function Get-SelectedRemote {
    $out = @()
    foreach ($i in $script:FList.SelectedItems) { $out += [pscustomobject]@{ path = (Join-SLRemotePath $script:FPath.Text $i.Text); type = $i.SubItems[1].Text; name = $i.Text } }
    return $out
}
function Invoke-List {
    $path = $script:FPath.Text.Trim()
    [void](Start-Cmd "List $path" 'ls' @{ Remote = $path } -OnDone {
        param($r)
        $script:FList.BeginUpdate(); $script:FList.Items.Clear()
        foreach ($i in (@($r.items) | Sort-Object @{ Expression = { $_.type -ne 'dir' } }, name)) {
            $it = New-Object System.Windows.Forms.ListViewItem($i.name)
            [void]$it.SubItems.Add($i.type); [void]$it.SubItems.Add((Format-Size $i.size)); [void]$it.SubItems.Add(([datetime]$i.modified).ToString('yyyy-MM-dd HH:mm'))
            if ($i.type -eq 'dir') { $it.ForeColor = [System.Drawing.Color]::DarkBlue }
            [void]$script:FList.Items.Add($it)
        }
        $script:FList.EndUpdate()
        $script:FPath.Text = $r.path
    })
}
function Invoke-Up {
    $p = $script:FPath.Text.TrimEnd('\', '/')
    $i = $p.LastIndexOfAny([char[]]@('\', '/'))
    if ($i -gt 2) { $script:FPath.Text = $p.Substring(0, $i); Invoke-List }
}
function Invoke-UploadFiles {
    $d = New-Object System.Windows.Forms.OpenFileDialog
    $d.Multiselect = $true; $d.Title = 'Upload to ' + $script:FPath.Text
    if ($d.ShowDialog() -ne 'OK') { return }
    $script = @'
param($Files, $Dir, $Ow)
$n = 0
foreach ($f in $Files) {
    Write-Host "Uploading $f"
    [void](Invoke-SLCommand -Cmd push -P @{ Local = $f; Remote = (Join-SLRemotePath $Dir ([IO.Path]::GetFileName($f))); Overwrite = $Ow })
    $n++
}
$n
'@
    [void](Start-Op "Upload $($d.FileNames.Count) file(s)" $script @{ Files = @($d.FileNames); Dir = $script:FPath.Text; Ow = $script:FOverwrite.Checked } -OnDone { param($r) Write-Log "  uploaded $r file(s)"; Invoke-List })
}
function Invoke-UploadFolder {
    $d = New-Object System.Windows.Forms.FolderBrowserDialog
    $d.Description = 'Folder to upload (it is created inside ' + $script:FPath.Text + ')'
    if ($d.ShowDialog() -ne 'OK') { return }
    $dest = Join-SLRemotePath $script:FPath.Text ([IO.Path]::GetFileName($d.SelectedPath.TrimEnd('\')))
    [void](Start-Cmd "Upload folder $($d.SelectedPath)" 'push' @{ Local = $d.SelectedPath; Remote = $dest } -OnDone { param($r) Write-Log "  $($r.files) files -> $($r.remote)"; Invoke-List })
}
function Invoke-Download {
    $sel = @(Get-SelectedRemote | Where-Object { $_.type -eq 'file' })
    if ($sel.Count -eq 0) { Write-Log 'Select one or more files (not folders) to download.'; return }
    $d = New-Object System.Windows.Forms.FolderBrowserDialog
    $d.Description = 'Save downloaded files to'
    if ($d.ShowDialog() -ne 'OK') { return }
    $script = @'
param($Items, $To)
foreach ($i in $Items) { Write-Host "Downloading $($i.path)"; [void](Invoke-SLCommand -Cmd pull -P @{ Remote = $i.path; Local = (Join-Path $To $i.name); Overwrite = $true }) }
"$($Items.Count) file(s) to $To"
'@
    [void](Start-Op "Download $($sel.Count) file(s)" $script @{ Items = $sel; To = $d.SelectedPath } -OnDone { param($r) Write-Log "  downloaded $r" })
}
function Invoke-NewFolder {
    $n = [Microsoft.VisualBasic.Interaction]::InputBox('New folder name:', 'SimLink', 'New folder')
    if (-not $n) { return }
    [void](Start-Cmd "New folder $n" 'mkdir' @{ Remote = (Join-SLRemotePath $script:FPath.Text $n) } -OnDone { param($r) Invoke-List })
}
function Invoke-DeleteSelected {
    $sel = @(Get-SelectedRemote)
    if ($sel.Count -eq 0) { return }
    if (-not (Confirm-Action ("Permanently delete on the sim PC:`r`n" + (($sel | ForEach-Object { $_.path }) -join "`r`n")))) { return }
    $script = @'
param($Items)
foreach ($i in $Items) { Write-Host "Deleting $($i.path)"; [void](Invoke-SLCommand -Cmd delete -P @{ Remote = $i.path; Recurse = ($i.type -eq 'dir') }) }
$Items.Count
'@
    [void](Start-Op "Delete $($sel.Count) item(s)" $script @{ Items = $sel } -OnDone { param($r) Invoke-List })
}
function Invoke-HashSelected {
    $sel = @(Get-SelectedRemote | Where-Object { $_.type -eq 'file' } | Select-Object -First 1)
    if ($sel.Count -eq 0) { return }
    [void](Start-Cmd "Hash $($sel[0].name)" 'stat' @{ Remote = $sel[0].path } -OnDone { param($r) Write-Log "  SHA-256 $($r.path): $($r.sha256)" })
}
function Copy-SelectedPath {
    $sel = @(Get-SelectedRemote)
    $t = $script:FPath.Text
    if ($sel.Count -gt 0) { $t = $sel[0].path }
    [System.Windows.Forms.Clipboard]::SetText($t); Write-Log "Copied: $t"
}

# ======================================================================= tab: Processes

$tP = New-Tab 'Processes'
$script:PFilter = New-Txt '' 60 13 200
$script:PList = New-LV @('PID', 'Name', 'Memory MB', 'CPU s', 'Window title', 'Path') @(70, 170, 90, 80, 300, 380)
$script:PList.Location = New-Object System.Drawing.Point(10, 46); $script:PList.Size = New-Object System.Drawing.Size(1120, 430)
$script:PList.Anchor = 'Top,Left,Right,Bottom'
$gStart = New-Grp 'Start a program on the sim PC' 10 484 1120 120
$gStart.Anchor = 'Left,Right,Bottom'
$script:SPath = New-Txt 'C:\Games\GolfSimZA\GolfSimZA.exe' 90 24 600
$script:SArgs = New-Txt '' 90 56 600
$script:SWait = New-Chk 'Wait for it to exit (installers)' 700 24 230
$gStart.Controls.AddRange(@((New-Lbl 'Program:' 14 27 70), $script:SPath, (New-Lbl 'Arguments:' 14 59 70), $script:SArgs, $script:SWait,
    (New-Btn 'Start' 700 54 120 30 { Invoke-StartProgram })))
$tP.Controls.AddRange(@((New-Lbl 'Filter:' 10 16 50), $script:PFilter, (New-Btn 'Refresh' 270 10 90 30 { Invoke-Ps }),
    (New-Btn 'Stop selected (force)' 368 10 160 30 { Invoke-StopSelected $true }),
    (New-Btn 'Close selected (gently)' 536 10 170 30 { Invoke-StopSelected $false }), $script:PList, $gStart))
$script:PFilter.Add_KeyDown({ if ($_.KeyCode -eq 'Enter') { Invoke-Ps } })

function Invoke-Ps {
    $p = @{}
    $f = $script:PFilter.Text.Trim()
    if ($f) { $p['Name'] = "*$f*" }
    [void](Start-Cmd 'List processes' 'ps' $p -OnDone {
        param($r)
        $script:PList.BeginUpdate(); $script:PList.Items.Clear()
        foreach ($x in (@($r.processes) | Sort-Object @{ Expression = { -[double]$_.memMB } })) {
            $it = New-Object System.Windows.Forms.ListViewItem([string]$x.id)
            foreach ($v in @($x.name, $x.memMB, $x.cpuSec, $x.title, $x.path)) { [void]$it.SubItems.Add([string]$v) }
            [void]$script:PList.Items.Add($it)
        }
        $script:PList.EndUpdate()
    })
}
function Invoke-StopSelected {
    param([bool]$Force)
    $sel = @($script:PList.SelectedItems)
    if ($sel.Count -eq 0) { return }
    $names = ($sel | ForEach-Object { "$($_.SubItems[1].Text) ($($_.Text))" }) -join ', '
    if (-not (Confirm-Action "Stop on the sim PC: $names ?")) { return }
    $ids = @($sel | ForEach-Object { [int]$_.Text })
    $script = 'param($Ids, $Force) foreach ($i in $Ids) { [void](Invoke-SLCommand -Cmd stop -P @{ Id = $i; Graceful = (-not $Force) }) }; $Ids.Count'
    [void](Start-Op "Stop $($ids.Count) process(es)" $script @{ Ids = $ids; Force = $Force } -OnDone { param($r) Invoke-Ps })
}
function Invoke-StartProgram {
    $p = @{ Path = $script:SPath.Text.Trim(); Wait = $script:SWait.Checked; TimeoutSec = 1800 }
    if ($script:SArgs.Text.Trim()) { $p['Arguments'] = $script:SArgs.Text.Trim() }
    [void](Start-Cmd "Start $($p.Path)" 'start' $p -OnDone { param($r) if ($r.exited) { Write-Log "  exited with code $($r.exitCode)" } else { Write-Log "  started (pid $($r.pid))" } })
}

# ======================================================================= tab: Command

$tC = New-Tab 'Command'
$script:Presets = [ordered]@{
    'Disk space'                    = 'Get-PSDrive -PSProvider FileSystem | Select-Object Name, @{n="UsedGB";e={[math]::Round($_.Used/1GB,1)}}, @{n="FreeGB";e={[math]::Round($_.Free/1GB,1)}} | Format-Table -AutoSize'
    'IP configuration'              = 'Get-NetIPAddress -AddressFamily IPv4 | Where-Object IPAddress -notlike "127.*" | Format-Table InterfaceAlias, IPAddress, PrefixLength -AutoSize'
    'Uptime + Windows version'      = '"Up since: " + (Get-CimInstance Win32_OperatingSystem).LastBootUpTime; (Get-CimInstance Win32_OperatingSystem).Caption + " " + [Environment]::OSVersion.Version'
    'GolfSimZA + R10 processes'     = 'Get-Process GolfSimZA, gspro-r10 -ErrorAction SilentlyContinue | Format-Table Id, ProcessName, StartTime, @{n="MemMB";e={[int]($_.WS/1MB)}} -AutoSize'
    'Top 15 processes by memory'    = 'Get-Process | Sort-Object WS -Descending | Select-Object -First 15 Id, ProcessName, @{n="MemMB";e={[int]($_.WS/1MB)}}, MainWindowTitle | Format-Table -AutoSize'
    'Screen resolution(s)'          = 'Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Screen]::AllScreens | Format-Table DeviceName, Primary, Bounds -AutoSize'
    'Installed GolfSimZA version'   = 'Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*" | Where-Object { $_.PSObject.Properties["DisplayName"] -and $_.DisplayName -like "GolfSimZA*" } | Format-List DisplayName, DisplayVersion, InstallLocation'
    'SimLink agent audit log today' = 'Get-Content ("C:\SimLink\logs\agent-" + (Get-Date -Format yyyyMMdd) + ".log") -Tail 100'
    'Windows Update pending reboot' = 'Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"'
    'Restart the sim PC (in 60 s)'  = 'shutdown.exe /r /t 60 /c "Restart requested from SimLink"'
    'Cancel pending restart'        = 'shutdown.exe /a'
}
$script:CPreset = New-Object System.Windows.Forms.ComboBox
$script:CPreset.DropDownStyle = 'DropDownList'; $script:CPreset.Location = New-Object System.Drawing.Point(80, 12); $script:CPreset.Size = New-Object System.Drawing.Size(300, 24)
foreach ($k in $script:Presets.Keys) { [void]$script:CPreset.Items.Add($k) }
$script:CInput = New-Txt '' 10 46 1120 150 -Multi
$script:CInput.Anchor = 'Top,Left,Right'
$script:CTimeout = New-Num 120 1 3600 560 12 80
$script:COutput = New-Txt '' 10 206 1120 400 -Multi -ReadOnly
$script:COutput.Anchor = 'Top,Left,Right,Bottom'
$tC.Controls.AddRange(@((New-Lbl 'Presets:' 10 15 65), $script:CPreset,
    (New-Btn 'Insert' 388 10 70 28 { if ($script:CPreset.SelectedItem) { $script:CInput.Text = $script:Presets[[string]$script:CPreset.SelectedItem] } }),
    (New-Lbl 'Timeout (s):' 475 15 85), $script:CTimeout,
    (New-Btn 'Run on sim PC  (Ctrl+Enter)' 660 8 220 32 { Invoke-RunCommand }), $script:CInput, $script:COutput))
$script:CInput.Add_KeyDown({ if ($_.Control -and $_.KeyCode -eq 'Enter') { $_.SuppressKeyPress = $true; Invoke-RunCommand } })

function Invoke-RunCommand {
    $cmd = $script:CInput.Text
    if (-not $cmd.Trim()) { return }
    $danger = '(?i)\b(shutdown(\.exe)?|Restart-Computer|Stop-Computer|Remove-Item|rm|rmdir|rd|del|erase|Format-Volume|Clear-Disk|Initialize-Disk|Remove-Partition|Uninstall-\w+)\b'
    if ($cmd -match $danger -and -not (Confirm-Action "This command can restart the sim PC or delete data:`r`n`r`n$cmd`r`n`r`nRun it?")) { return }
    [void](Start-Cmd 'Run command' 'exec' @{ Run = $cmd; TimeoutSec = [int]$script:CTimeout.Value } -OnDone {
        param($r)
        $t = $r.stdout
        if ($r.stderr) { $t += "`r`n--- errors ---`r`n" + $r.stderr }
        $t += "`r`n[exit code $($r.exitCode)$(if ($r.timedOut) { ', TIMED OUT' })$(if ($r.truncated) { ', output truncated' })]"
        Set-Text $script:COutput $t
    })
}

# ======================================================================= tab: Logs

$tL = New-Tab 'Logs'
$script:LPath = New-Txt '' 100 13 640
$script:LLines = New-Num 200 10 5000 820 12 80
$script:LOut = New-Txt '' 10 86 1120 520 -Multi -ReadOnly
$script:LOut.Anchor = 'Top,Left,Right,Bottom'
$tL.Controls.AddRange(@((New-Lbl 'Sim PC file:' 10 16 90), $script:LPath, (New-Lbl 'Lines:' 770 16 50), $script:LLines,
    (New-Btn 'Show' 910 10 90 28 { if ($script:LPath.Text.Trim()) { [void](Start-Cmd 'Tail file' 'tail' @{ Remote = $script:LPath.Text.Trim(); Lines = [int]$script:LLines.Value } -OnDone { param($r) Set-Text $script:LOut $r.text }) } }),
    (New-Btn 'GolfSimZA Player.log' 10 46 170 30 { [void](Start-Cmd 'Player.log' 'gsz-log' @{ Lines = [int]$script:LLines.Value } -OnDone { param($r) $script:LPath.Text = $r.path; Set-Text $script:LOut $r.text }) }),
    (New-Btn 'SimLink audit log (sim PC)' 188 46 200 30 { [void](Start-Cmd 'Agent audit log' 'exec' @{ Run = ('Get-Content ("C:\SimLink\logs\agent-" + (Get-Date -Format yyyyMMdd) + ".log") -Tail ' + [int]$script:LLines.Value) } -OnDone { param($r) Set-Text $script:LOut ($r.stdout + $r.stderr) }) }),
    (New-Btn 'Bridge log (this PC)' 396 46 170 30 { $f = Join-Path (Get-BridgeDir) 'bridge.log'; if (Test-Path -LiteralPath $f) { Set-Text $script:LOut ((Get-Content -LiteralPath $f -Tail ([int]$script:LLines.Value)) -join "`r`n") } else { Set-Text $script:LOut "$f not found" } }),
    (New-Btn 'Last self-test report' 574 46 170 30 { $f = Join-Path (Get-SLConfigDir) 'selftest-report.json'; if (Test-Path -LiteralPath $f) { Set-Text $script:LOut (Get-Content -LiteralPath $f -Raw) } else { Set-Text $script:LOut 'No self-test has been run yet.' } }),
    $script:LOut))

# ======================================================================= tab: Settings

$tX = New-Tab 'Settings'
$gPair = New-Grp 'Pairing with the simulator PC' 10 8 560 200
$script:PairInfo = New-Lbl '' 14 24 530 40
$script:PairCode = New-Txt '' 14 92 530
$script:PairCode.UseSystemPasswordChar = $true
$gPair.Controls.AddRange(@($script:PairInfo, (New-Lbl 'Pairing code from SimLink-SimPC-Setup (SIMLINK1:...):' 14 70 530), $script:PairCode,
    (New-Btn 'Pair / re-pair' 14 124 130 32 { Invoke-Pair }),
    (New-Btn 'Rotate access token' 152 124 160 32 { Invoke-Rotate }),
    (New-Btn 'Sim PC details' 320 124 130 32 { Invoke-Info })))

$gBridge = New-Grp 'Claude bridge (this PC)' 580 8 560 200
$script:BridgeInfo = New-Lbl '' 14 24 530 60
$gBridge.Controls.AddRange(@($script:BridgeInfo,
    (New-Btn 'Start / restart bridge' 14 124 170 32 { Restart-Bridge }),
    (New-Btn 'Open bridge folder' 192 124 150 32 { Start-Process explorer.exe (Get-BridgeDir) })))

$gPaths = New-Grp 'GolfSimZA folders' 10 214 1130 170
$script:XProj = New-Txt $script:Settings.GolfSimZAProject 230 24 600
$script:XUpd = New-Txt $script:Settings.UpdateFolder 230 56 600
$script:XRUpd = New-Txt $script:Settings.RemoteUpdateFolder 230 88 600
$gPaths.Controls.AddRange(@((New-Lbl 'GolfSimZA project (this PC):' 14 27 210), $script:XProj,
    (New-Lbl 'Published updates (this PC):' 14 59 210), $script:XUpd,
    (New-Lbl 'Installer copy folder (sim PC):' 14 91 210), $script:XRUpd,
    (New-Btn 'Save' 230 122 100 32 {
        $script:Settings.GolfSimZAProject = $script:XProj.Text.Trim(); $script:Settings.UpdateFolder = $script:XUpd.Text.Trim(); $script:Settings.RemoteUpdateFolder = $script:XRUpd.Text.Trim()
        Save-PanelSettings; Write-Log 'Settings saved.'; Import-Releases })))

$gAbout = New-Grp 'About' 10 390 1130 110
$script:AboutLbl = New-Lbl '' 14 24 1100 76
$gAbout.Controls.Add($script:AboutLbl)
$tX.Controls.AddRange(@($gPair, $gBridge, $gPaths, $gAbout))

function Update-PairInfo {
    if (Test-Paired) {
        $c = Get-Content -LiteralPath (Join-Path (Get-SLConfigDir) 'controller.json') -Raw | ConvertFrom-Json
        $script:PairInfo.Text = "Paired with $($c.Address):$($c.Port). The access token is stored encrypted for your Windows account."
    } else { $script:PairInfo.Text = 'NOT PAIRED. Paste the pairing code shown by SimLink-SimPC-Setup on the simulator PC.' }
    $script:AboutLbl.Text = "SimLink controller $($script:SLControllerVersion)    App folder: $($script:AppDir)`r`nSettings: $(Get-SLConfigDir)`r`nCommand line: open a terminal and type  simlink help"
}
function Invoke-Pair {
    $code = $script:PairCode.Text.Trim()
    if (-not $code) { return }
    [void](Start-Op 'Pair with sim PC' 'param($T) Invoke-SLSetup -Token $T' @{ T = $code } -OnDone { param($r) $script:PairCode.Text = ''; Update-PairInfo; Write-Log "  paired with $($r.host)"; Invoke-RefreshAll })
}
function Invoke-Rotate {
    if (-not (Confirm-Action 'Create a new random access token and switch both PCs to it now?')) { return }
    [void](Start-Cmd 'Rotate token' 'rotate-token' @{} -OnDone { param($r) Write-Log "  token rotated, verified: $($r.verified)" })
}
function Invoke-Info {
    [void](Start-Cmd 'Sim PC details' 'info' @{} -OnDone {
        param($r)
        $tabs.SelectedTab = $tL
        Set-Text $script:LOut ($r | ConvertTo-Json -Depth 5)
        $script:FPath.Items.Clear(); foreach ($x in $r.allowedRoots) { [void]$script:FPath.Items.Add($x) }
    })
}
function Update-BridgeInfo {
    $f = Join-Path (Get-BridgeDir) 'status.json'
    if (-not (Test-Path -LiteralPath $f)) { $script:BridgeInfo.Text = "Not installed ($f missing)."; return }
    try {
        $s = Get-Content -LiteralPath $f -Raw | ConvertFrom-Json
        $age = [int]((Get-Date) - [datetime]$s.time).TotalSeconds
        if ($age -lt 30) { $state = 'RUNNING' } else { $state = "NOT RUNNING (last heartbeat $age s ago)" }
        $script:BridgeInfo.Text = "Bridge: $state`r`nFolder: $(Get-BridgeDir)`r`nSim reachable from bridge: $($s.simReachable)"
    } catch { $script:BridgeInfo.Text = "Could not read $f" }
}
function Restart-Bridge {
    try {
        try { Stop-ScheduledTask -TaskName 'SimLink Bridge' -ErrorAction Stop } catch { }
        Start-Sleep -Milliseconds 800
        Start-ScheduledTask -TaskName 'SimLink Bridge' -ErrorAction Stop
        Write-Log 'Bridge task (re)started.'
    } catch { Write-Log "Could not restart the bridge task: $($_.Exception.Message)" }
}

# ======================================================================= status / refresh / self-test

function Set-Connected {
    param($R)
    $script:StatusDot.ForeColor = [System.Drawing.Color]::LimeGreen
    $script:StatusMain.Text = "Connected to $($R.host)"
    $c = Get-SLConn
    $script:StatusSub.Text = "$($c.Address):$($c.Port)   latency $($R.latencyMs) ms   agent $($R.agentVersion)   checked $(Get-Date -Format 'HH:mm:ss')"
}
function Set-Disconnected {
    param([string]$Msg)
    $script:StatusDot.ForeColor = [System.Drawing.Color]::OrangeRed
    $script:StatusMain.Text = 'Simulator PC not reachable'
    $script:StatusSub.Text = $Msg
}
function Invoke-Ping {
    param([switch]$Quiet)
    if (-not (Test-Paired)) { $script:StatusDot.ForeColor = [System.Drawing.Color]::Gold; $script:StatusMain.Text = 'Not paired - open Settings'; $script:StatusSub.Text = ''; return }
    [void](Start-Op 'Ping' 'Invoke-SLCommand -Cmd ping -P @{}' -Quiet:$Quiet -OnDone { param($r) Set-Connected $r } -OnError { param($m) Set-Disconnected $m })
}
function Invoke-RefreshAll {
    Update-PairInfo; Update-BridgeInfo; Import-Releases
    if (-not (Test-Paired)) { Invoke-Ping; $tabs.SelectedTab = $tX; return }
    $script = '@{ Ping = (Invoke-SLCommand -Cmd ping -P @{}); Gsz = (Invoke-SLCommand -Cmd gsz-status -P @{}) }'
    [void](Start-Op 'Refresh' $script -OnDone { param($r) Set-Connected $r.Ping; Show-GszStatus $r.Gsz } -OnError { param($m) Set-Disconnected $m })
}
function Invoke-SelfTest {
    $t = Join-Path $script:AppDir 'Test-SimLink.ps1'
    if (-not (Confirm-Action "Run the full SimLink self-test now?`r`n`r`nIt copies test files to C:\Shared on the sim PC (removed afterwards), takes a screenshot and briefly starts/stops a small program there.")) { return }
    $tabs.SelectedTab = $tL
    [void](Start-Op 'Self-test' 'param($T, $CD) & $T -ConfigDir $CD | Out-Null; $LASTEXITCODE' @{ T = $t; CD = [string]$ConfigDir } -OnDone {
        param($r)
        $f = Join-Path (Get-SLConfigDir) 'selftest-report.json'
        if (Test-Path -LiteralPath $f) {
            $rep = Get-Content -LiteralPath $f -Raw | ConvertFrom-Json
            $lines = @("SELF-TEST  $($rep.time)   passed $($rep.passed)   failed $($rep.failed)", '')
            foreach ($x in $rep.tests) { $lines += ('{0}  {1,-40} {2,6} ms  {3}' -f $(if ($x.ok) { 'PASS' } else { 'FAIL' }), $x.test, $x.ms, $x.detail) }
            Set-Text $script:LOut ($lines -join "`r`n")
            if ($rep.failed -eq 0) { Write-Log "  SELF-TEST: all $($rep.passed) tests passed" } else { Write-Log "  SELF-TEST: $($rep.failed) failed - see Logs tab" }
        }
    })
}

# ======================================================================= timers + start

$script:UiTimer = New-Object System.Windows.Forms.Timer
$script:UiTimer.Interval = 150
$script:UiTimer.Add_Tick({ try { Update-Op } catch { Write-Log "UI error: $($_.Exception.Message)" } })
$script:PingTimer = New-Object System.Windows.Forms.Timer
$script:PingTimer.Interval = [Math]::Max(5, [int]$script:Settings.AutoPingSeconds) * 1000
$script:PingTimer.Add_Tick({ Update-BridgeInfo; if ($null -eq $script:Op) { Invoke-Ping -Quiet } })

$script:Form.Controls.Add($tabs)
$script:Form.Controls.Add($bottom)
$script:Form.Controls.Add($top)
$script:Form.Add_Shown({
    $script:UiTimer.Start(); $script:PingTimer.Start()
    Write-Log "SimLink Control Panel $($script:SLControllerVersion) started."
    Invoke-RefreshAll
})
$script:Form.Add_FormClosing({
    $script:UiTimer.Stop(); $script:PingTimer.Stop(); $script:ShotTimer.Stop()
    try { $script:RS.Close() } catch { }
})

if ($env:SIMLINK_PANEL_SMOKETEST) {
    # used by the test suite: build the whole UI, report, close without showing
    $names = @($tabs.TabPages | ForEach-Object { $_.Text })
    Write-Output ("PANEL-OK tabs=" + ($names -join ','))
    $script:RS.Close()
    return
}
[void][System.Windows.Forms.Application]::Run($script:Form)
