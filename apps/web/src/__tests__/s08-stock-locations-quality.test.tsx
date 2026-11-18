import React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import InventoryPage from "@/app/(shell)/w/[tenantId]/[entityId]/inventory/page";
import CatalogPage from "@/app/(shell)/w/[tenantId]/[entityId]/catalog/page";
import ReceivingPage from "@/app/(shell)/w/[tenantId]/[entityId]/receiving/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import { formatSignedQuantityValue } from "@/lib/receivingMath";
import type {
  EntityWorkspaceDetailResponse,
  GoodsReceiptDetailDto,
  GoodsReceiptListResponse,
  InventoryOverviewResponse,
  ItemStockDetailResponse,
  ItemValuationLedgerResponse,
  PagedItemsResponse,
  UomListResponse,
  WarehouseListResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };

let activePermissions = [
  "workspace.view",
  "inventory.read",
  "inventory.manage",
  "quality.inspect",
  "receiving.read",
  "receiving.manage",
  "receiving.accept",
  "catalog.read",
  "catalog.manage",
];

const recordDispositionMock = vi.fn();
const createReservationMock = vi.fn();

const warehousesPayload: WarehouseListResponse = {
  warehouses: [
    {
      warehouseId: "wh-main",
      warehouseCode: "WH-MAIN",
      name: "Gudang Distribusi Utama",
      isDefault: true,
      status: "ACTIVE",
      createdAtUtc: "2026-11-01T00:00:00Z",
    },
  ],
  locations: [
    {
      locationId: "loc-main",
      warehouseId: "wh-main",
      warehouseCode: "WH-MAIN",
      warehouseName: "Gudang Distribusi Utama",
      locationCode: "LOC-MAIN",
      name: "Rak Penyimpanan Utama",
      zoneType: "STORAGE",
      isDefaultReceiving: true,
      status: "ACTIVE",
      createdAtUtc: "2026-11-01T00:00:00Z",
    },
    {
      locationId: "loc-rack-b2",
      warehouseId: "wh-main",
      warehouseCode: "WH-MAIN",
      warehouseName: "Gudang Distribusi Utama",
      locationCode: "LOC-RACK-B2",
      name: "Rak Sekunder B2",
      zoneType: "STORAGE",
      isDefaultReceiving: false,
      status: "ACTIVE",
      createdAtUtc: "2026-11-01T00:00:00Z",
    },
    {
      locationId: "loc-qc",
      warehouseId: "wh-main",
      warehouseCode: "WH-MAIN",
      warehouseName: "Gudang Distribusi Utama",
      locationCode: "LOC-QC",
      name: "Zona Karantina Mutu",
      zoneType: "QUARANTINE",
      isDefaultReceiving: false,
      status: "ACTIVE",
      createdAtUtc: "2026-11-01T00:00:00Z",
    },
    {
      locationId: "loc-blk",
      warehouseId: "wh-main",
      warehouseCode: "WH-MAIN",
      warehouseName: "Gudang Distribusi Utama",
      locationCode: "LOC-BLK",
      name: "Area Barang Diblokir",
      zoneType: "BLOCKED_HOLD",
      isDefaultReceiving: false,
      status: "ACTIVE",
      createdAtUtc: "2026-11-01T00:00:00Z",
    },
  ],
};

const overviewPayload: InventoryOverviewResponse = {
  positions: [
    {
      stockPositionId: "sp-chem",
      itemId: "itm-chem",
      itemCode: "ITM-CHEM-LOT-01",
      itemName: "Pelarut Industri Grade A",
      lotTrackingPolicy: "REQUIRED",
      baseUomId: "uom-pcs",
      baseUomCode: "PCS",
      valuationGroupCode: "DEFAULT",
      currencyCode: "IDR",
      unrestrictedBaseQuantity: 12,
      qualityInspectionBaseQuantity: 8,
      blockedBaseQuantity: 0,
      reservedBaseQuantity: 0,
      totalValuedOnHandBaseQuantity: 20,
      availableBaseQuantity: 12,
      carryingValueAmount: 6_400_000,
      movingAverageUnitCost: 320_000,
      poolStatus: "ACTIVE",
      lastEntrySequence: 1,
      locationCount: 2,
      activeLotCount: 1,
      pendingInspectionCount: 1,
      updatedAtUtc: "2026-11-07T03:00:00Z",
    },
  ],
  recentMovements: [],
  openAccruals: [],
  locations: warehousesPayload.locations,
  inspections: [
    {
      receiptInspectionId: "qci-1",
      inspectionNumber: "QCI-2026-00001",
      valuedAcceptanceId: "va-1",
      acceptanceNumber: "ACC-2026-00002",
      goodsReceiptId: "grn-s08",
      receiptNumber: "GRN-2026-00003",
      itemId: "itm-chem",
      itemCode: "ITM-CHEM-LOT-01",
      itemName: "Pelarut Industri Grade A",
      baseUomCode: "PCS",
      warehouseId: "wh-main",
      quarantineLocationId: "loc-qc",
      quarantineLocationCode: "LOC-QC",
      stockLotId: "lot-1",
      lotNumber: "LOT-JKT-2611-01",
      inspectedTotalBaseQuantity: 20,
      pendingBaseQuantity: 8,
      releasedBaseQuantity: 12,
      blockedBaseQuantity: 0,
      status: "PARTIALLY_DECIDED",
      createdAtUtc: "2026-11-07T03:00:00Z",
      updatedAtUtc: "2026-11-08T03:00:00Z",
      dispositions: [],
    },
  ],
  totalStockedItemsWithBalance: 1,
  totalCarryingValueIdr: 6_400_000,
  totalOpenGrniAccrualIdr: 6_400_000,
  restrictedStockItemCount: 1,
  od04Notice: "OD-04 active.",
  od10Notice: "OD-10 active.",
};

const ledgerPayload: ItemValuationLedgerResponse = {
  itemId: "itm-chem",
  itemCode: "ITM-CHEM-LOT-01",
  itemName: "Pelarut Industri Grade A",
  baseUomCode: "PCS",
  currencyCode: "IDR",
  valuationGroupCode: "DEFAULT",
  poolStatus: "ACTIVE",
  currentValuedBaseQuantity: 20,
  currentCarryingValueAmount: 6_400_000,
  currentMovingAverageUnitCost: 320_000,
  unrestrictedBaseQuantity: 12,
  qualityInspectionBaseQuantity: 8,
  blockedBaseQuantity: 0,
  availableBaseQuantity: 12,
  entries: [],
};

const stockDetailPayload: ItemStockDetailResponse = {
  itemId: "itm-chem",
  itemCode: "ITM-CHEM-LOT-01",
  itemName: "Pelarut Industri Grade A",
  baseUomCode: "PCS",
  lotTrackingPolicy: "REQUIRED",
  currencyCode: "IDR",
  poolStatus: "ACTIVE",
  carryingValueAmount: 6_400_000,
  movingAverageUnitCost: 320_000,
  reconciliation: {
    isReconciled: true,
    positionOnHandTotal: 20,
    positionUnrestricted: 12,
    positionQualityInspection: 8,
    positionBlocked: 0,
    positionReserved: 0,
    positionAvailable: 12,
    locationBalancesOnHandTotal: 20,
    locationBalancesUnrestricted: 12,
    locationBalancesQualityInspection: 8,
    locationBalancesBlocked: 0,
    locationBalancesReserved: 0,
    movementsNetTotal: 20,
    activeReservationsRemaining: 0,
  },
  locationBalances: [
    {
      stockLocationBalanceId: "bal-main",
      warehouseId: "wh-main",
      warehouseCode: "WH-MAIN",
      locationId: "loc-main",
      locationCode: "LOC-MAIN",
      locationName: "Rak Penyimpanan Utama",
      zoneType: "STORAGE",
      stockLotId: "lot-1",
      lotNumber: "LOT-JKT-2611-01",
      lotExpiryDate: "2027-11-30",
      stockStatus: "UNRESTRICTED",
      onHandBaseQuantity: 12,
      reservedBaseQuantity: 0,
      availableBaseQuantity: 12,
      updatedAtUtc: "2026-11-08T03:00:00Z",
    },
    {
      stockLocationBalanceId: "bal-qc",
      warehouseId: "wh-main",
      warehouseCode: "WH-MAIN",
      locationId: "loc-qc",
      locationCode: "LOC-QC",
      locationName: "Zona Karantina Mutu",
      zoneType: "QUARANTINE",
      stockLotId: "lot-1",
      lotNumber: "LOT-JKT-2611-01",
      lotExpiryDate: "2027-11-30",
      stockStatus: "QUALITY_INSPECTION",
      onHandBaseQuantity: 8,
      reservedBaseQuantity: 0,
      availableBaseQuantity: 0,
      updatedAtUtc: "2026-11-07T03:00:00Z",
    },
  ],
  lots: [],
  inspections: overviewPayload.inspections,
  reservations: [],
  movementTimeline: [],
};

const receiptsListPayload: GoodsReceiptListResponse = {
  items: [
    {
      goodsReceiptId: "grn-lot",
      receiptNumber: "GRN-2026-00010",
      purchaseOrderId: "po-10",
      poNumberSnapshot: "PO-2026-00010",
      poRevisionNumberSnapshot: 1,
      supplierCodeSnapshot: "PT-SUP-001",
      supplierLegalNameSnapshot: "PT Sumber Makmur Kemasan",
      supplierDeliveryNoteNumber: "SJ-LOT-010",
      receivedDate: "2026-11-09",
      receivingStation: "Dermaga Utama",
      status: "RELEASED_FOR_ACCEPTANCE",
      totalDeliveredBaseQuantity: 10,
      totalRejectedBaseQuantity: 0,
      totalValuedAcceptedBaseQuantity: 0,
      totalUnresolvedCustodyBaseQuantity: 10,
      totalReleasedBaseQuantity: 10,
      totalHeldBaseQuantity: 0,
      hasFinancialPosting: false,
      updatedAtUtc: "2026-11-09T03:00:00Z",
    },
  ],
  totalReceipts: 1,
  sumDeliveredBaseQuantity: 10,
  sumHeldBaseQuantity: 0,
  sumReleasedBaseQuantity: 10,
  sumRejectedBaseQuantity: 0,
  totalLinkedGlJournals: 0,
};

const lotReceiptDetail: GoodsReceiptDetailDto = {
  goodsReceiptId: "grn-lot",
  receiptNumber: "GRN-2026-00010",
  purchaseOrderId: "po-10",
  poRevisionId: "rev-10",
  poNumberSnapshot: "PO-2026-00010",
  poRevisionNumberSnapshot: 1,
  poSnapshotHash: "abcdef123456",
  supplierPartnerId: "sup-1",
  supplierCodeSnapshot: "PT-SUP-001",
  supplierLegalNameSnapshot: "PT Sumber Makmur Kemasan",
  supplierDeliveryNoteNumber: "SJ-LOT-010",
  idempotencyKey: "arr-s08-lot",
  arrivalPayloadHash: "hash-arr-s08",
  receivedDate: "2026-11-09",
  receivingStation: "Dermaga Utama",
  vehicleOrSealRef: null,
  documentEvidenceRef: null,
  notes: null,
  status: "RELEASED_FOR_ACCEPTANCE",
  totalDeliveredBaseQuantity: 10,
  totalRejectedBaseQuantity: 0,
  totalValuedAcceptedBaseQuantity: 0,
  totalUnresolvedCustodyBaseQuantity: 10,
  totalReleasedBaseQuantity: 10,
  totalHeldBaseQuantity: 0,
  hasFinancialPosting: false,
  linkedGlJournalCount: 0,
  receivedByUserId: "u-1",
  receivedByEmail: "budi.santoso@demo.athera.local",
  valuedAcceptanceJournalCount: 0,
  valuedAcceptances: [],
  createdAtUtc: "2026-11-09T03:00:00Z",
  updatedAtUtc: "2026-11-09T03:00:00Z",
  version: 2,
  wasIdempotentReplay: false,
  lines: [
    {
      goodsReceiptLineId: "grl-lot-1",
      lineNumber: 1,
      purchaseOrderId: "po-10",
      poRevisionId: "rev-10",
      poLineId: "pol-10",
      lineageLineKey: "L001",
      itemId: "itm-chem",
      itemCodeSnapshot: "ITM-CHEM-LOT-01",
      itemNameSnapshot: "Pelarut Industri Grade A",
      itemKindSnapshot: "STOCKED_GOOD",
      itemLotTrackingPolicy: "REQUIRED",
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
      custodyStatus: "RELEASED_FOR_ACCEPTANCE",
      discrepancyReasonCode: "NONE",
      discrepancyNotes: null,
      conditionEvidenceRef: null,
      maxApprovedReleaseCapBaseQty: 10,
      updatedAtUtc: "2026-11-09T03:00:00Z",
    },
  ],
  dockChecks: [],
};

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("next/link", () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/lib/api/client", () => ({
  fetchEntityWorkspaceDetail: async (): Promise<EntityWorkspaceDetailResponse> => ({
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
      permissions: activePermissions,
    },
    effectivePermissions: activePermissions,
    navigationCapabilities: [],
    serverTimestampUtc: "2026-11-18T00:00:00Z",
  }),
  fetchInventoryOverview: async () => overviewPayload,
  fetchItemValuationLedger: async () => ledgerPayload,
  getItemStockDetail: async () => stockDetailPayload,
  listInventoryWarehouses: async () => warehousesPayload,
  recordInspectionDisposition: (...args: unknown[]) => recordDispositionMock(...args),
  createStockReservation: (...args: unknown[]) => createReservationMock(...args),
  transferInventoryLocation: vi.fn(),
  transitionInventoryStockStatus: vi.fn(),
  releaseStockReservation: vi.fn(),
  createWarehouse: vi.fn(),
  createWarehouseLocation: vi.fn(),
  fetchUnitsOfMeasure: async (): Promise<UomListResponse> => ({
    items: [
      {
        uomId: "uom-pcs",
        uomCode: "PCS",
        name: "Pieces",
        quantityPrecisionScale: 0,
        status: "ACTIVE",
        updatedAtUtc: "2026-11-01T00:00:00Z",
        version: 1,
      },
    ],
  }),
  fetchCatalogItems: async (): Promise<PagedItemsResponse> => ({
    items: [
      {
        itemId: "itm-chem",
        itemCode: "ITM-CHEM-LOT-01",
        name: "Pelarut Industri Grade A",
        itemKind: "STOCKED_GOOD",
        lotTrackingPolicy: "REQUIRED",
        baseUomId: "uom-pcs",
        baseUomCode: "PCS",
        basePrecisionScale: 0,
        defaultPurchaseUomId: "uom-pcs",
        defaultPurchaseUomCode: "PCS",
        defaultSalesUomId: "uom-pcs",
        defaultSalesUomCode: "PCS",
        entityScopeMode: "ALL_ENTITIES",
        status: "ACTIVE",
        effectivePurchaseAllowed: true,
        effectiveSalesAllowed: true,
        conversionCount: 0,
        updatedAtUtc: "2026-11-07T00:00:00Z",
        version: 1,
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 50,
  }),
  fetchCatalogItemDetail: vi.fn(),
  createUnitOfMeasure: vi.fn(),
  createCatalogItem: vi.fn(),
  updateCatalogItem: vi.fn(),
  upsertItemUomConversion: vi.fn(),
  upsertItemEntityPolicy: vi.fn(),
  previewItemUomConversion: vi.fn(),
  fetchGoodsReceipts: async () => receiptsListPayload,
  fetchEligibleReceivingPos: async () => ({ items: [], od01Notice: "OD-01 active." }),
  fetchGoodsReceiptDetail: async () => lotReceiptDetail,
  recordGoodsReceiptArrival: vi.fn(),
  recordGoodsReceiptDockCheck: vi.fn(),
  previewReceiptAcceptance: vi.fn(),
  acceptGoodsReceiptValued: vi.fn(),
}));

describe("S08 Stock Availability, Locations, Lots & Quality Restrictions UI", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    activePermissions = [
      "workspace.view",
      "inventory.read",
      "inventory.manage",
      "quality.inspect",
      "receiving.read",
      "receiving.manage",
      "receiving.accept",
      "catalog.read",
      "catalog.manage",
    ];
  });

  it("renders 6-metric availability sheet and hides quality disposition controls when U1 lacks quality.inspect", async () => {
    activePermissions = ["workspace.view", "inventory.read", "inventory.manage"];

    render(
      <LocaleProvider>
        <InventoryPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("inv-stock-detail-sheet")).toBeInTheDocument();
      expect(screen.getByTestId("inv-reconciled-badge")).toHaveTextContent("Terekonsiliasi");
    });

    expect(screen.getByTestId("inv-metric-onhand")).toHaveTextContent("20 PCS");
    expect(screen.getByTestId("inv-metric-unrest")).toHaveTextContent("12 PCS");
    expect(screen.getByTestId("inv-metric-qi")).toHaveTextContent("8 PCS");
    expect(screen.getByTestId("inv-metric-avail")).toHaveTextContent("12 PCS");

    expect(screen.getByTestId("inv-quality-readonly-banner")).toBeInTheDocument();
    expect(document.getElementById("inv-disp-submit")).toBeNull();
    expect(document.getElementById("inv-xfer-submit")).toBeInTheDocument();
    expect(document.getElementById("inv-rsv-submit")).toBeInTheDocument();
  });

  it("submits quality disposition for U2 and surfaces OD-10 conflict banner on over-reservation", async () => {
    recordDispositionMock.mockResolvedValueOnce({
      inspection: overviewPayload.inspections[0],
      disposition: {},
      movementNumber: "MOV-2026-00019",
      wasIdempotentReplay: false,
    });
    createReservationMock.mockRejectedValueOnce({
      status: 409,
      errorCode: "od10_insufficient_available_stock",
      title: "Conflict",
      detail: "Exceeds available stock.",
    });

    render(
      <LocaleProvider>
        <InventoryPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("inv-disp-submit")).toBeInTheDocument();
    });

    fireEvent.change(document.getElementById("inv-disp-release-qty")!, { target: { value: "5" } });
    fireEvent.change(document.getElementById("inv-disp-block-qty")!, { target: { value: "3" } });
    fireEvent.click(document.getElementById("inv-disp-submit")!);

    await waitFor(() => {
      expect(recordDispositionMock).toHaveBeenCalledWith(
        TENANT_ID,
        ENTITY_ID,
        "qci-1",
        expect.objectContaining({
          releasedBaseQuantity: 5,
          blockedBaseQuantity: 3,
        }),
      );
      expect(document.getElementById("inv-op-success")).toHaveTextContent("MOV-2026-00019");
    });

    fireEvent.change(document.getElementById("inv-rsv-source-doc")!, { target: { value: "SO-OVER" } });
    fireEvent.change(document.getElementById("inv-rsv-qty")!, { target: { value: "99" } });
    fireEvent.click(document.getElementById("inv-rsv-submit")!);

    await waitFor(() => {
      expect(document.getElementById("inv-op-error")).toHaveTextContent("OD-10");
    });
  });

  it("exposes LotTrackingPolicy controls in /catalog and lot/location fields in /receiving Gate 2", async () => {
    const { unmount } = render(
      <LocaleProvider>
        <CatalogPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      const itemRow = screen.getByRole("row", { name: /ITM-CHEM-LOT-01/ });
      expect(within(itemRow).getByText("Lot Wajib (S08)")).toBeInTheDocument();
    });

    const lotPolicySelect = document.getElementById("new-item-lot-policy") as HTMLSelectElement;
    expect(lotPolicySelect).toBeInTheDocument();
    fireEvent.change(document.getElementById("new-item-kind")!, { target: { value: "NON_STOCK_SERVICE" } });
    expect(lotPolicySelect).toBeDisabled();
    expect(lotPolicySelect.value).toBe("NONE");
    unmount();

    render(
      <LocaleProvider>
        <ReceivingPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("gate2-acceptance-panel")).toBeInTheDocument();
      expect(document.getElementById("gate2-loc-0")).toBeInTheDocument();
      expect(document.getElementById("gate2-lot-0")).toBeInTheDocument();
      expect(document.getElementById("gate2-exp-0")).toBeInTheDocument();
    });
  });

  it("formats signed quantity deltas cleanly without coercing negative deltas to zero", () => {
    expect(formatSignedQuantityValue("-15.000000", "en-US")).toBe("-15");
    expect(formatSignedQuantityValue("-4.500000", "id-ID")).toBe("-4,5");
    expect(formatSignedQuantityValue("20.000000", "id-ID")).toBe("+20");
    expect(formatSignedQuantityValue("0.000000", "id-ID")).toBe("0");
  });
});
