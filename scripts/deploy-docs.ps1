param()

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

$target = switch ($branchName) {
    'dev' { 'dev' }
    'main' { 'prod' }
    default {
        throw "Docs deployment is only supported from the 'dev' or 'main' branches. Current branch: $branchName"
    }
}

Write-Host "Deploy-pages is configured to publish the static Pages content from docs/ to the target repository."
Write-Host "Current branch: $branchName"
Write-Host "Resolved target: $target"
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "This action does not build installers or update manifests."
Write-Host "It only pushes the contents of docs/ into the target Pages repos."
