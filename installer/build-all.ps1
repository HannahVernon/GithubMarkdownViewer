<#
.SYNOPSIS
    Builds installers for all platforms.
.DESCRIPTION
    1. Publishes self-contained binaries for win-x64, linux-x64, osx-x64
    2. Builds Windows installer with Inno Setup (if iscc is available)
    3. Shows instructions for Linux and macOS packaging (must be run on those platforms)
.PARAMETER SkipPublish
    Skip the dotnet publish step (use existing publish output).
#>
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$InstallerDir = $PSScriptRoot

Write-Host "=== GitHub Markdown Viewer - Installer Build ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Publish
if (-not $SkipPublish) {
    Write-Host "--- Step 1: Publishing binaries ---" -ForegroundColor Yellow
    & "$InstallerDir\publish.ps1"
    Write-Host ""
} else {
    Write-Host "--- Step 1: Skipping publish (using existing output) ---" -ForegroundColor Yellow
    Write-Host ""
}

# Step 2: Windows Installer (Inno Setup)
Write-Host "--- Step 2: Windows Installer ---" -ForegroundColor Yellow

$iscc = $null
$innoLocations = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "ISCC.exe"  # On PATH
)
foreach ($loc in $innoLocations) {
    if (Test-Path $loc -ErrorAction SilentlyContinue) {
        $iscc = $loc
        break
    }
    # Check if it's on PATH
    if ($loc -eq "ISCC.exe") {
        $found = Get-Command $loc -ErrorAction SilentlyContinue
        if ($found) { $iscc = $found.Source; break }
    }
}

$outputDir = Join-Path $InstallerDir "output"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

if ($iscc) {
    Write-Host "  Found Inno Setup: $iscc" -ForegroundColor Green
    $issFile = Join-Path $InstallerDir "windows" "setup.iss"
    & $iscc $issFile
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup compilation failed"
        exit 1
    }
    Write-Host "  Windows installer created in: $outputDir" -ForegroundColor Green
} else {
    Write-Host "  Inno Setup not found. To build the Windows installer:" -ForegroundColor DarkYellow
    Write-Host "    1. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php" -ForegroundColor DarkYellow
    Write-Host "    2. Run: iscc installer\windows\setup.iss" -ForegroundColor DarkYellow
    Write-Host ""
    Write-Host "  Creating portable ZIP instead..." -ForegroundColor Yellow
    $winPublish = Join-Path $InstallerDir "publish" "win-x64"
    if (Test-Path $winPublish) {
        $zipPath = Join-Path $outputDir "GithubMarkdownViewer-1.0.0-win-x64-portable.zip"
        Compress-Archive -Path "$winPublish\*" -DestinationPath $zipPath -Force
        Write-Host "  Portable ZIP created: $zipPath" -ForegroundColor Green
    }
}

Write-Host ""

# Step 3: Linux packages
Write-Host "--- Step 3: Linux Packages ---" -ForegroundColor Yellow
$linuxPublish = Join-Path $InstallerDir "publish" "linux-x64"
if (Test-Path $linuxPublish) {
    Write-Host "  Linux binaries published. To build packages, run on a Linux machine:" -ForegroundColor Green
    Write-Host "    .deb: bash installer/linux/build-deb.sh" -ForegroundColor White
    Write-Host "    .rpm: bash installer/linux/build-rpm.sh" -ForegroundColor White
    Write-Host ""
    # Create a portable tar.gz
    $tarDir = Join-Path $outputDir "linux-tar-staging"
    if (Test-Path $tarDir) { Remove-Item $tarDir -Recurse -Force }
    Copy-Item $linuxPublish $tarDir -Recurse
    $tarPath = Join-Path $outputDir "GithubMarkdownViewer-1.0.0-linux-x64.tar.gz"
    # Use tar if available, otherwise note it
    $tar = Get-Command tar -ErrorAction SilentlyContinue
    if ($tar) {
        Push-Location $outputDir
        tar -czf "GithubMarkdownViewer-1.0.0-linux-x64.tar.gz" -C "linux-tar-staging" .
        Pop-Location
        Write-Host "  Portable tar.gz created: $tarPath" -ForegroundColor Green
    } else {
        Write-Host "  (tar not available — create tar.gz on Linux)" -ForegroundColor DarkYellow
    }
    if (Test-Path $tarDir) { Remove-Item $tarDir -Recurse -Force }
} else {
    Write-Host "  Linux binaries not found (publish first)" -ForegroundColor DarkYellow
}

Write-Host ""

# Step 4: macOS
Write-Host "--- Step 4: macOS Package ---" -ForegroundColor Yellow
$macPublish = Join-Path $InstallerDir "publish" "osx-x64"
if (Test-Path $macPublish) {
    Write-Host "  macOS binaries published. To build the .dmg, run on a Mac:" -ForegroundColor Green
    Write-Host "    bash installer/macos/build-dmg.sh" -ForegroundColor White
} else {
    Write-Host "  macOS binaries not found (publish first)" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Cyan
Write-Host "Output directory: $outputDir" -ForegroundColor White
if (Test-Path $outputDir) {
    Get-ChildItem $outputDir -File | ForEach-Object {
        Write-Host "  $($_.Name)  ($([math]::Round($_.Length / 1MB, 1)) MB)" -ForegroundColor Gray
    }
}
