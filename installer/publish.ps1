<#
.SYNOPSIS
    Publishes GithubMarkdownViewer as self-contained binaries for Windows, Linux, and macOS.
.DESCRIPTION
    Builds Release self-contained, single-file executables for each target platform.
    Output goes to installer/publish/<rid>/.
.PARAMETER Runtime
    Optional: specify a single RID to build (e.g., win-x64). Defaults to all three.
#>
param(
    [string]$Runtime = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "GithubMarkdownViewer" "GithubMarkdownViewer.csproj"
$PublishBase = Join-Path $PSScriptRoot "publish"

$Runtimes = @("win-x64", "linux-x64", "osx-x64")
if ($Runtime -ne "") {
    $Runtimes = @($Runtime)
}

foreach ($rid in $Runtimes) {
    $outDir = Join-Path $PublishBase $rid
    Write-Host "Publishing for $rid -> $outDir" -ForegroundColor Cyan

    dotnet publish $ProjectPath `
        --configuration Release `
        --runtime $rid `
        --self-contained true `
        --output $outDir `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:IncludeNativeLibrariesForSelfExtract=true

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed for $rid"
        exit 1
    }

    Write-Host "  Done: $rid" -ForegroundColor Green
}

Write-Host "`nAll publish targets complete." -ForegroundColor Green
