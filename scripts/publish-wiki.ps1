param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$WikiUrl = "https://github.com/la1ona/CometenIRLSystem.wiki.git"
$WikiSource = Join-Path $RepoRoot "wiki"
$TempWiki = Join-Path $env:TEMP "CometenIRLSystem.wiki.publish"

if (-not (Test-Path $WikiSource)) {
    throw "Wiki source directory not found: $WikiSource"
}

if (Test-Path $TempWiki) {
    Remove-Item -Recurse -Force $TempWiki
}

Write-Host "Cloning GitHub Wiki..."
git clone $WikiUrl $TempWiki
if ($LASTEXITCODE -ne 0) {
    throw "Could not clone the Wiki. Enable Wikis in Repository Settings and create the first Home page once, then run this script again."
}

Get-ChildItem -Path $WikiSource -Filter "*.md" | ForEach-Object {
    Copy-Item -Force $_.FullName (Join-Path $TempWiki $_.Name)
}

Push-Location $TempWiki
try {
    git add -A

    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Wiki is already up to date."
        exit 0
    }

    git commit -m "Update Cometen IRL System Wiki"
    if ($LASTEXITCODE -ne 0) {
        throw "Wiki commit failed."
    }

    git push
    if ($LASTEXITCODE -ne 0) {
        throw "Wiki push failed. Check GitHub authentication/permissions."
    }

    Write-Host "Wiki published successfully."
}
finally {
    Pop-Location
}
