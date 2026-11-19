import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import BillingPage from "@/app/(shell)/w/[tenantId]/[entityId]/billing/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import * as api from "@/lib/api/client";

vi.mock("next/navigation", () => ({
  useParams: () => ({
    tenantId: "11111111-1111-1111-1111-111111111111",
    entityId: "22222222-2222-2222-2222-222222222222",
  }),
}));

const BASE_OVERVIEW: api.BillingOverviewResponse = {
  canDraft: true,
  canPost: true,
  stats: {
    unbilledDispatchedBaseQty: 4,
    openDraftInvoicesCount: 1,
    postedRevenueNetTotal: 300000,
    openReceivablesTotal: 333000,
  },
  outputTaxCodes: [
    {
      taxCodeId: "tax-11",
      code: "PPN-OUT-11",
      name: "PPN Keluaran Standar 11%",
      treatmentClass: "STANDARD_TAXABLE",
      nominalRatePercent: 11,
      taxableBaseRatioNumerator: 1,
      taxableBaseRatioDenominator: 1,
      effectiveRatePercent: 11,
      requiresDocumentaryEvidence: true,
      effectiveFrom: "2025-01-01",
      effectiveTo: null,
    },
    {
      taxCodeId: "tax-0",
      code: "PPN-OUT-EXEMPT",
      name: "PPN Dibebaskan 0%",
      treatmentClass: "EXEMPT",
      nominalRatePercent: 0,
      taxableBaseRatioNumerator: 1,
      taxableBaseRatioDenominator: 1,
      effectiveRatePercent: 0,
      requiresDocumentaryEvidence: true,
      effectiveFrom: "2025-01-01",
      effectiveTo: null,
    },
  ],
  billableShipmentLines: [
    {
      shipmentLineId: "shpl-1",
      shipmentId: "shp-1",
      shipmentNumber: "SHP-2026-00001",
      dispatchDate: "2026-11-19",
      shipmentStatus: "DELIVERED",
      salesOrderId: "so-1",
      salesOrderNumber: "SO-2026-00001",
      customerPartnerId: "cust-1",
      customerCode: "PT-CUST-001",
      customerName: "PT Graha Ritel Nusantara",
      customerNpwp: "01.777.332.1-011.000",
      paymentTermsDays: 30,
      soLineId: "sol-1",
      lineageLineKey: "SO-L001",
      itemId: "itm-1",
      itemCode: "ITM-S09-VALVE-01",
      itemName: "Katup Industri PN16",
      baseUomCode: "PCS",
      dispatchedBaseQuantity: 4,
      alreadyBilledBaseQuantity: 0,
      remainingBillableBaseQuantity: 4,
      unitPricePerBase: 150000,
      defaultTaxCodeId: "tax-11",
      defaultTaxCodeSnapshot: "PPN-OUT-11",
    },
  ],
  invoices: [
    {
      invoiceId: "inv-draft-1",
      invoiceNumber: "INV-2026-00001",
      documentType: "INVOICE",
      originalInvoiceId: null,
      originalInvoiceNumber: null,
      salesOrderId: "so-1",
      salesOrderNumber: "SO-2026-00001",
      customerPartnerId: "cust-1",
      customerCode: "PT-CUST-001",
      customerName: "PT Graha Ritel Nusantara",
      customerNpwp: "01.777.332.1-011.000",
      invoiceDate: "2026-11-19",
      dueDate: "2026-12-19",
      currencyCode: "IDR",
      status: "SUBMITTED",
      subtotalAmount: 300000,
      taxAmount: 33000,
      grandTotalAmount: 333000,
      taxCodeId: "tax-11",
      taxCodeSnapshot: "PPN-OUT-11",
      taxTreatmentClassSnapshot: "STANDARD_TAXABLE",
      taxRatePercentSnapshot: 11,
      requiresTaxEvidenceSnapshot: true,
      taxEvidenceReference: null,
      taxDeterminationSummary: "PPN-OUT-11 (STANDARD_TAXABLE, 11%)",
      glJournalId: null,
      glJournalNumber: null,
      arReceivableId: null,
      notes: null,
      createdAtUtc: "2026-11-19T01:00:00Z",
      submittedAtUtc: "2026-11-19T01:05:00Z",
      postedAtUtc: null,
      lines: [
        {
          invoiceLineId: "invl-1",
          lineNumber: 1,
          shipmentId: "shp-1",
          shipmentNumber: "SHP-2026-00001",
          shipmentLineId: "shpl-1",
          soLineId: "sol-1",
          lineageLineKey: "SO-L001",
          itemId: "itm-1",
          itemCode: "ITM-S09-VALVE-01",
          itemName: "Katup Industri PN16",
          baseUomCode: "PCS",
          billedBaseQuantity: 2,
          alreadyCreditedBaseQuantity: 0,
          remainingCreditableBaseQuantity: 2,
          unitPrice: 150000,
          lineSubtotalAmount: 300000,
          taxCodeSnapshot: "PPN-OUT-11",
          taxRatePercent: 11,
          lineTaxAmount: 33000,
          lineTotalAmount: 333000,
          originalInvoiceLineId: null,
        },
      ],
    },
  ],
  receivables: [],
};

describe("S14 Customer Billing, Output Tax & AR UI", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders billable shipments and previews PPN-OUT-11 vs PPN-OUT-EXEMPT totals live", async () => {
    vi.spyOn(api, "fetchBillingOverview").mockResolvedValue(BASE_OVERVIEW);

    render(
      <LocaleProvider>
        <BillingPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("SHP-2026-00001")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("draft-inv-SHP-2026-00001-SO-L001"));
    expect(document.getElementById("bil-draft-drawer")).toBeInTheDocument();
    expect(document.getElementById("bil-draft-drawer")?.textContent).toContain("666.000");

    fireEvent.change(document.getElementById("bil-draft-tax")!, { target: { value: "tax-0" } });
    expect(document.getElementById("bil-draft-drawer")?.textContent).toContain("600.000");
  });

  it("enforces mandatory OD-07 Faktur Pajak evidence before enabling irreversible posting", async () => {
    vi.spyOn(api, "fetchBillingOverview").mockResolvedValue(BASE_OVERVIEW);
    const postSpy = vi.spyOn(api, "postCustomerInvoice").mockResolvedValue({
      ...BASE_OVERVIEW.invoices[0],
      status: "POSTED",
      glJournalNumber: "JRN-2026-11-00050",
    });

    render(
      <LocaleProvider>
        <BillingPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("bil-tab-invoices")).toBeInTheDocument();
    });

    fireEvent.click(document.getElementById("bil-tab-invoices")!);
    fireEvent.click(screen.getByTestId("open-post-INV-2026-00001"));

    const confirmBtn = document.getElementById("bil-post-confirm-btn") as HTMLButtonElement;
    expect(confirmBtn.disabled).toBe(true);

    fireEvent.change(document.getElementById("bil-post-evidence")!, {
      target: { value: "FP-010.001-26.00000001" },
    });
    expect(confirmBtn.disabled).toBe(false);

    fireEvent.click(confirmBtn);
    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledTimes(1);
    });
  });

  it("enforces read-only mode on entities without draft/post permissions and hides action buttons", async () => {
    vi.spyOn(api, "fetchBillingOverview").mockResolvedValue({
      ...BASE_OVERVIEW,
      canDraft: false,
      canPost: false,
    });

    render(
      <LocaleProvider>
        <BillingPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(document.getElementById("bil-readonly-banner")).toBeInTheDocument();
    });
    expect(screen.queryByTestId("draft-inv-SHP-2026-00001-SO-L001")).not.toBeInTheDocument();
  });
});
