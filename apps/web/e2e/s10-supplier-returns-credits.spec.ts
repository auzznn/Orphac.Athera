import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S10/screenshots");

const TENANT_NUSA_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_NUSA_JKT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";
const ENTITY_NUSA_SBY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102";

const demoPassword = process.env.ATHERA_DEMO_PASSWORD ?? "AtheraDemo!2026";

async function signInAs(page: Page, email: string) {
  await page.setViewportSize({ width: 1440, height: 900 });
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

test.describe("S10 Supplier Returns, Replacements & Financial Corrections", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
  });

  test("S10-E2E-01 M1 unvalued dock rejection executes with zero GL, issues RPA, receives replacement & Gate 2 values it", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const rpaDeliveryNote = `SJ-RPA-${stamp}`;

    await signInAs(page, "rina.utami@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);

    await expect(page.getByTestId("ret-governance-banner")).toBeVisible();
    await page.locator("#ret-matrix-toggle").click();
    await expect(page.locator("#ret-matrix-panel")).toBeVisible();
    await expect(page.locator("#ret-matrix-panel")).toContainText("M7");
    await assertNoLayoutClipping(page);
    await page.screenshot({
      path: path.join(evidenceDir, "01-returns-overview-and-matrix-desktop.png"),
      fullPage: true,
    });

    // Prepare M1 return for ITM-S10-SEAL-01 (2 PCS rejected)
    const m1Row = page.locator("#ret-queue-m1-table tbody tr", { hasText: "ITM-S10-SEAL-01" }).first();
    await expect(m1Row).toBeVisible();
    await m1Row.getByRole("button", { name: /Buat Retur|Prepare Return/ }).click();

    await expect(page.locator("#ret-composer")).toBeVisible();
    await page.locator("#ret-composer-resolution").selectOption("REPLACEMENT_ONLY");
    await page.locator("#ret-composer-qty").fill("2");
    await page.locator("#ret-composer-carrier").fill(`TRK-M1-${stamp}`);
    await page.locator("#ret-preview-btn").click();

    const preview = page.getByTestId("ret-live-preview");
    await expect(preview).toBeVisible();
    await expect(preview).toContainText("M1");
    await expect(preview).toContainText("Rp");
    await page.locator("#ret-save-draft-btn").click();

    const sheet = page.locator("#ret-detail-sheet");
    await expect(sheet).toBeVisible();
    await expect(sheet.locator("h2")).toHaveText(/DRT-\d{4}-\d+/);
    const draftRef = (await sheet.locator("h2").textContent())?.trim() ?? "";
    expect(draftRef).toMatch(/DRT-\d{4}-\d+/);

    await page.locator("#ret-submit-btn").click();
    await expect(sheet).toContainText(/Menunggu Persetujuan|Pending Approval/);
    await expect(page.getByTestId("ret-sod-notice")).toBeVisible();
    await signOut(page);

    // U2 approves four-eyes
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);
    await page.locator("#ret-tab-returns").click();
    const retRow = page.locator("#ret-returns-table tbody tr", { hasText: draftRef }).first();
    await retRow.getByRole("button", { name: /Buka Detail|Open Detail/ }).click();
    await page.locator("#ret-approve-btn").click();
    await expect(sheet).toContainText(/Disetujui|Approved/);
    await signOut(page);

    // U1 executes M1 -> zero GL, issues RPA-... -> Receive Replacement -> Gate 1 release -> Gate 2 accept
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);
    await page.locator("#ret-tab-returns").click();
    const approvedRow = page.locator("#ret-returns-table tbody tr", { hasText: draftRef }).first();
    await approvedRow.getByRole("button", { name: /Buka Detail|Open Detail/ }).click();
    await page.locator("#ret-execute-btn").click();
    await expect(sheet).toContainText(/Dieksekusi|Executed/);
    await expect(sheet).toContainText("RPA-");

    const rpaMatch = (await sheet.textContent())?.match(/RPA-\d{4}-\d+/);
    expect(rpaMatch).toBeTruthy();
    const rpaNumber = rpaMatch![0];

    await page.locator("#ret-tab-replacements").click();
    const rpaRow = page.locator("#ret-replacements-table tbody tr", { hasText: rpaNumber }).first();
    await expect(rpaRow).toBeVisible();
    await rpaRow.getByRole("button", { name: /Terima Pengganti|Receive Replacement/ }).click();

    await expect(page.locator("#ret-replacement-modal")).toBeVisible();
    await page.locator("#ret-rpa-qty").fill("2");
    await page.locator("#ret-rpa-delivery-note").fill(rpaDeliveryNote);
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "07-replacement-modal-desktop.png"),
      fullPage: true,
    });
    await page.locator("#ret-rpa-submit-btn").click();
    await expect(page.getByTestId("ret-replacement-success")).toContainText(rpaNumber);

    // Verify replacement GRN on /receiving shows RPA badge, release Gate 1 & value at Gate 2 (2 * 75.000 = 150.000)
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/receiving`);
    const grnRow = page.locator("tbody tr", { hasText: rpaDeliveryNote }).first();
    await expect(grnRow).toBeVisible();
    await expect(grnRow).toContainText(rpaNumber);
    await grnRow.getByRole("button", { name: /Inspeksi|Inspect/ }).click();

    await expect(page.locator("#rcv-detail-sheet")).toContainText(rpaNumber);
    await page.locator("#dock-pending-0").fill("0");
    await page.locator("#dock-held-0").fill("0");
    await page.locator("#dock-released-0").fill("2");
    await page.locator("#dock-rejected-0").fill("0");
    await page.locator("#dock-submit-0").click();
    await expect(page.locator("#rcv-detail-sheet")).toContainText(/Dirilis ke Gate 2|Released to Gate 2/);

    await page.locator("#gate2-acceptance-date").fill("2026-11-11");
    await page.locator("#gate2-preview-btn").click();
    await expect(page.locator("#gate2-preview-card")).toContainText("150.000");
    await page.locator("#gate2-confirm-btn").click();
    await expect(page.locator("#gate2-success-banner")).toContainText("ACC-");
    await expect(page.locator("#gate2-linked-acceptances")).toContainText("150.000");
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "02-m1-replacement-rpa-and-gate2-desktop.png"),
      fullPage: true,
    });
  });

  test("S10-E2E-02 M2 valued unbilled return posts compensating GRNI/Inventory reversal and outbound movement", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);

    await signInAs(page, "rina.utami@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);

    const m2Row = page.locator("#ret-queue-m2-table tbody tr", { hasText: "ITM-S10-GASKET-02" }).first();
    await expect(m2Row).toBeVisible();
    await m2Row.getByRole("button", { name: /Buat Retur|Prepare Return/ }).click();

    await page.locator("#ret-composer-resolution").selectOption("CLOSE_WITHOUT_REPLACEMENT");
    await page.locator("#ret-composer-qty").fill("5");
    await page.locator("#ret-composer-carrier").fill(`TRK-M2-${stamp}`);
    await page.locator("#ret-preview-btn").click();
    await expect(page.getByTestId("ret-live-preview")).toContainText("600.000");
    await page.locator("#ret-save-draft-btn").click();

    const sheet = page.locator("#ret-detail-sheet");
    await expect(sheet.locator("h2")).toHaveText(/DRT-\d{4}-\d+/);
    const draftRef = (await sheet.locator("h2").textContent())?.trim() ?? "";
    await page.locator("#ret-submit-btn").click();
    await expect(sheet).toContainText(/Menunggu Persetujuan|Pending Approval/);
    await signOut(page);

    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);
    await page.locator("#ret-tab-returns").click();
    await page
      .locator("#ret-returns-table tbody tr", { hasText: draftRef })
      .first()
      .getByRole("button", { name: /Buka Detail|Open Detail/ })
      .click();
    await page.locator("#ret-approve-btn").click();
    await expect(sheet).toContainText(/Disetujui|Approved/);
    await signOut(page);

    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);
    await page.locator("#ret-tab-returns").click();
    await page
      .locator("#ret-returns-table tbody tr", { hasText: draftRef })
      .first()
      .getByRole("button", { name: /Buka Detail|Open Detail/ })
      .click();
    await page.locator("#ret-exec-date").fill("2026-11-11");
    await page.locator("#ret-execute-btn").click();

    await expect(sheet).toContainText(/Dieksekusi|Executed/);
    await expect(sheet).toContainText("MOV-");
    await expect(sheet).toContainText("JRN-");
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "03-m2-valued-unbilled-return-detail-desktop.png"),
      fullPage: true,
    });

    // Verify signed PURCHASE_RETURN_OUTBOUND (-5) appears on Inventory movements tab
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/inventory`);
    await page.locator("#inv-tab-movements").click();
    const movTable = page.locator("main table").first();
    await expect(movTable).toContainText(/Pengeluaran Retur ke Pemasok|Purchase Return Outbound/);
    await expect(movTable).toContainText("-5");
  });

  test("S10-E2E-03 M4 blocks replacement on billed item, OD-07 enforces Nota Retur, M3 creates SCN & reduces Payable", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const notaRetur = `NR-JKT-S10-${stamp}`;

    await signInAs(page, "siti.rahayu@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);

    const m3Row = page.locator("#ret-queue-m3-table tbody tr", { hasText: "INV-JKT-S10-M3-01" }).first();
    await expect(m3Row).toBeVisible();
    await m3Row.getByRole("button", { name: /Buat Retur|Prepare Return/ }).click();

    // Verify M4 block when REPLACEMENT_ONLY is attempted on a posted supplier bill
    await page.locator("#ret-composer-resolution").selectOption("REPLACEMENT_ONLY");
    await page.locator("#ret-composer-qty").fill("2");
    await page.locator("#ret-preview-btn").click();
    await expect(page.getByTestId("ret-preview-blocked-banner")).toContainText("OD-05 (M4)");
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "04-m4-billed-replacement-blocked-desktop.png"),
      fullPage: true,
    });

    // Switch to AP_CREDIT_NOTE without Nota Retur -> save draft -> submit blocked by OD-07
    await page.locator("#ret-composer-resolution").selectOption("AP_CREDIT_NOTE");
    await page.locator("#ret-composer-nota-retur").fill("");
    await page.locator("#ret-save-draft-btn").click();

    const sheet = page.locator("#ret-detail-sheet");
    await expect(sheet.locator("h2")).toHaveText(/DRT-\d{4}-\d+/);
    const draftRef = (await sheet.locator("h2").textContent())?.trim() ?? "";
    await page.locator("#ret-submit-btn").click();
    await expect(page.locator("#ret-detail-error")).toContainText("OD-07");
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "08-unexecuted-detail-od07-and-gl-preview-desktop.png"),
      fullPage: true,
    });

    // Edit draft to add Nota Retur -> submit cleanly
    await page.locator("#ret-edit-draft-btn").click();
    await page.locator("#ret-composer-nota-retur").fill(notaRetur);
    await page.locator("#ret-save-draft-btn").click();
    await expect(page.locator("#ret-composer")).toHaveCount(0);
    await expect(sheet).toContainText(notaRetur);
    await page.locator("#ret-submit-btn").click();
    await expect(sheet).toContainText(/Menunggu Persetujuan|Pending Approval/);
    await expect(page.getByTestId("ret-sod-notice")).toBeVisible();
    await signOut(page);

    // U2 approves & executes M3 Credit Note (2 PCS @ 75.000 + 11% PPN = Rp 166.500)
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);
    await page.locator("#ret-tab-returns").click();
    await page
      .locator("#ret-returns-table tbody tr", { hasText: draftRef })
      .first()
      .getByRole("button", { name: /Buka Detail|Open Detail/ })
      .click();
    await page.locator("#ret-approve-btn").click();
    await expect(sheet).toContainText(/Disetujui|Approved/);

    await page.locator("#ret-exec-date").fill("2026-11-11");
    await page.locator("#ret-execute-btn").click();
    await expect(sheet).toContainText(/Dieksekusi|Executed/);
    await expect(sheet).toContainText("SCN-");
    await expect(sheet).toContainText(notaRetur);
    await expect(sheet).toContainText("166.500");

    await page.locator("#ret-tab-credits").click();
    await expect(page.locator("#ret-tab-credits")).toHaveCSS("background-color", "rgb(14, 90, 83)");
    const creditRow = page.locator("#ret-credits-table tbody tr", { hasText: notaRetur }).first();
    await expect(creditRow).toBeVisible();
    await expect(creditRow).toContainText("166.500");
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "05-m3-credit-note-and-nota-retur-desktop.png"),
      fullPage: true,
    });

    // Verify /payables subledger displays Credited -Rp 166.500 on INV-JKT-S10-M3-01
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payables`);
    await page.locator("#ap-tab-subledger").click();
    const payableRow = page.locator("#ap-subledger-table tbody tr", { hasText: "INV-JKT-S10-M3-01" }).first();
    await expect(payableRow).toBeVisible();
    await expect(payableRow).toContainText("166.500");
  });

  test("S10-E2E-04 Enforces NUSA-SBY read-only boundary and 390x844 mobile id-ID layout without clipping", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/returns`);
    await expect(page.getByTestId("returns-readonly-banner")).toBeVisible();
    await expect(page.getByRole("button", { name: /Buat Retur|Prepare Return/ })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /Terima Pengganti|Receive Replacement/ })).toHaveCount(0);

    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/returns`);
    await expect(page.getByTestId("ret-governance-banner")).toBeVisible();
    await page.locator("#ret-matrix-toggle").click();
    await expect(page.locator("#ret-matrix-panel")).toBeVisible();
    const m3MobileRow = page.locator("#ret-queue-m3-table tbody tr", { hasText: "INV-JKT-S10-M3-01" }).first();
    await m3MobileRow.getByRole("button", { name: /Buat Retur|Prepare Return/ }).click();
    await page.locator("#ret-composer-qty").fill("1");
    await page.locator("#ret-preview-btn").click();
    await expect(page.getByTestId("ret-live-preview")).toBeVisible();
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "06-returns-mobile-id.png"),
      fullPage: true,
    });
  });
});
