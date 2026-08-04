param(
    [switch]$DryRun,
    [switch]$NonInteractive,
    [switch]$DevVersionSuggestion,
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

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-common.ps1")

$ExitCodes = @{
    Success = 0
    BranchGateFailed = 10
    CleanGateFailed = 11
    SyncGateFailed = 12
    VersionReadFailed = 20
    VersionValidationFailed = 21
    TagExistsFailed = 22
    DiffFailed = 30
    ReleaseNotesFailed = 40
    UserCancelled = 50
    PublishFailed = 60
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$releaseWorkRoot = Join-Path $repoRoot "artifacts/release-publish"
New-Item -ItemType Directory -Path $releaseWorkRoot -Force | Out-Null

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logPath = Join-Path $releaseWorkRoot "publish-$runStamp.log"
$promptPath = Join-Path $releaseWorkRoot "ai-release-prompt-$runStamp.md"
$summaryPath = Join-Path $releaseWorkRoot "release-diff-summary-$runStamp.txt"
$diffPath = Join-Path $releaseWorkRoot "release-diff-$runStamp.patch"

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet("INFO", "WARN", "ERROR")]
        [string]$Level = "INFO"
    )

    $line = "[{0}] [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Level, $Message
    Write-Host $line
    Add-Content -Path $logPath -Value $line
}

function Exit-WithError {
    param(
        [int]$Code,
        [string]$Message
    )

    Write-Log -Level "ERROR" -Message $Message
    Write-Log -Level "ERROR" -Message "Exiting with code $Code"
    exit $Code
}

function Invoke-Git {
    param(
        [string[]]$GitArgs,
        [switch]$AllowFailure
    )

    return Invoke-ReleaseGit -GitArgs $GitArgs -AllowFailure:$AllowFailure -FailureCode $ExitCodes.PublishFailed -OnFailure ${function:Exit-WithError}
}

function Resolve-RelativePath {
    param([string]$RelativePath)
    return (Resolve-ReleasePath -RepoRoot $repoRoot -RelativePath $RelativePath)
}

function Get-ChangedPaths {
    $statusResult = Invoke-Git -GitArgs @("status", "--porcelain")
    return Get-ReleaseChangedPaths -GitStatusOutput $statusResult.Output
}

Write-Log "Starting publish workflow. DryRun=$DryRun"

$branchResult = Invoke-Git -GitArgs @("rev-parse", "--abbrev-ref", "HEAD")
$currentBranch = $branchResult.Output.Trim()
$targetBranch = if ([string]::IsNullOrWhiteSpace($MainBranch)) { $currentBranch } else { $MainBranch.Trim() }

Write-Log "Current branch: $currentBranch"
Write-Log "Target publish branch: $targetBranch"

if ($currentBranch -ne $targetBranch) {
    Exit-WithError -Code $ExitCodes.BranchGateFailed -Message "Branch gate failed. Expected '$targetBranch' but found '$currentBranch'."
}

$dirtyPaths = Get-ChangedPaths
if ($dirtyPaths.Count -gt 0) {
    $joined = $dirtyPaths -join ", "
    Exit-WithError -Code $ExitCodes.CleanGateFailed -Message "Clean gate failed. Working tree has pending changes: $joined"
}

Write-Log "Fetching latest refs and tags from origin/$targetBranch"
$null = Invoke-Git -GitArgs @("fetch", "origin", $targetBranch, "--tags")

$localHead = (Invoke-Git -GitArgs @("rev-parse", "HEAD")).Output.Trim()
$remoteHead = (Invoke-Git -GitArgs @("rev-parse", "origin/$targetBranch")).Output.Trim()
if ($localHead -ne $remoteHead) {
    Exit-WithError -Code $ExitCodes.SyncGateFailed -Message "Sync gate failed. Local HEAD ($localHead) does not match origin/$targetBranch ($remoteHead)."
}

$versionProjectFullPath = Resolve-RelativePath -RelativePath $VersionProjectPath
if (-not (Test-Path $versionProjectFullPath)) {
    Exit-WithError -Code $ExitCodes.VersionReadFailed -Message "Version source file not found: $VersionProjectPath"
}

$projectXml = Get-Content -Path $versionProjectFullPath -Raw
if ($projectXml -notmatch '<Version>\s*(?<version>[^<\s]+)\s*</Version>') {
    Exit-WithError -Code $ExitCodes.VersionReadFailed -Message "No <Version> element found in $VersionProjectPath"
}

$currentVersion = $Matches.version.Trim()
if (-not (Test-ReleaseSemVer -Value $currentVersion)) {
    Exit-WithError -Code $ExitCodes.VersionValidationFailed -Message "Current version '$currentVersion' is not valid SemVer."
}

try {
    if ($DevVersionSuggestion) {
        $defaultVersion = Get-DevSuggestedVersion -CurrentVersion $currentVersion
    }
    else {
        $defaultVersion = Get-NextReleasePatchVersion -CurrentVersion $currentVersion
    }
}
catch {
    Exit-WithError -Code $ExitCodes.VersionValidationFailed -Message $_.Exception.Message
}

$targetVersion = if ([string]::IsNullOrWhiteSpace($PublishVersion)) {
    if ($DryRun) {
        Write-Log "DryRun mode: using default publish version $defaultVersion"
        $defaultVersion
    }
    elseif ($NonInteractive) {
        Write-Log "NonInteractive mode: using default publish version $defaultVersion"
        $defaultVersion
    }
    else {
        $enteredVersion = Read-Host "Enter version to publish [$defaultVersion]"
        if ([string]::IsNullOrWhiteSpace($enteredVersion)) { $defaultVersion } else { $enteredVersion.Trim() }
    }
}
else {
    $PublishVersion.Trim()
}

if (-not (Test-ReleaseSemVer -Value $targetVersion)) {
    Exit-WithError -Code $ExitCodes.VersionValidationFailed -Message "Chosen version '$targetVersion' is not valid SemVer."
}

if ($ConfirmVersion -or $DryRun) {
    if ($DryRun -and -not $ConfirmVersion) {
        Write-Log "DryRun mode: auto-confirming version $targetVersion"
    }
    else {
        Write-Log "Version confirmation override enabled via -ConfirmVersion for $targetVersion"
    }
}
elseif ($NonInteractive) {
    Exit-WithError -Code $ExitCodes.UserCancelled -Message "NonInteractive mode requires -ConfirmVersion for non-dry-run publishes."
}
else {
    $confirmVersion = Read-Host "Publish version $targetVersion? [y/N]"
    if ($confirmVersion -notin @("y", "Y", "yes", "YES")) {
        Write-Log -Level "WARN" -Message "User cancelled at version confirmation step."
        exit $ExitCodes.UserCancelled
    }
}

$tagName = "$TagPrefix$targetVersion"

$localTagExists = Invoke-Git -GitArgs @("show-ref", "--tags", "--verify", "--quiet", "refs/tags/$tagName") -AllowFailure
if ($localTagExists.ExitCode -eq 0) {
    Exit-WithError -Code $ExitCodes.TagExistsFailed -Message "Tag '$tagName' already exists locally."
}

$remoteTagLookup = Invoke-Git -GitArgs @("ls-remote", "--tags", "origin", "refs/tags/$tagName")
if (-not [string]::IsNullOrWhiteSpace($remoteTagLookup.Output)) {
    Exit-WithError -Code $ExitCodes.TagExistsFailed -Message "Tag '$tagName' already exists on origin."
}

$previousTagResult = Invoke-Git -GitArgs @("describe", "--tags", "--abbrev=0") -AllowFailure
$baselineRef = $null
$baselineDescription = $null

if ($previousTagResult.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($previousTagResult.Output)) {
    $baselineRef = $previousTagResult.Output.Trim()
    $baselineDescription = "latest tag $baselineRef"
}
else {
    $rootCommit = (Invoke-Git -GitArgs @("rev-list", "--max-parents=0", "HEAD")).Output.Split([Environment]::NewLine)[0].Trim()
    if ([string]::IsNullOrWhiteSpace($rootCommit)) {
        Exit-WithError -Code $ExitCodes.DiffFailed -Message "Unable to resolve root commit baseline."
    }

    $baselineRef = $rootCommit
    $baselineDescription = "root commit $baselineRef"
}

Write-Log "Diff baseline: $baselineDescription"

$diffRange = "$baselineRef..HEAD"
$fullDiff = Invoke-Git -GitArgs @("diff", $diffRange)
$summaryStat = Invoke-Git -GitArgs @("diff", "--stat", $diffRange)
$summaryNames = Invoke-Git -GitArgs @("diff", "--name-status", $diffRange)
$commitSummary = Invoke-Git -GitArgs @("log", "--oneline", $diffRange)

Set-Content -Path $diffPath -Value $fullDiff.Output -NoNewline

$summaryText = @(
    "Release Diff Summary"
    "===================="
    "Range: $diffRange"
    "Baseline: $baselineDescription"
    ""
    "Changed Files"
    "-------------"
    $summaryNames.Output
    ""
    "Stats"
    "-----"
    $summaryStat.Output
    ""
    "Commits"
    "-------"
    $commitSummary.Output
)

Set-Content -Path $summaryPath -Value $summaryText
Write-Log "Saved diff to $diffPath"
Write-Log "Saved diff summary to $summaryPath"

$releaseNotesFullPath = Resolve-RelativePath -RelativePath $ReleaseNotesPath
$releaseNotesDir = Split-Path -Parent $releaseNotesFullPath
if (-not (Test-Path $releaseNotesDir)) {
    New-Item -ItemType Directory -Path $releaseNotesDir -Force | Out-Null
}
Set-Content -Path $releaseNotesFullPath -Value ""
Write-Log "Cleared release notes file at $ReleaseNotesPath"

$styleGuideFullPath = Resolve-RelativePath -RelativePath $ReleaseNotesStyleGuidePath
if (-not (Test-Path $styleGuideFullPath)) {
    Exit-WithError -Code $ExitCodes.ReleaseNotesFailed -Message "Release notes style guide not found: $ReleaseNotesStyleGuidePath"
}

$promptText = @"
You are preparing GitHub release notes for a tagged release.

Inputs:
- Style guide: $ReleaseNotesStyleGuidePath
- Full code diff: $diffPath
- Diff summary: $summaryPath
- Release version: $targetVersion
- Release tag: $tagName
- Baseline: $baselineDescription

Instructions:
1. Read the style guide first and follow it exactly.
2. Review the full diff and summary files.
3. Write release notes for this release only (no historical carryover).
4. Output must be saved to: $ReleaseNotesPath
5. Replace all existing content in that file.
6. Keep entries factual and based only on the provided diff.
7. Include a concise risk/impact callout section.
8. If uncertain about a change, label it clearly as "Needs verification".
"@

Set-Content -Path $promptPath -Value $promptText
Write-Log "Saved AI prompt to $promptPath"

try {
    Set-Clipboard -Value $promptText
    Write-Log "Copied AI prompt to clipboard."
}
catch {
    Write-Log -Level "WARN" -Message "Could not copy to clipboard automatically. Use prompt file: $promptPath"
}

Write-Host ""
Write-Host "Next Step:" -ForegroundColor Yellow
Write-Host "1) Paste the clipboard prompt into your AI console."
Write-Host "2) Wait for AI to write release notes to $ReleaseNotesPath."
Write-Host ""

if ($DryRun) {
    Write-Log "Dry run complete. Skipping commit, tag, and push."
    Write-Log "Artifacts: diff=$diffPath summary=$summaryPath prompt=$promptPath log=$logPath"
    exit $ExitCodes.Success
}

if ($ContinueAfterReleaseNotes) {
    Write-Log "Continue override enabled via -ContinueAfterReleaseNotes"
}
elseif ($NonInteractive) {
    Exit-WithError -Code $ExitCodes.UserCancelled -Message "NonInteractive mode requires -ContinueAfterReleaseNotes for non-dry-run publishes."
}
else {
    $continueChoice = Read-Host "Type CONTINUE to proceed after AI finishes, or EXIT to cancel"
    if ($continueChoice -notin @("CONTINUE", "continue")) {
        Write-Log -Level "WARN" -Message "User exited before publish."
        exit $ExitCodes.UserCancelled
    }
}

if (-not (Test-Path $releaseNotesFullPath)) {
    Exit-WithError -Code $ExitCodes.ReleaseNotesFailed -Message "Release notes file does not exist: $ReleaseNotesPath"
}

$releaseNotesContent = Get-Content -Path $releaseNotesFullPath -Raw
if ([string]::IsNullOrWhiteSpace($releaseNotesContent)) {
    Exit-WithError -Code $ExitCodes.ReleaseNotesFailed -Message "Release notes file is empty: $ReleaseNotesPath"
}

Write-Host ""
Write-Host "Release Notes Preview:" -ForegroundColor Yellow
Write-Host "--------------------------------------------------"
Get-Content -Path $releaseNotesFullPath
Write-Host "--------------------------------------------------"
Write-Host ""

if ($ApprovePublish) {
    Write-Log "Final approval override enabled via -ApprovePublish"
}
elseif ($NonInteractive) {
    Exit-WithError -Code $ExitCodes.UserCancelled -Message "NonInteractive mode requires -ApprovePublish for non-dry-run publishes."
}
else {
    $approval = Read-Host "Proceed with commit, tag, and push? [y/N]"
    if ($approval -notin @("y", "Y", "yes", "YES")) {
        Write-Log -Level "WARN" -Message "User cancelled at final approval."
        exit $ExitCodes.UserCancelled
    }
}

$projectXmlLatest = Get-Content -Path $versionProjectFullPath -Raw
$updatedProjectXml = [regex]::Replace(
    $projectXmlLatest,
    '<Version>\s*[^<\s]+\s*</Version>',
    "<Version>$targetVersion</Version>",
    1
)

if ($updatedProjectXml -ne $projectXmlLatest) {
    Set-Content -Path $versionProjectFullPath -Value $updatedProjectXml
    Write-Log "Updated version source in $VersionProjectPath to $targetVersion"
}

$allowedChanges = @(
    ($VersionProjectPath -replace '\\', '/'),
    ($ReleaseNotesPath -replace '\\', '/')
)
$workingChanges = Get-ChangedPaths | ForEach-Object { $_ -replace '\\', '/' }
$unexpected = $workingChanges | Where-Object { $_ -notin $allowedChanges }
if ($unexpected.Count -gt 0) {
    $unexpectedJoined = $unexpected -join ", "
    Exit-WithError -Code $ExitCodes.CleanGateFailed -Message "Unexpected changed files before commit: $unexpectedJoined"
}

$null = Invoke-Git -GitArgs @("add", $VersionProjectPath, $ReleaseNotesPath)
$null = Invoke-Git -GitArgs @("commit", "-m", "release: $tagName")
$null = Invoke-Git -GitArgs @("tag", "-a", $tagName, "-m", "Release $tagName")

$null = Invoke-Git -GitArgs @("push", "origin", "HEAD:$targetBranch")
$null = Invoke-Git -GitArgs @("push", "origin", $tagName)

Write-Log "Publish complete. Pushed commit and tag $tagName to origin/$targetBranch"
Write-Log "Log file: $logPath"
exit $ExitCodes.Success
