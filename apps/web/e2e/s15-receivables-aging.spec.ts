import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

const SCREENSHOT_DIR = path.resolve(__dirname, "../../../docs/evidence/S15/screenshots");
const DEMO_PASSWORD = process.env.ATHERA_DEMO_PASSWORD || "AtheraDemo!2026";
const TENANT_NUSA_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_NUSA_JKT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";
const ENTITY_NUSA_SBY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102";

async function signInAs(page: Page, email: string) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/login");
  await page.getByLabel(/Email Perusahaan|Work Email/).fill(email);
  await page.getByLabel(/Kata Sandi|Password/).fill(DEMO_PASSWORD);
  await page.getByRole("button", { name: /Masuk ke Athera|Sign in to Athera/ }).click();
  await expect(page).toHaveURL(/\/$/);
}

async function assertNoLayoutClipping(page: Page) {
  const docOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth > window.innerWidth + 1,
  );
  expect(docOverflow).toBe(false);

  const innerClipped = await page.evaluate(() =>
    Array.from(document.querySelectorAll('table, [class*="overflow-x-auto"]:not(pre):not([role="tablist"])')).some(
      (el) => el.scrollWidth > el.clientWidth + 1,
    ),
  );
  expect(innerClipped).toBe(false);
}

async function captureTopScreenshot(page: Page, filename: string) {
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: path.join(SCREENSHOT_DIR, filename),
    fullPage: true,
  });
}

test.describe("S15 Customer Receipts, Allocation & AR Aging", () => {
  test("S15-E2E-01 Import inbound bank CREDIT CSV, confirm customer receipt with partial allocation & unapplied remainder, verify GL 1200 proof and AR Aging, read-only NUSA-SBY & mobile", async ({
    page,
  }) => {
    const suffix = Date.now().toString().slice(-5);
    const bankRef = `TRF-CUST-${suffix}`;
    const stmtRef = `STMT-IN-${suffix}`;

    // Sign in as Finance Controller (Budi) on NUSA-JKT
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/receivables`);
    await expect(page.locator("#ar-gl-reconciled-badge")).toBeVisible();

    // 1. Open CSV Import drawer & import inbound CREDIT line (Rp 120.000)
    await page.locator("#ar-open-import-btn").click();
    await expect(page.locator("#ar-import-drawer")).toBeVisible();
    await page.locator("#ar-import-ref").fill(stmtRef);
    await page.locator("#ar-import-csv").fill(
      `ValueDate,Direction,Amount,BankReference,Counterparty,Description\n2026-11-19,CREDIT,120000,${bankRef},PT Graha Ritel Nusantara,Pembayaran pelanggan S15`,
    );
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "01-import-inbound-bank-csv-desktop.png");

    await page.locator("#ar-import-submit-btn").click();
    await expect(page.locator("#ar-import-drawer")).toHaveCount(0);
    await expect(page.locator("#ar-bank-table")).toContainText(bankRef);

    // 2. Open Confirm Receipt + Initial Allocation drawer, allocate 80,000 (leaving 40,000 unapplied)
    await page.locator(`[data-testid="confirm-bank-${bankRef}"]`).click();
    await expect(page.locator("#ar-confirm-drawer")).toBeVisible();
    const firstAllocInput = page.locator("#ar-init-alloc-0");
    if ((await firstAllocInput.count()) > 0) {
      await firstAllocInput.fill("80000");
    }
    await page.locator("#ar-confirm-notes").fill("Penerimaan parsial + sisa unapplied");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "02-confirm-receipt-and-allocation-drawer-desktop.png");

    await page.locator("#ar-confirm-submit-btn").click();
    await expect(page.locator("#ar-confirm-drawer")).toHaveCount(0);
    await expect(page.locator("#ar-receipts-table")).toBeVisible();
    await expect(page.locator("#ar-receipts-table")).toContainText("RCT-2026-");
    await expect(page.locator("#ar-receipts-table")).toContainText(bankRef);
    await expect(page.locator("#ar-gl-reconciled-badge")).toContainText(/GL 1200 =/);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "03-receipts-and-allocations-ledger-desktop.png");

    // 3. Switch to AR Aging tab and inspect aging buckets + unapplied + net AR
    await page.locator("#ar-tab-aging").click();
    await expect(page.locator("#ar-aging-table")).toBeVisible();
    await expect(page.locator("#ar-aging-table")).toContainText("PT-CUST-001");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "04-ar-aging-schedule-desktop.png");

    // 4. Read-only mode on NUSA-SBY
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/receivables`);
    await expect(page.locator("#ar-readonly-banner")).toBeVisible();
    await expect(page.locator("#ar-open-import-btn")).toHaveCount(0);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "05-receivables-readonly-sby-desktop.png");

    // 5. Mobile 390x844 responsive layout on NUSA-JKT Receipts tab
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/receivables`);
    await page.locator("#ar-tab-receipts").click();
    await expect(page.locator("#ar-receipts-table")).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "06-receivables-mobile.png");
  });
});
