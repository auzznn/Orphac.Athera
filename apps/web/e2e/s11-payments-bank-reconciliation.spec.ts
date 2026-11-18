import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S11/screenshots");

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

test.describe("S11 Supplier Settlement, Outbound Clearing & Bank Reconciliation", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
  });

  test("S11-E2E-01 Proposal & simulated file (0 GL), four-eyes release (Dr 2100/Cr 1120), CSV statement import & bank match (Dr 1120/Cr 1110), and read-only entity + mobile", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);

    // 1. AP Clerk (Siti) prepares proposal + verifies simulated file & OD-02 SoD notice
    await signInAs(page, "siti.rahayu@demo.athera.local");
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payments`);

    await expect(page.locator("#pay-payments-table")).toBeVisible();
    await page.locator("#pay-stages-toggle").click();
    await expect(page.getByText(/Kliring 1120 Cocok|1120 Clearing Reconciled/)).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "01-payments-overview-desktop.png");

    // Inspect pending proposal prepared by Siti -> OD-02 notice prevents self-release, simulate additional non-live file
    await page.locator('[data-testid^="open-payment-PAY-DRF-"]').first().click();
    await expect(page.locator("#pay-detail-sheet")).toBeVisible();
    await expect(page.getByTestId("pay-sod-notice")).toBeVisible();
    await expect(page.getByText(/SIMULASI · BUKAN TRANSFER LANGSUNG|SIMULATED FILE/)).toBeVisible();
    await page.locator("#pay-simulate-file-btn").click();
    await expect(page.locator("#pay-detail-sheet")).toContainText("PIN-");

    // Collapse sheet and prepare a new draft proposal for INV-JKT-S11-03
    await page.locator("#pay-detail-close").click();
    await page.locator("#pay-composer-toggle").click();
    await page.locator("#pay-comp-ben-bank").fill("PT Bank Mandiri (Persero) Tbk");
    await page.locator("#pay-comp-ben-acct").fill("122-000-4455-889");
    await page.locator("#pay-comp-ben-holder").fill("PT Sumber Makmur Kemasan");
    await page.locator("#pay-create-submit").click();
    await expect(page.locator("#pay-detail-sheet")).toBeVisible();
    await page.locator("#pay-submit-btn").click();
    await expect(page.getByTestId("pay-sod-notice")).toBeVisible();

    // 2. Finance Controller (Budi) releases Siti's pending payment into Outbound Clearing 1120 (Dr 2100 / Cr 1120)
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payments`);

    await page.locator('[data-testid^="open-payment-PAY-DRF-"]').first().click();
    await expect(page.locator("#pay-detail-sheet")).toBeVisible();
    await page.locator("#pay-release-ref").fill(`BCA-PORTAL-${stamp}`);
    await page.locator("#pay-release-btn").click();
    await expect(page.locator("#pay-detail-sheet")).toContainText("JRN-");
    await expect(page.locator("#pay-detail-sheet")).toContainText(`BCA-PORTAL-${stamp}`);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "02-payment-simulated-file-and-release-desktop.png");
    await page.locator("#pay-detail-close").click();

    // 3. Reconcile seeded unmatched debit line TRF-BCA-20261115-01 against PAY-2026-00001 (Dr 1120 / Cr 1110) + import extra CSV statement
    await page.locator("#pay-tab-reconciliation").click();
    await expect(page.locator("#pay-tab-reconciliation")).toHaveAttribute("aria-selected", "true");
    await expect(page.locator("#pay-statement-lines-table")).toBeVisible();
    await page.getByTestId("reconcile-line-TRF-BCA-20261115-01").click();
    await expect(page.locator("#pay-reconcile-drawer")).toBeVisible();
    await page.locator("#pay-reconcile-confirm-btn").click();
    await expect(page.locator("#pay-matches-table")).toContainText("BMK-");

    // Import fresh CSV statement row via UI
    await page.locator("#pay-import-toggle").click();
    await page.locator("#pay-import-ref").fill(`BCA-E2E-${stamp}`);
    await page.locator("#pay-import-open").fill("500000000");
    await page.locator("#pay-import-close").fill("498890000");
    await page
      .locator("#pay-import-csv")
      .fill(
        `ValueDate,Direction,Amount,BankReference,Counterparty,Description\n2026-11-16,DEBIT,1110000,TRF-E2E-${stamp},PT Sumber Makmur,E2E transfer`,
      );
    await page.locator("#pay-import-submit").click();
    await expect(page.locator("#pay-statement-lines-table")).toContainText(`TRF-E2E-${stamp}`);
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "03-bank-statement-reconciliation-desktop.png");

    // 4. Verify Bank Accounts & Live 1120 Clearing Proof (Discrepancy == Rp 0)
    await page.locator("#pay-tab-accounts").click();
    await expect(page.locator("#pay-tab-accounts")).toHaveAttribute("aria-selected", "true");
    await expect(
      page.locator("#pay-panel-accounts").getByText(/Kliring 1120 Cocok|1120 Clearing Reconciled/),
    ).toBeVisible();
    await expect(page.locator("#pay-panel-accounts").getByText("BCA-IDR-001").first()).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "04-clearing-proof-and-bank-accounts-desktop.png");

    // 5. Read-only NUSA-SBY view for Budi (GET 200, no create/import buttons)
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/payments`);
    await expect(page.locator("#pay-readonly-banner")).toBeVisible();
    await expect(page.locator("#pay-composer-toggle")).toHaveCount(0);
    await page.locator("#pay-tab-accounts").click();
    await expect(page.locator("#pay-tab-accounts")).toHaveAttribute("aria-selected", "true");
    await expect(page.locator("#pay-panel-accounts").getByText("BCA-SBY-001").first()).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "05-payments-readonly-sby-desktop.png");

    // 6. Mobile 390x844 responsive layout without horizontal overflow
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payments`);
    await expect(page.locator("#pay-payments-table")).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureTopScreenshot(page, "06-payments-mobile.png");
  });
});
