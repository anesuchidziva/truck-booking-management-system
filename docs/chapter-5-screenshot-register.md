# Chapter 5 Screenshot Register

This register defines the recommended implementation evidence for Chapter 5. The images must be captured from the running Amcotts application, with passwords and private customer information hidden. The figure numbers can be adjusted if the final dissertation combines or separates images.

## Recommended figures

| Figure | Screen and role | Demonstration data/action | Evidence that must be visible |
|---|---|---|---|
| Figure 5.1 | Operational sign-in | Supervisor, Mechanic or Service Clerk role selected | Role selection, work-email/password fields and authorised entry control; do not show a real password |
| Figure 5.2 | Role dashboard | Supervisor signed in | Supervisor identity, role badge, live booking totals and permitted navigation |
| Figure 5.3 | New Booking form | Clerk; QA Haulage Test Fleet, ARK 2605, Scania R500 | Customer, registration, make/model, complaint and selected symptoms |
| Figure 5.4 | Booking validation | Clerk; submit with required fields empty | Clear validation message and the form remaining usable |
| Figure 5.5 | Created booking and queue | Clerk; use the newly created booking | Generated `BK-xxxx` reference, `Pending`/`Waiting` status and queue entry |
| Figure 5.6 | Diagnostic input and suggestions | Mechanic or Supervisor; use an active inspection booking | Recorded symptoms/evidence, ranked diagnostic suggestions and wording that mechanic verification is required |
| Figure 5.7 | Diagnostic fallback | Request suggestions with insufficient evidence or with the diagnostic service unavailable | Clear fallback/unavailable response and the option to continue with manual inspection |
| Figure 5.8 | Mechanic allocation | Supervisor; use `BK-0003` after its confirmed fault has an eligible skill | Selected job, mechanic skills, availability, workload and successful assignment/status change |
| Figure 5.9 | Job progress update | Mechanic; use the assigned booking | Checklist/findings saved, progress value and status reflected in the queue/workbench |
| Figure 5.10 | Completed repair and service history | Mechanic; use the assigned repair booking | Repair findings, work performed, parts/quality checks, `Completed` status and retrievable truck history |
| Figure 5.11 | Workshop reports | Supervisor; completed records available | Actual turnaround-time, mechanic-completion and confirmed-fault report values, not placeholder cards |

## Capture rules

Before capturing each figure:

1. Use a fresh browser session or refresh with `Ctrl + F5`.
2. Confirm the role name and role badge are visible where relevant.
3. Crop out the browser address bar, unrelated desktop space, passwords and private contact details.
4. Keep the same demonstration records and date format throughout the chapter.
5. Do not use promotional artwork, mock-ups or generated images as implementation evidence.

## Suggested capture order

Capture the workflow in this order so the screenshots are supported by real state transitions:

1. Figure 5.1–5.2: sign in and record the role dashboard.
2. Figure 5.3–5.5: create a new booking and record its queue entry.
3. Figure 5.6–5.7: request diagnostic suggestions and capture both normal and fallback responses.
4. Figure 5.8: make the booking eligible, allocate a mechanic and capture the result.
5. Figure 5.9: save inspection progress and capture the updated queue/workbench.
6. Figure 5.10: complete repair evidence, finalize the job and open service history.
7. Figure 5.11: open reports after the completed record has been persisted.

## Test evidence table

Screenshots support the test record but do not replace it. Use one row per test case:

| Test ID | Requirement/objective | Role | Input/action | Expected result | Actual result | Pass/Fail | Figure/error reference |
|---|---|---|---|---|---|---|---|
| T-01 | Role-based authentication | Supervisor | Valid supervisor credentials | Supervisor dashboard and permitted navigation load |  |  | Fig. 5.1–5.2 |
| T-02 | Required booking validation | Clerk | Submit required fields empty | Validation message; no booking created |  |  | Fig. 5.4 |
| T-03 | Booking persistence | Clerk | Create booking, refresh, sign out/in | Same booking reference remains in SQL Server |  |  | Fig. 5.5 |
| T-04 | Diagnostic human review | Supervisor/Mechanic | Request and review suggestion | Suggestion is stored as reviewed evidence; no automatic repair approval |  |  | Fig. 5.6 |
| T-05 | Diagnostic fallback | Supervisor/Mechanic | Insufficient evidence or unavailable service | Clear fallback message; manual inspection remains possible |  |  | Fig. 5.7 |
| T-06 | Skill-based allocation | Supervisor | Select eligible mechanic for ready booking | Assignment succeeds only when skill, availability and capacity pass |  |  | Fig. 5.8 |
| T-07 | Inspection persistence | Mechanic | Save checklist/findings, refresh | Inspection evidence and progress remain |  |  | Fig. 5.9 |
| T-08 | Repair completion/history | Mechanic | Save repair, complete quality checks, finalize | Booking becomes Completed and history is retrievable |  |  | Fig. 5.10 |
| T-09 | Operational reporting | Supervisor | Open reports after completion | Report values reflect persisted records |  |  | Fig. 5.11 |

## Caption style

Use captions in this form:

> Figure 5.8: Supervisor allocation of BK-0003 using mechanic skill, availability and workload data.

Introduce every image in the preceding paragraph and explain what the result proves immediately afterwards.

## Captured implementation artifacts

The following PNGs were captured from the local running application on 6 October 2026. They are evidence files, not interface mock-ups.

| Evidence | File |
|---|---|
| Role-based sign-in | [figure-5-01-login-role-selection.png](chapter5-screenshots/figure-5-01-login-role-selection.png) |
| Supervisor dashboard | [figure-5-02-supervisor-dashboard.png](chapter5-screenshots/figure-5-02-supervisor-dashboard.png) |
| Clerk booking form | [figure-5-03-new-booking-form.png](chapter5-screenshots/figure-5-03-new-booking-form.png) |
| Empty-booking validation | [figure-5-04-booking-validation.png](chapter5-screenshots/figure-5-04-booking-validation.png) |
| Created bookings and queue | [figure-5-05-created-booking-queue.png](chapter5-screenshots/figure-5-05-created-booking-queue.png) |
| Mechanic dashboard | [figure-5-06-mechanic-dashboard.png](chapter5-screenshots/figure-5-06-mechanic-dashboard.png) |
| Diagnostic input | [figure-5-07-diagnostic-input.png](chapter5-screenshots/figure-5-07-diagnostic-input.png) |
| Diagnostic fallback | [figure-5-07-diagnostic-fallback.png](chapter5-screenshots/figure-5-07-diagnostic-fallback.png) |
| Diagnostic suggestions | [figure-5-08-diagnostic-results.png](chapter5-screenshots/figure-5-08-diagnostic-results.png) |
| Allocation recommendation for BK-0003 | [figure-5-09-mechanic-allocation.png](chapter5-screenshots/figure-5-09-mechanic-allocation.png) |
| Allocation confirmed; BK-0003 assigned | [figure-5-09b-assignment-confirmed.png](chapter5-screenshots/figure-5-09b-assignment-confirmed.png) |
| Completed booking and workflow history | [figure-5-10-completed-repair-history.png](chapter5-screenshots/figure-5-10-completed-repair-history.png) |
| Live workshop reports | [figure-5-11-workshop-reports.png](chapter5-screenshots/figure-5-11-workshop-reports.png) |

For the successful allocation evidence, the Administrator configured David Smith with the `cooling-system-leak` skill, then BK-0003 was assigned through the Supervisor allocation board. This configuration change should be recorded in the test log.

For final dissertation numbering, treat the diagnostic input and diagnostic suggestions images as Figure 5.6(a) and Figure 5.6(b), the allocation recommendation and assignment confirmation as Figure 5.8(a) and Figure 5.8(b), and keep the mechanic dashboard as optional role-dashboard evidence.
