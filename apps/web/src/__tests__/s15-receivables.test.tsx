import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import ReceivablesPage from "@/app/(shell)/w/[tenantId]/[entityId]/receivables/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import * as api from "@/lib/api/client";

vi.mock("next/navigation", () => ({
  useParams: () => ({
    tenantId: "11111111-1111-1111-1111-111111111111",
    entityId: "22222222-2222-2222-2222-222222222222",
  }),
}));

const BASE_OVERVIEW: api.ReceivablesOverviewResponse = {
  canManage: true,
  aging: {
    asOfDate: "2026-11-19",
    currentTotal: 210900,
    days1To30Total: 0,
    days31To60Total: 0,
    days61To90Total: 0,
    over90Total: 0,
    totalOpenReceivables: 210900,
    totalUnappliedReceipts: 0,
    netArSubledger: 210900,
    glArControlBalance: 210900,
    isGlReconciled: true,
    customers: [
      {
        customerPartnerId: "cust-1",
        customerCode: "PT-CUST-001",
        customerName: "PT Graha Ritel Nusantara",
        currentAmount: 210900,
        days1To30Amount: 0,
        days31To60Amount: 0,
        days61To90Amount: 0,
        over90Amount: 0,
        totalOpenReceivables: 210900,
        unappliedReceiptsAmount: 0,
        netReceivableBalance: 210900,
      },
    ],
  },
  bankAccounts: [
    {
      bankAccountId: "bank-1",
      accountCode: "BCA-OPS-01",
      bankName: "Bank Central Asia",
      accountNumberMasked: "123-456-7890",
      currencyCode: "IDR",
    },
  ],
  inboundBankLines: [
    {
      bankStatementLineId: "line-1",
      bankStatementId: "stmt-1",
      bankAccountId: "bank-1",
      bankAccountCode: "BCA-OPS-01",
      valueDate: "2026-11-19",
      amount: 150000,
      matchedAmount: 0,
      unmatchedAmount: 150000,
      bankReference: "TRF-IN-001",
      counterpartyName: "PT Graha Ritel Nusantara",
      description: "Pembayaran parsial",
      status: "UNMATCHED",
    },
  ],
  customers: [
    { partnerId: "cust-1", partnerCode: "PT-CUST-001", legalName: "PT Graha Ritel Nusantara" },
  ],
  openReceivables: [
    {
      receivableId: "ar-1",
      receivableNumber: "AR-2026-00001",
      invoiceId: "inv-1",
      invoiceNumber: "INV-2026-00001",
      customerPartnerId: "cust-1",
      customerCode: "PT-CUST-001",
      customerName: "PT Graha Ritel Nusantara",
      invoiceDate: "2026-11-19",
      dueDate: "2026-12-19",
      currencyCode: "IDR",
      originalAmount: 210900,
      creditedAmount: 0,
      receivedAmount: 0,
      openAmount: 210900,
      status: "OPEN",
      glJournalId: "jrn-1",
      glJournalNumber: "JRN-2026-11-00028",
      createdAtUtc: "2026-11-19T01:00:00Z",
    },
  ],
  receipts: [],
};

describe("S15 Customer Receipts, Allocation & AR Aging UI", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("displays GL 1200 reconciled proof badge and computes live Received / Allocated / Unapplied totals", async () => {
    vi.spyOn(api, "fetchReceivablesOverview").mockResolvedValue(BASE_OVERVIEW);

    render(
      <LocaleProvider>
        <ReceivablesPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("ar-gl-reconciled-badge")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("confirm-bank-TRF-IN-001"));
    expect(document.getElementById("ar-confirm-drawer")).toBeInTheDocument();

    fireEvent.change(document.getElementById("ar-init-alloc-0")!, { target: { value: "100000" } });
    expect(document.getElementById("ar-confirm-drawer")?.textContent).toContain("100.000");
    expect(document.getElementById("ar-confirm-drawer")?.textContent).toContain("50.000");
  });

  it("disables confirmation when requested allocation exceeds received bank amount", async () => {
    vi.spyOn(api, "fetchReceivablesOverview").mockResolvedValue(BASE_OVERVIEW);

    render(
      <LocaleProvider>
        <ReceivablesPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("confirm-bank-TRF-IN-001")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("confirm-bank-TRF-IN-001"));
    fireEvent.change(document.getElementById("ar-init-alloc-0")!, { target: { value: "200000" } });
    const submitBtn = document.getElementById("ar-confirm-submit-btn") as HTMLButtonElement;
    expect(submitBtn.disabled).toBe(true);
  });

  it("enforces read-only mode on NUSA-SBY without import or confirm actions", async () => {
    vi.spyOn(api, "fetchReceivablesOverview").mockResolvedValue({
      ...BASE_OVERVIEW,
      canManage: false,
    });

    render(
      <LocaleProvider>
        <ReceivablesPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("ar-readonly-banner")).toBeInTheDocument();
    });
    expect(document.getElementById("ar-open-import-btn")).not.toBeInTheDocument();
    expect(screen.queryByTestId("confirm-bank-TRF-IN-001")).not.toBeInTheDocument();
  });
});
