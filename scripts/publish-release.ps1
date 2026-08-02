param(
    [switch]$DryRun,
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

& $publishScript `
    -DryRun:$DryRun `
    -MainBranch $MainBranch `
    -VersionProjectPath $VersionProjectPath `
    -ReleaseNotesPath $ReleaseNotesPath `
    -ReleaseNotesStyleGuidePath $ReleaseNotesStyleGuidePath `
    -TagPrefix $TagPrefix `
    -PublishVersion $PublishVersion `
    -ConfirmVersion:$ConfirmVersion `
    -ContinueAfterReleaseNotes:$ContinueAfterReleaseNotes `
    -ApprovePublish:$ApprovePublish
