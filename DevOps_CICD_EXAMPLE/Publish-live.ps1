[CmdletBinding()]
param(
  [switch]$NoPush,
  [switch]$WhatIfMode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Args,
    [switch]$CaptureOutput,
    [switch]$AllowFailure
  )

  $commandText = "git " + ($Args -join " ")

  if ($WhatIfMode) {
    Write-Host "[WhatIf] $commandText" -ForegroundColor Yellow
    if ($CaptureOutput) {
      return ""
    }
    return
  }

  if ($CaptureOutput) {
    $output = & git @Args 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
      if (-not $AllowFailure) {
        throw "Git command failed ($commandText): $($output -join "`n")"
      }
      # When AllowFailure is set and git fails, return empty string
      return ""
    }
    return (($output -join "`n").Trim())
  }

  & git @Args
  $exitCode = $LASTEXITCODE
  if ($exitCode -ne 0 -and -not $AllowFailure) {
    throw "Git command failed ($commandText)"
  }
}

function Confirm-YesNo {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Prompt,
    [switch]$DefaultNo
  )

  $suffix = if ($DefaultNo) { " [y/N]" } else { " [Y/n]" }
  $value = Read-Host ($Prompt + $suffix)
  if ([string]::IsNullOrWhiteSpace($value)) {
    return -not $DefaultNo
  }

  return $value -match "^(y|yes)$"
}

function Update-AssetVersionReferences {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [switch]$WhatIfMode
  )

  $indexFiles = Get-ChildItem -Path $RootPath -Recurse -File -Filter "index.html"
  if (-not $indexFiles) {
    return
  }

  $pattern = '(?<attr>\b(?:src|href))="(?<path>[^"]+\.(?:css|js))(?:\?v=[^"]*)?"'
  $updatedFiles = @()

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
      $updatedFiles += $file.FullName
      if ($WhatIfMode) {
        continue
      }
      Set-Content -LiteralPath $file.FullName -Value $updatedContent -Encoding utf8
    }
  }

  if ($updatedFiles.Count -gt 0) {
    if ($WhatIfMode) {
      Write-Host "[WhatIf] Would update asset versions in:" -ForegroundColor Yellow
    }
    else {
      Write-Host "Updated asset versions in:" -ForegroundColor Green
    }

    foreach ($path in $updatedFiles) {
      $relativePath = [IO.Path]::GetRelativePath($RootPath, $path)
      Write-Host "  - $relativePath"
    }
  }
}

function Invoke-BuildScript {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,
    [switch]$WhatIfMode
  )

  $buildScriptPath = Join-Path $RootPath "Build.ps1"
  if (-not (Test-Path -LiteralPath $buildScriptPath)) {
    throw "Required file not found: $buildScriptPath"
  }

  Write-Host "Running build script..." -ForegroundColor Cyan
  & $buildScriptPath -WhatIfMode:$WhatIfMode

  if ($LASTEXITCODE -ne 0) {
    throw "Build script failed: $buildScriptPath"
  }
}

$scriptDir = Split-Path -Parent $PSCommandPath
Push-Location $scriptDir
try {
  $repoRoot = Invoke-Git -Args @("rev-parse", "--show-toplevel") -CaptureOutput
  if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    throw "Unable to determine repository root."
  }

  Set-Location $repoRoot

  $branchName = Invoke-Git -Args @("rev-parse", "--abbrev-ref", "HEAD") -CaptureOutput
  $isTestRun = $branchName -ne "main"
  if ($branchName -eq "HEAD") {
    $isTestRun = $true
    Write-Warning "Detached HEAD detected. Running in test mode."
  }

  if ($isTestRun) {
    Write-Warning "Branch '$branchName' is not 'main'. This run is test-only; commit, tag, and push are disabled."
  }

  $releaseNotesPath = Join-Path $repoRoot "RELEASE_NOTES.md"
  if (-not (Test-Path -LiteralPath $releaseNotesPath)) {
    if ($WhatIfMode) {
      Write-Host "[WhatIf] Would create missing release notes file: $releaseNotesPath" -ForegroundColor Yellow
    }
    else {
      Set-Content -LiteralPath $releaseNotesPath -Value "" -Encoding UTF8
      Write-Host "Created missing release notes file: RELEASE_NOTES.md" -ForegroundColor Yellow
    }
  }

  $releaseNotesStylePath = Join-Path $repoRoot "RELEASE_NOTES_STYLE.md"
  if (-not (Test-Path -LiteralPath $releaseNotesStylePath)) {
    throw "Required file not found: $releaseNotesStylePath"
  }

  $statusBefore = Invoke-Git -Args @("status", "--porcelain") -CaptureOutput
  if (-not [string]::IsNullOrWhiteSpace($statusBefore)) {
    Write-Warning "Working tree is not clean."
    if (-not (Confirm-YesNo -Prompt "Continue release anyway?" -DefaultNo)) {
      throw "Release cancelled by user."
    }
  }

  $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd-HH-mm")
  $tagName = $timestamp

  Update-AssetVersionReferences -RootPath $repoRoot -Version $timestamp -WhatIfMode:$WhatIfMode
  Invoke-BuildScript -RootPath $repoRoot -WhatIfMode:$WhatIfMode

  $releaseDir = Join-Path $repoRoot "release"
  if (-not (Test-Path -LiteralPath $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
  }
  $diffFileName = "rel-$timestamp.txt"
  $diffFilePath = Join-Path $releaseDir $diffFileName

  $lastTag = Invoke-Git -Args @("describe", "--tags", "--abbrev=0") -CaptureOutput -AllowFailure
  $isFirstRelease = [string]::IsNullOrWhiteSpace($lastTag)
  $diffBody = ""
  $diffSource = ""

  if ($isFirstRelease) {
    $diffSource = "initial repository state to HEAD"
    $diffBody = Invoke-Git -Args @("diff", "--root", "HEAD") -CaptureOutput
  }
  else {
    $diffSource = "$lastTag..HEAD"
    $diffBody = Invoke-Git -Args @("diff", "$lastTag..HEAD") -CaptureOutput
  }

  $diffHeader = @(
    "Release timestamp: $timestamp"
    "Diff source: $diffSource"
    "Generated at: $([DateTime]::Now.ToString("u"))"
    ""
  ) -join "`r`n"

  $diffContent = $diffHeader + $diffBody

  if ($WhatIfMode) {
    Write-Host "[WhatIf] Would write diff file: $diffFilePath" -ForegroundColor Yellow
  }
  else {
    Set-Content -LiteralPath $diffFilePath -Value $diffContent -Encoding UTF8
    Write-Host "Saved diff file: $diffFileName" -ForegroundColor Green
  }

  $aiPrompt = @"
You are updating release notes for this repository.

Use these files:
- Diff input: $diffFileName
- Release notes target: RELEASE_NOTES.md
- Style guide: RELEASE_NOTES_STYLE.md

Instructions:
1) Read RELEASE_NOTES_STYLE.md and follow it exactly.
2) Read $diffFileName completely.
3) Update RELEASE_NOTES.md to reflect only the changes present in the diff.
4) Keep language factual, concise, and user-facing.
5) Do not invent features or fixes not present in the diff.
6) Preserve existing formatting conventions in RELEASE_NOTES.md.
7) Do not preserve previous RELEASE_NOTES.md content, only new content is preserved
8) Do not output to the console, only update the RELEASE_NOTES.md file
"@

  if ($WhatIfMode) {
    Write-Host "[WhatIf] Would copy AI prompt to clipboard." -ForegroundColor Yellow
  }
  else {
    Set-Clipboard -Value $aiPrompt
    Write-Host "Release-notes prompt copied to clipboard." -ForegroundColor Green
  }

  Write-Host ""
  Write-Host "Paste the prompt into AI, apply RELEASE_NOTES.md updates, then continue." -ForegroundColor Cyan
  Read-Host "Press Enter when release notes are ready"

  if ($isTestRun) {
    Write-Host ""
    Write-Host "Test run complete on branch '$branchName'. No commit, tag, or push was performed." -ForegroundColor Yellow
    return
  }

  $statusAfterNotes = Invoke-Git -Args @("status", "--porcelain") -CaptureOutput
  if ([string]::IsNullOrWhiteSpace($statusAfterNotes)) {
    Write-Warning "No file changes detected after release-notes step."
    if ($isFirstRelease) {
      Write-Host "No previous release tag detected; continuing with first-release tag/push." -ForegroundColor Cyan
    }
    else {
      if (-not (Confirm-YesNo -Prompt "Continue with tag/push anyway?" -DefaultNo)) {
        throw "Release cancelled by user."
      }
    }
  }
  else {
    Invoke-Git -Args @("add", "-A")
    Invoke-Git -Args @("commit", "-m", "release: $timestamp") -AllowFailure

    $stillDirty = Invoke-Git -Args @("status", "--porcelain") -CaptureOutput
    if (-not [string]::IsNullOrWhiteSpace($stillDirty)) {
      throw "Unable to create a clean release commit. Resolve git issues and rerun."
    }
  }

  $localTagMatch = Invoke-Git -Args @("tag", "-l", $tagName) -CaptureOutput
  if (-not [string]::IsNullOrWhiteSpace($localTagMatch)) {
    throw "Tag already exists locally: $tagName"
  }

  $remoteTagMatch = Invoke-Git -Args @("ls-remote", "--tags", "origin", "refs/tags/$tagName") -CaptureOutput -AllowFailure
  if (-not [string]::IsNullOrWhiteSpace($remoteTagMatch)) {
    throw "Tag already exists on origin: $tagName"
  }

  Invoke-Git -Args @("tag", "-a", $tagName, "-m", "Release $tagName")

  if ($NoPush) {
    Write-Host "NoPush enabled; skipping push." -ForegroundColor Yellow
  }
  else {
    Invoke-Git -Args @("push", "origin", $branchName)
    Invoke-Git -Args @("push", "origin", $tagName)
  }

  Write-Host ""
  Write-Host "Release workflow complete. Tag: $tagName" -ForegroundColor Green
}
finally {
  Pop-Location
}

