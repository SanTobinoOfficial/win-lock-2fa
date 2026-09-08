# Builds a single, dependency-free WinLock2FA.exe.
# The resulting .exe bundles the .NET runtime, so the target Windows PC does
# NOT need .NET (or anything else) installed to run it.

$ErrorActionPreference = "Stop"

$projectDir = Join-Path $PSScriptRoot "src\WinLock2FA"
$outDir = Join-Path $PSScriptRoot "dist"

dotnet publish $projectDir `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

Write-Host ""
Write-Host "Gotowe: $outDir\WinLock2FA.exe" -ForegroundColor Green
