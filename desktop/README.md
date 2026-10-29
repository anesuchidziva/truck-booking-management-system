# Amcotts Workshop Control desktop installer

This folder contains the Windows desktop shell and installer configuration. The desktop build keeps the existing React interface intact and starts the packaged ASP.NET API and FastAPI diagnostic service before opening the sign-in screen.

## Build the installer

From the repository root in Windows PowerShell:

```powershell
python -m pip install -r .\diagnosis-service\requirements-packaging.txt
cd .\desktop
npm.cmd install
cd ..
.\scripts\build-desktop.ps1
```

The installer is written to:

```text
desktop\release\Amcotts-Workshop-Control-Setup-1.0.0.exe
```

The build creates a self-contained .NET backend and a PyInstaller diagnostic sidecar. The generated resources and installer are intentionally excluded from Git; rebuild them from source when needed.

## Client requirements

The installer does not bundle SQL Server. The client computer or workshop server must provide the configured SQL Server instance. The default connection targets `localhost\SQLEXPRESS` and `AmcottsWorkshop`.

Optional connection overrides are stored per Windows user at:

```text
%APPDATA%\Amcotts Workshop Control\settings.json
```

Example:

```json
{
  "sqlConnection": "Server=WORKSHOP-SERVER\\SQLEXPRESS;Database=AmcottsWorkshop;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;Connect Timeout=5"
}
```

Application logs are stored at `%APPDATA%\Amcotts Workshop Control\logs`. The installer is currently unsigned; a trusted code-signing certificate should be added before client distribution to reduce Windows SmartScreen warnings.
