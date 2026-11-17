import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S05/screenshots");
const reviewDir = path.join(repoRoot, ".impeccable/review");

const TENANT_NUSA_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_NUSA_JKT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";
const ENTITY_NUSA_SBY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102";
const TENANT_SMDR_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01";
const ENTITY_SMDR_BDG_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb101";

const demoPassword = process.env.ATHERA_DEMO_PASSWORD ?? "AtheraDemo!2026";

async function signInAs(page: Page, email: string) {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/login");
  await page.getByLabel(/Email Perusahaan|Work Email/).fill(email);
  await page.getByLabel(/Kata Sandi|Password/).fill(demoPassword);
  await page.getByRole("button", { name: /Masuk ke Athera|Sign in to Athera/ }).click();
  await expect(page).toHaveURL(/\/$/);
}

async function signOut(page: Page) {
  await page.getByRole("button", { name: /Keluar|Sign out/ }).click();
  await expect(page).toHaveURL(/\/login$/);
}

async function selectOptionMatching(selectLocator: ReturnType<Page["locator"]>, pattern: RegExp) {
  const options = await selectLocator.locator("option").all();
  for (const opt of options) {
    const txt = (await opt.textContent()) ?? "";
    if (pattern.test(txt)) {
      const val = await opt.getAttribute("value");
      if (val !== null) {
        await selectLocator.selectOption(val);
        return;
      }
    }
  }
  throw new Error(`No option matching ${pattern} found in select`);
}

test.describe("S05 Purchase Orders, Approved Revisions and Four-Eyes", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
    fs.mkdirSync(reviewDir, { recursive: true });
  });

  test("S05-E2E-01 U1 drafts & submits multi-line PO, self-approval is blocked, U2 approves Rev 1, U1 amends Rev 2, U2 compares diff & approves, Rev 1 unchanged, U1 short-closes", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const destination = `Gudang E2E S05 ${stamp}`;

    // 1. U1 creates 2-line PO on NUSA-JKT and submits it
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/procurement`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText(/Pesanan Pembelian|Purchase Orders/);

    await page.getByRole("tab", { name: /Buat & Edit Draf PO|Create & Edit PO Draft/ }).click();
    await selectOptionMatching(page.locator("#po-supplier"), /PT-SUP-001/);
    await page.locator("#po-destination").fill(destination);

    // Line 1: ITM-PKG-001, 3 BOX (= 72 PCS) @ 480000 with PPN-12-DPP1112
    await selectOptionMatching(page.locator("#po-line-item-0"), /ITM-PKG-001/);
    await selectOptionMatching(page.locator("#po-line-uom-0"), /BOX/);
    await page.locator("#po-line-qty-0").fill("3");
    await page.locator("#po-line-price-0").fill("480000");
    await selectOptionMatching(page.locator("#po-line-tax-0"), /PPN-12-DPP1112/);

    // Line 2: ITM-RES-002, 2 BOX (= 72 PCS) @ 350000 with PPN-IN-11
    await page.getByRole("button", { name: /Tambah Baris Item|Add Line Item/ }).click();
    await selectOptionMatching(page.locator("#po-line-item-1"), /ITM-RES-002/);
    await selectOptionMatching(page.locator("#po-line-uom-1"), /BOX/);
    await page.locator("#po-line-qty-1").fill("2");
    await page.locator("#po-line-price-1").fill("350000");
    await selectOptionMatching(page.locator("#po-line-tax-1"), /PPN-IN-11/);

    await expect(page.getByTestId("po-editor-totals")).toContainText("Rp 2.140.000");
    await expect(page.getByTestId("po-editor-totals")).toContainText("Rp 235.400");
    await expect(page.getByTestId("po-editor-totals")).toContainText("Rp 2.375.400");

    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s05-po-editor-desktop.png"), fullPage: true });

    await page.getByRole("button", { name: /Simpan & Ajukan Persetujuan|Save & Submit/ }).click();

    // Self-approval blocked cleanly on pending PO submitted by U1
    const detailSheet = page.locator("#po-detail-sheet");
    await expect(detailSheet).toBeVisible();
    await expect(detailSheet).toContainText(/Menunggu Persetujuan|Pending Approval/);
    await expect(page.getByTestId("po-self-approval-blocked")).toBeVisible();
    await expect(page.getByRole("button", { name: /Setujui Revisi PO|Approve PO Revision/ })).toHaveCount(0);

    const poHeadingText = await detailSheet.locator("h2").innerText();
    const poMatch = /PO-\d{4}-\d{5}/.exec(poHeadingText);
    expect(poMatch).not.toBeNull();
    const createdPoNumber = poMatch![0];

    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s05-po-pending-sod-desktop.png"), fullPage: true });

    // 2. U2 independently approves Rev 1
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/procurement`);
    await page.locator("tbody tr", { hasText: createdPoNumber }).getByRole("button", { name: /Tinjau & Putuskan|Review & Decide/ }).click();
    await expect(detailSheet).toContainText(createdPoNumber);

    await page.locator("#po-decision-reason").fill(`Setuju empat mata Rev 1 ${stamp}`);
    await page.getByRole("button", { name: /Setujui Revisi PO|Approve PO Revision/ }).click();
    await expect(detailSheet).toContainText(/Disetujui|Approved/);
    await expect(detailSheet).toContainText("144");

    // 3. U1 creates & submits Amendment Rev 2 (raises Line 1 qty 3 -> 4 BOX and price 480k -> 495k)
    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/procurement`);
    await page.locator("tbody tr", { hasText: createdPoNumber }).getByRole("button", { name: /Kelola & Revisi|Manage & Revise/ }).click();
    await expect(detailSheet).toContainText(createdPoNumber);

    await page.locator("#po-amendment-reason").fill(`Kenaikan qty 4 box & harga 495rb ${stamp}`);
    await page.getByRole("button", { name: /Buat Draf Revisi Baru|Create Draft Amendment/ }).click();

    await expect(page.locator("#po-line-qty-0")).toBeVisible();
    await page.locator("#po-line-qty-0").fill("4");
    await page.locator("#po-line-price-0").fill("495000");
    await page.getByRole("button", { name: /Simpan & Ajukan Persetujuan|Save & Submit/ }).click();
    await expect(detailSheet).toContainText(/Rev 2/);
    await expect(detailSheet).toContainText(/Perbandingan Perubahan Revisi|Approval Comparison Diff/);

    // 4. U2 inspects Diff comparison (Rev 1 -> Rev 2), captures Desktop + Mobile PNGs, and approves Rev 2
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/procurement`);
    await page.locator("tbody tr", { hasText: createdPoNumber }).getByRole("button", { name: /Tinjau & Putuskan|Review & Decide/ }).click();

    await expect(detailSheet).toContainText(/Rev 1/);
    await expect(detailSheet).toContainText(/Rev 2/);
    await expect(detailSheet).toContainText(/Diubah|Modified/);

    const desktopClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some((el) => el.scrollWidth > el.clientWidth + 1),
    );
    expect(desktopClipped).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s05-po-diff-approval-desktop.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s05-po-diff-approval-desktop.png"), path.join(reviewDir, "desktop.png"));

    await page.setViewportSize({ width: 390, height: 844 });
    const mobileOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(mobileOverflow).toBe(false);
    const mobileInnerClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some((el) => el.scrollWidth > el.clientWidth + 1),
    );
    expect(mobileInnerClipped).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s05-po-mobile.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s05-po-mobile.png"), path.join(reviewDir, "mobile.png"));
    await page.setViewportSize({ width: 1280, height: 800 });

    await page.locator("#po-decision-reason").fill(`Revisi harga 495rb & 4 BOX disetujui ${stamp}`);
    await page.getByRole("button", { name: /Setujui Revisi PO|Approve PO Revision/ }).click();
    await expect(detailSheet).toContainText(/Rev 2.*Disetujui|Rev 2.*Approved/);

    // Verify historical Rev 1 snapshot stayed SUPERSEDED & unchanged (Rp 480.000)
    await detailSheet.getByRole("button", { name: /Rev 1/ }).click();
    await expect(detailSheet).toContainText(/Digantikan|Superseded/);
    await expect(detailSheet).toContainText("Rp 480.000");

    // 5. U1 short-closes remaining commitment cleanly
    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/procurement`);
    await page.locator("tbody tr", { hasText: createdPoNumber }).getByRole("button", { name: /Kelola & Revisi|Manage & Revise/ }).click();
    await page.locator("#po-close-reason").fill(`Penutupan sisa komitmen uji E2E S05 ${stamp}`);
    await page.getByRole("button", { name: /Tutup Sisa Komitmen \(Short-Close\)|Short-Close Remaining Commitment/ }).click();

    await expect(detailSheet).toContainText(/Ditutup Cepat \(Short-Close\)|Short-Closed/);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s05-po-closed-desktop.png"), fullPage: true });
  });

  test("S05-E2E-02 U2 sees NUSA-SBY read-only PO with Hash Snapshot, gets 403 on SMDR-BDG, and U1 gets 404 on SMDR", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/procurement`);

    await expect(page.getByText(/Mode baca saja|Read-only mode/)).toBeVisible();
    await expect(page.getByRole("tab", { name: /Buat & Edit Draf PO|Create & Edit PO Draft/ })).toHaveCount(0);

    await page.locator("tbody tr").first().getByRole("button", { name: /Lihat Rincian & Riwayat|Inspect Lines & History/ }).click();
    const detailSheet = page.locator("#po-detail-sheet");
    await expect(detailSheet).toBeVisible();
    await expect(detailSheet).toContainText("PO-2026-00001");
    await expect(detailSheet).toContainText(/Hash Snapshot|Snapshot Hash/);
    await expect(page.getByRole("button", { name: /Setujui Revisi PO|Approve PO Revision/ })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /Tutup Sisa Komitmen|Short-Close/ })).toHaveCount(0);

    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s05-po-readonly-desktop.png"), fullPage: true });

    // U2 has WorkspaceView on SMDR-BDG but zero procurement grants -> 403
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/procurement`);
    await expect(page.locator("main").getByRole("alert")).toContainText(/Izin Entitas Tidak Mencukupi|Insufficient Entity Permission/);

    // U1 has zero SMDR membership -> 404 without SMDR leakage
    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/procurement`);
    await expect(page.locator("main").getByRole("alert")).toContainText(/Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/);
    const bodyText = await page.locator("main").innerText();
    expect(bodyText).not.toContain("SMDR");
    expect(bodyText).not.toContain("Samudra");
  });
});
