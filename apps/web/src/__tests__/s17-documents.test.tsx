import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import DocumentsAndEvidencePage from "@/app/(shell)/w/[tenantId]/[entityId]/documents/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import * as api from "@/lib/api/client";

vi.mock("next/navigation", () => ({
  useParams: () => ({
    tenantId: "11111111-1111-1111-1111-111111111111",
    entityId: "22222222-2222-2222-2222-222222222222",
  }),
}));

const BASE_OVERVIEW: api.DocumentsOverviewResponse = {
  canUpload: true,
  canRetryOutbox: true,
  totalDocuments: 1,
  totalExceptions: 1,
  pendingOutboxCount: 0,
  failedOutboxCount: 1,
  processedOutboxCount: 4,
  documents: [
    {
      documentId: "doc-1",
      documentNumber: "DOC-2026-00001",
      fileName: "faktur-pajak-001.pdf",
      contentType: "application/pdf",
      byteSize: 2048,
      sha256Hash: "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
      category: "TAX_EVIDENCE",
      scanStatus: "CLEAN",
      notes: "Faktur Pajak tervalidasi",
      sourceModule: "BILLING",
      sourceDocumentType: "CUSTOMER_INVOICE",
      sourceDocumentId: "inv-1",
      sourceDocumentNumber: "INV-2026-00001",
      createdAtUtc: "2026-11-19T08:00:00Z",
    },
  ],
  exceptions: [
    {
      exceptionId: "outbox-1",
      category: "OUTBOX_FAILED",
      severity: "HIGH",
      referenceNumber: "gl.period.closed",
      title: "Integration Outbox Failed (gl.period.closed)",
      detail: "Simulated transient timeout",
      occurredAtUtc: "2026-11-19T08:05:00Z",
    },
  ],
  outboxMessages: [
    {
      messageId: "msg-1",
      topic: "gl.period.closed",
      sourceKey: "sk-1",
      status: "FAILED",
      attemptCount: 2,
      lastError: "Simulated transient timeout",
      createdAtUtc: "2026-11-19T08:05:00Z",
      processedAtUtc: null,
    },
  ],
  auditEvents: [
    {
      eventId: "ev-1",
      eventType: "DOCUMENT_EVIDENCE_ATTACHED",
      subjectType: "BusinessDocument",
      subjectId: "doc-1",
      summary: "Attached DOC-2026-00001 to CUSTOMER_INVOICE INV-2026-00001",
      occurredAtUtc: "2026-11-19T08:00:00Z",
    },
  ],
};

describe("S17 DocumentsAndEvidencePage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders linked documents, exceptions, outbox retry, and audit trail", async () => {
    vi.spyOn(api, "fetchDocumentsOverview").mockResolvedValue(BASE_OVERVIEW);
    const retrySpy = vi.spyOn(api, "retryOutboxMessage").mockResolvedValue({
      messageId: "msg-1",
      status: "PENDING",
      retriedAtUtc: "2026-11-19T08:10:00Z",
    });

    render(
      <LocaleProvider>
        <DocumentsAndEvidencePage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("DOC-2026-00001")).toBeInTheDocument();
      expect(screen.getByText("faktur-pajak-001.pdf")).toBeInTheDocument();
    });

    // Switch to Exceptions & Outbox tab and click Retry
    fireEvent.click(screen.getByRole("tab", { name: /Pengecualian|Exceptions/i }));
    await waitFor(() => {
      expect(screen.getByText("gl.period.closed")).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole("button", { name: /Coba Ulang|Retry/i }));
    await waitFor(() => {
      expect(retrySpy).toHaveBeenCalledWith(
        "11111111-1111-1111-1111-111111111111",
        "22222222-2222-2222-2222-222222222222",
        "msg-1",
      );
    });

    // Switch to Audit tab
    fireEvent.click(screen.getByRole("tab", { name: /Jejak Audit|Audit Trail/i }));
    expect(screen.getByText(/Attached DOC-2026-00001/i)).toBeInTheDocument();
  });

  it("hides upload and retry buttons in read-only mode", async () => {
    vi.spyOn(api, "fetchDocumentsOverview").mockResolvedValue({
      ...BASE_OVERVIEW,
      canUpload: false,
      canRetryOutbox: false,
    });

    render(
      <LocaleProvider>
        <DocumentsAndEvidencePage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Mode baca saja|Read-only mode/i)).toBeInTheDocument();
    });

    expect(screen.queryByRole("button", { name: /Unggah & Tautkan|Upload & Link/i })).toBeNull();
  });
});
