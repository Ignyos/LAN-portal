param()

$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
$apiExe = Join-Path $scriptRoot 'api\Ignyos.LanPortal.Api.exe'
$webExe = Join-Path $scriptRoot 'web\Ignyos.LanPortal.Web.exe'
$hostExe = Join-Path $scriptRoot 'host\Ignyos.LanPortal.Host.exe'
$setupUrl = 'http://localhost:5212/local/setup'
$apiListenUrl = 'http://0.0.0.0:5212'
$webListenUrl = 'http://0.0.0.0:80'

function Start-PortalProcess {
    param(
        [string]$ExecutablePath,
        [string]$FriendlyName,
        [string[]]$Arguments
    )

    if (-not (Test-Path $ExecutablePath)) {
        throw "$FriendlyName was not found at $ExecutablePath."
    }

    $processName = [System.IO.Path]::GetFileNameWithoutExtension($ExecutablePath)
    if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
        return
    }

    Start-Process -FilePath $ExecutablePath -ArgumentList $Arguments -WorkingDirectory $scriptRoot -WindowStyle Hidden | Out-Null
}

function Wait-ForSetupPage {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2 | Out-Null
            return
        }
        catch {
            [System.Threading.Thread]::Sleep(500)
        }
    }

    throw "Ignyos LAN Portal did not become ready within $TimeoutSeconds seconds."
}

if (Test-Path $hostExe) {
    Start-Process -FilePath $hostExe -WorkingDirectory (Split-Path $hostExe -Parent) | Out-Null
    return
}

Start-PortalProcess -ExecutablePath $apiExe -FriendlyName 'Ignyos LAN Portal API' -Arguments @('--urls', $apiListenUrl)
Start-PortalProcess -ExecutablePath $webExe -FriendlyName 'Ignyos LAN Portal Web' -Arguments @('--urls', $webListenUrl)
Wait-ForSetupPage -Url $setupUrl
Start-Process -FilePath $setupUrl | Out-Null