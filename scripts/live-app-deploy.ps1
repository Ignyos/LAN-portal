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

$token = $env:PROD_PAGES_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "No production Pages token found. Set PROD_PAGES_TOKEN before running this script."
}

$headers = @{
    Authorization = "Bearer $token"
    Accept = 'application/vnd.github+json'
    'User-Agent' = 'LAN-portal-local-deploy'
}

$workflowFile = 'release-artifacts.yml'
$uri = "https://api.github.com/repos/$owner/$repo/actions/workflows/$workflowFile/dispatches"
$body = @{ ref = $branchName } | ConvertTo-Json -Depth 6

Write-Host "Dispatching live workflow for branch '$branchName'..."
Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType 'application/json' -Body $body | Out-Null
Write-Host "Workflow dispatch sent successfully."
