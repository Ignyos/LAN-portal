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
}
finally {
    Pop-Location
}
