param(
    [switch]$DryRun,
    [switch]$NonInteractive,
    [string]$DevBranch = "dev",
    [string]$VersionProjectPath = "Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj",
    [string]$PublishVersion,
    [switch]$ConfirmVersion,
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
    UserCancelled = 50
    PublishFailed = 60
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$releaseWorkRoot = Join-Path $repoRoot "artifacts/release-publish"
New-Item -ItemType Directory -Path $releaseWorkRoot -Force | Out-Null

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logPath = Join-Path $releaseWorkRoot "publish-dev-$runStamp.log"

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

Write-Log "Starting dev publish workflow. DryRun=$DryRun"

$branchResult = Invoke-Git -GitArgs @("rev-parse", "--abbrev-ref", "HEAD")
$currentBranch = $branchResult.Output.Trim()
$targetBranch = if ([string]::IsNullOrWhiteSpace($DevBranch)) { "dev" } else { $DevBranch.Trim() }

Write-Log "Current branch: $currentBranch"
Write-Log "Target dev branch: $targetBranch"

if ($currentBranch -ne $targetBranch) {
    Exit-WithError -Code $ExitCodes.BranchGateFailed -Message "Branch gate failed. Expected 'dev' but found '$currentBranch'."
}

$dirtyPaths = Get-ChangedPaths
if ($dirtyPaths.Count -gt 0) {
    $joined = $dirtyPaths -join ", "
    Exit-WithError -Code $ExitCodes.CleanGateFailed -Message "Clean gate failed. Working tree has pending changes: $joined"
}

Write-Log "Fetching latest refs from origin/$targetBranch"
$null = Invoke-Git -GitArgs @("fetch", "origin", $targetBranch)

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
    $defaultVersion = Get-DevSuggestedVersion -CurrentVersion $currentVersion
}
catch {
    Exit-WithError -Code $ExitCodes.VersionValidationFailed -Message $_.Exception.Message
}

$targetVersion = if ([string]::IsNullOrWhiteSpace($PublishVersion)) {
    if ($DryRun) {
        Write-Log "DryRun mode: using suggested dev version $defaultVersion"
        $defaultVersion
    }
    elseif ($NonInteractive) {
        Write-Log "NonInteractive mode: using suggested dev version $defaultVersion"
        $defaultVersion
    }
    else {
        $enteredVersion = Read-Host "Enter dev publish version (current $currentVersion) [$defaultVersion]: "
        if ([string]::IsNullOrWhiteSpace($enteredVersion)) { $defaultVersion } else { $enteredVersion.Trim() }
    }
}
else {
    $PublishVersion.Trim()
}

if (-not (Test-ReleaseSemVer -Value $targetVersion)) {
    Exit-WithError -Code $ExitCodes.VersionValidationFailed -Message "Chosen version '$targetVersion' is not valid SemVer."
}

$targetVersionParts = $targetVersion -split '\.'
if ($targetVersionParts.Length -ne 4) {
    Exit-WithError -Code $ExitCodes.VersionValidationFailed -Message "Dev publish version '$targetVersion' must be a four-part version with a non-zero build component."
}

if ([int]$targetVersionParts[3] -le 0) {
    Exit-WithError -Code $ExitCodes.VersionValidationFailed -Message "Dev publish version '$targetVersion' must use a non-zero fourth node."
}

if ($ConfirmVersion -or $DryRun) {
    if ($DryRun -and -not $ConfirmVersion) {
        Write-Log "DryRun mode: auto-confirming dev version $targetVersion"
    }
    else {
        Write-Log "Version confirmation override enabled via -ConfirmVersion for $targetVersion"
    }
}
elseif ($NonInteractive) {
    Exit-WithError -Code $ExitCodes.UserCancelled -Message "NonInteractive mode requires -ConfirmVersion for non-dry-run publishes."
}
else {
    $confirmVersionResponse = Read-Host "Publish dev version $targetVersion? [y/N]"
    if ($confirmVersionResponse -notin @("y", "Y", "yes", "YES")) {
        Write-Log -Level "WARN" -Message "User cancelled at version confirmation step."
        exit $ExitCodes.UserCancelled
    }
}

Write-Log "Release notes are optional for the dev lane; skipping release-notes gate."

if ($DryRun) {
    Write-Log "Dry run complete. No commit or push performed."
    exit $ExitCodes.Success
}

if ($ApprovePublish) {
    Write-Log "Final approval override enabled via -ApprovePublish"
}
elseif ($NonInteractive) {
    Exit-WithError -Code $ExitCodes.UserCancelled -Message "NonInteractive mode requires -ApprovePublish for non-dry-run publishes."
}
else {
    $approval = Read-Host "Proceed with dev commit and push? [y/N]"
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

if ($updatedProjectXml -match '<InformationalVersion>\s*[^<]*\s*</InformationalVersion>') {
    $updatedProjectXml = [regex]::Replace(
        $updatedProjectXml,
        '<InformationalVersion>\s*[^<]*\s*</InformationalVersion>',
        "<InformationalVersion>$targetVersion</InformationalVersion>",
        1
    )
}
elseif ($updatedProjectXml -match '<Version>\s*[^<\s]+\s*</Version>') {
    $updatedProjectXml = [regex]::Replace(
        $updatedProjectXml,
        '(<Version>\s*[^<\s]+\s*</Version>)',
        "$1`r`n    <InformationalVersion>$targetVersion</InformationalVersion>",
        1
    )
}

if ($updatedProjectXml -ne $projectXmlLatest) {
    Set-Content -Path $versionProjectFullPath -Value $updatedProjectXml
    Write-Log "Updated version source in $VersionProjectPath to $targetVersion"
}

$allowedChanges = @(
    ($VersionProjectPath -replace '\\', '/')
)
$workingChanges = Get-ChangedPaths | ForEach-Object { $_ -replace '\\', '/' }
$unexpected = $workingChanges | Where-Object { $_ -notin $allowedChanges }
if ($unexpected.Count -gt 0) {
    $unexpectedJoined = $unexpected -join ", "
    Exit-WithError -Code $ExitCodes.CleanGateFailed -Message "Unexpected changed files before commit: $unexpectedJoined"
}

$null = Invoke-Git -GitArgs @("add", $VersionProjectPath)
$null = Invoke-Git -GitArgs @("commit", "-m", "dev: $targetVersion")
$null = Invoke-Git -GitArgs @("push", "origin", "HEAD:$targetBranch")

Write-Log "Dev publish complete. Pushed dev commit to origin/$targetBranch"
Write-Log "Log file: $logPath"
exit $ExitCodes.Success
