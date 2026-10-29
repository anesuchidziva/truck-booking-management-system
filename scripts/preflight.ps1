$ErrorActionPreference = 'Stop'
$checks = @('dotnet','python','npm')
foreach($name in $checks){
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if($command){ Write-Host "PASS  $name -> $($command.Source)" -ForegroundColor Green }
    else { Write-Host "FAIL  $name is not installed" -ForegroundColor Red }
}
Write-Host ''
Write-Host 'SQL Server options:' -ForegroundColor Cyan
if(Get-Command sqllocaldb -ErrorAction SilentlyContinue){
    sqllocaldb info
    Write-Host 'LocalDB is tooling only; its instance must also start successfully.' -ForegroundColor DarkCyan
} else { Write-Host 'SQL Server LocalDB tooling not found.' -ForegroundColor Yellow }
$sqlServices = Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^MSSQL(server|\$)' }
if($sqlServices){ $sqlServices | Select-Object Name, Status, DisplayName | Format-Table -AutoSize }
else { Write-Host 'WARNING SQL Server engine service not found. Install SQL Server Express or Developer before startup.' -ForegroundColor Yellow }
Write-Host ''
Write-Host 'Run scripts/start-amcotts.ps1 after the prerequisite checks pass.'
