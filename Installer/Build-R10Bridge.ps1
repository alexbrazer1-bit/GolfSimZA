<#
.SYNOPSIS
  Builds the GolfSimZA R10 Bluetooth bridge (gspro-r10.exe) on this PC.

.DESCRIPTION
  Uses the pinned upstream source in R10Bridge\_upstream (mholow/gsp-r10-adapter, MIT) and
  GolfSimZA's R10Bridge\settings.json. Output: R10Bridge\publish\gspro-r10.exe (self-contained, win-x64).
  If no .NET SDK is installed, the .NET 8 SDK is installed for this user only (no admin rights needed).
  Log: Logs\GolfSimZA-bridge.log
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..')
$logDir = Join-Path $root 'Logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
Start-Transcript -Path (Join-Path $logDir 'GolfSimZA-bridge.log') -Force | Out-Null

try {
    $source = Join-Path $root 'R10Bridge\_upstream'
    $project = Join-Path $source 'gspro-r10.csproj'
    if (-not (Test-Path $project)) { throw "Bridge source not found: $project" }

    function Find-Dotnet {
        $candidates = @()
        if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
        if ($env:LOCALAPPDATA) { $candidates += Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe' }
        foreach ($c in $candidates) {
            if (Test-Path $c) {
                $sdks = & $c --list-sdks 2>$null
                if ($sdks) { return $c }
            }
        }
        return $null
    }

    $dotnet = Find-Dotnet
    if (-not $dotnet) {
        Write-Host 'No .NET SDK found - installing .NET 8 SDK for this user...'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
        $ProgressPreference = 'SilentlyContinue'
        & $installer -Channel 8.0 -InstallDir (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet') -NoPath
        $dotnet = Find-Dotnet
        if (-not $dotnet) { throw '.NET SDK installation failed.' }
    }
    Write-Host "Using $dotnet"
    # Native tools write progress to stderr; do not treat that as a PowerShell error.
    $ErrorActionPreference = 'Continue'
    & $dotnet --list-sdks 2>&1 | ForEach-Object { Write-Host $_ }

    Copy-Item -Force (Join-Path $root 'R10Bridge\settings.json') (Join-Path $source 'settings.json')

    $output = Join-Path $root 'R10Bridge\publish'
    if (Test-Path $output) { Remove-Item -Recurse -Force $output }

    # InvariantGlobalization: settings.json uses '.' decimals; South African regional settings use ','.
    # The upstream project targets .NET 7 (end of life); build it on the supported .NET 8 runtime.
    $csproj = Get-Content -Raw $project
    $csproj = $csproj -replace '<TargetFramework>net7\.0-windows', '<TargetFramework>net8.0-windows'
    Set-Content -Path $project -Value $csproj -Encoding UTF8
    foreach ($generated in @('obj', 'bin')) {
        $path = Join-Path $source $generated
        if (Test-Path $path) { Remove-Item -Recurse -Force $path }
    }

    & $dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:InvariantGlobalization=true `
        -o $output 2>&1 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

    Copy-Item -Force (Join-Path $root 'R10Bridge\README.md') (Join-Path $output 'README.md')
    Copy-Item -Force (Join-Path $root 'R10Bridge\LICENSE-THIRD-PARTY.txt') (Join-Path $output 'LICENSE-THIRD-PARTY.txt')
    Copy-Item -Force (Join-Path $root 'R10Bridge\settings.json') (Join-Path $output 'settings.json')

    if (-not (Test-Path (Join-Path $output 'gspro-r10.exe'))) { throw 'gspro-r10.exe was not produced.' }
    Write-Host "R10 bridge built: $output"
}
catch {
    Write-Host "ERROR: $_"
    Stop-Transcript | Out-Null
    exit 1
}
Stop-Transcript | Out-Null
exit 0
