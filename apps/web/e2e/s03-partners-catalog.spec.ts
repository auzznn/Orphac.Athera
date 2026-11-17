import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S03/screenshots");
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

test.describe("S03 Partners, Catalogue and Unit of Measure", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
    fs.mkdirSync(reviewDir, { recursive: true });
  });

  test("S03-E2E-01 U1 creates dual partner & stock item with exact UoM 3 BOX = 72 PCS and NUSA-JKT eligibility", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const partnerCode = `DUAL-${stamp}`;
    const itemCode = `ITM-${stamp}`;

    await signInAs(page, "rina.utami@demo.athera.local");

    // 1. Partners workflow: create dual supplier+customer partner restricted to NUSA-JKT
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/partners`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText(
      /Mitra Bisnis|Business Partners/,
    );

    await page.getByLabel(/Kode Mitra|Partner Code/).fill(partnerCode);
    await page.getByLabel(/Nama Badan Hukum|Legal Entity Name/).fill(`PT Karya Niaga Utama ${stamp}`);
    await page.getByLabel(/Nama Tampilan|Display Name/).fill(`Karya Niaga ${stamp}`);
    await page.getByLabel(/Pelanggan|Customer/).check();
    await page.getByLabel(/Cakupan Entitas|Entity Scope/).selectOption("RESTRICTED_ENTITIES");
    await page.getByRole("button", { name: /Simpan Mitra Baru|Save New Partner/ }).click();

    await expect(page.locator("h2", { hasText: partnerCode })).toBeVisible();
    await page.getByLabel(/Nama Kontak|Contact Name/).fill("Dian Permata");
    await page.getByLabel(/Email Kontak|Contact Email/).fill("dian@karyaniaga.demo.local");
    await page.getByRole("button", { name: /Tambah Kontak|Add Contact/ }).click();
    await expect(page.getByText("Dian Permata")).toBeVisible();

    await page.getByLabel(/Izinkan sebagai Pemasok|Allow as Supplier/).check();
    await page.getByLabel(/Izinkan sebagai Pelanggan|Allow as Customer/).check();
    await page.getByRole("button", { name: /Simpan Profil Entitas|Save Entity Profile/ }).click();

    const partnerRow = page.locator("tbody tr", { hasText: partnerCode });
    await expect(partnerRow).toContainText(/Pemasok & Pelanggan|Supplier & Customer/);
    await expect(partnerRow).toContainText(/Bisa Beli|Purchasing Eligible/);
    await expect(partnerRow).toContainText(/Bisa Jual|Sales Eligible/);

    const desktopPartnersClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some((el) => el.scrollWidth > el.clientWidth + 1),
    );
    expect(desktopPartnersClipped).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s03-partners-desktop.png"), fullPage: true });

    await page.setViewportSize({ width: 390, height: 844 });
    const mobilePartnersOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(mobilePartnersOverflow).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s03-partners-mobile.png"), fullPage: true });
    await page.setViewportSize({ width: 1280, height: 800 });

    // 2. Catalogue & UoM workflow: create stock item, set 1 BOX = 24 PCS, verify 3 BOX = 72 PCS live
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/catalog`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText(
      /Katalog Barang & Satuan UoM|Item Catalogue & Units of Measure/,
    );

    await page.getByLabel(/^Kode Barang$|^Item Code$/).fill(itemCode);
    await page.getByLabel(/^Nama Barang \/ Jasa$|^Item \/ Service Name$/).fill(`Kardus Kemasan Standar ${stamp}`);
    await page.locator("#new-item-scope").selectOption("RESTRICTED_ENTITIES");
    await page.getByRole("button", { name: /Simpan Item Katalog|Save Catalogue Item/ }).click();

    await expect(page.locator("h2", { hasText: itemCode })).toBeVisible();
    await page.locator("#conv-factor").fill("24");
    await page.getByRole("button", { name: /Simpan Konversi|Save Conversion/ }).click();
    await expect(page.getByText(/1 BOX = 24 PCS/)).toBeVisible();

    await page.getByLabel(/Kuantitas Sumber|Source Quantity/).fill("3");
    await page.getByRole("button", { name: /Hitung ke Satuan Dasar|Convert to Base UoM/ }).click();
    await expect(page.getByTestId("uom-conversion-result")).toContainText("3 BOX = 72 PCS");
    await expect(page.getByTestId("uom-conversion-result")).toContainText("3 BOX");

    await page.getByLabel(/Izinkan Pengadaan|Allow Procurement/).check();
    await page.getByLabel(/Izinkan Penjualan|Allow Sales/).check();
    await page.getByRole("button", { name: /Simpan Kebijakan Entitas|Save Entity Policy/ }).click();
    const itemRow = page.locator("tbody tr", { hasText: itemCode });
    await expect(itemRow).toContainText(/Beli Diizinkan|Purchase Allowed/);

    const desktopCatalogClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some((el) => el.scrollWidth > el.clientWidth + 1),
    );
    expect(desktopCatalogClipped).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s03-catalog-desktop.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s03-catalog-desktop.png"), path.join(reviewDir, "desktop.png"));

    await page.setViewportSize({ width: 390, height: 844 });
    const mobileCatalogOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(mobileCatalogOverflow).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s03-catalog-mobile.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s03-catalog-mobile.png"), path.join(reviewDir, "mobile.png"));
  });

  test("S03-E2E-02 U2 on NUSA-SBY sees read-only restrictions and U1 is denied foreign SMDR master URL", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");

    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/partners`);
    await expect(page.getByText(/Mode baca saja|Read-only mode/)).toBeVisible();
    await expect(page.getByRole("button", { name: /Simpan Mitra Baru|Save New Partner/ })).toHaveCount(0);

    const restrictedPartner = page.locator("tbody tr", { hasText: "PT-DUAL-001" });
    await expect(restrictedPartner).toContainText(/Tidak Layak di Entitas Ini|Ineligible Here/);
    const viewDetailsBtn = restrictedPartner.getByRole("button", { name: /Lihat Rincian|View Details/ });
    await expect(viewDetailsBtn).toBeVisible();
    await viewDetailsBtn.click();
    await expect(page.getByRole("heading", { name: /PT-DUAL-001/ })).toBeVisible();
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s03-readonly-restricted-desktop.png"), fullPage: true });

    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/catalog`);
    await expect(page.getByText(/Mode baca saja|Read-only mode/)).toBeVisible();
    await expect(page.getByRole("button", { name: /Simpan Item Katalog|Save Catalogue Item/ })).toHaveCount(0);
    const restrictedItem = page.locator("tbody tr", { hasText: "ITM-RES-002" });
    await expect(restrictedItem).toContainText(/Diblokir di Entitas Ini|Blocked Here/);
    const viewItemBtn = restrictedItem.getByRole("button", { name: /Lihat UoM & Kebijakan|View UoM & Policy/ });
    await expect(viewItemBtn).toBeVisible();
    await viewItemBtn.click();
    await expect(page.getByRole("heading", { name: /ITM-RES-002/ })).toBeVisible();

    await signOut(page);

    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/partners`);
    await expect(page.locator("main").getByRole("alert")).toContainText(
      /Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/,
    );
    const body = await page.locator("main").innerText();
    expect(body).not.toContain("SMDR");
    expect(body).not.toContain("Logistik Priangan");
  });
});
