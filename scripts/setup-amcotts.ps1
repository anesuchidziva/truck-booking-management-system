[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Write-Host 'Amcotts first-run setup. Internet access is required for dependency downloads.' -ForegroundColor Cyan
Push-Location (Join-Path $Root 'backend')
dotnet restore
if ($LASTEXITCODE -ne 0) { throw 'Backend NuGet restore failed.' }
dotnet build --no-restore -p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
Pop-Location
Push-Location (Join-Path $Root 'frontend')
$frontendModules = Join-Path (Get-Location) 'node_modules'
if (Test-Path $frontendModules) {
    Write-Host 'Existing frontend node_modules found. Validating it before reinstalling.' -ForegroundColor DarkCyan
    npm run build
    if ($LASTEXITCODE -eq 0) {
        Write-Host 'Existing frontend dependencies are usable; no reinstall needed.' -ForegroundColor Green
    } else {
        Write-Host 'Existing frontend dependencies are incomplete. Running npm ci.' -ForegroundColor Yellow
        npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Frontend npm install failed. Close Vite/Node processes and retry.' }
    }
} else {
    npm ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Frontend npm install failed.' }
}
Pop-Location
Push-Location (Join-Path $Root 'diagnosis-service')
python -m pip install -r requirements.txt
if ($LASTEXITCODE -ne 0) { throw 'Python dependency installation failed.' }
Pop-Location
Write-Host 'Setup complete. Run scripts/start-amcotts.ps1 next.' -ForegroundColor Green
