param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/publish-dev.yml'

if (-not (Test-Path $workflowPath)) {
    throw "Workflow file not found: $workflowPath"
}

Write-Host "Dev-Deploy is configured to trigger the dev release workflow."
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "This action builds the dev release payload and updates the dev repo manifest only."
Write-Host "It does not publish back to the production repo."
