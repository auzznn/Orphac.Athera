import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PaymentsPage from "@/app/(shell)/w/[tenantId]/[entityId]/payments/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  BankPaymentMatchDto,
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  PaymentsOverviewResponse,
  SupplierPaymentDetailResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };

let mockUser: CurrentUserResponse = {
  userId: "11111111-1111-1111-1111-111111111104",
  email: "siti.rahayu@demo.athera.local",
  displayName: "Siti Rahayu (AP Clerk)",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-15T01:00:00Z",
  workspaceAccessStatus: "ACTIVE",
  authorizedWorkspaceCount: 1,
  mfaEnabled: false,
};

let activePermissions = [
  "workspace.view",
  "ap.read",
  "ap.manage",
  "finance.read",
];

const overviewMock = vi.fn();
const detailMock = vi.fn();
const createProposalMock = vi.fn();
const simulateFileMock = vi.fn();
const releaseMock = vi.fn();
const reconcileMock = vi.fn();

const overviewFixture: PaymentsOverviewResponse = {
  od06PolicyNotice: "OD-06 safe defaults active",
  bankAccounts: [
    {
      bankAccountId: "bank-1",
      accountCode: "BCA-IDR-001",
      bankName: "PT Bank Central Asia Tbk",
      branchName: "KCU Sudirman",
      accountNumber: "035-9988-771",
      accountHolderName: "PT Nusantara Distribusi Jakarta",
      currencyCode: "IDR",
      glAccountId: "gl-1110",
      glAccountCode: "1110",
      glAccountName: "Kas & Bank Operasional",
      clearingGlAccountId: "gl-1120",
      clearingGlAccountCode: "1120",
      clearingGlAccountName: "Kliring Pembayaran Keluar",
      status: "ACTIVE",
    },
  ],
  cashAndClearingAccounts: [
    { accountId: "gl-1110", accountCode: "1110", accountName: "Kas & Bank Operasional", accountCategory: "CASH_BANK" },
    { accountId: "gl-1120", accountCode: "1120", accountName: "Kliring Keluar", accountCategory: "BANK_OUTBOUND_CLEARING" },
  ],
  openPayables: [
    {
      payableItemId: "payb-1",
      payableNumber: "PAYB-2026-00010",
      supplierBillId: "bill-1",
      billNumber: "BILL-2026-00010",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Sumber Makmur Kemasan",
      supplierInvoiceNumber: "INV-JKT-S11-01",
      glJournalId: "jrn-bill-1",
      glJournalNumber: "JRN-2026-00010",
      postingDate: "2026-11-12",
      dueDate: "2026-12-12",
      currencyCode: "IDR",
      grossBillAmount: 1110000,
      withholdingTaxAmount: 0,
      netPayableAmount: 1110000,
      paidAmount: 0,
      creditedAmount: 0,
      remainingOpenAmount: 1110000,
      status: "OPEN",
      paymentBlockStatus: "NONE",
      paymentBlockReason: null,
    },
  ],
  payments: [
    {
      supplierPaymentId: "pay-prop-1",
      draftCode: "PAY-DRF-2026-00001",
      paymentNumber: null,
      bankAccountId: "bank-1",
      bankAccountCode: "BCA-IDR-001",
      bankName: "PT Bank Central Asia Tbk",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Sumber Makmur Kemasan",
      beneficiaryBankName: "PT Bank Mandiri Tbk",
      beneficiaryAccountNumber: "122-000-4455",
      beneficiaryAccountHolder: "PT Sumber Makmur Kemasan",
      currencyCode: "IDR",
      paymentMethod: "BANK_TRANSFER_IDR",
      paymentDate: "2026-11-15",
      totalAllocatedAmount: 1110000,
      reconciledAmount: 0,
      remainingClearingAmount: 0,
      status: "PENDING_RELEASE",
      simulatedInstructionCount: 1,
      latestInstructionId: "inst-1",
      latestInstructionCode: "PIN-2026-00001",
      releaseReference: null,
      releaseJournalId: null,
      releaseJournalNumber: null,
      rejectionReversalJournalId: null,
      rejectionReversalJournalNumber: null,
      rejectionReason: null,
      notes: "Siap rilis empat mata",
      preparedByEmail: "siti.rahayu@demo.athera.local",
      submittedByEmail: "siti.rahayu@demo.athera.local",
      releasedByEmail: null,
      createdAtUtc: "2026-11-15T02:00:00Z",
      updatedAtUtc: "2026-11-15T02:05:00Z",
    },
    {
      supplierPaymentId: "pay-rel-1",
      draftCode: "PAY-DRF-2026-00002",
      paymentNumber: "PAY-2026-00001",
      bankAccountId: "bank-1",
      bankAccountCode: "BCA-IDR-001",
      bankName: "PT Bank Central Asia Tbk",
      supplierPartnerId: "sup-1",
      supplierCode: "PT-SUP-001",
      supplierLegalName: "PT Sumber Makmur Kemasan",
      beneficiaryBankName: "PT Bank Mandiri Tbk",
      beneficiaryAccountNumber: "122-000-4455",
      beneficiaryAccountHolder: "PT Sumber Makmur Kemasan",
      currencyCode: "IDR",
      paymentMethod: "BANK_TRANSFER_IDR",
      paymentDate: "2026-11-15",
      totalAllocatedAmount: 2220000,
      reconciledAmount: 0,
      remainingClearingAmount: 2220000,
      status: "RELEASED",
      simulatedInstructionCount: 1,
      latestInstructionId: "inst-2",
      latestInstructionCode: "PIN-2026-00002",
      releaseReference: "BCA-BATCH-20261115-01",
      releaseJournalId: "jrn-rel-1",
      releaseJournalNumber: "JRN-2026-00021",
      rejectionReversalJournalId: null,
      rejectionReversalJournalNumber: null,
      rejectionReason: null,
      notes: "Kliring terbuka",
      preparedByEmail: "siti.rahayu@demo.athera.local",
      submittedByEmail: "siti.rahayu@demo.athera.local",
      releasedByEmail: "budi.santoso@demo.athera.local",
      createdAtUtc: "2026-11-15T02:00:00Z",
      updatedAtUtc: "2026-11-15T02:10:00Z",
    },
  ],
  bankStatements: [
    {
      bankStatementId: "stmt-1",
      statementNumber: "BST-2026-00001",
      bankAccountId: "bank-1",
      bankAccountCode: "BCA-IDR-001",
      statementReference: "BCA-STMT-2026-11-15",
      statementDate: "2026-11-15",
      currencyCode: "IDR",
      openingBalance: 500000000,
      closingBalance: 497780000,
      totalDebitAmount: 2220000,
      totalCreditAmount: 0,
      lineCount: 1,
      sourceFormat: "CSV_UPLOAD",
      importedByEmail: "budi.santoso@demo.athera.local",
      importedAtUtc: "2026-11-15T03:00:00Z",
    },
  ],
  statementLines: [
    {
      bankStatementLineId: "line-1",
      bankStatementId: "stmt-1",
      statementNumber: "BST-2026-00001",
      bankAccountId: "bank-1",
      bankAccountCode: "BCA-IDR-001",
      lineNumber: 1,
      valueDate: "2026-11-15",
      direction: "DEBIT",
      amount: 2220000,
      matchedAmount: 0,
      unmatchedAmount: 2220000,
      bankReference: "TRF-BCA-20261115-01",
      counterpartyName: "PT Sumber Makmur Kemasan",
      description: "TRANSFER KELUAR INV-JKT-S11-02",
      status: "UNMATCHED",
    },
  ],
  matches: [],
  clearingProof: {
    glOutboundClearingCreditNetBalance: 2220000,
    subledgerOpenClearingBalance: 2220000,
    discrepancyAmount: 0,
    isReconciled: true,
    glMainOperatingBankNetBalance: 0,
  },
  serverTimestampUtc: "2026-11-15T04:00:00Z",
};

const pendingDetailFixture: SupplierPaymentDetailResponse = {
  payment: overviewFixture.payments[0],
  allocations: [
    {
      paymentAllocationId: "alloc-1",
      lineNumber: 1,
      payableItemId: "payb-1",
      payableNumber: "PAYB-2026-00010",
      supplierBillId: "bill-1",
      supplierInvoiceNumber: "INV-JKT-S11-01",
      payableOpenBeforeAmount: 1110000,
      allocatedAmount: 1110000,
      payableRemainingAfterAmount: 1110000,
      notes: "Draft hold",
    },
  ],
  instructions: [
    {
      paymentInstructionId: "inst-1",
      instructionNumber: "PIN-2026-00001",
      formatCode: "ATH_CSV_SIM_V1",
      simulationOnly: true,
      totalAmount: 1110000,
      currencyCode: "IDR",
      payloadText: "# SIMULATED_NON_LIVE_PAYMENT_INSTRUCTION\nPIN-2026-00001,PAY-DRF-2026-00001,1110000.00",
      payloadSha256: "abc123def4567890abc123def4567890",
      generatedByEmail: "siti.rahayu@demo.athera.local",
      generatedAtUtc: "2026-11-15T02:02:00Z",
    },
  ],
  matches: [],
  idempotentReplay: false,
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

vi.mock("@/lib/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/client")>("@/lib/api/client");
  return {
    ...actual,
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
      serverTimestampUtc: "2026-11-15T00:00:00Z",
    }),
    fetchPaymentsOverview: (...args: unknown[]) => overviewMock(...args),
    fetchSupplierPaymentDetail: (...args: unknown[]) => detailMock(...args),
    createSupplierPaymentProposal: (...args: unknown[]) => createProposalMock(...args),
    simulateSupplierPaymentFile: (...args: unknown[]) => simulateFileMock(...args),
    submitSupplierPaymentForRelease: vi.fn(),
    releaseSupplierPayment: (...args: unknown[]) => releaseMock(...args),
    rejectReleasedSupplierPayment: vi.fn(),
    importBankStatement: vi.fn(),
    reconcileBankStatementLine: (...args: unknown[]) => reconcileMock(...args),
    reverseBankPaymentMatch: vi.fn(),
    upsertBankAccount: vi.fn(),
  };
});

describe("S11 Payments, Outbound Clearing & Bank Reconciliation UI", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUser = {
      userId: "11111111-1111-1111-1111-111111111104",
      email: "siti.rahayu@demo.athera.local",
      displayName: "Siti Rahayu (AP Clerk)",
      preferredLocale: "id-ID",
      lastSignedInAtUtc: "2026-11-15T01:00:00Z",
      workspaceAccessStatus: "ACTIVE",
      authorizedWorkspaceCount: 1,
      mfaEnabled: false,
    };
    activePermissions = ["workspace.view", "ap.read", "ap.manage", "finance.read"];
    overviewMock.mockResolvedValue(overviewFixture);
    detailMock.mockResolvedValue(pendingDetailFixture);
  });

  it("renders clearing KPIs with 1120 reconciled proof and enforces OD-02 self-release SoD notice", async () => {
    render(
      <LocaleProvider>
        <PaymentsPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
        "Pembayaran Pemasok, Kliring Keluar & Rekonsiliasi Bank",
      );
    });

    expect(screen.getByText("Kliring 1120 Cocok (GL = Subledger)")).toBeInTheDocument();
    expect(screen.getByText("PAY-DRF-2026-00001")).toBeInTheDocument();

    // Expand OD-06 stages drawer
    fireEvent.click(screen.getByRole("button", { name: /Lihat Tahapan Kliring/ }));
    expect(screen.getByText("1. Draf & Simulasi Berkas")).toBeInTheDocument();

    // Open PAY-DRF-2026-00001 prepared by Siti -> OD-02 blocks self-release even if ap.approve were held
    fireEvent.click(screen.getByTestId("open-payment-PAY-DRF-2026-00001"));
    await waitFor(() => {
      expect(screen.getByTestId("pay-sod-notice")).toBeInTheDocument();
    });
    expect(screen.getByText("SIMULASI · BUKAN TRANSFER LANGSUNG")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Rilis & Posting/ })).not.toBeInTheDocument();
  });

  it("allows independent controller to release payment and reconcile debit bank line", async () => {
    mockUser = {
      ...mockUser,
      userId: "11111111-1111-1111-1111-111111111102",
      email: "budi.santoso@demo.athera.local",
      displayName: "Budi Santoso (Controller)",
    };
    activePermissions = ["workspace.view", "ap.read", "ap.approve", "ap.post", "finance.read", "finance.journal.post"];

    const releasedResponse: SupplierPaymentDetailResponse = {
      ...pendingDetailFixture,
      payment: {
        ...overviewFixture.payments[0],
        paymentNumber: "PAY-2026-00009",
        status: "RELEASED",
        remainingClearingAmount: 1110000,
        releaseReference: "BCA-PORTAL-20261115",
        releaseJournalNumber: "JRN-2026-00099",
        releasedByEmail: "budi.santoso@demo.athera.local",
      },
    };
    releaseMock.mockResolvedValueOnce(releasedResponse);

    const matchResponse: BankPaymentMatchDto = {
      bankPaymentMatchId: "match-1",
      matchNumber: "BMK-2026-00001",
      bankAccountId: "bank-1",
      bankAccountCode: "BCA-IDR-001",
      bankStatementId: "stmt-1",
      statementNumber: "BST-2026-00001",
      bankStatementLineId: "line-1",
      bankReference: "TRF-BCA-20261115-01",
      supplierPaymentId: "pay-rel-1",
      paymentDisplayNumber: "PAY-2026-00001",
      postingDate: "2026-11-15",
      matchedAmount: 2220000,
      status: "POSTED",
      clearingGlJournalId: "jrn-bmk-1",
      clearingGlJournalNumber: "JRN-2026-00100",
      reversalGlJournalId: null,
      reversalGlJournalNumber: null,
      reversalReason: null,
      matchedByEmail: "budi.santoso@demo.athera.local",
      matchedAtUtc: "2026-11-15T05:00:00Z",
    };
    reconcileMock.mockResolvedValueOnce(matchResponse);

    render(
      <LocaleProvider>
        <PaymentsPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("open-payment-PAY-DRF-2026-00001")).toBeInTheDocument();
    });

    // Budi releases Siti's proposal
    fireEvent.click(screen.getByTestId("open-payment-PAY-DRF-2026-00001"));
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Rilis & Posting Dr 2100 \/ Cr 1120/ })).toBeInTheDocument();
    });
    const releaseRefInput = document.getElementById("pay-release-ref") as HTMLInputElement;
    fireEvent.change(releaseRefInput, { target: { value: "BCA-PORTAL-20261119-01" } });
    fireEvent.click(screen.getByRole("button", { name: /Rilis & Posting Dr 2100 \/ Cr 1120/ }));

    await waitFor(() => {
      expect(releaseMock).toHaveBeenCalledTimes(1);
    });

    // Switch to Bank Statements & Reconciliation tab and match TRF-BCA-20261115-01
    fireEvent.click(screen.getByRole("tab", { name: /Mutasi Bank & Rekonsiliasi/ }));
    await waitFor(() => {
      expect(screen.getByTestId("reconcile-line-TRF-BCA-20261115-01")).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId("reconcile-line-TRF-BCA-20261115-01"));
    fireEvent.click(screen.getByRole("button", { name: /Konfirmasi & Posting Dr 1120 \/ Cr 1110/ }));

    await waitFor(() => {
      expect(reconcileMock).toHaveBeenCalledTimes(1);
    });
  });

  it("shows read-only banner and hides mutating actions when actor holds only ap.read", async () => {
    activePermissions = ["workspace.view", "ap.read", "finance.read"];

    render(
      <LocaleProvider>
        <PaymentsPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Mode hanya-baca/)).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: /Buat Usulan Pembayaran/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Impor Mutasi CSV/ })).not.toBeInTheDocument();
  });
});
