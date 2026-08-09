param(
    [ValidateSet('false', 'true')]
    [string]$IncludeHost = 'false',
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/publish-dev.yml'

if (-not (Test-Path $workflowPath)) {
    throw "Workflow file not found: $workflowPath"
}

$branchName = git rev-parse --abbrev-ref HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branchName)) {
    throw "Unable to determine the current Git branch."
}

Write-Host "Dev-App-Deploy is configured to trigger the remote GitHub workflow for the dev publish lane."
Write-Host "Current branch: $branchName"
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "Please run the 'Dev-Deploy' workflow from the GitHub Actions UI for this repository."
Write-Host "Set include_host='$IncludeHost' in the workflow inputs before starting the run."
