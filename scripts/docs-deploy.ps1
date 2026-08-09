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

$remoteUrl = git config --get remote.origin.url 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
    throw "Unable to determine the GitHub remote from the repository configuration."
}

$repoMatch = [regex]::Match($remoteUrl, 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$')
if (-not $repoMatch.Success) {
    throw "Could not parse the GitHub repository from remote URL: $remoteUrl"
}

$owner = $repoMatch.Groups['owner'].Value
$repo = $repoMatch.Groups['repo'].Value

$token = $env:DEV_PAGES_TOKEN
if ($Target -eq 'prod') {
    $token = $env:PROD_PAGES_TOKEN
}
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "No Pages token found. Set DEV_PAGES_TOKEN or PROD_PAGES_TOKEN before running this script."
}

$headers = @{
    Authorization = "Bearer $token"
    Accept = 'application/vnd.github+json'
    'User-Agent' = 'LAN-portal-local-deploy'
}

$workflowFile = 'deploy-pages.yml'
$uri = "https://api.github.com/repos/$owner/$repo/actions/workflows/$workflowFile/dispatches"
$body = @{ ref = $branchName; inputs = @{ target = $Target } } | ConvertTo-Json -Depth 6

Write-Host "Dispatching docs workflow for branch '$branchName' targeting '$Target'..."
Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType 'application/json' -Body $body | Out-Null
Write-Host "Workflow dispatch sent successfully."
