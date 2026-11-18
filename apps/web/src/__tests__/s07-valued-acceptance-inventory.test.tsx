import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ReceivingPage from "@/app/(shell)/w/[tenantId]/[entityId]/receiving/page";
import InventoryPage from "@/app/(shell)/w/[tenantId]/[entityId]/inventory/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  AcceptancePreviewDto,
  EligibleReceivingPosResponse,
  EntityWorkspaceDetailResponse,
  GoodsReceiptDetailDto,
  GoodsReceiptListResponse,
  InventoryOverviewResponse,
  ItemValuationLedgerResponse,
  ValuedAcceptanceDetailDto,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const perms = [
  "workspace.view",
  "receiving.read",
  "receiving.manage",
  "receiving.accept",
  "inventory.read",
];

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };

const workspacePayload: EntityWorkspaceDetailResponse = {
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
    permissions: perms,
  },
  effectivePermissions: perms,
  navigationCapabilities: [],
  serverTimestampUtc: "2026-11-18T00:00:00Z",
};

const eligiblePosPayload: EligibleReceivingPosResponse = {
  items: [],
  od01Notice: "OD-01 active.",
};

const receiptsListPayload: GoodsReceiptListResponse = {
  items: [
    {
      goodsReceiptId: "grn-s07",
      receiptNumber: "GRN-2026-00002",
      purchaseOrderId: "po-2",
      poNumberSnapshot: "PO-2026-00003",
      poRevisionNumberSnapshot: 1,
      supplierCodeSnapshot: "PT-SUP-001",
      supplierLegalNameSnapshot: "PT Sumber Makmur Kemasan",
      supplierDeliveryNoteNumber: "SJ-JKT-DEMO-S07-02",
      receivedDate: "2026-11-06",
      receivingStation: "Dermaga Utama",
      status: "RELEASED_FOR_ACCEPTANCE",
      totalDeliveredBaseQuantity: 10,
      totalRejectedBaseQuantity: 0,
      totalValuedAcceptedBaseQuantity: 0,
      totalUnresolvedCustodyBaseQuantity: 10,
      totalReleasedBaseQuantity: 10,
      totalHeldBaseQuantity: 0,
      hasFinancialPosting: false,
      updatedAtUtc: "2026-11-06T03:00:00Z",
    },
  ],
  totalReceipts: 1,
  sumDeliveredBaseQuantity: 10,
  sumHeldBaseQuantity: 0,
  sumReleasedBaseQuantity: 10,
  sumRejectedBaseQuantity: 0,
  totalLinkedGlJournals: 0,
};

const detailPayload: GoodsReceiptDetailDto = {
  goodsReceiptId: "grn-s07",
  receiptNumber: "GRN-2026-00002",
  purchaseOrderId: "po-2",
  poRevisionId: "rev-2",
  poNumberSnapshot: "PO-2026-00003",
  poRevisionNumberSnapshot: 1,
  poSnapshotHash: "abcdef0123456789fedcba",
  supplierPartnerId: "sup-1",
  supplierCodeSnapshot: "PT-SUP-001",
  supplierLegalNameSnapshot: "PT Sumber Makmur Kemasan",
  supplierDeliveryNoteNumber: "SJ-JKT-DEMO-S07-02",
  idempotencyKey: "arr-2",
  arrivalPayloadHash: "hash-arr-2",
  receivedDate: "2026-11-06",
  receivingStation: "Dermaga Utama",
  vehicleOrSealRef: null,
  documentEvidenceRef: null,
  status: "RELEASED_FOR_ACCEPTANCE",
  totalDeliveredBaseQuantity: 10,
  totalRejectedBaseQuantity: 0,
  totalValuedAcceptedBaseQuantity: 0,
  totalUnresolvedCustodyBaseQuantity: 10,
  totalReleasedBaseQuantity: 10,
  totalHeldBaseQuantity: 0,
  hasFinancialPosting: false,
  linkedGlJournalCount: 0,
  notes: null,
  receivedByUserId: "u-1",
  receivedByEmail: "rina.utami@demo.athera.local",
  createdAtUtc: "2026-11-06T03:00:00Z",
  updatedAtUtc: "2026-11-06T03:00:00Z",
  version: 1,
  wasIdempotentReplay: false,
  valuedAcceptanceJournalCount: 0,
  valuedAcceptances: [],
  lines: [
    {
      goodsReceiptLineId: "grl-s07",
      lineNumber: 1,
      purchaseOrderId: "po-2",
      poRevisionId: "rev-2",
      poLineId: "pol-2",
      lineageLineKey: "L001",
      itemId: "itm-steel",
      itemCodeSnapshot: "ITM-STL-PLATE-10",
      itemNameSnapshot: "Pelat Baja 10mm Grade SS400",
      itemKindSnapshot: "STOCKED_GOOD",
      receivedUomId: "uom-pcs",
      receivedUomCodeSnapshot: "PCS",
      baseUomId: "uom-pcs",
      baseUomCodeSnapshot: "PCS",
      uomFactorToBaseSnapshot: 1,
      poOrderedBaseQuantitySnapshot: 10,
      physicallyDeliveredQuantity: 10,
      physicallyDeliveredBaseQuantity: 10,
      pendingCheckBaseQuantity: 0,
      dockHeldBaseQuantity: 0,
      dockReleasedBaseQuantity: 10,
      dockRejectedBaseQuantity: 0,
      valuedAcceptedBaseQuantity: 0,
      unresolvedCustodyBaseQuantity: 10,
      overdeliveryExcessBaseQuantity: 0,
      custodyStatus: "DOCK_RELEASED",
      discrepancyReasonCode: "NONE",
      discrepancyNotes: null,
      conditionEvidenceRef: null,
      maxApprovedReleaseCapBaseQty: 10,
      updatedAtUtc: "2026-11-06T03:00:00Z",
    },
  ],
  dockChecks: [],
};

const previewMock = vi.fn(
  async (
    _tid: string,
    _eid: string,
    _rid: string,
    payload: { acceptanceDate: string; stockStatus: string },
  ): Promise<AcceptancePreviewDto> => {
    const blocked = payload.acceptanceDate.startsWith("2026-10");
    return {
      goodsReceiptId: "grn-s07",
      receiptNumber: "GRN-2026-00002",
      currencyCode: "IDR",
      acceptanceDate: payload.acceptanceDate,
      stockStatus: payload.stockStatus,
      canPost: !blocked,
      totalAcceptedBaseQuantity: 10,
      totalAcceptedAmount: 9_500_000,
      od04Notice: "OD-04 active",
      lines: [
        {
          goodsReceiptLineId: "grl-s07",
          itemId: "itm-steel",
          itemCode: "ITM-STL-PLATE-10",
          itemName: "Pelat Baja 10mm Grade SS400",
          baseUomCode: "PCS",
          acceptedBaseQuantity: 10,
          unitCostBase: 950_000,
          acceptedLineAmount: 9_500_000,
          previousPoolBaseQuantity: 10,
          previousCarryingValue: 8_500_000,
          previousMovingAverageCost: 850_000,
          projectedPoolBaseQuantity: 20,
          projectedCarryingValue: 18_000_000,
          projectedMovingAverageCost: 900_000,
          blockingReason: null,
          lotTrackingPolicy: "NONE",
          lotNumber: null,
          destinationLocationId: null,
          destinationLocationCode: null,
        },
      ],
      journalPreview: [
        {
          side: "DEBIT",
          accountCode: "1300",
          accountName: "Persediaan Barang Dagang",
          amount: 9_500_000,
          description: "Inventory",
        },
        {
          side: "CREDIT",
          accountCode: "2110",
          accountName: "Akrual GRNI",
          amount: 9_500_000,
          description: "GRNI",
        },
      ],
      blockingReasons: blocked ? ["period_closed: Fiscal period 2026-10 is CLOSED."] : [],
    };
  },
);

const acceptMock = vi.fn(
  async (): Promise<ValuedAcceptanceDetailDto> => ({
    valuedAcceptanceId: "va-1",
    acceptanceNumber: "ACC-2026-00002",
    goodsReceiptId: "grn-s07",
    receiptNumber: "GRN-2026-00002",
    receiptStatus: "VALUED_ACCEPTED",
    purchaseOrderId: "po-2",
    poNumber: "PO-2026-00003",
    poRevisionNumber: 1,
    supplierPartnerId: "sup-1",
    supplierCode: "PT-SUP-001",
    supplierName: "PT Sumber Makmur Kemasan",
    acceptanceDate: "2026-11-10",
    stockStatus: "UNRESTRICTED",
    currencyCode: "IDR",
    totalAcceptedBaseQuantity: 10,
    totalAcceptedAmount: 9_500_000,
    stockMovementId: "mov-2",
    movementNumber: "MOV-2026-00002",
    receiptAccrualId: "accrual-2",
    accrualNumber: "GRNI-2026-00002",
    glJournalId: "jrn-2",
    glJournalNumber: "JRN-2026-11-00003",
    notes: null,
    acceptedByUserId: "u-2",
    acceptedByEmail: "budi.santoso@demo.athera.local",
    createdAtUtc: "2026-11-10T05:00:00Z",
    wasIdempotentReplay: false,
    lines: [],
  }),
);

const overviewPayload: InventoryOverviewResponse = {
  positions: [
    {
      stockPositionId: "sp-1",
      itemId: "itm-steel",
      itemCode: "ITM-STL-PLATE-10",
      itemName: "Pelat Baja 10mm Grade SS400",
      lotTrackingPolicy: "NONE",
      baseUomId: "uom-pcs",
      baseUomCode: "PCS",
      valuationGroupCode: "DEFAULT",
      currencyCode: "IDR",
      unrestrictedBaseQuantity: 16,
      qualityInspectionBaseQuantity: 4,
      blockedBaseQuantity: 0,
      reservedBaseQuantity: 0,
      totalValuedOnHandBaseQuantity: 20,
      availableBaseQuantity: 16,
      carryingValueAmount: 18_000_000,
      movingAverageUnitCost: 900_000,
      poolStatus: "ACTIVE",
      lastEntrySequence: 2,
      locationCount: 2,
      activeLotCount: 0,
      pendingInspectionCount: 0,
      updatedAtUtc: "2026-11-10T05:00:00Z",
    },
  ],
  recentMovements: [
    {
      stockMovementId: "mov-2",
      movementNumber: "MOV-2026-00002",
      movementType: "PURCHASE_RECEIPT_ACCEPTANCE",
      postingDate: "2026-11-10",
      sourceDocumentNumber: "ACC-2026-00002",
      lineCount: 1,
      totalBaseQuantity: 10,
      totalValuationAmount: 9_500_000,
      createdByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-10T05:00:00Z",
    },
  ],
  openAccruals: [
    {
      receiptAccrualId: "accrual-2",
      accrualNumber: "GRNI-2026-00002",
      valuedAcceptanceId: "va-2",
      acceptanceNumber: "ACC-2026-00002",
      goodsReceiptId: "grn-s07",
      receiptNumber: "GRN-2026-00002",
      purchaseOrderId: "po-2",
      poNumber: "PO-2026-00003",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierName: "PT Sumber Makmur Kemasan",
      journalId: "jrn-2",
      journalNumber: "JRN-2026-11-00003",
      postingDate: "2026-11-10",
      currencyCode: "IDR",
      accruedAmount: 9_500_000,
      clearedAmount: 0,
      remainingOpenAmount: 9_500_000,
      status: "OPEN",
      createdAtUtc: "2026-11-10T05:00:00Z",
    },
  ],
  totalStockedItemsWithBalance: 1,
  totalCarryingValueIdr: 18_000_000,
  totalOpenGrniAccrualIdr: 9_500_000,
  restrictedStockItemCount: 1,
  locations: [],
  inspections: [],
  od04Notice: "OD-04 active.",
  od10Notice: "OD-10 active.",
};

const ledgerPayload: ItemValuationLedgerResponse = {
  itemId: "itm-steel",
  itemCode: "ITM-STL-PLATE-10",
  itemName: "Pelat Baja 10mm Grade SS400",
  baseUomCode: "PCS",
  currencyCode: "IDR",
  valuationGroupCode: "DEFAULT",
  poolStatus: "ACTIVE",
  currentValuedBaseQuantity: 20,
  currentCarryingValueAmount: 18_000_000,
  currentMovingAverageUnitCost: 900_000,
  unrestrictedBaseQuantity: 16,
  qualityInspectionBaseQuantity: 4,
  blockedBaseQuantity: 0,
  availableBaseQuantity: 16,
  entries: [
    {
      valuationEntryId: "ve-1",
      poolSequenceNumber: 1,
      postingDate: "2026-11-05",
      entryType: "RECEIPT_ACCEPTANCE",
      sourceDocumentType: "VALUED_ACCEPTANCE",
      sourceDocumentId: "va-1",
      sourceDocumentNumber: "ACC-2026-00001",
      movementNumber: "MOV-2026-00001",
      glJournalId: "jrn-1",
      journalNumber: "JRN-2026-11-00002",
      quantityDelta: 10,
      unitCostApplied: 850_000,
      valueDelta: 8_500_000,
      previousPoolBaseQuantity: 0,
      previousPoolCarryingValue: 0,
      previousMovingAverageCost: 0,
      newPoolBaseQuantity: 10,
      newPoolCarryingValue: 8_500_000,
      newMovingAverageCost: 850_000,
      createdByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-05T05:00:00Z",
    },
    {
      valuationEntryId: "ve-2",
      poolSequenceNumber: 2,
      postingDate: "2026-11-10",
      entryType: "RECEIPT_ACCEPTANCE",
      sourceDocumentType: "VALUED_ACCEPTANCE",
      sourceDocumentId: "va-2",
      sourceDocumentNumber: "ACC-2026-00002",
      movementNumber: "MOV-2026-00002",
      glJournalId: "jrn-2",
      journalNumber: "JRN-2026-11-00003",
      quantityDelta: 10,
      unitCostApplied: 950_000,
      valueDelta: 9_500_000,
      previousPoolBaseQuantity: 10,
      previousPoolCarryingValue: 8_500_000,
      previousMovingAverageCost: 850_000,
      newPoolBaseQuantity: 20,
      newPoolCarryingValue: 18_000_000,
      newMovingAverageCost: 900_000,
      createdByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-10T05:00:00Z",
    },
  ],
};

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/lib/api/client", () => ({
  fetchEntityWorkspaceDetail: async () => workspacePayload,
  fetchEligibleReceivingPos: async () => eligiblePosPayload,
  fetchGoodsReceipts: async () => receiptsListPayload,
  fetchGoodsReceiptDetail: async () => detailPayload,
  recordGoodsReceiptArrival: vi.fn(),
  recordGoodsReceiptDockCheck: vi.fn(),
  previewReceiptAcceptance: (...args: unknown[]) =>
    previewMock(args[0] as string, args[1] as string, args[2] as string, args[3] as { acceptanceDate: string; stockStatus: string }),
  acceptGoodsReceiptValued: () => acceptMock(),
  fetchInventoryOverview: async () => overviewPayload,
  fetchItemValuationLedger: async () => ledgerPayload,
  listInventoryWarehouses: async () => ({ warehouses: [], locations: [] }),
  getItemStockDetail: async () => null,
}));

describe("S07 Gate 2 Valued Acceptance & Inventory Valuation UI", () => {
  it("enforces Gate 2 preview-before-confirm, closed-period blocking, and invalidation on edit", async () => {
    render(
      <LocaleProvider>
        <ReceivingPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      const panel = document.getElementById("gate2-acceptance-panel");
      expect(panel).toBeInTheDocument();
      expect(panel).toHaveTextContent(/ITM-STL-PLATE-10\s*—\s*Pelat Baja 10mm Grade SS400/);
    });

    const confirmBtn = document.getElementById("gate2-confirm-btn") as HTMLButtonElement;
    expect(confirmBtn).toBeDisabled();

    // Closed period preview keeps confirm button disabled & surfaces period_closed block
    fireEvent.change(document.getElementById("gate2-acceptance-date")!, {
      target: { value: "2026-10-15" },
    });
    fireEvent.click(document.getElementById("gate2-preview-btn")!);

    await waitFor(() => {
      expect(document.getElementById("gate2-preview-blocked")).toBeInTheDocument();
    });
    expect(confirmBtn).toBeDisabled();

    // Valid open period unlocks confirm button and displays moving-average recalculation
    fireEvent.change(document.getElementById("gate2-acceptance-date")!, {
      target: { value: "2026-11-10" },
    });
    fireEvent.click(document.getElementById("gate2-preview-btn")!);

    await waitFor(() => {
      expect(document.getElementById("gate2-preview-card")).toBeInTheDocument();
      expect(confirmBtn).not.toBeDisabled();
    });

    // Editing quantity invalidates preview and disables confirm until recalculated
    fireEvent.change(document.getElementById("gate2-qty-0")!, { target: { value: "5" } });
    expect(confirmBtn).toBeDisabled();

    fireEvent.change(document.getElementById("gate2-qty-0")!, { target: { value: "10" } });
    fireEvent.click(document.getElementById("gate2-preview-btn")!);
    await waitFor(() => expect(confirmBtn).not.toBeDisabled());

    fireEvent.click(confirmBtn);
    await waitFor(() => {
      expect(document.getElementById("gate2-success-banner")).toHaveTextContent("ACC-2026-00002");
      expect(document.getElementById("gate2-success-banner")).toHaveTextContent("GRNI-2026-00002");
    });
  });

  it("renders perpetual inventory positions, sequential moving-average ledger, GRNI accruals and movements", async () => {
    render(
      <LocaleProvider>
        <InventoryPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("inv-kpi-carrying")).toHaveTextContent("18.000.000");
      expect(screen.getByTestId("inv-kpi-grni")).toHaveTextContent("9.500.000");
    });

    expect(document.getElementById("inv-item-ledger-sheet")).toHaveTextContent("ACC-2026-00001");
    expect(document.getElementById("inv-item-ledger-sheet")).toHaveTextContent("ACC-2026-00002");

    fireEvent.click(document.getElementById("inv-tab-accruals")!);
    expect(screen.getByText("GRNI-2026-00002")).toBeInTheDocument();

    fireEvent.click(document.getElementById("inv-tab-movements")!);
    expect(screen.getByText("MOV-2026-00002")).toBeInTheDocument();
  });
});
