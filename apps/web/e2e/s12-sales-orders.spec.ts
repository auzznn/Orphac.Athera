import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S12/screenshots");

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
    Array.from(document.querySelectorAll('table, [class*="overflow-x-auto"]:not(pre)')).some(
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

test.describe("S12 Customer Sales Orders and Commercial Commitments", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
  });

  test("S12-E2E-01 Live availability + Output VAT draft/submit (U5), four-eyes commitment approve (U2), Rev 2 amendment diff, read-only NUSA-SBY & mobile", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const customerRef = `PO-GRN-E2E-${stamp}`;

    // 1. Sales Officer (Dewi) inspects NUSA-JKT sales orders, OD-09 policy & OD-02 SoD lock on her pending submission
    await signInAs(page, "dewi.lestari@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/sales-orders`);

    await expect(page.locator("#so-orders-table")).toBeVisible();
    await expect(page.locator("#so-orders-table")).toContainText("SO-2026-00001");
    await page.locator("#so-policy-toggle").click();
    await expect(page.getByText(/Aturan Harga Aman OD-09|OD-09 Safe Commercial Defaults/)).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "01-sales-orders-overview-desktop.png");

    await page.locator('[data-testid^="open-so-SO-DRF-"]').first().click();
    await expect(page.locator("#so-detail-sheet")).toBeVisible();
    await expect(page.getByTestId("so-sod-notice")).toBeVisible();
    await expect(page.locator("#so-approve-btn")).toHaveCount(0);
    await page.locator("#so-detail-close").click();

    // 2. Dewi opens Order Editor, sees live stock availability & Output VAT PPN-OUT-11, saves draft & submits
    await page.locator("#so-new-draft-btn").click();
    await expect(page.locator("#so-panel-editor")).toBeVisible();

    await selectOptionMatching(page.locator("#so-ed-customer"), /PT-CUST-001/);
    await page.locator("#so-ed-req-date").fill("2026-11-26");
    await page.locator("#so-ed-address").fill("Kawasan Industri Jababeka Blok K-14 Bekasi");
    await page.locator("#so-ed-cust-ref").fill(customerRef);

    await selectOptionMatching(page.locator("#so-line-item-0"), /ITM-S09-VALVE-01/);
    await expect(page.locator("#so-panel-editor")).toContainText(/Bebas:|Avail:/);
    await selectOptionMatching(page.locator("#so-line-uom-0"), /PCS/);
    await page.locator("#so-line-qty-0").fill("4");
    await page.locator("#so-line-price-0").fill("95000");
    await selectOptionMatching(page.locator("#so-line-tax-0"), /PPN-OUT-11/);

    await expect(page.locator("#so-panel-editor")).toContainText("Rp 380.000");
    await expect(page.locator("#so-panel-editor")).toContainText("Rp 41.800");
    await expect(page.locator("#so-panel-editor")).toContainText("Rp 421.800");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "02-sales-order-editor-and-draft-desktop.png");

    await page.locator("#so-save-draft-btn").click();
    await expect(page.locator("#so-detail-sheet")).toBeVisible();
    const createdDraftCode = ((await page.locator("#so-detail-sheet h2").textContent()) ?? "").trim();
    expect(createdDraftCode).toMatch(/^SO-DRF-/);

    await page.locator("#so-submit-btn").click();
    await expect(page.getByTestId("so-sod-notice")).toBeVisible();

    // 3. Independent Finance Controller (Budi) opens Four-Eyes queue and commits SO number
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/sales-orders`);

    await page.locator("#so-tab-queue").click();
    await expect(page.locator("#so-queue-table")).toBeVisible();
    await page.getByTestId(`queue-open-${createdDraftCode}`).click();
    await expect(page.locator("#so-detail-sheet")).toBeVisible();
    await expect(page.locator("#so-approve-btn")).toBeDisabled();

    await page
      .locator("#so-decision-reason")
      .fill("Verified customer PO reference, stock availability, and PPN-OUT-11 output VAT.");
    await page.locator("#so-approve-btn").click();

    await expect(page.locator("#so-detail-sheet h2")).toContainText(/^SO-2026-/);
    const committedSoNumber = ((await page.locator("#so-detail-sheet h2").textContent()) ?? "").trim();
    expect(committedSoNumber).toMatch(/^SO-2026-\d{5}$/);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "03-sales-order-four-eyes-approve-desktop.png");

    // 4. Dewi amends committed SO (Rev 2: 4 -> 6 PCS) and submits; Budi inspects diff & Rev 1 hash and approves Rev 2
    await signOut(page);
    await signInAs(page, "dewi.lestari@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/sales-orders`);

    await page.getByTestId(`open-so-${committedSoNumber}`).click();
    await expect(page.locator("#so-detail-sheet")).toBeVisible();
    await page.locator("#so-amend-reason").fill("Customer increased valve order quantity from 4 to 6 PCS");
    await page.locator("#so-amend-btn").click();

    await expect(page.locator("#so-ed-change-reason")).toBeVisible();
    await page.locator("#so-line-qty-0").fill("6");
    await page.locator("#so-save-draft-btn").click();

    await expect(page.locator("#so-detail-sheet")).toBeVisible();
    await expect(page.locator("#so-revision-diff")).toContainText("Rp 210.900");
    await page.locator("#so-submit-btn").click();
    await expect(page.getByTestId("so-sod-notice")).toBeVisible();

    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/sales-orders`);
    await page.locator("#so-tab-queue").click();
    await expect(page.locator("#so-queue-table")).toContainText("Rp 632.700");
    await page.getByTestId(`queue-open-${committedSoNumber}`).click();

    await expect(page.locator("#so-revision-diff")).toContainText("Rp 210.900");
    await expect(page.locator("#so-rev-select")).toBeVisible();

    // Verify Rev 1 historical snapshot remains intact
    await selectOptionMatching(page.locator("#so-rev-select"), /Rev 1/);
    await expect(page.locator("#so-detail-sheet")).toContainText("Rp 421.800");
    await selectOptionMatching(page.locator("#so-rev-select"), /Rev 2/);

    await page
      .locator("#so-decision-reason")
      .fill("Revision 2 delta (+2 PCS / +Rp 210.900) verified within unrestricted warehouse stock.");
    await page.locator("#so-approve-btn").click();
    await expect(page.locator("#so-approve-btn")).toHaveCount(0);

    await expect(page.locator("#so-detail-sheet")).toContainText("Rp 632.700");
    await expect(page.locator("#so-revision-diff")).toContainText("Rp 210.900");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "04-sales-order-revision-diff-desktop.png");

    // 5. Read-only multi-entity view on NUSA-SBY for Budi (GET 200, no editor/create/approve controls)
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/sales-orders`);
    await expect(page.locator("#so-readonly-banner")).toBeVisible();
    await expect(page.locator("#so-new-draft-btn")).toHaveCount(0);
    await expect(page.locator("#so-tab-editor")).toHaveCount(0);
    await expect(page.locator("#so-orders-table")).toContainText("SO-2026-00001");
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "05-sales-orders-readonly-sby-desktop.png");

    // 6. Mobile 390x844 responsive layout without horizontal overflow
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/sales-orders`);
    await expect(page.locator("#so-orders-table")).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "06-sales-orders-mobile.png");
  });
});
