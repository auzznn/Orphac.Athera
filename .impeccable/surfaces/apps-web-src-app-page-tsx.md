---
version: 1
slug: "apps-web-src-app-page-tsx"
primary_target: "apps/web/src/app/page.tsx"
related_targets: ["apps/web/src/app/login/page.tsx"]
---

# Surface Brief: Orphac Athera Application Shell & Sign-In

- **Visitor mode:** Operate
- **Scope:** `apps/web/src/app/(shell)/page.tsx` and `apps/web/src/app/login/page.tsx`
- **Audience & job:** Operational SME staff (Procurement, Warehouse, Finance, Admin, Owner) signing in via ASP.NET Core Identity and verifying their active PostgreSQL-backed session and tenant/legal-entity scope status.
- **Unresolved decisions:** Product Owner acceptance pending; tenant/entity selection and RLS isolation belong to S01, so S00 explicitly enforces default-deny workspace access.

## Direction contract

THESIS: Every screen behaves like a calm, verifiable Indonesian commercial ledger and dispatch book—precise rows, explicit scope badges, and clear blocking reasons—refusing generic SaaS gradient cards, hero metric tiles, and unanchored modals.

OWN-WORLD: Warm archival paper canvas (`#f6f5f0`), crisp white operational sheets (`#ffffff`), cool slate-rail chrome (`#eef0eb`) bounded by 1px hairline rules (`#d5d9d2`), deep charcoal ink (`#171c1e`), and deep Sumatra teal-jade (`#0e5a53`) reserved strictly for primary actions, active navigation, and focus indicators; crisp system workhorse sans (`system-ui, -apple-system, "Segoe UI", sans-serif`) with `tabular-nums` alignment and monospace reserved exclusively for codes, timestamps, and IDs.

STORY: On `/login`, the operator immediately sees a grounded, authoritative sign-in book (no marketing fluff) with clear credential inputs, Bahasa Indonesia default copy (`ID`/`EN` toggle), and actionable failure messages; once signed in, the operator lands in a single coherent ERP workspace shell displaying their verified PostgreSQL user identity and explicit `S00_DEFAULT_DENY` boundary status before S01 unlocks tenant/entity switching.

FIRST VIEWPORT: Full-height two-column operational split on `/login` (left: compact operational security statement and authentic verification guarantees; right: focused credential form with primary `Masuk ke Athera` button); on `/` (authenticated shell), a 48px top scope bar + 232px single left navigation rail + crisp document work area leading directly with the session & security posture table and explicit next step.

FORM: Candidate 6 of 7 — Indonesian Commercial Ledger Sheet (crisp paper surfaces, hairline table rules, strict action-only deep teal accent, zero decorative cards or eyebrows); seed:fca3d83ef16641d8.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
