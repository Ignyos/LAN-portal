function Invoke-ReleaseGit {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$GitArgs,
        [switch]$AllowFailure,
        [int]$FailureCode = 60,
        [scriptblock]$OnFailure
    )

    $output = & git @GitArgs 2>&1
    $exitCode = $LASTEXITCODE

    if (-not $AllowFailure -and $exitCode -ne 0) {
        $message = "git $($GitArgs -join ' ') failed: $($output | Out-String).TrimEnd()"
        if ($null -ne $OnFailure) {
            & $OnFailure $FailureCode $message
        }
        throw $message
    }

    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output = ($output | Out-String).TrimEnd()
    }
}

function Test-ReleaseSemVer {
    param([string]$Value)

    return $Value -match '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:\.(0|[1-9]\d*))?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$'
}

function Get-NextReleasePatchVersion {
    param([string]$CurrentVersion)

    if ($CurrentVersion -notmatch '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)') {
        throw "Current version '$CurrentVersion' is not valid for patch increment."
    }

    $major = [int]$Matches.major
    $minor = [int]$Matches.minor
    $patch = [int]$Matches.patch

    return "$major.$minor.$($patch + 1).0"
}

function Get-DevSuggestedVersion {
    param([string]$CurrentVersion)

    if ($CurrentVersion -notmatch '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)') {
        throw "Current version '$CurrentVersion' is not valid for dev version suggestion."
    }

    $major = [int]$Matches.major
    $minor = [int]$Matches.minor
    $patch = [int]$Matches.patch
    $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmm")

    return "$major.$minor.$patch.$stamp"
}

function Resolve-ReleasePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    return (Join-Path $RepoRoot $RelativePath)
}

function Get-ReleaseChangedPaths {
    param([string]$GitStatusOutput)

    if ([string]::IsNullOrWhiteSpace($GitStatusOutput)) {
        return @()
    }

    $paths = @()
    foreach ($line in ($GitStatusOutput -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line.Length -ge 4) {
            $paths += $line.Substring(3).Trim()
        }
    }

    return $paths
}
