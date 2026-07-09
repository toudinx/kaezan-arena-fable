# tools/verify.ps1 - one-command build + determinism gate.
#
# Runs, in order: backend build, backend tests, frontend build, and the FF-01 replay-check on the
# committed golden battery. Any step failing exits non-zero. This is the gate every engine refactor
# should pass before commit (README "Invariantes" / FF-02+).
#
#   powershell -File tools/verify.ps1              # full gate
#   powershell -File tools/verify.ps1 -NoFrontend  # skip the Angular build
#   powershell -File tools/verify.ps1 -Quick       # backend build + tests only (fast inner loop)
#   powershell -File tools/verify.ps1 -Regen       # regenerate the replay battery, then check it
param(
    [switch]$NoFrontend,
    [switch]$Quick,
    [switch]$Regen,
    [int]$RegenSeeds = 4
)

$ErrorActionPreference = 'Stop'
$root       = Resolve-Path (Join-Path $PSScriptRoot '..')
$api        = Join-Path $root 'backend\src\KaezanArenaFable.Api'
$tests      = Join-Path $root 'backend\tests\KaezanArenaFable.Api.Tests'
$balanceSim = Join-Path $root 'tools\BalanceSim'
$frontend   = Join-Path $root 'frontend'
$replays    = Join-Path $api '.data\replays'

function Step($name) { Write-Host ''; Write-Host "== $name ==" -ForegroundColor Cyan }
function Fail($msg)  { Write-Host ''; Write-Host "VERIFY FAILED - $msg" -ForegroundColor Red; exit 1 }

# 1. backend build (Release: measurement/behavior in Release is the project convention)
Step 'backend build'
dotnet build $api -c Release
if ($LASTEXITCODE -ne 0) { Fail 'backend build' }

# 2. backend tests (includes GameConfig.Validate() guard, dungeon/determinism suites)
Step 'backend tests'
dotnet test $tests -c Release
if ($LASTEXITCODE -ne 0) { Fail 'backend tests' }

# 3. frontend build
if ($Quick -or $NoFrontend) {
    Write-Host '(skipping frontend build)' -ForegroundColor DarkGray
} else {
    Step 'frontend build'
    Push-Location $frontend
    try {
        npx ng build
        if ($LASTEXITCODE -ne 0) { Fail 'frontend build' }
    } finally { Pop-Location }
}

# 4. FF-01 determinism replay-check on the golden battery
if ($Quick) {
    Write-Host '(skipping replay-check - -Quick)' -ForegroundColor DarkGray
} else {
    if ($Regen) {
        Step "regenerate replay battery ($RegenSeeds seeds x Kaeli x tier)"
        dotnet run --project $balanceSim -c Release -- --save-replays $replays --seeds $RegenSeeds | Out-Host
        if ($LASTEXITCODE -ne 0) { Fail 'replay battery regeneration' }
    }

    $battery = @(Get-ChildItem -Path $replays -Filter '*.replay.json.gz' -ErrorAction SilentlyContinue)
    if ($battery.Count -eq 0) {
        Write-Host ''
        Write-Host "WARNING - no golden replay battery in $replays" -ForegroundColor Yellow
        Write-Host '  The determinism gate has no baseline to check against. Restore the committed' -ForegroundColor Yellow
        Write-Host '  battery, or regenerate one with:  tools/verify.ps1 -Regen' -ForegroundColor Yellow
    } else {
        Step "replay-check ($($battery.Count) replays)"
        dotnet run --project $balanceSim -c Release -- --replay-check $replays
        if ($LASTEXITCODE -ne 0) { Fail 'replay-check (determinism drift)' }
    }
}

Write-Host ''
Write-Host 'VERIFY GREEN' -ForegroundColor Green
