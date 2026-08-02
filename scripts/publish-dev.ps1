param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$PassthroughArgs
)

$publishScript = Join-Path $PSScriptRoot 'publish-live.ps1'
if (-not (Test-Path -LiteralPath $publishScript)) {
    throw "Required script not found: $publishScript"
}

& $publishScript -DryRun @PassthroughArgs
