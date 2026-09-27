<#
.SYNOPSIS
  Makes the GolfSimZA installer from a Unity build and publishes it to the local update folder.

.DESCRIPTION
  Called by Unity (GolfSimZA > Release > Release Manager > Build + Publish Update), or by hand:
    .\Publish-Update.ps1 -BuildDir ..\Builds\GolfSimZA -Version 1.1.0 -UpdateFolder C:\Shared\GolfSimZA-Updates -Notes "New course importer"

  Result in the update folder:
    latest.json                         list of all published versions (newest first)
    installers\GolfSimZA-Setup-x.y.z.exe

  Inno Setup 6 is installed automatically with winget the first time if it is missing.
#>
param(
    [Parameter(Mandatory = $true)] [string] $BuildDir,
    [Parameter(Mandatory = $true)] [string] $Version,
    [string] $UpdateFolder = 'C:\Shared\GolfSimZA-Updates',
    [string] $Notes = '',
    [string] $NotesFile = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$Version' must look like 1.2.3" }
$BuildDir = (Resolve-Path $BuildDir).Path
if (-not (Test-Path (Join-Path $BuildDir 'GolfSimZA.exe'))) { throw "GolfSimZA.exe not found in $BuildDir. Build the Windows player first." }
if ($NotesFile -and (Test-Path $NotesFile)) { $Notes = (Get-Content -Raw -Encoding UTF8 $NotesFile).Trim() }

function Find-Iscc {
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe' }
    if ($env:LOCALAPPDATA) { $candidates += Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe' }
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    $cmd = Get-Command 'iscc.exe' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    Write-Host 'Inno Setup not found - installing it once with winget...'
    $winget = Get-Command 'winget.exe' -ErrorAction SilentlyContinue
    if (-not $winget) { throw 'Inno Setup 6 is required. Install it from https://jrsoftware.org/isdl.php and run again.' }
    & winget install --id JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements --scope user | Out-Host
    $iscc = Find-Iscc
    if (-not $iscc) { throw 'Inno Setup could not be installed automatically. Install it from https://jrsoftware.org/isdl.php and run again.' }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $scriptDir 'Output'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Creating installer for GolfSimZA $Version ..."
& $iscc "/Q" "/DAppVersion=$Version" "/DSourceDir=$BuildDir" "/DOutputDir=$outDir" (Join-Path $scriptDir 'GolfSimZA.iss') | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }

$setupName = "GolfSimZA-Setup-$Version.exe"
$setup = Join-Path $outDir $setupName
if (-not (Test-Path $setup)) { throw "Installer was not created: $setup" }

$installers = Join-Path $UpdateFolder 'installers'
New-Item -ItemType Directory -Force -Path $installers | Out-Null
$published = Join-Path $installers $setupName
Copy-Item -Force $setup $published
$hash = (Get-FileHash -Algorithm SHA256 -Path $published).Hash.ToLowerInvariant()

$manifestPath = Join-Path $UpdateFolder 'latest.json'
$releases = @()
if (Test-Path $manifestPath) {
    try {
        $existing = Get-Content -Raw -Encoding UTF8 $manifestPath | ConvertFrom-Json
        if ($existing.PSObject.Properties['releases'] -and $existing.releases) { $releases = @($existing.releases | Where-Object { $_.version -ne $Version }) }
    } catch {
        Write-Warning "Existing latest.json could not be read and will be replaced: $_"
    }
}

$entry = [pscustomobject][ordered]@{
    version      = $Version
    installer    = "installers/$setupName"
    sha256       = $hash
    publishedUtc = (Get-Date).ToUniversalTime().ToString('o')
    notes        = $Notes
}
$all = @(@($entry) + $releases | Sort-Object -Property @{ Expression = { [version]$_.version } } -Descending)

$manifest = [ordered]@{
    product       = 'GolfSimZA'
    latestVersion = $all[0].version
    releases      = $all
}

$json = $manifest | ConvertTo-Json -Depth 6
$tmp = "$manifestPath.tmp"
[System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
Move-Item -Force $tmp $manifestPath

Write-Host "Published GolfSimZA $Version"
Write-Host "  Installer : $published"
Write-Host "  SHA-256   : $hash"
Write-Host "  Manifest  : $manifestPath"
