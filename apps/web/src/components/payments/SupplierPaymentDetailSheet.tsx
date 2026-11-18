"use client";

import React, { useState } from "react";
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  FileText,
  Lock,
  RotateCcw,
  ShieldCheck,
  X,
} from "lucide-react";
import {
  rejectReleasedSupplierPayment,
  releaseSupplierPayment,
  simulateSupplierPaymentFile,
  submitSupplierPaymentForRelease,
  SupplierPaymentDetailResponse,
} from "@/lib/api/client";
import { formatIdr, formatJakartaDateTime } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";

function makeIdempotencyKey(prefix: string): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

export function todayJakartaDate(): string {
  try {
    const formatted = new Intl.DateTimeFormat("en-CA", {
      timeZone: "Asia/Jakarta",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    }).format(new Date());
    if (/^\d{4}-\d{2}-\d{2}$/.test(formatted)) return formatted;
  } catch {
    // Fallback cleanly
  }
  return new Date().toISOString().slice(0, 10);
}

export function PaymentStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "RECONCILED" || status === "POSTED" || status === "MATCHED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "BANK_REJECTED" || status === "CANCELLED" || status === "REVERSED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-danger-text)]">
        <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "RELEASED" || status === "PARTIALLY_RECONCILED" || status === "PARTIALLY_MATCHED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-warn-text)]">
        <RotateCcw className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-warn-text)]">
      <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

export function SupplierPaymentDetailSheet({
  tenantId,
  entityId,
  detail,
  currentUserEmail,
  canPrepare,
  canRelease,
  canReconcile,
  onClose,
  onUpdated,
  mapError,
}: {
  tenantId: string;
  entityId: string;
  detail: SupplierPaymentDetailResponse;
  currentUserEmail: string;
  canPrepare: boolean;
  canRelease: boolean;
  canReconcile: boolean;
  onClose: () => void;
  onUpdated: (updated: SupplierPaymentDetailResponse) => void;
  mapError: (err: unknown) => string;
}) {
  const { locale, t } = useLocale();
  const pt = t.payments;
  const dt = pt.detail;
  const p = detail.payment;

  const [releaseRef, setReleaseRef] = useState("");
  const [releaseDate, setReleaseDate] = useState(todayJakartaDate);
  const [rejectReason, setRejectReason] = useState("");
  const [busy, setBusy] = useState<"simulate" | "submit" | "release" | "reject" | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const normalizedMe = currentUserEmail.trim().toLowerCase();
  const isSameActor =
    (p.preparedByEmail && p.preparedByEmail.trim().toLowerCase() === normalizedMe) ||
    (p.submittedByEmail && p.submittedByEmail.trim().toLowerCase() === normalizedMe);

  const handleSimulateFile = async () => {
    setBusy("simulate");
    setActionError(null);
    try {
      const updated = await simulateSupplierPaymentFile(tenantId, entityId, p.supplierPaymentId, { formatCode: null });
      onUpdated(updated);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  const handleSubmit = async () => {
    setBusy("submit");
    setActionError(null);
    try {
      const updated = await submitSupplierPaymentForRelease(tenantId, entityId, p.supplierPaymentId);
      onUpdated(updated);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  const handleRelease = async () => {
    if (!releaseRef.trim()) {
      setActionError(pt.errors.release_reference_required);
      return;
    }
    setBusy("release");
    setActionError(null);
    try {
      const updated = await releaseSupplierPayment(tenantId, entityId, p.supplierPaymentId, {
        postingDate: releaseDate,
        releaseReference: releaseRef.trim(),
        idempotencyKey: makeIdempotencyKey("pay-rel"),
      });
      onUpdated(updated);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  const handleRejectReleased = async () => {
    if (!rejectReason.trim()) return;
    setBusy("reject");
    setActionError(null);
    try {
      const updated = await rejectReleasedSupplierPayment(tenantId, entityId, p.supplierPaymentId, {
        reversalPostingDate: todayJakartaDate(),
        reason: rejectReason.trim(),
      });
      onUpdated(updated);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  const stageBannerText =
    p.status === "DRAFT" || p.status === "PENDING_RELEASE"
      ? dt.stageBannerSimulated
      : p.status === "RECONCILED"
        ? dt.stageBannerReconciled
        : p.status === "BANK_REJECTED"
          ? dt.stageBannerRejected
          : dt.stageBannerReleased;

  const isStageOk = p.status === "RECONCILED";
  const isStageDanger = p.status === "BANK_REJECTED";

  return (
    <section
      id="pay-detail-sheet"
      aria-label={`${dt.sheetLabel} ${p.paymentNumber ?? p.draftCode}`}
      className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
    >
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
        <div className="flex flex-wrap items-center gap-2">
          <ShieldCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="font-code text-sm font-semibold tabular-nums text-[var(--text-primary)]">
            {p.paymentNumber ?? p.draftCode}
          </h2>
          <PaymentStatusBadge status={p.status} label={pt.statusLabels[p.status] ?? p.status} />
          {p.latestInstructionCode ? (
            <span className="font-code inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-2 py-0.5 text-[11px] font-medium text-[var(--text-secondary)]">
              <FileText className="h-3 w-3" strokeWidth={1.75} aria-hidden="true" />
              {p.latestInstructionCode}
            </span>
          ) : null}
        </div>
        <button
          type="button"
          id="pay-detail-close"
          onClick={onClose}
          aria-label={dt.closeLabel}
          className="inline-flex min-h-9 min-w-9 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <X className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
        </button>
      </div>

      {/* Header 3-column hairline grid */}
      <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] border-b border-[var(--border-hairline)] md:grid-cols-3 md:divide-x md:divide-y-0">
        <div className="p-3.5">
          <div className="text-[11px] font-semibold text-[var(--text-secondary)]">{dt.headerPayment}</div>
          <div className="mt-1 font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
            {p.paymentNumber ?? p.draftCode}
          </div>
          <div className="mt-0.5 text-[11px] text-[var(--text-secondary)]">
            <span className="font-code tabular-nums">{p.paymentDate}</span> ·{" "}
            <span>{pt.methodLabels[p.paymentMethod] ?? p.paymentMethod}</span>
          </div>
          {p.preparedByEmail ? (
            <div className="mt-1 text-[11px] text-[var(--text-muted)]">
              <span>{pt.preparedByPrefix} </span>
              <span className="font-code">{p.preparedByEmail}</span>
            </div>
          ) : null}
          {p.releasedByEmail ? (
            <div className="text-[11px] text-[var(--text-muted)]">
              <span>{pt.releasedByPrefix} </span>
              <span className="font-code">{p.releasedByEmail}</span>
            </div>
          ) : null}
        </div>

        <div className="p-3.5">
          <div className="text-[11px] font-semibold text-[var(--text-secondary)]">{dt.headerBankBeneficiary}</div>
          <div className="mt-1 text-xs font-medium text-[var(--text-primary)]">
            <span className="font-code">{p.bankAccountCode}</span> · {p.bankName}
          </div>
          <div className="mt-0.5 text-xs text-[var(--text-secondary)]">
            <span className="font-code">{p.supplierCode}</span> · {p.supplierLegalName}
          </div>
          <div className="mt-1 text-[11px] text-[var(--text-muted)]">
            <span>{p.beneficiaryBankName} · </span>
            <span className="font-code tabular-nums">{p.beneficiaryAccountNumber}</span>{" "}
            <span>({p.beneficiaryAccountHolder})</span>
          </div>
        </div>

        <div className="p-3.5">
          <div className="text-[11px] font-semibold text-[var(--text-secondary)]">{dt.headerAmounts}</div>
          <div className="mt-1 font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(p.totalAllocatedAmount)}
          </div>
          <div className="mt-0.5 text-[11px] text-[var(--text-secondary)]">
            <span>{pt.clearingOpenShort} </span>
            <span className="font-code tabular-nums">{formatIdr(p.remainingClearingAmount)}</span>
            <span> · {pt.settledShort} </span>
            <span className="font-code tabular-nums">{formatIdr(p.reconciledAmount)}</span>
          </div>
          {p.releaseJournalNumber ? (
            <div className="mt-1 font-code text-[11px] text-[var(--text-primary)]">
              {dt.journalLabel}: {p.releaseJournalNumber} {p.releaseReference ? `(${p.releaseReference})` : ""}
            </div>
          ) : null}
          {p.rejectionReversalJournalNumber ? (
            <div className="font-code text-[11px] text-[var(--status-danger-text)]">
              {dt.reversalJournalLabel}: {p.rejectionReversalJournalNumber}
            </div>
          ) : null}
        </div>
      </div>

      {/* Explicit stage governance banner */}
      <div
        role="status"
        className={`flex items-start gap-2 border-b border-[var(--border-hairline)] px-4 py-2.5 text-xs ${
          isStageOk
            ? "bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
            : isStageDanger
              ? "bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
              : "bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
        }`}
      >
        {isStageOk ? (
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        ) : isStageDanger ? (
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        ) : (
          <Clock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        )}
        <span>{stageBannerText}</span>
      </div>

      {actionError ? (
        <div
          id="pay-detail-error"
          role="alert"
          className="flex items-start gap-2 border-b border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-2.5 text-xs text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{actionError}</span>
        </div>
      ) : null}

      {/* Payable Allocations */}
      <div className="border-b border-[var(--border-hairline)]">
        <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
          {dt.allocationsTitle}
        </div>
        <div className="md:overflow-x-auto">
          <table className="w-full border-collapse text-left text-xs">
            <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
              <tr>
                <th scope="col" className="py-2 md:pl-4 md:pr-2">{dt.colPayableNo}</th>
                <th scope="col" className="py-2 md:px-2">{dt.colInvoiceNo}</th>
                <th scope="col" className="py-2 md:px-2 text-right">{dt.colBefore}</th>
                <th scope="col" className="py-2 md:px-2 text-right">{dt.colAllocated}</th>
                <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{dt.colAfter}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border-hairline)]">
              {detail.allocations.map((a) => (
                <tr key={a.paymentAllocationId} className="block p-3 md:table-row md:p-0">
                  <td className="block py-1 md:table-cell md:py-2 md:pl-4 md:pr-2">
                    <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colPayableNo}</span>
                    <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{a.payableNumber}</span>
                  </td>
                  <td className="block py-1 md:table-cell md:py-2 md:px-2">
                    <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colInvoiceNo}</span>
                    <span className="font-code tabular-nums text-[var(--text-primary)]">{a.supplierInvoiceNumber}</span>
                  </td>
                  <td className="block py-1 md:table-cell md:py-2 md:px-2 md:text-right">
                    <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colBefore}</span>
                    <span className="font-code tabular-nums text-[var(--text-secondary)]">{formatIdr(a.payableOpenBeforeAmount)}</span>
                  </td>
                  <td className="block py-1 md:table-cell md:py-2 md:px-2 md:text-right">
                    <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colAllocated}</span>
                    <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">{formatIdr(a.allocatedAmount)}</span>
                  </td>
                  <td className="block py-1 md:table-cell md:py-2 md:pl-2 md:pr-4 md:text-right">
                    <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colAfter}</span>
                    <span className="font-code tabular-nums text-[var(--text-primary)]">{formatIdr(a.payableRemainingAfterAmount)}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Simulated Instructions (OD-06) */}
      <div className="border-b border-[var(--border-hairline)] p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-xs font-semibold text-[var(--text-primary)]">{dt.instructionsTitle}</h3>
          <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-warn-text)]">
            <AlertCircle className="h-3 w-3" strokeWidth={1.75} aria-hidden="true" />
            <span>{dt.simulationWarningBadge}</span>
          </span>
        </div>

        {detail.instructions.length === 0 ? (
          <p className="mt-2 text-xs text-[var(--text-secondary)]">{dt.noInstructions}</p>
        ) : (
          <div className="mt-2 divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)]">
            {detail.instructions.map((inst) => (
              <div key={inst.paymentInstructionId} className="py-2.5">
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                    {inst.instructionNumber} · {inst.formatCode} · {formatIdr(inst.totalAmount)}
                  </span>
                  <span className="font-code text-[11px] text-[var(--text-muted)]">
                    SHA256 {inst.payloadSha256.slice(0, 16)}… · {formatJakartaDateTime(inst.generatedAtUtc, locale)}
                  </span>
                </div>
                <pre className="mt-2 max-w-full overflow-x-auto whitespace-pre border-t border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-2.5 font-code text-[11px] leading-relaxed text-[var(--text-primary)]">
                  {inst.payloadText}
                </pre>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Linked Bank Matches */}
      {detail.matches.length > 0 ? (
        <div className="border-b border-[var(--border-hairline)] p-4">
          <h3 className="text-xs font-semibold text-[var(--text-primary)]">{dt.matchesSectionTitle}</h3>
          <div className="mt-2 divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)] text-xs">
            {detail.matches.map((m) => (
              <div key={m.bankPaymentMatchId} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <div>
                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{m.matchNumber}</span>
                  {" · "}
                  <span className="font-code tabular-nums text-[var(--text-secondary)]">
                    {m.statementNumber} / {m.bankReference}
                  </span>
                  {" · "}
                  <span className="font-code tabular-nums text-[var(--text-secondary)]">{m.clearingGlJournalNumber}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatIdr(m.matchedAmount)}
                  </span>
                  <PaymentStatusBadge status={m.status} label={pt.matchStatusLabels[m.status] ?? m.status} />
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : null}

      {/* Lifecycle Actions */}
      <div className="space-y-4 p-4">
        {p.status === "DRAFT" || p.status === "PENDING_RELEASE" ? (
          <div className="flex flex-wrap items-center gap-2.5">
            {canPrepare ? (
              <button
                type="button"
                id="pay-simulate-file-btn"
                disabled={busy !== null}
                onClick={handleSimulateFile}
                className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
              >
                <FileText className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{busy === "simulate" ? dt.simulatingFile : dt.simulateFileBtn}</span>
              </button>
            ) : null}

            {p.status === "DRAFT" && canPrepare ? (
              <button
                type="button"
                id="pay-submit-btn"
                disabled={busy !== null}
                onClick={handleSubmit}
                className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{busy === "submit" ? dt.submitting : dt.submitBtn}</span>
              </button>
            ) : null}
          </div>
        ) : null}

        {p.status === "PENDING_RELEASE" ? (
          <div className="space-y-2.5 border-t border-[var(--border-hairline)] pt-3">
            <h3 className="text-xs font-semibold text-[var(--text-primary)]">{dt.releaseTitle}</h3>
            {isSameActor ? (
              <div
                data-testid="pay-sod-notice"
                role="status"
                className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]"
              >
                <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{dt.sodHint}</span>
              </div>
            ) : !canRelease ? (
              <p className="text-xs text-[var(--text-secondary)]">{dt.noReleasePermHint}</p>
            ) : (
              <div className="flex flex-wrap items-end gap-3">
                <label className="block min-w-[220px] flex-1 space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{dt.releaseRefLabel}</span>
                  <input
                    id="pay-release-ref"
                    type="text"
                    value={releaseRef}
                    placeholder="BCA-PORTAL-20261119-01"
                    onChange={(e) => setReleaseRef(e.target.value)}
                    className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                  />
                </label>
                <label className="block space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{dt.postingDateLabel}</span>
                  <input
                    id="pay-release-date"
                    type="date"
                    value={releaseDate}
                    onChange={(e) => setReleaseDate(e.target.value)}
                    className="font-code min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                  />
                </label>
                <button
                  type="button"
                  id="pay-release-btn"
                  disabled={busy !== null}
                  onClick={handleRelease}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                >
                  <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                  <span>{busy === "release" ? dt.releasing : dt.releaseBtn}</span>
                </button>
              </div>
            )}
          </div>
        ) : null}

        {p.status === "RELEASED" && Number(p.reconciledAmount) === 0 && canReconcile ? (
          <div className="space-y-2.5 border-t border-[var(--border-hairline)] pt-3">
            <h3 className="text-xs font-semibold text-[var(--text-primary)]">{dt.rejectTitle}</h3>
            <div className="flex flex-wrap items-end gap-3">
              <label className="block min-w-[260px] flex-1 space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{dt.rejectReasonLabel}</span>
                <input
                  id="pay-reject-reason"
                  type="text"
                  value={rejectReason}
                  onChange={(e) => setRejectReason(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <button
                type="button"
                id="pay-reject-btn"
                disabled={busy !== null || !rejectReason.trim()}
                onClick={handleRejectReleased}
                className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
              >
                <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{busy === "reject" ? dt.rejecting : dt.rejectBtn}</span>
              </button>
            </div>
          </div>
        ) : null}
      </div>
    </section>
  );
}
