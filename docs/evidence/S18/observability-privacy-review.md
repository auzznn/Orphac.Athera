# S18 Observability & Privacy Review

- **Scope:** `src/Orphac.Athera.Api` (startup, endpoints, worker), `src/Orphac.Athera.Modules` (audit, outbox), `apps/web/src/app/api/[...path]/route.ts` (same-origin proxy).
- **Method:** source reading only. Nothing here was measured under load.
- **Yardstick:** PRD §10 objective "Observability — correlation IDs, structured logs, event failure and reconciliation diagnostics" and "Event latency — target p95 < 60 s". The PRD calls these proposed pilot targets, not guarantees.
- **Result:** **partially met.** Durable business diagnostics exist; operational logging and tracing do not. Findings `O-1`, `O-2` and `O-3` must be closed or accepted by the Owner before a pilot on real data.

## 1. What exists

| Capability | Evidence |
|---|---|
| Append-only audit trail (actor, subject, summary, JSON payload) | `audit.audit_events`; runtime role has `SELECT, INSERT` only; browsable in `/documents` → Audit Trail |
| Event failure diagnostics | `integration.outbox_messages` keeps status, attempt count, last error (clipped to 500 chars) and next attempt time; operators can retry from the Exceptions & Outbox tab |
| Reconciliation diagnostics | Five control-account reconciliations with GL drill-down in `/reports`; period close is blocked on any variance |
| Liveness | `GET /api/health` runs `SELECT 1` and returns `503 database_unavailable` on failure |
| Error identifiers | Every problem response carries `errorCode` and `traceId` |
| Sensitive EF logging off | `EnableSensitiveDataLogging` and `EnableDetailedErrors` are not enabled anywhere |

## 2. Observability findings

| ID | Severity | Finding | Evidence | Suggested fix |
|---|---|---|---|---|
| `O-1` | High | **Pending outbox messages are not swept.** The worker only wakes on an in-process signal held in a bounded channel (256 entries, drop-oldest). There is no start-up sweep and no periodic poll. After an API restart, or a dropped signal, `PENDING` and backed-off `FAILED` messages for a tenant wait until that tenant produces another event or an authorized admin presses dispatch (`POST /admin/outbox/dispatch`). The documents `Coba Ulang` action (`POST /documents/outbox/{messageId}/retry`) only requeues `FAILED` entries; it does not drain the queue. There is no eventual-delivery guarantee, so BRS `UAT-P2P-025` (worker interruption, at-least-once delivery) cannot be claimed, and no event p95 evidence exists. | `TenantOutboxProcessor.cs` (`TenantOutboxSignal`), `OutboxBackgroundWorker.cs`, `AdminAndApprovalEndpoints.cs`, `DocumentsEndpoints.cs` | A periodic sweep needs a way to find tenants with due messages without a tenant context. Under forced RLS that means a narrowly scoped `SECURITY DEFINER` function or a dedicated worker role — a security-architecture change needing its own design and review; it was not made in S18. The existing authorized admin dispatch may suffice operationally until then; do not widen any role without an Owner ruling. |
| `O-2` | Medium | **No correlation across tiers.** `traceId` is Kestrel's per-connection request id. The Next.js proxy forwards a fixed header list that has no `traceparent` or `x-correlation-id`, and generates none. A browser error cannot be tied to an API log line. | `ProblemResults.cs`, `route.ts` | Generate or accept `traceparent` in the proxy, forward it, and add it to a logging scope in the API. |
| `O-3` | Medium | **No request or business logging.** Application code contains one log statement (the outbox worker warning). `Microsoft.AspNetCore` is set to `Warning`, so requests, 4xx denials and posting outcomes are not logged; only unhandled exceptions are. The default console formatter is plain text, not structured JSON. | `appsettings.json`, grep for `Log*(` in `src/` | Enable JSON console logging and log one structured line per mutating request (route template, status, tenant id, entity id, actor id, duration, trace id) without bodies. |
| `O-4` | Low | Health check covers the database only. It does not report worker state or outbox backlog. | `HealthAndAuthEndpoints.cs` | Add a readiness endpoint once `O-1` is designed. |
| `O-5` | Low | No latency or backlog metrics are emitted, so the PRD performance and event-latency targets can only be checked by tests, not in operation. | — | Add counters/histograms when a hosting target is chosen. |

## 3. Privacy findings

| ID | Severity | Finding | Evidence | Suggested fix |
|---|---|---|---|---|
| `P-1` | Medium | **Personal data in immutable audit rows.** Invitation events write the invited e-mail address into both the summary and the payload. Audit rows cannot be updated or deleted, so the address cannot be erased or corrected later. | `AdminAndApprovalEndpoints.cs` (invitation create / revoke / accept) | Store the user or invitation id and resolve the address at read time; decide together with `OD-12`. |
| `P-2` | Medium | **No retention or erasure path.** `OD-12` (retention, tenant offboarding) is open and the safe default is "no destructive tenant deletion". Personal data (user e-mail and name, partner contacts, NPWP, bank account numbers) is therefore kept indefinitely. | `docs/product/ATH-OPEN-DECISIONS-001.md` | Owner decision `OD-12`. |
| `P-3` | Low | Bank account numbers are stored and returned in full to any user holding the payments read permission; there is no masking. | `CashModels.cs`, `PaymentsAndBankingContracts.cs` | Mask in list views; show in full only on the release screen. |
| `P-4` | Low | Signed download tokens travel in the query string, so they appear in browser history and any proxy access log. Mitigated: five-minute expiry, bound to tenant, entity, document and the signed-in user. | `DocumentsEndpoints.cs` | Acceptable for pilot; consider `POST`-then-stream later. |
| `P-5` | Low | Outbox `last_error` stores exception messages. Npgsql omits row values unless `Include Error Detail` is set, and it is not, so the risk is limited to handler-authored messages. | `OutboxModels.cs` | Keep `Include Error Detail` off in every environment. |
| `P-6` | Info | Local-only defaults are committed: disposable database role passwords and the demo password used by tests. The seeder refuses to run when the environment is `Production`. | `scripts/dev/bootstrap-postgres.sh`, `playwright.config.ts`, `DemoDataSeeder.cs` | Never reuse them outside a developer machine; already stated in the runbook. |

## 4. Confirmed good

- Session cookie is `HttpOnly`, `SameSite=Lax`, eight-hour sliding expiry, and `Secure` outside Development/Testing. The CSRF cookie follows the same rules and every mutation needs `X-CSRF-Token`.
- Security-stamp validation interval is zero, so a changed security stamp (for example a password change) ends existing sessions on the next request.
- Account lockout: five failed attempts, fifteen minutes.
- The proxy sets `Cache-Control: no-store` on every API response and forwards only an allow-list of request headers.
- No tokens are written to browser storage; the only `localStorage` entry is the interface language preference.
