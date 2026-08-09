param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/release-artifacts.yml'

if (-not (Test-Path $workflowPath)) {
    throw "Workflow file not found: $workflowPath"
}

$branchName = git rev-parse --abbrev-ref HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branchName)) {
    throw "Unable to determine the current Git branch."
}

Write-Host "Live-App-Deploy is configured to trigger the remote GitHub workflow for the live publish lane."
Write-Host "Current branch: $branchName"
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "Please run the 'Live-Deploy' workflow from the GitHub Actions UI for this repository."
