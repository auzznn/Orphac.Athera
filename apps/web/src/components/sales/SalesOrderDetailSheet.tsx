"use client";

import React, { useState } from "react";
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Edit3,
  FilePlus2,
  Lock,
  Send,
  ShieldCheck,
  X,
  XCircle,
} from "lucide-react";
import {
  cancelSalesOrder,
  closeSalesOrder,
  createSalesOrderAmendment,
  decideSalesOrderRevision,
  SalesOrderDetailDto,
  SalesOrderRevisionDto,
  submitSalesOrderRevision,
} from "@/lib/api/client";
import { formatIdr, formatJakartaDateTime } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";

export function SalesStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "APPROVED" || status === "FULLY_DISPATCHED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "REJECTED" || status === "CANCELLED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-danger-text)]">
        <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "SUPERSEDED" || status === "CLOSED" || status === "SHORT_CLOSED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-medium text-[var(--text-secondary)]">
        <Lock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
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

export function SalesOrderDetailSheet({
  tenantId,
  entityId,
  detail,
  currentUserId,
  canManage,
  canApprove,
  onClose,
  onUpdated,
  onEditDraft,
  mapError,
}: {
  tenantId: string;
  entityId: string;
  detail: SalesOrderDetailDto;
  currentUserId: string;
  canManage: boolean;
  canApprove: boolean;
  onClose: () => void;
  onUpdated: (updated: SalesOrderDetailDto) => void;
  onEditDraft: (detail: SalesOrderDetailDto, revision: SalesOrderRevisionDto) => void;
  mapError: (err: unknown) => string;
}) {
  const { locale, t } = useLocale();
  const st = t.salesOrders;
  const dt = st.detail;

  const [selectedRevId, setSelectedRevId] = useState<string>(() => detail.revisions[0]?.revisionId ?? "");
  const [decisionReason, setDecisionReason] = useState("");
  const [amendReason, setAmendReason] = useState("");
  const [closeReason, setCloseReason] = useState("");
  const [busy, setBusy] = useState<"submit" | "approve" | "reject" | "amend" | "close" | "cancel" | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const activeRev =
    detail.revisions.find((r) => r.revisionId === selectedRevId) ?? detail.revisions[0] ?? null;

  if (!activeRev) return null;

  const hasOpenRevision = detail.revisions.some(
    (r) => r.status === "DRAFT" || r.status === "PENDING_APPROVAL",
  );

  const isSelfActor =
    activeRev.submittedByUserId?.toLowerCase() === currentUserId.trim().toLowerCase() ||
    activeRev.approval?.requestedByUserId?.toLowerCase() === currentUserId.trim().toLowerCase();

  const handleSubmit = async () => {
    setBusy("submit");
    setActionError(null);
    try {
      const updated = await submitSalesOrderRevision(
        tenantId,
        entityId,
        detail.salesOrderId,
        activeRev.revisionId,
      );
      setSelectedRevId(updated.revisions[0]?.revisionId ?? activeRev.revisionId);
      onUpdated(updated);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  const handleDecide = async (decision: "APPROVED" | "REJECTED") => {
    if (!decisionReason.trim()) return;
    setBusy(decision === "APPROVED" ? "approve" : "reject");
    setActionError(null);
    try {
      const updated = await decideSalesOrderRevision(
        tenantId,
        entityId,
        detail.salesOrderId,
        activeRev.revisionId,
        { decision, reason: decisionReason.trim() },
      );
      setDecisionReason("");
      setSelectedRevId(updated.revisions[0]?.revisionId ?? activeRev.revisionId);
      onUpdated(updated);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  const handleCreateAmendment = async () => {
    if (!amendReason.trim()) return;
    setBusy("amend");
    setActionError(null);
    try {
      const updated = await createSalesOrderAmendment(tenantId, entityId, detail.salesOrderId, {
        changeReason: amendReason.trim(),
      });
      setAmendReason("");
      const newDraft = updated.revisions[0];
      setSelectedRevId(newDraft?.revisionId ?? "");
      onUpdated(updated);
      if (newDraft && newDraft.status === "DRAFT") {
        onEditDraft(updated, newDraft);
      }
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  const handleCloseOrCancel = async (isClose: boolean) => {
    if (!closeReason.trim()) return;
    setBusy(isClose ? "close" : "cancel");
    setActionError(null);
    try {
      const updated = isClose
        ? await closeSalesOrder(tenantId, entityId, detail.salesOrderId, { reason: closeReason.trim() })
        : await cancelSalesOrder(tenantId, entityId, detail.salesOrderId, { reason: closeReason.trim() });
      setCloseReason("");
      onUpdated(updated);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusy(null);
    }
  };

  return (
    <section
      id="so-detail-sheet"
      aria-label={dt.titlePrefix}
      className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
    >
      {/* Top Strip */}
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
        <div className="flex flex-wrap items-center gap-2">
          <ShieldCheck className="h-4 w-4 shrink-0 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">
            {detail.soNumber ?? detail.draftCode}
          </h2>
          <SalesStatusBadge
            status={detail.status}
            label={st.statusLabels[detail.status] ?? detail.status}
          />
          <SalesStatusBadge
            status={activeRev.status}
            label={`${st.tables.revPrefix} ${activeRev.revisionNumber} · ${st.revisionStatusLabels[activeRev.status] ?? activeRev.status}`}
          />
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {detail.revisions.length > 1 ? (
            <label className="inline-flex items-center gap-1.5 text-xs text-[var(--text-secondary)]">
              <span>{dt.selectRevLabel}</span>
              <select
                id="so-rev-select"
                value={activeRev.revisionId}
                onChange={(e) => setSelectedRevId(e.target.value)}
                className="font-code min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
              >
                {detail.revisions.map((r) => (
                  <option key={r.revisionId} value={r.revisionId}>
                    {st.tables.revPrefix} {r.revisionNumber} ({st.revisionStatusLabels[r.status] ?? r.status})
                  </option>
                ))}
              </select>
            </label>
          ) : null}
          <button
            type="button"
            id="so-detail-close"
            onClick={onClose}
            aria-label={dt.closeLabel}
            className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <X className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
          </button>
        </div>
      </div>

      {/* Header 3-Column Hairline Grid */}
      <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] border-b border-[var(--border-hairline)] md:grid-cols-3 md:divide-x md:divide-y-0">
        <div className="p-3.5">
          <div className="text-[11px] font-semibold text-[var(--text-secondary)]">{dt.headerCustomer}</div>
          <div className="mt-1 text-xs font-medium text-[var(--text-primary)]">
            <span className="font-code font-bold">{activeRev.customerCodeSnapshot}</span> · {activeRev.customerLegalNameSnapshot}
          </div>
          <div className="mt-0.5 text-xs text-[var(--text-secondary)]">{activeRev.deliveryAddressSnapshot}</div>
          {activeRev.customerNpwpSnapshot ? (
            <div className="mt-1 font-code text-[11px] tabular-nums text-[var(--text-muted)]">
              NPWP {activeRev.customerNpwpSnapshot}
            </div>
          ) : null}
        </div>

        <div className="p-3.5">
          <div className="text-[11px] font-semibold text-[var(--text-secondary)]">{dt.headerScheduleTerms}</div>
          <div className="mt-1 text-xs text-[var(--text-primary)]">
            <span className="font-code tabular-nums">{activeRev.orderDate}</span> ·{" "}
            <span className="font-code tabular-nums">{activeRev.paymentTermsDays}</span> {dt.termsSuffix}
          </div>
          {activeRev.requestedDeliveryDate ? (
            <div className="mt-0.5 text-xs text-[var(--text-secondary)]">
              <span>{dt.requestedPrefix} </span>
              <span className="font-code tabular-nums">{activeRev.requestedDeliveryDate}</span>
            </div>
          ) : null}
          {activeRev.customerReference ? (
            <div className="mt-0.5 text-[11px] text-[var(--text-secondary)]">
              <span>{dt.refPrefix} </span>
              <span className="font-code">{activeRev.customerReference}</span>
            </div>
          ) : null}
          {activeRev.approval ? (
            <div className="mt-1 text-[11px] text-[var(--text-muted)]">
              <span>{st.tables.submittedByPrefix} </span>
              <span className="font-code">{activeRev.approval.requestedByEmail}</span>
              {activeRev.approval.decidedByEmail ? (
                <span>
                  {" · "}
                  {dt.approvedByPrefix} <span className="font-code">{activeRev.approval.decidedByEmail}</span>
                </span>
              ) : null}
            </div>
          ) : null}
        </div>

        <div className="p-3.5">
          <div className="text-[11px] font-semibold text-[var(--text-secondary)]">{dt.headerAmounts}</div>
          <div className="mt-1 font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(activeRev.grandTotalAmount)}
          </div>
          <div className="mt-0.5 text-[11px] text-[var(--text-secondary)]">
            <span>{dt.subtotalPrefix} </span>
            <span className="font-code tabular-nums">{formatIdr(activeRev.subtotalAmount)}</span>
            <span> · {dt.taxPrefix} </span>
            <span className="font-code tabular-nums">{formatIdr(activeRev.outputTaxTotalAmount)}</span>
          </div>
          <div className="mt-1 font-code text-[11px] text-[var(--text-muted)]" title={activeRev.snapshotHash}>
            {dt.hashLabel}: {activeRev.snapshotHash.slice(0, 16)}…
          </div>
        </div>
      </div>

      {/* Body Sections */}
      <div className="space-y-4 p-4">
        {activeRev.status === "DRAFT" ? (
          <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]">
            <Clock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{dt.approvalBannerDraft}</span>
          </div>
        ) : null}

        {activeRev.status === "PENDING_APPROVAL" ? (
          <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{dt.approvalBannerPending}</span>
          </div>
        ) : null}

        {actionError ? (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
          >
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{actionError}</span>
          </div>
        ) : null}

        {/* Lines Table */}
        <div>
          <h3 className="mb-2 text-xs font-semibold text-[var(--text-primary)]">{dt.linesTitle}</h3>
          <div className="overflow-hidden rounded-[var(--radius-sm)] border border-[var(--border-hairline)] md:overflow-x-auto">
            <table id="so-detail-lines-table" className="w-full border-collapse text-left text-xs">
              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="py-2 md:pl-3 md:pr-2">{dt.colLineItem}</th>
                  <th scope="col" className="py-2 md:px-2">{dt.colLineQtyBase}</th>
                  <th scope="col" className="py-2 md:px-2 text-right">{dt.colLinePriceTax}</th>
                  <th scope="col" className="py-2 md:pl-2 md:pr-3 text-right">{dt.colLineTotal}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border-hairline)]">
                {activeRev.lines.map((line) => (
                  <tr key={line.soLineId} className="block p-3 md:table-row md:p-0">
                    <td className="block py-1 md:table-cell md:py-2 md:pl-3 md:pr-2">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colLineItem}</span>
                      <div className="font-medium text-[var(--text-primary)]">
                        <span className="font-code font-bold">{line.itemCodeSnapshot}</span> · {line.itemNameSnapshot}
                      </div>
                      {line.lineDescription ? (
                        <div className="text-[11px] text-[var(--text-secondary)]">{line.lineDescription}</div>
                      ) : null}
                    </td>
                    <td className="block py-1 md:table-cell md:py-2 md:px-2">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colLineQtyBase}</span>
                      <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                        {line.orderedQuantity} {line.orderedUomCodeSnapshot}
                      </div>
                      <div className="text-[11px] text-[var(--text-secondary)]">
                        <span>{st.tables.baseQtyShort} </span>
                        <span className="font-code tabular-nums">
                          {line.orderedBaseQuantity} {line.baseUomCodeSnapshot}
                        </span>
                      </div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2 md:px-2 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colLinePriceTax}</span>
                      <div className="font-code tabular-nums text-[var(--text-primary)]">{formatIdr(line.unitPrice)}</div>
                      <div className="text-[11px] text-[var(--text-secondary)]">
                        {line.taxCodeSnapshot ? (
                          <span>
                            <span className="font-code">{line.taxCodeSnapshot}</span> ({formatIdr(line.taxAmount)})
                          </span>
                        ) : (
                          <span>{st.composer.noTaxOption}</span>
                        )}
                      </div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2 md:pl-2 md:pr-3 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colLineTotal}</span>
                      <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                        {formatIdr(line.lineTotalAmount)}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Revision Diff vs Baseline */}
        {activeRev.diffFromBaseline ? (
          <div
            id="so-revision-diff"
            className="overflow-hidden rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)]"
          >
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] px-3 py-2 text-xs font-semibold text-[var(--text-primary)]">
              <span>
                {dt.diffTitle} ({st.tables.revPrefix} {activeRev.diffFromBaseline.baselineRevisionNumber} →{" "}
                {st.tables.revPrefix} {activeRev.diffFromBaseline.targetRevisionNumber})
              </span>
              <span className="font-code tabular-nums text-[var(--accent-primary)]">
                {dt.diffGrandTotalDelta}: {formatIdr(activeRev.diffFromBaseline.grandTotalDelta)} ·{" "}
                {dt.diffBaseQtyDelta}: {activeRev.diffFromBaseline.orderedBaseQtyDelta}
              </span>
            </div>
            <div className="space-y-2 p-3 text-xs">
              {activeRev.diffFromBaseline.headerChanges.length === 0 ? (
                <p className="text-[var(--text-secondary)]">{dt.diffNoHeaderChanges}</p>
              ) : (
                <div className="space-y-1">
                  <div className="font-semibold text-[var(--text-secondary)]">{dt.diffHeaderTitle}</div>
                  {activeRev.diffFromBaseline.headerChanges.map((hc, idx) => (
                    <div key={`${hc.fieldKey}-${idx}`} className="text-[var(--text-primary)]">
                      <span className="font-semibold">{dt.diffFieldNames[hc.fieldKey] ?? hc.fieldKey}: </span>
                      <span className="text-[var(--text-muted)] line-through">{hc.previousValue ?? "—"}</span>
                      {" → "}
                      <span>{hc.currentValue ?? "—"}</span>
                    </div>
                  ))}
                </div>
              )}

              <div className="divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)] pt-2">
                {activeRev.diffFromBaseline.lineChanges.map((lc) => (
                  <div key={lc.lineageLineKey} className="flex flex-wrap items-center justify-between gap-2 py-1.5">
                    <div>
                      <span className="font-code font-bold text-[var(--text-primary)]">{lc.lineageLineKey}</span>{" "}
                      <span className="rounded bg-[var(--bg-surface)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--text-secondary)]">
                        {dt.diffChangeLabels[lc.changeKind] ?? lc.changeKind}
                      </span>{" "}
                      <span className="font-code text-[var(--text-primary)]">
                        {lc.currentItemCode ?? lc.previousItemCode}
                      </span>
                    </div>
                    <div className="font-code tabular-nums text-[var(--text-secondary)]">
                      {lc.previousQuantity ?? 0} {lc.previousUomCode ?? ""} → {lc.currentQuantity ?? 0}{" "}
                      {lc.currentUomCode ?? ""} ({formatIdr(lc.currentSubtotal ?? 0)})
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        ) : null}

        {/* State Actions */}
        {activeRev.status === "DRAFT" && canManage ? (
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border-hairline)] pt-3">
            <button
              type="button"
              id="so-edit-draft-btn"
              onClick={() => onEditDraft(detail, activeRev)}
              className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              <Edit3 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
              <span>{dt.editDraftCta}</span>
            </button>
            <button
              type="button"
              id="so-submit-btn"
              disabled={busy !== null}
              onClick={handleSubmit}
              className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
            >
              <Send className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
              <span>{busy === "submit" ? dt.submitting : dt.submitCta}</span>
            </button>
          </div>
        ) : null}

        {activeRev.status === "PENDING_APPROVAL" ? (
          <div className="space-y-3 border-t border-[var(--border-hairline)] pt-3">
            {isSelfActor ? (
              <div
                id="so-sod-notice"
                data-testid="so-sod-notice"
                role="status"
                className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]"
              >
                <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{dt.sodHint}</span>
              </div>
            ) : !canApprove ? (
              <p className="text-xs text-[var(--text-secondary)]">{dt.noApprovePermHint}</p>
            ) : (
              <div className="space-y-2.5">
                <label className="block space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                    {dt.decisionReasonLabel}
                  </span>
                  <input
                    id="so-decision-reason"
                    type="text"
                    value={decisionReason}
                    placeholder={dt.decisionReasonPlaceholder}
                    onChange={(e) => setDecisionReason(e.target.value)}
                    className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                  />
                </label>
                <div className="flex flex-wrap items-center justify-end gap-2">
                  <button
                    type="button"
                    id="so-reject-btn"
                    disabled={busy !== null || !decisionReason.trim()}
                    onClick={() => handleDecide("REJECTED")}
                    className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                  >
                    <XCircle className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                    <span>{busy === "reject" ? dt.deciding : dt.rejectCta}</span>
                  </button>
                  <button
                    type="button"
                    id="so-approve-btn"
                    disabled={busy !== null || !decisionReason.trim()}
                    onClick={() => handleDecide("APPROVED")}
                    className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                    <span>{busy === "approve" ? dt.deciding : dt.approveCta}</span>
                  </button>
                </div>
              </div>
            )}
          </div>
        ) : null}

        {detail.status === "APPROVED" && !hasOpenRevision && canManage ? (
          <div className="space-y-3 border-t border-[var(--border-hairline)] pt-3">
            <h3 className="text-xs font-semibold text-[var(--text-primary)]">{dt.amendTitle}</h3>
            <div className="flex flex-wrap items-end gap-3">
              <label className="block min-w-[240px] flex-1 space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {dt.amendReasonLabel}
                </span>
                <input
                  id="so-amend-reason"
                  type="text"
                  value={amendReason}
                  placeholder={dt.amendReasonPlaceholder}
                  onChange={(e) => setAmendReason(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <button
                type="button"
                id="so-amend-btn"
                disabled={busy !== null || !amendReason.trim()}
                onClick={handleCreateAmendment}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <FilePlus2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{busy === "amend" ? dt.amending : dt.amendCta}</span>
              </button>
            </div>
          </div>
        ) : null}

        {detail.status !== "CLOSED" && detail.status !== "CANCELLED" && canManage ? (
          <div className="space-y-2.5 border-t border-[var(--border-hairline)] pt-3">
            <h3 className="text-xs font-semibold text-[var(--text-primary)]">{dt.closeOrCancelTitle}</h3>
            <div className="flex flex-wrap items-end gap-3">
              <label className="block min-w-[240px] flex-1 space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {dt.closeOrCancelReasonLabel}
                </span>
                <input
                  id="so-close-cancel-reason"
                  type="text"
                  value={closeReason}
                  placeholder={dt.closeOrCancelReasonPlaceholder}
                  onChange={(e) => setCloseReason(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              {detail.status === "APPROVED" && !hasOpenRevision ? (
                <button
                  type="button"
                  id="so-close-btn"
                  disabled={busy !== null || !closeReason.trim()}
                  onClick={() => handleCloseOrCancel(true)}
                  className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                >
                  <Lock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  <span>{busy === "close" ? dt.processingAction : dt.closeCta}</span>
                </button>
              ) : null}
              <button
                type="button"
                id="so-cancel-btn"
                disabled={busy !== null || !closeReason.trim()}
                onClick={() => handleCloseOrCancel(false)}
                className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
              >
                <XCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{busy === "cancel" ? dt.processingAction : dt.cancelCta}</span>
              </button>
            </div>
          </div>
        ) : null}

        <div className="border-t border-[var(--border-hairline)] pt-2 text-[11px] text-[var(--text-muted)]">
          <span className="font-code">{formatJakartaDateTime(detail.updatedAtUtc, locale)}</span>
        </div>
      </div>
    </section>
  );
}
