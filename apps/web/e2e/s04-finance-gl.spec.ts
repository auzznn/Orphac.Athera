import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S04/screenshots");
const reviewDir = path.join(repoRoot, ".impeccable/review");

const TENANT_NUSA_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_NUSA_JKT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";
const ENTITY_NUSA_SBY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102";
const TENANT_SMDR_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01";
const ENTITY_SMDR_BDG_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb101";

const demoPassword = process.env.ATHERA_DEMO_PASSWORD ?? "AtheraDemo!2026";

async function signInAs(page: Page, email: string) {
  await page.setViewportSize({ width: 1280, height: 800 });
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

test.describe("S04 Financial Configuration and Immutable Posting Kernel", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
    fs.mkdirSync(reviewDir, { recursive: true });
  });

  test("S04-E2E-01 U2 tries unbalanced/closed-period rejections, posts balanced journal, reverses it, and checks CoA & OD-07 tax", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const memoText = `Biaya Kirim Jakarta ${stamp}`;

    await signInAs(page, "budi.santoso@demo.athera.local");

    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/finance`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText(
      /Konfigurasi Keuangan & Jurnal Buku Besar|Financial Configuration & General Ledger/,
    );

    // 1. Unbalanced attempt -> rejected with clear inline alert
    await page.locator("#journal-date").fill("2026-11-18");
    await page.locator("#journal-source-key").fill(`e2e:unbal-${stamp}`);
    await page.locator("#journal-memo").fill(`Uji Tidak Seimbang ${stamp}`);
    await page.locator("#draft-debit-0").fill("250000");
    await page.locator("#draft-credit-1").fill("200000");
    await expect(page.getByTestId("draft-balance-badge")).toContainText(/Tidak Seimbang|Unbalanced/);
    await page.getByRole("button", { name: /Posting Jurnal Permanen|Commit Balanced Journal/ }).click();
    await expect(page.locator("#composer-error")).toContainText(/tidak seimbang|unbalanced/i);

    // 2. Closed period attempt (2026-10-15) -> rejected cleanly
    await page.locator("#journal-date").fill("2026-10-15");
    await page.locator("#journal-source-key").fill(`e2e:closed-${stamp}`);
    await page.locator("#draft-credit-1").fill("250000");
    await expect(page.getByTestId("draft-balance-badge")).toContainText(/Seimbang|Balanced/);
    await page.getByRole("button", { name: /Posting Jurnal Permanen|Commit Balanced Journal/ }).click();
    await expect(page.locator("#composer-error")).toContainText(/ditutup|closed/i);

    // 3. Valid balanced journal into OPEN 2026-11 -> commits atomically
    await page.locator("#journal-date").fill("2026-11-18");
    await page.locator("#journal-source-key").fill(`e2e:valid-${stamp}`);
    await page.locator("#journal-memo").fill(memoText);
    await page.getByRole("button", { name: /Posting Jurnal Permanen|Commit Balanced Journal/ }).click();

    const postedRow = page.locator("tbody tr", { hasText: memoText });
    await expect(postedRow).toContainText(/Diposting|Posted/);

    const desktopClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some((el) => el.scrollWidth > el.clientWidth + 1),
    );
    expect(desktopClipped).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s04-finance-journals-desktop.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s04-finance-journals-desktop.png"), path.join(reviewDir, "desktop.png"));

    await page.setViewportSize({ width: 390, height: 844 });
    const mobileOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(mobileOverflow).toBe(false);
    const mobileInnerClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some((el) => el.scrollWidth > el.clientWidth + 1),
    );
    expect(mobileInnerClipped).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s04-finance-journals-mobile.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s04-finance-journals-mobile.png"), path.join(reviewDir, "mobile.png"));
    await page.setViewportSize({ width: 1280, height: 800 });

    // 4. Reversal workflow -> single linked compensating REVERSAL row
    const revReason = `Koreksi E2E S04 ${stamp}`;
    await page.locator("#reversal-reason").fill(revReason);
    await page.getByRole("button", { name: /Posting Jurnal Pembalik|Post Reversal Journal/ }).click();

    await expect(page.locator("tbody tr", { hasText: revReason })).toContainText(/Jurnal Pembalik|Reversal/);
    await expect(page.locator("tbody tr", { hasText: memoText })).toContainText(/Telah Dibalik|Reversed/);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s04-finance-reversal-desktop.png"), fullPage: true });

    // 5. Chart of Accounts & Mappings tab
    await page.getByRole("tab", { name: /Bagan Akun & Pemetaan|Chart of Accounts & Mappings/ }).click();
    await expect(page.locator("#panel-coa")).toContainText("1000");
    await expect(page.locator("#panel-coa")).toContainText("GRNI_CLEARING");
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s04-finance-coa-desktop.png"), fullPage: true });

    // 6. Fiscal Periods & Tax Preview (12% DPP 11/12 -> 110,000 on 1,000,000)
    await page.getByRole("tab", { name: /Periode Fiskal & Pajak|Periods & Tax Rules/ }).click();
    const dppOptionVal = await page
      .locator("#tax-preview-code option", { hasText: "PPN-12-DPP1112" })
      .getAttribute("value");
    expect(dppOptionVal).toBeTruthy();
    await page.locator("#tax-preview-code").selectOption(dppOptionVal!);
    await page.locator("#tax-preview-amount").fill("1000000");
    await page.getByRole("button", { name: /Hitung Dasar & Pajak|Evaluate Tax & Base/ }).click();
    await expect(page.getByTestId("tax-preview-result")).toContainText("916.667");
    await expect(page.getByTestId("tax-preview-result")).toContainText("110.000");
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s04-finance-tax-periods-desktop.png"), fullPage: true });
  });

  test("S04-E2E-02 U2 on NUSA-SBY sees read-only GL, U1 gets 403 on NUSA-JKT finance and 404 on SMDR", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");

    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/finance`);
    await expect(page.getByText(/Mode baca saja|Read-only mode/)).toBeVisible();
    await expect(page.getByRole("button", { name: /Posting Jurnal Permanen|Commit Balanced Journal/ })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /Posting Jurnal Pembalik|Post Reversal Journal/ })).toHaveCount(0);
    const readOnlyRow = page.locator("tbody tr", { hasText: "JRN-2026-11-00001" });
    await expect(readOnlyRow).toBeVisible();
    const viewOnlyBtn = readOnlyRow.getByRole("button", { name: /Lihat Baris & Sumber|View Lines & Source/ });
    await expect(viewOnlyBtn).toBeVisible();
    await viewOnlyBtn.click();
    await expect(page.getByText(/Hash Payload|Payload Hash/)).toBeVisible();
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s04-finance-readonly-desktop.png"), fullPage: true });

    await signOut(page);

    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/finance`);
    await expect(page.locator("main").getByRole("alert")).toContainText(/Izin Entitas Tidak Mencukupi|Insufficient/);

    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/finance`);
    await expect(page.locator("main").getByRole("alert")).toContainText(
      /Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/,
    );
    const body = await page.locator("main").innerText();
    expect(body).not.toContain("SMDR");
    expect(body).not.toContain("Samudra");
  });
});
