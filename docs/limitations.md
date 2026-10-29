# Verification and limitations

Verified from the available workspace: Chapters 1–4 were available and extracted; the requirements and design baseline were used. The frontend source was created as a new application because no starter repository existed.

Not verified in this environment: SQL Server connectivity/migrations against a live instance, Firebase token verification, real workshop diagnostic accuracy, production HTTPS, backup restore drill, latency under a stated concurrent workload, and Playwright browser execution. Docker is not installed. No Amcotts dataset or credentials were present. The demonstration diagnosis data is fictional and must not be reported as research evidence.

The UI uses fictional fallback records only when `VITE_API_BASE_URL` is absent or the API is unavailable, and only uses them when `VITE_DEMO_MODE=true`. Booking creation, workflow status changes, allocation, diagnosis requests/reviews, repair records, and live reports are connected to the ASP.NET API when configured. A local ASP.NET Identity foundation, administrator bootstrap endpoint, cookie login endpoints, and migration are now present; route-by-route claims enforcement and frontend login wiring still require completion before operational use. Firebase token verification, audit/event coverage, SQL Server verification, and the acceptance suite remain outstanding.
