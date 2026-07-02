#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstalls the ETCS POS Bridge Windows Service.
.PARAMETER InstallDir
    Installation directory to remove.
.EXAMPLE
    .\Uninstall-PosBridge.ps1
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\Program Files\ETCS\POSBridge'
)

$ErrorActionPreference = 'Stop'

$ServiceName = 'ETCSPosBridge'
$BridgeExeName = 'ETCS.Pos.Bridge.exe'
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

    if ($InstallDir -and $InstallDir -ne 'C:\Program Files\ETCS\POSBridge') {
        $argList += '-InstallDir'
        $argList += $InstallDir
    }

    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs -Wait | Out-Null
    exit 0
}

function Invoke-QuietCommand {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList
    )

    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -Wait -PassThru -WindowStyle Hidden
    return $process.ExitCode
}

try {
    Ensure-Administrator

    Write-Host ''
    Write-Host 'ETCS POS Bridge Uninstaller' -ForegroundColor White
    Write-Host '===========================' -ForegroundColor White
    Write-Host ''

    $installedExe = Join-Path $InstallDir $BridgeExeName
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

    if ($service) {
        Write-Step "Stopping service '$ServiceName'..."
        if ($service.Status -ne 'Stopped') {
            Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
        Write-Ok 'Service stopped'
    }

    if ((Test-Path -LiteralPath $InstallUtilPath) -and (Test-Path -LiteralPath $installedExe)) {
        Write-Step 'Unregistering Windows Service (installutil)...'
        $null = Invoke-QuietCommand -FilePath $InstallUtilPath -ArgumentList @('/u', $installedExe)
    }

    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Write-Step 'Removing service via sc.exe...'
        $null = Invoke-QuietCommand -FilePath 'sc.exe' -ArgumentList @('delete', $ServiceName)
        Write-Ok 'Service unregistered'
    }

    if (Test-Path -LiteralPath $InstallDir) {
        Write-Step "Removing '$InstallDir'..."
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
        Write-Ok 'Install folder removed'
    }

    $remaining = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($remaining) {
        throw "Service '$ServiceName' is still registered."
    }

    Write-Host ''
    Write-Ok 'ETCS POS Bridge uninstalled successfully'
    Write-Host ''
    exit 0
}
catch {
    Write-Host ''
    Write-Fail $_.Exception.Message
    Write-Host ''
    exit 1
}
