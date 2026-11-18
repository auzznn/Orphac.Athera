import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import SalesOrdersPage from "@/app/(shell)/w/[tenantId]/[entityId]/sales-orders/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  SalesOrderDetailDto,
  SalesOrderListResponse,
  SalesOrdersReferenceDataResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const dewiUser: CurrentUserResponse = {
  userId: "55555555-5555-5555-5555-555555555105",
  email: "dewi.lestari@demo.athera.local",
  displayName: "Dewi Lestari",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-19T00:00:00Z",
  workspaceAccessStatus: "ACTIVE_WORKSPACES",
  authorizedWorkspaceCount: 1,
  mfaEnabled: false,
};

const budiUser: CurrentUserResponse = {
  userId: "22222222-2222-2222-2222-222222222102",
  email: "budi.santoso@demo.athera.local",
  displayName: "Budi Santoso",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-19T00:00:00Z",
  workspaceAccessStatus: "ACTIVE_WORKSPACES",
  authorizedWorkspaceCount: 3,
  mfaEnabled: false,
};

let activeUser: CurrentUserResponse = dewiUser;
let currentPermissions: string[] = ["workspace.view", "sales.read", "sales.manage", "inventory.read"];

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };

const refDataPayload: SalesOrdersReferenceDataResponse = {
  customers: [
    {
      partnerId: "cust-1",
      partnerCode: "PT-CUST-001",
      legalName: "PT Graha Ritel Nusantara",
      displayName: "Graha Ritel Nusantara",
      taxRegistrationNpwp: "01.777.332.1-011.000",
      taxPkpStatus: "PKP",
      defaultPaymentTermsDays: 14,
      entityScopeMode: "ALL_ENTITIES",
    },
  ],
  items: [
    {
      itemId: "itm-valve",
      itemCode: "ITM-S09-VALVE-01",
      name: "Valve Kuningan 1 Inch",
      itemKind: "STOCKED_GOOD",
      baseUomId: "uom-pcs",
      baseUomCode: "PCS",
      defaultSalesUomId: "uom-pcs",
      allowedUoms: [
        { uomId: "uom-pcs", uomCode: "PCS", name: "Pieces", factorToBase: 1, precisionScale: 0 },
      ],
      stockAvailability: {
        unrestrictedBaseQuantity: 100,
        reservedBaseQuantity: 0,
        availableBaseQuantity: 100,
        totalValuedOnHandBaseQuantity: 100,
      },
    },
  ],
  outputTaxCodes: [
    {
      taxCodeId: "tax-ppn-out-11",
      code: "PPN-OUT-11",
      name: "PPN Keluaran 11%",
      taxFamily: "PPN",
      direction: "OUTPUT",
      treatmentClass: "STANDARD",
      nominalRatePercent: 11,
      taxableBaseRatioNumerator: 1,
      taxableBaseRatioDenominator: 1,
      requiresCounterpartyNpwp: false,
      effectiveFrom: "2025-01-01",
      effectiveTo: null,
      reviewStatus: "UNREVIEWED_CONFIGURABLE",
    },
  ],
  od09Notice: "OD-09 safe defaults active.",
  od02Notice: "OD-02 four-eyes approval active.",
};

const listPayload: SalesOrderListResponse = {
  items: [
    {
      salesOrderId: "so-1",
      draftCode: "SO-DRF-20261115-A1",
      soNumber: "SO-2026-00001",
      status: "APPROVED",
      fulfillmentProgress: "NOT_FULFILLED",
      customerPartnerId: "cust-1",
      customerCode: "PT-CUST-001",
      customerName: "PT Graha Ritel Nusantara",
      currencyCode: "IDR",
      orderDate: "2026-11-15",
      requestedDeliveryDate: "2026-11-22",
      deliveryAddressSnapshot: "Gudang Distribusi Bekasi Timur Blok C-12",
      paymentTermsDays: 14,
      customerReference: "PO-CUST-GRN-2026-88",
      currentApprovedRevisionNumber: 1,
      latestRevisionNumber: 2,
      hasOpenRevision: true,
      pendingApprovalRevisionId: "rev-2",
      pendingApprovalSubmittedByUserId: dewiUser.userId,
      currentSubtotalAmount: 380000,
      currentOutputTaxTotalAmount: 41800,
      currentGrandTotalAmount: 421800,
      currentOrderedBaseQuantity: 4,
      totalReservedBaseQuantity: 0,
      totalDispatchedBaseQuantity: 0,
      totalBilledBaseQuantity: 0,
      updatedAtUtc: "2026-11-18T04:00:00Z",
      pendingGrandTotalAmount: 632700,
    },
  ],
};

const pendingAmendedDetailPayload: SalesOrderDetailDto = {
  salesOrderId: "so-1",
  draftCode: "SO-DRF-20261115-A1",
  soNumber: "SO-2026-00001",
  status: "APPROVED",
  fulfillmentProgress: "NOT_FULFILLED",
  customerPartnerId: "cust-1",
  customerCode: "PT-CUST-001",
  customerName: "PT Graha Ritel Nusantara",
  currencyCode: "IDR",
  orderDate: "2026-11-15",
  requestedDeliveryDate: "2026-11-22",
  deliveryAddressSnapshot: "Gudang Distribusi Bekasi Timur Blok C-12",
  paymentTermsDays: 14,
  customerReference: "PO-CUST-GRN-2026-88",
  currentApprovedRevisionId: "rev-1",
  currentApprovedRevisionNumber: 1,
  latestRevisionNumber: 2,
  approvedOrderedBaseQuantity: 4,
  totalReservedBaseQuantity: 0,
  totalDispatchedBaseQuantity: 0,
  totalBilledBaseQuantity: 0,
  closedByUserId: null,
  closedAtUtc: null,
  closureReason: null,
  cancelledByUserId: null,
  cancelledAtUtc: null,
  cancellationReason: null,
  createdByUserId: dewiUser.userId,
  createdAtUtc: "2026-11-15T01:00:00Z",
  updatedAtUtc: "2026-11-18T04:00:00Z",
  version: 2,
  revisions: [
    {
      revisionId: "rev-2",
      revisionNumber: 2,
      supersedesRevisionId: "rev-1",
      status: "PENDING_APPROVAL",
      isFrozen: true,
      customerPartnerId: "cust-1",
      customerCodeSnapshot: "PT-CUST-001",
      customerLegalNameSnapshot: "PT Graha Ritel Nusantara",
      customerNpwpSnapshot: "01.777.332.1-011.000",
      customerPkpStatusSnapshot: "PKP",
      currencyCode: "IDR",
      orderDate: "2026-11-15",
      requestedDeliveryDate: "2026-11-22",
      deliveryAddressSnapshot: "Gudang Distribusi Bekasi Timur Blok C-12",
      paymentTermsDays: 14,
      customerReference: "PO-CUST-GRN-2026-88",
      changeReason: "Penambahan kuantitas menjadi 6 PCS",
      notes: null,
      subtotalAmount: 570000,
      outputTaxTotalAmount: 62700,
      grandTotalAmount: 632700,
      totalOrderedBaseQuantity: 6,
      snapshotHash: "abcd1234abcd1234abcd1234abcd1234abcd1234abcd1234abcd1234abcd1234",
      createdByUserId: dewiUser.userId,
      createdAtUtc: "2026-11-18T03:00:00Z",
      submittedByUserId: dewiUser.userId,
      submittedAtUtc: "2026-11-18T03:10:00Z",
      decidedByUserId: null,
      decidedAtUtc: null,
      decisionReason: null,
      version: 2,
      lines: [
        {
          soLineId: "sol-21",
          lineNumber: 1,
          lineageLineKey: "SO-L001",
          itemId: "itm-valve",
          itemCodeSnapshot: "ITM-S09-VALVE-01",
          itemNameSnapshot: "Valve Kuningan 1 Inch",
          itemKindSnapshot: "STOCKED_GOOD",
          orderedUomId: "uom-pcs",
          orderedUomCodeSnapshot: "PCS",
          baseUomId: "uom-pcs",
          baseUomCodeSnapshot: "PCS",
          uomFactorToBaseSnapshot: 1,
          orderedQuantity: 6,
          orderedBaseQuantity: 6,
          reservedBaseQuantity: 0,
          dispatchedBaseQuantity: 0,
          billedBaseQuantity: 0,
          unitPrice: 95000,
          discountAmount: 0,
          lineSubtotalAmount: 570000,
          taxCodeId: "tax-ppn-out-11",
          taxCodeSnapshot: "PPN-OUT-11",
          taxTreatmentClassSnapshot: "STANDARD",
          taxRatePercentSnapshot: 11,
          taxableBaseAmount: 570000,
          taxAmount: 62700,
          lineTotalAmount: 632700,
          requestedDeliveryDate: "2026-11-22",
          lineDescription: null,
        },
      ],
      approval: {
        approvalId: "app-so-2",
        policyId: "pol-so-1",
        status: "PENDING",
        requestSummary: "SO-2026-00001 rev 2",
        requestedByUserId: dewiUser.userId,
        requestedByEmail: dewiUser.email,
        requestedAtUtc: "2026-11-18T03:10:00Z",
        decidedByUserId: null,
        decidedByEmail: null,
        decidedAtUtc: null,
        decisionReason: null,
      },
      diffFromBaseline: {
        targetRevisionNumber: 2,
        baselineRevisionNumber: 1,
        headerChanges: [],
        lineChanges: [
          {
            lineageLineKey: "SO-L001",
            changeKind: "MODIFIED",
            previousItemCode: "ITM-S09-VALVE-01",
            currentItemCode: "ITM-S09-VALVE-01",
            previousQuantity: 4,
            currentQuantity: 6,
            previousUomCode: "PCS",
            currentUomCode: "PCS",
            previousUnitPrice: 95000,
            currentUnitPrice: 95000,
            previousSubtotal: 380000,
            currentSubtotal: 570000,
          },
        ],
        previousGrandTotal: 421800,
        currentGrandTotal: 632700,
        grandTotalDelta: 210900,
        previousOrderedBaseQty: 4,
        currentOrderedBaseQty: 6,
        orderedBaseQtyDelta: 2,
      },
    },
    {
      revisionId: "rev-1",
      revisionNumber: 1,
      supersedesRevisionId: null,
      status: "APPROVED",
      isFrozen: true,
      customerPartnerId: "cust-1",
      customerCodeSnapshot: "PT-CUST-001",
      customerLegalNameSnapshot: "PT Graha Ritel Nusantara",
      customerNpwpSnapshot: "01.777.332.1-011.000",
      customerPkpStatusSnapshot: "PKP",
      currencyCode: "IDR",
      orderDate: "2026-11-15",
      requestedDeliveryDate: "2026-11-22",
      deliveryAddressSnapshot: "Gudang Distribusi Bekasi Timur Blok C-12",
      paymentTermsDays: 14,
      customerReference: "PO-CUST-GRN-2026-88",
      changeReason: null,
      notes: null,
      subtotalAmount: 380000,
      outputTaxTotalAmount: 41800,
      grandTotalAmount: 421800,
      totalOrderedBaseQuantity: 4,
      snapshotHash: "1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff",
      createdByUserId: dewiUser.userId,
      createdAtUtc: "2026-11-15T01:00:00Z",
      submittedByUserId: dewiUser.userId,
      submittedAtUtc: "2026-11-15T01:05:00Z",
      decidedByUserId: budiUser.userId,
      decidedAtUtc: "2026-11-15T01:15:00Z",
      decisionReason: "Terverifikasi",
      version: 2,
      lines: [
        {
          soLineId: "sol-11",
          lineNumber: 1,
          lineageLineKey: "SO-L001",
          itemId: "itm-valve",
          itemCodeSnapshot: "ITM-S09-VALVE-01",
          itemNameSnapshot: "Valve Kuningan 1 Inch",
          itemKindSnapshot: "STOCKED_GOOD",
          orderedUomId: "uom-pcs",
          orderedUomCodeSnapshot: "PCS",
          baseUomId: "uom-pcs",
          baseUomCodeSnapshot: "PCS",
          uomFactorToBaseSnapshot: 1,
          orderedQuantity: 4,
          orderedBaseQuantity: 4,
          reservedBaseQuantity: 0,
          dispatchedBaseQuantity: 0,
          billedBaseQuantity: 0,
          unitPrice: 95000,
          discountAmount: 0,
          lineSubtotalAmount: 380000,
          taxCodeId: "tax-ppn-out-11",
          taxCodeSnapshot: "PPN-OUT-11",
          taxTreatmentClassSnapshot: "STANDARD",
          taxRatePercentSnapshot: 11,
          taxableBaseAmount: 380000,
          taxAmount: 41800,
          lineTotalAmount: 421800,
          requestedDeliveryDate: "2026-11-22",
          lineDescription: null,
        },
      ],
      approval: null,
      diffFromBaseline: null,
    },
  ],
};

const createSalesOrderMock = vi.fn(async () => pendingAmendedDetailPayload);
const decideSalesOrderRevisionMock = vi.fn(async () => pendingAmendedDetailPayload);

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

vi.mock("@/components/AppShell", () => ({
  useSessionUser: () => activeUser,
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
  fetchSalesOrdersReferenceData: async () => refDataPayload,
  fetchSalesOrders: async () => listPayload,
  fetchSalesOrderDetail: async () => pendingAmendedDetailPayload,
  createSalesOrder: (...args: unknown[]) => createSalesOrderMock(...(args as [])),
  updateSalesOrderRevision: vi.fn(async () => pendingAmendedDetailPayload),
  createSalesOrderAmendment: vi.fn(async () => pendingAmendedDetailPayload),
  submitSalesOrderRevision: vi.fn(async () => pendingAmendedDetailPayload),
  decideSalesOrderRevision: (...args: unknown[]) => decideSalesOrderRevisionMock(...(args as [])),
  cancelSalesOrder: vi.fn(async () => pendingAmendedDetailPayload),
  closeSalesOrder: vi.fn(async () => pendingAmendedDetailPayload),
}));

describe("S12 Customer Sales Orders UI", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    activeUser = dewiUser;
    currentPermissions = ["workspace.view", "sales.read", "sales.manage", "inventory.read"];
  });

  it("displays live stock availability, computes PPN-OUT-11 preview totals, and submits draft payload", async () => {
    render(
      <LocaleProvider>
        <SalesOrdersPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("SO-2026-00001")).toBeInTheDocument();
    });

    // Expand OD-09 governance rule strip
    fireEvent.click(document.getElementById("so-policy-toggle") as HTMLButtonElement);
    expect(screen.getByText(/Aturan Harga Aman OD-09|OD-09 Safe Commercial Defaults/)).toBeInTheDocument();

    // Open editor tab & inspect live stock availability + totals
    fireEvent.click(document.getElementById("so-tab-editor") as HTMLButtonElement);
    expect(document.getElementById("so-panel-editor")).toHaveTextContent("100 PCS");

    fireEvent.change(document.getElementById("so-ed-address") as HTMLInputElement, {
      target: { value: "Kawasan Industri Cikarang Blok F-7" },
    });
    fireEvent.change(document.getElementById("so-ed-cust-ref") as HTMLInputElement, {
      target: { value: "CUST-PO-2026-901" },
    });
    fireEvent.change(document.getElementById("so-line-qty-0") as HTMLInputElement, { target: { value: "4" } });
    fireEvent.change(document.getElementById("so-line-price-0") as HTMLInputElement, {
      target: { value: "95000" },
    });
    fireEvent.change(document.getElementById("so-line-tax-0") as HTMLSelectElement, {
      target: { value: "tax-ppn-out-11" },
    });

    const editorPanel = document.getElementById("so-panel-editor") as HTMLElement;
    expect(editorPanel).toHaveTextContent("Rp 380.000");
    expect(editorPanel).toHaveTextContent("Rp 41.800");
    expect(editorPanel).toHaveTextContent("Rp 421.800");

    fireEvent.click(document.getElementById("so-save-draft-btn") as HTMLButtonElement);
    await waitFor(() => {
      expect(createSalesOrderMock).toHaveBeenCalledWith(
        TENANT_ID,
        ENTITY_ID,
        expect.objectContaining({
          customerPartnerId: "cust-1",
          deliveryAddressSnapshot: "Kawasan Industri Cikarang Blok F-7",
          customerReference: "CUST-PO-2026-901",
          creditLimitOverrideRequested: false,
          lines: [
            expect.objectContaining({
              itemId: "itm-valve",
              orderedUomId: "uom-pcs",
              orderedQuantity: 4,
              unitPrice: 95000,
              discountAmount: 0,
              taxCodeId: "tax-ppn-out-11",
            }),
          ],
        }),
      );
    });
  });

  it("enforces OD-02 self-approval SoD notice for submitter and enables independent approver diff approval", async () => {
    const firstRender = render(
      <LocaleProvider>
        <SalesOrdersPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("open-so-SO-2026-00001")).toBeInTheDocument();
    });

    // Ledger row shows approved Rev 1 amount & base qty (Rp 421.800, Rev 1 · Basis: 4) with Rev 2 open tag
    const ordersTable = document.getElementById("so-orders-table") as HTMLElement;
    expect(ordersTable).toHaveTextContent("Rp 421.800");
    expect(ordersTable).toHaveTextContent(/Rev 1 · Basis: 4|Rev 1 · Base: 4/);
    expect(ordersTable).toHaveTextContent(/Rev 2 · Revisi Terbuka|Rev 2 · Open Revision/);

    // Four-eyes queue displays the pending revision amount (Rp 632.700, Rev 2)
    fireEvent.click(document.getElementById("so-tab-queue") as HTMLButtonElement);
    const queueTable = document.getElementById("so-queue-table") as HTMLElement;
    expect(queueTable).toHaveTextContent("Rp 632.700");
    expect(queueTable).toHaveTextContent("Rev 2");
    fireEvent.click(document.getElementById("so-tab-orders") as HTMLButtonElement);

    fireEvent.click(screen.getByTestId("open-so-SO-2026-00001"));
    await waitFor(() => {
      expect(screen.getByTestId("so-sod-notice")).toBeInTheDocument();
    });
    expect(document.getElementById("so-approve-btn")).not.toBeInTheDocument();
    expect(document.getElementById("so-revision-diff")).toHaveTextContent("Rp 210.900");

    firstRender.unmount();

    // Switch to independent Finance Controller actor with sales.approve
    activeUser = budiUser;
    currentPermissions = ["workspace.view", "sales.read", "sales.approve"];

    render(
      <LocaleProvider>
        <SalesOrdersPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("open-so-SO-2026-00001")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("open-so-SO-2026-00001"));
    await waitFor(() => {
      expect(document.getElementById("so-approve-btn")).toBeInTheDocument();
    });
    expect(screen.queryByTestId("so-sod-notice")).not.toBeInTheDocument();

    fireEvent.change(document.getElementById("so-decision-reason") as HTMLInputElement, {
      target: { value: "Qty 4 -> 6 verified against stock & PO ref." },
    });
    fireEvent.click(document.getElementById("so-approve-btn") as HTMLButtonElement);

    await waitFor(() => {
      expect(decideSalesOrderRevisionMock).toHaveBeenCalledWith(
        TENANT_ID,
        ENTITY_ID,
        "so-1",
        "rev-2",
        { decision: "APPROVED", reason: "Qty 4 -> 6 verified against stock & PO ref." },
      );
    });
  });

  it("shows read-only banner and hides composer, amend, and approval buttons for sales.read-only viewers", async () => {
    activeUser = budiUser;
    currentPermissions = ["workspace.view", "sales.read"];

    render(
      <LocaleProvider>
        <SalesOrdersPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("so-readonly-banner")).toBeInTheDocument();
    });

    expect(document.getElementById("so-new-draft-btn")).not.toBeInTheDocument();
    expect(document.getElementById("so-tab-editor")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("open-so-SO-2026-00001"));
    await waitFor(() => {
      expect(document.getElementById("so-detail-sheet")).toBeInTheDocument();
    });
    expect(document.getElementById("so-amend-btn")).not.toBeInTheDocument();
    expect(document.getElementById("so-approve-btn")).not.toBeInTheDocument();
    expect(document.getElementById("so-cancel-btn")).not.toBeInTheDocument();
  });
});
