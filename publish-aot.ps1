# ============================================================
# DS.Tools - NativeAOT publish script (ASCII only, no CJK chars)
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\publish-aot.ps1
#   powershell -ExecutionPolicy Bypass -File .\publish-aot.ps1 -Runtime win-arm64
#   powershell -ExecutionPolicy Bypass -File .\publish-aot.ps1 -StripSymbols
#
# Output: D:\Code\Self\DS.Tools\publish  (default)
# ============================================================
[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "",
    [switch]$StripSymbols
)

$ErrorActionPreference = "Stop"

# PSScriptRoot is NOT available inside the param block when the script is
# dot-sourced (e.g. ". .\publish-aot.ps1"), so resolve it in the body.
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

# UTF-8 console output - prevents garbled text on Chinese-locale Windows
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $ScriptDir "publish"
}

$Project = Join-Path $ScriptDir "DS.Tools\DS.Tools.csproj"

if (-not (Test-Path $Project)) {
    Write-Host "ERROR: project not found: $Project" -ForegroundColor Red
    exit 1
}

Write-Host "==> Publishing NativeAOT ($Runtime) -> $OutputDir" -ForegroundColor Cyan

# Clean output for a reproducible publish
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

$PublishArgs = @(
    "publish", $Project,
    "-c", "Release",
    "-r", $Runtime,
    "-p:PublishAot=true",
    "-p:PublishSingleFile=true",
    "--nologo",
    "-o", $OutputDir
)
if ($StripSymbols) { $PublishArgs += "-p:StripSymbols=true" }

& dotnet @PublishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "==> Publish FAILED (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "==> Publish succeeded. Output:" -ForegroundColor Green
Get-ChildItem $OutputDir -File | ForEach-Object { Write-Host "    $($_.Name)" -ForegroundColor Green }
