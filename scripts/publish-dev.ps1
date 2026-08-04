param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$PassthroughArgs
)

$publishScript = Join-Path $PSScriptRoot 'publish-live.ps1'
if (-not (Test-Path -LiteralPath $publishScript)) {
    throw "Required script not found: $publishScript"
}

function Test-HasDryRunArgument {
    param([object[]]$ArgsToInspect)

    foreach ($arg in $ArgsToInspect) {
        if ($arg -isnot [string]) {
            continue
        }

        if ($arg -match '^-DryRun(?::\$(true|false))?$') {
            return $true
        }
    }

    return $false
}

$hasDryRunArgument = Test-HasDryRunArgument -ArgsToInspect $PassthroughArgs
if ($hasDryRunArgument) {
    & $publishScript @PassthroughArgs
    exit $LASTEXITCODE
}

if (-not [Environment]::UserInteractive) {
    throw "Dry-run mode was not specified in non-interactive execution. Pass -DryRun or -DryRun:`$false explicitly."
}

$choice = Read-Host "Run as dry run? [Y/n]"
$runAsDryRun = $choice -notmatch '^(n|no)$'

& $publishScript -DryRun:$runAsDryRun @PassthroughArgs
exit $LASTEXITCODE
