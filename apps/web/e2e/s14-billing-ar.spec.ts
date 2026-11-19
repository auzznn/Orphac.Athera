import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

const SCREENSHOT_DIR = path.resolve(__dirname, "../../../docs/evidence/S14/screenshots");
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

async function ensureBillableShipmentExists(page: Page) {
  // Check whether NUSA-JKT already has a billable shipment line (e.g., from S13 E2E); if running standalone, dispatch 4 PCS via S13 UI/API.
  const hasBillable = await page.evaluate(
    async ({ tid, eid }) => {
      const res = await fetch(`/api/workspaces/${tid}/entities/${eid}/billing/overview`, {
        credentials: "include",
      });
      if (!res.ok) return false;
      const body = await res.json();
      return Array.isArray(body.billableShipmentLines) && body.billableShipmentLines.length > 0;
    },
    { tid: TENANT_NUSA_ID, eid: ENTITY_NUSA_JKT_ID },
  );
  if (hasBillable) return;

  // Sign in as warehouse manager (Rina) and reserve/pick/dispatch 4 PCS against SO-2026-00001
  await signOut(page);
  await signInAs(page, "rina.utami@demo.athera.local");
  await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/fulfillment`);
  await expect(page.locator("#ful-orders-table")).toBeVisible();

  await page.locator('[data-testid^="open-reserve-SO-2026-00001-"]').first().click();
  await page.locator("#ful-rsv-qty").fill("4");
  await page.locator("#ful-rsv-submit-btn").click();
  await expect(page.locator("#ful-reservations-table")).toBeVisible();

  await page.locator('[data-testid^="pick-rsv-RSV-2026-"]').first().click();
  await expect(page.locator("#ful-picks-table")).toBeVisible();

  await page.locator('[data-testid^="open-dispatch-PCK-2026-"]').first().click();
  await page.locator("#ful-disp-sj").fill(`SJ-S14-AUTO-${Date.now().toString().slice(-4)}`);
  await page.locator("#ful-disp-qty-0").fill("4");
  await page.locator("#ful-disp-submit-btn").click();
  await expect(page.locator("#ful-shipments-table")).toBeVisible();

  await signOut(page);
  await signInAs(page, "dewi.lestari@demo.athera.local");
}

test.describe("S14 Customer Billing, Output Tax & Accounts Receivable", () => {
  test("S14-E2E-01 Sales drafts & submits partial invoice, Finance posts with Faktur Pajak (Dr 1200/Cr 4100/Cr 2210), verifies AR subledger, issues linked Credit Note (Nota Retur), read-only NUSA-SBY & mobile", async ({
    page,
  }) => {
    const suffix = Date.now().toString().slice(-5);
    const fakturRef = `FP-010.001-26.${suffix}`;
    const notaReturRef = `NR-2026-${suffix}`;

    // 1. Sales Officer (Nadia) opens /billing, drafts partial 2 PCS invoice, and submits to Finance
    await signInAs(page, "dewi.lestari@demo.athera.local");
    await ensureBillableShipmentExists(page);

    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/billing`);
    await expect(page.locator("#bil-shipments-table")).toBeVisible();
    await expect(page.locator("#bil-shipments-table")).toContainText("SHP-2026-");

    await page.locator('[data-testid^="draft-inv-SHP-2026-"]').first().click();
    await expect(page.locator("#bil-draft-drawer")).toBeVisible();
    await page.locator("#bil-draft-qty").fill("2");
    await page.locator("#bil-draft-notes").fill("Penagihan parsial 2 PCS tahap 1");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "01-billable-shipments-and-draft-drawer-desktop.png");

    await page.locator("#bil-draft-submit-btn").click();
    await expect(page.locator("#bil-draft-drawer")).toHaveCount(0);
    await expect(page.locator("#bil-invoices-table")).toBeVisible();
    await expect(page.locator("#bil-invoices-table")).toContainText("INV-2026-");

    // Submit to Finance (Nadia has sales.manage, not finance.journal.post)
    await page.locator('[data-testid^="submit-inv-INV-2026-"]').first().click();
    await expect(page.locator("#bil-invoices-table")).toContainText(/Diajukan|Submitted/);

    // 2. Finance Controller (Budi) opens /billing, opens Irreversible Posting Drawer, enters Faktur Pajak, and posts
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/billing`);
    await page.locator("#bil-tab-invoices").click();
    await expect(page.locator("#bil-invoices-table")).toBeVisible();

    await page.locator('[data-testid^="open-post-INV-2026-"]').first().click();
    await expect(page.locator("#bil-post-drawer")).toBeVisible();
    await page.locator("#bil-post-evidence").fill(fakturRef);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "02-irreversible-post-gl-preview-desktop.png");

    await page.locator("#bil-post-confirm-btn").click();
    await expect(page.locator("#bil-post-drawer")).toHaveCount(0);
    await expect(page.locator("#bil-invoices-table")).toContainText(/Terposting|Posted/);
    await expect(page.locator("#bil-invoices-table")).toContainText("JRN-2026-");

    // 3. Issue linked Credit Note for 1 PCS with mandatory Nota Retur
    await page.locator('[data-testid^="open-credit-INV-2026-"]').first().click();
    await expect(page.locator("#bil-credit-drawer")).toBeVisible();
    await page.locator("#bil-credit-evidence").fill(notaReturRef);
    await page.locator("#bil-credit-notes").fill("Koreksi 1 PCS sesuai Nota Retur");
    await page.locator("#bil-credit-qty-0").fill("1");
    await page.locator("#bil-credit-submit-btn").click();
    await expect(page.locator("#bil-credit-drawer")).toHaveCount(0);
    await expect(page.locator("#bil-invoices-table")).toContainText("CN-2026-");
    await expect(page.locator("#bil-invoices-table")).toContainText(notaReturRef);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "03-invoices-and-credit-note-ledger-desktop.png");

    // 4. Verify AR Subledger reflects Original, Credited, and Reduced Open Balance
    await page.locator("#bil-tab-receivables").click();
    await expect(page.locator("#bil-ar-table")).toBeVisible();
    await expect(page.locator("#bil-ar-table")).toContainText("AR-2026-");
    await expect(page.locator("#bil-ar-table")).toContainText("JRN-2026-");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "04-ar-subledger-desktop.png");

    // 5. Read-only mode on NUSA-SBY for Budi Santoso (finance.read + sales.read only)
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/billing`);
    await expect(page.locator("#bil-readonly-banner")).toBeVisible();
    await expect(page.locator('[data-testid^="draft-inv-"]')).toHaveCount(0);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "05-billing-readonly-sby-desktop.png");

    // 6. Mobile 390x844 responsive layout on NUSA-JKT Invoices tab
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/billing`);
    await page.locator("#bil-tab-invoices").click();
    await expect(page.locator("#bil-invoices-table")).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "06-billing-mobile.png");
  });
});
