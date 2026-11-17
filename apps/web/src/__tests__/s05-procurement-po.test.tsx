import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ProcurementPage from "@/app/(shell)/w/[tenantId]/[entityId]/procurement/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  ProcurementReferenceDataResponse,
  PurchaseOrderDetailDto,
  PurchaseOrderListResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const mockUser: CurrentUserResponse = {
  userId: "11111111-1111-1111-1111-111111111101",
  email: "rina.utami@demo.athera.local",
  displayName: "Rina Utami",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-18T00:00:00Z",
  workspaceAccessStatus: "ACTIVE_WORKSPACES",
  authorizedWorkspaceCount: 1,
  mfaEnabled: false,
};

let currentPermissions = ["workspace.view", "procurement.read", "procurement.manage"];

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

const refDataPayload: ProcurementReferenceDataResponse = {
  suppliers: [
    {
      partnerId: "sup-1",
      partnerCode: "PT-SUP-001",
      legalName: "PT Sumber Makmur Kemasan",
      displayName: "Sumber Makmur",
      taxRegistrationNpwp: "01.888.221.4-012.000",
      taxPkpStatus: "PKP",
      defaultPaymentTermsDays: 30,
      entityScopeMode: "ALL_ENTITIES",
    },
  ],
  items: [
    {
      itemId: "itm-1",
      itemCode: "ITM-PKG-001",
      name: "Karton Lipat Polos 40x30x25 cm",
      itemKind: "STOCKED_GOOD",
      baseUomId: "uom-pcs",
      baseUomCode: "PCS",
      defaultPurchaseUomId: "uom-box",
      allowedUoms: [
        { uomId: "uom-pcs", uomCode: "PCS", name: "Pieces", factorToBase: 1, precisionScale: 0 },
        { uomId: "uom-box", uomCode: "BOX", name: "Box Karton", factorToBase: 24, precisionScale: 0 },
      ],
    },
  ],
  taxCodes: [
    {
      taxCodeId: "tax-1112",
      code: "PPN-12-DPP1112",
      name: "PPN Masukan 12% DPP 11/12 (Efektif 11%)",
      taxFamily: "PPN",
      direction: "INPUT",
      nominalRatePercent: 12,
      taxableBaseRatioNumerator: 11,
      taxableBaseRatioDenominator: 12,
      requiresCounterpartyNpwp: false,
      effectiveFrom: "2025-01-01",
      effectiveTo: null,
      reviewStatus: "UNREVIEWED_CONFIGURABLE",
    },
  ],
  tolerancePolicies: [
    {
      policyId: "tol-1",
      code: "EXACT_QTY_OD01_SAFE",
      name: "Exact Approved Quantity Only (OD-01 Safe Default)",
      mode: "EXACT_APPROVED_QTY_ONLY",
      overdeliveryTolerancePercent: 0,
      overdeliveryToleranceBaseQty: 0,
      allowUnlimitedOverdelivery: false,
      reviewStatus: "UNREVIEWED_SAFE_DEFAULT",
      isDefault: true,
    },
  ],
  od01Notice: "OD-01 notice",
  od02Notice: "OD-02 notice",
};

const poListPayload: PurchaseOrderListResponse = {
  items: [
    {
      purchaseOrderId: "po-1",
      draftCode: "DRF-20261118-A1",
      poNumber: "PO-2026-00001",
      status: "APPROVED",
      receivingProgress: "NOT_RECEIVED",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierName: "Sumber Makmur",
      currencyCode: "IDR",
      orderDate: "2026-11-18",
      expectedDeliveryDate: "2026-11-28",
      deliveryDestination: "Gudang Pusat Jakarta",
      paymentTermsDays: 30,
      currentApprovedRevisionNumber: 1,
      latestRevisionNumber: 2,
      hasOpenRevision: true,
      pendingApprovalRevisionId: "rev-2",
      pendingApprovalSubmittedByUserId: mockUser.userId,
      currentGrandTotalAmount: 1065600,
      currentOrderedBaseQuantity: 48,
      acceptedBaseQuantityTotal: 0,
      remainingOpenBaseQuantity: 48,
      downstreamReferenceCount: 0,
      updatedAtUtc: "2026-11-18T04:00:00Z",
    },
  ],
};

const poDetailPayload: PurchaseOrderDetailDto = {
  purchaseOrderId: "po-1",
  draftCode: "DRF-20261118-A1",
  poNumber: "PO-2026-00001",
  status: "APPROVED",
  receivingProgress: "NOT_RECEIVED",
  supplierPartnerId: "sup-1",
  supplierCode: "PT-SUP-001",
  supplierName: "Sumber Makmur",
  currencyCode: "IDR",
  orderDate: "2026-11-18",
  expectedDeliveryDate: "2026-11-28",
  deliveryDestination: "Gudang Pusat Jakarta",
  paymentTermsDays: 30,
  currentApprovedRevisionId: "rev-1",
  currentApprovedRevisionNumber: 1,
  latestRevisionNumber: 2,
  downstreamReferenceCount: 0,
  approvedOrderedBaseQuantity: 48,
  acceptedBaseQuantityTotal: 0,
  remainingOpenBaseQuantity: 48,
  closedByUserId: null,
  closedAtUtc: null,
  closureReason: null,
  cancelledByUserId: null,
  cancelledAtUtc: null,
  cancellationReason: null,
  createdByUserId: mockUser.userId,
  createdAtUtc: "2026-11-18T03:00:00Z",
  updatedAtUtc: "2026-11-18T04:00:00Z",
  version: 2,
  revisions: [
    {
      revisionId: "rev-2",
      revisionNumber: 2,
      supersedesRevisionId: "rev-1",
      status: "PENDING_APPROVAL",
      isFrozen: true,
      supplierPartnerId: "sup-1",
      supplierCodeSnapshot: "PT-SUP-001",
      supplierLegalNameSnapshot: "PT Sumber Makmur Kemasan",
      supplierNpwpSnapshot: "01.888.221.4-012.000",
      supplierPkpStatusSnapshot: "PKP",
      currencyCode: "IDR",
      orderDate: "2026-11-18",
      expectedDeliveryDate: "2026-11-28",
      deliveryDestination: "Gudang Pusat Jakarta",
      paymentTermsDays: 30,
      tolerancePolicyId: "tol-1",
      toleranceModeSnapshot: "EXACT_APPROVED_QTY_ONLY",
      changeReason: "Penyesuaian kuantitas ke 3 BOX",
      notes: null,
      subtotalAmount: 1440000,
      ppnTaxTotalAmount: 158400,
      withholdingTaxTotalAmount: 0,
      grandTotalAmount: 1598400,
      totalOrderedBaseQuantity: 72,
      snapshotHash: "11223344556677889900aabbccddeeff",
      createdByUserId: mockUser.userId,
      createdAtUtc: "2026-11-18T03:50:00Z",
      submittedByUserId: mockUser.userId,
      submittedAtUtc: "2026-11-18T03:55:00Z",
      decidedByUserId: null,
      decidedAtUtc: null,
      decisionReason: null,
      version: 2,
      lines: [
        {
          poLineId: "l-21",
          lineNumber: 1,
          lineageLineKey: "L001",
          itemId: "itm-1",
          itemCodeSnapshot: "ITM-PKG-001",
          itemNameSnapshot: "Karton Lipat Polos 40x30x25 cm",
          itemKindSnapshot: "STOCKED_GOOD",
          orderedUomId: "uom-box",
          orderedUomCodeSnapshot: "BOX",
          baseUomId: "uom-pcs",
          baseUomCodeSnapshot: "PCS",
          uomFactorToBaseSnapshot: 24,
          orderedQuantity: 3,
          orderedBaseQuantity: 72,
          unitPrice: 480000,
          lineSubtotalAmount: 1440000,
          taxCodeId: "tax-1112",
          taxCodeSnapshot: "PPN-12-DPP1112",
          taxFamilySnapshot: "PPN",
          taxRatePercentSnapshot: 11,
          taxableBaseAmount: 1320000,
          taxAmount: 158400,
          overdeliveryTolerancePercent: 0,
          overdeliveryToleranceBaseQty: 0,
          allowUnlimitedOverdelivery: false,
          lineDescription: null,
        },
      ],
      approval: {
        approvalId: "app-2",
        policyId: "pol-1",
        status: "PENDING",
        requestSummary: "PO-2026-00001 rev 2",
        requestedByUserId: mockUser.userId,
        requestedByEmail: mockUser.email,
        requestedAtUtc: "2026-11-18T03:55:00Z",
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
            lineageLineKey: "L001",
            changeKind: "MODIFIED",
            previousItemCode: "ITM-PKG-001",
            currentItemCode: "ITM-PKG-001",
            previousQuantity: 2,
            currentQuantity: 3,
            previousUomCode: "BOX",
            currentUomCode: "BOX",
            previousUnitPrice: 480000,
            currentUnitPrice: 480000,
            previousSubtotal: 960000,
            currentSubtotal: 1440000,
          },
        ],
        previousGrandTotal: 1065600,
        currentGrandTotal: 1598400,
        grandTotalDelta: 532800,
        previousOrderedBaseQty: 48,
        currentOrderedBaseQty: 72,
        orderedBaseQtyDelta: 24,
      },
    },
  ],
};

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/components/AppShell", () => ({
  useSessionUser: () => mockUser,
}));

vi.mock("@/lib/api/client", () => ({
  fetchEntityWorkspaceDetail: () => fetchDetailMock(),
  fetchProcurementReferenceData: async () => refDataPayload,
  fetchPurchaseOrders: async () => poListPayload,
  fetchPurchaseOrderDetail: async () => poDetailPayload,
  createPurchaseOrder: vi.fn(),
  updatePurchaseOrderDraftRevision: vi.fn(),
  createPurchaseOrderAmendment: vi.fn(),
  submitPurchaseOrderRevision: vi.fn(),
  decidePurchaseOrderRevision: vi.fn(),
  cancelPurchaseOrder: vi.fn(),
  shortClosePurchaseOrder: vi.fn(),
}));

describe("S05 Procurement & PO UI", () => {
  it("computes exact BigInt UoM base & PPN 11/12 totals and renders revision diff with self-approval SoD lock", async () => {
    currentPermissions = ["workspace.view", "procurement.read", "procurement.manage", "procurement.approve"];

    render(
      <LocaleProvider>
        <ProcurementPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("PO-2026-00001")).toBeInTheDocument();
    });

    // Open PO detail where submitter === currentUser -> must show self-approval four-eyes lock without approve button
    fireEvent.click(screen.getByRole("button", { name: /Pantau Status/ }));
    await waitFor(() => {
      expect(screen.getByTestId("po-self-approval-blocked")).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: /Setujui Revisi PO/ })).not.toBeInTheDocument();
    expect(screen.getByText(/Perbandingan Perubahan Revisi/)).toBeInTheDocument();

    // Switch to PO editor & check exact 11/12 DPP BigInt calculation
    fireEvent.click(screen.getByRole("tab", { name: /Buat & Edit Draf PO/ }));
    fireEvent.change(document.getElementById("po-line-qty-0") as HTMLInputElement, { target: { value: "3" } });
    fireEvent.change(document.getElementById("po-line-price-0") as HTMLInputElement, { target: { value: "480000" } });
    fireEvent.change(document.getElementById("po-line-tax-0") as HTMLSelectElement, { target: { value: "tax-1112" } });

    expect(screen.getByTestId("po-editor-totals")).toHaveTextContent("Rp 1.440.000");
    expect(screen.getByTestId("po-editor-totals")).toHaveTextContent("Rp 158.400");
    expect(screen.getByTestId("po-editor-totals")).toHaveTextContent("Rp 1.598.400");
    expect(screen.getByText(/72 PCS/)).toBeInTheDocument();
  });

  it("hides editor tab and all approval/lifecycle controls for read-only procurement.read actors", async () => {
    currentPermissions = ["workspace.view", "procurement.read"];

    render(
      <LocaleProvider>
        <ProcurementPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Mode baca saja/)).toBeInTheDocument();
    });

    expect(screen.queryByRole("tab", { name: /Buat & Edit Draf PO/ })).not.toBeInTheDocument();
    const inspectBtn = screen.getByRole("button", { name: /Lihat Rincian & Riwayat/ });
    fireEvent.click(inspectBtn);

    await waitFor(() => {
      expect(screen.getByText(/1122334455667788/)).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: /Setujui Revisi PO/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Tutup Sisa Komitmen/ })).not.toBeInTheDocument();
  });
});
