[CmdletBinding()]
param(
  [string]$CommitMessage = 'chore: dev publish',
  [switch]$SkipBuild,
  [switch]$NoPush,
  [string]$RequiredBranch = 'dev'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Args,
    [switch]$CaptureOutput,
    [switch]$AllowFailure
  )

  if ($CaptureOutput) {
    $output = & git @Args 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and -not $AllowFailure) {
      throw "Git command failed: git $($Args -join ' ')`n$($output -join "`n")"
    }
    return (($output -join "`n").Trim())
  }

  & git @Args
  $exitCode = $LASTEXITCODE
  if ($exitCode -ne 0 -and -not $AllowFailure) {
    throw "Git command failed: git $($Args -join ' ')"
  }
}

function Update-AssetVersionReferences {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,
    [Parameter(Mandatory = $true)]
    [string]$Version
  )

  $indexFiles = Get-ChildItem -Path $RootPath -Recurse -File -Filter 'index.html'
  if (-not $indexFiles) {
    return
  }

  $pattern = '(?<attr>\b(?:src|href))="(?<path>[^"]+\.(?:css|js))(?:\?v=[^"]*)?"'
  foreach ($file in $indexFiles) {
    $originalContent = Get-Content -LiteralPath $file.FullName -Raw
    $updatedContent = [regex]::Replace(
      $originalContent,
      $pattern,
      {
        param($match)
        $attr = $match.Groups['attr'].Value
        $path = $match.Groups['path'].Value
        return ('{0}="{1}?v={2}"' -f $attr, $path, $Version)
      }
    )

    if ($updatedContent -ne $originalContent) {
      Set-Content -LiteralPath $file.FullName -Value $updatedContent -Encoding utf8
    }
  }
}

function Update-ServiceWorkerCacheName {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,
    [Parameter(Mandatory = $true)]
    [string]$Version
  )

  $serviceWorkerPath = Join-Path $RootPath 'docs\service-worker.js'
  if (-not (Test-Path -LiteralPath $serviceWorkerPath)) {
    return
  }

  $content = Get-Content -LiteralPath $serviceWorkerPath -Raw
  $updated = [regex]::Replace(
    $content,
    "var CACHE_NAME = 'kap-app-v[^']+';",
    "var CACHE_NAME = 'kap-app-v$Version';"
  )

  if ($updated -ne $content) {
    Set-Content -LiteralPath $serviceWorkerPath -Value $updated -Encoding utf8
  }
}

$scriptDir = Split-Path -Parent $PSCommandPath
Push-Location $scriptDir
try {
  $branch = Invoke-Git -Args @('rev-parse', '--abbrev-ref', 'HEAD') -CaptureOutput
  if ($branch -ne $RequiredBranch) {
    throw "Current branch '$branch' does not match required branch '$RequiredBranch'."
  }

  if (-not $SkipBuild) {
    Write-Host 'Running build script...' -ForegroundColor Cyan
    & (Join-Path $scriptDir 'Build.ps1')
    if ($LASTEXITCODE -ne 0) {
      throw 'Build failed.'
    }
  }

  $versionStamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd-HH-mm')
  Update-AssetVersionReferences -RootPath $scriptDir -Version $versionStamp
  Update-ServiceWorkerCacheName -RootPath $scriptDir -Version $versionStamp

  Invoke-Git -Args @('add', '-A')
  Invoke-Git -Args @('commit', '-m', $CommitMessage)

  if ($NoPush) {
    Write-Host 'NoPush enabled; skipping push.' -ForegroundColor Yellow
    return
  }

  Invoke-Git -Args @('push', 'origin', $branch)
  Write-Host "Published changes to $branch. Dev deployment workflow should start automatically." -ForegroundColor Green
}
finally {
  Pop-Location
}

