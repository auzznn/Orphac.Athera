---
name: Orphac Athera
description: Calm, high-density Indonesian commercial ledger and dispatch-book ERP workspace built on archival paper surfaces, 1px hairline rules, and strict action-only Sumatra teal-jade accents.
colors:
  accent-primary: "#0e5a53"
  accent-primary-hover: "#0a4640"
  accent-primary-ink: "#ffffff"
  accent-tint: "#e7f1ef"
  bg-canvas: "#f6f5f0"
  bg-surface: "#ffffff"
  bg-chrome: "#eef0eb"
  bg-subtle: "#f1f3ee"
  border-hairline: "#d5d9d2"
  border-strong: "#b2b8ae"
  text-primary: "#171c1e"
  text-secondary: "#495256"
  text-muted: "#5d676b"
  status-ok-bg: "#e7f3ec"
  status-ok-text: "#115c38"
  status-ok-border: "#b6dbc5"
  status-warn-bg: "#fbf2df"
  status-warn-text: "#7b4900"
  status-warn-border: "#e5cb94"
  status-danger-bg: "#fbeaea"
  status-danger-text: "#941b20"
  status-danger-border: "#e5b7b9"
typography:
  headline:
    fontFamily: 'ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif'
    fontSize: "1.5rem"
    fontWeight: 600
    lineHeight: 1.333
    letterSpacing: "-0.025em"
  title:
    fontFamily: 'ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif'
    fontSize: "1.25rem"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "-0.025em"
  body:
    fontFamily: 'ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif'
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.4286
    letterSpacing: "0em"
  label:
    fontFamily: 'ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif'
    fontSize: "0.75rem"
    fontWeight: 600
    lineHeight: 1.333
    letterSpacing: "0em"
  code:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace"
    fontSize: "0.8125rem"
    fontWeight: 400
    lineHeight: 1.4
    fontFeature: "tnum"
rounded:
  sm: "4px"
  md: "6px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  2xl: "32px"
  3xl: "48px"
components:
  button-primary:
    backgroundColor: "{colors.accent-primary}"
    textColor: "{colors.accent-primary-ink}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "10px 16px"
    height: "44px"
  button-primary-hover:
    backgroundColor: "{colors.accent-primary-hover}"
  button-secondary:
    backgroundColor: "{colors.bg-surface}"
    textColor: "{colors.text-primary}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: "0px 10px"
    height: "36px"
  button-secondary-hover:
    backgroundColor: "{colors.bg-chrome}"
  button-icon:
    backgroundColor: "transparent"
    textColor: "{colors.text-secondary}"
    rounded: "{rounded.sm}"
    width: "36px"
    height: "36px"
  button-icon-hover:
    backgroundColor: "{colors.bg-chrome}"
  input-text:
    backgroundColor: "{colors.bg-surface}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"
    height: "44px"
  card-surface:
    backgroundColor: "{colors.bg-surface}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.md}"
    padding: "16px"
  card-warn:
    backgroundColor: "{colors.status-warn-bg}"
    textColor: "{colors.status-warn-text}"
    rounded: "{rounded.md}"
    padding: "16px"
  badge-warn:
    backgroundColor: "{colors.status-warn-bg}"
    textColor: "{colors.status-warn-text}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: "2px 10px"
  alert-danger:
    backgroundColor: "{colors.status-danger-bg}"
    textColor: "{colors.status-danger-text}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "12px 14px"
  nav-item-active:
    backgroundColor: "{colors.accent-primary}"
    textColor: "{colors.accent-primary-ink}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "8px 10px"
    height: "40px"
  nav-item-locked:
    backgroundColor: "transparent"
    textColor: "{colors.text-muted}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "8px 10px"
    height: "40px"
  locale-toggle:
    backgroundColor: "{colors.bg-surface}"
    textColor: "{colors.text-secondary}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: "2px"
---

# Design System: Orphac Athera

## Overview

**Creative North Star: "Indonesian Commercial Ledger Sheet"**

Orphac Athera treats enterprise operations like a calm, verifiable Indonesian commercial ledger and dispatch book rather than a promotional SaaS dashboard. Every screen pairs a warm archival paper canvas with crisp white operational sheets, cool slate navigation chrome, and tight `1px` hairline structural rules so procurement, warehouse, and finance staff can audit identity, scope, and record tables without visual noise.

Visual hierarchy relies on tonal paper stepping, explicit document status copy, and tabular numeric alignment rather than ornamental cards or elevated shadows. Sumatra teal-jade acts exclusively as a functional signal for primary submissions, active navigation items, the bilingual locale switch, and keyboard focus rings, leaving business warnings and errors in dedicated high-contrast semantic registers.

Confirmed visual rejections include gradient metric hero tiles, decorative avatars, unanchored modals, color-only status indicators, and uppercase eyebrow kickers above section titles.

**Key Characteristics:**
- Warm archival paper ground (`#f6f5f0`) stepped against cool slate chrome (`#eef0eb`) and crisp white work sheets (`#ffffff`).
- Flat surfaces separated strictly by `1px` structural hairlines (`#d5d9d2`) and stronger control strokes (`#b2b8ae`).
- Action-only Sumatra teal-jade (`#0e5a53`) isolated from semantic warning (`#7b4900`) and danger (`#941b20`) states.
- Bilingual Indonesian (`id-ID` default) and English (`en-US`) operational copy with `.font-code` monospace and `tabular-nums` reserved for identifiers and WIB timestamps.
- Direct, unadorned headings with `1.75px`-stroke vector icons (`lucide-react`) and explicit fail-closed boundary indicators.

## Colors

A single deep teal-jade accent anchors an archival paper neutral scale and three high-contrast semantic status registers, with every foreground/background pairing verified above `5.0:1` WCAG AA contrast.

### Primary
- **Sumatra Teal-Jade** (`accent-primary`, `#0e5a53`): Reserved strictly for primary submit/retry actions, the active navigation pill, the active locale toggle button, the brand emblem badge, text carets, checkbox accents, inline operational anchor icons, and `2px` `focus-visible` outlines.
- **Deepened Teal-Jade** (`accent-primary-hover`, `#0a4640`): Hover fill for primary buttons (`#0e5a53` -> `#0a4640`), delivering `10.70:1` contrast against white ink.
- **Pure White Ink** (`accent-primary-ink`, `#ffffff`): Foreground label and icon ink inside primary buttons, the brand emblem, and the active sidebar navigation link (`8.06:1` on `#0e5a53`).
- **Soft Teal-Jade Tint** (`accent-tint`, `#e7f1ef`): Text `::selection` highlight background paired with deep charcoal ink (`#171c1e`).

### Neutral
- **Warm Archival Paper** (`bg-canvas`, `#f6f5f0`): Full-viewport application canvas behind the main workspace and loading/error views.
- **Operational Sheet White** (`bg-surface`, `#ffffff`): Top scope header bar, login credential panel, ledger record table card, next-step card, inputs, secondary buttons, and segmented toggle frame.
- **Cool Slate-Rail Chrome** (`bg-chrome`, `#eef0eb`): Left navigation rail (`232px`), login security posture panel, environment indicator pill, skeleton pulse bar, and secondary/icon button hover fills.
- **Subtle Record Header** (`bg-subtle`, `#f1f3ee`): Top caption strip inside structured record table containers (`Rekam Identitas PostgreSQL`).
- **Hairline Structural Rule** (`border-hairline`, `#d5d9d2`): `1px` borders separating shell header, left rail, table rows, standard cards, and badge frames.
- **Strong Control Stroke** (`border-strong`, `#b2b8ae`): `1px` border on interactive text inputs, checkboxes, secondary outline buttons, and custom scrollbar thumbs.
- **Deep Charcoal Ink** (`text-primary`, `#171c1e`): Page headings, table values, primary body prose, form labels, and secondary button labels (`15.75:1` on canvas; `17.20:1` on white sheet).
- **Muted Slate Ink** (`text-secondary`, `#495256`): Subtitle leads, key-value row labels, navigation group titles, security bullet points, inactive locale buttons, and secondary header metadata (`7.33:1` on canvas).
- **Subtle Slate Muted** (`text-muted`, `#5d676b`): Input placeholder text and disabled/locked S01 module links (`5.06:1` on chrome; `5.80:1` on white sheet).

### Semantic Status
- **Verified / Ok Register** (`status-ok-bg` `#e7f3ec`, `status-ok-text` `#115c38`, `status-ok-border` `#b6dbc5`): Verified/live/eligible operational states (`7.06:1` text-on-wash); unused on unbound default-deny screens.
- **Warning / Default-Deny Register** (`status-warn-bg` `#fbf2df`, `status-warn-text` `#7b4900`, `status-warn-border` `#e5cb94`): Unbound scope badges, read-only boundary banners, restricted-entity notices, pending approval badges, and default-deny security callouts (`6.75:1` text-on-wash).
- **Danger / Error Register** (`status-danger-bg` `#fbeaea`, `status-danger-text` `#941b20`, `status-danger-border` `#e5b7b9`): Inline login failure alerts, sign-out error alerts, session verification failure card border/icon, and `aria-invalid="true"` input border shifts (`7.41:1` text-on-wash).

### Named Rules
**The Action-Only Accent Rule.** Sumatra Teal-Jade (`#0e5a53`) is restricted to interactive primary controls, active navigation state, the brand shield mark, inline anchor icons, and keyboard focus rings; never flood cards, banners, or decorative tiles with teal background fills.
**The Distinct Semantic Signal Rule.** Semantic status tokens (`status-ok`, `status-warn`, `status-danger`) stay strictly separate from brand teal and never rely on color alone—every badge, banner, or callout pairs tinted fill and `1px` matching border with an icon and explicit state text.

## Typography

**Display / UI Font:** `ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif` (native system workhorse sans; no external webfont loaded)
**Body Font:** `ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`
**Label / Mono Font:** `ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace` (applied via `.font-code`)

**Character:** Utilitarian, fast-rendering system typography tuned for dense Indonesian and English commercial scanning, tight heading tracking, and clear optical distinction between prose labels and fixed-width technical codes. Note: the direction contract (`OWN-WORLD`) named `system-ui, -apple-system, "Segoe UI", sans-serif`, whereas the shipped stylesheet prepends `ui-sans-serif` and appends `Roboto`.

### Hierarchy
- **Headline** (`600`, `1.5rem` / `24px` on `sm+` login view, `1.25rem` / `20px` on mobile, line-height `1.333`, letter-spacing `-0.025em`): Primary authentication page heading (`Masuk ke Sistem Operasional`). No oversized hero display tier exists in the workspace.
- **Title** (`600`, `1.25rem` / `20px` workspace `h1` with `-0.025em` tracking and `1.4` line-height; `1rem` / `16px` panel `h2` and error card `h1` with `1.5` line-height; `0.875rem` / `14px` sub-card `h2`): Workspace view headers and structural panel headings.
- **Body** (`400`–`500`, `0.875rem` / `14px`, line-height `1.4286`, `1.625` with `leading-relaxed` on multi-line callouts and security notes): Primary prose, form inputs, form labels (`500`), navigation links, and table row labels/values (`500` on non-mono cells).
- **Label** (`500`–`600`, `0.75rem` / `12px`, line-height `1.333`): Environment tag, scope status badges, table card header title, mobile table field labels, navigation group header, sign-out button, and `ID`/`EN` toggle labels (`tracking-wide` `0.025em`).
- **Code / Tabular Mono** (`400`, `0.8125rem` / `13px` in ledger table cells and `0.6875rem` / `11px` in header user email, `.font-code` + `tabular-nums`, line-height `1.4`): Email identifiers, locale codes (`id-ID`), WIB timestamps, and workspace status codes (`S00_DEFAULT_DENY`).

### Named Rules
**The Tabular Ledger Rule.** Machine identifiers, emails, status codes, WIB timestamps, and numeric ledger entries pair `.font-code` monospace with `font-variant-numeric: tabular-nums` so columns and key-value sheets align down the vertical scanline.
**The Direct Heading Rule.** Page and section headings enter directly in `semibold` (`600`) with `-0.025em` tracking without uppercase eyebrow kickers above them.

## Layout

Orphac Athera enforces two disciplined structural shells built on `1px` borders and Tailwind responsive breakpoints (`sm: 640px`, `md: 768px`, `lg: 1024px`).

**Authenticated Shell (`AppShell.tsx` + `(shell)/page.tsx` + `/w/[tenantId]/[entityId]/*`):** A sticky `48px` (`h-12`) full-width top scope bar on white sheet (`#ffffff`) displays the mobile drawer button (`< 1024px`), brand mark, Scope Switcher disclosure or unbound default-deny warning badge (`md+`), `LocaleToggle`, signed-in identity (`sm+`), and Sign Out button. Below `768px` (`md`), a compact full-width scope switcher strip docks directly beneath the header bar. Below the header, a single `232px` left navigation rail on cool slate chrome (`#eef0eb`) flanks the scrollable warm paper workspace (`#f6f5f0`), where main content centers inside `max-w-4xl` (`896px`) on session overview or `max-w-5xl` (`1024px`) on dense ledger workspace/master-data tables with `16px` mobile, `24px` `sm`, and `32px` `lg` padding. A keyboard `skip-to-main` link jumps directly to `#main-content`.

**Authentication Split (`/login`):** Shares the identical `48px` top header bar (showing brand mark, PostgreSQL environment pill on `sm+`, and `LocaleToggle`) above a full-height 12-column grid (`lg:grid-cols-12`): `5` columns left security posture aside on cool slate chrome (`#eef0eb`) and `7` columns right focused credential form (`max-w-md` / `448px`) on white sheet (`#ffffff`). Below `1024px`, the form stacks first (`order-1`) above the security boundary checklist (`order-2`) so operators reach credentials without scrolling past explanatory copy.

**Ledger Table & Grid Rhythm:** Key-value record tables switch from stacked vertical `block` rows below `640px` to aligned horizontal `table-row` entries on `sm+` with a fixed `240px` (`sm:w-60`) label column. Callout cards below the table form a 2-column grid (`grid-cols-1 md:grid-cols-2`, `16px` gap) separated from the table by a `1px` top hairline and `20px` top padding.

### Named Rules
**The Single-Rail Workspace Rule.** Primary navigation occupies one `232px` left rail on desktop (collapsing into an on-demand slide-over drawer below `1024px`)—never secondary inner sidebars or competing horizontal nav bars.

## Elevation & Depth

Desktop surfaces are strictly flat at rest. Hierarchy is conveyed exclusively through tonal surface stepping—warm paper ground (`#f6f5f0`) -> cool slate chrome (`#eef0eb`) -> white operational sheet (`#ffffff`) -> subtle header strip (`#f1f3ee`)—anchored by `1px` solid hairline borders (`#d5d9d2`) and stronger interactive strokes (`#b2b8ae`).

### Shadow Vocabulary
- **Mobile Navigation Drawer (`--shadow-lg`):** `box-shadow: 0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1)` — Applied exclusively to the off-canvas navigation `<aside>` when opened below `1024px` (`lg:shadow-none`), backed by a `rgba(0, 0, 0, 0.35)` scrim (`bg-black/35`) that dismisses on click or `Escape`.

### Named Rules
**The Hairline Paper Rule.** Surfaces are flat at rest and separated by tonal paper steps and `1px` structural hairlines (`#d5d9d2`), reserving drop shadows strictly for transient mobile drawer overlays.

## Shapes

Geometry stays crisp and low-radius across all controls and containers:
- **Controls & Pills (`--radius-sm`, `4px`):** Applied to buttons, text inputs, alert banners, scope badges, environment pills, the `28px x 28px` brand emblem, sidebar navigation items, and the `LocaleToggle` outer frame.
- **Sheets & Cards (`--radius-md`, `6px`):** Applied to record table containers (paired with `overflow-hidden` so internal `#f1f3ee` header strips and `border-b` rules clip cleanly), loading/error cards, and post-table callout panels.
- **Concentric Toggle Inset (`3px`):** Inside `LocaleToggle`, inner buttons (`min-h-9 min-w-10`) sit inside a `4px` frame with `2px` (`p-0.5`) padding using a `3px` inner radius (`rounded-[3px]`).
- **Stroke Hierarchy:** `1px solid` `--border-hairline` (`#d5d9d2`) bounds containers, tables, and inactive frames; `1px solid` `--border-strong` (`#b2b8ae`) frames interactive inputs, checkboxes, and secondary buttons; `1px solid` `--status-danger-text` (`#941b20`) replaces the stroke when `aria-invalid="true"`.

## Components

Every component is self-contained, hairline-bounded, keyboard-operable (`2px solid #0e5a53` `focus-visible` outline with `2px` offset), and free of gratuitous motion (`150ms cubic-bezier(0.4, 0, 0.2, 1)` color transitions only, suppressed under `prefers-reduced-motion: reduce`).

### Buttons
Confidence through crisp flat contrast and generous hit targets rather than elevation.
- **Shape:** Subtle square corners (`4px` radius via `--radius-sm`).
- **Primary (`button-primary`):** Sumatra Teal-Jade (`#0e5a53`) background, pure white (`#ffffff`) semibold `14px` text, `44px` min-height with `10px 16px` padding on form submission (`40px` min-height, `0 14px` padding on compact recovery actions).
- **Hover / Focus:** Transitions over `150ms` to deepened teal-jade (`#0a4640`); disabled state retains fill at `opacity: 0.6` with `not-allowed` cursor; `focus-visible` renders `2px solid #0e5a53` with `2px` offset.
- **Secondary (`button-secondary`) & Icon (`button-icon`):** White sheet (`#ffffff`) fill with `1px solid #b2b8ae` stroke and `#171c1e` `12px`/`14px` text (`36px`–`40px` height), shifting to `#eef0eb` on hover; icon buttons (password eye reveal, mobile drawer toggle) use `36px x 36px` square footprint with `#495256` or `#171c1e` `16px` `lucide-react` icons (`1.75px` stroke).

### Chips / Badges & Alerts
Explicit state framing using semantic washes, matching `1px` borders, and mandatory `1.75px`-stroke icons.
- **Scope Status Badge (`badge-warn`) & Disclosure Switcher:** When zero workspaces are active, renders amber parchment wash (`#fbf2df`), `1px solid #e5cb94` border, `#7b4900` `12px` medium text paired with `14px` `Building2` SVG icon. When authorized entities exist, the header displays a bordered Scope Switcher disclosure (`aria-expanded`, `aria-controls`, `Escape` focus return) showing `{tenant.code} · {entity.code} · {currency}` in `.font-code tabular-nums`. Collapses cleanly into a full-width top sub-strip below `768px`.
- **Three-State Module Readiness & Governance Badges:** Shipped/live states (`Tersedia (S01–S04)`, `Aktif`, `Diposting`, `Terbuka`, `Disetujui`, `Diterima`, `Diproses`) use emerald `status-ok` (`#e7f3ec`/`#115c38`/`#b6dbc5`) paired with `CheckCircle2`; permitted-future slices (`PLANNED_S05+`), pending approvals, `Draf` composer states, closed fiscal periods (`Ditutup`), `UNREVIEWED_CONFIGURABLE` tax rules (`OD-07`), restricted-entity warnings, and pending invitations use amber `status-warn` paired with `Clock` or `RotateCcw`; ungranted/locked capabilities and non-eligible entity policies use `--bg-subtle`/`--border-hairline` paired with `Lock`; deactivated masters (`Nonaktif`), hard-locked periods (`Terkunci`), reversed journals (`Telah Dibalik`), unbalanced variances, and rejected/revoked/failed states use `status-danger` paired with `AlertCircle` or `RotateCcw`.
- **Environment Pill:** Cool slate fill (`#eef0eb`), `1px solid #d5d9d2` border, `#495256` `12px` medium text (`4px` radius, `2px 8px` padding).
- **Inline Danger Alert (`alert-danger`):** Crimson wash (`#fbeaea`), `1px solid #e5b7b9` border, `#941b20` `14px` text (`4px` radius, `12px 14px` padding) with `role="alert"` and `16px` `AlertCircle` icon.

### Cards / Containers
Structured archival sheets clipped cleanly to `6px` corners with zero rest shadows.
- **Corner Style:** Soft crisp corners (`6px` radius via `--radius-md`) with `overflow-hidden` on multi-band table containers.
- **Background & Border:** Operational white (`#ffffff`) with `1px solid #d5d9d2` border for record sheets, loading skeletons, and next-step cards; `#fbf2df` background with `1px solid #e5cb94` border for default-deny warning callouts; `#ffffff` with `1px solid #e5b7b9` border for full-screen session errors.
- **Header Strip & Row Rhythm:** Ledger table cards open with a `10px 16px` `#f1f3ee` strip (`12px` semibold title + `16px` `#0e5a53` `ShieldCheck` icon) above `border-b` `#d5d9d2` rows (`12px 16px` desktop cell padding, `240px` key column, `.font-code tabular-nums` `13px` values).
- **Internal Padding:** `16px` (`p-4`) on operational callout cards; `24px` (`p-6`) on centered loading/error panels.

### Inputs / Fields
High-legibility operational fields designed for error association and keyboard flow.
- **Style:** `44px` min-height (`min-h-11`), `#ffffff` background, `1px solid #b2b8ae` border, `4px` radius (`--radius-sm`), `8px 12px` padding (`pr-12` when hosting the `36px` password visibility toggle), `#171c1e` `14px` text, `#5d676b` placeholder, `#0e5a53` text caret; checkboxes (`16px x 16px`) use `accent-color: #0e5a53`.
- **Focus:** `2px solid #0e5a53` `focus-visible` outline (`2px` offset).
- **Error:** `aria-invalid="true"` shifts the stroke to `#941b20` and links `aria-describedby` to `#login-error-banner`.

### Navigation
Single-rail navigation anchored by a `48px` sticky scope bar.
- **Top Bar & Rail:** `48px` sticky `#ffffff` header (`border-b #d5d9d2`) above a `232px` `#eef0eb` sidebar (`border-r #d5d9d2`, `14px` padding) hosting global links (`/`, `/workspaces`, `/invitations`) above a `12px` semibold module group title (`#495256`).
- **Active / Shipped / Planned Entity Modules:** When inside an authorized `/w/[tenantId]/[entityId]` workspace, permitted live S02–S04 modules (`partners`, `catalog`, `approvals`, `finance`, `admin`) become clickable `<Link>` items highlighting the active route with a `40px` min-height `#0e5a53` pill (`4px` radius, `#ffffff` semibold `14px` text, `aria-current="page"`). Unopened S05+ modules remain `disabled` buttons in muted `#5d676b` ink paired with `Segera`/`Planned` or trailing `14px` `Lock` icon.
- **Mobile Treatment & Ledger Sheets:** Below `1024px`, the sidebar hides behind a `36px x 36px` header menu toggle and slides out as a full-height drawer (`z-40`) with a `36px x 36px` close button (`--shadow-lg` + `rgba(0,0,0,0.35)` click/Escape backdrop). Across `/workspaces`, `/partners`, `/catalog`, `/finance`, `/approvals`, and `/admin`, desktop tables reflow cleanly below `768px` (`md:table-row`) with inline field labels, right-aligned `.font-code tabular-nums` Debit/Credit accounting columns, contextual inspection/reversal sheets below/beside tables (no modal sprawl), and zero horizontal scroll.

### Bilingual Locale Segmented Control (LocaleToggle)
A compact, signature switch keeping `ID` (`id-ID`) and `EN` (`en-US`) language state instantly accessible in every top bar.
- **Structure:** Inline flex group (`role="group"`) with `#ffffff` surface, `1px solid #d5d9d2` border, `4px` radius, and `2px` inset padding.
- **Segments:** `36px` min-height by `40px` min-width buttons (`3px` inner radius, `12px` semibold `tracking-wide` labels, `aria-pressed`). Active locale takes `#0e5a53` fill with `#ffffff` text; inactive locale renders `#495256` text with `#eef0eb` hover fill over `150ms`.

## Do's and Don'ts

### Do:
- **Do** preserve the four-tier paper surface ladder (`#f6f5f0` canvas, `#eef0eb` navigation chrome, `#ffffff` operational sheet, `#f1f3ee` table header strips) separated by `1px solid #d5d9d2` hairlines.
- **Do** format machine identifiers, emails, status codes, WIB timestamps, and currency/quantity cells with `.font-code` and `tabular-nums` (`font-variant-numeric: tabular-nums`) so rows align down scanlines.
- **Do** pair every semantic status (`status-warn`, `status-danger`, `status-ok`) with an explicit `1.75px`-stroke `lucide-react` icon, `1px` matching border, and clear bilingual copy rather than color alone.
- **Do** maintain `36px`–`44px` minimum interactive heights and preserve the global `2px solid #0e5a53` `focus-visible` outline (`2px` offset) across every link, input, and button.
- **Do** disable smooth scrolling and pulse animations under `@media (prefers-reduced-motion: reduce)`.

### Don't:
- **Don't** add drop shadows to desktop cards, record tables, inputs, or buttons; shadows belong strictly on transient mobile drawer overlays below `1024px`.
- **Don't** apply Sumatra Teal-Jade (`#0e5a53`) to decorative metric banners, background gradients, or warning states; reserve it strictly for actions, active navigation, brand shield, inline anchor icons, and focus rings.
- **Don't** place uppercase eyebrow kickers above headings or fabricate placeholder KPI dashboard tiles before real ledger data exists.
- **Don't** ship vague failure messages ("Something went wrong"); display concrete server/boundary states and next legal actions via `role="alert"` and `aria-describedby`.
- **Don't** substitute unicode/emoji glyphs for `1.75px`-stroke `lucide-react` vector icons (`aria-hidden="true"`).
