# Architecture and workflow

```mermaid
flowchart LR
 Browser[React + TypeScript + Vite] --> API[ASP.NET Core API]
 API --> SQL[(SQL Server Express / Developer)]
 API --> Python[Private FastAPI diagnostic service]
 Python --> Model[Reproducible fictional baseline]
 API --> Auth[Local Identity or Firebase token verification]
```

```mermaid
stateDiagram-v2
 Waiting --> UnderInspection
 UnderInspection --> ReadyForAllocation
 ReadyForAllocation --> Assigned
 Assigned --> InRepair
 InRepair --> Completed
 Waiting --> Cancelled
 UnderInspection --> Cancelled
 ReadyForAllocation --> Cancelled
 Assigned --> Cancelled
 InRepair --> Cancelled
```

The UI local adapter exists for an offline demonstration. In a deployment, the API is authoritative for transitions, ownership, idempotency, allocation transactions, audit events and SQL persistence.
