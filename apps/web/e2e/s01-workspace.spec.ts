import fs from "node:fs";
import path from "node:path";
import { expect, test, Page } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S01/screenshots");
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

test.describe("S01 Secure Tenant and Legal-Entity Workspace", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
    fs.mkdirSync(reviewDir, { recursive: true });
  });

  test("S01-E2E-01 U1 sees only NUSA-JKT and hostile tenant/entity URLs fail safely with zero leak", async ({
    page,
  }) => {
    await signInAs(page, "rina.utami@demo.athera.local");

    await page.getByRole("link", { name: /Buka Direktori Ruang Kerja|Open Workspace Directory/ }).click();
    await expect(page).toHaveURL(/\/workspaces$/);

    await expect(page.getByText("NUSA-JKT")).toBeVisible();
    await expect(page.getByText("PT Nusantara Distribusi Jakarta")).toBeVisible();
    await expect(page.getByText("NUSA-SBY")).toHaveCount(0);
    await expect(page.getByText("SMDR-BDG")).toHaveCount(0);

    await page.getByRole("link", { name: /Masuk Ruang Kerja|Enter Workspace/ }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}$`));
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("PT Nusantara Distribusi Jakarta");
    await expect(page.getByRole("cell", { name: "01.234.567.8-011.000" })).toBeVisible();

    // Hostile direct URL to sibling entity A2 in same tenant must render unavailable without leaking A2 name/code
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}`);
    await expect(page.locator("main").getByRole("alert")).toContainText(/Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/);
    const bodyA2 = await page.locator("main").innerText();
    expect(bodyA2).not.toContain("NUSA-SBY");
    expect(bodyA2).not.toContain("Surabaya");

    await page.screenshot({ path: path.join(evidenceDir, "s01-denial-desktop.png"), fullPage: true });

    // Hostile direct URL to foreign Tenant B entity must fail cleanly with zero foreign details
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}`);
    await expect(page.locator("main").getByRole("alert")).toContainText(/Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/);
    const bodyB1 = await page.locator("main").innerText();
    expect(bodyB1).not.toContain("SMDR");
    expect(bodyB1).not.toContain("Samudra");
    expect(bodyB1).not.toContain("Bandung");
  });

  test("S01-E2E-02 U2 navigates multi-tenant directory, switches entities and tenants cleanly, and fits mobile", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");

    await page.getByRole("link", { name: /Ruang Kerja Entitas|Entity Workspaces/ }).click();
    await expect(page).toHaveURL(/\/workspaces$/);
    await expect(page.getByText("NUSA-JKT")).toBeVisible();
    await expect(page.getByText("NUSA-SBY")).toBeVisible();
    await expect(page.getByText("SMDR-BDG")).toBeVisible();

    const tableClippedDesktop = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some(
        (el) => el.scrollWidth > el.clientWidth + 1,
      ),
    );
    expect(tableClippedDesktop).toBe(false);
    await page.screenshot({ path: path.join(evidenceDir, "s01-directory-desktop.png"), fullPage: true });

    await page.setViewportSize({ width: 390, height: 844 });
    const directoryOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(directoryOverflow).toBe(false);
    await page.screenshot({ path: path.join(evidenceDir, "s01-directory-mobile.png"), fullPage: true });
    await page.setViewportSize({ width: 1280, height: 800 });

    // Enter NUSA-JKT first
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("PT Nusantara Distribusi Jakarta");
    await expect(page.getByRole("cell", { name: "01.234.567.8-011.000" })).toBeVisible();

    // Switch via top-bar Scope Switcher to NUSA-SBY
    await page.getByRole("button", { name: /Ganti ruang kerja entitas|Switch legal-entity workspace/ }).first().click();
    await page.locator("#scope-switcher-panel-desktop").getByRole("link", { name: /NUSA-SBY/ }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}$`));
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("PT Nusantara Niaga Surabaya");
    await expect(page.getByRole("cell", { name: "01.234.567.8-601.000" })).toBeVisible();

    // Switch across tenant to SMDR-BDG and ensure zero NUSA leakage in main ledger
    await page.getByRole("button", { name: /Ganti ruang kerja entitas|Switch legal-entity workspace/ }).first().click();
    await page.locator("#scope-switcher-panel-desktop").getByRole("link", { name: /SMDR-BDG/ }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}$`));
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("CV Samudra Teknik Bandung");
    await expect(page.getByRole("cell", { name: "02.345.678.9-421.000" })).toBeVisible();
    const mainText = await page.locator("main").innerText();
    expect(mainText).not.toContain("NUSA-JKT");
    expect(mainText).not.toContain("NUSA-SBY");

    await page.screenshot({ path: path.join(evidenceDir, "s01-entity-desktop.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s01-entity-desktop.png"), path.join(reviewDir, "desktop.png"));

    // Mobile responsive check (390x844) without horizontal overflow
    await page.setViewportSize({ width: 390, height: 844 });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(overflow).toBe(false);
    await page.screenshot({ path: path.join(evidenceDir, "s01-entity-mobile.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s01-entity-mobile.png"), path.join(reviewDir, "mobile.png"));
  });

  test("S01-E2E-03 U3 authenticated without workspaces sees accessible default-deny guidance", async ({
    page,
  }) => {
    await signInAs(page, "hendra.wijaya@demo.athera.local");

    await expect(page.getByRole("cell", { name: "NO_ACTIVE_WORKSPACE" })).toBeVisible();
    await page.getByRole("link", { name: /Buka Direktori Ruang Kerja|Open Workspace Directory/ }).click();
    await expect(page).toHaveURL(/\/workspaces$/);
    await expect(
      page.getByRole("heading", { name: /Belum Ada Ruang Kerja yang Diotorisasi|No Authorized Workspaces Assigned/ }),
    ).toBeVisible();
    await expect(page.getByText("hendra.wijaya@demo.athera.local").last()).toBeVisible();

    await page.screenshot({ path: path.join(evidenceDir, "s01-no-access-desktop.png"), fullPage: true });
  });
});
