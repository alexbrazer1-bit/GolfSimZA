<#
.SYNOPSIS
  One-click release without opening Unity: builds GolfSimZA.exe in Unity batch mode,
  makes the installer and publishes it to the update folder.

.EXAMPLE
  .\Build-And-Publish.ps1 -Notes "Faster putting greens"
  .\Build-And-Publish.ps1 -BuildOnly

  Close the Unity Editor first (batch mode cannot open a project that is already open).
  Bump the version in Unity (Release Manager) or in Assets\GolfSim\Core\GolfSimVersion.cs before publishing.
#>
param(
    [string] $Notes = '',
    [string] $UpdateFolder = 'C:\Shared\GolfSimZA-Updates',
    [switch] $BuildOnly
)

$ErrorActionPreference = 'Stop'
$project = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..')
$versionLine = Get-Content (Join-Path $project 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
$unityVersion = ($versionLine -split ':')[1].Trim()
$unity = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $unityVersion not found at $unity" }

$log = Join-Path $project 'Logs\GolfSimZA-release.log'
$arguments = @('-batchmode', '-quit', '-projectPath', "`"$project`"", '-executeMethod', 'GolfSimZA.Editor.GolfSimRelease.BuildAndPublishFromCommandLine', '-logFile', "`"$log`"", '-updateFolder', "`"$UpdateFolder`"")
if ($BuildOnly) { $arguments += '-buildOnly' } else { $arguments += @('-notes', "`"$Notes`"") }

Write-Host "Building GolfSimZA with Unity $unityVersion (this takes a few minutes)..."
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0) { throw "Unity build failed (exit $($process.ExitCode)). See $log" }
Write-Host 'Done.'
