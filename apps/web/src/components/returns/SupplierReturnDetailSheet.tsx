"use client";

import React, { useState } from "react";
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Lock,
  Pencil,
  RotateCcw,
  ShieldCheck,
  X,
} from "lucide-react";
import {
  ApiProblemError,
  decideSupplierReturn,
  executeSupplierReturn,
  submitSupplierReturn,
  SupplierReturnDetailResponse,
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

export function ReturnStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "EXECUTED" || status === "APPROVED") {
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

export function DispositionBadge({ disposition, label }: { disposition: string; label: string }) {
  if (disposition === "SUPPORTED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-danger-text)]">
      <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

export function RpaStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "FULFILLED") {
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
      <RotateCcw className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

interface Props {
  tenantId: string;
  entityId: string;
  detail: SupplierReturnDetailResponse | null;
  currentUserEmail: string;
  canAuthorCase: (caseType: string) => boolean;
  canApproveCase: (caseType: string) => boolean;
  canExecuteCase: (caseType: string) => boolean;
  onUpdated: (updated: SupplierReturnDetailResponse) => void;
  onEditDraft: (detail: SupplierReturnDetailResponse) => void;
  onOpenReceiveReplacement?: (authorizationId: string) => void;
  onClose: () => void;
}

export function SupplierReturnDetailSheet({
  tenantId,
  entityId,
  detail,
  currentUserEmail,
  canAuthorCase,
  canApproveCase,
  canExecuteCase,
  onUpdated,
  onEditDraft,
  onOpenReceiveReplacement,
  onClose,
}: Props) {
  const { locale, t } = useLocale();
  const rt = t.returns;
  const dt = rt.detail;

  const [decisionReason, setDecisionReason] = useState("");
  const [postingDate, setPostingDate] = useState(detail?.returnHeader.returnDate ?? todayJakartaDate());
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  if (!detail) {
    return (
      <section
        id="ret-detail-sheet"
        className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6 text-center"
      >
        <h2 className="text-sm font-semibold text-[var(--text-primary)]">{dt.selectPromptTitle}</h2>
        <p className="mx-auto mt-1 max-w-lg text-xs text-[var(--text-secondary)]">{dt.selectPromptBody}</p>
      </section>
    );
  }

  const hdr = detail.returnHeader;
  const canAuthor = canAuthorCase(hdr.returnCaseType);
  const canApprove = canApproveCase(hdr.returnCaseType);
  const canExecute = canExecuteCase(hdr.returnCaseType);
  const isSameActor =
    Boolean(hdr.submittedByEmail) &&
    hdr.submittedByEmail!.trim().toLowerCase() === currentUserEmail.trim().toLowerCase();

  function formatApiErr(err: unknown): string {
    const problem = err as Partial<ApiProblemError>;
    if (problem.errorCode && rt.errors[problem.errorCode]) {
      return rt.errors[problem.errorCode];
    }
    return problem.detail ?? t.login.errors.unknown;
  }

  async function handleSubmit() {
    setBusyAction("submit");
    setActionError(null);
    try {
      const updated = await submitSupplierReturn(tenantId, entityId, hdr.supplierReturnId);
      onUpdated(updated);
    } catch (err) {
      setActionError(formatApiErr(err));
    } finally {
      setBusyAction(null);
    }
  }

  async function handleDecide(decision: "APPROVED" | "REJECTED") {
    setBusyAction("decide");
    setActionError(null);
    try {
      const reason =
        decisionReason.trim() ||
        (decision === "APPROVED" ? dt.defaultApproveReason : dt.defaultRejectReason);
      const updated = await decideSupplierReturn(tenantId, entityId, hdr.supplierReturnId, {
        decision,
        reason,
      });
      onUpdated(updated);
    } catch (err) {
      setActionError(formatApiErr(err));
    } finally {
      setBusyAction(null);
    }
  }

  async function handleExecute() {
    setBusyAction("execute");
    setActionError(null);
    try {
      const updated = await executeSupplierReturn(tenantId, entityId, hdr.supplierReturnId, {
        postingDate: postingDate || hdr.returnDate || todayJakartaDate(),
        idempotencyKey: makeIdempotencyKey("ret-exec"),
      });
      onUpdated(updated);
    } catch (err) {
      setActionError(formatApiErr(err));
    } finally {
      setBusyAction(null);
    }
  }

  const preview = detail.liveEvaluation;
  const isBlockedPreview = preview?.disposition !== undefined && preview.disposition !== "SUPPORTED";

  return (
    <section
      id="ret-detail-sheet"
      aria-label={hdr.returnNumber ?? hdr.draftCode}
      className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
    >
      {/* Sheet Header Bar */}
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
        <div className="flex min-w-0 items-center gap-2">
          <ShieldCheck className="h-4 w-4 shrink-0 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="font-code text-xs font-semibold tabular-nums text-[var(--text-primary)]">
            {hdr.returnNumber ?? hdr.draftCode}
          </h2>
          <ReturnStatusBadge status={hdr.status} label={rt.statusLabels[hdr.status] ?? hdr.status} />
          <DispositionBadge
            disposition={hdr.matrixDisposition}
            label={rt.dispositionLabels[hdr.matrixDisposition] ?? hdr.matrixDisposition}
          />
        </div>
        <button
          type="button"
          onClick={onClose}
          className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <X className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
          <span>{dt.closeSheet}</span>
        </button>
      </div>

      <div className="divide-y divide-[var(--border-hairline)]">
        {/* 4-column hairline summary strip */}
        <div className="grid grid-cols-1 gap-px bg-[var(--border-hairline)] sm:grid-cols-2 lg:grid-cols-4">
          <div className="bg-[var(--bg-surface)] p-3.5">
            <div className="text-xs text-[var(--text-secondary)]">{dt.headerReturnNumber}</div>
            <div className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--text-primary)]">
              {hdr.returnNumber ?? hdr.draftCode}
            </div>
            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
              {hdr.draftCode} · {hdr.returnDate}
            </div>
          </div>

          <div className="bg-[var(--bg-surface)] p-3.5">
            <div className="text-xs text-[var(--text-secondary)]">{dt.headerSourceChain}</div>
            <div className="font-code mt-0.5 text-xs font-semibold tabular-nums text-[var(--text-primary)]">
              {hdr.poNumberSnapshot}
              {hdr.receiptNumberSnapshot ? ` · ${hdr.receiptNumberSnapshot}` : ""}
            </div>
            <div className="text-[11px] text-[var(--text-secondary)]">
              {hdr.billNumberSnapshot ? (
                <span>
                  {rt.micro.billShort} <span className="font-code tabular-nums">{hdr.billNumberSnapshot}</span>
                </span>
              ) : (
                rt.reasonCodeLabels[hdr.reasonCode] ?? hdr.reasonCode
              )}
            </div>
          </div>

          <div className="bg-[var(--bg-surface)] p-3.5">
            <div className="text-xs text-[var(--text-secondary)]">{dt.headerSupplier}</div>
            <div className="mt-0.5 text-xs font-semibold text-[var(--text-primary)]">
              {hdr.supplierLegalNameSnapshot}
            </div>
            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
              {hdr.supplierCodeSnapshot}
              {hdr.taxReturnNoteNumber ? ` · NR ${hdr.taxReturnNoteNumber}` : ""}
            </div>
          </div>

          <div className="bg-[var(--bg-surface)] p-3.5">
            <div className="text-xs text-[var(--text-secondary)]">{dt.headerCaseResolution}</div>
            <div className="mt-0.5 text-xs font-semibold text-[var(--text-primary)]">
              {rt.caseTypeLabels[hdr.returnCaseType] ?? hdr.returnCaseType}
            </div>
            <div className="text-[11px] text-[var(--text-secondary)]">
              {rt.resolutionModeLabels[hdr.resolutionMode] ?? hdr.resolutionMode}
            </div>
          </div>
        </div>

        {/* Lineage & Actors Strip */}
        <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs text-[var(--text-secondary)]">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-medium text-[var(--text-primary)]">{dt.lineageTitle}:</span>
            <span>
              PO <span className="font-code tabular-nums">{hdr.poNumberSnapshot}</span>
            </span>
            {hdr.receiptNumberSnapshot ? (
              <span>
                · GRN <span className="font-code tabular-nums">{hdr.receiptNumberSnapshot}</span>
              </span>
            ) : null}
            {hdr.billNumberSnapshot ? (
              <span>
                · {rt.micro.billShort} <span className="font-code tabular-nums">{hdr.billNumberSnapshot}</span>
              </span>
            ) : null}
            {hdr.movementNumber ? (
              <span>
                · MOV <span className="font-code tabular-nums">{hdr.movementNumber}</span>
              </span>
            ) : null}
            {hdr.journalNumber ? (
              <span>
                · GL <span className="font-code tabular-nums">{hdr.journalNumber}</span>
              </span>
            ) : null}
            {hdr.creditNumber ? (
              <span>
                · SCN <span className="font-code tabular-nums">{hdr.creditNumber}</span>
              </span>
            ) : null}
          </div>
          <div className="font-code text-[11px] tabular-nums">
            {hdr.submittedByEmail ? `${hdr.submittedByEmail}` : ""}
            {hdr.approvedByEmail ? ` → ${hdr.approvedByEmail}` : ""}
            {` · ${formatJakartaDateTime(hdr.updatedAtUtc, locale)}`}
          </div>
        </div>

        {/* Policy Blocking Reason Notice (if present) */}
        {hdr.matrixBlockingReason ? (
          <div className="px-4 py-3">
            <div
              role="alert"
              className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
            >
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <div>
                <span>
                  {(hdr.matrixBlockingCode && rt.errors[hdr.matrixBlockingCode]) ||
                    hdr.matrixBlockingReason}
                </span>
              </div>
            </div>
          </div>
        ) : null}

        {/* Return Lines Table */}
        <div>
          <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
            {dt.linesTitle}
          </div>
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-xs">
              <thead className="hidden border-y border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="py-2 pl-4 pr-2 font-semibold">{dt.colItem}</th>
                  <th scope="col" className="py-2 px-2 font-semibold">{dt.colSourceBucket}</th>
                  <th scope="col" className="py-2 px-2 text-right font-semibold">{dt.colQty}</th>
                  <th scope="col" className="py-2 px-2 text-right font-semibold">{dt.colInvGrni}</th>
                  <th scope="col" className="py-2 pl-2 pr-4 text-right font-semibold">{dt.colCreditPpnPpv}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border-hairline)]">
                {detail.lines.map((line) => (
                  <tr key={line.supplierReturnLineId} className="block p-3 md:table-row md:p-0">
                    <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colItem}</span>
                      <div className="font-code font-semibold text-[var(--text-primary)]">{line.itemCode}</div>
                      <div className="text-[11px] text-[var(--text-secondary)]">{line.itemName}</div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colSourceBucket}</span>
                      <div className="text-[11px] text-[var(--text-primary)]">
                        {line.sourceLocationCode ? (
                          <span className="font-code">
                            {`${line.sourceLocationCode} · ${line.sourceStockStatus ?? "UNRESTRICTED"}`}
                          </span>
                        ) : (
                          rt.micro.dockCustodyLabel
                        )}
                      </div>
                      {line.sourceLotNumber ? (
                        <div className="font-code text-[11px] text-[var(--text-secondary)]">
                          Lot {line.sourceLotNumber}
                        </div>
                      ) : null}
                    </td>
                    <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colQty}</span>
                      <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                        {formatQuantityValue(line.returnedBaseQuantity, locale)} {line.baseUomCode}
                      </div>
                      <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                        @ {formatUnitCostIdr(line.authorizedUnitCostBase)}
                      </div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colInvGrni}</span>
                      <div className="font-code tabular-nums text-[var(--text-primary)]">
                        1300: {formatIdr(line.inventoryCreditAmount)}
                      </div>
                      <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                        2110: {formatIdr(line.grniReversalAmount)}
                      </div>
                    </td>
                    <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                      <span className="block font-medium text-[var(--text-secondary)] md:hidden">{dt.colCreditPpnPpv}</span>
                      <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                        2100: {formatIdr(line.totalCreditAmount)}
                      </div>
                      <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                        PPN {formatIdr(line.ppnReversalAmount)} · PPV {formatIdr(line.ppvDifferenceAmount)}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Issued Replacement Authorizations (RPA) */}
        {detail.replacementAuthorizations.length > 0 ? (
          <div>
            <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
              {dt.replacementLinksTitle}
            </div>
            <div className="divide-y divide-[var(--border-hairline)]">
              {detail.replacementAuthorizations.map((rpa) => (
                <div
                  key={rpa.replacementAuthorizationId}
                  className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs"
                >
                  <div>
                    <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                      {rpa.authorizationNumber}
                    </span>
                    <span className="ml-2 font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                      {rpa.itemCode} ·{" "}
                      {formatQuantityValue(rpa.remainingBaseQuantity, locale)}/
                      {formatQuantityValue(rpa.authorizedBaseQuantity, locale)} {rpa.baseUomCode} ·{" "}
                      {formatUnitCostIdr(rpa.authorizedInventoryUnitCostBase)}
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <RpaStatusBadge
                      status={rpa.status}
                      label={rt.rpaStatusLabels[rpa.status] ?? rpa.status}
                    />
                    {onOpenReceiveReplacement &&
                    canAuthor &&
                    Number(rpa.remainingBaseQuantity) > 0 &&
                    (rpa.status === "ACTIVE" || rpa.status === "PARTIALLY_RECEIVED") ? (
                      <button
                        type="button"
                        onClick={() => onOpenReceiveReplacement(rpa.replacementAuthorizationId)}
                        className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                      >
                        {rt.receiveReplacementButton}
                      </button>
                    ) : null}
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : null}

        {/* Supplier Credit Note Summary (Append-Only) */}
        {detail.supplierCredit ? (
          <div className="px-4 py-3">
            <div className="text-xs font-semibold text-[var(--text-primary)]">{dt.creditNoteSummaryTitle}</div>
            <div className="mt-2 flex flex-wrap items-center justify-between gap-3 text-xs">
              <div className="font-code space-y-0.5 tabular-nums">
                <div className="font-bold text-[var(--text-primary)]">
                  <span className="font-code tabular-nums">{detail.supplierCredit.creditNumber}</span> ·{" "}
                  <span className="font-sans font-medium">{rt.micro.payableShort}</span>{" "}
                  <span className="font-code tabular-nums">{detail.supplierCredit.payableNumber}</span>
                </div>
                <div className="text-[11px] text-[var(--text-secondary)]">
                  Nota Retur {detail.supplierCredit.taxReturnNoteNumber ?? "—"} · GL {detail.supplierCredit.journalNumber} · MOV{" "}
                  {detail.supplierCredit.movementNumber}
                </div>
              </div>
              <div className="font-code text-right tabular-nums">
                <div className="font-bold text-[var(--status-ok-text)]">
                  Dr 2100: {formatIdr(detail.supplierCredit.totalCreditAmount)}
                </div>
                <div className="text-[11px] text-[var(--text-secondary)]">
                  DPP {formatIdr(detail.supplierCredit.subtotalCreditAmount)} · Cr 1410{" "}
                  {formatIdr(detail.supplierCredit.ppnReversalAmount)} · Cr 1300{" "}
                  {formatIdr(detail.supplierCredit.inventoryCreditAmount)}
                </div>
              </div>
            </div>
          </div>
        ) : null}

        {/* Tax Reversal Determinations */}
        {detail.taxDeterminations.length > 0 ? (
          <div className="px-4 py-3">
            <div className="text-xs font-semibold text-[var(--text-primary)]">{dt.taxDeterminationsTitle}</div>
            <div className="mt-2 divide-y divide-[var(--border-hairline)]">
              {detail.taxDeterminations.map((td) => (
                <div key={td.determinationId} className="flex flex-wrap items-center justify-between gap-2 py-1.5 text-xs">
                  <div className="font-code tabular-nums">
                    <span className="font-semibold text-[var(--text-primary)]">{td.taxCodeSnapshot}</span>{" "}
                    <span className="text-[11px] text-[var(--text-secondary)]">
                      ({td.taxRole}) · {td.documentaryEvidenceRef ?? "—"}
                    </span>
                  </div>
                  <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                    {dt.taxBaseLabel}: {formatIdr(td.taxableBaseAmount)} · {dt.taxAmountLabel}:{" "}
                    <span className="font-semibold text-[var(--text-primary)]">{formatIdr(td.taxAmount)}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : null}

        {/* Live GL Preview for unexecuted documents */}
        {preview && hdr.status !== "EXECUTED" ? (
          <div className="space-y-2.5 px-4 py-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-semibold text-[var(--text-primary)]">{dt.livePreviewTitle}</span>
                <span className="font-code text-xs font-bold text-[var(--text-primary)]">{preview.matrixCode}</span>
                <DispositionBadge
                  disposition={preview.disposition}
                  label={rt.dispositionLabels[preview.disposition] ?? preview.disposition}
                />
              </div>
              <span className="text-[11px] text-[var(--text-secondary)]">
                {preview.isPeriodOpen ? rt.composer.previewPeriodOpen : rt.composer.previewPeriodClosed}
              </span>
            </div>

            {isBlockedPreview || preview.blockingReason ? (
              <div
                role="alert"
                className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
              >
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>
                  {(preview.blockingCode && rt.errors[preview.blockingCode]) ||
                    preview.blockingReason ||
                    rt.composer.previewBlockedGlNotice}
                </span>
              </div>
            ) : null}

            {!isBlockedPreview && preview.journalPreview.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-y border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-1.5 pl-2 pr-2 font-semibold">{rt.composer.journalPreviewTitle}</th>
                      <th scope="col" className="py-1.5 px-2 text-right font-semibold">{rt.composer.colDebit}</th>
                      <th scope="col" className="py-1.5 pl-2 pr-2 text-right font-semibold">{rt.composer.colCredit}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {preview.journalPreview.map((j, idx) => (
                      <tr key={`${j.side}-${j.accountCode}-${idx}`} className="block py-2 md:table-row md:py-0">
                        <td className="block py-0.5 md:table-cell md:py-1.5 md:pl-2 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {rt.composer.journalPreviewTitle}
                          </span>
                          <span className="font-code font-semibold text-[var(--text-primary)]">{j.accountCode}</span>{" "}
                          <span className="text-[var(--text-secondary)]">{j.accountName}</span>
                        </td>
                        <td className="block py-0.5 md:table-cell md:py-1.5 md:px-2 md:text-right">
                          <span className="mr-2 font-medium text-[var(--text-secondary)] md:hidden">
                            {rt.composer.colDebit}:
                          </span>
                          <span className="font-code tabular-nums text-[var(--text-primary)]">
                            {j.side === "DEBIT" ? formatIdr(j.amount) : "—"}
                          </span>
                        </td>
                        <td className="block py-0.5 md:table-cell md:py-1.5 md:pl-2 md:pr-2 md:text-right">
                          <span className="mr-2 font-medium text-[var(--text-secondary)] md:hidden">
                            {rt.composer.colCredit}:
                          </span>
                          <span className="font-code tabular-nums text-[var(--text-primary)]">
                            {j.side === "CREDIT" ? formatIdr(j.amount) : "—"}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-xs text-[var(--text-secondary)]">
                {isBlockedPreview ? rt.composer.previewBlockedGlNotice : preview.od05Notice}
              </p>
            )}
          </div>
        ) : null}

        {/* Action Footer: Submit / OD-02 Four-Eyes Decision / Execute */}
        {hdr.status !== "EXECUTED" && hdr.status !== "CANCELLED" ? (
          <div className="space-y-3 bg-[var(--bg-subtle)]/50 px-4 py-3.5">
            {actionError ? (
              <div
                id="ret-detail-error"
                role="alert"
                className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
              >
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{actionError}</span>
              </div>
            ) : null}

            {hdr.status === "DRAFT" ? (
              canAuthor ? (
                <div className="flex flex-wrap items-center gap-2.5">
                  <button
                    type="button"
                    id="ret-submit-btn"
                    disabled={busyAction !== null}
                    onClick={handleSubmit}
                    className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                    <span>{busyAction === "submit" ? dt.submitBusy : dt.submitButton}</span>
                  </button>
                  <button
                    type="button"
                    id="ret-edit-draft-btn"
                    disabled={busyAction !== null}
                    onClick={() => onEditDraft(detail)}
                    className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                  >
                    <Pencil className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                    <span>{rt.composer.updateDraftButton}</span>
                  </button>
                </div>
              ) : (
                <p className="text-xs text-[var(--text-secondary)]">{dt.noPermManageHint}</p>
              )
            ) : null}

            {hdr.status === "PENDING_APPROVAL" ? (
              <div className="space-y-2.5">
                <p className="text-xs font-semibold text-[var(--text-primary)]">{dt.decisionTitle}</p>
                {isSameActor ? (
                  <div
                    data-testid="ret-sod-notice"
                    role="status"
                    className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]"
                  >
                    <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    <span>{dt.sodHint}</span>
                  </div>
                ) : !canApprove ? (
                  <p className="text-xs text-[var(--text-secondary)]">{dt.noPermApproveHint}</p>
                ) : (
                  <div className="space-y-2.5">
                    <label className="block max-w-md space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {dt.decisionReasonLabel}
                      </span>
                      <input
                        id="ret-decision-reason"
                        type="text"
                        value={decisionReason}
                        onChange={(e) => setDecisionReason(e.target.value)}
                        placeholder={dt.defaultApproveReason}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </label>
                    <div className="flex flex-wrap items-center gap-2.5">
                      <button
                        type="button"
                        id="ret-approve-btn"
                        disabled={busyAction !== null}
                        onClick={() => handleDecide("APPROVED")}
                        className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                      >
                        <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                        <span>{busyAction === "decide" ? dt.decisionBusy : dt.approveButton}</span>
                      </button>
                      <button
                        type="button"
                        id="ret-reject-btn"
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

            {hdr.status === "APPROVED" ? (
              <div className="space-y-2.5">
                <p className="text-xs font-semibold text-[var(--text-primary)]">{dt.executeTitle}</p>
                {!canExecute ? (
                  <p className="text-xs text-[var(--text-secondary)]">{dt.noPermExecuteHint}</p>
                ) : (
                  <div className="flex flex-wrap items-end gap-3">
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {dt.postingDateLabel}
                      </span>
                      <input
                        id="ret-exec-date"
                        type="date"
                        value={postingDate}
                        onChange={(e) => setPostingDate(e.target.value)}
                        className="font-code min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                      />
                    </label>
                    <button
                      type="button"
                      id="ret-execute-btn"
                      disabled={busyAction !== null || isBlockedPreview}
                      onClick={handleExecute}
                      className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                    >
                      <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                      <span>{busyAction === "execute" ? dt.executeBusy : dt.executeButton}</span>
                    </button>
                  </div>
                )}
              </div>
            ) : null}
          </div>
        ) : null}
      </div>
    </section>
  );
}
