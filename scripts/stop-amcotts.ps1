$ErrorActionPreference = 'SilentlyContinue'
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'Amcotts|uvicorn app.main|vite' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Write-Host 'Amcotts local services stopped.'
