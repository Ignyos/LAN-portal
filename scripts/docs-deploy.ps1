param(
    [ValidateSet('dev', 'prod')]
    [string]$Target
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/deploy-pages.yml'

if (-not (Test-Path $workflowPath)) {
    throw "Workflow file not found: $workflowPath"
}

$branchName = git rev-parse --abbrev-ref HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branchName)) {
    throw "Unable to determine the current Git branch."
}

if ([string]::IsNullOrWhiteSpace($Target)) {
    $Target = switch ($branchName) {
        'dev' { 'dev' }
        'main' { 'prod' }
        default {
            throw "Docs deployment is only supported from the 'dev' or 'main' branches. Current branch: $branchName"
        }
    }
}

Write-Host "Docs-Deploy is configured to trigger the remote GitHub workflow for the '$Target' Pages target."
Write-Host "Current branch: $branchName"
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "Please run the 'Deploy-pages' workflow from the GitHub Actions UI for this repository."
Write-Host "Select target '$Target' and the matching branch."
