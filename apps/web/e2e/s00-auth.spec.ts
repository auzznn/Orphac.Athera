import fs from "node:fs";
import path from "node:path";
import { expect, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S00/screenshots");
const reviewDir = path.join(repoRoot, ".impeccable/review");

test.describe("S00 Authenticated Vertical Path", () => {
  test("unauthenticated redirect, CSRF check, login failure/success, screenshots and sign out", async ({
    page,
    request,
  }) => {
    fs.mkdirSync(evidenceDir, { recursive: true });
    fs.mkdirSync(reviewDir, { recursive: true });

    // 1. Direct protected API probe without session returns 401
    const unauthMe = await request.get("/api/auth/me");
    expect(unauthMe.status()).toBe(401);

    // 2. Direct unsafe request without CSRF token returns 403
    const noCsrfLogout = await request.post("/api/auth/logout");
    expect(noCsrfLogout.status()).toBe(403);
    const noCsrfBody = await noCsrfLogout.json();
    expect(noCsrfBody.errorCode).toBe("csrf_validation_failed");

    // 3. Unauthenticated visit to / redirects to /login
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto("/");
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    await page.screenshot({ path: path.join(evidenceDir, "s00-login-desktop.png"), fullPage: true });

    // 4. Invalid credentials stay on /login with accessible error alert
    await page.getByLabel(/Email Perusahaan|Work Email/).fill("rina.utami@demo.athera.local");
    await page.getByLabel(/Kata Sandi|Password/).fill("WrongPass!9999");
    await page.getByRole("button", { name: /Masuk ke Athera|Sign in to Athera/ }).click();

    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);

    // 5. Valid credentials sign in and load PostgreSQL identity record on /
    const demoPassword = process.env.ATHERA_DEMO_PASSWORD ?? "AtheraDemo!2026";
    await page.getByLabel(/Kata Sandi|Password/).fill(demoPassword);
    await page.getByRole("button", { name: /Masuk ke Athera|Sign in to Athera/ }).click();

    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole("banner").getByText("Rina Utami (Demo Procurement)")).toBeVisible();
    await expect(page.getByRole("cell", { name: "Rina Utami (Demo Procurement)" })).toBeVisible();
    await expect(page.getByRole("cell", { name: "S00_DEFAULT_DENY" })).toBeVisible();

    await page.screenshot({ path: path.join(evidenceDir, "s00-shell-desktop.png"), fullPage: true });
    fs.copyFileSync(
      path.join(evidenceDir, "s00-shell-desktop.png"),
      path.join(reviewDir, "desktop.png"),
    );

    // 6. Mobile viewport check (390x844) without horizontal overflow
    await page.setViewportSize({ width: 390, height: 844 });
    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth,
    );
    expect(hasHorizontalOverflow).toBe(false);
    await page.screenshot({ path: path.join(evidenceDir, "s00-shell-mobile.png"), fullPage: true });
    fs.copyFileSync(
      path.join(evidenceDir, "s00-shell-mobile.png"),
      path.join(reviewDir, "mobile.png"),
    );

    // 7. Sign out clears session cookie and subsequent /api/auth/me returns 401
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.getByRole("button", { name: /Keluar|Sign out/ }).click();
    await expect(page).toHaveURL(/\/login$/);

    const postSignOutMe = await page.request.get("/api/auth/me");
    expect(postSignOutMe.status()).toBe(401);
  });
});
