import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S09/screenshots");

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

test.describe("S09 Supplier Invoice Three-Way Match, AP & Price Difference", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
  });

  test("S09-E2E-01 U4 drafts & submits variance bill, U2 approves/posts DEC-P2P-013 split, releases hold & verifies Inventory", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const vendorInvNo = `INV-JKT-S09-${stamp}`;
    const taxInvNo = `010.001-26.${stamp.padStart(8, "0")}`;

    // 1. U4 (AP_CLERK) signs in on NUSA-JKT & verifies open GRNI on ITM-S09-VALVE-01
    await signInAs(page, "siti.rahayu@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payables`);

    await expect(page.getByTestId("ap-od03-governance-banner")).toBeVisible();
    await page.locator("#ap-tab-grniQueue").click();
    await expect(page.locator("#ap-tab-grniQueue")).toHaveAttribute("aria-selected", "true");

    const valveGrniRow = page.locator("#ap-grni-table tbody tr", { hasText: "ITM-S09-VALVE-01" }).first();
    await expect(valveGrniRow).toBeVisible();
    await expect(valveGrniRow).toContainText("100");
    await expect(valveGrniRow).toContainText(/Stok Pool:\s+40\s+PCS/);
    await assertNoLayoutClipping(page);
    await page.screenshot({ path: path.join(evidenceDir, "01-payables-overview-desktop.png"), fullPage: true });

    // 2. Open composer from Open GRNI Queue -> enter +10% price variance @ 55.000 + PPN-IN-11 + PPH23-SVC-2
    await valveGrniRow.getByRole("button", { name: /Tagih Baris Ini|Bill This Receipt/ }).click();
    await expect(page.locator("#ap-bill-composer")).toBeVisible();

    await page.locator("#ap-composer-vendor-inv").fill(vendorInvNo);
    await page.locator("#ap-composer-tax-inv").fill(taxInvNo);
    await page.locator("#ap-composer-qty").fill("100");
    await page.locator("#ap-composer-unit-price").fill("55000");

    // Select PPH23-SVC-2 withholding code
    const pphOptionValue = await page.locator("#ap-composer-pph option", { hasText: "PPH23-SVC-2" }).first().getAttribute("value");
    expect(pphOptionValue).toBeTruthy();
    await page.locator("#ap-composer-pph").selectOption(pphOptionValue!);

    await page.locator("#ap-composer-declared-gross").fill("6105000");
    await page.locator("#ap-preview-match-btn").click();

    const previewBox = page.getByTestId("ap-live-match-preview");
    await expect(previewBox).toBeVisible();
    await expect(previewBox).toContainText("200.000");
    await expect(previewBox).toContainText("300.000");
    await expect(previewBox).toContainText("5.995.000");
    await expect(previewBox).toContainText(/Wajib Persetujuan Selisih|Approval Required/);
    await assertNoLayoutClipping(page);
    await page.screenshot({ path: path.join(evidenceDir, "02-three-way-match-variance-preview-desktop.png"), fullPage: true });

    // 3. Save Draft & Submit by U4 (verify U4 cannot approve or post)
    await page.locator("#ap-save-draft-btn").click();
    await expect(page.locator("#ap-bill-detail-sheet")).toBeVisible();

    await page.locator("#ap-submit-bill-btn").click();
    await expect(page.locator("#ap-bill-detail-sheet")).toContainText(/Menunggu Persetujuan|Pending Approval/);
    await expect(page.locator("#ap-approve-bill-btn")).toHaveCount(0);
    await expect(page.locator("#ap-post-bill-btn")).toHaveCount(0);

    await signOut(page);

    // 4. U2 (Controller) signs in on NUSA-JKT -> four-eyes approve with ApplyPaymentBlock, post, release hold, inspect subledger
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payables`);

    const submittedRow = page.locator("#ap-bills-table tbody tr", { hasText: vendorInvNo }).first();
    await expect(submittedRow).toBeVisible();
    await submittedRow.getByRole("button", { name: /Buka Detail|Open Detail/ }).click();

    const sheet = page.locator("#ap-bill-detail-sheet");
    await expect(sheet).toBeVisible();
    await page.locator("#ap-approve-variance-checkbox").check();
    await page.locator("#ap-apply-block-checkbox").check();
    await page.locator("#ap-decision-reason").fill("Selisih harga +5k disetujui Controller");
    await page.locator("#ap-decision-block-reason").fill("Menunggu salinan fisik Faktur Pajak");
    await page.evaluate(() => window.scrollTo(0, 0));
    await assertNoLayoutClipping(page);
    await page.screenshot({ path: path.join(evidenceDir, "06-bill-detail-four-eyes-approval-desktop.png"), fullPage: true });
    await page.locator("#ap-approve-bill-btn").click();

    await expect(sheet).toContainText(/Disetujui \(Siap Post\)|Approved \(Ready to Post\)/);

    await page.locator("#ap-post-date-input").fill("2026-11-10");
    await page.locator("#ap-post-bill-btn").click();
    await expect(sheet).toContainText(/Sudah Diposting|Posted/);
    await expect(sheet).toContainText(/Pembayaran Ditahan|Payment Blocked/);

    await page.locator("#ap-payment-block-reason").fill("Salinan Faktur Pajak asli terverifikasi");
    await page.locator("#ap-payment-release-btn").click();
    await expect(sheet).toContainText(/Blokir Dirilis|Released/);
    await page.evaluate(() => window.scrollTo(0, 0));
    await assertNoLayoutClipping(page);
    await page.screenshot({ path: path.join(evidenceDir, "07-bill-detail-posted-desktop.png"), fullPage: true });

    // Switch to Subledger tab to inspect open AP item & DEC-P2P-013 price adjustment audit record
    await page.locator("#ap-tab-subledger").click();
    await expect(page.locator("#ap-tab-subledger")).toHaveAttribute("aria-selected", "true");
    const subRow = page.locator("#ap-subledger-table tbody tr", { hasText: vendorInvNo }).first();
    await expect(subRow).toBeVisible();
    await expect(subRow).toContainText("5.995.000");
    await assertNoLayoutClipping(page);
    await page.screenshot({ path: path.join(evidenceDir, "03-posted-bill-journal-and-ap-subledger-desktop.png"), fullPage: true });

    // 5. Inspect Inventory ledger on ITM-S09-VALVE-01 (carrying value = Rp 2.200.000, MAC = Rp 55.000)
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/inventory`);
    const invRow = page.locator("tbody tr", { hasText: "ITM-S09-VALVE-01" }).first();
    await expect(invRow).toBeVisible();
    await expect(invRow).toContainText("2.200.000");
    await expect(invRow).toContainText("55.000");
    await invRow.getByRole("button", { name: /Buka Buku Besar|Buku Besar Dibuka|Open Ledger|Ledger Active/ }).click();
    await expect(page.locator("body")).toContainText(/Penyesuaian Harga Faktur|Invoice Price Adjustment/);
    await assertNoLayoutClipping(page);
    await page.screenshot({ path: path.join(evidenceDir, "04-inventory-revaluation-ledger-desktop.png"), fullPage: true });

    // 6. Verify 390x844 mobile viewport layout on /payables with zero overflow
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payables`);
    await expect(page.locator("#ap-bills-table")).toBeVisible();
    await assertNoLayoutClipping(page);
    await page.screenshot({ path: path.join(evidenceDir, "05-payables-mobile-390x844.png"), fullPage: true });
  });

  test("S09-E2E-02 Enforces AP access denial for U1 and read-only enforcement on NUSA-SBY for U2", async ({
    page,
  }) => {
    // 1. U1 (Rina) has zero AP permissions -> nav locked & direct route returns 403 UI without leaking data
    await signInAs(page, "rina.utami@demo.athera.local");
    await expect(page.locator(`aside a[href="/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payables"]`)).toHaveCount(0);

    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payables`);
    await expect(page.getByTestId("ap-access-denied")).toBeVisible();
    await expect(page.locator("#ap-bills-table")).toHaveCount(0);
    await signOut(page);

    // 2. U2 (Budi) on NUSA-SBY has ap.read only -> read-only banner shown and mutation controls hidden
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/payables`);
    await expect(page.getByTestId("ap-readonly-banner")).toBeVisible();
    await expect(page.locator("#ap-new-bill-btn")).toHaveCount(0);
  });
});
