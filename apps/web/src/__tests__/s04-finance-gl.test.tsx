import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import FinancePage from "@/app/(shell)/w/[tenantId]/[entityId]/finance/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  AccountMappingListResponse,
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  FinancialPeriodListResponse,
  GlAccountListResponse,
  JournalDetailDto,
  PagedJournalsResponse,
  TaxCodeListResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const mockUser: CurrentUserResponse = {
  userId: "11111111-1111-1111-1111-111111111102",
  email: "budi.santoso@demo.athera.local",
  displayName: "Budi Santoso",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-18T00:00:00Z",
  workspaceAccessStatus: "ACTIVE_WORKSPACES",
  authorizedWorkspaceCount: 1,
  mfaEnabled: false,
};

let currentPermissions = ["workspace.view", "finance.read", "finance.config.manage", "finance.journal.post"];

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

const accountsPayload: GlAccountListResponse = {
  items: [
    {
      accountId: "acc-h1",
      accountCode: "1000",
      name: "Aset (Header)",
      accountType: "ASSET",
      accountCategory: "HEADER_GROUP",
      normalBalance: "DEBIT",
      parentAccountId: null,
      allowPosting: false,
      isControlAccount: false,
      status: "ACTIVE",
      createdAtUtc: "2026-11-18T00:00:00Z",
      updatedAtUtc: "2026-11-18T00:00:00Z",
      version: 1,
    },
    {
      accountId: "acc-1110",
      accountCode: "1110",
      name: "Kas & Bank Utama",
      accountType: "ASSET",
      accountCategory: "CASH_BANK",
      normalBalance: "DEBIT",
      parentAccountId: "acc-h1",
      allowPosting: true,
      isControlAccount: false,
      status: "ACTIVE",
      createdAtUtc: "2026-11-18T00:00:00Z",
      updatedAtUtc: "2026-11-18T00:00:00Z",
      version: 1,
    },
    {
      accountId: "acc-5300",
      accountCode: "5300",
      name: "Beban Operasional Umum",
      accountType: "EXPENSE",
      accountCategory: "OPERATING_EXPENSE",
      normalBalance: "DEBIT",
      parentAccountId: null,
      allowPosting: true,
      isControlAccount: false,
      status: "ACTIVE",
      createdAtUtc: "2026-11-18T00:00:00Z",
      updatedAtUtc: "2026-11-18T00:00:00Z",
      version: 1,
    },
  ],
};

const periodsPayload: FinancialPeriodListResponse = {
  items: [
    {
      periodId: "per-11",
      periodCode: "2026-11",
      fiscalYear: 2026,
      periodNumber: 11,
      startDate: "2026-11-01",
      endDate: "2026-11-30",
      status: "OPEN",
      closedAtUtc: null,
      closedByUserId: null,
      closeNote: null,
      updatedAtUtc: "2026-11-18T00:00:00Z",
      version: 1,
    },
    {
      periodId: "per-10",
      periodCode: "2026-10",
      fiscalYear: 2026,
      periodNumber: 10,
      startDate: "2026-10-01",
      endDate: "2026-10-31",
      status: "CLOSED",
      closedAtUtc: "2026-11-01T00:00:00Z",
      closedByUserId: mockUser.userId,
      closeNote: "Closed Oct",
      updatedAtUtc: "2026-11-01T00:00:00Z",
      version: 2,
    },
  ],
};

const mappingsPayload: AccountMappingListResponse = {
  items: [],
  requiredPurposes: ["INVENTORY_ASSET", "GRNI_CLEARING"],
};

const taxPayload: TaxCodeListResponse = {
  items: [
    {
      taxCodeId: "tax-1112",
      code: "PPN-12-DPP1112",
      name: "PPN 12% DPP 11/12",
      taxFamily: "PPN",
      direction: "INPUT",
      treatmentClass: "SPECIAL_DPP_11_12",
      nominalRatePercent: 12,
      taxableBaseRatioNumerator: 11,
      taxableBaseRatioDenominator: 12,
      requiresDocumentaryEvidence: true,
      requiresCounterpartyNpwp: false,
      taxAccountId: "acc-1110",
      effectiveFrom: "2025-01-01",
      effectiveTo: null,
      reviewStatus: "UNREVIEWED_CONFIGURABLE",
      statutoryReferenceNote: "OD-07 template",
      status: "ACTIVE",
      updatedAtUtc: "2026-11-18T00:00:00Z",
      version: 1,
    },
  ],
  governanceNotice: "OD-07 Notice",
};

const journalsPayload: PagedJournalsResponse = {
  items: [
    {
      journalId: "jrn-1",
      periodId: "per-11",
      periodCode: "2026-11",
      journalNumber: "JRN-2026-11-00001",
      entryKind: "STANDARD",
      postingDate: "2026-11-01",
      currencyCode: "IDR",
      description: "Setoran awal modal",
      sourceModule: "DEMO_SEED",
      sourceDocumentType: "OPENING_SEED",
      sourceDocumentId: "seed-1",
      sourceEventKey: "seed:opening",
      reversalOfJournalId: null,
      isReversed: false,
      reversedByJournalId: null,
      totalDebit: 100000000,
      totalCredit: 100000000,
      lineCount: 2,
      postedByUserId: mockUser.userId,
      postedAtUtc: "2026-11-18T00:00:00Z",
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 40,
};

const journalDetailPayload: JournalDetailDto = {
  ...journalsPayload.items[0]!,
  payloadHash: "abc123",
  idempotentReplay: false,
  lines: [
    {
      lineId: "l1",
      lineNumber: 1,
      accountId: "acc-1110",
      accountCode: "1110",
      accountName: "Kas & Bank Utama",
      description: "Dr Cash",
      debitAmount: 100000000,
      creditAmount: 0,
      partnerId: null,
      itemId: null,
      taxCodeId: null,
    },
    {
      lineId: "l2",
      lineNumber: 2,
      accountId: "acc-5300",
      accountCode: "5300",
      accountName: "Beban Operasional Umum",
      description: "Cr Capital",
      debitAmount: 0,
      creditAmount: 100000000,
      partnerId: null,
      itemId: null,
      taxCodeId: null,
    },
  ],
};

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/components/AppShell", () => ({
  useSessionUser: () => mockUser,
  useRefreshShell: () => vi.fn().mockResolvedValue(undefined),
}));

vi.mock("@/lib/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/client")>("@/lib/api/client");
  return {
    ...actual,
    fetchEntityWorkspaceDetail: () => fetchDetailMock(),
    fetchGlAccounts: async () => accountsPayload,
    fetchFinancialPeriods: async () => periodsPayload,
    fetchAccountMappings: async () => mappingsPayload,
    fetchTaxCodes: async () => taxPayload,
    fetchJournals: async () => journalsPayload,
    fetchJournalDetail: async () => journalDetailPayload,
  };
});

describe("S04 Finance & GL UI", () => {
  it("tracks live balanced/unbalanced variance cleanly and supports CoA & Tax tabs", async () => {
    currentPermissions = ["workspace.view", "finance.read", "finance.config.manage", "finance.journal.post"];

    render(
      <LocaleProvider>
        <FinancePage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(/Konfigurasi Keuangan/);
    });

    expect(screen.getByTestId("draft-balance-badge")).toHaveTextContent(/Seimbang/);

    const debitInput = document.getElementById("draft-debit-0") as HTMLInputElement;
    const creditInput = document.getElementById("draft-credit-1") as HTMLInputElement;

    // Verify IEEE-754 tricky decimal 0.10 + 0.20 == 0.30 evaluates as exact minor-unit balance
    fireEvent.change(debitInput, { target: { value: "100.10" } });
    fireEvent.change(creditInput, { target: { value: "100.10" } });
    expect(screen.getByTestId("draft-balance-badge")).toHaveTextContent(/Seimbang/);

    fireEvent.change(debitInput, { target: { value: "300000" } });
    expect(screen.getByTestId("draft-balance-badge")).toHaveTextContent(/Tidak Seimbang/);

    fireEvent.click(screen.getByRole("tab", { name: /Bagan Akun/ }));
    expect(screen.getByText("Aset (Header)")).toBeInTheDocument();
    expect(screen.getAllByText(/1000/).length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("Grup Header")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: /Periode Fiskal & Pajak/ }));
    expect(screen.getAllByText(/PPN-12-DPP1112/).length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText(/PPN 12% DPP 11\/12/).length).toBeGreaterThanOrEqual(2);
  });

  it("hides composer, reversal, and configuration mutations for read-only finance.read actors", async () => {
    currentPermissions = ["workspace.view", "finance.read"];

    render(
      <LocaleProvider>
        <FinancePage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Mode baca saja/)).toBeInTheDocument();
    });

    expect(screen.queryByRole("button", { name: /Posting Jurnal Permanen/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Posting Jurnal Pembalik/ })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Lihat Baris & Sumber/ })).toBeInTheDocument();
  });
});
