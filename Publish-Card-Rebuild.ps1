<#
Publishes the completed Unity WebGL card-frame rebuild.
Run this file from Windows Explorer with PowerShell while signed in as MSI\12517.
#>

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repo

$source = Join-Path $repo 'unity-client\Builds\WebGL'
$destination = Join-Path $repo 'backend\public\game'

if (-not (Test-Path (Join-Path $source 'index.html'))) {
    throw "WebGL build was not found at $source. Build the WebGL profile in Unity first."
}

Write-Host 'Syncing Unity WebGL build into the backend public game bundle...' -ForegroundColor Cyan
robocopy $source $destination /E /NFL /NDL /NJH /NJS /NC /NS
if ($LASTEXITCODE -ge 8) {
    throw "Robocopy failed with exit code $LASTEXITCODE."
}

Write-Host 'Staging the card renderer, rarity frames, and WebGL bundle...' -ForegroundColor Cyan
git add -- `
    'unity-client/Assets/Scripts/UI/RarityMetadataCardRenderer.cs' `
    'unity-client/Assets/Scripts/UI/RarityMetadataCardRenderer.cs.meta' `
    'unity-client/Assets/Resources/Art/Official/CardTemplate/rarity_frames'
git add --force -- 'backend/public/game'

git diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host 'No publishable changes were detected.' -ForegroundColor Yellow
    exit 0
}

$message = 'Rebuild cards with live rarity metadata frames'
Write-Host 'Creating commit...' -ForegroundColor Cyan
git commit -m $message

Write-Host 'Pushing main to GitHub (Render will deploy automatically)...' -ForegroundColor Cyan
git push origin main

$commit = git rev-parse --short HEAD
Write-Host ''
Write-Host "Published commit $commit." -ForegroundColor Green
Write-Host "Test after Render finishes deploying: https://appreciators-tcg-backend.onrender.com/game/?build=$commit" -ForegroundColor Green
Read-Host 'Press Enter to close'
