import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import FulfillmentPage from "@/app/(shell)/w/[tenantId]/[entityId]/fulfillment/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  EntityWorkspaceDetailResponse,
  FulfillmentOverviewResponse,
  PickOrderDto,
  SalesReservationDto,
  SalesShipmentDto,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

let currentPermissions: string[] = [
  "workspace.view",
  "inventory.read",
  "inventory.manage",
  "sales.read",
];

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };

const sampleReservation: SalesReservationDto = {
  stockReservationId: "rsv-1",
  reservationNumber: "RSV-2026-00001",
  salesOrderId: "so-1",
  salesOrderNumber: "SO-2026-00001",
  soLineId: "sol-1",
  lineageLineKey: "SO-L001",
  itemId: "itm-valve",
  itemCode: "ITM-S09-VALVE-01",
  itemName: "Katup Industri PN16 50mm",
  baseUomCode: "PCS",
  warehouseId: "wh-1",
  warehouseCode: "WH-MAIN",
  locationId: "loc-main",
  locationCode: "LOC-MAIN",
  stockLotId: null,
  lotNumber: null,
  reservedBaseQuantity: 6,
  consumedBaseQuantity: 0,
  releasedBaseQuantity: 0,
  remainingReservedBaseQuantity: 6,
  allocatedToOpenPickBaseQuantity: 0,
  unallocatedReservedBaseQuantity: 6,
  status: "ACTIVE",
  notes: "Reserve 6 PCS",
  createdAtUtc: "2026-11-19T02:00:00Z",
  updatedAtUtc: "2026-11-19T02:00:00Z",
};

const samplePickOrder: PickOrderDto = {
  pickOrderId: "pck-1",
  pickNumber: "PCK-2026-00001",
  salesOrderId: "so-1",
  salesOrderNumber: "SO-2026-00001",
  revisionId: "rev-1",
  warehouseId: "wh-1",
  warehouseCode: "WH-MAIN",
  status: "PICKED",
  totalRequestedBaseQuantity: 6,
  totalPickedBaseQuantity: 6,
  totalDispatchedBaseQuantity: 0,
  remainingToDispatchBaseQuantity: 6,
  notes: "Quick pick from RSV-2026-00001",
  createdAtUtc: "2026-11-19T02:05:00Z",
  pickedAtUtc: "2026-11-19T02:06:00Z",
  updatedAtUtc: "2026-11-19T02:06:00Z",
  lines: [
    {
      pickLineId: "pl-1",
      lineNumber: 1,
      soLineId: "sol-1",
      lineageLineKey: "SO-L001",
      stockReservationId: "rsv-1",
      reservationNumber: "RSV-2026-00001",
      itemId: "itm-valve",
      itemCode: "ITM-S09-VALVE-01",
      itemName: "Katup Industri PN16 50mm",
      baseUomCode: "PCS",
      warehouseId: "wh-1",
      locationId: "loc-main",
      locationCode: "LOC-MAIN",
      stockLotId: null,
      lotNumber: null,
      requestedPickBaseQuantity: 6,
      pickedBaseQuantity: 6,
      dispatchedBaseQuantity: 0,
      remainingToDispatchBaseQuantity: 6,
    },
  ],
};

const sampleShipment: SalesShipmentDto = {
  shipmentId: "shp-1",
  shipmentNumber: "SHP-2026-00001",
  pickOrderId: "pck-1",
  pickNumber: "PCK-2026-00001",
  salesOrderId: "so-1",
  salesOrderNumber: "SO-2026-00001",
  customerPartnerId: "cust-1",
  customerCode: "PT-CUST-001",
  customerName: "PT Graha Ritel Nusantara",
  deliveryAddressSnapshot: "Gudang Distribusi Bekasi Timur Blok C-12",
  dispatchDate: "2026-11-19",
  status: "DISPATCHED",
  carrierName: "Armada Distribusi Nusantara",
  trackingOrVehicleNumber: "B 9412 TCD",
  deliveryNoteReference: "SJ-SO-2026-00001-00001",
  deliveryConfirmedByName: null,
  deliveryEvidenceReference: null,
  deliveredAtUtc: null,
  stockMovementId: "mov-1",
  stockMovementNumber: "MOV-2026-00005",
  glJournalId: "jrn-1",
  glJournalNumber: "JRN-2026-11-00008",
  currencyCode: "IDR",
  totalDispatchedBaseQuantity: 4,
  totalCogsAmount: 160000,
  notes: null,
  dispatchedAtUtc: "2026-11-19T02:15:00Z",
  lines: [
    {
      shipmentLineId: "shpl-1",
      lineNumber: 1,
      pickLineId: "pl-1",
      soLineId: "sol-1",
      lineageLineKey: "SO-L001",
      stockReservationId: "rsv-1",
      itemId: "itm-valve",
      itemCode: "ITM-S09-VALVE-01",
      itemName: "Katup Industri PN16 50mm",
      baseUomCode: "PCS",
      locationId: "loc-main",
      locationCode: "LOC-MAIN",
      stockLotId: null,
      lotNumber: null,
      dispatchedBaseQuantity: 4,
      unitCostApplied: 40000,
      lineCogsAmount: 160000,
      movementLineId: "ml-1",
      valuationEntryId: "ve-1",
    },
  ],
};

const overviewPayload: FulfillmentOverviewResponse = {
  fulfillableOrders: [
    {
      salesOrderId: "so-1",
      soNumber: "SO-2026-00001",
      status: "APPROVED",
      fulfillmentProgress: "PARTIALLY_RESERVED",
      customerPartnerId: "cust-1",
      customerCode: "PT-CUST-001",
      customerName: "PT Graha Ritel Nusantara",
      deliveryAddressSnapshot: "Gudang Distribusi Bekasi Timur Blok C-12",
      orderDate: "2026-11-14",
      requestedDeliveryDate: "2026-11-22",
      currentApprovedRevisionId: "rev-1",
      currentApprovedRevisionNumber: 1,
      totalOrderedBaseQuantity: 10,
      totalReservedBaseQuantity: 6,
      totalDispatchedBaseQuantity: 0,
      remainingUnfulfilledBaseQuantity: 10,
      lines: [
        {
          soLineId: "sol-1",
          lineNumber: 1,
          lineageLineKey: "SO-L001",
          itemId: "itm-valve",
          itemCode: "ITM-S09-VALVE-01",
          itemName: "Katup Industri PN16 50mm",
          lotTrackingPolicy: "NONE",
          orderedUomCode: "PCS",
          orderedQuantity: 10,
          baseUomCode: "PCS",
          orderedBaseQuantity: 10,
          reservedBaseQuantity: 6,
          dispatchedBaseQuantity: 0,
          remainingToReserveBaseQuantity: 4,
          remainingToDispatchBaseQuantity: 10,
          movingAverageUnitCost: 40000,
          eligibleLocations: [
            {
              stockLocationBalanceId: "bal-main",
              warehouseId: "wh-1",
              warehouseCode: "WH-MAIN",
              locationId: "loc-main",
              locationCode: "LOC-MAIN",
              zoneType: "STORAGE",
              stockLotId: null,
              lotNumber: null,
              expiryDate: null,
              onHandBaseQuantity: 40,
              reservedBaseQuantity: 6,
              availableBaseQuantity: 34,
            },
          ],
        },
      ],
    },
  ],
  reservations: [sampleReservation],
  pickOrders: [samplePickOrder],
  shipments: [sampleShipment],
  od10Notice: "OD-10 active",
  od09Notice: "OD-09 active",
};

const reserveMock = vi.fn(async () => sampleReservation);
const dispatchMock = vi.fn(async () => sampleShipment);
const deliverMock = vi.fn(async () => ({ ...sampleShipment, status: "DELIVERED" }));

const fetchWorkspaceMock = vi.fn(async (): Promise<EntityWorkspaceDetailResponse> => ({
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
  serverTimestampUtc: "2026-11-19T00:00:00Z",
}));

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/lib/api/client", () => ({
  ApiProblemError: class ApiProblemError extends Error {
    status: number;
    detail: string;
    code?: string;
    constructor(status: number, detail: string, code?: string) {
      super(detail);
      this.status = status;
      this.detail = detail;
      this.code = code;
    }
  },
  fetchEntityWorkspaceDetail: () => fetchWorkspaceMock(),
  fetchFulfillmentOverview: async () => overviewPayload,
  reserveSalesOrderLine: (...args: unknown[]) => reserveMock(...(args as [])),
  releaseSalesOrderReservation: vi.fn(async () => sampleReservation),
  createPickOrder: vi.fn(async () => samplePickOrder),
  confirmPickOrder: vi.fn(async () => samplePickOrder),
  dispatchSalesShipment: (...args: unknown[]) => dispatchMock(...(args as [])),
  confirmSalesShipmentDelivery: (...args: unknown[]) => deliverMock(...(args as [])),
}));

describe("S13 Sales Reservation, Pick and Dispatch UI", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentPermissions = ["workspace.view", "inventory.read", "inventory.manage", "sales.read"];
  });

  it("renders SO fulfillment queue with eligible UNRESTRICTED location balances and submits stock reservation", async () => {
    render(
      <LocaleProvider>
        <FulfillmentPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("SO-2026-00001")).toBeInTheDocument();
    });

    // Expand OD-10 governance rule strip
    fireEvent.click(document.getElementById("ful-policy-toggle") as HTMLButtonElement);
    expect(
      screen.getByText(/Larangan Stok Tertahan & Negatif \(OD-10\)|No Restricted or Negative Stock \(OD-10\)/),
    ).toBeInTheDocument();

    // Open reserve drawer for SO-2026-00001 SO-L001
    fireEvent.click(screen.getByTestId("open-reserve-SO-2026-00001-SO-L001"));
    expect(document.getElementById("ful-reserve-drawer")).toBeInTheDocument();

    fireEvent.change(document.getElementById("ful-rsv-qty") as HTMLInputElement, { target: { value: "4" } });
    fireEvent.change(document.getElementById("ful-rsv-notes") as HTMLInputElement, {
      target: { value: "Batch ke-2" },
    });
    fireEvent.click(document.getElementById("ful-rsv-submit-btn") as HTMLButtonElement);

    await waitFor(() => {
      expect(reserveMock).toHaveBeenCalledWith(
        TENANT_ID,
        ENTITY_ID,
        expect.objectContaining({
          salesOrderId: "so-1",
          soLineId: "sol-1",
          locationId: "loc-main",
          stockLotId: null,
          quantityBase: 4,
          notes: "Batch ke-2",
        }),
      );
    });
  });

  it("previews live weighted-average COGS on partial shipment dispatch and records POD delivery confirmation", async () => {
    render(
      <LocaleProvider>
        <FulfillmentPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("SO-2026-00001")).toBeInTheDocument();
    });

    // Switch to Reservations & Warehouse Picks tab and open dispatch drawer on PCK-2026-00001
    fireEvent.click(document.getElementById("ful-tab-picks") as HTMLButtonElement);
    fireEvent.click(screen.getByTestId("open-dispatch-PCK-2026-00001"));

    const dispatchDrawer = document.getElementById("ful-dispatch-drawer") as HTMLElement;
    expect(dispatchDrawer).toBeInTheDocument();
    // 6 PCS @ Rp 40.000 = Rp 240.000 initially
    expect(dispatchDrawer).toHaveTextContent("Rp 240.000");

    // Partial dispatch 4 PCS @ Rp 40.000 -> Rp 160.000 projected COGS
    fireEvent.change(document.getElementById("ful-disp-qty-0") as HTMLInputElement, { target: { value: "4" } });
    expect(dispatchDrawer).toHaveTextContent("Rp 160.000");

    fireEvent.change(document.getElementById("ful-disp-sj") as HTMLInputElement, {
      target: { value: "SJ-JKT-2026-77" },
    });
    fireEvent.click(document.getElementById("ful-disp-submit-btn") as HTMLButtonElement);

    await waitFor(() => {
      expect(dispatchMock).toHaveBeenCalledWith(
        TENANT_ID,
        ENTITY_ID,
        expect.objectContaining({
          pickOrderId: "pck-1",
          deliveryNoteReference: "SJ-JKT-2026-77",
          lines: [{ pickLineId: "pl-1", dispatchedBaseQuantity: 4 }],
        }),
      );
    });

    // Automatically transitions to Shipments tab -> confirm delivery POD
    await waitFor(() => {
      expect(screen.getByTestId("open-deliver-SHP-2026-00001")).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId("open-deliver-SHP-2026-00001"));

    fireEvent.change(document.getElementById("ful-pod-receiver") as HTMLInputElement, {
      target: { value: "Tari Wibowo" },
    });
    fireEvent.change(document.getElementById("ful-pod-ref") as HTMLInputElement, {
      target: { value: "POD-GRN-2026-77" },
    });
    fireEvent.click(document.getElementById("ful-pod-submit-btn") as HTMLButtonElement);

    await waitFor(() => {
      expect(deliverMock).toHaveBeenCalledWith(TENANT_ID, ENTITY_ID, "shp-1", {
        confirmedByName: "Tari Wibowo",
        evidenceReference: "POD-GRN-2026-77",
      });
    });
  });

  it("shows read-only banner and hides reserve, release, pick, dispatch, and POD controls for viewers without inventory.manage", async () => {
    currentPermissions = ["workspace.view", "inventory.read", "sales.read"];

    render(
      <LocaleProvider>
        <FulfillmentPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("ful-readonly-banner")).toBeInTheDocument();
    });

    expect(screen.queryByTestId("open-reserve-SO-2026-00001-SO-L001")).not.toBeInTheDocument();

    fireEvent.click(document.getElementById("ful-tab-picks") as HTMLButtonElement);
    expect(screen.queryByTestId("release-rsv-RSV-2026-00001")).not.toBeInTheDocument();
    expect(screen.queryByTestId("pick-rsv-RSV-2026-00001")).not.toBeInTheDocument();
    expect(screen.queryByTestId("open-dispatch-PCK-2026-00001")).not.toBeInTheDocument();

    fireEvent.click(document.getElementById("ful-tab-shipments") as HTMLButtonElement);
    expect(screen.queryByTestId("open-deliver-SHP-2026-00001")).not.toBeInTheDocument();
  });
});
