import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S07/screenshots");

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

async function assertNoLayoutClipping(page: Page) {
  const docOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth > window.innerWidth + 1,
  );
  expect(docOverflow).toBe(false);

  const innerClipped = await page.evaluate(() =>
    Array.from(document.querySelectorAll('[class*="overflow-x-auto"]')).some(
      (el) => el.scrollWidth > el.clientWidth + 1,
    ),
  );
  expect(innerClipped).toBe(false);
}

async function apiJson<T>(
  page: Page,
  method: "GET" | "POST",
  url: string,
  body?: unknown,
): Promise<T> {
  return page.evaluate(
    async ({ m, u, b }) => {
      const headers: Record<string, string> = {};
      if (m !== "GET") {
        const csrfRes = await fetch("/api/auth/csrf", { credentials: "include" });
        const csrfData = (await csrfRes.json()) as { requestToken: string };
        headers["Content-Type"] = "application/json";
        headers["X-CSRF-Token"] = csrfData.requestToken;
      }
      const res = await fetch(u, {
        method: m,
        credentials: "include",
        headers,
        body: b !== undefined ? JSON.stringify(b) : undefined,
      });
      const text = await res.text();
      if (!res.ok) {
        throw new Error(`API ${m} ${u} failed (${res.status}): ${text}`);
      }
      return (text ? JSON.parse(text) : null) as T;
    },
    { m: method, u: url, b: body },
  );
}

test.describe("S07 Valued Acceptance, Moving-Average Inventory & Open GRNI", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
  });

  test("S07-E2E-01 Gate 2 closed-period block, preview-before-confirm moving-average recalculation, GRNI & inventory ledger", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const itemCode = `ITM-S07-${stamp}`;
    const sj1 = `SJ-S07-${stamp}-01`;
    const sj2 = `SJ-S07-${stamp}-02`;

    // 1. Setup repeatable two-batch stocked item & POs via U1 (create & submit) + U2 (four-eyes approve & Batch 1 baseline)
    await signInAs(page, "rina.utami@demo.athera.local");
    const catBase = `/api/workspaces/${TENANT_NUSA_ID}/entities/${ENTITY_NUSA_JKT_ID}/catalog`;
    const procBase = `/api/workspaces/${TENANT_NUSA_ID}/entities/${ENTITY_NUSA_JKT_ID}/procurement`;
    const rcvBase = `/api/workspaces/${TENANT_NUSA_ID}/entities/${ENTITY_NUSA_JKT_ID}/receiving`;

    const refData = await apiJson<{
      suppliers: { partnerId: string; partnerCode: string }[];
    }>(page, "GET", `${procBase}/reference-data`);
    const uoms = await apiJson<{ items: { uomId: string; uomCode: string }[] }>(
      page,
      "GET",
      `${catBase}/uoms`,
    );

    const supplierId = refData.suppliers.find((s) => s.partnerCode === "PT-SUP-001")!.partnerId;
    const pcsUomId = uoms.items.find((u) => u.uomCode === "PCS")!.uomId;

    const createdItem = await apiJson<{ itemId: string }>(page, "POST", `${catBase}/items`, {
      itemCode,
      name: `Pelat Baja S07 ${stamp}`,
      itemKind: "STOCKED_GOOD",
      baseUomId: pcsUomId,
      defaultPurchaseUomId: pcsUomId,
      defaultSalesUomId: pcsUomId,
      entityScopeMode: "ALL_ENTITIES",
    });

    type PoCreated = {
      purchaseOrderId: string;
      revisions: { revisionId: string; lines: { poLineId: string }[] }[];
    };
    const po1 = await apiJson<PoCreated>(page, "POST", `${procBase}/purchase-orders`, {
      supplierPartnerId: supplierId,
      currencyCode: "IDR",
      orderDate: "2026-11-05",
      expectedDeliveryDate: "2026-11-18",
      deliveryDestination: "Gudang Pusat Jakarta",
      paymentTermsDays: 30,
      tolerancePolicyId: null,
      notes: "S07 E2E Batch 1",
      lines: [
        {
          lineageLineKey: "L001",
          itemId: createdItem.itemId,
          orderedUomId: pcsUomId,
          orderedQuantity: 10,
          unitPrice: 850_000,
          taxCodeId: null,
        },
      ],
    });
    await apiJson(
      page,
      "POST",
      `${procBase}/purchase-orders/${po1.purchaseOrderId}/revisions/${po1.revisions[0].revisionId}/submit`,
      { notes: null },
    );

    const po2 = await apiJson<PoCreated>(page, "POST", `${procBase}/purchase-orders`, {
      supplierPartnerId: supplierId,
      currencyCode: "IDR",
      orderDate: "2026-11-06",
      expectedDeliveryDate: "2026-11-19",
      deliveryDestination: "Gudang Pusat Jakarta",
      paymentTermsDays: 30,
      tolerancePolicyId: null,
      notes: "S07 E2E Batch 2",
      lines: [
        {
          lineageLineKey: "L001",
          itemId: createdItem.itemId,
          orderedUomId: pcsUomId,
          orderedQuantity: 10,
          unitPrice: 950_000,
          taxCodeId: null,
        },
      ],
    });
    await apiJson(
      page,
      "POST",
      `${procBase}/purchase-orders/${po2.purchaseOrderId}/revisions/${po2.revisions[0].revisionId}/submit`,
      { notes: null },
    );

    // U2 approves both POs, posts Batch 1 (10 @ 850k -> 8.5M baseline), and releases Batch 2 for Gate 2 UI test
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");

    await apiJson(
      page,
      "POST",
      `${procBase}/purchase-orders/${po1.purchaseOrderId}/revisions/${po1.revisions[0].revisionId}/decide`,
      { decision: "APPROVED", reason: "S07 E2E Batch 1 Approved" },
    );
    await apiJson(
      page,
      "POST",
      `${procBase}/purchase-orders/${po2.purchaseOrderId}/revisions/${po2.revisions[0].revisionId}/decide`,
      { decision: "APPROVED", reason: "S07 E2E Batch 2 Approved" },
    );

    type GrnCreated = {
      goodsReceiptId: string;
      lines: { goodsReceiptLineId: string }[];
    };
    const grn1 = await apiJson<GrnCreated>(page, "POST", `${rcvBase}/receipts`, {
      purchaseOrderId: po1.purchaseOrderId,
      supplierDeliveryNoteNumber: sj1,
      idempotencyKey: `e2e-s07-gr1-${stamp}`,
      receivedDate: "2026-11-05",
      receivingStation: "Dermaga Utama",
      vehicleOrSealRef: null,
      documentEvidenceRef: null,
      notes: "S07 E2E Batch 1 Arrival",
      lines: [
        {
          poLineId: po1.revisions[0].lines[0].poLineId,
          receivedUomId: pcsUomId,
          physicallyDeliveredQuantity: 10,
          flagForInspection: false,
          discrepancyReasonCode: "NONE",
        },
      ],
    });
    await apiJson(
      page,
      "POST",
      `${rcvBase}/receipts/${grn1.goodsReceiptId}/lines/${grn1.lines[0].goodsReceiptLineId}/dock-checks`,
      {
        idempotencyKey: `e2e-s07-dc1-${stamp}`,
        pendingCheckBaseQuantity: 0,
        dockHeldBaseQuantity: 0,
        dockReleasedBaseQuantity: 10,
        dockRejectedBaseQuantity: 0,
        discrepancyReasonCode: "NONE",
      },
    );
    const acc1 = await apiJson<{ acceptanceNumber: string; accrualNumber: string }>(
      page,
      "POST",
      `${rcvBase}/receipts/${grn1.goodsReceiptId}/accept`,
      {
        idempotencyKey: `e2e-s07-acc1-${stamp}`,
        acceptanceDate: "2026-11-05",
        stockStatus: "UNRESTRICTED",
        notes: "S07 E2E Batch 1 baseline",
        lines: [{ goodsReceiptLineId: grn1.lines[0].goodsReceiptLineId, acceptedBaseQuantity: 10 }],
      },
    );

    const grn2 = await apiJson<GrnCreated>(page, "POST", `${rcvBase}/receipts`, {
      purchaseOrderId: po2.purchaseOrderId,
      supplierDeliveryNoteNumber: sj2,
      idempotencyKey: `e2e-s07-gr2-${stamp}`,
      receivedDate: "2026-11-06",
      receivingStation: "Dermaga Utama",
      vehicleOrSealRef: null,
      documentEvidenceRef: null,
      notes: "S07 E2E Batch 2 Arrival",
      lines: [
        {
          poLineId: po2.revisions[0].lines[0].poLineId,
          receivedUomId: pcsUomId,
          physicallyDeliveredQuantity: 10,
          flagForInspection: false,
          discrepancyReasonCode: "NONE",
        },
      ],
    });
    await apiJson(
      page,
      "POST",
      `${rcvBase}/receipts/${grn2.goodsReceiptId}/lines/${grn2.lines[0].goodsReceiptLineId}/dock-checks`,
      {
        idempotencyKey: `e2e-s07-dc2-${stamp}`,
        pendingCheckBaseQuantity: 0,
        dockHeldBaseQuantity: 0,
        dockReleasedBaseQuantity: 10,
        dockRejectedBaseQuantity: 0,
        discrepancyReasonCode: "NONE",
      },
    );

    // 2. Drive Gate 2 Valued Acceptance UI on SJ-S07-{stamp}-02
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/receiving`);
    const batch2Row = page.locator("tbody tr", { hasText: sj2 });
    await expect(batch2Row).toBeVisible();
    await batch2Row.getByRole("button", { name: /Inspeksi|Inspect|Sedang Dibuka|Inspecting/ }).click();

    await expect(page.locator("#gate2-acceptance-panel")).toBeVisible();
    const confirmBtn = page.locator("#gate2-confirm-btn");
    await expect(confirmBtn).toBeDisabled();

    // Closed period preview (2026-10-15) returns block & keeps confirm disabled
    await page.locator("#gate2-acceptance-date").fill("2026-10-15");
    await page.locator("#gate2-preview-btn").click();
    await expect(page.locator("#gate2-preview-blocked")).toBeVisible();
    await expect(confirmBtn).toBeDisabled();

    // Open period preview (2026-11-10) recalculates MAC 850.000 -> 900.000 & balanced 1300/2110 journal
    await page.locator("#gate2-acceptance-date").fill("2026-11-10");
    await page.locator("#gate2-preview-btn").click();
    await expect(page.locator("#gate2-preview-card")).toBeVisible();
    await expect(page.locator("#gate2-preview-card")).toContainText("850.000");
    await expect(page.locator("#gate2-preview-card")).toContainText("900.000");
    await expect(page.locator("#gate2-preview-card")).toContainText("9.500.000");
    await expect(page.locator("#gate2-preview-card")).toContainText("1300");
    await expect(page.locator("#gate2-preview-card")).toContainText("2110");
    await expect(confirmBtn).toBeEnabled();

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "01-receiving-gate2-preview-desktop.png"),
      fullPage: true,
    });

    // Capture 390x844 mobile viewport of Gate 2 acceptance & preview reflow before confirming
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.locator("#gate2-preview-card")).toBeVisible();
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "05-receiving-gate2-mobile.png"),
      fullPage: true,
    });
    await page.setViewportSize({ width: 1280, height: 800 });

    // Confirm Gate 2 Valued Acceptance & capture ACC/GRNI reference
    await confirmBtn.click();
    await expect(page.locator("#gate2-success-banner")).toBeVisible();
    const bannerText = await page.locator("#gate2-success-banner").innerText();
    const accMatch = /ACC-\d{4}-\d{5}/.exec(bannerText);
    const grniMatch = /GRNI-\d{4}-\d{5}/.exec(bannerText);
    expect(accMatch).not.toBeNull();
    expect(grniMatch).not.toBeNull();
    const acc2Number = accMatch![0];
    const grni2Number = grniMatch![0];

    await expect(page.locator("#gate2-linked-acceptances")).toBeVisible();
    await expect(page.getByTestId("gate1-gl-count-card")).toContainText("0");

    // 3. Navigate via sidebar link to /inventory & inspect moving-average pool + sequential ledger
    await page.getByRole("link", { name: /^Persediaan$|^Inventory$/ }).first().click();
    await expect(page).toHaveURL(new RegExp(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/inventory$`));

    const searchInput = page.getByRole("searchbox");
    await searchInput.fill(itemCode);
    await searchInput.press("Enter");

    const itemRow = page.locator("tbody tr", { hasText: itemCode }).first();
    await expect(itemRow).toBeVisible();
    await itemRow.getByRole("button").click();

    await expect(page.locator("#inv-item-ledger-sheet")).toContainText(itemCode);
    await expect(page.locator("#inv-item-ledger-sheet")).toContainText("#1");
    await expect(page.locator("#inv-item-ledger-sheet")).toContainText("#2");
    await expect(page.locator("#inv-item-ledger-sheet")).toContainText("900.000");
    await expect(page.locator("#inv-item-ledger-sheet")).toContainText("18.000.000");

    // Switch to English & capture desktop ledger screenshot
    await page.getByRole("button", { name: "English", exact: true }).click();
    await expect(page.getByRole("heading", { name: /Inventory, Moving-Average Valuation/ })).toBeVisible();
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "02-inventory-ledger-desktop.png"),
      fullPage: true,
    });

    // Clear search & inspect Open GRNI Accruals tab
    await searchInput.fill("");
    await searchInput.press("Enter");
    await page.locator("#inv-tab-accruals").click();
    await expect(page.locator("#inv-tab-accruals")).toHaveAttribute("aria-selected", "true");
    await expect(page.locator("main")).toContainText(acc1.accrualNumber);
    await expect(page.locator("main")).toContainText(grni2Number);
    await expect(page.locator("main")).toContainText(acc2Number);
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "03-inventory-grni-desktop.png"),
      fullPage: true,
    });

    // Mobile 390x844 inspection (focused on itemCode for a compact clean capture)
    await page.setViewportSize({ width: 390, height: 844 });
    await page.locator("#inv-tab-positions").click();
    await expect(page.locator("#inv-tab-positions")).toHaveAttribute("aria-selected", "true");
    await searchInput.fill(itemCode);
    await searchInput.press("Enter");
    await expect(page.locator("#inv-item-ledger-sheet")).toContainText(itemCode);
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "04-inventory-mobile.png"),
      fullPage: true,
    });

    // Return desktop viewport & Indonesian locale
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.getByRole("button", { name: "Bahasa Indonesia", exact: true }).click();

    // 4. NUSA-SBY read-only /inventory empty state (zero NUSA-JKT cross-entity leakage)
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/inventory`);
    await expect(page.getByTestId("inv-kpi-carrying")).toContainText("Rp");
    await expect(page.locator("main")).not.toContainText(itemCode);
    await expect(page.locator("main")).toContainText(/Belum Ada Saldo Persediaan Bernilai|No Valued Inventory Balances Yet/);

    // 5. Hostile cross-tenant route access by U1 (rina.utami) -> unavailable alert & zero foreign leak
    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/inventory`);
    await expect(page.locator("main").getByRole("alert")).toContainText(/Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/);
    const bodyText = await page.locator("main").innerText();
    expect(bodyText).not.toContain("SMDR");
    expect(bodyText).not.toContain("Samudra");
  });
});
