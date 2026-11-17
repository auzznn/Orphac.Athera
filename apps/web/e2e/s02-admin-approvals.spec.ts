import fs from "node:fs";
import path from "node:path";
import { expect, test, Page } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S02/screenshots");
const reviewDir = path.join(repoRoot, ".impeccable/review");

const TENANT_NUSA_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_NUSA_JKT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

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

test.describe("S02 Organization Administration, Approvals, Audit and Outbox", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
    fs.mkdirSync(reviewDir, { recursive: true });
  });

  test("S02-E2E-01 U2 invites U3, U3 accepts token and gains NUSA-JKT, then U2 dispatches outbox and revokes U3", async ({
    page,
  }) => {
    // 1. Admin U2 opens Administration sheet for NUSA-JKT and invites U3
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/admin`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText(
      /Administrasi Entitas, Undangan & Audit|Entity Administration, Invitations & Audit/,
    );

    const desktopClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some(
        (el) => el.scrollWidth > el.clientWidth + 1,
      ),
    );
    expect(desktopClipped).toBe(false);

    await page.getByLabel(/Email Penerima|Recipient Email/).fill("hendra.wijaya@demo.athera.local");
    await page.getByRole("button", { name: /Buat Undangan|Create Invitation/ }).click();

    const tokenBanner = page.getByTestId("issued-invite-token");
    await expect(tokenBanner).toBeVisible();
    const rawToken = (await tokenBanner.innerText()).trim();
    expect(rawToken.length).toBeGreaterThan(20);

    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s02-admin-desktop.png"), fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    const mobileAdminOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(mobileAdminOverflow).toBe(false);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(evidenceDir, "s02-admin-mobile.png"), fullPage: true });
    await page.setViewportSize({ width: 1280, height: 800 });

    await signOut(page);

    // 2. Unassigned user U3 signs in and redeems invitation token
    await signInAs(page, "hendra.wijaya@demo.athera.local");
    await page.goto(`/invitations?token=${encodeURIComponent(rawToken)}`);
    await page.getByRole("button", { name: /Terima Undangan & Aktifkan Akses|Accept Invitation & Activate Scope/ }).click();
    await expect(page.getByText(/Undangan Diterima|Invitation Accepted/)).toBeVisible();
    await expect(page.getByRole("banner").getByText(/Pilih Entitas \(1\)|Select Entity \(1\)/).first()).toBeVisible();
    await expect(page.getByText(/Tanpa Ruang Kerja Aktif|No Active Workspace/)).toHaveCount(0);
    await page.screenshot({ path: path.join(evidenceDir, "s02-invitation-accept-desktop.png"), fullPage: true });

    await page.getByRole("link", { name: /Masuk ke Entitas Sekarang|Open Granted Entity/ }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}$`));
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("PT Nusantara Distribusi Jakarta");
    await signOut(page);

    // 3. Admin U2 returns to NUSA-JKT Admin, dispatches outbox, and revokes U3 cleanly (restoring default-deny state)
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/admin`);
    await page.getByRole("button", { name: /Proses Antrean Outbox|Process Outbox Queue/ }).click();

    const hendraRow = page
      .locator("tbody tr", { hasText: "hendra.wijaya@demo.athera.local" })
      .filter({ has: page.getByRole("button", { name: /Cabut Akses|Revoke Access/ }) });
    await hendraRow.getByRole("button", { name: /Cabut Akses|Revoke Access/ }).click();
    await expect(
      page.locator("tbody tr", { hasText: "hendra.wijaya@demo.athera.local" }).first(),
    ).toContainText(/Dicabut|Revoked|REVOKED/i);
    await expect(page.getByText("MEMBERSHIP_REVOKED").first()).toBeVisible();
  });

  test("S02-E2E-02 U1 submits approval request, self-approval blocked in UI, and U2 approves independently", async ({
    page,
  }) => {
    const justification = `E2E Sales Read Request ${Date.now()}`;

    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/approvals`);
    await page.getByLabel(/Izin yang Diminta|Requested Permission/).selectOption("sales.read");
    await page.getByLabel(/Justifikasi Operasional|Business Justification/).fill(justification);
    await page.getByRole("button", { name: /Kirim Permintaan Persetujuan|Submit Approval Request/ }).click();

    const ownRow = page.locator("tr", { hasText: justification });
    await expect(ownRow).toBeVisible();
    await expect(ownRow).toContainText(/Aturan Empat Mata \(OD-02\)|Four-Eyes Rule \(OD-02\)/);
    await expect(ownRow.getByRole("button", { name: /Setujui|Approve/ })).toHaveCount(0);

    await page.screenshot({ path: path.join(evidenceDir, "s02-approvals-requester-desktop.png"), fullPage: true });
    await signOut(page);

    // Distinct authorized approver U2 reviews and approves
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/approvals`);
    const reviewRow = page.locator("tr", { hasText: justification });
    await expect(reviewRow).toBeVisible();
    await reviewRow.locator("input[type='text']").fill("Approved independently by group controller");
    await page.screenshot({ path: path.join(evidenceDir, "s02-approvals-pending-approver-desktop.png"), fullPage: true });
    await reviewRow.getByRole("button", { name: /Setujui|Approve/ }).click();
    await expect(reviewRow).toContainText(/Disetujui|Approved/);

    const desktopClipped = await page.evaluate(() =>
      Array.from(document.querySelectorAll(".overflow-x-auto")).some(
        (el) => el.scrollWidth > el.clientWidth + 1,
      ),
    );
    expect(desktopClipped).toBe(false);

    await page.screenshot({ path: path.join(evidenceDir, "s02-approvals-desktop.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s02-approvals-desktop.png"), path.join(reviewDir, "desktop.png"));

    await page.setViewportSize({ width: 390, height: 844 });
    const mobileOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(mobileOverflow).toBe(false);
    await page.screenshot({ path: path.join(evidenceDir, "s02-approvals-mobile.png"), fullPage: true });
    fs.copyFileSync(path.join(evidenceDir, "s02-approvals-mobile.png"), path.join(reviewDir, "mobile.png"));
  });
});
