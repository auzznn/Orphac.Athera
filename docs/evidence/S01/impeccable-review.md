# ATH-S01 Impeccable UX & Finish Review

**Mode:** `operate` (extending established `DESIGN.md` "Indonesian Commercial Ledger Sheet" direction `seed:fca3d83ef16641d8`)  
**Detector (`impeccable detect --json apps/web/src`):** `[]` (zero anti-pattern violations)

## Round 1 -> Material Fixes Applied & Verified (`impeccable-finish-reviewer` `6ea971c8`)
Round 1 flagged 7 material findings (`disposition: fix`), all of which were implemented, re-captured across desktop (`1280x800`) and mobile (`390x844`), and scored **`resolved`** in Round 2 -> **`disposition: ship`**:

1. **Module readiness differentiation & badge icons (`w/[tenantId]/[entityId]/page.tsx`, `workspaces/page.tsx`) — RESOLVED:** Live `AVAILABLE` (`Tersedia (S01)`) and tenant `Aktif` badges pair `status-ok` (`#e7f3ec`/`#115c38`) with `CheckCircle2` (`strokeWidth={1.75}`); permitted unshipped modules (`PLANNED_S02`, `PLANNED_S10–S13`) use warm `status-warn` + `Clock`; ungranted modules use `Lock`; effective permission chips match neutral `--bg-subtle`/`--border-hairline`.
2. **Disabled sidebar module ink (`AppShell.tsx`) — RESOLVED:** Unshipped disabled sidebar buttons use muted `text-[var(--text-muted)]` alongside `Segera`/`Planned`, keeping clickable navigation clearly distinguished.
3. **Directory column proportions & `<768px` inline labels (`workspaces/page.tsx`) — RESOLVED:** Consolidated desktop directory into 4 balanced columns (`Entitas Hukum`, `NPWP & Konteks Fiskal`, `Izin Aktif`, `Tindakan`) with horizontal pill flow and zero desktop horizontal overflow (`scrollWidth <= clientWidth` asserted in Playwright); stacked `<768px` rows include explicit inline `NPWP:`, `Mata Uang & Zona Waktu:`, and `Izin Aktif` labels. Captured `s01-directory-mobile.png` (`390x844`).
4. **Drawer close hit target & mobile `Keluar` wrapping (`AppShell.tsx`) — RESOLVED:** Raised mobile drawer close button to `h-9 w-9` (`36px`) and added `whitespace-nowrap shrink-0` to header sign-out button.
5. **Scope Switcher disclosure ARIA & alignment (`AppShell.tsx`) — RESOLVED:** Replaced partial menu ARIA attributes with disclosure triggers (`aria-expanded`, `aria-controls`, `Escape` focus return) anchored inside each trigger wrapper (`scope-switcher-panel-desktop` and `scope-switcher-panel-mobile`).
6. **`404` vs `5xx` error separation & semantic border (`w/[tenantId]/[entityId]/page.tsx`) — RESOLVED:** Kept non-leaking `Ruang Kerja Tidak Tersedia` strictly for `404`, separated `403` permission errors and `5xx`/network failures (with `Retry` button), and applied `border-[var(--status-danger-border)]`.
7. **Icon stroke weight & `.font-code` currency (`AppShell.tsx`, `w/[tenantId]/[entityId]/page.tsx`) — RESOLVED:** Added `strokeWidth={1.75}` on `ChevronDown`/`Check` and `.font-code tabular-nums` on `functionalCurrencyCode` in the entity subheader.

## Visual & Automated Artifacts
- [s01-directory-desktop.png](./screenshots/s01-directory-desktop.png)
- [s01-directory-mobile.png](./screenshots/s01-directory-mobile.png)
- [s01-entity-desktop.png](./screenshots/s01-entity-desktop.png)
- [s01-entity-mobile.png](./screenshots/s01-entity-mobile.png)
- [s01-denial-desktop.png](./screenshots/s01-denial-desktop.png)
- [s01-no-access-desktop.png](./screenshots/s01-no-access-desktop.png)
- **Reviewer disposition note:** `ship` verdict covers the 7 resolved findings across the 6 captured S01 views and changed source files (`/`, `/login`, open-drawer/open-panel states, and `5xx` failure views were outside this fix-pass capture set).
