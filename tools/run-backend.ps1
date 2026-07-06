# tools/run-backend.ps1 - stop, build (Release) and run the API on :5210.
# Use -NoBuild to just restart the last Release build.
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$api = Join-Path $PSScriptRoot '..\backend\src\KaezanArenaFable.Api'

Get-Process -Name 'KaezanArenaFable.Api' -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not $NoBuild) {
    dotnet build $api -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$env:ASPNETCORE_URLS = 'http://localhost:5210'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Push-Location $api
try {
    & (Join-Path $api 'bin\Release\net8.0\KaezanArenaFable.Api.exe')
} finally {
    Pop-Location
}
