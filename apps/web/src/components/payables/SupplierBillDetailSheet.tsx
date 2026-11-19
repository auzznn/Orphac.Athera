"use client";

import React, { useEffect, useRef, useState } from "react";
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Lock,
  Receipt,
  Scale,
  ShieldCheck,
  X,
} from "lucide-react";
import {
  ApiProblemError,
  decideSupplierBill,
  postSupplierBill,
  submitSupplierBill,
  SupplierBillDetailResponse,
  updateSupplierBillPaymentBlock,
} from "@/lib/api/client";
import { formatIdr, formatJakartaDateTime, formatUnitCostIdr } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatQuantityValue } from "@/lib/receivingMath";

function makeIdempotencyKey(prefix: string): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

function todayJakartaDate(): string {
  try {
    const formatted = new Intl.DateTimeFormat("en-CA", {
      timeZone: "Asia/Jakarta",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    }).format(new Date());
    if (/^\d{4}-\d{2}-\d{2}$/.test(formatted)) return formatted;
  } catch {
    // Fallback to open seeded fiscal period
  }
  return "2026-11-10";
}

export function MatchStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "MATCHED" || status === "WITHIN_TOLERANCE") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "POSTING_BLOCKED" || status === "BLOCKED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-danger-text)]">
        <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-warn-text)]">
      <Scale className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

export function BillStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "POSTED" || status === "APPROVED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "CANCELLED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-danger-text)]">
        <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
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

export function PaymentBlockBadge({ status, label }: { status: string; label: string }) {
  if (status === "PAYMENT_BLOCKED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-danger-text)]">
        <Lock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
      <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

interface SupplierBillDetailSheetProps {
  tenantId: string;
  entityId: string;
  detail: SupplierBillDetailResponse;
  currentUserEmail: string;
  canManage: boolean;
  canApprove: boolean;
  canPost: boolean;
  onEditDraft?: () => void;
  onClose: () => void;
  onAuthExpired: () => void;
  onActionCompleted: (updatedDetail: SupplierBillDetailResponse) => Promise<void> | void;
}

export function SupplierBillDetailSheet({
  tenantId,
  entityId,
  detail,
  currentUserEmail,
  canManage,
  canApprove,
  canPost,
  onEditDraft,
  onClose,
  onAuthExpired,
  onActionCompleted,
}: SupplierBillDetailSheetProps) {
  const { locale, t } = useLocale();
  const pt = t.payables;
  const dt = pt.detail;

  const bill = detail.bill;
  const [busyAction, setBusyAction] = useState<"submit" | "decide" | "post" | "block" | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [approveVariance, setApproveVariance] = useState(
    bill.matchStatus !== "APPROVAL_REQUIRED",
  );
  const [applyBlockOnDecision, setApplyBlockOnDecision] = useState(false);
  const [decisionReason, setDecisionReason] = useState("");
  const [decisionBlockReason, setDecisionBlockReason] = useState("");

  const [postingDate, setPostingDate] = useState(() =>
    bill.invoiceDate >= "2026-11-01" && bill.invoiceDate <= "2026-11-30"
      ? bill.invoiceDate
      : todayJakartaDate(),
  );

  const [postReviewOpen, setPostReviewOpen] = useState(false);
  const postReviewTrigger = useRef<HTMLButtonElement>(null);
  const cancelPostReview = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (postReviewOpen) cancelPostReview.current?.focus();
  }, [postReviewOpen]);

  const [blockReason, setBlockReason] = useState("");

  const isSubmitter = Boolean(
    bill.submittedByEmail &&
      currentUserEmail &&
      bill.submittedByEmail.toLowerCase() === currentUserEmail.toLowerCase(),
  );
  const needsVarianceAck = bill.matchStatus === "APPROVAL_REQUIRED";
  const isBlockedMatch = bill.matchStatus === "POSTING_BLOCKED";
  const blockingReasons =
    detail.liveComparison?.blockingReasons && detail.liveComparison.blockingReasons.length > 0
      ? detail.liveComparison.blockingReasons
      : (detail.evaluations[detail.evaluations.length - 1]?.blockingReasons ?? []);

  function mapError(err: unknown): string | null {
    const apiErr = err as ApiProblemError;
    if (apiErr?.status === 401) {
      onAuthExpired();
      return null;
    }
    return (
      (apiErr?.errorCode && pt.errors[apiErr.errorCode]) ||
      apiErr?.detail ||
      pt.loadErrorTitle
    );
  }

  async function handleSubmit() {
    setBusyAction("submit");
    setActionError(null);
    try {
      const updated = await submitSupplierBill(tenantId, entityId, bill.supplierBillId);
      await onActionCompleted(updated);
    } catch (err) {
      const msg = mapError(err);
      if (msg) setActionError(msg);
    } finally {
      setBusyAction(null);
    }
  }

  async function handleDecide(decision: "APPROVED" | "REJECTED") {
    setBusyAction("decide");
    setActionError(null);
    try {
      const updated = await decideSupplierBill(tenantId, entityId, bill.supplierBillId, {
        decision,
        reason: decisionReason.trim() || (decision === "APPROVED" ? dt.defaultApproveReason : dt.defaultRejectReason),
        approvePriceVariance: decision === "APPROVED" ? (needsVarianceAck ? approveVariance : true) : false,
        applyPaymentBlock: decision === "APPROVED" ? applyBlockOnDecision : false,
        paymentBlockReason: decision === "APPROVED" && applyBlockOnDecision ? (decisionBlockReason.trim() || dt.defaultManualHoldReason) : null,
      });
      setDecisionReason("");
      await onActionCompleted(updated);
    } catch (err) {
      const msg = mapError(err);
      if (msg) setActionError(msg);
    } finally {
      setBusyAction(null);
    }
  }

  function dismissPostReview() {
    setPostReviewOpen(false);
    postReviewTrigger.current?.focus();
  }

  async function handlePost() {
    if (!postReviewOpen || !canPost || bill.status !== "APPROVED" || isBlockedMatch || !postingDate || busyAction !== null) return;
    setBusyAction("post");
    setActionError(null);
    try {
      const updated = await postSupplierBill(tenantId, entityId, bill.supplierBillId, {
        postingDate,
        idempotencyKey: makeIdempotencyKey("ap-post"),
      });
      await onActionCompleted(updated);
    } catch (err) {
      const msg = mapError(err);
      if (msg) setActionError(msg);
    } finally {
      setBusyAction(null);
    }
  }

  async function handleTogglePaymentBlock(targetBlockStatus: "PAYMENT_BLOCKED" | "RELEASED") {
    setBusyAction("block");
    setActionError(null);
    try {
      const updated = await updateSupplierBillPaymentBlock(tenantId, entityId, bill.supplierBillId, {
        targetBlockStatus,
        reason:
          blockReason.trim() ||
          (targetBlockStatus === "PAYMENT_BLOCKED"
            ? dt.defaultBlockHoldReason
            : dt.defaultReleaseReason),
      });
      setBlockReason("");
      await onActionCompleted(updated);
    } catch (err) {
      const msg = mapError(err);
      if (msg) setActionError(msg);
    } finally {
      setBusyAction(null);
    }
  }

  return (
    <section
      id="ap-bill-detail-sheet"
      aria-label={dt.sectionTitle}
      className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
    >
      {/* Header strip */}
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
        <div className="flex items-center gap-2">
          <Receipt className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-sm font-semibold text-[var(--text-primary)]">{dt.sectionTitle}</h2>
        </div>
        <div className="flex items-center gap-2">
          <BillStatusBadge
            status={bill.status}
            label={pt.billStatusLabels[bill.status] ?? bill.status}
          />
          <MatchStatusBadge
            status={bill.matchStatus}
            label={pt.matchStatusLabels[bill.matchStatus] ?? bill.matchStatus}
          />
          <PaymentBlockBadge
            status={bill.paymentBlockStatus}
            label={pt.paymentBlockLabels[bill.paymentBlockStatus] ?? bill.paymentBlockStatus}
          />
          <button
            type="button"
            onClick={onClose}
            aria-label={dt.closeButton}
            className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <X className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{dt.closeButton}</span>
          </button>
        </div>
      </div>

      {/* Hairline summary grid (no nested bordered cards) */}
      <div className="divide-y divide-[var(--border-hairline)]">
        <div className="grid grid-cols-1 gap-px bg-[var(--border-hairline)] sm:grid-cols-2 lg:grid-cols-4">
          <div className="bg-[var(--bg-surface)] px-4 py-3">
            <p className="text-[11px] font-medium text-[var(--text-secondary)]">{dt.headerRef}</p>
            <p className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--text-primary)]">
              {bill.billNumber ?? bill.draftCode}
            </p>
            <p className="font-code text-[11px] text-[var(--text-secondary)]">
              PO: {bill.poNumber} · {pt.micro.invShort}: {bill.supplierInvoiceNumber}
            </p>
            {bill.taxInvoiceNumber ? (
              <p className="font-code text-[11px] text-[var(--text-secondary)]">FP: {bill.taxInvoiceNumber}</p>
            ) : null}
          </div>

          <div className="bg-[var(--bg-surface)] px-4 py-3">
            <p className="text-[11px] font-medium text-[var(--text-secondary)]">{dt.headerVendor}</p>
            <p className="mt-0.5 text-sm font-semibold text-[var(--text-primary)]">{bill.supplierLegalName}</p>
            <p className="font-code text-[11px] text-[var(--text-secondary)]">
              {bill.supplierCode} · {pt.micro.invShort} {bill.invoiceDate} → {pt.micro.dueShort} {bill.dueDate}
            </p>
          </div>

          <div className="bg-[var(--bg-surface)] px-4 py-3">
            <p className="text-[11px] font-medium text-[var(--text-secondary)]">{dt.headerTotals}</p>
            <p className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--text-primary)]">
              {pt.composer.netApLabel}: {formatIdr(bill.netPayableAmount)}
            </p>
            <p className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
              GRNI: {formatIdr(bill.totalGrniClearAmount)} · {pt.micro.varShort}: {formatIdr(bill.totalPriceVarianceAmount)}
            </p>
            <p className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
              PPN: {formatIdr(bill.ppnTaxTotal)} · PPh: {formatIdr(bill.withholdingTaxTotal)}
            </p>
          </div>

          <div className="bg-[var(--bg-surface)] px-4 py-3">
            <p className="text-[11px] font-medium text-[var(--text-secondary)]">{pt.composer.colVarianceSplit}</p>
            <p className="font-code mt-0.5 text-xs font-semibold tabular-nums text-[var(--text-primary)]">
              {pt.composer.coveredStockLabel}: {formatIdr(bill.totalInventoryAdjAmount)}
            </p>
            <p className="font-code text-xs font-semibold tabular-nums text-[var(--text-primary)]">
              {pt.composer.uncoveredPpvLabel}: {formatIdr(bill.totalPnlVarianceAmount)}
            </p>
            {bill.postedJournalNumber ? (
              <p className="font-code mt-0.5 text-[11px] font-semibold text-[var(--accent-primary)]">
                GL: {bill.postedJournalNumber}
                {detail.payableItem ? ` · AP: ${detail.payableItem.payableNumber}` : ""}
              </p>
            ) : null}
          </div>
        </div>

        {/* Action error alert */}
        {actionError ? (
          <div className="px-4 py-3">
            <div
              role="alert"
              data-testid="ap-detail-error"
              className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
            >
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{actionError}</span>
            </div>
          </div>
        ) : null}

        {/* Billed Lines Table */}
        <div>
          <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
            {dt.linesTitle}
          </div>
          <div className="md:overflow-x-auto">
            <table className="w-full border-collapse text-left text-xs">
              <thead className="hidden border-y border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.composer.colOrderedAcceptedOpen}</th>
                  <th scope="col" className="py-2 md:px-2 text-right">{pt.composer.colBilledPrice}</th>
                  <th scope="col" className="py-2 md:px-2 text-right">{dt.colLineVarianceOutcome}</th>
                  <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.composer.colTaxes}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border-hairline)]">
                {detail.lines.map((line) => (
                  <tr key={line.supplierBillLineId} className="block p-3 md:table-row md:p-0">
                    <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                        {pt.composer.colOrderedAcceptedOpen}
                      </span>
                      <div className="font-semibold text-[var(--text-primary)]">
                        <span className="font-code mr-1.5">#{line.lineNumber}</span>
                        <span className="font-code">{line.itemCode}</span> — {line.itemName}
                      </div>
                      <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                        PO {formatQuantityValue(line.orderedBaseQty, locale)} {line.baseUomCode} @{" "}
                        {formatUnitCostIdr(line.poUnitPriceFunctional)} · {pt.micro.unbilledGrniShort}:{" "}
                        {formatQuantityValue(line.acceptedUnbilledQtySnapshot, locale)} {line.baseUomCode}
                      </div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{pt.composer.colBilledPrice}</span>
                      <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                        {formatQuantityValue(line.billedBaseQuantity, locale)} {line.baseUomCode} @{" "}
                        {formatUnitCostIdr(line.invoiceUnitPriceBase)}
                      </div>
                      <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                        {pt.micro.netShort}: {formatIdr(line.lineNetAmount)} (GRNI {formatIdr(line.grniClearAmount)})
                      </div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                        {dt.colLineVarianceOutcome}
                      </span>
                      <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                        {formatIdr(line.priceVarianceAmount)}
                      </div>
                      <div className="mt-1 flex flex-wrap gap-1 md:justify-end">
                        <MatchStatusBadge
                          status={line.dqOutcome}
                          label={`DQ: ${pt.dimensionOutcomeLabels[line.dqOutcome] ?? line.dqOutcome}`}
                        />
                        <MatchStatusBadge
                          status={line.ppOutcome}
                          label={`PP: ${pt.dimensionOutcomeLabels[line.ppOutcome] ?? line.ppOutcome}`}
                        />
                      </div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                        {pt.composer.colTaxes}
                      </span>
                      <div className="font-code tabular-nums text-[var(--text-primary)]">
                        {line.ppnTaxCodeSnapshot}: {formatIdr(line.ppnTaxAmount)}
                      </div>
                      {line.withholdingTaxCodeSnapshot ? (
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {line.withholdingTaxCodeSnapshot}:{" "}
                          <span className="whitespace-nowrap">-{formatIdr(line.withholdingTaxAmount)}</span>
                        </div>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* DEC-P2P-013 Stock Coverage Adjustments (when posted) */}
        {detail.priceAdjustments.length > 0 ? (
          <div>
            <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
              {dt.adjustmentsTitle}
            </div>
            <div className="divide-y divide-[var(--border-hairline)]">
              {detail.priceAdjustments.map((adj) => (
                <div key={adj.adjustmentId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs">
                  <div>
                    <span className="font-code font-bold text-[var(--text-primary)]">{adj.itemCode}</span>
                    <span className="ml-2 text-[var(--text-secondary)]">
                      {pt.micro.onHandBeforeBillShort}: {formatQuantityValue(adj.poolOnHandBeforeQty, locale)} · {pt.micro.coveredShort}:{" "}
                      {formatQuantityValue(adj.coveredBaseQuantity, locale)} / {pt.micro.uncoveredShort}:{" "}
                      {formatQuantityValue(adj.uncoveredBaseQuantity, locale)}
                    </span>
                  </div>
                  <div className="font-code tabular-nums text-right text-[var(--text-primary)]">
                    <span className="font-semibold">
                      {pt.micro.inv1300Short}: {formatIdr(adj.inventoryAdjustmentAmount)} · {pt.micro.ppv5200Short}: {formatIdr(adj.pnlVarianceAmount)}
                    </span>
                    <span className="ml-2 text-[11px] text-[var(--text-secondary)]">
                      {pt.micro.macShort} {formatUnitCostIdr(adj.previousMovingAverageCost)} → {formatUnitCostIdr(adj.newMovingAverageCost)}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : null}

        {/* Evaluation History & Tax Determinations */}
        <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] lg:grid-cols-2 lg:divide-x lg:divide-y-0">
          <div>
            <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
              {dt.evaluationsTitle}
            </div>
            <div className="divide-y divide-[var(--border-hairline)]">
              {detail.evaluations.map((ev) => (
                <div key={ev.evaluationId} className="px-4 py-2.5 text-xs">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-semibold text-[var(--text-primary)]">
                      {pt.evaluationStageLabels[ev.evaluationStage] ?? ev.evaluationStage}
                    </span>
                    <MatchStatusBadge
                      status={ev.overallMatchStatus}
                      label={pt.matchStatusLabels[ev.overallMatchStatus] ?? ev.overallMatchStatus}
                    />
                  </div>
                  <p className="font-code mt-1 text-[11px] text-[var(--text-secondary)]">
                    {ev.evaluatedByEmail} · {formatJakartaDateTime(ev.evaluatedAtUtc, locale)}
                  </p>
                  {ev.blockingReasons.length > 0 ? (
                    <p className="mt-1 text-[11px] text-[var(--status-danger-text)]">
                      {ev.blockingReasons.join(" | ")}
                    </p>
                  ) : null}
                </div>
              ))}
            </div>
          </div>

          <div>
            <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
              {dt.taxesTitle}
            </div>
            {detail.taxDeterminations.length === 0 ? (
              <div className="px-4 py-3 text-xs text-[var(--text-secondary)]">{pt.od07TaxDisclaimer}</div>
            ) : (
              <div className="divide-y divide-[var(--border-hairline)]">
                {detail.taxDeterminations.map((td) => (
                  <div key={td.determinationId} className="flex items-center justify-between gap-2 px-4 py-2.5 text-xs">
                    <div>
                      <span className="font-semibold text-[var(--text-primary)]">
                        {pt.taxRoleLabels[td.taxRole] ?? td.taxRole}
                      </span>{" "}
                      <span className="font-code text-[var(--text-secondary)]">
                        ({td.taxCodeSnapshot} · {Number(td.effectiveRatePercent).toFixed(2)}%)
                      </span>
                      {td.counterpartyNpwpSnapshot ? (
                        <div className="font-code text-[11px] text-[var(--text-secondary)]">
                          NPWP: {td.counterpartyNpwpSnapshot}
                          {td.documentaryEvidenceRef ? ` · Ref: ${td.documentaryEvidenceRef}` : ""}
                        </div>
                      ) : null}
                    </div>
                    <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                      {formatIdr(td.taxAmount)}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Workflow Actions Band */}
        <div className="space-y-4 bg-[var(--bg-subtle)] p-4">
          {bill.status === "DRAFT" ? (
            <div className="space-y-3">
              {isBlockedMatch || blockingReasons.length > 0 ? (
                <div
                  role="alert"
                  data-testid="ap-blocking-reasons-alert"
                  className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                >
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <div className="space-y-0.5">
                    <p className="font-semibold">{dt.blockingReasonsTitle}</p>
                    <p>{blockingReasons.length > 0 ? blockingReasons.join(" | ") : pt.errors.posting_blocked}</p>
                  </div>
                </div>
              ) : null}
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="text-xs text-[var(--text-secondary)]">
                  {!canManage ? dt.noPermSubmitHint : pt.od03GovernanceBody}
                </div>
                {canManage ? (
                  <div className="flex flex-wrap items-center gap-2.5">
                    {onEditDraft ? (
                      <button
                        type="button"
                        id="ap-edit-draft-btn"
                        disabled={busyAction !== null}
                        onClick={onEditDraft}
                        className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                      >
                        {dt.editDraftButton}
                      </button>
                    ) : null}
                    <button
                      type="button"
                      id="ap-submit-bill-btn"
                      disabled={busyAction !== null || isBlockedMatch}
                      onClick={handleSubmit}
                      className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                    >
                      <ShieldCheck className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                      <span>{busyAction === "submit" ? dt.submitBusy : dt.submitButton}</span>
                    </button>
                  </div>
                ) : null}
              </div>
            </div>
          ) : null}

          {bill.status === "PENDING_APPROVAL" ? (
            <div className="space-y-3">
              {isSubmitter ? (
                <div
                  data-testid="ap-sod-self-approval-notice"
                  role="status"
                  className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]"
                >
                  <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{dt.sodSelfApprovalNotice}</span>
                </div>
              ) : !canApprove ? (
                <p className="text-xs text-[var(--text-secondary)]">{dt.noPermApproveHint}</p>
              ) : (
                <div className="space-y-3">
                  {needsVarianceAck ? (
                    <label className="flex items-start gap-2 text-xs font-medium text-[var(--text-primary)]">
                      <input
                        id="ap-approve-variance-checkbox"
                        type="checkbox"
                        checked={approveVariance}
                        onChange={(e) => setApproveVariance(e.target.checked)}
                        className="mt-0.5 h-4 w-4 accent-[var(--accent-primary)]"
                      />
                      <span>{dt.approveVarianceCheckbox}</span>
                    </label>
                  ) : null}

                  <label className="flex items-start gap-2 text-xs text-[var(--text-primary)]">
                    <input
                      id="ap-apply-block-checkbox"
                      type="checkbox"
                      checked={applyBlockOnDecision}
                      onChange={(e) => setApplyBlockOnDecision(e.target.checked)}
                      className="mt-0.5 h-4 w-4 accent-[var(--accent-primary)]"
                    />
                    <span>{dt.applyBlockCheckbox}</span>
                  </label>

                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {dt.decisionReasonLabel}
                      </span>
                      <input
                        id="ap-decision-reason"
                        type="text"
                        placeholder={dt.decisionReasonPlaceholder}
                        value={decisionReason}
                        onChange={(e) => setDecisionReason(e.target.value)}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </label>
                    {applyBlockOnDecision ? (
                      <label className="block space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                          {dt.blockReasonLabel}
                        </span>
                        <input
                          id="ap-decision-block-reason"
                          type="text"
                          value={decisionBlockReason}
                          onChange={(e) => setDecisionBlockReason(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                        />
                      </label>
                    ) : null}
                  </div>

                  <div className="flex flex-wrap items-center gap-2.5">
                    <button
                      type="button"
                      id="ap-approve-bill-btn"
                      disabled={busyAction !== null || (needsVarianceAck && !approveVariance)}
                      onClick={() => handleDecide("APPROVED")}
                      className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                    >
                      <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                      <span>{busyAction === "decide" ? dt.decideBusy : dt.approveButton}</span>
                    </button>
                    <button
                      type="button"
                      id="ap-reject-bill-btn"
                      disabled={busyAction !== null}
                      onClick={() => handleDecide("REJECTED")}
                      className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                    >
                      {dt.rejectButton}
                    </button>
                  </div>
                </div>
              )}
            </div>
          ) : null}

          {bill.status === "APPROVED" ? (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-[var(--text-primary)]">{dt.postSectionTitle}</p>
              {!canPost ? (
                <p className="text-xs text-[var(--text-secondary)]">{dt.noPermPostHint}</p>
              ) : (
                <div className="space-y-3">
                  <div className="flex flex-wrap items-end gap-3">
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{dt.postingDateLabel}</span>
                      <input
                        id="ap-post-date-input"
                        type="date"
                        value={postingDate}
                        disabled={postReviewOpen}
                        onChange={(e) => setPostingDate(e.target.value)}
                        className="font-code min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)] disabled:opacity-60"
                      />
                    </label>
                    <button
                      type="button"
                      id="ap-post-bill-btn"
                      ref={postReviewTrigger}
                      aria-expanded={postReviewOpen}
                      aria-controls={postReviewOpen ? "ap-post-review" : undefined}
                      disabled={busyAction !== null || isBlockedMatch || !postingDate}
                      onClick={() => postReviewOpen ? dismissPostReview() : setPostReviewOpen(true)}
                      className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-4 text-sm font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                    >
                      {dt.postButton}
                    </button>
                  </div>
                  {postReviewOpen ? (
                    <section
                      id="ap-post-review"
                      aria-labelledby="ap-post-review-title"
                      aria-busy={busyAction === "post"}
                      className="space-y-3 border-t border-[var(--border-hairline)] pt-3 text-sm"
                      onKeyDown={(e) => {
                        if (e.key === "Escape" && busyAction === null) {
                          e.stopPropagation();
                          dismissPostReview();
                        }
                      }}
                    >
                      <h3 id="ap-post-review-title" className="font-semibold text-[var(--text-primary)]">{dt.postButton}</h3>
                      <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-[var(--status-warn-text)]">
                        <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <p>{dt.postWarning}</p>
                      </div>
                      <dl className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                        <div className="min-w-0">
                          <dt className="text-[var(--text-secondary)]">{dt.headerVendor}</dt>
                          <dd className="break-words font-semibold text-[var(--text-primary)]">{bill.supplierLegalName}</dd>
                          <dd className="font-code break-words text-[var(--text-secondary)]">{bill.supplierInvoiceNumber}</dd>
                        </div>
                        <div className="min-w-0">
                          <dt className="text-[var(--text-secondary)]">{dt.postingDateLabel}</dt>
                          <dd className="font-code tabular-nums text-[var(--text-primary)]">{postingDate}</dd>
                        </div>
                        <div className="min-w-0">
                          <dt className="text-[var(--text-secondary)]">{pt.composer.netApLabel}</dt>
                          <dd className="font-code break-words font-semibold tabular-nums text-[var(--text-primary)]">{formatIdr(bill.netPayableAmount)}</dd>
                        </div>
                      </dl>
                      <div className="flex flex-wrap items-center gap-2">
                        <button
                          type="button"
                          ref={cancelPostReview}
                          disabled={busyAction !== null}
                          onClick={dismissPostReview}
                          className="min-h-11 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-4 font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                        >
                          {dt.postCancelButton}
                        </button>
                        <button
                          type="button"
                          disabled={busyAction !== null || isBlockedMatch}
                          onClick={handlePost}
                          className="min-h-11 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                        >
                          {busyAction === "post" ? dt.postBusy : dt.postConfirmButton}
                        </button>
                      </div>
                    </section>
                  ) : null}
                </div>
              )}
            </div>
          ) : null}

          {bill.status === "POSTED" && (canApprove || canPost) ? (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-[var(--text-primary)]">{dt.blockControlTitle}</p>
              <div className="flex flex-wrap items-end gap-3">
                <label className="min-w-[220px] flex-1 space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                    {dt.blockActionReasonLabel}
                  </span>
                  <input
                    id="ap-payment-block-reason"
                    type="text"
                    value={blockReason}
                    onChange={(e) => setBlockReason(e.target.value)}
                    className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                  />
                </label>
                {bill.paymentBlockStatus !== "PAYMENT_BLOCKED" ? (
                  <button
                    type="button"
                    id="ap-payment-block-btn"
                    disabled={busyAction !== null}
                    onClick={() => handleTogglePaymentBlock("PAYMENT_BLOCKED")}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                  >
                    <Lock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                    <span>{busyAction === "block" ? dt.blockBusy : dt.holdPaymentButton}</span>
                  </button>
                ) : (
                  <button
                    type="button"
                    id="ap-payment-release-btn"
                    disabled={busyAction !== null}
                    onClick={() => handleTogglePaymentBlock("RELEASED")}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                    <span>{busyAction === "block" ? dt.blockBusy : dt.releasePaymentButton}</span>
                  </button>
                )}
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </section>
  );
}
