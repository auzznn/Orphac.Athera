import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S06/screenshots");
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

async function assertNoLayoutClipping(page: Page) {
  const docOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth > window.innerWidth + 1,
  );
  expect(docOverflow).toBe(false);

  const innerClipped = await page.evaluate(() =>
    Array.from(document.querySelectorAll('[class*="overflow-x-auto"]')).some(
      (el) => el.scrollWidth > el.clientWidth + 1,
    ),
  );
  expect(innerClipped).toBe(false);
}

test.describe("S06 Dock Custody & Receiving Gate One (Unvalued)", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
    fs.mkdirSync(reviewDir, { recursive: true });
  });

  test("S06-E2E-01 Partial arrival, unvalued rejection split, OD-01 overdelivery hold & release block, and zero GL/GRNI proof", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const sj1 = `SJ-JKT-${stamp}-A`;
    const sj2 = `SJ-JKT-${stamp}-B`;

    // 1. U1 creates & submits a fresh 2 BOX (= 48 PCS) approved stock PO on NUSA-JKT
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/procurement`);
    await page.getByRole("tab", { name: /Buat & Edit Draf PO|Create & Edit PO Draft/ }).click();
    await selectOptionMatching(page.locator("#po-supplier"), /PT-SUP-001/);
    await page.locator("#po-destination").fill(`Dermaga Utama S06 ${stamp}`);
    await selectOptionMatching(page.locator("#po-line-item-0"), /ITM-PKG-001/);
    await selectOptionMatching(page.locator("#po-line-uom-0"), /BOX/);
    await page.locator("#po-line-qty-0").fill("2");
    await page.locator("#po-line-price-0").fill("480000");
    await selectOptionMatching(page.locator("#po-line-tax-0"), /PPN-12-DPP1112/);
    await page.getByRole("button", { name: /Simpan & Ajukan Persetujuan|Save & Submit/ }).click();

    const poDetailSheet = page.locator("#po-detail-sheet");
    await expect(poDetailSheet).toBeVisible();
    const poHeadingText = await poDetailSheet.locator("h2").innerText();
    const poMatch = /PO-\d{4}-\d{5}/.exec(poHeadingText);
    expect(poMatch).not.toBeNull();
    const createdPoNumber = poMatch![0];

    // 2. U2 independently approves Rev 1 of the PO
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/procurement`);
    await page
      .locator("tbody tr", { hasText: createdPoNumber })
      .getByRole("button", { name: /Tinjau & Putuskan|Review & Decide/ })
      .click();
    await page.locator("#po-decision-reason").fill(`Approve PO for S06 dock receiving ${stamp}`);
    await page.getByRole("button", { name: /Setujui Revisi PO|Approve PO Revision/ }).click();
    await expect(poDetailSheet).toContainText(/Disetujui|Approved/);

    // 3. U1 signs in and opens Gate 1 Dock Receiving on NUSA-JKT
    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/receiving`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText(
      /Penerimaan Dermaga|Dock Custody/,
    );

    // 4. Arrival #1: Partial delivery 1 BOX (= 24 PCS)
    await page.getByRole("tab", { name: /Catat Kedatangan|Record Dock Arrival/ }).click();
    await selectOptionMatching(page.locator("#rcv-po-select"), new RegExp(createdPoNumber));
    await page.locator("#rcv-delivery-note").fill(sj1);
    await page.locator("#rcv-station").fill("Dermaga Utama Jakarta · Pintu 2");
    await page.locator("#rcv-vehicle-ref").fill("B 9104 UX · Segel A-09");
    await selectOptionMatching(page.locator("#rcv-line-uom-0"), /BOX/);
    await page.locator("#rcv-line-qty-0").fill("1");
    await expect(page.locator("form")).toContainText(/24 PCS/);

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "s06-arrival-composer-desktop.png"),
      fullPage: true,
    });

    await page.locator("#rcv-submit-arrival").click();
    await expect(page.getByTestId("rcv-arrival-success")).toBeVisible();
    await expect(page.locator("#rcv-detail-sheet")).toContainText(sj1);
    await expect(page.getByTestId("gate1-gl-count-card")).toContainText("0");

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "s06-receipt-ledger-desktop.png"),
      fullPage: true,
    });

    // 5. Dock Check Split on Arrival #1: 20 PCS released + 4 PCS rejected unvalued
    await page.locator("#dock-pending-0").fill("0");
    await page.locator("#dock-held-0").fill("0");
    await page.locator("#dock-released-0").fill("20");
    await page.locator("#dock-rejected-0").fill("4");
    await page.locator("#dock-reason-0").selectOption("DAMAGED_PACKAGING");
    await page.locator("#dock-notes-0").fill("4 PCS kemasan basah ditolak tanpa nilai");
    await page.locator("#dock-evidence-0").fill(`BA-QC-${stamp}-1`);
    await expect(page.getByTestId("dock-conservation-0")).toContainText(
      /Seimbang dengan Fisik Tiba|Matches Physical Delivery/,
    );
    await page.locator("#dock-submit-0").click();
    await expect(page.locator("#rcv-detail-sheet")).toContainText(/Dirilis ke Gate 2|Released to Gate 2/);
    await expect(page.locator("#rcv-detail-sheet")).toContainText(/(?:Cek|Pend) 0 · (?:Tahan|Hold) 0 · (?:Rilis|Rel) 20 · (?:Tolak|Rej) 4/);

    // 6. Arrival #2: 2 BOX (= 48 PCS) exceeds 28 PCS remaining PO cap -> 20 PCS OD-01 hold
    await page.getByRole("tab", { name: /Catat Kedatangan|Record Dock Arrival/ }).click();
    await selectOptionMatching(page.locator("#rcv-po-select"), new RegExp(createdPoNumber));
    await page.locator("#rcv-delivery-note").fill(sj2);
    await selectOptionMatching(page.locator("#rcv-line-uom-0"), /BOX/);
    await page.locator("#rcv-line-qty-0").fill("2");
    await expect(page.getByTestId("arrival-od01-warning-0")).toContainText("20 PCS");
    await page.locator("#rcv-submit-arrival").click();

    const detailSheet = page.locator("#rcv-detail-sheet");
    await expect(detailSheet).toContainText(sj2);
    await expect(detailSheet).toContainText("OD-01 +20 PCS");

    // 7. Attempt releasing 30 PCS (above 28 cap) -> live cap warning AND server OD-01 409 block
    await page.locator("#dock-pending-0").fill("0");
    await page.locator("#dock-held-0").fill("18");
    await page.locator("#dock-released-0").fill("30");
    await page.locator("#dock-rejected-0").fill("0");
    await page.locator("#dock-reason-0").selectOption("OVERDELIVERY_OD01_HOLD");
    await expect(page.getByTestId("dock-conservation-0")).toContainText(
      /Melebihi batas PO|Exceeds approved PO cap/,
    );
    await page.locator("#dock-submit-0").click();
    await expect(page.locator("#dock-error-0")).toContainText(/OD-01/);

    // 8. Apply "Release Within PO Cap" preset -> releases 28, holds 20 in DOCK_HELD
    await page.locator("#dock-preset-release-0").click();
    await expect(page.locator("#dock-released-0")).toHaveValue("28");
    await expect(page.locator("#dock-held-0")).toHaveValue("20");
    await page.locator("#dock-notes-0").fill("28 PCS dirilis ke Gate 2; 20 PCS ditahan OD-01");
    await page.locator("#dock-submit-0").click();

    await expect(detailSheet).toContainText(
      /(?:Cek|Pend) 0 · (?:Tahan|Hold) 20 · (?:Rilis|Rel) 28 · (?:Tolak|Rej) 0/,
    );
    await expect(page.getByTestId("gate1-gl-count-card")).toContainText("0");

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "s06-dock-inspection-od01-desktop.png"),
      fullPage: true,
    });
    fs.copyFileSync(
      path.join(evidenceDir, "s06-dock-inspection-od01-desktop.png"),
      path.join(reviewDir, "desktop.png"),
    );

    // Mobile 390x844 check & screenshot
    await page.setViewportSize({ width: 390, height: 844 });
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "s06-receiving-mobile.png"),
      fullPage: true,
    });
    fs.copyFileSync(
      path.join(evidenceDir, "s06-receiving-mobile.png"),
      path.join(reviewDir, "mobile.png"),
    );
    await page.setViewportSize({ width: 1280, height: 800 });

    // 9. Verify U2 finance ledger contains zero Gate 1 GRN/GRNI postings
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/finance`);
    await expect(page.locator("main")).not.toContainText(sj1);
    await expect(page.locator("main")).not.toContainText(sj2);
  });

  test("S06-E2E-02 U2 views NUSA-SBY seeded SJ-SBY-DEMO-001 read-only, gets 403 on SMDR-BDG, and U1 gets 404 on SMDR", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/receiving`);

    await expect(page.getByTestId("receiving-readonly-banner")).toBeVisible();
    await expect(page.getByRole("tablist")).toHaveCount(0);
    await expect(page.locator("#dock-submit-0")).toHaveCount(0);
    await expect(page.locator("main")).toContainText("SJ-SBY-DEMO-001");
    await expect(page.locator("#rcv-detail-sheet")).toContainText(
      /Menunggu Cek Fisik|Pending Dock Check/,
    );
    await expect(page.getByTestId("gate1-gl-count-card")).toContainText("0");

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "s06-readonly-sby-desktop.png"),
      fullPage: true,
    });

    // U2 has WorkspaceView on SMDR-BDG without receiving grants -> 403
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/receiving`);
    await expect(page.locator("main").getByRole("alert")).toContainText(
      /Izin Entitas Tidak Mencukupi|Insufficient Entity Permission/,
    );

    // U1 holds zero SMDR membership -> 404 without leaking foreign entity names
    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/receiving`);
    await expect(page.locator("main").getByRole("alert")).toContainText(
      /Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/,
    );
    const bodyText = await page.locator("main").innerText();
    expect(bodyText).not.toContain("SMDR");
    expect(bodyText).not.toContain("Samudra");
    expect(bodyText).not.toContain("SJ-SBY");
  });
});
