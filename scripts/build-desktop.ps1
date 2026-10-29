[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Desktop = Join-Path $Root 'desktop'
$Resources = Join-Path $Desktop 'resources'
$BackendOutput = Join-Path $Resources 'backend'
$DiagnosisOutput = Join-Path $Resources 'diagnosis-service'

function Require-Command([string]$Name, [string]$Hint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "$Name is required. $Hint" }
}

Require-Command 'node' 'Install Node.js LTS.'
Require-Command 'npm' 'Install Node.js LTS.'
Require-Command 'dotnet' 'Install the .NET 10 SDK.'
Require-Command 'python' 'Install Python 3.12 or later.'

Write-Host 'Building Amcotts Workshop Control desktop package' -ForegroundColor Cyan

Push-Location (Join-Path $Root 'frontend')
$env:VITE_API_BASE_URL = 'http://localhost:5000'
$env:VITE_DEMO_MODE = 'false'
npm.cmd run build
Pop-Location

if (Test-Path $BackendOutput) { Remove-Item -LiteralPath $BackendOutput -Recurse -Force }
New-Item -ItemType Directory -Force -Path $BackendOutput | Out-Null
dotnet publish (Join-Path $Root 'backend\Amcotts.Workshop.Api.csproj') -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $BackendOutput

if (-not (Get-Command pyinstaller -ErrorAction SilentlyContinue)) {
    throw 'PyInstaller is required to package the diagnostic service. Run: python -m pip install -r diagnosis-service\requirements-packaging.txt'
}
if (Test-Path $DiagnosisOutput) { Remove-Item -LiteralPath $DiagnosisOutput -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Resources | Out-Null
Push-Location (Join-Path $Root 'diagnosis-service')
pyinstaller --noconfirm --clean --onedir --name diagnosis-service --distpath $Resources --workpath (Join-Path $Desktop 'pyinstaller-build') --specpath (Join-Path $Desktop 'pyinstaller-spec') --paths . launcher.py
Pop-Location

Push-Location $Desktop
npm.cmd run generate-icon
npm.cmd run dist
Pop-Location

Write-Host ''
Write-Host "Installer created in: $(Join-Path $Desktop 'release')" -ForegroundColor Green
