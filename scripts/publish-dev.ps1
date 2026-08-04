param(
    [switch]$DryRun,
    [switch]$Live,
    [switch]$NonInteractive,
    [string]$MainBranch,
    [string]$VersionProjectPath = "Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj",
    [string]$ReleaseNotesPath = ".github/release/release-notes.md",
    [string]$ReleaseNotesStyleGuidePath = ".github/release/release-notes-style-guide.md",
    [string]$TagPrefix = "v",
    [string]$PublishVersion,
    [switch]$ConfirmVersion,
    [switch]$ContinueAfterReleaseNotes,
    [switch]$ApprovePublish
)

$publishScript = Join-Path $PSScriptRoot 'publish-live.ps1'
if (-not (Test-Path -LiteralPath $publishScript)) {
    throw "Required script not found: $publishScript"
}

$hasDryRunSwitch = $PSBoundParameters.ContainsKey('DryRun')
$hasLiveSwitch = $PSBoundParameters.ContainsKey('Live') -and $Live

if ($hasDryRunSwitch -and $hasLiveSwitch) {
    throw "Specify only one mode switch: -DryRun or -Live."
}

if ($hasDryRunSwitch) {
    $runAsDryRun = [bool]$DryRun
}
elseif ($hasLiveSwitch) {
    $runAsDryRun = $false
}
else {
    if (-not [Environment]::UserInteractive) {
        throw "Dry-run mode was not specified in non-interactive execution. Pass -DryRun or -Live explicitly."
    }

    $choice = Read-Host "Run as dry run? [Y/n]"
    $runAsDryRun = $choice -notmatch '^(n|no)$'
}

& $publishScript `
    -DryRun:$runAsDryRun `
    -NonInteractive:$NonInteractive `
    -DevVersionSuggestion `
    -MainBranch $MainBranch `
    -VersionProjectPath $VersionProjectPath `
    -ReleaseNotesPath $ReleaseNotesPath `
    -ReleaseNotesStyleGuidePath $ReleaseNotesStyleGuidePath `
    -TagPrefix $TagPrefix `
    -PublishVersion $PublishVersion `
    -ConfirmVersion:$ConfirmVersion `
    -ContinueAfterReleaseNotes:$ContinueAfterReleaseNotes `
    -ApprovePublish:$ApprovePublish

exit $LASTEXITCODE
