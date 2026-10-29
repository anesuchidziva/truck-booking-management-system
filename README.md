# Truck Booking Management System with AI-Assisted Fault Diagnosis

Anesu Chidziva's final-year project for Amcotts Trading. The dissertation supplied in this workspace was reviewed before implementation. The application keeps its core scope: workshop bookings, queue/status workflow, mechanic allocation, human-reviewed diagnostic suggestions, repairs, history and reports.

## Current implementation

- `frontend`: React + TypeScript + Vite + Tailwind-compatible CSS, with a fully working local demonstration flow persisted in browser storage.
- `diagnosis-service`: FastAPI service with a small fictional, explicitly demonstrative scikit-learn-compatible baseline and rule-backed fallback for supported symptom combinations.
- `backend`: ASP.NET Core API with SQL Server EF Core models, initial migration, customer/truck/booking endpoints, transactional queue numbering, idempotent booking creation and status history. The local UI still uses its local adapter until the database connection is configured.
- `docs`: architecture, workflow, ERD, diagnostic notes, requirements mapping and dissertation alignment.

The AI boundary is documented in [`docs/ai-integration.md`](docs/ai-integration.md): the browser records inspection context, the ASP.NET API calls the private FastAPI service, and a mechanic must explicitly review any suggestion before a confirmed fault can affect allocation or reports.
- `scripts`: SQL Server backup/restore guidance scripts.

## Run the working UI

```powershell
cd frontend
npm.cmd install
Copy-Item .env.example .env.local
npm.cmd run dev
```

Open the printed Vite URL. Select a role in the sign-in screen; no shared production account is included. The local adapter demonstrates the complete path from customer/truck creation through booking, inspection, diagnosis, confirmation, allocation, repair completion, history and reports.

When the API is running, set `VITE_API_BASE_URL` in `frontend/.env.local` to its URL, for example `http://localhost:5000`. The booking list, queue board and dashboard then load bookings from ASP.NET. Fictional records are disabled by default; enable them explicitly with `VITE_DEMO_MODE=true` for a presentation-only walkthrough.

## Run the diagnostic service

```powershell
cd diagnosis-service
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8001
```

## Run the API

Install the .NET 10 SDK and configure `ConnectionStrings__DefaultConnection` to a SQL Server Express/Developer database. Then:

```powershell
cd backend
dotnet restore
dotnet ef database update
dotnet run
```

`/openapi/v1.json` is available in development. The packaged launcher applies checked-in EF migrations automatically when the compiled API starts. Local authentication is configured through ASP.NET Core Identity. Set `Authentication__BootstrapSecret` as an environment variable, start the API, then bootstrap the first administrator once:

```powershell
$env:Authentication__BootstrapSecret = 'generate-a-long-development-secret'
Invoke-RestMethod http://localhost:5000/api/auth/bootstrap -Method Post -ContentType 'application/json' -Body ('{"email":"admin@local.test","password":"ChangeThisStrongPassword1!","displayName":"Workshop Administrator","bootstrapSecret":"generate-a-long-development-secret"}')
```

Do not commit or reuse that development secret. Firebase token verification remains an optional provider to configure separately; SQL Server remains the operational database.

## Client handover startup

From the project folder on Windows PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\preflight.ps1
.\scripts\setup-amcotts.ps1
.\scripts\start-amcotts.ps1
```

The launcher starts the Python diagnostic service, starts the ASP.NET API (which applies EF migrations from its compiled assembly), and starts the Vite frontend. It writes service logs to `runtime-logs`. Stop the local services with:

```powershell
.\scripts\stop-amcotts.ps1
```

The package does not bundle SQL Server, passwords, Firebase credentials, or production secrets. SQL Server Express, Developer, or a working LocalDB instance must be installed separately. The launcher stops with a specific migration error if the database is unavailable; it does not silently switch to Firestore or another database.

## Honest limitations

No SQL Server instance, Firebase project, Docker engine or validated Amcotts diagnostic dataset was present in the workspace. The included diagnostic data is fictional and is not workshop evidence; no diagnostic accuracy is claimed. Backup RPO/RTO and latency targets remain to be measured with the deployed services. See `docs/limitations.md`.
