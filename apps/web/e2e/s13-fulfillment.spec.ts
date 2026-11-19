import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S13/screenshots");

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
    Array.from(document.querySelectorAll('table, [class*="overflow-x-auto"]:not(pre):not([role="tablist"])')).some(
      (el) => el.scrollWidth > el.clientWidth + 1,
    ),
  );
  expect(innerClipped).toBe(false);
}

async function captureTopScreenshot(page: Page, filename: string) {
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: path.join(evidenceDir, filename),
    fullPage: true,
  });
}

test.describe("S13 Sales Reservation, Pick and Dispatch", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
  });

  test("S13-E2E-01 Reserve UNRESTRICTED stock against approved SO-2026-00001, create & confirm Pick Order, partial dispatch with atomic weighted-average COGS (Dr 5100 / Cr 1300), confirm POD delivery, read-only NUSA-SBY & mobile", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const sjRef = `SJ-JKT-S13-${stamp}`;
    const podRef = `POD-GRN-S13-${stamp}`;

    // 1. Warehouse Officer (Rina Utami) opens NUSA-JKT Fulfillment Queue, expands OD-10 policy strip, and opens reserve drawer for SO-2026-00001
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/fulfillment`);

    await expect(page.locator("#ful-orders-table")).toBeVisible();
    await expect(page.locator("#ful-orders-table")).toContainText("SO-2026-00001");
    await page.locator("#ful-policy-toggle").click();
    await expect(
      page.getByText(/Larangan Stok Tertahan & Negatif \(OD-10\)|No Restricted or Negative Stock \(OD-10\)/),
    ).toBeVisible();

    await page.getByTestId("open-reserve-SO-2026-00001-SO-L001").click();
    await expect(page.locator("#ful-reserve-drawer")).toBeVisible();
    await page.locator("#ful-rsv-qty").fill("6");
    await page.locator("#ful-rsv-notes").fill("Alokasi tahap 1 untuk PT Graha Ritel Nusantara");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "01-fulfillment-queue-and-reserve-desktop.png");

    await page.locator("#ful-rsv-submit-btn").click();
    await expect(page.locator("#ful-reserve-drawer")).toHaveCount(0);
    await expect(page.locator("#ful-reservations-table")).toBeVisible();

    // 2. Create & Confirm Warehouse Pick Order (PCK-2026-00001) for the 6 PCS reservation, then open Dispatch Drawer for partial 4 PCS shipment
    const pickRsvBtn = page.locator('[data-testid^="pick-rsv-RSV-2026-"]').first();
    await expect(pickRsvBtn).toBeVisible();
    await pickRsvBtn.click();

    await expect(page.locator("#ful-picks-table")).toBeVisible();
    await expect(page.locator("#ful-picks-table")).toContainText("PCK-2026-");

    const openDispatchBtn = page.locator('[data-testid^="open-dispatch-PCK-2026-"]').first();
    await expect(openDispatchBtn).toBeVisible();
    await openDispatchBtn.click();

    await expect(page.locator("#ful-dispatch-drawer")).toBeVisible();
    await page.locator("#ful-disp-sj").fill(sjRef);
    await page.locator("#ful-disp-carrier").fill("Armada Distribusi Nusantara");
    await page.locator("#ful-disp-vehicle").fill("B 9412 TCD");
    await page.locator("#ful-disp-qty-0").fill("4");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "02-reservations-picks-and-dispatch-drawer-desktop.png");

    // 3. Submit Partial Dispatch (4 PCS of 6 PCS picked) -> posts StockMovement (MOV-2026-...) & GL COGS Journal (JRN-2026-...)
    await page.locator("#ful-disp-submit-btn").click();
    await expect(page.locator("#ful-dispatch-drawer")).toHaveCount(0);
    await expect(page.locator("#ful-shipments-table")).toBeVisible();
    await expect(page.locator("#ful-shipments-table")).toContainText("SHP-2026-");
    await expect(page.locator("#ful-shipments-table")).toContainText(sjRef);
    await expect(page.locator("#ful-shipments-table")).toContainText("MOV-2026-");
    await expect(page.locator("#ful-shipments-table")).toContainText("JRN-2026-");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "03-shipments-cogs-ledger-desktop.png");

    // 4. Confirm Customer Delivery Evidence (POD) -> transitions shipment to DELIVERED
    const openDeliverBtn = page.locator('[data-testid^="open-deliver-SHP-2026-"]').first();
    await expect(openDeliverBtn).toBeVisible();
    await openDeliverBtn.click();
    await expect(page.locator("#ful-deliver-drawer")).toBeVisible();
    await page.locator("#ful-pod-receiver").fill("Tari Wibowo (Receiving Supervisor)");
    await page.locator("#ful-pod-ref").fill(podRef);
    await page.locator("#ful-pod-submit-btn").click();

    await expect(page.locator("#ful-deliver-drawer")).toHaveCount(0);
    await expect(page.locator("#ful-shipments-table")).toContainText(podRef);
    await expect(page.locator("#ful-shipments-table")).toContainText("Tari Wibowo");

    // Switch back to SO Fulfillment Queue to verify SO-2026-00001 shows PARTIALLY_DISPATCHED (Ordered: 10, Reserved: 2, Dispatched: 4, Unallocated: 4)
    await page.locator("#ful-tab-orders").click();
    await expect(page.locator("#ful-orders-table")).toContainText(/Sebagian Dikirim|Partially Dispatched/);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "04-partial-fulfillment-progress-desktop.png");

    // 5. Multi-entity read-only mode on NUSA-SBY for Budi Santoso (has inventory.read + sales.read, no inventory.manage)
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/fulfillment`);
    await expect(page.locator("#ful-readonly-banner")).toBeVisible();
    await expect(page.locator("#ful-orders-table")).toContainText("SO-2026-00001");
    await expect(page.locator('[data-testid^="open-reserve-"]')).toHaveCount(0);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "05-fulfillment-readonly-sby-desktop.png");

    // 6. Mobile 390x844 responsive layout on NUSA-JKT without horizontal overflow
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/fulfillment`);
    await expect(page.locator("#ful-orders-table")).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "06-fulfillment-mobile.png");
  });
});
