import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PayablesPage from "@/app/(shell)/w/[tenantId]/[entityId]/payables/page";
import InventoryPage from "@/app/(shell)/w/[tenantId]/[entityId]/inventory/page";
import { LocaleProvider, useLocale } from "@/lib/i18n/LocaleProvider";
import type {
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  InventoryOverviewResponse,
  ItemValuationLedgerResponse,
  PayablesOverviewResponse,
  SupplierBillDetailResponse,
  SupplierBillPreviewResponse,
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

let activePermissions = ["workspace.view", "ap.read", "ap.manage", "procurement.read", "receiving.read", "inventory.read"];

const previewMock = vi.fn();
const createBillMock = vi.fn();
const updateBillMock = vi.fn();
const billDetailMock = vi.fn();
const decideBillMock = vi.fn();

const overviewFixture: PayablesOverviewResponse = {
  bills: [
    {
      supplierBillId: "bill-1",
      draftCode: "BILL-DRF-0001",
      billNumber: "BILL-2026-00001",
      billType: "PO_THREE_WAY_MATCH",
      purchaseOrderId: "po-1",
      poNumber: "PO-2026-00004",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Mitra Baja Indonesia",
      supplierInvoiceNumber: "INV-VND-001",
      taxInvoiceNumber: "010.001-26.00000001",
      invoiceDate: "2026-11-10",
      dueDate: "2026-12-10",
      currencyCode: "IDR",
      declaredGrossAmount: 6105000,
      totalGrniClearAmount: 5000000,
      totalPriceVarianceAmount: 500000,
      totalInventoryAdjAmount: 200000,
      totalPnlVarianceAmount: 300000,
      ppnTaxTotal: 605000,
      withholdingTaxTotal: 110000,
      netPayableAmount: 5995000,
      status: "PENDING_APPROVAL",
      matchStatus: "APPROVAL_REQUIRED",
      paymentBlockStatus: "NONE",
      varianceApproved: false,
      postedJournalId: null,
      postedJournalNumber: null,
      submittedByEmail: "siti.rahayu@demo.athera.local",
      approvedByEmail: null,
      createdAtUtc: "2026-11-10T02:00:00Z",
      updatedAtUtc: "2026-11-10T02:05:00Z",
    },
  ],
  payables: [],
  eligibleAccrualLines: [
    {
      receiptAccrualLineId: "acc-line-1",
      receiptAccrualId: "acc-1",
      accrualNumber: "GRNI-2026-00004",
      purchaseOrderId: "po-1",
      poNumber: "PO-2026-00004",
      poRevisionId: "rev-1",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierName: "PT Mitra Baja Indonesia",
      supplierNpwp: "01.234.567.8-011.000",
      defaultPaymentTermsDays: 30,
      goodsReceiptId: "gr-1",
      receiptNumber: "GRN-2026-00004",
      receiptPostingDate: "2026-11-08",
      valuedAcceptanceLineId: "val-1",
      goodsReceiptLineId: "grl-1",
      poLineId: "pol-1",
      lineNumber: 1,
      itemId: "itm-valve",
      itemCode: "ITM-S09-VALVE-01",
      itemName: "Katup Industri PN16 50mm",
      baseUomId: "uom-pcs",
      baseUomCode: "PCS",
      orderedBaseQty: 100,
      acceptedBaseQty: 100,
      billedBaseQty: 0,
      remainingUnbilledBaseQty: 100,
      unitCostBase: 50000,
      poUnitPriceFunctional: 50000,
      accruedLineAmount: 5000000,
      remainingOpenLineAmount: 5000000,
      currentPoolOnHandQty: 40,
      currentPoolCarryingValue: 2000000,
      currentPoolMovingAverageCost: 50000,
      linePpnTaxCodeId: "tax-ppn-11",
    },
  ],
  taxCodes: [
    {
      taxCodeId: "tax-ppn-11",
      code: "PPN-IN-11",
      name: "PPN Masukan 11%",
      taxFamily: "PPN",
      direction: "INPUT",
      treatmentClass: "STANDARD_TAXABLE",
      nominalRatePercent: 11,
      taxableBaseRatioNumerator: 1,
      taxableBaseRatioDenominator: 1,
      requiresCounterpartyNpwp: false,
      effectiveFrom: "2026-01-01",
      effectiveTo: null,
      reviewStatus: "UNREVIEWED_CONFIGURABLE",
    },
    {
      taxCodeId: "tax-pph-2",
      code: "PPH23-SVC-2",
      name: "PPh 23 Jasa 2%",
      taxFamily: "PPH",
      direction: "WITHHOLDING",
      treatmentClass: "WITHHOLDING_DEDUCTION",
      nominalRatePercent: 2,
      taxableBaseRatioNumerator: 1,
      taxableBaseRatioDenominator: 1,
      requiresCounterpartyNpwp: true,
      effectiveFrom: "2026-01-01",
      effectiveTo: null,
      reviewStatus: "UNREVIEWED_CONFIGURABLE",
    },
  ],
  activeMatchPolicy: {
    policyId: "pol-1",
    code: "EXACT_MATCH_OD03_SAFE",
    name: "Exact Three-Way Match Only (OD-03 Safe Default)",
    reviewStatus: "UNREVIEWED_SAFE_DEFAULT",
    ppTolerancePercent: 0,
    ppToleranceAmount: 0,
    dqTolerancePercent: 0,
    dqToleranceBaseQty: 0,
    bdWriteOffLimitAmount: 0,
    bdWriteOffAccountId: null,
  },
  recentPriceAdjustments: [],
  openGrniBalanceIdr: 5000000,
  openPayableTotalIdr: 0,
  blockedPayableCount: 0,
  pendingApprovalBillCount: 1,
  od03Notice: "OD-03",
  od08Notice: "OD-08",
};

const positiveVariancePreviewFixture: SupplierBillPreviewResponse = {
  purchaseOrderId: "po-1",
  poNumber: "PO-2026-00004",
  supplierPartnerId: "sup-1",
  supplierCode: "PT-SUP-001",
  supplierLegalName: "PT Mitra Baja Indonesia",
  currencyCode: "IDR",
  invoiceDate: "2026-11-10",
  dqOutcome: "MATCHED",
  ppOutcome: "VARIANCE_APPROVAL_REQUIRED",
  bdOutcome: "MATCHED",
  overallMatchStatus: "APPROVAL_REQUIRED",
  isPostingBlocked: false,
  requiresApproval: true,
  linesNetSubtotal: 5500000,
  totalGrniClearAmount: 5000000,
  totalPriceVarianceAmount: 500000,
  totalInventoryAdjAmount: 200000,
  totalPnlVarianceAmount: 300000,
  declaredGrossAmount: 6105000,
  computedGrossTotal: 6105000,
  bdWriteOffAmount: 0,
  ppnTaxTotal: 605000,
  withholdingTaxTotal: 110000,
  netPayableAmount: 5995000,
  lines: [
    {
      lineNumber: 1,
      receiptAccrualLineId: "acc-line-1",
      poLineId: "pol-1",
      itemId: "itm-valve",
      itemCode: "ITM-S09-VALVE-01",
      itemName: "Katup Industri PN16 50mm",
      baseUomCode: "PCS",
      orderedBaseQty: 100,
      remainingUnbilledBaseQty: 100,
      billedBaseQuantity: 100,
      poUnitPriceFunctional: 50000,
      receiptUnitCostBase: 50000,
      invoiceUnitPriceBase: 55000,
      lineNetAmount: 5500000,
      grniClearAmount: 5000000,
      priceVarianceAmount: 500000,
      currentPoolOnHandQty: 40,
      currentPoolCarryingValue: 2000000,
      currentPoolMovingAvgCost: 50000,
      coveredBaseQuantity: 40,
      uncoveredBaseQuantity: 60,
      inventoryAdjustmentAmount: 200000,
      pnlVarianceAmount: 300000,
      projectedMovingAvgCost: 55000,
      ppnTaxCodeId: "tax-ppn-11",
      ppnTaxCodeSnapshot: "PPN-IN-11",
      ppnTaxAmount: 605000,
      withholdingTaxCodeId: "tax-pph-2",
      withholdingTaxCodeSnapshot: "PPH23-SVC-2",
      withholdingTaxAmount: 110000,
      dqOutcome: "MATCHED",
      ppOutcome: "VARIANCE_APPROVAL_REQUIRED",
      blockingReason: null,
    },
  ],
  journalPreview: [
    { side: "DEBIT", accountCode: "2110", accountName: "Hutang Belum Ditagih (GRNI)", amount: 5000000, description: "GRNI Clear" },
    { side: "DEBIT", accountCode: "1300", accountName: "Persediaan Barang Dagang", amount: 200000, description: "Covered Stock Inv Adj" },
    { side: "DEBIT", accountCode: "5200", accountName: "Selisih Harga Pembelian", amount: 300000, description: "Uncovered PPV" },
    { side: "DEBIT", accountCode: "1410", accountName: "PPN Masukan", amount: 605000, description: "PPN Masukan" },
    { side: "CREDIT", accountCode: "2220", accountName: "Hutang PPh 23", amount: 110000, description: "PPh 23" },
    { side: "CREDIT", accountCode: "2100", accountName: "Hutang Usaha", amount: 5995000, description: "Net AP" },
  ],
  blockingReasons: [],
  od03Notice: "OD-03",
};

const billDetailPendingFixture: SupplierBillDetailResponse = {
  bill: overviewFixture.bills[0]!,
  lines: [
    {
      supplierBillLineId: "bl-1",
      lineNumber: 1,
      receiptAccrualLineId: "acc-line-1",
      valuedAcceptanceLineId: "val-1",
      goodsReceiptLineId: "grl-1",
      poLineId: "pol-1",
      itemId: "itm-valve",
      itemCode: "ITM-S09-VALVE-01",
      itemName: "Katup Industri PN16 50mm",
      baseUomCode: "PCS",
      orderedBaseQty: 100,
      acceptedUnbilledQtySnapshot: 100,
      billedBaseQuantity: 100,
      poUnitPriceFunctional: 50000,
      invoiceUnitPriceBase: 55000,
      lineNetAmount: 5500000,
      grniClearAmount: 5000000,
      priceVarianceAmount: 500000,
      ppnTaxCodeId: "tax-ppn-11",
      ppnTaxCodeSnapshot: "PPN-IN-11",
      ppnTaxAmount: 605000,
      withholdingTaxCodeId: "tax-pph-2",
      withholdingTaxCodeSnapshot: "PPH23-SVC-2",
      withholdingTaxAmount: 110000,
      dqOutcome: "MATCHED",
      ppOutcome: "VARIANCE_APPROVAL_REQUIRED",
      lineNotes: null,
    },
  ],
  evaluations: [
    {
      evaluationId: "ev-1",
      evaluationStage: "SUBMIT",
      dqOutcome: "MATCHED",
      ppOutcome: "VARIANCE_APPROVAL_REQUIRED",
      bdOutcome: "MATCHED",
      overallMatchStatus: "APPROVAL_REQUIRED",
      isPostingBlocked: false,
      requiresApproval: true,
      requiresPaymentBlock: false,
      blockingReasons: [],
      evaluatedByEmail: "siti.rahayu@demo.athera.local",
      evaluatedAtUtc: "2026-11-10T02:05:00Z",
    },
  ],
  receiptMatches: [],
  priceAdjustments: [],
  taxDeterminations: [],
  payableItem: null,
  liveComparison: null,
  wasIdempotentReplay: false,
};

const invOverviewFixture: InventoryOverviewResponse = {
  positions: [
    {
      stockPositionId: "sp-valve",
      itemId: "itm-valve",
      itemCode: "ITM-S09-VALVE-01",
      itemName: "Katup Industri PN16 50mm",
      lotTrackingPolicy: "NONE",
      baseUomId: "uom-pcs",
      baseUomCode: "PCS",
      valuationGroupCode: "DEFAULT",
      currencyCode: "IDR",
      unrestrictedBaseQuantity: 40,
      qualityInspectionBaseQuantity: 0,
      blockedBaseQuantity: 0,
      reservedBaseQuantity: 0,
      totalValuedOnHandBaseQuantity: 40,
      availableBaseQuantity: 40,
      carryingValueAmount: 2200000,
      movingAverageUnitCost: 55000,
      poolStatus: "ACTIVE",
      lastEntrySequence: 3,
      locationCount: 1,
      activeLotCount: 0,
      pendingInspectionCount: 0,
      updatedAtUtc: "2026-11-10T03:00:00Z",
    },
  ],
  recentMovements: [
    {
      stockMovementId: "mov-iss-1",
      movementNumber: "MOV-2026-00004",
      movementType: "OPERATIONAL_ISSUE",
      sourceDocumentNumber: "ISS-JKT-DEMO-S09-01",
      postingDate: "2026-11-09",
      totalBaseQuantity: 60,
      totalValuationAmount: 3000000,
      lineCount: 1,
      createdByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-09T01:00:00Z",
    },
  ],
  openAccruals: [],
  locations: [],
  inspections: [],
  totalStockedItemsWithBalance: 1,
  totalCarryingValueIdr: 2200000,
  totalOpenGrniAccrualIdr: 0,
  restrictedStockItemCount: 0,
  od04Notice: "OD-04",
  od10Notice: "OD-10",
};

const invLedgerFixture: ItemValuationLedgerResponse = {
  itemId: "itm-valve",
  itemCode: "ITM-S09-VALVE-01",
  itemName: "Katup Industri PN16 50mm",
  baseUomCode: "PCS",
  currencyCode: "IDR",
  valuationGroupCode: "DEFAULT",
  poolStatus: "ACTIVE",
  currentValuedBaseQuantity: 40,
  currentCarryingValueAmount: 2200000,
  currentMovingAverageUnitCost: 55000,
  unrestrictedBaseQuantity: 40,
  qualityInspectionBaseQuantity: 0,
  blockedBaseQuantity: 0,
  availableBaseQuantity: 40,
  entries: [
    {
      valuationEntryId: "ve-1",
      poolSequenceNumber: 1,
      entryType: "RECEIPT_ACCEPTANCE",
      sourceDocumentType: "VALUED_ACCEPTANCE",
      sourceDocumentId: "va-1",
      postingDate: "2026-11-08",
      movementNumber: "MOV-2026-00003",
      sourceDocumentNumber: "ACC-2026-00003",
      quantityDelta: 100,
      valueDelta: 5000000,
      unitCostApplied: 50000,
      previousPoolBaseQuantity: 0,
      previousPoolCarryingValue: 0,
      previousMovingAverageCost: 0,
      newPoolBaseQuantity: 100,
      newPoolCarryingValue: 5000000,
      newMovingAverageCost: 50000,
      glJournalId: "jrn-1",
      journalNumber: "JRN-2026-00003",
      createdByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-08T01:00:00Z",
    },
    {
      valuationEntryId: "ve-2",
      poolSequenceNumber: 2,
      entryType: "OPERATIONAL_ISSUE",
      sourceDocumentType: "OPERATIONAL_ISSUE",
      sourceDocumentId: "iss-1",
      postingDate: "2026-11-09",
      movementNumber: "MOV-2026-00004",
      sourceDocumentNumber: "ISS-JKT-DEMO-S09-01",
      quantityDelta: -60,
      valueDelta: -3000000,
      unitCostApplied: 50000,
      previousPoolBaseQuantity: 100,
      previousPoolCarryingValue: 5000000,
      previousMovingAverageCost: 50000,
      newPoolBaseQuantity: 40,
      newPoolCarryingValue: 2000000,
      newMovingAverageCost: 50000,
      glJournalId: "jrn-2",
      journalNumber: "JRN-2026-00004",
      createdByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-09T01:00:00Z",
    },
    {
      valuationEntryId: "ve-3",
      poolSequenceNumber: 3,
      entryType: "INVOICE_PRICE_ADJUSTMENT",
      sourceDocumentType: "SUPPLIER_BILL",
      sourceDocumentId: "bill-1",
      postingDate: "2026-11-10",
      movementNumber: "—",
      sourceDocumentNumber: "BILL-2026-00001",
      quantityDelta: 0,
      valueDelta: 200000,
      unitCostApplied: 55000,
      previousPoolBaseQuantity: 40,
      previousPoolCarryingValue: 2000000,
      previousMovingAverageCost: 50000,
      newPoolBaseQuantity: 40,
      newPoolCarryingValue: 2200000,
      newMovingAverageCost: 55000,
      glJournalId: "jrn-3",
      journalNumber: "JRN-2026-00005",
      createdByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-10T01:00:00Z",
    },
  ],
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
  fetchPayablesOverview: async () => overviewFixture,
  fetchSupplierBillDetail: (...args: unknown[]) => billDetailMock(...args),
  previewSupplierBill: (...args: unknown[]) => previewMock(...args),
  createSupplierBill: (...args: unknown[]) => createBillMock(...args),
  updateSupplierBill: (...args: unknown[]) => updateBillMock(...args),
  submitSupplierBill: vi.fn(),
  decideSupplierBill: (...args: unknown[]) => decideBillMock(...args),
  postSupplierBill: vi.fn(),
  updateSupplierBillPaymentBlock: vi.fn(),
  updateInvoiceMatchPolicy: vi.fn(),
  fetchInventoryOverview: async () => invOverviewFixture,
  fetchItemValuationLedger: async () => invLedgerFixture,
  listInventoryWarehouses: async () => ({ warehouses: [], locations: [] }),
  getItemStockDetail: async () => null,
  postInventoryIssue: vi.fn(),
}));

function TestLocaleSwitcher() {
  const { locale, setLocale } = useLocale();
  return (
    <button type="button" data-testid="test-locale-btn" onClick={() => setLocale(locale === "id-ID" ? "en-US" : "id-ID")}>
      SwitchLocale
    </button>
  );
}

describe("S09 Supplier Invoice Three-Way Match, AP & Price Difference UI", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUser = {
      ...mockUser,
      email: "siti.rahayu@demo.athera.local",
      displayName: "Siti Rahayu (AP Officer)",
    };
    activePermissions = ["workspace.view", "ap.read", "ap.manage", "procurement.read", "receiving.read", "inventory.read"];
    previewMock.mockResolvedValue(positiveVariancePreviewFixture);
    billDetailMock.mockResolvedValue(billDetailPendingFixture);
  });

  it("renders live 3-way match preview with DEC-P2P-013 stock-coverage split, taxes, and locale reactivity", async () => {
    render(
      <LocaleProvider>
        <TestLocaleSwitcher />
        <PayablesPage />
      </LocaleProvider>,
    );

    const newBtn = await screen.findByRole("button", { name: /Buat Faktur Pemasok Baru/ });
    expect(screen.getByTestId("ap-od03-governance-banner").textContent).toContain("OD-03");

    // Verify dynamic locale switch translates governance banner immediately
    fireEvent.click(screen.getByTestId("test-locale-btn"));
    expect(screen.getByTestId("ap-od03-governance-banner").textContent).toContain("Automatic PP/DQ/BD tolerances = 0");
    fireEvent.click(screen.getByTestId("test-locale-btn"));

    // Open composer and preview positive price variance (100 billed, 40 on-hand -> Rp 200.000 Inv + Rp 300.000 PPV)
    fireEvent.click(newBtn);
    fireEvent.change(document.getElementById("ap-composer-vendor-inv")!, { target: { value: "INV-JKT-001" } });
    fireEvent.change(document.getElementById("ap-composer-unit-price")!, { target: { value: "55000" } });
    fireEvent.change(document.getElementById("ap-composer-pph")!, { target: { value: "tax-pph-2" } });
    fireEvent.change(document.getElementById("ap-composer-declared-gross")!, { target: { value: "6105000" } });

    fireEvent.click(document.getElementById("ap-preview-match-btn")!);

    const previewBox = await screen.findByTestId("ap-live-match-preview");
    expect(previewBox.textContent).toContain("200.000");
    expect(previewBox.textContent).toContain("300.000");
    expect(previewBox.textContent).toContain("5.995.000");
    expect(previewBox.textContent).toContain("Wajib Persetujuan Selisih");
  });

  it("blocks submit on DQ/BD POSTING_BLOCKED preview and enforces OD-02 SoD & read-only gating", async () => {
    // 1. Overbilling DQ POSTING_BLOCKED preview renders blocking badge + blocking reason
    previewMock.mockResolvedValueOnce({
      ...positiveVariancePreviewFixture,
      dqOutcome: "BLOCKED",
      ppOutcome: "MATCHED",
      bdOutcome: "BLOCKED",
      overallMatchStatus: "POSTING_BLOCKED",
      isPostingBlocked: true,
      requiresApproval: false,
      blockingReasons: ["dq_exceeds_unbilled_accepted_capacity: 110 > 100 PCS"],
    });

    mockUser = {
      ...mockUser,
      email: "siti.rahayu@demo.athera.local",
    };
    activePermissions = ["workspace.view", "ap.read", "ap.manage", "ap.approve", "approval.decide"];

    render(
      <LocaleProvider>
        <PayablesPage />
      </LocaleProvider>,
    );

    fireEvent.click(await screen.findByRole("button", { name: /Buat Faktur Pemasok Baru/ }));
    fireEvent.change(document.getElementById("ap-composer-qty")!, { target: { value: "110" } });
    fireEvent.click(document.getElementById("ap-preview-match-btn")!);

    const blockedPreview = await screen.findByTestId("ap-live-match-preview");
    expect(blockedPreview.textContent).toContain("Diblokir Sebelum Post");
    expect(blockedPreview.textContent).toContain("dq_exceeds_unbilled_accepted_capacity");

    // 2. Self-approval SoD notice when submittedByEmail === mockUser.email
    const openDetailBtn = await screen.findByRole("button", { name: /Buka Detail|Detail Terbuka/ });
    fireEvent.click(openDetailBtn);

    // Since submittedByEmail === mockUser.email, four-eyes SoD notice blocks self-approval
    const sodNotice = await screen.findByTestId("ap-sod-self-approval-notice");
    expect(sodNotice.textContent).toContain("OD-02");
    expect(document.getElementById("ap-approve-bill-btn")).toBeNull();
  });

  it("shows read-only banner when only ap.read is granted and allows independent approver with variance checkbox", async () => {
    activePermissions = ["workspace.view", "ap.read"];
    const view1 = render(
      <LocaleProvider>
        <PayablesPage />
      </LocaleProvider>,
    );

    await screen.findByTestId("ap-readonly-banner");
    expect(document.getElementById("ap-new-bill-btn")).toBeNull();
    view1.unmount();

    // Independent approver (Budi) sees Approve button locked until ApprovePriceVariance checkbox is checked
    mockUser = { ...mockUser, email: "budi.santoso@demo.athera.local" };
    activePermissions = ["workspace.view", "ap.read", "ap.approve", "ap.post", "approval.decide"];
    render(
      <LocaleProvider>
        <PayablesPage />
      </LocaleProvider>,
    );

    fireEvent.click(await screen.findByRole("button", { name: /Buka Detail|Detail Terbuka/ }));
    const checkbox = await waitFor(() => {
      const el = document.getElementById("ap-approve-variance-checkbox") as HTMLInputElement | null;
      expect(el).not.toBeNull();
      return el!;
    });
    const approveBtn = document.getElementById("ap-approve-bill-btn") as HTMLButtonElement;
    expect(checkbox.checked).toBe(false);
    expect(approveBtn.disabled).toBe(true);

    fireEvent.click(checkbox);
    expect(approveBtn.disabled).toBe(false);
  });

  it("displays blocking reasons and supports Edit Draft remediation via updateSupplierBill on blocked DRAFT", async () => {
    const blockedDraftDetail: SupplierBillDetailResponse = {
      ...billDetailPendingFixture,
      bill: {
        ...billDetailPendingFixture.bill,
        billNumber: null,
        status: "DRAFT",
        matchStatus: "POSTING_BLOCKED",
        submittedByEmail: null,
      },
      evaluations: [
        {
          ...billDetailPendingFixture.evaluations[0]!,
          evaluationStage: "DRAFT_PREVIEW",
          dqOutcome: "BLOCKED",
          overallMatchStatus: "POSTING_BLOCKED",
          isPostingBlocked: true,
          blockingReasons: ["dq_exceeds_unbilled_accepted_capacity: 110 > 100 PCS"],
        },
      ],
    };
    billDetailMock.mockResolvedValueOnce(blockedDraftDetail);
    updateBillMock.mockResolvedValueOnce(billDetailPendingFixture);

    render(
      <LocaleProvider>
        <PayablesPage />
      </LocaleProvider>,
    );

    fireEvent.click(await screen.findByRole("button", { name: /Buka Detail|Detail Terbuka/ }));
    const blockedAlert = await screen.findByTestId("ap-blocking-reasons-alert");
    expect(blockedAlert.textContent).toContain("dq_exceeds_unbilled_accepted_capacity");
    expect((document.getElementById("ap-submit-bill-btn") as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click(document.getElementById("ap-edit-draft-btn")!);
    expect(document.getElementById("ap-bill-composer")).not.toBeNull();
    fireEvent.change(document.getElementById("ap-composer-qty")!, { target: { value: "100" } });
    fireEvent.click(document.getElementById("ap-save-draft-btn")!);

    await waitFor(() => expect(updateBillMock).toHaveBeenCalledTimes(1));
    expect(updateBillMock.mock.calls[0]?.[2]).toBe("bill-1");
  });

  it("renders signed OPERATIONAL_ISSUE (-60) and INVOICE_PRICE_ADJUSTMENT (+200.000) in movements and ledger", async () => {
    activePermissions = ["workspace.view", "inventory.read"];
    render(
      <LocaleProvider>
        <InventoryPage />
      </LocaleProvider>,
    );

    await screen.findByText("ITM-S09-VALVE-01");
    expect(screen.getByText(/Pengeluaran Stok Operasional/)).toBeDefined();
    expect(screen.getByText(/Penyesuaian Harga Faktur/)).toBeDefined();
    expect(screen.getByText(/-60 @/)).toBeDefined();
    expect(screen.queryByText(/\+-60/)).toBeNull();

    // Switch to Recent Movements tab and verify OPERATIONAL_ISSUE renders -60 (never +60)
    fireEvent.click(document.getElementById("inv-tab-movements")!);
    expect(screen.getByText("MOV-2026-00004")).toBeDefined();
    expect(screen.getByText("-60")).toBeDefined();
    expect(screen.queryByText(/\+60/)).toBeNull();
  });
});
