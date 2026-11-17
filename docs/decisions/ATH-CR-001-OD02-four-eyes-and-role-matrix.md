# ATH-CR-001 — Open Decision Request OD-02: Four-Eyes Thresholds, Role Matrix & SME Exceptions

- **Decision ID:** `OD-02` (`docs/product/ATH-OPEN-DECISIONS-001.md`)
- **Affected Slices:** `S02` (Organization Administration & Approval Core), `S05`, `S09`, `S10`, `S14`
- **Status:** **OPEN — Awaiting Product Owner Decision**

---

## 1. Decision Question
1. Which monetary value thresholds require dual approval (four-eyes) versus single-person execution across PO (`S05`), AP three-way match variance (`S09`), returns (`S10`), and billing/credit adjustments (`S14`)?
2. Which role combinations are mutually incompatible (Segregation of Duties matrix across Purchasing Requestor, Approver, Warehouse Receiver, AP Accountant, Payment Releaser)?
3. Should small-team SMEs ever be permitted an exception/waiver for self-approval under emergency audit controls, or must self-approval remain unconditionally prohibited?

## 2. Options Considered
- **Option A (Fail-Closed Default Implemented in S02):** Unconditionally forbid self-approval (`decided_by_user_id <> requested_by_user_id`) at both service and PostgreSQL `CHECK` constraint levels for all approval-governed actions (`SENSITIVE_ACCESS_CHANGE`, `HIGH_RISK_PRIVILEGE_ESCALATION`), allow only whitelisted permissions/role templates, require distinct authorized `approval.decide` holders, and require TOTP MFA verification for `HIGH_RISK_PRIVILEGE_ESCALATION`.
- **Option B (Configurable Monetary & Small-Team SoD Policy):** Allow PO-signed legal-entity thresholds above which four-eyes applies, plus formally logged compensating dual-signoff exceptions if explicitly approved by the Product Owner.

## 3. Recommended Option & Safe Default Behavior Until Signed
- **Safe Default Active Now:** Option A (strictly fail-closed; zero self-approval paths; no custom waiver bypass). Any future monetary threshold rules or SoD matrix overrides will be configured in subsequent slices only upon written Product Owner approval.

## 4. Product Owner Decision Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
