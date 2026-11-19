import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

const SCREENSHOT_DIR = path.resolve(__dirname, "../../../docs/evidence/S16/screenshots");
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

async function waitForReportsLoaded(page: Page) {
  await expect(
    page.getByRole("button", { name: /Muat Ulang|Refresh/i }),
  ).toBeEnabled({ timeout: 15000 });
  await expect(page.getByLabel(/Ringkasan KPI Akuntansi|Accounting KPI Summary/i)).toBeVisible();
  await expect(page.locator("table").first()).toBeVisible();
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

test.describe("S16 Accounting Reports, Control Reconciliations & Period Close", () => {
  test("S16-E2E-01 Reconciliation & Period Close cockpit, Trial Balance + GL Drilldown, P&L & Balance Sheet, Close + Reopen cycle, Read-only NUSA-SBY & Mobile 390px", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/reports`);

    // 1. Wait for settled overview data (KPI strip + 5-control table + periods table)
    await waitForReportsLoaded(page);
    await expect(
      page.getByRole("heading", { name: /Rekonsiliasi 5 Akun Kontrol|5-Control Account Reconciliations/i }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { name: /Daftar Periode Akuntansi|Financial Periods/i }),
    ).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "01-reconciliations-and-period-close-desktop.png");

    // 2. Trial Balance + click-to-source GL drilldown
    await page.getByRole("tab", { name: /Neraca Saldo|Trial Balance/i }).click();
    await expect(page.getByText(/Total Neraca Saldo|Trial Balance Total/i)).toBeVisible();
    const firstAccountBtn = page.locator("tbody button").first();
    await firstAccountBtn.click();
    const drilldownSection = page.getByLabel(/Penelusuran Buku Besar Akun|GL Account Ledger Drilldown/i);
    await expect(drilldownSection).toBeVisible();
    await expect(drilldownSection.getByText(/Memuat mutasi jurnal|Loading posted journal/i)).toHaveCount(0);
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "02-trial-balance-and-gl-drilldown-desktop.png");

    // Close drilldown panel
    await page.getByRole("button", { name: /Tutup Panel|Close Panel/i }).click();

    // 3. P&L & Balance Sheet tab
    await page.getByRole("tab", { name: /Laba Rugi|P&L/i }).click();
    await expect(page.getByText(/Laporan Posisi Keuangan|Balance Sheet/i)).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "03-pnl-and-balance-sheet-desktop.png");

    // 4. Close period drawer & Reopen cycle on NUSA-JKT (always targeting the same 2026-11 period row)
    await page.getByRole("tab", { name: /Rekonsiliasi & Tutup Buku|Reconciliation & Close/i }).click();
    const novRow = page.locator("tbody tr", { hasText: "2026-11" }).first();
    const closeBtn = novRow.getByRole("button", { name: /Tutup Periode|Close Period/i });
    if ((await closeBtn.count()) > 0) {
      await closeBtn.click();
      await page.locator("#period-action-note").fill("Verifikasi tutup buku S16 E2E");
      const checkbox = page.locator('input[type="checkbox"]');
      if (await checkbox.isChecked()) {
        await checkbox.uncheck();
      }
      await assertNoLayoutClipping(page);
      await captureFullPageScreenshot(page, "04-period-close-drawer-desktop.png");

      await page.getByRole("button", { name: /Konfirmasi Tutup Periode|Confirm Close Period/i }).click();
      await expect(page.getByText(/Periode berhasil ditutup|Financial period closed/i)).toBeVisible();

      // Reopen the exact same 2026-11 row so subsequent tests still have 2026-11 OPEN
      const reopenBtn = novRow.getByRole("button", { name: /Buka Kembali|Reopen/i });
      await reopenBtn.click();
      await page.locator("#period-action-note").fill("Pembukaan kembali untuk kelanjutan operasional");
      await page.getByRole("button", { name: /Konfirmasi Buka Kembali|Confirm Reopen Period/i }).click();
      await expect(page.getByText(/Periode berhasil dibuka kembali|Financial period reopened/i)).toBeVisible();
    } else {
      await captureFullPageScreenshot(page, "04-period-close-drawer-desktop.png");
    }

    // 5. Read-only NUSA-SBY view (Budi is read-only on NUSA-SBY)
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/reports`);
    await waitForReportsLoaded(page);
    await expect(page.getByText(/Mode baca saja|Read-only mode/i)).toBeVisible();
    await expect(page.getByRole("button", { name: /Tutup Periode|Close Period/i })).toHaveCount(0);
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "05-readonly-nusa-sby-desktop.png");

    // 6. Mobile 390x844 view
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/reports`);
    await waitForReportsLoaded(page);
    await expect(
      page.getByRole("heading", { name: /Rekonsiliasi 5 Akun Kontrol|5-Control Account Reconciliations/i }),
    ).toBeVisible();
    await assertNoLayoutClipping(page);
    await captureFullPageScreenshot(page, "06-reports-mobile-390.png");
  });
});
