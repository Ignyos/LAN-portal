param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $repoRoot 'scripts/publish-dev.ps1'

if (-not (Test-Path $publishScript)) {
    throw "Publish script not found: $publishScript"
}

Write-Host "Launching Dev-App-Deploy entry point..."
Write-Host "Publish script: $publishScript"
Write-Host ""

& $publishScript @RemainingArgs
exit $LASTEXITCODE
