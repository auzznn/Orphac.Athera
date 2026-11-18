import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ReturnsPage from "@/app/(shell)/w/[tenantId]/[entityId]/returns/page";
import InventoryPage from "@/app/(shell)/w/[tenantId]/[entityId]/inventory/page";
import PayablesPage from "@/app/(shell)/w/[tenantId]/[entityId]/payables/page";
import { LocaleProvider, useLocale } from "@/lib/i18n/LocaleProvider";
import type {
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  GoodsReceiptDetailDto,
  InventoryOverviewResponse,
  PayablesOverviewResponse,
  ReturnsOverviewResponse,
  SupplierReturnDetailResponse,
  SupplierReturnPreviewResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };

let mockUser: CurrentUserResponse = {
  userId: "11111111-1111-1111-1111-111111111104",
  email: "siti.rahayu@demo.athera.local",
  displayName: "Siti Rahayu (AP Officer)",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-10T01:00:00Z",
  workspaceAccessStatus: "ACTIVE",
  authorizedWorkspaceCount: 1,
  mfaEnabled: false,
};

let activePermissions = [
  "workspace.view",
  "procurement.read",
  "procurement.manage",
  "receiving.read",
  "receiving.manage",
  "inventory.read",
  "inventory.manage",
  "ap.read",
  "ap.manage",
  "approval.decide",
];

const previewMock = vi.fn();
const createDraftMock = vi.fn();
const updateDraftMock = vi.fn();
const detailMock = vi.fn();
const submitMock = vi.fn();
const decideMock = vi.fn();
const executeMock = vi.fn();
const receiveReplacementMock = vi.fn();

const returnsOverviewFixture: ReturnsOverviewResponse = {
  pendingApprovalCount: 1,
  openReplacementCount: 1,
  openReplacementUnitsTotal: 2,
  unbilledGrniReversedTotalIdr: 600000,
  supplierCreditNotesTotalIdr: 166500,
  od05Notice: "OD-05 notice",
  od07Notice: "OD-07 notice",
  matrixRules: [
    {
      matrixCode: "M1",
      returnCaseType: "UNVALUED_DOCK_REJECTION",
      resolutionMode: "REPLACEMENT_ONLY",
      precondition: "Rejected at Gate 1 dock check",
      accountingAndStockEffect: "Zero GL; issues RPA replacement entitlement",
      disposition: "SUPPORTED",
    },
    {
      matrixCode: "M2",
      returnCaseType: "VALUED_UNBILLED_RETURN",
      resolutionMode: "CLOSE_WITHOUT_REPLACEMENT",
      precondition: "Accepted unbilled GRNI & open period",
      accountingAndStockEffect: "Dr 2110 GRNI / Cr 1300 Inventory",
      disposition: "SUPPORTED",
    },
    {
      matrixCode: "M3",
      returnCaseType: "VALUED_BILLED_RETURN",
      resolutionMode: "AP_CREDIT_NOTE",
      precondition: "Unpaid POSTED bill, zero WHT, Nota Retur if vatable",
      accountingAndStockEffect: "Dr 2100 AP / Cr 1300 Inv / Cr 1410 PPN",
      disposition: "SUPPORTED",
    },
    {
      matrixCode: "M4",
      returnCaseType: "VALUED_BILLED_RETURN",
      resolutionMode: "REPLACEMENT_ONLY",
      precondition: "After bill POSTED",
      accountingAndStockEffect: "Blocked by OD-05 (M4)",
      disposition: "BLOCKED_POLICY_OD05",
    },
  ],
  queuedUnvaluedRejections: [
    {
      goodsReceiptLineId: "grl-m1",
      goodsReceiptId: "gr-m1",
      receiptNumber: "GRN-2026-00010",
      receivedDate: "2026-11-10",
      purchaseOrderId: "po-seal",
      poNumber: "PO-2026-00010",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Mitra Baja Indonesia",
      poLineId: "pol-seal",
      itemId: "itm-seal",
      itemCode: "ITM-S10-SEAL-01",
      itemName: "Seal Silikon Hidrolik Industrial",
      baseUomCode: "PCS",
      dockRejectedBaseQuantity: 6,
      alreadyReturnedQuantity: 0,
      eligibleQuantity: 6,
      authorizedUnitCostBase: 75000,
      discrepancyReasonCode: "WET_OR_DEFECTIVE_GOODS",
    },
  ],
  queuedValuedUnbilledLines: [
    {
      receiptAccrualLineId: "acc-m2",
      receiptAccrualId: "acc-hdr-m2",
      accrualNumber: "GRNI-2026-00011",
      valuedAcceptanceLineId: "val-m2",
      goodsReceiptLineId: "grl-m2",
      goodsReceiptId: "gr-m2",
      receiptNumber: "GRN-2026-00011",
      accrualPostingDate: "2026-11-10",
      purchaseOrderId: "po-gasket",
      poNumber: "PO-2026-00011",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Mitra Baja Indonesia",
      itemId: "itm-gasket",
      itemCode: "ITM-S10-GASKET-02",
      itemName: "Gasket PTFE Flange 4 Inch",
      baseUomCode: "PCS",
      remainingUnbilledBaseQuantity: 30,
      eligibleQuantity: 30,
      receiptUnitCostBase: 120000,
      poolMovingAverageCost: 120000,
      poolValuedBaseQuantity: 30,
      stockLocations: [
        {
          locationId: "loc-1",
          locationCode: "WH-MAIN",
          locationName: "Gudang Utama",
          zoneType: "STORAGE",
          stockLotId: null,
          lotNumber: null,
          stockStatus: "UNRESTRICTED",
          eligibleQuantity: 30,
        },
      ],
    },
  ],
  queuedValuedBilledLines: [
    {
      supplierBillLineId: "sbl-1",
      supplierBillId: "bill-m3",
      billNumber: "BILL-2026-00010",
      supplierInvoiceNumber: "INV-JKT-S10-M3-01",
      payableItemId: "pay-1",
      payableNumber: "AP-2026-00010",
      payablePaidAmount: 0,
      payableRemainingOpenAmount: 1165500,
      goodsReceiptLineId: "grl-m1",
      goodsReceiptId: "gr-m1",
      receiptNumber: "GRN-2026-00010",
      billPostingDate: "2026-11-10",
      purchaseOrderId: "po-seal",
      poNumber: "PO-2026-00010",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Mitra Baja Indonesia",
      itemId: "itm-seal",
      itemCode: "ITM-S10-SEAL-01",
      itemName: "Seal Silikon Hidrolik Industrial",
      baseUomCode: "PCS",
      billedBaseQuantity: 14,
      priorCreditedQuantity: 0,
      remainingCreditableQuantity: 14,
      invoiceUnitPriceBase: 75000,
      ppnTaxAmount: 115500,
      withholdingTaxAmount: 0,
      ppnTaxCodeSnapshot: "PPN-IN-11",
      poolMovingAverageCost: 75000,
      poolValuedBaseQuantity: 14,
      matrixStatusCode: "M3",
      matrixBlockReason: null,
      stockLocations: [
        {
          locationId: "loc-1",
          locationCode: "WH-MAIN",
          locationName: "Gudang Utama",
          zoneType: "STORAGE",
          stockLotId: null,
          lotNumber: null,
          stockStatus: "UNRESTRICTED",
          eligibleQuantity: 14,
        },
      ],
    },
    {
      supplierBillLineId: "sbl-m5-paid",
      supplierBillId: "bill-m5",
      billNumber: "BILL-2026-00009",
      supplierInvoiceNumber: "INV-PAID-M5",
      payableItemId: "pay-2",
      payableNumber: "AP-2026-00009",
      payablePaidAmount: 500000,
      payableRemainingOpenAmount: 0,
      goodsReceiptLineId: "grl-m5",
      goodsReceiptId: "gr-m5",
      receiptNumber: "GRN-2026-00009",
      billPostingDate: "2026-11-09",
      purchaseOrderId: "po-5",
      poNumber: "PO-2026-00009",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Mitra Baja Indonesia",
      itemId: "itm-seal",
      itemCode: "ITM-S10-SEAL-01",
      itemName: "Seal Silikon Hidrolik Industrial",
      baseUomCode: "PCS",
      billedBaseQuantity: 5,
      priorCreditedQuantity: 0,
      remainingCreditableQuantity: 0,
      invoiceUnitPriceBase: 75000,
      ppnTaxAmount: 41250,
      withholdingTaxAmount: 0,
      ppnTaxCodeSnapshot: "PPN-IN-11",
      poolMovingAverageCost: 75000,
      poolValuedBaseQuantity: 14,
      matrixStatusCode: "M5",
      matrixBlockReason: "od05_paid_bill_return_policy_blocked",
      stockLocations: [],
    },
  ],
  returns: [
    {
      supplierReturnId: "ret-1",
      draftCode: "SRN-DRF-0001",
      returnNumber: null,
      returnCaseType: "VALUED_BILLED_RETURN",
      resolutionMode: "AP_CREDIT_NOTE",
      matrixDisposition: "SUPPORTED",
      matrixBlockingCode: null,
      matrixBlockingReason: null,
      purchaseOrderId: "po-seal",
      poNumberSnapshot: "PO-2026-00010",
      goodsReceiptId: "gr-m1",
      receiptNumberSnapshot: "GRN-2026-00010",
      receiptAccrualId: null,
      supplierBillId: "bill-m3",
      billNumberSnapshot: "BILL-2026-00010",
      supplierCodeSnapshot: "PT-SUP-001",
      supplierLegalNameSnapshot: "PT Mitra Baja Indonesia",
      returnDate: "2026-11-10",
      reasonCode: "DAMAGED_OR_DEFECTIVE",
      taxReturnNoteNumber: "NR-2026-0001",
      status: "PENDING_APPROVAL",
      totalReturnedBaseQuantity: 2,
      totalInventoryCreditAmount: 150000,
      totalGrniReversalAmount: 0,
      totalCreditSubtotalAmount: 150000,
      totalPpnReversalAmount: 16500,
      totalCreditNoteAmount: 166500,
      totalPpvDifferenceAmount: 0,
      stockMovementId: null,
      movementNumber: null,
      glJournalId: null,
      journalNumber: null,
      supplierCreditId: null,
      creditNumber: null,
      submittedByEmail: "siti.rahayu@demo.athera.local",
      approvedByEmail: null,
      createdAtUtc: "2026-11-10T02:00:00Z",
      updatedAtUtc: "2026-11-10T02:01:00Z",
    },
  ],
  replacementAuthorizations: [
    {
      replacementAuthorizationId: "rpa-1",
      authorizationNumber: "RPA-2026-00001",
      supplierReturnId: "ret-m1",
      returnNumber: "SRN-2026-00001",
      sourceCaseType: "UNVALUED_DOCK_REJECTION",
      purchaseOrderId: "po-seal",
      poNumber: "PO-2026-00010",
      itemId: "itm-seal",
      itemCode: "ITM-S10-SEAL-01",
      itemName: "Seal Silikon Hidrolik Industrial",
      baseUomId: "uom-pcs",
      baseUomCode: "PCS",
      authorizedBaseQuantity: 2,
      deliveredBaseQuantity: 0,
      acceptedBaseQuantity: 0,
      remainingBaseQuantity: 2,
      commercialIncrementalUnitPrice: 0,
      authorizedInventoryUnitCostBase: 75000,
      status: "ACTIVE",
      createdAtUtc: "2026-11-10T01:00:00Z",
      updatedAtUtc: "2026-11-10T01:00:00Z",
    },
  ],
  supplierCredits: [
    {
      supplierCreditId: "scn-1",
      creditNumber: "SCN-2026-00001",
      supplierReturnId: "ret-prev",
      returnNumber: "SRN-2026-00002",
      supplierBillId: "bill-m3",
      billNumber: "BILL-2026-00010",
      payableItemId: "pay-1",
      payableNumber: "AP-2026-00010",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Mitra Baja Indonesia",
      supplierNpwpSnapshot: "01.234.567.8-011.000",
      taxReturnNoteNumber: "NR-2026-0001",
      postingDate: "2026-11-10",
      currencyCode: "IDR",
      subtotalCreditAmount: 150000,
      ppnReversalAmount: 16500,
      totalCreditAmount: 166500,
      inventoryCreditAmount: 150000,
      ppvDifferenceAmount: 0,
      glJournalId: "jrn-10",
      journalNumber: "JRN-2026-00010",
      stockMovementId: "mov-10",
      movementNumber: "MOV-2026-00010",
      createdAtUtc: "2026-11-10T03:00:00Z",
    },
  ],
};

const m4BlockedPreviewFixture: SupplierReturnPreviewResponse = {
  matrixCode: "M4",
  disposition: "BLOCKED_POLICY_OD05",
  blockingCode: "od05_billed_replacement_policy_blocked",
  blockingReason: "Replacement not permitted once supplier bill is posted.",
  requiresPeriodOpen: true,
  isPeriodOpen: true,
  requiresNotaRetur: false,
  totalReturnedBaseQuantity: 2,
  totalInventoryCreditAmount: 0,
  totalGrniReversalAmount: 0,
  totalCreditSubtotalAmount: 0,
  totalPpnReversalAmount: 0,
  totalCreditNoteAmount: 0,
  totalPpvDifferenceAmount: 0,
  lines: [],
  journalPreview: [],
  od05Notice: "OD-05",
  od07Notice: "OD-07",
};

const returnDetailDraftFixture: SupplierReturnDetailResponse = {
  returnHeader: {
    ...returnsOverviewFixture.returns[0]!,
    status: "DRAFT",
    submittedByEmail: null,
  },
  lines: [
    {
      supplierReturnLineId: "retl-1",
      lineNumber: 1,
      goodsReceiptLineId: "grl-m1",
      valuedAcceptanceLineId: null,
      receiptAccrualLineId: null,
      supplierBillLineId: "sbl-1",
      poLineId: "pol-seal",
      itemId: "itm-seal",
      itemCode: "ITM-S10-SEAL-01",
      itemName: "Seal Silikon Hidrolik Industrial",
      baseUomCode: "PCS",
      sourceLocationId: "loc-1",
      sourceLocationCode: "WH-MAIN",
      sourceStockLotId: null,
      sourceLotNumber: null,
      sourceStockStatus: "UNRESTRICTED",
      returnedBaseQuantity: 2,
      authorizedUnitCostBase: 75000,
      inventoryCreditAmount: 150000,
      grniReversalAmount: 0,
      creditSubtotalAmount: 150000,
      ppnReversalAmount: 16500,
      totalCreditAmount: 166500,
      ppvDifferenceAmount: 0,
      lineNotes: null,
    },
  ],
  replacementAuthorizations: [],
  supplierCredit: null,
  taxDeterminations: [],
  liveEvaluation: {
    ...m4BlockedPreviewFixture,
    matrixCode: "M3",
    disposition: "SUPPORTED",
    blockingCode: null,
    blockingReason: null,
    requiresNotaRetur: true,
    totalInventoryCreditAmount: 150000,
    totalCreditSubtotalAmount: 150000,
    totalPpnReversalAmount: 16500,
    totalCreditNoteAmount: 166500,
    journalPreview: [
      { side: "DEBIT", accountCode: "2100", accountName: "Hutang Usaha (AP)", amount: 166500, description: "AP Credit" },
      { side: "CREDIT", accountCode: "1300", accountName: "Persediaan", amount: 150000, description: "Return Outbound" },
      { side: "CREDIT", accountCode: "1410", accountName: "PPN Masukan", amount: 16500, description: "Nota Retur" },
    ],
  },
  wasIdempotentReplay: false,
};

const invReturnMovementFixture: InventoryOverviewResponse = {
  positions: [],
  recentMovements: [
    {
      stockMovementId: "mov-ret-1",
      movementNumber: "MOV-2026-00010",
      movementType: "PURCHASE_RETURN_OUTBOUND",
      sourceDocumentNumber: "SRN-2026-00002",
      postingDate: "2026-11-10",
      totalBaseQuantity: 5,
      totalValuationAmount: 600000,
      lineCount: 1,
      createdByEmail: "rina.utami@demo.athera.local",
      createdAtUtc: "2026-11-10T04:00:00Z",
    },
  ],
  openAccruals: [],
  locations: [],
  inspections: [],
  totalStockedItemsWithBalance: 0,
  totalCarryingValueIdr: 0,
  totalOpenGrniAccrualIdr: 0,
  restrictedStockItemCount: 0,
  od04Notice: "OD-04",
  od10Notice: "OD-10",
};

const payablesCreditedFixture: PayablesOverviewResponse = {
  bills: [],
  payables: [
    {
      payableItemId: "pay-1",
      payableNumber: "AP-2026-00010",
      supplierBillId: "bill-m3",
      billNumber: "BILL-2026-00010",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Mitra Baja Indonesia",
      supplierInvoiceNumber: "INV-JKT-S10-M3-01",
      glJournalId: "jrn-9",
      glJournalNumber: "JRN-2026-00009",
      postingDate: "2026-11-10",
      dueDate: "2026-12-10",
      currencyCode: "IDR",
      grossBillAmount: 1165500,
      withholdingTaxAmount: 0,
      netPayableAmount: 1165500,
      paidAmount: 0,
      creditedAmount: 166500,
      remainingOpenAmount: 999000,
      status: "OPEN",
      paymentBlockStatus: "NONE",
      paymentBlockReason: null,
    },
  ],
  eligibleAccrualLines: [],
  taxCodes: [],
  activeMatchPolicy: {
    policyId: "pol-1",
    code: "EXACT_MATCH_OD03_SAFE",
    name: "OD-03 Safe",
    reviewStatus: "UNREVIEWED_SAFE_DEFAULT",
    ppTolerancePercent: 0,
    ppToleranceAmount: 0,
    dqTolerancePercent: 0,
    dqToleranceBaseQty: 0,
    bdWriteOffLimitAmount: 0,
    bdWriteOffAccountId: null,
  },
  recentPriceAdjustments: [],
  openGrniBalanceIdr: 0,
  openPayableTotalIdr: 999000,
  blockedPayableCount: 0,
  pendingApprovalBillCount: 0,
  od03Notice: "OD-03",
  od08Notice: "OD-08",
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

vi.mock("@/components/AppShell", () => ({
  useSessionUser: () => mockUser,
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
    serverTimestampUtc: "2026-11-10T00:00:00Z",
  }),
  fetchReturnsOverview: async () => returnsOverviewFixture,
  fetchSupplierReturnDetail: (...args: unknown[]) => detailMock(...args),
  previewSupplierReturn: (...args: unknown[]) => previewMock(...args),
  createSupplierReturnDraft: (...args: unknown[]) => createDraftMock(...args),
  updateSupplierReturnDraft: (...args: unknown[]) => updateDraftMock(...args),
  submitSupplierReturn: (...args: unknown[]) => submitMock(...args),
  decideSupplierReturn: (...args: unknown[]) => decideMock(...args),
  executeSupplierReturn: (...args: unknown[]) => executeMock(...args),
  receiveLinkedReplacement: (...args: unknown[]) => receiveReplacementMock(...args),
  fetchInventoryOverview: async () => invReturnMovementFixture,
  fetchItemValuationLedger: async () => null,
  listInventoryWarehouses: async () => ({ warehouses: [], locations: [] }),
  getItemStockDetail: async () => null,
  postInventoryIssue: vi.fn(),
  fetchPayablesOverview: async () => payablesCreditedFixture,
  fetchSupplierBillDetail: vi.fn(),
  previewSupplierBill: vi.fn(),
  createSupplierBill: vi.fn(),
  updateSupplierBill: vi.fn(),
  submitSupplierBill: vi.fn(),
  decideSupplierBill: vi.fn(),
  postSupplierBill: vi.fn(),
  updateSupplierBillPaymentBlock: vi.fn(),
  updateInvoiceMatchPolicy: vi.fn(),
}));

function TestLocaleSwitcher() {
  const { locale, setLocale } = useLocale();
  return (
    <button type="button" data-testid="test-locale-btn" onClick={() => setLocale(locale === "id-ID" ? "en-US" : "id-ID")}>
      SwitchLocale
    </button>
  );
}

describe("S10 Supplier Returns, Replacements & Credits UI", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    activePermissions = [
      "workspace.view",
      "procurement.read",
      "procurement.manage",
      "receiving.read",
      "receiving.manage",
      "inventory.read",
      "inventory.manage",
      "ap.read",
      "ap.manage",
      "approval.decide",
    ];
    mockUser = { ...mockUser, email: "siti.rahayu@demo.athera.local", preferredLocale: "id-ID" };
    previewMock.mockResolvedValue(m4BlockedPreviewFixture);
    createDraftMock.mockResolvedValue(returnDetailDraftFixture);
    detailMock.mockResolvedValue(returnDetailDraftFixture);
    receiveReplacementMock.mockResolvedValue({
      receiptNumber: "GRN-2026-00012",
      replacementAuthorizationNumber: "RPA-2026-00001",
    } as Partial<GoodsReceiptDetailDto>);
  });

  it("renders queued M1/M2/M3 sources, toggles OD-05/OD-07 matrix, previews M4 block, and creates credit return draft", async () => {
    render(
      <LocaleProvider>
        <TestLocaleSwitcher />
        <ReturnsPage />
      </LocaleProvider>,
    );

    await screen.findByTestId("ret-governance-banner");
    expect(screen.getByText("GRN-2026-00010")).toBeDefined();
    expect(screen.getByText("GRNI-2026-00011")).toBeDefined();
    expect(screen.getByText("BILL-2026-00010")).toBeDefined();
    expect(screen.getByText(/OD-05 \(M5\)/)).toBeDefined();

    // Toggle policy matrix M1-M7
    fireEvent.click(document.getElementById("ret-matrix-toggle")!);
    expect(await screen.findByText("M4")).toBeDefined();

    // Open composer from M3 billed line, test M4 REPLACEMENT_ONLY block banner
    fireEvent.click(document.getElementById("ret-prepare-m3-sbl-1")!);
    expect(document.getElementById("ret-composer")).not.toBeNull();
    fireEvent.change(document.getElementById("ret-composer-resolution")!, {
      target: { value: "REPLACEMENT_ONLY" },
    });
    fireEvent.click(document.getElementById("ret-preview-btn")!);

    const blockBanner = await screen.findByTestId("ret-preview-blocked-banner");
    expect(blockBanner.textContent).toContain("OD-05 (M4)");
    expect(document.querySelector('[data-testid="ret-live-preview"] table')).toBeNull();

    // Switch back to AP_CREDIT_NOTE, fill Nota Retur and save draft
    fireEvent.change(document.getElementById("ret-composer-resolution")!, {
      target: { value: "AP_CREDIT_NOTE" },
    });
    fireEvent.change(document.getElementById("ret-composer-qty")!, { target: { value: "2" } });
    fireEvent.change(document.getElementById("ret-composer-nota-retur")!, { target: { value: "NR-2026-0001" } });
    fireEvent.click(document.getElementById("ret-save-draft-btn")!);

    await waitFor(() => expect(createDraftMock).toHaveBeenCalledTimes(1));
    expect(document.getElementById("ret-detail-sheet")).not.toBeNull();
  });

  it("handles Replacement RPA receiving, Supplier Credits ledger, and NUSA-SBY read-only enforcement", async () => {
    const view = render(
      <LocaleProvider>
        <ReturnsPage />
      </LocaleProvider>,
    );

    await screen.findByTestId("ret-governance-banner");
    fireEvent.click(document.getElementById("ret-tab-replacements")!);
    expect(screen.getByText("RPA-2026-00001")).toBeDefined();
    fireEvent.click(document.getElementById("ret-receive-rpa-rpa-1")!);
    expect(document.getElementById("ret-replacement-modal")).not.toBeNull();
    fireEvent.click(document.getElementById("ret-rpa-submit-btn")!);

    const successBanner = await screen.findByTestId("ret-replacement-success");
    expect(successBanner.textContent).toContain("GRN-2026-00012");
    expect(successBanner.textContent).toContain("RPA-2026-00001");

    fireEvent.click(document.getElementById("ret-tab-credits")!);
    expect(screen.getByText("SCN-2026-00001")).toBeDefined();
    view.unmount();

    // Read-only grant hides all mutation triggers
    activePermissions = ["workspace.view", "procurement.read", "receiving.read", "inventory.read", "ap.read"];
    render(
      <LocaleProvider>
        <ReturnsPage />
      </LocaleProvider>,
    );
    await screen.findByTestId("returns-readonly-banner");
    expect(document.getElementById("ret-prepare-m1-grl-m1")).toBeNull();
    expect(document.getElementById("ret-prepare-m2-acc-m2")).toBeNull();
    expect(document.getElementById("ret-prepare-m3-sbl-1")).toBeNull();
  });

  it("renders signed PURCHASE_RETURN_OUTBOUND (-5) on Inventory and Credited amount on Payables", async () => {
    const invView = render(
      <LocaleProvider>
        <InventoryPage />
      </LocaleProvider>,
    );
    fireEvent.click(await screen.findByRole("tab", { name: /Pergerakan Stok|Stock Movements/ }));
    expect(screen.getByText("MOV-2026-00010")).toBeDefined();
    expect(screen.getByText("-5")).toBeDefined();
    expect(screen.queryByText(/\+5/)).toBeNull();
    invView.unmount();

    render(
      <LocaleProvider>
        <PayablesPage />
      </LocaleProvider>,
    );
    fireEvent.click(await screen.findByRole("tab", { name: /Sub-Buku Besar AP|AP Subledger/ }));
    expect(screen.getByText(/Dikreditkan:/)).toBeDefined();
    expect(screen.getByText(/166\.500/)).toBeDefined();
  });
});
