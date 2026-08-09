param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $repoRoot 'scripts/publish-live.ps1'

if (-not (Test-Path $publishScript)) {
    throw "Publish script not found: $publishScript"
}

Write-Host "Launching Live-App-Deploy entry point..."
Write-Host "Publish script: $publishScript"
Write-Host ""

& $publishScript @RemainingArgs
exit $LASTEXITCODE
