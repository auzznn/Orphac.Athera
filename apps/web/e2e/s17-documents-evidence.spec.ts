import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

const SCREENSHOT_DIR = path.resolve(__dirname, "../../../docs/evidence/S17/screenshots");
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
}

async function waitForDocumentsLoaded(page: Page) {
  await expect(page.getByRole("button", { name: /Muat Ulang|Refresh/i })).toBeEnabled({ timeout: 15000 });
  await expect(
    page.getByLabel(/Ringkasan KPI Dokumen|Documents and Operations KPI Summary/i),
  ).toBeVisible();
}

async function captureFullPageScreenshot(page: Page, filename: string) {
  await page.evaluate(() => window.scrollTo(0, 0));
  const fullHeight = await page.evaluate(() =>
    Math.max(document.documentElement.scrollHeight, document.body.scrollHeight, 900),
  );
  const currentViewport = page.viewportSize() ?? { width: 1440, height: 900 };
  await page.setViewportSize({ width: currentViewport.width, height: fullHeight + 80 });
  await page.waitForTimeout(100);
  await page.screenshot({
    path: path.join(SCREENSHOT_DIR, filename),
    fullPage: true,
    animations: "disabled",
  });
  await page.setViewportSize(currentViewport);
}

test.describe("S17 Documents, Evidence Trails & Operational UX", () => {
  test("S17-E2E-01 Upload & link evidence with SHA-256, inspect Exceptions/Outbox & Audit Trail, verify read-only NUSA-SBY & Mobile 390px", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/documents`);
    await waitForDocumentsLoaded(page);

    // 1. Open Upload & Link Evidence drawer
    await page.getByRole("button", { name: /Unggah & Tautkan|Upload & Link/i }).click();
    await page.locator("#doc-file-input").setInputFiles({
      name: "faktur-pajak-s17.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.from("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF", "ascii"),
    });
    await page.locator("#doc-source-number").fill("INV-2026-00001");
    await page.locator("#doc-source-id").fill("INV-2026-00001");
    await page.locator("#doc-notes").fill("Bukti Faktur Pajak S17 terverifikasi");
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "01-upload-evidence-drawer-desktop.png");

    await page.getByRole("button", { name: /Simpan & Tautkan Bukti|Upload & Link Evidence/i }).click();
    await expect(page.getByText(/faktur-pajak-s17\.pdf/i).first()).toBeVisible({ timeout: 10000 });
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "02-documents-ledger-desktop.png");

    // 3. Exceptions & Outbox tab
    await page.getByRole("tab", { name: /Pengecualian|Exceptions/i }).click();
    await expect(
      page.getByRole("heading", { name: /Diagnostik Outbox|Integration Outbox Diagnostics/i }),
    ).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "03-exceptions-and-outbox-desktop.png");

    // 4. Audit Trail tab
    await page.getByRole("tab", { name: /Jejak Audit|Audit Trail/i }).click();
    await expect(page.getByText(/faktur-pajak-s17\.pdf/i).first()).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "04-audit-trail-desktop.png");

    // 5. Read-only NUSA-SBY view
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/documents`);
    await waitForDocumentsLoaded(page);
    await expect(page.getByText(/Mode baca saja|Read-only mode/i)).toBeVisible();
    await expect(page.getByRole("button", { name: /Unggah & Tautkan|Upload & Link/i })).toHaveCount(0);
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "05-readonly-nusa-sby-desktop.png");

    // 6. Mobile 390x844 view
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/documents`);
    await waitForDocumentsLoaded(page);
    await expect(page.getByText(/faktur-pajak-s17\.pdf/i).first()).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "06-documents-mobile-390.png");
  });
});
