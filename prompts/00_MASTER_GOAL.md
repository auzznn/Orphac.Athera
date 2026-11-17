# /goal — Build Orphac Athera MVP from a completely empty codebase

You are the **Principal Enterprise Engineer and Product Engineering Lead** executing the Product Owner's preauthorized **S00–S18 engineering workstream** for Orphac Athera, an industry-neutral, multi-tenant ERP for Indonesian SMEs.

## Critical starting assumption — GREENFIELD
**This is a new repository, starting from zero code.** The extracted files are **specifications and prompts**, not a previous application. Do not migrate, port, rebaseline, recover, preserve, or inspect old Blazor/SQL Server work. Do not assume legacy code, migrations, databases, users, or tests exist. Create the project from scratch. Initialize Git in this folder if it is not already a repository.

## Binding technology stack
- Next.js + React + TypeScript frontend, purposeful Tailwind/shadcn UI where useful.
- ASP.NET Core 10 Web API modular monolith and .NET hosted workers when needed.
- PostgreSQL + EF Core 10 / compatible Npgsql provider; shared tenant-owned tables with module-owned schemas.
- ASP.NET Core Identity as **sole** authentication authority; secure browser-session design, same-origin proxy/routing, CSRF defense, explicit tenant membership and entity authorization.
- OpenAPI-generated typed frontend client. One DB transaction for critical Inventory + GL/source/outbox effects.

## Mandatory reading
1. Read `AGENTS.md`, `README.md`, `docs/implementation/GREENFIELD_BOOTSTRAP.md`, `docs/implementation/WORKSTREAM-STATE.md` and `docs/product/ATH-MVP-SLICES-001.md`.
2. Read `reference/ATH-PRD-001-v0.2.md` and `reference/ATH-BRS-P2P-001-v0.3.md` to understand approved business scope and unresolved policies.
3. Read `docs/architecture/ATH-ADR-STACK-001.md`, `ATH-DOMAIN-BOUNDARIES-001.md`, `ATH-DB-FOUNDATION-001.md`, `ATH-POSTGRES-SECURITY-001.md`, `docs/product/ATH-OPEN-DECISIONS-001.md`.
4. Read `docs/design/ATH-UX-001.md`, `docs/testing/ATH-TESTING-001.md`, `docs/governance/ATH-PO-GOVERNANCE-001.md`, and the specific Sxx contract before each slice.

## Mandatory design/engineering skills
- Use **Ponytail (full)** for each slice's implementation and engineering review. Read its real installed instructions first. Minimize unnecessary abstraction while retaining authentication, validation, audit, financial integrity and tests.
- Use **Impeccable** for user-facing information architecture, layout, typography, forms, data tables, accessible states, UX critique and purposeful motion. Read its real installed instructions. Use its command taxonomy only as actually provided, keep PRODUCT.md and DESIGN.md consistent across slices.
- If a skill isn't installed, state which skill is unavailable and continue using the written engineering/UX standards; **never claim to have invoked it**.

## Long-horizon autonomous execution
For S00 then S01 … S18, in order:
1. Verify environment, dependencies and the true current repository state; read the slice contract.
2. Write `docs/design/slices/Sxx-design.md` with business behavior, user journey, module/data ownership, state machine, API/DB/UX contracts, atomicity/invariants, positive/negative tests and open decisions.
3. Implement the smallest complete functioning vertical slice, including .NET API, Npgsql/PostgreSQL migrations, typed Next.js UI, auth, domain rules and tests. Do **not** create speculative schemas for all future modules.
4. Prove isolation, permission, concurrency, idempotency and accounting behavior with real PostgreSQL integration tests where applicable. Run standard `dotnet test`, frontend lint/types/build, browser-level tests when available. Record PASS / FAIL / BLOCKED honestly.
5. Review code with Ponytail; run Impeccable UX critique/audit and inspect actual screenshots where possible. Fix defects, log constraints, commit locally, and write the evidence packet under `docs/evidence/Sxx/` with exact Git SHA. Update `WORKSTREAM-STATE.md`.
6. Proceed to the next engineering slice only when prerequisites have passed or the next work is logically independent and safe; never mask blocked security or financial requirements by creating shallow scaffolds.

## Product Owner authority (mandatory)
This message authorizes **development, local tests and local Git commits** within S00–S18, not approval of results. The Product Owner alone approves material business policies, architecture deviations, final slice acceptance, production deployment, paid provisioning, real financial movement and statutory filings. An agent must **never sign, date or mark an acceptance record APPROVED**, nor pretend silence grants permission.

Open decisions (approval thresholds, overreceipt tolerance, match tolerance, partial price variance edge cases, Indonesian taxes, payment release evidence, returns/credits) remain open until explicitly signed. Build safe reversible prerequisites and disable unapproved irreversible posting paths. Raise a concise `docs/decisions/ATH-CR-*.md` when needed; continue only independent work safely.

## Hard limits
No production or paid infrastructure provisioning, public pushes, secrets exposure, destructive changes to real data, external bank submissions, real filings, unauthorized extra modules, microservices or blanket schema generation. Manufacturing/foundry/MRP/APS, payroll, POS and intercompany consolidation are out of MVP.

## Start immediately
Begin with S00: **greenfield full-stack bootstrap and first authenticated vertical path**. There is no pre-existing implementation to evaluate. Create a real runnable repository; write commands, test results and commits. Your goal is a functional, verifiable MVP candidate and owner-ready evidence, not just generated architecture documents. Continue autonomously through safe approved scope until genuinely blocked.
