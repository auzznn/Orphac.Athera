# ATH-PO-GOV-001 - Product Owner authority and sign-off

**Principle:** Agent implementation authority != product ownership != Product Owner acceptance.

## What the Owner has preauthorized
The agent may independently design, code, run local/disposable tests, change local branch files, refactor within approved modular boundaries and make local Git commits for S00-S18. It may select low-risk library and code structure details when they do not contradict requirements or introduce material dependencies.

## What remains exclusively with the Product Owner
1. Approve the PRD, BRS and binding business-policy changes.
2. Approve material architecture changes, public commitments and new MVP scope.
3. Determine user value and business-behavior acceptance of every slice.
4. Confirm module ownership and critical invariants based on reviewed evidence.
5. Sign the formal Product Owner acceptance record (including date and reviewed SHA).
6. Authorize any production deployment, destructive migration, live financial activity, paid service purchase or external data transfer.
7. Authorize the final release candidate.

## Three distinct status tracks
- `IMPLEMENTATION`: NOT_STARTED | IN_PROGRESS | CODE_COMPLETE | BLOCKED.
- `ENGINEERING_VERIFICATION`: NOT_RUN | PASS | FAIL | BLOCKED (must record actual evidence).
- `PRODUCT_OWNER_ACCEPTANCE`: NOT_REQUESTED | PENDING | ACCEPTED | REJECTED. **Only the human Owner can set ACCEPTED.**

The agent may move to the next preauthorized slice after engineering PASS if there is no unresolved Owner-only decision, while leaving Product Owner acceptance PENDING. This is **workstream continuation**, not retroactive sign-off. Any downstream functionality relying on unverified security or accounting remains acceptance-blocked.

## Mandatory four-pillar acceptance review
A. **Business behavior**: approved user stories, normal/exception states, manual demonstration.
B. **Module ownership**: authoritative source of every fact, bounded context interfaces, no unauthorized cross-schema writes.
C. **Critical invariants**: authorization, tenant isolation, financial balancing/immutability, idempotency, quantity conservation, transaction atomicity and scope-specific rules.
D. **Acceptance evidence**: reproducible test outputs, DB integration logs, screenshots when relevant, exact immutable Git SHA, defect/exception register.

## Approval record fields
Document ID (`ATH-ACC-Sxx`), implementation and evidence SHA, design version, applicable PRD/BRS/ADR refs, test environment/build versions, outcome for each four-pillar review, defects and exemptions, independent reviewer names (if any), Owner decision/identity/date/signature, archival location. Blank until signed. An AI agent must never forge or pre-fill a signature/date. Signed records are never overwritten; append amendment/superseding signoff.

## Change requests
When a material decision is needed, create `docs/decisions/ATH-CR-<id>.md` with: question, affected requirements and slices, options, recommendation, benefit/cost/risk, default denied behavior, evidence, requested Owner decision. Stop the affected functionality only. Do not assume silence is approval.

## Audit and release discipline
Keep all design docs, logs and acceptance records in version control / secure artifact store with immutable snapshot. A signed acceptance of one SHA does not auto-approve later code changes or other slices. Production release is a distinct gate from feature acceptance; must include backup/restore proof, roll-forward/rollback drills, tenant security verification, environment isolation, and support/incident readiness.
