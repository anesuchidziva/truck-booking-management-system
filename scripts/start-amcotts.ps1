[CmdletBinding()]
param([string]$SqlConnection = "Server=localhost\SQLEXPRESS;Database=AmcottsWorkshop;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;Connect Timeout=5")

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$LogDir = Join-Path $Root 'runtime-logs'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Require-Command([string]$Name, [string]$InstallHint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required but was not found. $InstallHint"
    }
}

Write-Host 'Amcotts Workshop Control - local startup' -ForegroundColor Cyan
Require-Command 'dotnet' '.NET 10 SDK/runtime is required.'
Require-Command 'python' 'Python 3.12 or later is required.'
Require-Command 'npm' 'Node.js/npm is required.'

$env:ConnectionStrings__DefaultConnection = $SqlConnection
$env:FrontendOrigin = 'http://localhost:5173'

if (-not (Test-Path (Join-Path $Root 'frontend\node_modules'))) {
    throw 'Frontend dependencies are missing. Run scripts/setup-amcotts.ps1 once with internet access.'
}
if (-not (Test-Path (Join-Path $Root 'backend\obj\project.assets.json'))) {
    throw 'Backend dependencies are missing. Run scripts/setup-amcotts.ps1 once with internet access.'
}

function Test-PortInUse([int]$Port) {
    try {
        return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop)
    } catch {
        $client = New-Object System.Net.Sockets.TcpClient
        try { $client.Connect('127.0.0.1', $Port); return $true } catch { return $false } finally { $client.Dispose() }
    }
}

function Start-LoggedProcess([string]$Name, [string]$WorkingDirectory, [string]$FilePath, [string[]]$Arguments, [int]$Port) {
    if (Test-PortInUse $Port) {
        Write-Host "$Name already responds on port $Port. Reusing the existing process." -ForegroundColor Yellow
        return
    }
    $stdout = Join-Path $LogDir "$Name.out.log"
    $stderr = Join-Path $LogDir "$Name.error.log"
    Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr | Out-Null
    Write-Host "Started $Name. Logs: $stdout" -ForegroundColor Green
}

Start-LoggedProcess 'diagnosis' (Join-Path $Root 'diagnosis-service') 'python' @('-m','uvicorn','app.main:app','--host','127.0.0.1','--port','8001') 8001
Start-LoggedProcess 'backend' (Join-Path $Root 'backend') 'dotnet' @('bin\Debug\net10.0\Amcotts.Workshop.Api.dll','--urls','http://localhost:5000') 5000
Start-LoggedProcess 'frontend' (Join-Path $Root 'frontend') 'npm.cmd' @('run','dev','--','--host','127.0.0.1') 5173

Write-Host ''
Write-Host 'Amcotts is starting:' -ForegroundColor Cyan
Write-Host '  Frontend:  http://localhost:5173'
Write-Host '  API:       http://localhost:5000/api/health'
Write-Host '  Diagnosis: http://127.0.0.1:8001/health'
Write-Host '  Logs:      runtime-logs\'
Write-Host '  Database migrations run automatically when the API starts.' -ForegroundColor DarkCyan
