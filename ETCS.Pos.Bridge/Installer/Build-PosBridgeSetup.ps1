#Requires -Version 5.1
<#
.SYNOPSIS
    Builds ETCS.Pos.Bridge Release, stages files in POSBridge folder, and compiles Setup.exe.
.DESCRIPTION
    Output layout for kiosk distribution (copy the whole POSBridge folder):

        POSBridge/
          ETCS.Pos.Bridge.Setup.exe   <- run this on the kiosk
          ETCS.Pos.Bridge.exe
          Newtonsoft.Json.dll
          ...

.EXAMPLE
    .\Build-PosBridgeSetup.ps1
.EXAMPLE
    .\Build-PosBridgeSetup.ps1 -InnoSetupPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
#>
[CmdletBinding()]
param(
    [string]$InnoSetupPath,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$projectRoot = Resolve-Path (Join-Path $scriptDir '..')
$issFile = Join-Path $scriptDir 'ETCS.Pos.Bridge.Setup.iss'
$stagingDir = Join-Path $scriptDir 'POSBridge'
$outputDir = Join-Path $scriptDir 'Output'
$setupFileName = 'ETCS.Pos.Bridge.Setup.exe'

function Find-InnoSetupCompiler {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path -LiteralPath $ExplicitPath)) {
        return $ExplicitPath
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    return $null
}

function Stage-PosBridgeFiles {
    param(
        [string]$SourceDir,
        [string]$TargetDir
    )

    if (Test-Path -LiteralPath $TargetDir) {
        Get-ChildItem -LiteralPath $TargetDir -Force | Remove-Item -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    }

    Copy-Item -Path (Join-Path $SourceDir '*') -Destination $TargetDir -Recurse -Force

    @('*.pdb', '*.InstallLog', 'publish') | ForEach-Object {
        Get-ChildItem -LiteralPath $TargetDir -Filter $_ -Recurse -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Staged bridge files to: $TargetDir" -ForegroundColor Green
}

Write-Host 'Building ETCS.Pos.Bridge...' -ForegroundColor Cyan
Push-Location $projectRoot
try {
    & dotnet build -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$releaseDir = Join-Path $projectRoot "bin\$Configuration\net48"
$releaseExe = Join-Path $releaseDir 'ETCS.Pos.Bridge.exe'
if (-not (Test-Path -LiteralPath $releaseExe)) {
    throw "Release build not found at $releaseExe"
}

Stage-PosBridgeFiles -SourceDir $releaseDir -TargetDir $stagingDir

$iscc = Find-InnoSetupCompiler -ExplicitPath $InnoSetupPath
if (-not $iscc) {
    Write-Host ''
    Write-Host 'Inno Setup 6 not found.' -ForegroundColor Yellow
    Write-Host 'Install from https://jrsoftware.org/isinfo.php then re-run this script.' -ForegroundColor Yellow
    Write-Host ''
    Write-Host 'POSBridge folder is ready for manual copy / PowerShell install:' -ForegroundColor Green
    Write-Host "  $stagingDir" -ForegroundColor Green
    Write-Host ''
    Write-Host '  cd POSBridge\.. ; .\Install-PosBridge.ps1 -SourcePath .\POSBridge' -ForegroundColor Green
    exit 2
}

if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host 'Compiling installer...' -ForegroundColor Cyan
& $iscc $issFile
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$setupExe = Join-Path $outputDir $setupFileName
$deploySetup = Join-Path $stagingDir $setupFileName
Copy-Item -LiteralPath $setupExe -Destination $deploySetup -Force

Write-Host ''
Write-Host 'Build complete. Distribute this folder to kiosk PCs:' -ForegroundColor Green
Write-Host "  $stagingDir" -ForegroundColor Green
Write-Host ''
Write-Host 'Folder contents:' -ForegroundColor Cyan
Write-Host "  $setupFileName          <- double-click on kiosk (UAC)" -ForegroundColor Cyan
Write-Host '  ETCS.Pos.Bridge.exe + dependencies' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Installs to: C:\Program Files\ETCS\POSBridge' -ForegroundColor Cyan
Write-Host 'Service: ETCSPosBridge (display name: ETCS POS Bridge)' -ForegroundColor Cyan
