import fs from "node:fs";
import path from "node:path";
import { expect, Page, test } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../../..");
const evidenceDir = path.join(repoRoot, "docs/evidence/S08/screenshots");

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

test.describe("S08 Stock Availability, Warehouses, Lots & Quality Restrictions", () => {
  test.beforeAll(() => {
    fs.mkdirSync(evidenceDir, { recursive: true });
  });

  test("S08-E2E-01 Catalog lot policy, Gate 2 quarantine receipt, U1/U2 SoD, partial disposition, putaway transfer & OD-10 reservations", async ({
    page,
  }) => {
    const stamp = String(Date.now()).slice(-6);
    const itemCode = `ITM-S08-${stamp}`;
    const sjNum = `SJ-S08-${stamp}`;
    const lotNum = `LOT-E2E-${stamp}`;

    // 1. U1 creates a REQUIRED lot-tracked stocked item on /catalog and submits PO (20 PCS @ 350k)
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/catalog`);
    await page.getByLabel(/^Kode Barang$|^Item Code$/).fill(itemCode);
    await page.getByLabel(/^Nama Barang \/ Jasa$|^Item \/ Service Name$/).fill(`Reagen Pelarut Uji ${stamp}`);
    await page.locator("#new-item-kind").selectOption("STOCKED_GOOD");
    await page.locator("#new-item-lot-policy").selectOption("REQUIRED");
    await page.locator("#new-item-scope").selectOption("ALL_ENTITIES");
    await page.getByRole("button", { name: /Simpan Item Katalog|Save Catalogue Item/ }).click();

    const catRow = page.locator("tbody tr", { hasText: itemCode }).first();
    await expect(catRow).toBeVisible();
    await expect(catRow).toContainText(/Lot Wajib|Required Lot/);

    const catBase = `/api/workspaces/${TENANT_NUSA_ID}/entities/${ENTITY_NUSA_JKT_ID}/catalog`;
    const procBase = `/api/workspaces/${TENANT_NUSA_ID}/entities/${ENTITY_NUSA_JKT_ID}/procurement`;
    const rcvBase = `/api/workspaces/${TENANT_NUSA_ID}/entities/${ENTITY_NUSA_JKT_ID}/receiving`;

    const refData = await apiJson<{ suppliers: { partnerId: string; partnerCode: string }[] }>(
      page,
      "GET",
      `${procBase}/reference-data`,
    );
    const uoms = await apiJson<{ items: { uomId: string; uomCode: string }[] }>(page, "GET", `${catBase}/uoms`);
    const itemsPaged = await apiJson<{ items: { itemId: string; itemCode: string }[] }>(
      page,
      "GET",
      `${catBase}/items?q=${encodeURIComponent(itemCode)}`,
    );

    const supplierId = refData.suppliers.find((s) => s.partnerCode === "PT-SUP-001")!.partnerId;
    const pcsUomId = uoms.items.find((u) => u.uomCode === "PCS")!.uomId;
    const itemId = itemsPaged.items.find((i) => i.itemCode === itemCode)!.itemId;

    type PoCreated = {
      purchaseOrderId: string;
      revisions: { revisionId: string; lines: { poLineId: string }[] }[];
    };
    const po = await apiJson<PoCreated>(page, "POST", `${procBase}/purchase-orders`, {
      supplierPartnerId: supplierId,
      currencyCode: "IDR",
      orderDate: "2026-11-11",
      expectedDeliveryDate: "2026-11-22",
      deliveryDestination: "Gudang Pusat Jakarta",
      paymentTermsDays: 30,
      tolerancePolicyId: null,
      notes: "S08 E2E Lot PO",
      lines: [
        {
          lineageLineKey: "L001",
          itemId,
          orderedUomId: pcsUomId,
          orderedQuantity: 20,
          unitPrice: 350_000,
          taxCodeId: null,
        },
      ],
    });
    await apiJson(
      page,
      "POST",
      `${procBase}/purchase-orders/${po.purchaseOrderId}/revisions/${po.revisions[0].revisionId}/submit`,
      { notes: null },
    );

    // 2. U2 approves PO, releases Gate 1 dock check, and drives Gate 2 quarantine acceptance with LOT & Expiry
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");

    await apiJson(
      page,
      "POST",
      `${procBase}/purchase-orders/${po.purchaseOrderId}/revisions/${po.revisions[0].revisionId}/decide`,
      { decision: "APPROVED", reason: "S08 E2E Lot Approved" },
    );

    type GrnCreated = { goodsReceiptId: string; lines: { goodsReceiptLineId: string }[] };
    const grn = await apiJson<GrnCreated>(page, "POST", `${rcvBase}/receipts`, {
      purchaseOrderId: po.purchaseOrderId,
      supplierDeliveryNoteNumber: sjNum,
      idempotencyKey: `e2e-s08-gr-${stamp}`,
      receivedDate: "2026-11-11",
      receivingStation: "Dermaga Utama",
      vehicleOrSealRef: null,
      documentEvidenceRef: null,
      notes: "S08 Lot Arrival",
      lines: [
        {
          poLineId: po.revisions[0].lines[0].poLineId,
          receivedUomId: pcsUomId,
          physicallyDeliveredQuantity: 20,
          flagForInspection: true,
          discrepancyReasonCode: "NONE",
        },
      ],
    });
    await apiJson(
      page,
      "POST",
      `${rcvBase}/receipts/${grn.goodsReceiptId}/lines/${grn.lines[0].goodsReceiptLineId}/dock-checks`,
      {
        idempotencyKey: `e2e-s08-dc-${stamp}`,
        pendingCheckBaseQuantity: 0,
        dockHeldBaseQuantity: 0,
        dockReleasedBaseQuantity: 20,
        dockRejectedBaseQuantity: 0,
        discrepancyReasonCode: "NONE",
      },
    );

    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/receiving`);
    const receiptRow = page.locator("tbody tr", { hasText: sjNum });
    await expect(receiptRow).toBeVisible();
    await receiptRow.getByRole("button", { name: /Inspeksi|Inspect|Sedang Dibuka|Inspecting/ }).click();

    await expect(page.locator("#gate2-acceptance-panel")).toBeVisible();
    await page.locator("#gate2-acceptance-date").fill("2026-11-12");
    await page.locator("#gate2-stock-status").selectOption("QUALITY_INSPECTION");
    await expect(page.locator("#gate2-lot-0")).toBeVisible();
    await page.locator("#gate2-lot-0").fill(lotNum);
    await page.locator("#gate2-exp-0").fill("2027-12-31");

    await page.locator("#gate2-preview-btn").click();
    await expect(page.locator("#gate2-preview-card")).toBeVisible();
    await expect(page.locator("#gate2-preview-card")).toContainText("7.000.000");
    await expect(page.locator("#gate2-preview-card")).toContainText(lotNum);

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "01-receiving-gate2-lot-quarantine-desktop.png"),
      fullPage: true,
    });

    await page.locator("#gate2-confirm-btn").click();
    await expect(page.locator("#gate2-success-banner")).toBeVisible();

    // 3. Verify U1 (inventory.manage WITHOUT quality.inspect) sees Quality Inspections tab & Eye read-only guard
    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/inventory`);

    await page.locator("#inv-tab-inspections").click();
    await expect(page.locator("#inv-inspections-panel")).toContainText(itemCode);
    await expect(page.locator("#inv-inspections-panel")).toContainText(lotNum);

    const inspRow = page.locator("#inv-inspections-panel tbody tr", { hasText: itemCode }).first();
    await inspRow.getByRole("button").click();

    await expect(page.locator("#inv-stock-detail-sheet")).toContainText(itemCode);
    await expect(page.getByTestId("inv-quality-readonly-banner")).toBeVisible();
    await expect(page.locator("#inv-disp-submit")).toHaveCount(0);
    await expect(page.locator("#inv-xfer-submit")).toBeVisible();
    await expect(page.locator("#inv-rsv-submit")).toBeVisible();

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "02-inventory-u1-quality-readonly-desktop.png"),
      fullPage: true,
    });

    // 4. Sign in as U2 (quality.inspect + inventory.manage): split disposition -> transfer to LOC-RACK-B2 -> reserve & release
    await signOut(page);
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_JKT_ID}/inventory`);

    const searchInput = page.getByRole("searchbox");
    await searchInput.fill(itemCode);
    await searchInput.press("Enter");
    const posRow = page.locator("tbody tr", { hasText: itemCode }).first();
    await expect(posRow).toBeVisible();
    await posRow.getByRole("button").click();

    await expect(page.locator("#inv-stock-detail-sheet")).toContainText(itemCode);
    await expect(page.getByTestId("inv-reconciled-badge")).toContainText(/Terekonsiliasi|Reconciled/);
    await expect(page.getByTestId("inv-metric-qi")).toContainText("20 PCS");

    // Partial split disposition: release 12 to LOC-MAIN, block 3 to LOC-BLK
    const relLocMainVal = await page
      .locator("#inv-disp-release-loc option", { hasText: "LOC-MAIN" })
      .first()
      .getAttribute("value");
    const blkLocVal = await page
      .locator("#inv-disp-block-loc option", { hasText: "LOC-BLK" })
      .first()
      .getAttribute("value");

    await page.locator("#inv-disp-release-loc").selectOption(relLocMainVal!);
    await page.locator("#inv-disp-block-loc").selectOption(blkLocVal!);
    await page.locator("#inv-disp-release-qty").fill("12");
    await page.locator("#inv-disp-block-qty").fill("3");
    await page.locator("#inv-disp-reason").selectOption("DAMAGED_OR_CONTAMINATED");
    await page.locator("#inv-disp-submit").click();

    await expect(page.locator("#inv-op-success")).toBeVisible();
    await expect(page.getByTestId("inv-metric-unrest")).toContainText("12 PCS");
    await expect(page.getByTestId("inv-metric-qi")).toContainText("5 PCS");
    await expect(page.getByTestId("inv-metric-blk")).toContainText("3 PCS");

    // Complete remaining 5 release
    await page.locator("#inv-disp-release-qty").fill("5");
    await page.locator("#inv-disp-block-qty").fill("0");
    await page.locator("#inv-disp-reason").selectOption("PASSED_SPECIFICATION");
    await page.locator("#inv-disp-submit").click();

    await expect(page.getByTestId("inv-metric-unrest")).toContainText("17 PCS");
    await expect(page.getByTestId("inv-metric-qi")).toContainText("0 PCS");
    await expect(page.getByTestId("inv-metric-avail")).toContainText("17 PCS");

    // Intra-warehouse transfer: 4 UNRESTRICTED from LOC-MAIN -> LOC-RACK-B2
    const unrMainBalVal = await page
      .locator("#inv-xfer-balance option", { hasText: "LOC-MAIN" })
      .first()
      .getAttribute("value");
    const rackB2Val = await page
      .locator("#inv-xfer-to-loc option", { hasText: "LOC-RACK-B2" })
      .first()
      .getAttribute("value");
    await page.locator("#inv-xfer-balance").selectOption(unrMainBalVal!);
    await page.locator("#inv-xfer-to-loc").selectOption(rackB2Val!);
    await page.locator("#inv-xfer-qty").fill("4");
    await page.locator("#inv-xfer-submit").click();

    await expect(page.locator("#inv-stock-detail-sheet")).toContainText("LOC-RACK-B2");

    // Over-reservation guard -> OD-10 error alert
    await page.locator("#inv-rsv-source-doc").fill(`SO-OVER-${stamp}`);
    await page.locator("#inv-rsv-qty").fill("25");
    await page.locator("#inv-rsv-submit").click();
    await expect(page.locator("#inv-op-error")).toContainText(/OD-10/);

    // Valid reservation of 6 from LOC-MAIN -> Available drops from 17 to 11
    const rsvMainBalVal = await page
      .locator("#inv-rsv-balance option", { hasText: "LOC-MAIN" })
      .first()
      .getAttribute("value");
    await page.locator("#inv-rsv-balance").selectOption(rsvMainBalVal!);
    await page.locator("#inv-rsv-source-doc").fill(`SO-S08-${stamp}`);
    await page.locator("#inv-rsv-qty").fill("6");
    await page.locator("#inv-rsv-submit").click();

    await expect(page.getByTestId("inv-metric-rsv")).toContainText("6 PCS");
    await expect(page.getByTestId("inv-metric-avail")).toContainText("11 PCS");

    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "03-inventory-stock-detail-operations-desktop.png"),
      fullPage: true,
    });

    // Mobile 390x844 capture on operational stock detail
    await page.setViewportSize({ width: 390, height: 844 });
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "05-inventory-stock-detail-mobile.png"),
      fullPage: true,
    });
    await page.setViewportSize({ width: 1280, height: 800 });

    // Partial release 2 -> Reserved 4, Avail 13; then full release -> Reserved 0, Avail 17
    await page.locator("#inv-rsv-release-qty-0").fill("2");
    await page.locator("#inv-rsv-release-0").click();
    await expect(page.getByTestId("inv-metric-rsv")).toContainText("4 PCS");
    await expect(page.getByTestId("inv-metric-avail")).toContainText("13 PCS");

    await page.locator("#inv-rsv-release-qty-0").fill("");
    await page.locator("#inv-rsv-release-0").click();
    await expect(page.getByTestId("inv-metric-rsv")).toContainText("0 PCS");
    await expect(page.getByTestId("inv-metric-avail")).toContainText("17 PCS");
    await expect(page.getByTestId("inv-reconciled-badge")).toContainText(/Terekonsiliasi|Reconciled/);
    await expect(page.locator("#inv-item-ledger-sheet")).toContainText("7.000.000");

    // Inspect State & Movement Timeline in English
    await page.locator("#inv-detail-tab-timeline").click();
    await expect(page.locator("#inv-stock-detail-sheet")).toContainText(lotNum);
    await page.getByRole("button", { name: "English", exact: true }).click();
    await expect(page.getByTestId("inv-reconciled-badge")).toContainText("Reconciled");
    await expect(page.locator("#inv-op-success")).toContainText("Released");
    await expect(page.locator("#inv-stock-detail-sheet")).toContainText("-15 PCS");
    await expect(page.locator("#inv-stock-detail-sheet")).toContainText("-5 PCS");
    await expect(page.locator("#inv-stock-detail-sheet")).toContainText("-4 PCS");
    await expect(page.locator("#inv-stock-detail-sheet")).toContainText("+20 PCS");
    await assertNoLayoutClipping(page);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: path.join(evidenceDir, "04-inventory-timeline-en-desktop.png"),
      fullPage: true,
    });
  });

  test("S08-E2E-02 NUSA-SBY empty-state isolation and U1 hostile cross-tenant SMDR inventory denial", async ({
    page,
  }) => {
    await signInAs(page, "budi.santoso@demo.athera.local");
    await page.goto(`/w/${TENANT_NUSA_ID}/${ENTITY_NUSA_SBY_ID}/inventory`);
    await expect(page.locator("main")).not.toContainText("ITM-CHEM-LOT-01");
    await expect(page.locator("main")).toContainText(/Belum Ada Saldo Persediaan Bernilai|No Valued Inventory Balances Yet/);

    await signOut(page);
    await signInAs(page, "rina.utami@demo.athera.local");
    await page.goto(`/w/${TENANT_SMDR_ID}/${ENTITY_SMDR_BDG_ID}/inventory`);
    await expect(page.locator("main").getByRole("alert")).toContainText(/Ruang Kerja Tidak Tersedia|Workspace Scope Unavailable/);
    const bodyText = await page.locator("main").innerText();
    expect(bodyText).not.toContain("SMDR");
    expect(bodyText).not.toContain("Samudra");
  });
});
