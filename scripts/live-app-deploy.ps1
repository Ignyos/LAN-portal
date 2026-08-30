param(
    [string]$Version,
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/release-artifacts.yml'
$versionProjectPath = Join-Path $repoRoot 'Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj'

if (-not (Test-Path $workflowPath)) {
    throw "Workflow file not found: $workflowPath"
}

if (-not (Test-Path $versionProjectPath)) {
    throw "Version source file not found: $versionProjectPath"
}

function Get-CurrentVersion {
    param([string]$ProjectPath)

    $projectXml = Get-Content -Path $ProjectPath -Raw
    $versionMatch = [regex]::Match($projectXml, '<Version>\s*(?<version>[^<\s]+)\s*</Version>')
    if (-not $versionMatch.Success) {
        throw "Could not resolve the application version from $ProjectPath"
    }

    return $versionMatch.Groups['version'].Value.Trim()
}

function Get-ProdSuggestedVersion {
    param([string]$CurrentVersion)

    $match = [regex]::Match($CurrentVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.(?<build>\d+))?$')
    if (-not $match.Success) {
        throw "Current version '$CurrentVersion' is not in expected numeric format (major.minor.patch[.build])."
    }

    $major = [int]$match.Groups['major'].Value
    $minor = [int]$match.Groups['minor'].Value
    $patch = [int]$match.Groups['patch'].Value

    return "$major.$minor.$($patch + 1).0"
}

function Set-LocalVersion {
    param(
        [string]$ProjectPath,
        [string]$TargetVersion
    )

    $projectXml = Get-Content -Path $ProjectPath -Raw
    $updatedProjectXml = [regex]::Replace($projectXml, '<Version>\s*[^<\s]+\s*</Version>', "<Version>$TargetVersion</Version>", 1)
    if ($updatedProjectXml -match '<InformationalVersion>\s*[^<]*\s*</InformationalVersion>') {
        $updatedProjectXml = [regex]::Replace($updatedProjectXml, '<InformationalVersion>\s*[^<]*\s*</InformationalVersion>', "<InformationalVersion>$TargetVersion</InformationalVersion>", 1)
    }
    elseif ($updatedProjectXml -ne $projectXml) {
        $updatedProjectXml = [regex]::Replace($updatedProjectXml, '(<Version>\s*[^<\s]+\s*</Version>)', "$1`r`n    <InformationalVersion>$TargetVersion</InformationalVersion>", 1)
    }

    if ($updatedProjectXml -ne $projectXml) {
        Set-Content -Path $ProjectPath -Value $updatedProjectXml
        Write-Host "Updated local version source to $TargetVersion."
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $currentVersion = Get-CurrentVersion -ProjectPath $versionProjectPath
    $suggestedVersion = Get-ProdSuggestedVersion -CurrentVersion $currentVersion
    $enteredVersion = Read-Host "Enter live publish version (current $currentVersion) [$suggestedVersion]: "
    $Version = if ([string]::IsNullOrWhiteSpace($enteredVersion)) { $suggestedVersion } else { $enteredVersion.Trim() }
}

Set-LocalVersion -ProjectPath $versionProjectPath -TargetVersion $Version

$branchName = git rev-parse --abbrev-ref HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branchName)) {
    throw "Unable to determine the current Git branch."
}

$repoSlug = $env:GITHUB_REPOSITORY
if ([string]::IsNullOrWhiteSpace($repoSlug)) {
    $originUrl = git config --get remote.origin.url 2>$null
    if ($LASTEXITCODE -eq 0 -and $originUrl) {
        if ($originUrl -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$') {
            $repoSlug = "$($Matches.owner)/$($Matches.repo)"
        }
    }
}

$ghCommand = $null
$ghCandidates = @(
    (Get-Command gh -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "$env:LOCALAPPDATA\Programs\GitHub CLI\gh.exe",
    "$env:ProgramFiles\GitHub CLI\gh.exe",
    "${env:ProgramFiles(x86)}\GitHub CLI\gh.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
$ghCandidates = @($ghCandidates)

if ($ghCandidates.Count -gt 0) {
    $ghCommand = $ghCandidates[0]
}

if ($ghCommand) {
    Write-Host "Triggering GitHub Actions workflow for the live publish lane..."
    $ghArgs = @('workflow', 'run', '.github/workflows/release-artifacts.yml', '--repo', $repoSlug, '--ref', $branchName, '-f', "version=$Version")
    & $ghCommand @ghArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Remote workflow dispatch started successfully."
        exit 0
    }

    Write-Host "GitHub CLI workflow dispatch did not complete successfully. Falling back to manual guidance."
}

Write-Host "Live-App-Deploy is configured to trigger the remote GitHub workflow for the live publish lane."
Write-Host "Current branch: $branchName"
Write-Host "Workflow: $workflowPath"
Write-Host ""
Write-Host "If GitHub CLI is available and authenticated, the script will trigger the workflow automatically."
Write-Host "Otherwise, please run the 'Live-Deploy' workflow from the GitHub Actions UI for this repository."
Write-Host "Set version='$Version' in the workflow inputs before starting the run."
