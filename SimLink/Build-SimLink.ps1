<#
.SYNOPSIS
  Rebuilds SimLink: SimLink.exe (Control Panel launcher) and both setup programs
  (dist\SimLink-SimPC-Setup.exe, dist\SimLink-MainPC-Setup.exe).

.DESCRIPTION
  Lives in GolfSimZA\SimLink. Run it after changing any SimLink source file:
      .\Build-SimLink.ps1
  - copies Common\SimLink.Common.ps1 into SimPC-Agent and MainPC-Controller (one shared protocol file)
  - compiles Launcher\SimLinkLauncher.cs with the C# compiler that ships with Windows (.NET Framework 4)
  - builds both installers with NSIS (installed automatically with winget the first time if missing)
  - checks every PowerShell file for syntax errors first
  The GolfSimZA installer (Installer\GolfSimZA.iss) bundles dist\SimLink-SimPC-Setup.exe automatically.
#>
[CmdletBinding()]
param([switch]$SkipLauncher)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
function Write-Step([string]$m) { Write-Host "==> $m" -ForegroundColor Cyan }

Write-Step 'Syncing shared protocol file'
$common = Join-Path $root 'Common\SimLink.Common.ps1'
foreach ($d in 'SimPC-Agent', 'MainPC-Controller') { Copy-Item -LiteralPath $common -Destination (Join-Path $root "$d\SimLink.Common.ps1") -Force }
# Screen-swap helper shared with the game (GolfSimZA\Assets\GolfSim\UI\WindowsDisplays.cs)
$wd = Join-Path $root '..\Assets\GolfSim\UI\WindowsDisplays.cs'
if (Test-Path -LiteralPath $wd) { Copy-Item -LiteralPath $wd -Destination (Join-Path $root 'MainPC-Controller\WindowsDisplays.cs') -Force }

Write-Step 'Checking PowerShell syntax'
$bad = 0
foreach ($f in Get-ChildItem -Path (Join-Path $root 'SimPC-Agent'), (Join-Path $root 'MainPC-Controller'), (Join-Path $root 'Common') -Filter *.ps1) {
    $tokens = $null; $errs = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errs)
    if ($errs.Count -gt 0) { $bad++; Write-Host "  $($f.Name): $($errs[0].Message) (line $($errs[0].Extent.StartLineNumber))" -ForegroundColor Red }
}
if ($bad -gt 0) { throw "$bad file(s) have syntax errors." }

if (-not $SkipLauncher) {
    Write-Step 'Compiling SimLink.exe'
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (-not (Test-Path $csc)) { throw 'C# compiler (.NET Framework 4) not found.' }
    $l = Join-Path $root 'Launcher'
    & $csc /nologo /target:winexe /platform:anycpu /reference:System.Windows.Forms.dll "/win32icon:$l\simlink.ico" "/out:$l\SimLink.exe" "$l\SimLinkLauncher.cs"
    if ($LASTEXITCODE -ne 0) { throw 'SimLink.exe failed to compile.' }
    Copy-Item -LiteralPath "$l\SimLink.exe" -Destination (Join-Path $root 'MainPC-Controller\SimLink.exe') -Force
    Copy-Item -LiteralPath "$l\simlink.ico" -Destination (Join-Path $root 'MainPC-Controller\simlink.ico') -Force
}

function Find-MakeNsis {
    foreach ($c in @((Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'), (Join-Path $env:ProgramFiles 'NSIS\makensis.exe'))) { if ($c -and (Test-Path $c)) { return $c } }
    $cmd = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}
$makensis = Find-MakeNsis
if (-not $makensis) {
    Write-Step 'NSIS not found - installing it once with winget'
    & winget install --id NSIS.NSIS -e --silent --accept-package-agreements --accept-source-agreements | Out-Host
    $makensis = Find-MakeNsis
    if (-not $makensis) { throw 'NSIS could not be installed. Install it from https://nsis.sourceforge.io and run again.' }
}

New-Item -ItemType Directory -Path (Join-Path $root 'dist') -Force | Out-Null
Push-Location (Join-Path $root 'installer')
try {
    foreach ($n in 'SimPC-Setup.nsi', 'MainPC-Setup.nsi') {
        Write-Step "Building $n"
        & $makensis /V2 $n
        if ($LASTEXITCODE -ne 0) { throw "makensis failed for $n" }
    }
} finally { Pop-Location }

Get-ChildItem (Join-Path $root 'dist') -Filter *.exe | ForEach-Object {
    Write-Host ("  {0,-30} {1,8:N0} KB  SHA256 {2}" -f $_.Name, ($_.Length / 1KB), (Get-FileHash $_.FullName).Hash.Substring(0, 16)) -ForegroundColor Green
}
Write-Host 'Done. Rebuild the GolfSimZA installer to bundle the new SimLink-SimPC-Setup.exe.'
