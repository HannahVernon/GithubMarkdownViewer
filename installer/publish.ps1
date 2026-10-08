<#
.SYNOPSIS
    Publishes GithubMarkdownViewer as self-contained binaries for Windows, Linux, and macOS.
.DESCRIPTION
    Builds Release self-contained, single-file executables for each target platform.
    Output goes to installer/publish/<rid>/.
.PARAMETER Runtime
    Optional: specify a single RID to build (e.g., win-x64). Defaults to all three.
.PARAMETER Version
    Optional: version to build into the app, for example 1.6.35. Defaults to <Version> in the csproj.
#>
param(
    [string]$Runtime = "",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path (Join-Path $RepoRoot "GithubMarkdownViewer") "GithubMarkdownViewer.csproj"
$PublishBase = Join-Path $PSScriptRoot "publish"

if ($Version -ne "" -and $Version -notmatch '^\d+(\.\d+){1,3}$') {
    Write-Error "Invalid version '$Version'. Use a numeric version such as 1.2.3."
    exit 1
}

# Resolve dotnet to full path to prevent PATH hijacking
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Error "dotnet SDK not found on PATH"
    exit 1
}
$DotnetPath = $dotnetCmd.Source

$Runtimes = @("win-x64", "linux-x64", "osx-x64")
if ($Runtime -ne "") {
    $Runtimes = @($Runtime)
}

foreach ($rid in $Runtimes) {
    $outDir = Join-Path $PublishBase $rid
    Write-Host "Publishing for $rid -> $outDir" -ForegroundColor Cyan

    $publishArgs = @(
        "publish", $ProjectPath,
        "--configuration", "Release",
        "--runtime", $rid,
        "--self-contained", "true",
        "--output", $outDir,
        "-p:PublishSingleFile=true",
        "-p:PublishTrimmed=false",
        "-p:IncludeNativeLibrariesForSelfExtract=true"
    )
    if ($Version -ne "") { $publishArgs += "-p:Version=$Version" }

    & $DotnetPath @publishArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed for $rid"
        exit 1
    }

    Write-Host "  Done: $rid" -ForegroundColor Green
}

Write-Host "`nAll publish targets complete." -ForegroundColor Green
