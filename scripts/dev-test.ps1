param(
    [string]$WorkspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$WorkspaceRoot = (Resolve-Path $WorkspaceRoot).Path

& (Join-Path $WorkspaceRoot 'scripts/stop-dev-processes.ps1') -WorkspaceRoot $WorkspaceRoot
if ($LASTEXITCODE -ne 0) {
    throw "Unable to stop stale development processes."
}

$projectDirectories = @(
    'Ignyos.LanPortal.Contracts',
    'Ignyos.LanPortal.Api',
    'Ignyos.LanPortal.Web',
    'Ignyos.LanPortal.Host',
    'Ignyos.LanPortal.Api.Tests'
)

# The SQLite database lives under bin, so it must survive the clean or every
# launch discards the storage root, JWT signing key, access history, and logs.
$databaseDirectory = Join-Path $WorkspaceRoot 'Ignyos.LanPortal.Api/bin/Debug/net9.0/data'
$databaseBackup = $null
if (Test-Path $databaseDirectory) {
    $databaseBackup = Join-Path ([System.IO.Path]::GetTempPath()) ("lanportal-dev-data-" + [Guid]::NewGuid().ToString('N'))
    Copy-Item -Path $databaseDirectory -Destination $databaseBackup -Recurse -Force
    Write-Host "Preserved development database from $databaseDirectory"
}

foreach ($projectDirectory in $projectDirectories) {
    foreach ($buildDirectory in @('bin', 'obj')) {
        $path = Join-Path $WorkspaceRoot (Join-Path $projectDirectory $buildDirectory)
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Push-Location $WorkspaceRoot
try {
    $versionProjectPath = Join-Path $WorkspaceRoot 'Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj'
    [xml]$versionProject = Get-Content $versionProjectPath
    $version = $versionProject.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "No <Version> value found in $versionProjectPath."
    }

    dotnet build '.\Ignyos.LanPortal.sln' "-p:Version=$version" "-p:InformationalVersion=$version"
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed."
    }

    if ($databaseBackup -and (Test-Path $databaseBackup)) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $databaseDirectory) -Force | Out-Null
        Copy-Item -Path $databaseBackup -Destination $databaseDirectory -Recurse -Force
        Remove-Item $databaseBackup -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Restored development database to $databaseDirectory"
    }
}
finally {
    Pop-Location
}
