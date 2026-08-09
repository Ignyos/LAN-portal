param(
    [ValidateSet('prod','dev','both')]
    [string]$Target = 'both'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/deploy-pages.yml'

if (-not (Test-Path $workflowPath)) {
    throw "Workflow file not found: $workflowPath"
}

Write-Host "Deploy-pages is configured to publish the static Pages content from docs/ to the target repository or repositories."
Write-Host "Target: $Target"
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "This action does not build installers or update manifests."
Write-Host "It only pushes the contents of docs/ into the target Pages repos."
