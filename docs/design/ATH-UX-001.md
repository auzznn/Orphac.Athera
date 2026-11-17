# ATH-UX-001 - Orphac Athera product experience and design quality

## North star
Create a serious, visually distinctive enterprise product for operational staff at Indonesian SMEs. A high-quality workspace prioritizes clarity, density, progressive disclosure, speed, context and precision over generic SaaS ornament. The first-session experience should teach users what to do next without overwhelming them. Use Impeccable for information architecture and visual refinement and Ponytail for minimal, maintainable component engineering.

## Audience and tasks
- Owner/executive: overdue liabilities, receivables, risk and working capital; drill into source data.
- Procurement: prepare/revise PO, obtain approval, follow receipts and matching exceptions.
- Warehouse: dock intake, quantity verification, stock status/location, picks and shipments.
- Finance: GRNI/AP/AR matching, tax decisions, ledger evidence, reconciliation and close.
- Organization administrator: assign least-privilege access and configure policies.

## Application shell
Use one coherent app-level navigation (e.g. Overview, Procurement, Inventory, Sales, Finance, Administration) based on user permissions. Tenant/legal entity scope switcher should be predictable, explicit and safe; avoid redundant sidebars, duplicated primary actions, context-free chatbot boxes and navigation competing for attention. Preserve route context and show current business document breadcrumbs/state. Make the work surface useful at desktop and usable on tablet; mobile is primarily read/light-action unless workflow requires more.

## Interaction conventions
- Transactions: list -> detail -> action or dedicated editor -> preview consequences -> submit -> approval/result evidence. Never create irrecoverable postings from accidental single-click controls.
- Data tables: column priority, filter/search, pagination, legible status, number/date formats, keyboard navigation, stable row identities, exports only if permitted.
- Forms: grouped commercial/financial fields, real-time validated quantities and currency formatting, summaries that explain implications, field-level errors, safe save drafts.
- Status: distinguish draft, pending approval, posted, blocked, reversed, canceled and reconciled; never use only color. Use specific copy: `Posting blocked: missing account mapping` instead of `Something went wrong`.
- Empty/loading/error states: explain next legal action; useful loading skeleton where appropriate; graceful no-permission/404 and connection failure states.
- Audit: every high-stakes detail view exposes origin, actor, approved revision, posting reference and correction linkage.
- Motion: short restrained transitions for navigation, list feedback, disclosure and progress; obey reduced-motion preferences. Avoid gratuitous dashboard scroll effects and slow animated modals.
- Accessibility: keyboard form operations, focus states, readable contrast, accessible labels, semantic tables, error associations.

## Design system
Start with PRODUCT.md (audience, voice, purpose, anti-references) and DESIGN.md (tokens, typography, surfaces, spacing, component rules). Keep brand tokens distinct from business status colors; no random gradients. Use shadcn/ui and Tailwind if they accelerate, but don't build a custom headless component framework in the first slice.

## Per-slice Impeccable workflow
1. `init` / `shape` (depending on installed skill syntax), define task flow and page architecture.
2. Build a working realistic interface with actual state, using representative Indonesian business currency and dates (clearly labeled sample data only in demos).
3. `critique` the completed user journey for hierarchy, friction, cognitive load, redundant navigation and generic visual patterns.
4. `audit` for responsiveness, accessibility and performance; `distill` for UI clutter; `layout` and `typeset` for craft.
5. `polish`, `adapt` and `harden` for errors, edge cases, long supplier names, large numbers, locale and empty states.
6. `animate` only for purposeful affordance/feedback. Screenshot at desktop and narrow viewport and store evidence if browser tools exist.

## UX acceptance
A novice can identify the primary action, understand document status and confidently correct mistakes; a power user can navigate quickly via search, filters and keyboard. Financial figures use exact currency format and source references. No decorative/fabricated metrics or generic empty charts.
