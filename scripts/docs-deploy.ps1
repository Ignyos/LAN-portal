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

$refName = if ($Target -eq 'dev') { 'dev' } else { 'main' }
$repoSlug = $env:GITHUB_REPOSITORY
if ([string]::IsNullOrWhiteSpace($repoSlug)) {
    $originUrl = git config --get remote.origin.url 2>$null
    if ($LASTEXITCODE -eq 0 -and $originUrl) {
        if ($originUrl -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$') {
            $repoSlug = "$($Matches.owner)/$($Matches.repo)"
        }
    }
}

if (Get-Command gh -ErrorAction SilentlyContinue) {
    Write-Host "Triggering GitHub Actions workflow for target '$Target'..."
    $ghArgs = @('workflow', 'run', '.github/workflows/deploy-pages.yml', '--repo', $repoSlug, '--ref', $refName, '-f', "target=$Target")
    & gh @ghArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Remote workflow dispatch started successfully."
        exit 0
    }

    Write-Host "GitHub CLI workflow dispatch did not complete successfully. Falling back to manual guidance."
}

Write-Host "Docs-Deploy is configured to trigger the remote GitHub workflow for the '$Target' Pages target."
Write-Host "Current branch: $branchName"
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "If GitHub CLI is available and authenticated, the script will trigger the workflow automatically."
Write-Host "Otherwise, please run the 'Deploy-pages' workflow from the GitHub Actions UI for this repository."
Write-Host "Select target '$Target' and the matching branch."
