param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/release-artifacts.yml'

if (-not (Test-Path $workflowPath)) {
    throw "Workflow file not found: $workflowPath"
}

Write-Host "Live-Deploy is configured to trigger the live release workflow."
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "This action builds the live release payload and updates the live manifest."
Write-Host "It also mirrors the live release to the dev repo as part of the intended release flow."
