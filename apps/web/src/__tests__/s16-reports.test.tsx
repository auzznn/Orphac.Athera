import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import AccountingReportsPage from "@/app/(shell)/w/[tenantId]/[entityId]/reports/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import * as api from "@/lib/api/client";

vi.mock("next/navigation", () => ({
  useParams: () => ({
    tenantId: "11111111-1111-1111-1111-111111111111",
    entityId: "22222222-2222-2222-2222-222222222222",
  }),
}));

const BASE_OVERVIEW: api.AccountingReportsOverviewResponse = {
  canClosePeriod: true,
  canLockOrReopen: true,
  trialBalance: {
    periodId: "per-1",
    periodCode: "2026-11",
    startDate: "2026-11-01",
    endDate: "2026-11-30",
    totalOpeningDebit: 0,
    totalOpeningCredit: 0,
    totalPeriodDebit: 500000,
    totalPeriodCredit: 500000,
    totalEndingDebit: 500000,
    totalEndingCredit: 500000,
    isBalanced: true,
    rows: [
      {
        accountId: "acc-1120",
        accountCode: "1120",
        accountName: "Kas & Bank Operasional",
        accountType: "ASSET",
        normalBalance: "DEBIT",
        openingDebit: 0,
        openingCredit: 0,
        periodDebit: 500000,
        periodCredit: 0,
        endingDebit: 500000,
        endingCredit: 0,
      },
      {
        accountId: "acc-4000",
        accountCode: "4000",
        accountName: "Pendapatan Penjualan",
        accountType: "REVENUE",
        normalBalance: "CREDIT",
        openingDebit: 0,
        openingCredit: 0,
        periodDebit: 0,
        periodCredit: 500000,
        endingDebit: 0,
        endingCredit: 500000,
      },
    ],
  },
  profitAndLoss: {
    totalRevenue: 500000,
    totalExpense: 0,
    netIncome: 500000,
    revenueLines: [
      {
        accountId: "acc-4000",
        accountCode: "4000",
        accountName: "Pendapatan Penjualan",
        accountType: "REVENUE",
        amount: 500000,
      },
    ],
    expenseLines: [],
  },
  balanceSheet: {
    totalAssets: 500000,
    totalLiabilities: 0,
    totalEquity: 0,
    currentEarnings: 500000,
    totalLiabilitiesAndEquity: 500000,
    isBalanced: true,
    assetLines: [
      {
        accountId: "acc-1120",
        accountCode: "1120",
        accountName: "Kas & Bank Operasional",
        accountType: "ASSET",
        amount: 500000,
      },
    ],
    liabilityLines: [],
    equityLines: [],
  },
  reconciliations: [
    {
      purposeCode: "CASH_BANK_CONTROL",
      controlLabel: "Kas & Bank",
      accountCode: "1120",
      accountName: "Kas & Bank Operasional",
      accountId: "acc-1120",
      glBalance: 500000,
      subledgerBalance: 500000,
      varianceAmount: 0,
      status: "RECONCILED",
      openItemCount: 0,
      explanation: "Terekonsiliasi",
    },
  ],
  apAging: {
    currentAmount: 100000,
    days1To30Amount: 0,
    days31To60Amount: 0,
    days61To90Amount: 0,
    over90Amount: 0,
    totalOpenAmount: 100000,
    openItemCount: 1,
  },
  arAging: {
    currentAmount: 200000,
    days1To30Amount: 0,
    days31To60Amount: 0,
    days61To90Amount: 0,
    over90Amount: 0,
    totalOpenAmount: 200000,
    openItemCount: 1,
  },
  periods: [
    {
      periodId: "per-1",
      periodCode: "2026-11",
      startDate: "2026-11-01",
      endDate: "2026-11-30",
      status: "OPEN",
      closeNote: null,
      closedAtUtc: null,
    },
  ],
  closeReviews: [],
};

describe("S16 AccountingReportsPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders 5-control reconciliation, trial balance drilldown, and statements", async () => {
    vi.spyOn(api, "fetchAccountingReportsOverview").mockResolvedValue(BASE_OVERVIEW);
    const drillSpy = vi.spyOn(api, "fetchAccountLedgerDrilldown").mockResolvedValue({
      accountId: "acc-1120",
      accountCode: "1120",
      accountName: "Kas & Bank Operasional",
      normalBalance: "DEBIT",
      entries: [
        {
          journalId: "jrn-1",
          journalNumber: "JRN-2026-00001",
          postingDate: "2026-11-19",
          entryKind: "STANDARD",
          sourceModule: "AR",
          sourceDocumentType: "CUSTOMER_RECEIPT",
          sourceDocumentId: "rcp-1",
          description: "Penerimaan pelanggan",
          debitAmount: 500000,
          creditAmount: 0,
          runningBalance: 500000,
        },
      ],
    });

    render(
      <LocaleProvider>
        <AccountingReportsPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Kas & Bank Operasional/i)).toBeInTheDocument();
    });

    // Click drilldown
    fireEvent.click(screen.getByRole("button", { name: /Telusuri GL|Inspect GL/i }));
    await waitFor(() => {
      expect(drillSpy).toHaveBeenCalled();
      expect(screen.getByText("JRN-2026-00001")).toBeInTheDocument();
    });

    // Switch to Trial Balance tab
    fireEvent.click(screen.getByRole("tab", { name: /Neraca Saldo|Trial Balance/i }));
    expect(screen.getByText(/4000/)).toBeInTheDocument();

    // Switch to Statements tab
    fireEvent.click(screen.getByRole("tab", { name: /Laba Rugi|P&L/i }));
    expect(screen.getByText(/Laporan Posisi Keuangan|Balance Sheet/i)).toBeInTheDocument();
  });

  it("hides period mutation buttons when user is read-only", async () => {
    vi.spyOn(api, "fetchAccountingReportsOverview").mockResolvedValue({
      ...BASE_OVERVIEW,
      canClosePeriod: false,
      canLockOrReopen: false,
    });

    render(
      <LocaleProvider>
        <AccountingReportsPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Mode baca saja|Read-only mode/i)).toBeInTheDocument();
    });

    expect(screen.queryByRole("button", { name: /Tutup Periode|Close Period/i })).toBeNull();
  });

  it("submits close period action through inline drawer", async () => {
    vi.spyOn(api, "fetchAccountingReportsOverview").mockResolvedValue(BASE_OVERVIEW);
    const closeSpy = vi.spyOn(api, "closeReportPeriod").mockResolvedValue({
      closeReviewId: "rev-1",
      periodId: "per-1",
      periodCode: "2026-11",
      actionType: "CLOSED",
      trialBalanceDebits: 500000,
      trialBalanceCredits: 500000,
      isTrialBalanceBalanced: true,
      reconciliationVarianceCount: 0,
      totalVarianceAmount: 0,
      unbilledGrniAmount: 0,
      reason: "Bulan selesai",
      createdAtUtc: "2026-11-19T08:00:00Z",
    });

    render(
      <LocaleProvider>
        <AccountingReportsPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Tutup Periode|Close Period/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /Tutup Periode|Close Period/i }));
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Bulan selesai" } });
    fireEvent.click(screen.getByRole("button", { name: /Konfirmasi Tutup Periode|Confirm Close Period/i }));

    await waitFor(() => {
      expect(closeSpy).toHaveBeenCalledWith(
        "11111111-1111-1111-1111-111111111111",
        "22222222-2222-2222-2222-222222222222",
        "per-1",
        { note: "Bulan selesai", blockOnVariance: true },
      );
    });
  });
});
