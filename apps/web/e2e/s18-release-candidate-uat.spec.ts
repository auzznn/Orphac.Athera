import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

const SCREENSHOT_DIR = path.resolve(__dirname, "../../../docs/evidence/S18/screenshots");
const DEMO_PASSWORD = process.env.ATHERA_DEMO_PASSWORD || "AtheraDemo!2026";
const TENANT_NUSA_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_NUSA_JKT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

async function signInAs(page: Page, email: string) {
  await page.setViewportSize({ width: 1280, height: 800 });
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
}

async function captureFullPage(page: Page, filename: string) {
  await page.evaluate(() => window.scrollTo(0, 0));
  const fullHeight = await page.evaluate(() =>
    Math.max(document.documentElement.scrollHeight, document.body.scrollHeight, 800),
  );
  const vp = page.viewportSize() ?? { width: 1280, height: 800 };
  await page.setViewportSize({ width: vp.width, height: fullHeight + 80 });
  await page.waitForTimeout(100);
  await page.screenshot({
    path: path.join(SCREENSHOT_DIR, filename),
    fullPage: true,
    animations: "disabled",
  });
  await page.setViewportSize(vp);
}

test.describe("S18 Cross-Module Release Candidate UAT & Multi-Viewport Polish Audit", () => {
  test("S18-E2E-01 Verifies Entity Overview, P2P Payables, O2C Receivables, Accounting Reports & Documents across 1280px desktop, 768px tablet, and 390px mobile with keyboard focusability", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");

    // 1. Entity Overview at 1280x800 desktop + keyboard Tab navigation check
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}`);
    await expect(page.getByRole("heading").first()).toBeVisible();
    await page.keyboard.press("Tab");
    const focusedTag = await page.evaluate(() => document.activeElement?.tagName ?? "");
    expect(["A", "BUTTON", "INPUT", "SELECT"]).toContain(focusedTag);
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
    await assertNoLayoutClipping(page);
    await captureFullPage(page, "01-entity-overview-1280.png");

    // 2. P2P Payables workbench at 1280x800 desktop
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/payables`);
    await expect(page.getByRole("heading").first()).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPage(page, "02-p2p-payables-1280.png");

    // 3. O2C Receivables & AR Aging workbench at 1280x800 desktop
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/receivables`);
    await expect(page.locator("#ar-gl-reconciled-badge")).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPage(page, "03-o2c-receivables-1280.png");

    // 4. Accounting Reports & 5-Control Reconciliations at 1280x800 desktop
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/reports`);
    await expect(page.getByRole("button", { name: /Segarkan|Muat Ulang|Refresh/i })).toBeEnabled({ timeout: 15000 });
    await expect(page.locator("table").first()).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPage(page, "04-accounting-reports-1280.png");

    // 5. Accounting Reports at 768x1024 tablet width
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/reports`);
    await expect(page.getByRole("button", { name: /Segarkan|Muat Ulang|Refresh/i })).toBeEnabled({ timeout: 15000 });
    await expect(page.locator("table").first()).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPage(page, "05-accounting-reports-tablet-768.png");

    // 6. Documents & Evidence Trails at 390x844 mobile width
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/documents`);
    await expect(page.getByRole("button", { name: /Segarkan|Muat Ulang|Refresh/i })).toBeEnabled({ timeout: 15000 });
    await assertNoLayoutClipping(page);
    await captureFullPage(page, "06-documents-mobile-390.png");
  });
});
