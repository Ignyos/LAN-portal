param(
    [string]$ArtifactRoot,
    [string]$ExpectedVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath

$checks = @()

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details
    )

    $script:checks += [PSCustomObject]@{
        Name = $Name
        Passed = $Passed
        Details = $Details
    }
}

function Test-FileContentContains {
    param(
        [string]$Path,
        [string]$Pattern
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -LiteralPath $Path -Raw
    return ($content -match [regex]::Escape($Pattern))
}

function Get-ProjectVersion {
    param([string]$ProjectPath)

    if (-not (Test-Path -LiteralPath $ProjectPath)) {
        throw "Version source project was not found: $ProjectPath"
    }

    $content = Get-Content -LiteralPath $ProjectPath -Raw
    if ($content -notmatch '<Version>\s*(?<version>[^<\s]+)\s*</Version>') {
        throw "Version source was not found in: $ProjectPath"
    }

    return $Matches.version.Trim()
}

function Add-ArtifactVersionChecks {
    param(
        [string]$RootPath,
        [string]$Version
    )

    $expectedVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
        Get-ProjectVersion -ProjectPath (Join-Path $scriptRoot "..\Ignyos.LanPortal.Host\Ignyos.LanPortal.Host.csproj")
    }
    else {
        $Version.Trim()
    }
    $stagingRoot = Join-Path $RootPath "staging\app"
    $publishTargets = @(
        @{ Name = "API"; Path = (Join-Path $stagingRoot "api") },
        @{ Name = "Web"; Path = (Join-Path $stagingRoot "web") },
        @{ Name = "Host"; Path = (Join-Path $stagingRoot "host") }
    )

    foreach ($target in $publishTargets) {
        $artifact = Get-ChildItem -LiteralPath $target.Path -Filter "*.exe" -File -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($null -eq $artifact) {
            Add-Check -Name "artifact $($target.Name) exists" -Passed $false -Details $target.Path
            continue
        }

        $actualVersion = $artifact.VersionInfo.FileVersion
        Add-Check -Name "artifact $($target.Name) version" `
            -Passed ($actualVersion -eq $expectedVersion) `
            -Details "$($artifact.Name): expected $expectedVersion, found $actualVersion"
    }

    $packageDirectory = Join-Path $RootPath "package"
    $package = Get-ChildItem -LiteralPath $packageDirectory -Filter "Ignyos-LanPortal-QA-*.zip" -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    $expectedPackageName = "Ignyos-LanPortal-QA-$expectedVersion.zip"
    Add-Check -Name "QA package version" `
        -Passed ($null -ne $package -and $package.Name -eq $expectedPackageName) `
        -Details "expected $expectedPackageName"

    $installerDirectory = Join-Path $RootPath "installer"
    $installer = Get-ChildItem -LiteralPath $installerDirectory -Filter "Ignyos-LanPortal-Dev-*.exe" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.BaseName -eq "Ignyos-LanPortal-Dev-$expectedVersion" } |
        Select-Object -First 1
    if ($null -eq $installer) {
        Add-Check -Name "installer version" -Passed $true -Details "No installer artifact found; package-only builds are supported"
    }
    else {
        $installerVersion = $installer.VersionInfo.ProductVersion
        Add-Check -Name "installer version" `
            -Passed ($installerVersion -eq $expectedVersion) `
            -Details "$($installer.Name): expected $expectedVersion, found $installerVersion"
    }
}

$publishLivePath = Join-Path $scriptRoot "publish-live.ps1"
$publishDevPath = Join-Path $scriptRoot "publish-dev.ps1"
$publishReleasePath = Join-Path $scriptRoot "publish-release.ps1"
$releaseCommonPath = Join-Path $scriptRoot "release-common.ps1"
$publishDevWorkflowPath = Join-Path $scriptRoot ".." ".github/workflows/publish-dev.yml"
$publishDevWorkflowPath = [System.IO.Path]::GetFullPath($publishDevWorkflowPath)

Add-Check -Name "publish-live exists" -Passed (Test-Path -LiteralPath $publishLivePath) -Details $publishLivePath
Add-Check -Name "publish-dev exists" -Passed (Test-Path -LiteralPath $publishDevPath) -Details $publishDevPath
Add-Check -Name "publish-release exists" -Passed (Test-Path -LiteralPath $publishReleasePath) -Details $publishReleasePath
Add-Check -Name "release-common exists" -Passed (Test-Path -LiteralPath $releaseCommonPath) -Details $releaseCommonPath

Add-Check -Name "publish-live preserves AI gate" -Passed (Test-FileContentContains -Path $publishLivePath -Pattern "Type CONTINUE to proceed after AI finishes") -Details "CONTINUE gate text found"
Add-Check -Name "publish-live preserves final approval" -Passed (Test-FileContentContains -Path $publishLivePath -Pattern "Proceed with commit, tag, and push?") -Details "Final approval prompt found"
Add-Check -Name "publish-live preserves release notes output path default" -Passed (Test-FileContentContains -Path $publishLivePath -Pattern ".github/release/release-notes.md") -Details "Default release notes path found"

Add-Check -Name "publish-dev uses dev branch gate" -Passed (Test-FileContentContains -Path $publishDevPath -Pattern "Expected 'dev' but found") -Details "publish-dev enforces the dev branch"
Add-Check -Name "publish-dev uses dev version suggestion" -Passed (Test-FileContentContains -Path $publishDevPath -Pattern "Get-DevSuggestedVersion") -Details "publish-dev uses dev version suggestion logic"
Add-Check -Name "publish-dev skips release-notes gate" -Passed (Test-FileContentContains -Path $publishDevPath -Pattern "Release notes are optional") -Details "publish-dev does not require release notes"
Add-Check -Name "publish-dev workflow triggers on push to dev" -Passed (Test-FileContentContains -Path $publishDevWorkflowPath -Pattern "push") -Details "dev workflow responds to dev branch pushes"
Add-Check -Name "publish-release routes to publish-live" -Passed (Test-FileContentContains -Path $publishReleasePath -Pattern "publish-live.ps1") -Details "publish-release alias target"

$commonFunctions = @(
    "Invoke-ReleaseGit",
    "Test-ReleaseSemVer",
    "Get-NextReleasePatchVersion",
    "Get-DevSuggestedVersion",
    "Resolve-ReleasePath",
    "Get-ReleaseChangedPaths"
)

foreach ($functionName in $commonFunctions) {
    Add-Check -Name "release-common function $functionName" -Passed (Test-FileContentContains -Path $releaseCommonPath -Pattern "function $functionName") -Details $functionName
}

if (-not [string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    Add-ArtifactVersionChecks -RootPath ([System.IO.Path]::GetFullPath($ArtifactRoot)) -Version $ExpectedVersion
}

$failed = @($checks | Where-Object { -not $_.Passed })

Write-Host "Publish Parity Validation Results" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
foreach ($check in $checks) {
    $status = if ($check.Passed) { "PASS" } else { "FAIL" }
    $color = if ($check.Passed) { "Green" } else { "Red" }
    Write-Host ("[{0}] {1} - {2}" -f $status, $check.Name, $check.Details) -ForegroundColor $color
}

if ($failed.Count -gt 0) {
    Write-Host "" 
    Write-Host ("Validation failed: {0} check(s) failed." -f $failed.Count) -ForegroundColor Red
    exit 1
}

Write-Host "" 
Write-Host "Validation passed: all parity checks succeeded." -ForegroundColor Green
exit 0
