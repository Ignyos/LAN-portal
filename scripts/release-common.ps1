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

    $match = [regex]::Match($Value, '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:\.(?<revision>0|[1-9]\d*))?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')
    if (-not $match.Success) {
        return $false
    }

    $numericComponents = @(
        $match.Groups['major'].Value,
        $match.Groups['minor'].Value,
        $match.Groups['patch'].Value
    )

    if ($match.Groups['revision'].Success) {
        $numericComponents += $match.Groups['revision'].Value
    }

    foreach ($component in $numericComponents) {
        $parsedComponent = 0
        if (-not [int]::TryParse($component, [ref]$parsedComponent)) {
            return $false
        }
    }

    return $true
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

    $match = [regex]::Match($CurrentVersion, '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:\.(?<build>0|[1-9]\d*))?$')
    if (-not $match.Success) {
        throw "Current version '$CurrentVersion' is not valid for dev version suggestion."
    }

    $major = [int]$match.Groups['major'].Value
    $minor = [int]$match.Groups['minor'].Value
    $patch = [int]$match.Groups['patch'].Value

    if ($match.Groups['build'].Success) {
        $build = [int]$match.Groups['build'].Value
        $build += 1
    }
    else {
        $build = 1
    }

    return "$major.$minor.$patch.$build"
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
