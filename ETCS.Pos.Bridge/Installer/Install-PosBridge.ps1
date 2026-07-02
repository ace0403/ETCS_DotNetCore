#Requires -Version 5.1
<#
.SYNOPSIS
    Installs ETCS POS Bridge as a Windows Service with automatic start.
.DESCRIPTION
    Copies Release build files to Program Files, registers ETCSPosBridge via installutil,
    configures auto-start and failure recovery, starts the service, and verifies /health.
.PARAMETER SourcePath
    Folder containing ETCS.Pos.Bridge.exe and dependencies. Defaults to Release build output.
.PARAMETER InstallDir
    Target installation directory.
.PARAMETER Build
    Run dotnet build -c Release before installing.
.EXAMPLE
    .\Install-PosBridge.ps1 -Build
.EXAMPLE
    .\Install-PosBridge.ps1 -SourcePath ".\bin"
#>
[CmdletBinding()]
param(
    [string]$SourcePath,
    [string]$InstallDir = 'C:\Program Files\ETCS\POSBridge',
    [switch]$Build
)

$ErrorActionPreference = 'Stop'

$ServiceName = 'ETCSPosBridge'
$BridgeExeName = 'ETCS.Pos.Bridge.exe'
$HealthUrl = 'http://127.0.0.1:5050/health'
$InstallUtilPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\installutil.exe'

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok([string]$Message) {
    Write-Host "OK  $Message" -ForegroundColor Green
}

function Write-Fail([string]$Message) {
    Write-Host "ERR $Message" -ForegroundColor Red
}

function Test-IsAdministrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Administrator {
    if (Test-IsAdministrator) {
        return
    }

    Write-Step 'Administrator rights required. Re-launching elevated...'
    $argList = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath
    )

    if ($Build) { $argList += '-Build' }
    if ($SourcePath) { $argList += '-SourcePath'; $argList += $SourcePath }
    if ($InstallDir -and $InstallDir -ne 'C:\Program Files\ETCS\POSBridge') {
        $argList += '-InstallDir'
        $argList += $InstallDir
    }

    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs -Wait | Out-Null
    exit 0
}

function Test-DotNet48OrLater {
    $release = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction SilentlyContinue
    if (-not $release -or $release.Release -lt 528040) {
        throw '.NET Framework 4.8 or later is required. Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48'
    }
}

function Get-DefaultSourcePath {
    $scriptDir = Split-Path -Parent $PSCommandPath

    $candidates = @(
        (Join-Path $scriptDir 'POSBridge'),
        $scriptDir,
        (Join-Path $scriptDir '..\bin\Release\net48')
    )

    foreach ($path in $candidates) {
        $resolved = Resolve-Path -LiteralPath $path -ErrorAction SilentlyContinue
        if ($resolved -and (Test-Path -LiteralPath (Join-Path $resolved.Path $BridgeExeName))) {
            return $resolved.Path
        }
    }

    return $null
}

function Invoke-CommandWithOutput {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList,
        [switch]$Quiet
    )

    if ($Quiet) {
        $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -Wait -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput ([System.IO.Path]::GetTempFileName()) `
            -RedirectStandardError ([System.IO.Path]::GetTempFileName())
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = ''; Error = '' }
    }

    $output = & $FilePath @ArgumentList 2>&1
    $exitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } else { 0 }
    return [pscustomobject]@{ ExitCode = $exitCode; Output = ($output | Out-String) }
}

function Stop-BridgeServiceIfPresent {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        return
    }

    Write-Step "Stopping existing service '$ServiceName'..."
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
}

function Remove-BridgeServiceRegistration {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        return
    }

    Write-Step 'Removing previous service registration...'
    if ($service.Status -ne 'Stopped') {
        $null = Invoke-CommandWithOutput -FilePath 'sc.exe' -ArgumentList @('stop', $ServiceName) -Quiet
        Start-Sleep -Seconds 2
    }

    $installedExe = Join-Path $InstallDir $BridgeExeName
    if ((Test-Path -LiteralPath $InstallUtilPath) -and (Test-Path -LiteralPath $installedExe)) {
        $null = Invoke-CommandWithOutput -FilePath $InstallUtilPath -ArgumentList @('/u', $installedExe) -Quiet
    }

    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        $null = Invoke-CommandWithOutput -FilePath 'sc.exe' -ArgumentList @('delete', $ServiceName) -Quiet
        Start-Sleep -Seconds 1
    }
}

function Register-BridgeServiceWithSc {
    param([string]$ExePath)

    Write-Step 'Registering Windows Service via sc.exe...'
    $binPath = "`"$ExePath`""
    $create = Invoke-CommandWithOutput -FilePath 'sc.exe' -ArgumentList @(
        'create', $ServiceName,
        'binPath=', $binPath,
        'start=', 'auto',
        'DisplayName=', 'ETCS POS Bridge'
    )
    if ($create.ExitCode -ne 0) {
        throw "sc create failed: $($create.Output)"
    }

    $null = Invoke-CommandWithOutput -FilePath 'sc.exe' -ArgumentList @(
        'description', $ServiceName, 'Local HTTP bridge for iBonus SOAP and receipt printing.'
    ) -Quiet
}

function Install-BridgeService {
    param([string]$ExePath)

    Register-BridgeServiceWithSc -ExePath $ExePath

    if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
        throw "Service '$ServiceName' was not created. Run this script as Administrator."
    }

    Write-Step 'Configuring automatic start...'
    $null = Invoke-CommandWithOutput -FilePath 'sc.exe' -ArgumentList @('config', $ServiceName, 'start=', 'auto') -Quiet

    Write-Step 'Configuring service recovery...'
    $null = Invoke-CommandWithOutput -FilePath 'sc.exe' -ArgumentList @(
        'failure', $ServiceName,
        'reset=', '86400',
        'actions=', 'restart/60000/restart/60000/restart/60000'
    ) -Quiet
}

function Start-BridgeService {
    Write-Step 'Starting service...'
    $null = Invoke-CommandWithOutput -FilePath 'sc.exe' -ArgumentList @('start', $ServiceName) -Quiet

    $service = Get-Service -Name $ServiceName -ErrorAction Stop
    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))

    if ($service.Status -ne 'Running') {
        throw "Service '$ServiceName' did not reach Running state (current: $($service.Status))."
    }
}

function Test-BridgeHealth {
    Write-Step 'Verifying health endpoint...'
    $deadline = (Get-Date).AddSeconds(30)

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Uri $HealthUrl -Method Get -TimeoutSec 5
            $statusValue = if ($null -ne $response.Status) { $response.Status } else { $response.status }
            if ($statusValue -eq 'ok') {
                Write-Ok "Bridge healthy at $HealthUrl"
                return
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "Health check failed at $HealthUrl"
}

try {
    Ensure-Administrator

    Write-Host ''
    Write-Host 'ETCS POS Bridge Installer' -ForegroundColor White
    Write-Host '=========================' -ForegroundColor White
    Write-Host ''

    Write-Step 'Checking .NET Framework 4.8+...'
    Test-DotNet48OrLater
    Write-Ok '.NET Framework requirement satisfied'

    $projectRoot = Resolve-Path (Join-Path (Split-Path -Parent $PSCommandPath) '..')

    if ($Build) {
        Write-Step 'Building Release configuration...'
        Push-Location $projectRoot
        try {
            & dotnet build -c Release
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet build failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }
        Write-Ok 'Release build completed'
    }

    if (-not $SourcePath) {
        $SourcePath = Get-DefaultSourcePath
    }
    else {
        $SourcePath = (Resolve-Path -LiteralPath $SourcePath).Path
    }

    if (-not $SourcePath -or -not (Test-Path -LiteralPath (Join-Path $SourcePath $BridgeExeName))) {
        throw "Source path must contain $BridgeExeName. Use -Build or set -SourcePath to the Release output folder."
    }

    Write-Ok "Source: $SourcePath"
    Write-Ok "Target: $InstallDir"

    Stop-BridgeServiceIfPresent
    Remove-BridgeServiceRegistration

    Write-Step 'Copying files...'
    if (Test-Path -LiteralPath $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Copy-Item -Path (Join-Path $SourcePath '*') -Destination $InstallDir -Recurse -Force

    $installedExe = Join-Path $InstallDir $BridgeExeName
    Install-BridgeService -ExePath $installedExe
    Start-BridgeService
    Test-BridgeHealth

    $service = Get-Service -Name $ServiceName
    Write-Host ''
    Write-Ok "Service '$ServiceName' installed and running"
    Write-Ok "Start type: $($service.StartType)"
    Write-Ok "Install path: $InstallDir"
    Write-Ok "Health URL: $HealthUrl"
    Write-Host ''
    exit 0
}
catch {
    Write-Host ''
    Write-Fail $_.Exception.Message
    Write-Host ''
    exit 1
}
