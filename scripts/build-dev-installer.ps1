param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version,
    [string]$DevUpdateBaseUrl = "https://ignyos.github.io/LAN-Portal-dev",
    [string]$DevUpdateChannel = "test"
)

$ErrorActionPreference = "Stop"

$versionStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmm")

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionProjectPath = Join-Path $repoRoot "Ignyos.LanPortal.Host\Ignyos.LanPortal.Host.csproj"
$defaultVersionCore = "0.1.0"
if (Test-Path $versionProjectPath) {
    $projectXml = Get-Content -Path $versionProjectPath -Raw
    if ($projectXml -match '<Version>\s*(?<version>[^<\s]+)\s*</Version>') {
        $candidate = $Matches.version.Trim()
        if ($candidate -match '^(?<core>\d+\.\d+\.\d+)') {
            $defaultVersionCore = $Matches.core
        }
    }
}

$defaultVersion = "$defaultVersionCore-dev.$versionStamp"
$artifactsRoot = Join-Path $repoRoot "artifacts\dev-installer"
$stagingRoot = Join-Path $artifactsRoot "staging"
$appRoot = Join-Path $stagingRoot "app"
$apiOut = Join-Path $appRoot "api"
$webOut = Join-Path $appRoot "web"
$hostOut = Join-Path $appRoot "host"
$installerOut = Join-Path $artifactsRoot "installer"
$packageOut = Join-Path $artifactsRoot "package"

function Test-VersionAlreadyExists {
    param(
        [string]$CandidateVersion,
        [string]$InstallerDirectory
    )

    $installerPath = Join-Path $InstallerDirectory "Ignyos-LanPortal-Dev-$CandidateVersion.exe"

    return (Test-Path $installerPath)
}

function Get-NextPatchVersion {
    param([string]$CurrentVersion)

    if ([string]::IsNullOrWhiteSpace($CurrentVersion)) {
        return $defaultVersion
    }

    $parts = $CurrentVersion -split '-', 2
    $core = $parts[0]
    $suffix = if ($parts.Length -gt 1) { "-$($parts[1])" } else { "" }
    $coreSegments = $core -split '\.'

    if ($coreSegments.Length -lt 3) {
        return $defaultVersion
    }

    $major = 0
    $minor = 0
    $patch = 0

    if (-not ([int]::TryParse($coreSegments[0], [ref]$major))) {
        return $defaultVersion
    }

    if (-not ([int]::TryParse($coreSegments[1], [ref]$minor))) {
        return $defaultVersion
    }

    if (-not ([int]::TryParse($coreSegments[2], [ref]$patch))) {
        return $defaultVersion
    }

    return "$major.$minor.$($patch + 1)$suffix"
}

function Get-SuggestedVersion {
    param(
        [string]$InstallerDirectory,
        [string]$FallbackVersion
    )

    if (-not (Test-Path $InstallerDirectory)) {
        return $FallbackVersion
    }

    $versions = @()
    $files = @()

    if (Test-Path $InstallerDirectory) {
        $files += Get-ChildItem -Path $InstallerDirectory -Filter "Ignyos-LanPortal-Dev-*.exe" -File -ErrorAction SilentlyContinue
    }

    foreach ($file in $files) {
        $candidate = $null
        if ($file.Name -match '^Ignyos-LanPortal-Dev-(?<version>.+)\.exe$') {
            $candidate = $Matches['version']
        }

        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $parts = $candidate -split '-', 2
            $core = $parts[0]
            $coreSegments = $core -split '\.'

            if ($coreSegments.Length -ge 3) {
                $major = 0
                $minor = 0
                $patch = 0

                if ([int]::TryParse($coreSegments[0], [ref]$major) -and
                    [int]::TryParse($coreSegments[1], [ref]$minor) -and
                    [int]::TryParse($coreSegments[2], [ref]$patch)) {
                    $versions += [PSCustomObject]@{
                        Raw = $candidate
                        Major = $major
                        Minor = $minor
                        Patch = $patch
                    }
                }
            }
        }
    }

    if ($versions.Count -eq 0) {
        return $FallbackVersion
    }

    $latest = $versions |
        Sort-Object -Property Major, Minor, Patch -Descending |
        Select-Object -First 1

    return Get-NextPatchVersion -CurrentVersion $latest.Raw
}

function Get-UniqueVersionFromUser {
    param(
        [string]$InitialVersion,
        [string]$InstallerDirectory
    )

    $candidateVersion = $InitialVersion

    while ($true) {
        if (-not (Test-VersionAlreadyExists -CandidateVersion $candidateVersion -InstallerDirectory $InstallerDirectory)) {
            return $candidateVersion
        }

        $enteredVersion = Read-Host "Version '$candidateVersion' already exists. Enter a different installer version"
        if ([string]::IsNullOrWhiteSpace($enteredVersion)) {
            continue
        }

        $candidateVersion = $enteredVersion.Trim()
    }
}

function Resolve-InnoCompiler {
    $command = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidatePaths = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    )

    foreach ($candidate in $candidatePaths) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Set-ApiUpdateChannelConfiguration {
    param(
        [string]$ApiOutputDirectory,
        [string]$BaseUrl,
        [string]$Channel
    )

    function Set-JsonPropertyValue {
        param(
            [Parameter(Mandatory = $true)]
            [psobject]$Target,
            [Parameter(Mandatory = $true)]
            [string]$PropertyName,
            [Parameter(Mandatory = $true)]
            $PropertyValue
        )

        $property = $Target.PSObject.Properties[$PropertyName]
        if ($null -eq $property) {
            $Target | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $PropertyValue
        }
        else {
            $Target.$PropertyName = $PropertyValue
        }
    }

    $configFiles = @(
        "appsettings.json",
        "appsettings.Production.json"
    )

    foreach ($configFile in $configFiles) {
        $configPath = Join-Path $ApiOutputDirectory $configFile
        if (-not (Test-Path $configPath)) {
            continue
        }

        $config = Get-Content -Path $configPath -Raw | ConvertFrom-Json

        if ($null -eq $config.UpdateChannel) {
            $config | Add-Member -NotePropertyName UpdateChannel -NotePropertyValue ([PSCustomObject]@{})
        }

        $updateChannel = $config.UpdateChannel
        Set-JsonPropertyValue -Target $updateChannel -PropertyName "BaseUrl" -PropertyValue $BaseUrl
        Set-JsonPropertyValue -Target $updateChannel -PropertyName "Channel" -PropertyValue $Channel

        if ($null -eq $updateChannel.PSObject.Properties["ProductionManifestPath"] -or
            [string]::IsNullOrWhiteSpace([string]$updateChannel.ProductionManifestPath)) {
            Set-JsonPropertyValue -Target $updateChannel -PropertyName "ProductionManifestPath" -PropertyValue "/updates/manifest.json"
        }

        if ($null -eq $updateChannel.PSObject.Properties["TestManifestPath"] -or
            [string]::IsNullOrWhiteSpace([string]$updateChannel.TestManifestPath)) {
            Set-JsonPropertyValue -Target $updateChannel -PropertyName "TestManifestPath" -PropertyValue "/updates/manifest-test.json"
        }

        if ($null -eq $updateChannel.PSObject.Properties["PollIntervalMinutes"]) {
            Set-JsonPropertyValue -Target $updateChannel -PropertyName "PollIntervalMinutes" -PropertyValue 60
        }

        $config | ConvertTo-Json -Depth 20 | Set-Content -Path $configPath -Encoding utf8
    }
}

if (-not (Test-Path $packageOut)) {
    New-Item -ItemType Directory -Force -Path $packageOut | Out-Null
}

Get-ChildItem -Path $packageOut -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

if ([string]::IsNullOrWhiteSpace($Version)) {
    $suggestedVersion = Get-SuggestedVersion -InstallerDirectory $installerOut -FallbackVersion $defaultVersion
    $enteredVersion = Read-Host "Enter installer version [$suggestedVersion]"
    $candidateVersion = if ([string]::IsNullOrWhiteSpace($enteredVersion)) { $suggestedVersion } else { $enteredVersion.Trim() }
    $Version = Get-UniqueVersionFromUser -InitialVersion $candidateVersion -InstallerDirectory $installerOut
}
elseif (Test-VersionAlreadyExists -CandidateVersion $Version -InstallerDirectory $installerOut) {
    if ([Environment]::UserInteractive) {
        $Version = Get-UniqueVersionFromUser -InitialVersion $Version -InstallerDirectory $installerOut
    }
    else {
        throw "Version '$Version' already exists (installer). Please provide a new version."
    }
}

if (Test-Path $stagingRoot) {
    Remove-Item -Recurse -Force $stagingRoot
}

New-Item -ItemType Directory -Force -Path $apiOut | Out-Null
New-Item -ItemType Directory -Force -Path $webOut | Out-Null
New-Item -ItemType Directory -Force -Path $hostOut | Out-Null
New-Item -ItemType Directory -Force -Path $installerOut | Out-Null

Write-Host "Publishing API..."
dotnet publish (Join-Path $repoRoot "Ignyos.LanPortal.Api\Ignyos.LanPortal.Api.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $apiOut

Set-ApiUpdateChannelConfiguration -ApiOutputDirectory $apiOut -BaseUrl $DevUpdateBaseUrl -Channel $DevUpdateChannel

Write-Host "Publishing Web..."
dotnet publish (Join-Path $repoRoot "Ignyos.LanPortal.Web\Ignyos.LanPortal.Web.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $webOut

Write-Host "Publishing Host..."
dotnet publish (Join-Path $repoRoot "Ignyos.LanPortal.Host\Ignyos.LanPortal.Host.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:InformationalVersion=$Version `
    -o $hostOut

Set-Content -Path (Join-Path $hostOut "installer-flavor.txt") -Value "dev"

Copy-Item (Join-Path $repoRoot "installer\templates\Launch-LanPortal.ps1") (Join-Path $appRoot "Launch-LanPortal.ps1")
Copy-Item (Join-Path $repoRoot "installer\templates\Open-LanPortal-Admin.cmd") (Join-Path $appRoot "Open-LanPortal-Admin.cmd")
Copy-Item (Join-Path $repoRoot "installer\templates\README-QA.txt") (Join-Path $appRoot "README-QA.txt")

$zipName = "Ignyos-LanPortal-QA-$Version.zip"
$zipPath = Join-Path $packageOut $zipName

Write-Host "Creating QA zip package: $zipPath"
Compress-Archive -Path (Join-Path $appRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
"$($hash.Hash)  $zipName" | Set-Content -Path (Join-Path $packageOut "$zipName.sha256")

$isccPath = Resolve-InnoCompiler
if ($null -ne $isccPath) {
    Write-Host "Building Inno Setup installer..."
    & $isccPath (Join-Path $repoRoot "installer\Ignyos.LanPortal.Dev.iss") "/DMyAppVersion=$Version" "/DInstallerFlavor=dev" "/DStagingRoot=$stagingRoot" "/DInstallerOutRoot=$installerOut"
}
else {
    Write-Warning "Inno Setup compiler (iscc.exe) not found. Zip package was created; skipping .exe installer build."
}

if (Test-Path $packageOut) {
    Get-ChildItem -Path $packageOut -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
}

Write-Host "Done. Artifacts root: $artifactsRoot"
exit 0
