import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ReceivingPage from "@/app/(shell)/w/[tenantId]/[entityId]/receiving/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import {
  convertArrivalQtyToBase,
  evaluateDockCheckPreview,
  formatQuantityValue,
  parseScaledBigInt,
} from "@/lib/receivingMath";
import type {
  EligibleReceivingPosResponse,
  EntityWorkspaceDetailResponse,
  GoodsReceiptDetailDto,
  GoodsReceiptListResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

let currentPermissions = ["workspace.view", "receiving.read", "receiving.manage"];

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };

const fetchDetailMock = vi.fn(async (): Promise<EntityWorkspaceDetailResponse> => ({
  tenant: { tenantId: TENANT_ID, code: "NUSA", name: "PT Nusantara Niaga Utama", status: "ACTIVE" },
  entity: {
    legalEntityId: ENTITY_ID,
    tenantId: TENANT_ID,
    code: "NUSA-JKT",
    name: "PT Nusantara Distribusi Jakarta",
    functionalCurrencyCode: "IDR",
    countryCode: "ID",
    timeZoneId: "Asia/Jakarta",
    taxRegistrationNpwp: "01.234.567.8-011.000",
    status: "ACTIVE",
    permissions: currentPermissions,
  },
  effectivePermissions: currentPermissions,
  navigationCapabilities: [],
  serverTimestampUtc: "2026-11-18T00:00:00Z",
}));

const eligiblePosPayload: EligibleReceivingPosResponse = {
  items: [
    {
      purchaseOrderId: "po-1",
      poNumber: "PO-2026-00001",
      approvedRevisionId: "rev-1",
      approvedRevisionNumber: 1,
      snapshotHash: "abcdef0123456789",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierName: "PT Sumber Makmur Kemasan",
      orderDate: "2026-11-18",
      expectedDeliveryDate: "2026-11-25",
      deliveryDestination: "Gudang Pusat Jakarta",
      lines: [
        {
          poLineId: "pol-1",
          lineNumber: 1,
          lineageLineKey: "L001",
          itemId: "itm-1",
          itemCode: "ITM-PKG-001",
          itemName: "Karton Lipat Polos 40x30x25 cm",
          itemKind: "STOCKED_GOOD",
          orderedUomId: "uom-box",
          orderedUomCode: "BOX",
          baseUomId: "uom-pcs",
          baseUomCode: "PCS",
          approvedOrderedBaseQuantity: 48,
          deliveredBaseQuantity: 24,
          pendingCheckBaseQuantity: 24,
          heldBaseQuantity: 0,
          releasedBaseQuantity: 0,
          rejectedBaseQuantity: 0,
          valuedAcceptedBaseQuantity: 0,
          remainingEligibleBaseQuantity: 24,
          allowedUoms: [
            { uomId: "uom-pcs", uomCode: "PCS", name: "Pieces", factorToBase: 1, precisionScale: 0 },
            { uomId: "uom-box", uomCode: "BOX", name: "Box Karton", factorToBase: 24, precisionScale: 0 },
          ],
        },
      ],
    },
  ],
  od01Notice: "OD-01 strict zero overdelivery lock active.",
};

const receiptsListPayload: GoodsReceiptListResponse = {
  items: [
    {
      goodsReceiptId: "grn-1",
      receiptNumber: "GRN-2026-00001",
      purchaseOrderId: "po-1",
      poNumberSnapshot: "PO-2026-00001",
      poRevisionNumberSnapshot: 1,
      supplierCodeSnapshot: "PT-SUP-001",
      supplierLegalNameSnapshot: "PT Sumber Makmur Kemasan",
      supplierDeliveryNoteNumber: "SJ-JKT-001",
      receivedDate: "2026-11-18",
      receivingStation: "Dermaga Utama",
      status: "IN_DOCK_INSPECTION",
      totalDeliveredBaseQuantity: 60,
      totalRejectedBaseQuantity: 0,
      totalValuedAcceptedBaseQuantity: 0,
      totalUnresolvedCustodyBaseQuantity: 60,
      totalReleasedBaseQuantity: 0,
      totalHeldBaseQuantity: 12,
      hasFinancialPosting: false,
      updatedAtUtc: "2026-11-18T03:00:00Z",
    },
  ],
  totalReceipts: 1,
  sumDeliveredBaseQuantity: 60,
  sumHeldBaseQuantity: 12,
  sumReleasedBaseQuantity: 0,
  sumRejectedBaseQuantity: 0,
  totalLinkedGlJournals: 0,
};

const receiptDetailPayload: GoodsReceiptDetailDto = {
  goodsReceiptId: "grn-1",
  receiptNumber: "GRN-2026-00001",
  purchaseOrderId: "po-1",
  poRevisionId: "rev-1",
  poNumberSnapshot: "PO-2026-00001",
  poRevisionNumberSnapshot: 1,
  poSnapshotHash: "abcdef0123456789fedcba",
  supplierPartnerId: "sup-1",
  supplierCodeSnapshot: "PT-SUP-001",
  supplierLegalNameSnapshot: "PT Sumber Makmur Kemasan",
  supplierDeliveryNoteNumber: "SJ-JKT-001",
  idempotencyKey: "arr-1",
  arrivalPayloadHash: "hash-arr-1",
  receivedDate: "2026-11-18",
  receivingStation: "Dermaga Utama",
  vehicleOrSealRef: "B 9012 AA",
  documentEvidenceRef: "SCAN-SJ-001.pdf",
  status: "IN_DOCK_INSPECTION",
  totalDeliveredBaseQuantity: 60,
  totalRejectedBaseQuantity: 0,
  totalValuedAcceptedBaseQuantity: 0,
  totalUnresolvedCustodyBaseQuantity: 60,
  totalReleasedBaseQuantity: 0,
  totalHeldBaseQuantity: 12,
  hasFinancialPosting: false,
  linkedGlJournalCount: 0,
  notes: "Overdelivery batch 1",
  receivedByUserId: "u-1",
  receivedByEmail: "rina.utami@demo.athera.local",
  createdAtUtc: "2026-11-18T03:00:00Z",
  updatedAtUtc: "2026-11-18T03:00:00Z",
  version: 1,
  wasIdempotentReplay: false,
  valuedAcceptanceJournalCount: 0,
  valuedAcceptances: [],
  lines: [
    {
      goodsReceiptLineId: "grl-1",
      lineNumber: 1,
      purchaseOrderId: "po-1",
      poRevisionId: "rev-1",
      poLineId: "pol-1",
      lineageLineKey: "L001",
      itemId: "itm-1",
      itemCodeSnapshot: "ITM-PKG-001",
      itemNameSnapshot: "Karton Lipat Polos 40x30x25 cm",
      itemKindSnapshot: "STOCKED_GOOD",
      receivedUomId: "uom-pcs",
      receivedUomCodeSnapshot: "PCS",
      baseUomId: "uom-pcs",
      baseUomCodeSnapshot: "PCS",
      uomFactorToBaseSnapshot: 1,
      poOrderedBaseQuantitySnapshot: 48,
      physicallyDeliveredQuantity: 60,
      physicallyDeliveredBaseQuantity: 60,
      pendingCheckBaseQuantity: 48,
      dockHeldBaseQuantity: 12,
      dockReleasedBaseQuantity: 0,
      dockRejectedBaseQuantity: 0,
      valuedAcceptedBaseQuantity: 0,
      unresolvedCustodyBaseQuantity: 60,
      overdeliveryExcessBaseQuantity: 12,
      custodyStatus: "DOCK_HELD",
      discrepancyReasonCode: "OVERDELIVERY_OD01_HOLD",
      discrepancyNotes: "12 PCS melebihi PO",
      conditionEvidenceRef: null,
      maxApprovedReleaseCapBaseQty: 48,
      updatedAtUtc: "2026-11-18T03:00:00Z",
    },
  ],
  dockChecks: [],
};

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/lib/api/client", () => ({
  fetchEntityWorkspaceDetail: () => fetchDetailMock(),
  fetchEligibleReceivingPos: async () => eligiblePosPayload,
  fetchGoodsReceipts: async () => receiptsListPayload,
  fetchGoodsReceiptDetail: async () => receiptDetailPayload,
  recordGoodsReceiptArrival: vi.fn(),
  recordGoodsReceiptDockCheck: vi.fn(),
  previewReceiptAcceptance: vi.fn(),
  acceptGoodsReceiptValued: vi.fn(),
  listInventoryWarehouses: async () => ({ warehouses: [], locations: [] }),
}));

describe("S06 Dock Custody & Receiving Gate 1 Math + UI", () => {
  it("computes exact BigInt base conversions, conservation checks, and OD-01 caps", () => {
    expect(parseScaledBigInt("24.5")).toBe(24_500_000n);
    expect(parseScaledBigInt(1e-6)).toBe(1n);
    expect(formatQuantityValue(1e-6, "en-US")).toBe("0.000001");
    expect(parseScaledBigInt("-1")).toBeNull();
    expect(formatQuantityValue(48, "id-ID")).toBe("48");

    const conv = convertArrivalQtyToBase({
      qtyRaw: "2",
      factorToBase: 24,
      remainingEligibleBase: 24,
    });
    expect(conv.validFormat).toBe(true);
    expect(conv.baseQty).toBe(48_000_000n);
    expect(conv.exceedsRemainingBase).toBe(24_000_000n);

    const balanced = evaluateDockCheckPreview({
      deliveredBase: 24,
      valuedAcceptedBase: 0,
      maxReleaseCapBase: 24,
      pendingRaw: "0",
      heldRaw: "4",
      releasedRaw: "16",
      rejectedRaw: "4",
    });
    expect(balanced.balanced).toBe(true);
    expect(balanced.exceedsOd01Cap).toBe(false);

    const overCap = evaluateDockCheckPreview({
      deliveredBase: 60,
      valuedAcceptedBase: 0,
      maxReleaseCapBase: 48,
      pendingRaw: "0",
      heldRaw: "0",
      releasedRaw: "60",
      rejectedRaw: "0",
    });
    expect(overCap.balanced).toBe(true);
    expect(overCap.exceedsOd01Cap).toBe(true);
  });

  it("enforces Gate 1 zero-GL visibility, preset split within OD-01 cap, and live arrival excess warning", async () => {
    currentPermissions = ["workspace.view", "receiving.read", "receiving.manage"];

    render(
      <LocaleProvider>
        <ReceivingPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getAllByText("GRN-2026-00001").length).toBeGreaterThanOrEqual(1);
      expect(screen.getByTestId("dock-check-card-0")).toBeInTheDocument();
    });

    expect(screen.getByTestId("gate1-gl-count-card")).toHaveTextContent("0");
    expect(screen.getByTestId("dock-check-card-0")).toHaveTextContent("OD-01 +12 PCS");

    // Apply "Release Within PO Cap" preset -> splits 60 into 48 released + 12 held
    fireEvent.click(screen.getByRole("button", { name: /Lepas Sesuai Batas PO/ }));
    expect(screen.getByTestId("dock-conservation-0")).toHaveTextContent("Seimbang dengan Fisik Tiba");

    // Exceed PO cap manually -> triggers live OD-01 warning tag
    const releasedInput = document.getElementById("dock-released-0") as HTMLInputElement;
    const heldInput = document.getElementById("dock-held-0") as HTMLInputElement;
    fireEvent.change(releasedInput, { target: { value: "55" } });
    fireEvent.change(heldInput, { target: { value: "5" } });
    expect(screen.getByTestId("dock-conservation-0")).toHaveTextContent(/Melebihi batas PO/);

    // Switch to Arrival tab and test OD-01 excess preview when entering 2 BOX against 24 PCS remaining
    fireEvent.click(screen.getByRole("tab", { name: /Catat Kedatangan/ }));
    const qtyInput = document.getElementById("rcv-line-qty-0") as HTMLInputElement;
    fireEvent.change(qtyInput, { target: { value: "2" } });
    expect(screen.getByTestId("arrival-od01-warning-0")).toHaveTextContent("24 PCS");
  });

  it("hides tablist and all dock mutation controls when user only holds receiving.read", async () => {
    currentPermissions = ["workspace.view", "receiving.read"];

    render(
      <LocaleProvider>
        <ReceivingPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("receiving-readonly-banner")).toBeInTheDocument();
      expect(screen.getAllByText("GRN-2026-00001").length).toBeGreaterThanOrEqual(1);
    });

    expect(screen.queryByRole("tablist")).toBeNull();
    expect(document.getElementById("dock-submit-0")).toBeNull();
  });
});
