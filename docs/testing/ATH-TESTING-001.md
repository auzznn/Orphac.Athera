# ATH-TEST-001 - Test and evidence strategy

## Test layers
1. Domain unit tests for deterministic rule boundaries, state transitions, amount/quantity arithmetic and approvals.
2. API tests for authentication, authorization, validation, idempotency, error semantics and business workflow endpoints.
3. **Real PostgreSQL integration** for migrations, constraints, RLS, role isolation, concurrency, locks, valuation pool and financial posting.
4. Next.js component/integration tests for form rules, business status display, request/response contract and safety of mutation UI.
5. Browser end-to-end tests for one authorized and one prohibited user journey per slice; more for finance-critical or security-critical workflows.
6. Data integrity tests for reconciliations and cross-module source-to-ledger linkage, financial period lock, and rollback.
7. Restorable backup and operational incident/release tests before production candidate.

## Execution requirements
- Standard `dotnet build`, `dotnet test`, Next.js typecheck/lint/build and browser runner. Do not substitute a custom test runner merely to claim success in a constrained sandbox.
- Integration tests must use a real disposable PostgreSQL DB, with appropriate low-privilege roles; Testcontainers are reasonable when Docker is available, but a controlled remote disposable DB is acceptable. If unavailable, mark BLOCKED and do not claim PASS.
- Evidence manifest per test: requirement ID, executable test ID, exact command, UTC timestamp, environment, result and log/artifact path. Evidence is commit-specific.
- Never count a test's source code as passing evidence. Do not make a test green by skipping its critical assertion or mocking the system under test.
- Financial tests must include zero/negative edges, rounding, exact match/threshold boundaries, partial receipts/bills, concurrent retries, reversal chain and outbox failure.
- Security tests must include foreign tenant and foreign legal entity reads/writes, CSRF, stale sessions, forged ID/header, RLS missing context, pooled connections, unauthorized document access and privilege changes.

## Continuous gates
- Per slice: migrations + relevant security integration PASS, backend/frontend builds PASS, critical negative tests PASS, accessibility/basic UX verified, no hardcoded secret, reproducible evidence + SHA.
- S07, S09, S10, S14 and S15 are financially critical; require added accounting reconciliation and rollback proofs.
- Product Owner may reject a feature despite passing automated tests when behavior or ownership doesn't meet intent. Engineering PASS is not product acceptance.

## Greenfield baseline
No earlier test results or test files are authoritative in this repository. All PASS claims require command output from this new implementation against its own disposable PostgreSQL database and actual runtime principal. Never cite prior SQL Server tests or an earlier Blazor build.
