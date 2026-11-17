# ATH-GREENFIELD-001 — Clean Repository Bootstrap Contract

## Starting condition
This workspace is intentionally new. There is no Athera production code, no prior Git history to preserve, no SQL Server deployment, no Blazor UI, and no migration in progress. The files you see initially are planning assets. **Do not search for or request the earlier implementation**, and do not claim a greenfield feature is already implemented.

## S00 milestones
1. Inspect environment and skill locations; read actual Ponytail and Impeccable instructions where installed. Log unavailable dependencies without pretending the skills ran.
2. Initialize Git **once** in this folder if absent; establish a baseline containing the planning pack (do not commit secrets). Set `.gitignore`, `.editorconfig`, `.env` examples and solution/package tooling.
3. Bootstrap minimal Next.js + TypeScript (stable supported release) under `apps/web` and .NET 10 API under `src`; install a compatible Npgsql EF Core 10 provider.
4. Run a disposable PostgreSQL instance; local-only `bootstrap/compose.postgres.dev.yml` is an optional starting point, not a production deployment file. Give the application/runtime and migrations different roles/credentials.
5. Establish ASP.NET Core Identity authority, documented secure same-origin browser-to-API routing and CSRF defense; avoid a separate Next.js user database.
6. Implement a minimal real user journey (login -> authenticated API -> shell), not merely a `/health` endpoint. Keep tenant/entity authorization for S01, but all protected data must deny by default until S01 is verified.
7. Create the first minimal PostgreSQL migration, run it Up and tested rollback/reapplication on a disposable DB. Generate OpenAPI TypeScript client.
8. Run standard backend/frontend unit/lint/types/build tests; verify login failure path and no sensitive data in tracked output. Record actual command output and commit SHA.

## Never assume installation
If SDK/Node/PostgreSQL cannot run, document `BLOCKED` tests, correct environment using local/free tools when allowed, and do not present smoke checks as real PostgreSQL verification. No paid provisioning or production deployment.

## Required local commands in final S00 runbook
- install/restore and start PostgreSQL;
- apply migrations and seed **disposable demo** identities;
- start .NET API and Next.js behind documented same-origin arrangement;
- build, type-check, lint, `dotnet test`, migration and PG tests;
- stop services and safely reset disposable data.

Use local secrets or environment injection; never hardcode production secrets, never expose PostgreSQL to public network by default.
