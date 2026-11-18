"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ClipboardCheck,
  Clock,
  Eye,
  Lock,
  PackageCheck,
  PlusCircle,
  RefreshCw,
  ShieldCheck,
} from "lucide-react";
import {
  AcceptancePreviewDto,
  acceptGoodsReceiptValued,
  AcceptReceiptLineRequest,
  ApiProblemError,
  EligiblePoReceivingLineDto,
  EligiblePurchaseOrderForReceivingDto,
  EntityWorkspaceDetailResponse,
  fetchEligibleReceivingPos,
  fetchEntityWorkspaceDetail,
  fetchGoodsReceiptDetail,
  fetchGoodsReceipts,
  GoodsReceiptDetailDto,
  GoodsReceiptLineDto,
  GoodsReceiptListResponse,
  listInventoryWarehouses,
  previewReceiptAcceptance,
  recordGoodsReceiptArrival,
  recordGoodsReceiptDockCheck,
  ValuedAcceptanceDetailDto,
  WarehouseLocationDto,
} from "@/lib/api/client";
import {
  formatIdr,
  formatJakartaDateTime,
  formatUnitCostIdr,
  type SupportedLocale,
} from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import {
  convertArrivalQtyToBase,
  evaluateDockCheckPreview,
  formatQuantityValue,
  formatScaledBigInt,
  formatScaledPlainInput,
  parseScaledBigInt,
} from "@/lib/receivingMath";

const DISCREPANCY_REASON_CODES = [
  "NONE",
  "DAMAGED_PACKAGING",
  "WET_OR_DEFECTIVE_GOODS",
  "OVERDELIVERY_OD01_HOLD",
  "WRONG_ITEM_OR_SPEC",
  "MISSING_SUPPLIER_DOCS",
] as const;

const RECEIPT_FILTER_STATUSES = [
  "ARRIVED",
  "IN_DOCK_INSPECTION",
  "RELEASED_FOR_ACCEPTANCE",
  "PARTIALLY_ACCEPTED",
  "VALUED_ACCEPTED",
  "FULLY_REJECTED_UNVALUED",
] as const;

const STOCK_STATUS_OPTIONS = ["UNRESTRICTED", "QUALITY_INSPECTION", "BLOCKED"] as const;

interface ArrivalLineDraft {
  poLineId: string;
  receivedUomId: string;
  qtyText: string;
  flagForInspection: boolean;
  discrepancyReasonCode: string;
  discrepancyNotes: string;
  conditionEvidenceRef: string;
}

interface DockCheckDraft {
  idempotencyKey: string;
  pendingText: string;
  heldText: string;
  releasedText: string;
  rejectedText: string;
  discrepancyReasonCode: string;
  inspectionNotes: string;
  evidenceReference: string;
}

function makeIdempotencyKey(prefix: string): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

function defaultJakartaIsoDate(): string {
  try {
    const formatted = new Intl.DateTimeFormat("en-CA", {
      timeZone: "Asia/Jakarta",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    }).format(new Date());
    if (/^\d{4}-\d{2}-\d{2}$/.test(formatted)) return formatted;
  } catch {
    // Fallback below if ICU timezone formatting is unavailable.
  }
  return "2026-11-18";
}

function buildArrivalLineDrafts(po: EligiblePurchaseOrderForReceivingDto | null): ArrivalLineDraft[] {
  if (!po) return [];
  const drafts: ArrivalLineDraft[] = [];
  for (const line of po.lines) {
    if (line.itemKind !== "STOCKED_GOOD") continue;
    const defaultUom =
      line.allowedUoms.find((u) => u.uomId === line.orderedUomId)?.uomId ??
      line.allowedUoms[0]?.uomId ??
      line.baseUomId;
    drafts.push({
      poLineId: line.poLineId,
      receivedUomId: defaultUom,
      qtyText: "",
      flagForInspection: false,
      discrepancyReasonCode: "NONE",
      discrepancyNotes: "",
      conditionEvidenceRef: "",
    });
  }
  return drafts;
}

function buildDockDraftFromLine(line: GoodsReceiptLineDto): DockCheckDraft {
  const pendingScaled = parseScaledBigInt(line.pendingCheckBaseQuantity) ?? 0n;
  const heldScaled = parseScaledBigInt(line.dockHeldBaseQuantity) ?? 0n;
  const releasedScaled = parseScaledBigInt(line.dockReleasedBaseQuantity) ?? 0n;
  const rejectedScaled = parseScaledBigInt(line.dockRejectedBaseQuantity) ?? 0n;

  return {
    idempotencyKey: makeIdempotencyKey("chk"),
    pendingText: formatScaledPlainInput(pendingScaled),
    heldText: formatScaledPlainInput(heldScaled),
    releasedText: formatScaledPlainInput(releasedScaled),
    rejectedText: formatScaledPlainInput(rejectedScaled),
    discrepancyReasonCode: line.discrepancyReasonCode || "NONE",
    inspectionNotes: line.discrepancyNotes ?? "",
    evidenceReference: line.conditionEvidenceRef ?? "",
  };
}

function mapDockDrafts(detail: GoodsReceiptDetailDto): Record<string, DockCheckDraft> {
  const next: Record<string, DockCheckDraft> = {};
  for (const line of detail.lines) {
    next[line.goodsReceiptLineId] = buildDockDraftFromLine(line);
  }
  return next;
}

function getLineRemainingReleasedScaled(line: GoodsReceiptLineDto): bigint {
  const rel = parseScaledBigInt(line.dockReleasedBaseQuantity) ?? 0n;
  const val = parseScaledBigInt(line.valuedAcceptedBaseQuantity) ?? 0n;
  return rel > val ? rel - val : 0n;
}

function isZoneCompatibleForGate2(zoneType: string, stockStatus: string): boolean {
  if (stockStatus === "QUALITY_INSPECTION") return zoneType === "QUARANTINE";
  if (stockStatus === "BLOCKED") return zoneType === "BLOCKED_HOLD";
  return zoneType === "STORAGE" || zoneType === "RECEIVING_STAGING" || zoneType === "DISPATCH";
}

function buildGate2QtyInputs(detail: GoodsReceiptDetailDto): Record<string, string> {
  const next: Record<string, string> = {};
  for (const line of detail.lines) {
    next[line.goodsReceiptLineId] = formatScaledPlainInput(getLineRemainingReleasedScaled(line));
  }
  return next;
}

function ReceivingStatusBadge({
  status,
  label,
}: {
  status: string;
  label: string;
}) {
  if (
    status === "RELEASED_FOR_ACCEPTANCE" ||
    status === "VALUED_ACCEPTED" ||
    status === "DOCK_RELEASED"
  ) {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }

  if (status === "FULLY_REJECTED_UNVALUED" || status === "DOCK_REJECTED") {
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

function ReceivingErrorCard({
  problem,
  onRetry,
}: {
  problem: ApiProblemError;
  onRetry: () => void;
}) {
  const { t } = useLocale();
  const isForbidden = problem.status === 403;
  const isNotFound = problem.status === 404;

  const title = isForbidden
    ? t.entitySheet.forbiddenTitle
    : isNotFound
      ? t.entitySheet.unavailableTitle
      : t.receiving.loadErrorTitle;
  const body = isForbidden
    ? t.entitySheet.forbiddenBody
    : isNotFound
      ? t.entitySheet.unavailableBody
      : problem.detail;

  return (
    <div
      role="alert"
      className="mx-auto max-w-2xl rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
    >
      <div className="flex items-start gap-3">
        <div className="mt-0.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] p-2 text-[var(--status-danger-text)]">
          {isForbidden ? (
            <Lock className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
          ) : (
            <AlertCircle className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
          )}
        </div>
        <div className="min-w-0 flex-1 space-y-2">
          <h1 className="font-heading text-lg font-bold tracking-tight text-[var(--text-primary)]">
            {title}
          </h1>
          <p className="text-sm leading-6 text-[var(--text-secondary)]">{body}</p>
          <div className="flex flex-wrap gap-2.5 pt-2">
            {!isForbidden && !isNotFound ? (
              <button
                type="button"
                onClick={onRetry}
                className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-sm font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
              >
                <RefreshCw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{t.receiving.retryButton}</span>
              </button>
            ) : null}
            <Link
              href="/workspaces"
              className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <span>{t.entitySheet.returnWorkspaces}</span>
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

function DockCheckLineCard({
  line,
  lineIndex,
  draft,
  canManage,
  locale,
  saving,
  lineError,
  lineSuccess,
  onChangeDraft,
  onApplyPreset,
  onSubmit,
}: {
  line: GoodsReceiptLineDto;
  lineIndex: number;
  draft: DockCheckDraft;
  canManage: boolean;
  locale: SupportedLocale;
  saving: boolean;
  lineError: string | null;
  lineSuccess: boolean;
  onChangeDraft: (updater: (prev: DockCheckDraft) => DockCheckDraft) => void;
  onApplyPreset: (preset: "release_cap" | "hold_all" | "reject_all") => void;
  onSubmit: () => void;
}) {
  const { t } = useLocale();
  const evaluation = useMemo(
    () =>
      evaluateDockCheckPreview({
        deliveredBase: line.physicallyDeliveredBaseQuantity,
        valuedAcceptedBase: line.valuedAcceptedBaseQuantity,
        maxReleaseCapBase: line.maxApprovedReleaseCapBaseQty,
        pendingRaw: draft.pendingText,
        heldRaw: draft.heldText,
        releasedRaw: draft.releasedText,
        rejectedRaw: draft.rejectedText,
      }),
    [
      line.physicallyDeliveredBaseQuantity,
      line.valuedAcceptedBaseQuantity,
      line.maxApprovedReleaseCapBaseQty,
      draft.pendingText,
      draft.heldText,
      draft.releasedText,
      draft.rejectedText,
    ],
  );

  const deliveredDisplay = formatQuantityValue(line.physicallyDeliveredBaseQuantity, locale);
  const sumDisplay =
    evaluation.sumBase !== null ? formatScaledBigInt(evaluation.sumBase, 6, locale) : "—";
  const maxCapDisplay = formatQuantityValue(line.maxApprovedReleaseCapBaseQty, locale);
  const excessScaled = parseScaledBigInt(line.overdeliveryExcessBaseQuantity) ?? 0n;
  const custodyLabel = t.receiving.custodyStatusLabels[line.custodyStatus] ?? line.custodyStatus;

  return (
    <div
      data-testid={`dock-check-card-${lineIndex}`}
      className="space-y-3 bg-[var(--bg-surface)] p-4"
    >
      <div className="flex flex-wrap items-start justify-between gap-2 border-b border-[var(--border-hairline)] pb-3">
        <div className="space-y-0.5">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-code text-xs font-semibold tabular-nums text-[var(--text-secondary)]">
              L{String(line.lineNumber)} · {line.itemCodeSnapshot}
            </span>
            <span className="text-sm font-semibold text-[var(--text-primary)]">
              {line.itemNameSnapshot}
            </span>
          </div>
          <p className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
            {formatQuantityValue(line.physicallyDeliveredQuantity, locale)}{" "}
            {line.receivedUomCodeSnapshot} = {deliveredDisplay} {line.baseUomCodeSnapshot} ·{" "}
            {t.receiving.od01CapLabel}: {maxCapDisplay} {line.baseUomCodeSnapshot}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <ReceivingStatusBadge status={line.custodyStatus} label={custodyLabel} />
          {excessScaled > 0n ? (
            <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 font-code text-xs font-medium tabular-nums text-[var(--status-warn-text)]">
              <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>
                OD-01 +{formatScaledBigInt(excessScaled, 6, locale)} {line.baseUomCodeSnapshot}
              </span>
            </span>
          ) : null}
        </div>
      </div>

      {!canManage ? (
        <p className="pt-3 text-xs text-[var(--text-secondary)]">
          {t.receiving.dockCheckReadOnlyNote}
        </p>
      ) : (
        <div className="mt-3 space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h4 className="text-xs font-semibold text-[var(--text-primary)]">
              {t.receiving.dockCheckFormHeading}
            </h4>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                id={`dock-preset-release-${lineIndex}`}
                onClick={() => onApplyPreset("release_cap")}
                className="inline-flex min-h-9 items-center whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
              >
                {t.receiving.presetReleaseWithinCap}
              </button>
              <button
                type="button"
                id={`dock-preset-hold-${lineIndex}`}
                onClick={() => onApplyPreset("hold_all")}
                className="inline-flex min-h-9 items-center whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
              >
                {t.receiving.presetHoldAll}
              </button>
              <button
                type="button"
                id={`dock-preset-reject-${lineIndex}`}
                onClick={() => onApplyPreset("reject_all")}
                className="inline-flex min-h-9 items-center whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)]"
              >
                {t.receiving.presetRejectAll}
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <label className="block space-y-1">
              <span className="block text-xs font-medium text-[var(--text-secondary)]">
                {t.receiving.pendingQtyLabel} ({line.baseUomCodeSnapshot})
              </span>
              <input
                id={`dock-pending-${lineIndex}`}
                type="text"
                inputMode="decimal"
                value={draft.pendingText}
                aria-invalid={!evaluation.validFormat || !evaluation.balanced}
                aria-describedby={lineError ? `dock-error-${lineIndex}` : undefined}
                onChange={(e) => {
                  const val = e.target.value;
                  onChangeDraft((prev) => ({ ...prev, pendingText: val }));
                }}
                className={`font-code min-h-11 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)] ${
                  !evaluation.validFormat || !evaluation.balanced
                    ? "border-[var(--status-danger-text)]"
                    : "border-[var(--border-strong)]"
                }`}
              />
            </label>

            <label className="block space-y-1">
              <span className="block text-xs font-medium text-[var(--text-secondary)]">
                {t.receiving.heldQtyLabel} ({line.baseUomCodeSnapshot})
              </span>
              <input
                id={`dock-held-${lineIndex}`}
                type="text"
                inputMode="decimal"
                value={draft.heldText}
                aria-invalid={!evaluation.validFormat || !evaluation.balanced}
                aria-describedby={lineError ? `dock-error-${lineIndex}` : undefined}
                onChange={(e) => {
                  const val = e.target.value;
                  onChangeDraft((prev) => ({ ...prev, heldText: val }));
                }}
                className={`font-code min-h-11 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)] ${
                  !evaluation.validFormat || !evaluation.balanced
                    ? "border-[var(--status-danger-text)]"
                    : "border-[var(--border-strong)]"
                }`}
              />
            </label>

            <label className="block space-y-1">
              <span className="block text-xs font-medium text-[var(--text-secondary)]">
                {t.receiving.releasedQtyLabel} ({line.baseUomCodeSnapshot})
              </span>
              <input
                id={`dock-released-${lineIndex}`}
                type="text"
                inputMode="decimal"
                value={draft.releasedText}
                aria-invalid={
                  !evaluation.validFormat ||
                  !evaluation.balanced ||
                  evaluation.exceedsOd01Cap
                }
                aria-describedby={lineError ? `dock-error-${lineIndex}` : undefined}
                onChange={(e) => {
                  const val = e.target.value;
                  onChangeDraft((prev) => ({ ...prev, releasedText: val }));
                }}
                className={`font-code min-h-11 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)] ${
                  !evaluation.validFormat ||
                  !evaluation.balanced ||
                  evaluation.exceedsOd01Cap
                    ? "border-[var(--status-danger-text)]"
                    : "border-[var(--border-strong)]"
                }`}
              />
            </label>

            <label className="block space-y-1">
              <span className="block text-xs font-medium text-[var(--text-secondary)]">
                {t.receiving.rejectedQtyLabel} ({line.baseUomCodeSnapshot})
              </span>
              <input
                id={`dock-rejected-${lineIndex}`}
                type="text"
                inputMode="decimal"
                value={draft.rejectedText}
                aria-invalid={!evaluation.validFormat || !evaluation.balanced}
                aria-describedby={lineError ? `dock-error-${lineIndex}` : undefined}
                onChange={(e) => {
                  const val = e.target.value;
                  onChangeDraft((prev) => ({ ...prev, rejectedText: val }));
                }}
                className={`font-code min-h-11 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)] ${
                  !evaluation.validFormat || !evaluation.balanced
                    ? "border-[var(--status-danger-text)]"
                    : "border-[var(--border-strong)]"
                }`}
              />
            </label>
          </div>

          <div
            data-testid={`dock-conservation-${lineIndex}`}
            className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] bg-[var(--bg-subtle)] px-3 py-2 text-xs"
          >
            <span className="text-[var(--text-secondary)]">
              {t.receiving.conservationFormulaLabel}:{" "}
              <strong className="font-code tabular-nums text-[var(--text-primary)]">
                {sumDisplay} / {deliveredDisplay} {line.baseUomCodeSnapshot}
              </strong>
            </span>
            <div className="flex flex-wrap items-center gap-2">
              {evaluation.balanced ? (
                <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 font-medium text-[var(--status-ok-text)]">
                  <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{t.receiving.conservationBalancedTag}</span>
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 font-medium text-[var(--status-danger-text)]">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{t.receiving.conservationMismatchTag}</span>
                </span>
              )}
              {evaluation.exceedsOd01Cap ? (
                <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 font-medium text-[var(--status-danger-text)]">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{t.receiving.od01CapExceededTag}</span>
                </span>
              ) : null}
            </div>
          </div>

          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <label className="block space-y-1">
              <span className="block text-xs font-medium text-[var(--text-secondary)]">
                {t.receiving.discrepancyReasonLabel}
              </span>
              <select
                id={`dock-reason-${lineIndex}`}
                value={draft.discrepancyReasonCode}
                onChange={(e) => {
                  const val = e.target.value;
                  onChangeDraft((prev) => ({ ...prev, discrepancyReasonCode: val }));
                }}
                className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
              >
                {DISCREPANCY_REASON_CODES.map((code) => (
                  <option key={code} value={code}>
                    {t.receiving.discrepancyReasonLabels[code] ?? code}
                  </option>
                ))}
              </select>
            </label>

            <label className="block space-y-1">
              <span className="block text-xs font-medium text-[var(--text-secondary)]">
                {t.receiving.evidenceRefLabel}
              </span>
              <input
                id={`dock-evidence-${lineIndex}`}
                type="text"
                value={draft.evidenceReference}
                placeholder={t.receiving.evidenceRefPlaceholder}
                onChange={(e) => {
                  const val = e.target.value;
                  onChangeDraft((prev) => ({ ...prev, evidenceReference: val }));
                }}
                className="font-code min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
              />
            </label>

            <label className="block space-y-1 md:col-span-2">
              <span className="block text-xs font-medium text-[var(--text-secondary)]">
                {t.receiving.inspectionNotesLabel}
              </span>
              <input
                id={`dock-notes-${lineIndex}`}
                type="text"
                value={draft.inspectionNotes}
                placeholder={t.receiving.inspectionNotesPlaceholder}
                aria-describedby={lineError ? `dock-error-${lineIndex}` : undefined}
                onChange={(e) => {
                  const val = e.target.value;
                  onChangeDraft((prev) => ({ ...prev, inspectionNotes: val }));
                }}
                className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
              />
            </label>
          </div>

          {lineError ? (
            <div
              id={`dock-error-${lineIndex}`}
              role="alert"
              className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2.5 text-sm text-[var(--status-danger-text)]"
            >
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{lineError}</span>
            </div>
          ) : null}

          {lineSuccess ? (
            <div
              role="status"
              className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-3 py-2.5 text-sm text-[var(--status-ok-text)]"
            >
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{t.receiving.dockCheckSuccessBanner}</span>
            </div>
          ) : null}

          <div className="flex justify-end">
            <button
              type="button"
              id={`dock-submit-${lineIndex}`}
              disabled={saving}
              onClick={onSubmit}
              className="inline-flex min-h-11 items-center gap-2 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
            >
              <ClipboardCheck className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>
                {saving ? t.receiving.submitDockCheckBusy : t.receiving.submitDockCheckButton}
              </span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function ReceivingSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const { locale, t } = useLocale();

  const [workspace, setWorkspace] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [receiptsData, setReceiptsData] = useState<GoodsReceiptListResponse | null>(null);
  const [eligiblePos, setEligiblePos] = useState<EligiblePurchaseOrderForReceivingDto[]>([]);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loading, setLoading] = useState(true);
  const [retryNonce, setRetryNonce] = useState(0);

  const [activeTab, setActiveTab] = useState<"ledger" | "arrival">("ledger");
  const [searchFilter, setSearchFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [ledgerLoading, setLedgerLoading] = useState(false);

  const [selectedReceiptId, setSelectedReceiptId] = useState<string | null>(null);
  const [selectedDetail, setSelectedDetail] = useState<GoodsReceiptDetailDto | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  // Arrival Composer state
  const [selectedPoId, setSelectedPoId] = useState<string>("");
  const [deliveryNote, setDeliveryNote] = useState<string>("");
  const [receivedDate, setReceivedDate] = useState<string>(() => defaultJakartaIsoDate());
  const [receivingStation, setReceivingStation] = useState<string>(
    () => t.receiving.defaultReceivingStation,
  );
  const [vehicleRef, setVehicleRef] = useState<string>("");
  const [documentRef, setDocumentRef] = useState<string>("");
  const [arrivalNotes, setArrivalNotes] = useState<string>("");
  const [arrivalDraftKey, setArrivalDraftKey] = useState<string>(() => makeIdempotencyKey("arr"));
  const [arrivalLines, setArrivalLines] = useState<ArrivalLineDraft[]>([]);
  const [arrivalError, setArrivalError] = useState<string | null>(null);
  const [arrivalSuccess, setArrivalSuccess] = useState<string | null>(null);
  const [arrivalSubmitting, setArrivalSubmitting] = useState(false);

  // Per-line Dock Inspection drafts
  const [dockDrafts, setDockDrafts] = useState<Record<string, DockCheckDraft>>({});
  const [dockSubmittingLineId, setDockSubmittingLineId] = useState<string | null>(null);
  const [dockErrors, setDockErrors] = useState<Record<string, string>>({});
  const [dockSuccessLineId, setDockSuccessLineId] = useState<string | null>(null);

  // Gate 2 Valued Acceptance state
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [gate2QtyInputs, setGate2QtyInputs] = useState<Record<string, string>>({});
  const [gate2LocInputs, setGate2LocInputs] = useState<Record<string, string>>({});
  const [gate2LotInputs, setGate2LotInputs] = useState<Record<string, string>>({});
  const [gate2ExpInputs, setGate2ExpInputs] = useState<Record<string, string>>({});
  const [gate2Date, setGate2Date] = useState<string>(() => defaultJakartaIsoDate());
  const [gate2StockStatus, setGate2StockStatus] = useState<string>("UNRESTRICTED");
  const [gate2Notes, setGate2Notes] = useState<string>("");
  const [gate2IdemKey, setGate2IdemKey] = useState<string>(() => makeIdempotencyKey("acc"));
  const [gate2Preview, setGate2Preview] = useState<AcceptancePreviewDto | null>(null);
  const [gate2PreviewStale, setGate2PreviewStale] = useState(false);
  const [gate2PreviewBusy, setGate2PreviewBusy] = useState(false);
  const [gate2Submitting, setGate2Submitting] = useState(false);
  const [gate2Error, setGate2Error] = useState<string | null>(null);
  const [gate2Success, setGate2Success] = useState<ValuedAcceptanceDetailDto | null>(null);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  const canManage = useMemo(() => {
    if (!workspace) return false;
    return workspace.effectivePermissions.includes("receiving.manage");
  }, [workspace]);

  const canAccept = useMemo(() => {
    if (!workspace) return false;
    return workspace.effectivePermissions.includes("receiving.accept");
  }, [workspace]);

  const selectedEligiblePo = useMemo(() => {
    for (const po of eligiblePos) {
      if (po.purchaseOrderId === selectedPoId) return po;
    }
    return null;
  }, [eligiblePos, selectedPoId]);

  const compatibleGate2Locations = useMemo(() => {
    return locations.filter(
      (loc) => loc.status === "ACTIVE" && isZoneCompatibleForGate2(loc.zoneType, gate2StockStatus),
    );
  }, [locations, gate2StockStatus]);

  const mapApiErrorText = useCallback(
    (err: unknown): string => {
      const apiErr = err as ApiProblemError;
      if (apiErr?.errorCode && t.receiving.errors[apiErr.errorCode]) {
        return t.receiving.errors[apiErr.errorCode];
      }
      return apiErr?.detail || apiErr?.title || t.receiving.loadErrorTitle;
    },
    [t],
  );

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchGoodsReceipts(tenantId, entityId),
      fetchEligibleReceivingPos(tenantId, entityId),
      Promise.resolve(
        typeof listInventoryWarehouses === "function" ? listInventoryWarehouses(tenantId, entityId) : null,
      ).catch(() => null),
    ])
      .then(([ws, list, eligible, whData]) => {
        if (!active) return;
        setWorkspace(ws);
        setReceiptsData(list);
        setEligiblePos(eligible.items);
        if (whData?.locations) {
          setLocations(whData.locations);
        }
        setProblem(null);
        setLoading(false);

        const defaultPo = eligible.items[0] ?? null;
        if (defaultPo) {
          setSelectedPoId(defaultPo.purchaseOrderId);
          setArrivalLines(buildArrivalLineDrafts(defaultPo));
        } else {
          setSelectedPoId("");
          setArrivalLines([]);
        }

        if (list.items.length > 0) {
          const firstId = list.items[0].goodsReceiptId;
          setSelectedReceiptId(firstId);
          fetchGoodsReceiptDetail(tenantId, entityId, firstId)
            .then((detail) => {
              if (!active) return;
              setSelectedDetail(detail);
              setDockDrafts(mapDockDrafts(detail));
              setGate2QtyInputs(buildGate2QtyInputs(detail));
              setGate2Preview(null);
              setGate2PreviewStale(false);
            })
            .catch(() => {
              // Keep initial ledger usable even if auto-detail load hits transient error.
            });
        }
      })
      .catch((err) => {
        if (!active) return;
        const apiProblem = err as ApiProblemError;
        if (apiProblem.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
        setProblem(apiProblem);
        setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, retryNonce]);

  const refreshData = useCallback(
    async (opts?: {
      receiptId?: string | null;
      skipDetailFetch?: boolean;
      search?: string;
      status?: string;
    }) => {
      setLedgerLoading(true);
      try {
        const effectiveSearch = opts?.search !== undefined ? opts.search : searchFilter;
        const effectiveStatus = opts?.status !== undefined ? opts.status : statusFilter;
        const [list, eligible] = await Promise.all([
          fetchGoodsReceipts(tenantId, entityId, {
            search: effectiveSearch || undefined,
            status: effectiveStatus || undefined,
          }),
          fetchEligibleReceivingPos(tenantId, entityId),
        ]);
        setReceiptsData(list);
        setEligiblePos(eligible.items);

        if (!opts?.skipDetailFetch) {
          const targetId = opts?.receiptId ?? selectedReceiptId;
          if (targetId) {
            const detail = await fetchGoodsReceiptDetail(tenantId, entityId, targetId);
            setSelectedReceiptId(targetId);
            setSelectedDetail(detail);
            setDockDrafts(mapDockDrafts(detail));
            setGate2QtyInputs(buildGate2QtyInputs(detail));
            setGate2Preview(null);
            setGate2PreviewStale(false);
          }
        }
      } catch (err) {
        const apiProblem = err as ApiProblemError;
        if (apiProblem.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
        setProblem(apiProblem);
      } finally {
        setLedgerLoading(false);
      }
    },
    [tenantId, entityId, searchFilter, statusFilter, selectedReceiptId],
  );

  const handleOpenReceipt = async (receiptId: string) => {
    setSelectedReceiptId(receiptId);
    setDetailLoading(true);
    setDockErrors({});
    setDockSuccessLineId(null);
    setGate2Error(null);
    setGate2Success(null);
    setGate2Preview(null);
    setGate2PreviewStale(false);
    setGate2IdemKey(makeIdempotencyKey("acc"));
    try {
      const detail = await fetchGoodsReceiptDetail(tenantId, entityId, receiptId);
      setSelectedDetail(detail);
      setDockDrafts(mapDockDrafts(detail));
      setGate2QtyInputs(buildGate2QtyInputs(detail));
      setGate2LocInputs({});
      setGate2LotInputs({});
      setGate2ExpInputs({});
    } catch (err) {
      const apiProblem = err as ApiProblemError;
      if (apiProblem.status === 401) {
        routerRef.current.replace("/login");
        return;
      }
      setProblem(apiProblem);
    } finally {
      setDetailLoading(false);
    }
  };

  const handleSelectPo = (poId: string) => {
    setSelectedPoId(poId);
    setArrivalError(null);
    const chosen = eligiblePos.find((po) => po.purchaseOrderId === poId) ?? null;
    setArrivalLines(buildArrivalLineDrafts(chosen));
  };

  const handleSubmitArrival = async (e: React.FormEvent) => {
    e.preventDefault();
    setArrivalError(null);
    setArrivalSuccess(null);

    if (!selectedPoId) {
      setArrivalError(t.receiving.validation.poRequired);
      return;
    }
    if (!deliveryNote.trim()) {
      setArrivalError(t.receiving.validation.deliveryNoteRequired);
      return;
    }
    if (!receivedDate.trim()) {
      setArrivalError(t.receiving.validation.receivedDateRequired);
      return;
    }
    if (!receivingStation.trim()) {
      setArrivalError(t.receiving.validation.stationRequired);
      return;
    }

    const linesToSubmit: {
      poLineId: string;
      receivedUomId: string;
      physicallyDeliveredQuantity: number;
      flagForInspection: boolean;
      discrepancyReasonCode: string | null;
      discrepancyNotes: string | null;
      conditionEvidenceRef: string | null;
    }[] = [];

    for (const draft of arrivalLines) {
      if (!draft.qtyText.trim()) continue;
      const qtyScaled = parseScaledBigInt(draft.qtyText);
      if (qtyScaled === null) {
        setArrivalError(t.receiving.validation.invalidQtyFormat);
        return;
      }
      if (qtyScaled === 0n) continue;
      linesToSubmit.push({
        poLineId: draft.poLineId,
        receivedUomId: draft.receivedUomId,
        physicallyDeliveredQuantity: Number(formatScaledPlainInput(qtyScaled)),
        flagForInspection: draft.flagForInspection,
        discrepancyReasonCode: draft.discrepancyReasonCode || "NONE",
        discrepancyNotes: draft.discrepancyNotes.trim() || null,
        conditionEvidenceRef: draft.conditionEvidenceRef.trim() || null,
      });
    }

    if (linesToSubmit.length === 0) {
      setArrivalError(t.receiving.validation.arrivalLinesRequired);
      return;
    }

    setArrivalSubmitting(true);
    try {
      const created = await recordGoodsReceiptArrival(tenantId, entityId, {
        purchaseOrderId: selectedPoId,
        supplierDeliveryNoteNumber: deliveryNote.trim(),
        idempotencyKey: arrivalDraftKey,
        receivedDate: receivedDate.trim(),
        receivingStation: receivingStation.trim(),
        vehicleOrSealRef: vehicleRef.trim() || null,
        documentEvidenceRef: documentRef.trim() || null,
        notes: arrivalNotes.trim() || null,
        lines: linesToSubmit,
      });

      const replayNote = created.wasIdempotentReplay
        ? ` ${t.receiving.arrivalReplaySuffix}`
        : "";
      setArrivalSuccess(
        `${t.receiving.arrivalSuccessPrefix} ${created.receiptNumber}${replayNote}`,
      );
      setDeliveryNote("");
      setVehicleRef("");
      setDocumentRef("");
      setArrivalNotes("");
      setArrivalDraftKey(makeIdempotencyKey("arr"));
      setSelectedReceiptId(created.goodsReceiptId);
      setSelectedDetail(created);
      setDockDrafts(mapDockDrafts(created));
      await refreshData({ skipDetailFetch: true });
      setActiveTab("ledger");
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr.status === 401) {
        routerRef.current.replace("/login");
        return;
      }
      setArrivalError(mapApiErrorText(err));
    } finally {
      setArrivalSubmitting(false);
    }
  };

  const applyDockPreset = (
    line: GoodsReceiptLineDto,
    preset: "release_cap" | "hold_all" | "reject_all",
  ) => {
    const delivered = parseScaledBigInt(line.physicallyDeliveredBaseQuantity) ?? 0n;
    const valued = parseScaledBigInt(line.valuedAcceptedBaseQuantity) ?? 0n;
    const maxCap = parseScaledBigInt(line.maxApprovedReleaseCapBaseQty) ?? 0n;

    let nextHeld = 0n;
    let nextReleased = valued;
    let nextRejected = 0n;
    let nextReason = "NONE";

    if (preset === "release_cap") {
      const targetRelease = delivered < maxCap ? delivered : maxCap;
      nextReleased = targetRelease > valued ? targetRelease : valued;
      nextHeld = delivered > nextReleased ? delivered - nextReleased : 0n;
      nextReason = nextHeld > 0n ? "OVERDELIVERY_OD01_HOLD" : "NONE";
    } else if (preset === "hold_all") {
      nextHeld = delivered > valued ? delivered - valued : 0n;
      nextReleased = valued;
      nextReason =
        (parseScaledBigInt(line.overdeliveryExcessBaseQuantity) ?? 0n) > 0n
          ? "OVERDELIVERY_OD01_HOLD"
          : "DAMAGED_PACKAGING";
    } else {
      nextRejected = delivered > valued ? delivered - valued : 0n;
      nextReleased = valued;
      nextReason = "DAMAGED_PACKAGING";
    }

    setDockDrafts((prev) => {
      const existing = prev[line.goodsReceiptLineId] ?? buildDockDraftFromLine(line);
      return {
        ...prev,
        [line.goodsReceiptLineId]: {
          ...existing,
          pendingText: "0",
          heldText: formatScaledPlainInput(nextHeld),
          releasedText: formatScaledPlainInput(nextReleased),
          rejectedText: formatScaledPlainInput(nextRejected),
          discrepancyReasonCode:
            existing.discrepancyReasonCode !== "NONE"
              ? existing.discrepancyReasonCode
              : nextReason,
        },
      };
    });
  };

  const handleSubmitDockCheck = async (line: GoodsReceiptLineDto) => {
    if (!selectedDetail) return;
    const draft = dockDrafts[line.goodsReceiptLineId] ?? buildDockDraftFromLine(line);
    setDockErrors((prev) => {
      const copy = { ...prev };
      delete copy[line.goodsReceiptLineId];
      return copy;
    });
    setDockSuccessLineId(null);

    const evaluation = evaluateDockCheckPreview({
      deliveredBase: line.physicallyDeliveredBaseQuantity,
      valuedAcceptedBase: line.valuedAcceptedBaseQuantity,
      maxReleaseCapBase: line.maxApprovedReleaseCapBaseQty,
      pendingRaw: draft.pendingText,
      heldRaw: draft.heldText,
      releasedRaw: draft.releasedText,
      rejectedRaw: draft.rejectedText,
    });

    if (!evaluation.validFormat) {
      setDockErrors((prev) => ({
        ...prev,
        [line.goodsReceiptLineId]: t.receiving.validation.invalidQtyFormat,
      }));
      return;
    }
    if (!evaluation.balanced) {
      setDockErrors((prev) => ({
        ...prev,
        [line.goodsReceiptLineId]: t.receiving.validation.conservationMismatch,
      }));
      return;
    }

    if (
      evaluation.hasHoldOrReject &&
      draft.discrepancyReasonCode === "NONE" &&
      !draft.inspectionNotes.trim()
    ) {
      setDockErrors((prev) => ({
        ...prev,
        [line.goodsReceiptLineId]: t.receiving.validation.discrepancyReasonRequired,
      }));
      return;
    }

    const p = parseScaledBigInt(draft.pendingText) ?? 0n;
    const h = parseScaledBigInt(draft.heldText) ?? 0n;
    const r = parseScaledBigInt(draft.releasedText) ?? 0n;
    const x = parseScaledBigInt(draft.rejectedText) ?? 0n;

    setDockSubmittingLineId(line.goodsReceiptLineId);
    try {
      const updated = await recordGoodsReceiptDockCheck(
        tenantId,
        entityId,
        selectedDetail.goodsReceiptId,
        line.goodsReceiptLineId,
        {
          idempotencyKey: draft.idempotencyKey,
          pendingCheckBaseQuantity: Number(formatScaledPlainInput(p)),
          dockHeldBaseQuantity: Number(formatScaledPlainInput(h)),
          dockReleasedBaseQuantity: Number(formatScaledPlainInput(r)),
          dockRejectedBaseQuantity: Number(formatScaledPlainInput(x)),
          discrepancyReasonCode: draft.discrepancyReasonCode || "NONE",
          inspectionNotes: draft.inspectionNotes.trim() || null,
          evidenceReference: draft.evidenceReference.trim() || null,
        },
      );

      setSelectedDetail(updated);
      setDockDrafts(mapDockDrafts(updated));
      setGate2QtyInputs(buildGate2QtyInputs(updated));
      setGate2Preview(null);
      setGate2PreviewStale(false);
      setDockSuccessLineId(line.goodsReceiptLineId);
      await refreshData({ skipDetailFetch: true });
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr.status === 401) {
        routerRef.current.replace("/login");
        return;
      }
      setDockErrors((prev) => ({
        ...prev,
        [line.goodsReceiptLineId]: mapApiErrorText(err),
      }));
    } finally {
      setDockSubmittingLineId(null);
    }
  };

  const formatGate2BlockText = useCallback(
    (rawReason: string): string => {
      const colonIdx = rawReason.indexOf(":");
      const candidateCode = (colonIdx > 0 ? rawReason.slice(0, colonIdx) : rawReason).trim();
      if (t.receiving.errors[candidateCode]) {
        return t.receiving.errors[candidateCode];
      }
      return rawReason;
    },
    [t],
  );

  const buildGate2PayloadLines = useCallback(():
    | { ok: true; lines: AcceptReceiptLineRequest[] }
    | { ok: false; message: string } => {
    if (!selectedDetail) {
      return { ok: false, message: t.receiving.validation.acceptanceLinesRequired };
    }
    const collected: AcceptReceiptLineRequest[] = [];
    for (const line of selectedDetail.lines) {
      const remaining = getLineRemainingReleasedScaled(line);
      if (remaining <= 0n || line.itemKindSnapshot !== "STOCKED_GOOD") continue;

      const rawText = (gate2QtyInputs[line.goodsReceiptLineId] ?? "").trim();
      if (!rawText) continue;
      const parsed = parseScaledBigInt(rawText);
      if (parsed === null || parsed < 0n) {
        return { ok: false, message: t.receiving.validation.invalidQtyFormat };
      }
      if (parsed > 0n) {
        const chosenLoc = (gate2LocInputs[line.goodsReceiptLineId] ?? "").trim();
        const chosenLot = (gate2LotInputs[line.goodsReceiptLineId] ?? "").trim();
        const chosenExp = (gate2ExpInputs[line.goodsReceiptLineId] ?? "").trim();
        collected.push({
          goodsReceiptLineId: line.goodsReceiptLineId,
          acceptedBaseQuantity: Number(formatScaledPlainInput(parsed)),
          destinationLocationId: chosenLoc || null,
          lotNumber: chosenLot || null,
          expiryDate: chosenExp || null,
        });
      }
    }
    if (collected.length === 0) {
      return { ok: false, message: t.receiving.validation.acceptanceLinesRequired };
    }
    return { ok: true, lines: collected };
  }, [selectedDetail, gate2QtyInputs, gate2LocInputs, gate2LotInputs, gate2ExpInputs, t]);

  const handleGate2Preview = async () => {
    if (!selectedDetail) return;
    setGate2Error(null);
    setGate2Success(null);

    const parsed = buildGate2PayloadLines();
    if (!parsed.ok) {
      setGate2Error(parsed.message);
      return;
    }

    setGate2PreviewBusy(true);
    try {
      const preview = await previewReceiptAcceptance(
        tenantId,
        entityId,
        selectedDetail.goodsReceiptId,
        {
          acceptanceDate: gate2Date,
          stockStatus: gate2StockStatus,
          lines: parsed.lines,
        },
      );
      setGate2Preview(preview);
      setGate2PreviewStale(false);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr.status === 401) {
        routerRef.current.replace("/login");
        return;
      }
      setGate2Error(mapApiErrorText(err));
    } finally {
      setGate2PreviewBusy(false);
    }
  };

  const handleGate2Confirm = async () => {
    if (!selectedDetail || !gate2Preview?.canPost || gate2PreviewStale) return;
    setGate2Error(null);

    const parsed = buildGate2PayloadLines();
    if (!parsed.ok) {
      setGate2Error(parsed.message);
      return;
    }

    setGate2Submitting(true);
    try {
      const outcome = await acceptGoodsReceiptValued(
        tenantId,
        entityId,
        selectedDetail.goodsReceiptId,
        {
          idempotencyKey: gate2IdemKey,
          acceptanceDate: gate2Date,
          stockStatus: gate2StockStatus,
          notes: gate2Notes.trim() || null,
          lines: parsed.lines,
        },
      );
      setGate2Success(outcome);
      setGate2Notes("");
      setGate2Preview(null);
      setGate2PreviewStale(false);
      setGate2IdemKey(makeIdempotencyKey("acc"));
      await refreshData({ receiptId: selectedDetail.goodsReceiptId });
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr.status === 401) {
        routerRef.current.replace("/login");
        return;
      }
      setGate2Error(mapApiErrorText(err));
    } finally {
      setGate2Submitting(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-64 rounded-[var(--radius-sm)] bg-[var(--bg-chrome)]" />
        <div className="h-44 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]" />
      </div>
    );
  }

  if (problem && !workspace) {
    return <ReceivingErrorCard problem={problem} onRetry={() => setRetryNonce((n) => n + 1)} />;
  }

  const eligiblePoLineMap = new Map<string, EligiblePoReceivingLineDto>();
  if (selectedEligiblePo) {
    for (const poLine of selectedEligiblePo.lines) {
      eligiblePoLineMap.set(poLine.poLineId, poLine);
    }
  }

  const glCount = Number(receiptsData?.totalLinkedGlJournals ?? 0);
  const heldSummaryScaled = parseScaledBigInt(receiptsData?.sumHeldBaseQuantity ?? 0) ?? 0n;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      {/* Page Header */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 text-xs font-semibold text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.receiving.backToOverview}</span>
          </Link>
          <h1 className="font-heading text-xl font-bold tracking-tight text-[var(--text-primary)] md:text-2xl">
            {t.receiving.pageTitle}
          </h1>
          {workspace ? (
            <p className="text-xs text-[var(--text-secondary)]">
              <span className="font-code font-semibold tabular-nums">
                {workspace.tenant.code} · {workspace.entity.code}
              </span>{" "}
              — {workspace.entity.name} ({workspace.entity.functionalCurrencyCode})
            </p>
          ) : null}
        </div>
      </div>

      {/* Inline Runtime Error Alert (if partial reload failed) */}
      {problem ? (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-sm text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{mapApiErrorText(problem)}</span>
        </div>
      ) : null}

      {/* Read-Only / Gate 1 Boundary & OD-01 Notice */}
      <div className="space-y-2.5">
        {!canManage && !canAccept ? (
          <div
            data-testid="receiving-readonly-banner"
            className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-4 py-3 text-sm text-[var(--status-warn-text)]"
          >
            <Eye className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.receiving.readOnlyBanner}</span>
          </div>
        ) : null}

        <div className="grid grid-cols-1 gap-2.5 lg:grid-cols-2">
          <div className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-4 py-3 text-xs leading-5 text-[var(--text-secondary)]">
            <ShieldCheck
              className="mt-0.5 h-4 w-4 shrink-0 text-[var(--text-secondary)]"
              strokeWidth={1.75}
              aria-hidden="true"
            />
            <span>{t.receiving.gateOneZeroValuationBanner}</span>
          </div>
          <div className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-4 py-3 text-xs leading-5 text-[var(--status-warn-text)]">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.receiving.od01PolicyBanner}</span>
          </div>
        </div>
      </div>

      {/* KPI Strip */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.receiving.stats.totalReceipts}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {String(receiptsData?.totalReceipts ?? 0)}
          </div>
        </div>

        <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.receiving.stats.deliveredBase}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatQuantityValue(receiptsData?.sumDeliveredBaseQuantity ?? 0, locale)}
          </div>
        </div>

        <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.receiving.stats.heldBase}
          </div>
          <div
            className={`font-code mt-1 text-lg font-bold tabular-nums ${
              heldSummaryScaled > 0n ? "text-[var(--status-warn-text)]" : "text-[var(--text-primary)]"
            }`}
          >
            {formatQuantityValue(receiptsData?.sumHeldBaseQuantity ?? 0, locale)}
          </div>
        </div>

        <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.receiving.stats.releasedBase}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatQuantityValue(receiptsData?.sumReleasedBaseQuantity ?? 0, locale)}
          </div>
        </div>

        <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.receiving.stats.rejectedBase}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatQuantityValue(receiptsData?.sumRejectedBaseQuantity ?? 0, locale)}
          </div>
        </div>

        <div
          data-testid="gate1-gl-count-card"
          className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5"
        >
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.receiving.stats.linkedGlJournals}
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-1.5">
            <span className="font-code text-lg font-bold tabular-nums text-[var(--text-primary)]">
              {String(glCount)}
            </span>
            <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)]">
              <ShieldCheck className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{t.receiving.noGlPostingBadge}</span>
            </span>
          </div>
        </div>
      </div>

      {/* Multi-Tab Segmented Switch (only when user can manage Gate 1 arrivals) */}
      {canManage ? (
        <div
          role="tablist"
          aria-label={t.receiving.tabListLabel}
          className="inline-flex flex-wrap gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-1"
        >
          <button
            type="button"
            id="rcv-tab-ledger"
            role="tab"
            aria-selected={activeTab === "ledger"}
            aria-controls="rcv-panel-ledger"
            onClick={() => setActiveTab("ledger")}
            className={`inline-flex min-h-9 items-center gap-2 rounded-[var(--radius-sm)] px-3.5 text-sm font-semibold transition-colors ${
              activeTab === "ledger"
                ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
            }`}
          >
            <PackageCheck className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.receiving.tabs.ledger}</span>
          </button>
          <button
            type="button"
            id="rcv-tab-arrival"
            role="tab"
            aria-selected={activeTab === "arrival"}
            aria-controls="rcv-panel-arrival"
            onClick={() => setActiveTab("arrival")}
            className={`inline-flex min-h-9 items-center gap-2 rounded-[var(--radius-sm)] px-3.5 text-sm font-semibold transition-colors ${
              activeTab === "arrival"
                ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
            }`}
          >
            <PlusCircle className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.receiving.tabs.arrival}</span>
          </button>
        </div>
      ) : null}

      {/* Arrival Success Confirmation Banner */}
      {arrivalSuccess ? (
        <div
          data-testid="rcv-arrival-success"
          role="status"
          className="flex items-start gap-2 rounded-[var(--radius-md)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-4 py-3 text-sm font-medium text-[var(--status-ok-text)]"
        >
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{arrivalSuccess}</span>
        </div>
      ) : null}

      {/* TAB 1: RECEIPT REGISTER + INSPECTION DETAIL */}
      {activeTab === "ledger" || !canManage ? (
        <div
          id={canManage ? "rcv-panel-ledger" : undefined}
          role={canManage ? "tabpanel" : undefined}
          aria-labelledby={canManage ? "rcv-tab-ledger" : undefined}
          className="space-y-5"
        >
          {/* Receipt Register Sheet */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <div className="flex items-center gap-2">
                <PackageCheck
                  className="h-4 w-4 text-[var(--accent-primary)]"
                  strokeWidth={1.75}
                  aria-hidden="true"
                />
                <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                  {t.receiving.registerCardTitle}
                </h2>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <input
                  type="search"
                  aria-label={t.receiving.filterSearchLabel}
                  placeholder={t.receiving.filterSearchPlaceholder}
                  value={searchFilter}
                  onChange={(e) => setSearchFilter(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      void refreshData({ search: searchFilter, status: statusFilter });
                    }
                  }}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] placeholder:text-[var(--text-muted)] sm:w-64"
                />
                <select
                  aria-label={t.receiving.filterStatusLabel}
                  value={statusFilter}
                  onChange={(e) => {
                    const nextStatus = e.target.value;
                    setStatusFilter(nextStatus);
                    void refreshData({ search: searchFilter, status: nextStatus });
                  }}
                  className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="">{t.receiving.allStatusesOption}</option>
                  {RECEIPT_FILTER_STATUSES.map((st) => (
                    <option key={st} value={st}>
                      {t.receiving.receiptStatusLabels[st] ?? st}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  disabled={ledgerLoading}
                  onClick={() => void refreshData({ search: searchFilter, status: statusFilter })}
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                >
                  <RefreshCw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  <span>{t.receiving.refreshButton}</span>
                </button>
              </div>
            </div>

            {!receiptsData || receiptsData.items.length === 0 ? (
              <div className="p-6 text-center">
                <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                  {t.receiving.emptyLedgerTitle}
                </h3>
                <p className="mx-auto mt-1 max-w-lg text-xs text-[var(--text-secondary)]">
                  {t.receiving.emptyLedgerBody}
                </p>
              </div>
            ) : (
              <div className="md:overflow-x-auto">
                <table className="w-full border-collapse text-left text-sm">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2.5 md:pl-4 md:pr-2.5">
                        {t.receiving.colReceipt}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5">
                        {t.receiving.colPoAndSupplier}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5">
                        {t.receiving.colStationAndDate}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5 text-right">
                        {t.receiving.colQuantities}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5">
                        {t.receiving.colStatus}
                      </th>
                      <th scope="col" className="py-2.5 md:pl-2.5 md:pr-4 text-right">
                        {t.receiving.colAction}
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {receiptsData.items.map((item) => {
                      const isSelected = item.goodsReceiptId === selectedReceiptId;
                      const statusLabel =
                        t.receiving.receiptStatusLabels[item.status] ?? item.status;
                      return (
                        <tr
                          key={item.goodsReceiptId}
                          className={`block p-4 md:table-row md:p-0 ${
                            isSelected ? "bg-[var(--bg-subtle)]" : "hover:bg-[var(--bg-subtle)]/60"
                          }`}
                        >
                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-3 md:pl-4 md:pr-2.5">
                            <span className="text-xs font-medium text-[var(--text-secondary)] md:hidden">
                              {t.receiving.colReceipt}
                            </span>
                            <div className="sm:text-right md:text-left">
                              <div className="font-code text-xs font-semibold tabular-nums sm:whitespace-nowrap text-[var(--text-primary)]">
                                {item.receiptNumber}
                              </div>
                              <div className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                                {item.supplierDeliveryNoteNumber}
                              </div>
                            </div>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-3 md:px-2.5">
                            <span className="text-xs font-medium text-[var(--text-secondary)] md:hidden">
                              {t.receiving.colPoAndSupplier}
                            </span>
                            <div className="sm:text-right md:text-left">
                              <div className="font-code text-xs font-semibold tabular-nums sm:whitespace-nowrap text-[var(--text-primary)]">
                                {item.poNumberSnapshot} · Rev {String(item.poRevisionNumberSnapshot)}
                              </div>
                              <div className="text-xs text-[var(--text-secondary)]">
                                {item.supplierCodeSnapshot} — {item.supplierLegalNameSnapshot}
                              </div>
                            </div>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-3 md:px-2.5">
                            <span className="text-xs font-medium text-[var(--text-secondary)] md:hidden">
                              {t.receiving.colStationAndDate}
                            </span>
                            <div className="sm:text-right md:text-left">
                              <div className="text-xs text-[var(--text-primary)]">
                                {item.receivingStation}
                              </div>
                              <div className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                                {item.receivedDate}
                              </div>
                            </div>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-3 md:px-2.5 md:text-right">
                            <span className="text-xs font-medium text-[var(--text-secondary)] md:hidden">
                              {t.receiving.colQuantities}
                            </span>
                            <div className="font-code text-xs tabular-nums text-[var(--text-primary)]">
                              <div className="sm:whitespace-nowrap">
                                {t.receiving.splitCodes.delivered}{" "}
                                {formatQuantityValue(item.totalDeliveredBaseQuantity, locale)} ·{" "}
                                {t.receiving.splitCodes.released}{" "}
                                {formatQuantityValue(item.totalReleasedBaseQuantity, locale)}
                              </div>
                              <div className="text-[var(--text-secondary)] sm:whitespace-nowrap">
                                {t.receiving.splitCodes.held}{" "}
                                {formatQuantityValue(item.totalHeldBaseQuantity, locale)} ·{" "}
                                {t.receiving.splitCodes.rejected}{" "}
                                {formatQuantityValue(item.totalRejectedBaseQuantity, locale)}
                              </div>
                            </div>
                          </td>

                          <td className="flex items-center justify-between gap-3 py-1.5 md:table-cell md:py-3 md:px-2.5">
                            <span className="text-xs font-medium text-[var(--text-secondary)] md:hidden">
                              {t.receiving.colStatus}
                            </span>
                            <ReceivingStatusBadge status={item.status} label={statusLabel} />
                          </td>

                          <td className="flex items-center justify-end pt-2 md:table-cell md:py-3 md:pl-2.5 md:pr-4 md:text-right">
                            <button
                              type="button"
                              onClick={() => void handleOpenReceipt(item.goodsReceiptId)}
                              className={`inline-flex min-h-9 items-center whitespace-nowrap rounded-[var(--radius-sm)] border px-3 text-xs font-semibold ${
                                isSelected
                                  ? "border-[var(--border-strong)] bg-[var(--bg-chrome)] text-[var(--text-primary)]"
                                  : "border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                              }`}
                            >
                              {isSelected
                                ? t.receiving.activeDetailButton
                                : t.receiving.openDetailButton}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* Receipt Detail & Gate 1 Dock Inspection Sheet */}
          <section
            id="rcv-detail-sheet"
            className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
          >
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <div className="flex items-center gap-2">
                <ClipboardCheck
                  className="h-4 w-4 text-[var(--accent-primary)]"
                  strokeWidth={1.75}
                  aria-hidden="true"
                />
                <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                  {t.receiving.detailCardTitle}
                </h2>
              </div>
              {selectedDetail ? (
                <div className="flex flex-wrap items-center gap-2">
                  {Number(selectedDetail.valuedAcceptanceJournalCount) > 0 ? (
                    <>
                      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-2 py-0.5 text-xs font-medium text-[var(--text-secondary)]">
                        <ShieldCheck className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.receiving.gate2.gate1GlBadgePrefix}</span>
                        <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                          {String(selectedDetail.linkedGlJournalCount)}
                        </span>
                      </span>
                      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
                        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.receiving.gate2.glBadgePrefix}</span>
                        <span className="font-code font-semibold tabular-nums">
                          {String(selectedDetail.valuedAcceptanceJournalCount)}
                        </span>
                      </span>
                    </>
                  ) : (
                    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
                      <ShieldCheck className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                      <span className="font-code font-semibold tabular-nums">
                        GL: {String(selectedDetail.linkedGlJournalCount)}
                      </span>
                      <span>· {t.receiving.noGlPostingBadge}</span>
                    </span>
                  )}
                </div>
              ) : null}
            </div>

            {detailLoading ? (
              <div className="p-6 text-xs text-[var(--text-secondary)]">{t.receiving.loadingDetail}</div>
            ) : !selectedDetail ? (
              <div className="p-6 text-center">
                <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                  {t.receiving.selectReceiptPromptTitle}
                </h3>
                <p className="mx-auto mt-1 max-w-lg text-xs text-[var(--text-secondary)]">
                  {t.receiving.selectReceiptPromptBody}
                </p>
              </div>
            ) : (
              <div className="divide-y divide-[var(--border-hairline)]">
                {/* Header Snapshot Summary */}
                <div className="grid grid-cols-1 gap-px bg-[var(--border-hairline)] sm:grid-cols-2 lg:grid-cols-4">
                  <div className="bg-[var(--bg-subtle)] p-3.5">
                    <div className="text-xs text-[var(--text-secondary)]">
                      {t.receiving.fieldReceiptNumber}
                    </div>
                    <div className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--text-primary)]">
                      {selectedDetail.receiptNumber}
                    </div>
                    <div className="mt-1">
                      <ReceivingStatusBadge
                        status={selectedDetail.status}
                        label={
                          t.receiving.receiptStatusLabels[selectedDetail.status] ??
                          selectedDetail.status
                        }
                      />
                    </div>
                  </div>

                  <div className="bg-[var(--bg-subtle)] p-3.5">
                    <div className="text-xs text-[var(--text-secondary)]">
                      {t.receiving.fieldPoRef}
                    </div>
                    <div className="font-code mt-0.5 text-sm font-semibold tabular-nums text-[var(--text-primary)]">
                      {selectedDetail.poNumberSnapshot} · Rev{" "}
                      {String(selectedDetail.poRevisionNumberSnapshot)}
                    </div>
                    <div className="font-code truncate text-[11px] text-[var(--text-muted)]">
                      {t.receiving.hashPrefix} {selectedDetail.poSnapshotHash.slice(0, 14)}...
                    </div>
                  </div>

                  <div className="bg-[var(--bg-subtle)] p-3.5">
                    <div className="text-xs text-[var(--text-secondary)]">
                      {t.receiving.fieldDeliveryNote}
                    </div>
                    <div className="font-code mt-0.5 text-sm font-semibold tabular-nums text-[var(--text-primary)]">
                      {selectedDetail.supplierDeliveryNoteNumber}
                    </div>
                    <div className="text-xs text-[var(--text-secondary)]">
                      {selectedDetail.supplierCodeSnapshot} — {selectedDetail.supplierLegalNameSnapshot}
                    </div>
                  </div>

                  <div className="bg-[var(--bg-subtle)] p-3.5">
                    <div className="text-xs text-[var(--text-secondary)]">
                      {t.receiving.fieldStation}
                    </div>
                    <div className="mt-0.5 text-sm font-semibold text-[var(--text-primary)]">
                      {selectedDetail.receivingStation}
                    </div>
                    <div className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                      {selectedDetail.receivedDate}
                    </div>
                    <div className="font-code break-words text-[11px] text-[var(--text-secondary)]">
                      {selectedDetail.receivedByEmail.replace("@", "\u200B@")}
                    </div>
                  </div>
                </div>

                {/* Line Custody Table Summary */}
                <div>
                  <h3 className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
                    {t.receiving.fieldLineCustody}
                  </h3>
                  <div className="md:overflow-x-auto">
                    <table className="w-full border-collapse border-t border-[var(--border-hairline)] text-left text-xs">
                      <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                        <tr>
                          <th scope="col" className="py-2 md:pl-3 md:pr-2">
                            {t.receiving.colCustodyLine}
                          </th>
                          <th scope="col" className="py-2 md:px-2 text-right">
                            {t.receiving.colBaseSplit}
                          </th>
                          <th scope="col" className="py-2 md:px-2 text-right">
                            {t.receiving.colValuedAccepted}
                          </th>
                          <th scope="col" className="py-2 md:pl-2 md:pr-3 text-right">
                            {t.receiving.colOd01Excess}
                          </th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[var(--border-hairline)]">
                        {selectedDetail.lines.map((line) => (
                          <tr key={line.goodsReceiptLineId} className="block p-3 md:table-row md:p-0">
                            <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-3 md:pr-2">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                {t.receiving.colCustodyLine}
                              </span>
                              <div className="sm:text-right md:text-left">
                                <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                                  L{String(line.lineNumber)} · {line.itemCodeSnapshot}
                                </span>{" "}
                                <span className="text-[var(--text-secondary)]">
                                  {line.itemNameSnapshot} ({line.baseUomCodeSnapshot})
                                </span>
                              </div>
                            </td>
                            <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2 md:text-right">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                {t.receiving.colBaseSplit}
                              </span>
                              <span className="font-code tabular-nums md:whitespace-nowrap text-[var(--text-primary)]">
                                {formatQuantityValue(line.physicallyDeliveredBaseQuantity, locale)} /{" "}
                                {formatQuantityValue(line.pendingCheckBaseQuantity, locale)} /{" "}
                                {formatQuantityValue(line.dockHeldBaseQuantity, locale)} /{" "}
                                {formatQuantityValue(line.dockReleasedBaseQuantity, locale)} /{" "}
                                {formatQuantityValue(line.dockRejectedBaseQuantity, locale)}
                              </span>
                            </td>
                            <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2 md:text-right">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                {t.receiving.colValuedAccepted}
                              </span>
                              <span className="font-code tabular-nums md:whitespace-nowrap text-[var(--text-primary)]">
                                {formatQuantityValue(line.valuedAcceptedBaseQuantity, locale)}{" "}
                                {line.baseUomCodeSnapshot}
                              </span>
                            </td>
                            <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-2 md:pr-3 md:text-right">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                {t.receiving.colOd01Excess}
                              </span>
                              <span className="font-code tabular-nums md:whitespace-nowrap text-[var(--text-primary)]">
                                {t.receiving.capPrefix}{" "}
                                {formatQuantityValue(line.maxApprovedReleaseCapBaseQty, locale)} · +
                                {formatQuantityValue(line.overdeliveryExcessBaseQuantity, locale)}
                              </span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>

                {/* Per-line Dock Check Interactive Cards */}
                <div className="divide-y divide-[var(--border-hairline)]">
                  {selectedDetail.lines.map((line, index) => (
                    <DockCheckLineCard
                      key={line.goodsReceiptLineId}
                      line={line}
                      lineIndex={index}
                      draft={dockDrafts[line.goodsReceiptLineId] ?? buildDockDraftFromLine(line)}
                      canManage={canManage}
                      locale={locale}
                      saving={dockSubmittingLineId === line.goodsReceiptLineId}
                      lineError={dockErrors[line.goodsReceiptLineId] ?? null}
                      lineSuccess={dockSuccessLineId === line.goodsReceiptLineId}
                      onChangeDraft={(updater) =>
                        setDockDrafts((prev) => ({
                          ...prev,
                          [line.goodsReceiptLineId]: updater(
                            prev[line.goodsReceiptLineId] ?? buildDockDraftFromLine(line),
                          ),
                        }))
                      }
                      onApplyPreset={(preset) => applyDockPreset(line, preset)}
                      onSubmit={() => void handleSubmitDockCheck(line)}
                    />
                  ))}
                </div>

                {/* Append-Only Dock Inspection Audit Trail */}
                <div>
                  <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
                    {t.receiving.historyCardTitle}
                  </div>
                  {selectedDetail.dockChecks.length === 0 ? (
                    <div className="p-4 text-xs text-[var(--text-secondary)]">
                      {t.receiving.emptyHistoryBody}
                    </div>
                  ) : (
                    <div className="md:overflow-x-auto">
                      <table className="w-full border-collapse text-left text-xs">
                        <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                          <tr>
                            <th scope="col" className="py-2 md:pl-3.5 md:pr-2">
                              {t.receiving.colCheckedAt}
                            </th>
                            <th scope="col" className="py-2 md:px-2">
                              {t.receiving.colTransition}
                            </th>
                            <th scope="col" className="py-2 md:px-2 text-right">
                              {t.receiving.colSplitSnapshot}
                            </th>
                            <th scope="col" className="py-2 md:pl-2 md:pr-3.5">
                              {t.receiving.colReasonAndActor}
                            </th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--border-hairline)]">
                          {selectedDetail.dockChecks.map((chk) => (
                            <tr key={chk.dockCheckId} className="block p-3 md:table-row md:p-0">
                              <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-3.5 md:pr-2">
                                <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                  {t.receiving.colCheckedAt}
                                </span>
                                <span className="font-code tabular-nums text-[var(--text-primary)]">
                                  {formatJakartaDateTime(chk.checkedAtUtc, locale)}
                                </span>
                              </td>
                              <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2">
                                <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                  {t.receiving.colTransition}
                                </span>
                                <span className="font-medium text-[var(--text-primary)] sm:text-right md:text-left">
                                  {t.receiving.custodyStatusLabels[chk.previousCustodyStatus] ??
                                    chk.previousCustodyStatus}{" "}
                                  →{" "}
                                  {t.receiving.custodyStatusLabels[chk.resultingCustodyStatus] ??
                                    chk.resultingCustodyStatus}
                                </span>
                              </td>
                              <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2 md:text-right">
                                <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                  {t.receiving.colSplitSnapshot}
                                </span>
                                <span className="font-code tabular-nums md:whitespace-nowrap text-[var(--text-primary)]">
                                  {t.receiving.splitCodes.pending}{" "}
                                  {formatQuantityValue(chk.pendingCheckBaseQuantity, locale)} ·{" "}
                                  {t.receiving.splitCodes.held}{" "}
                                  {formatQuantityValue(chk.dockHeldBaseQuantity, locale)} ·{" "}
                                  {t.receiving.splitCodes.released}{" "}
                                  {formatQuantityValue(chk.dockReleasedBaseQuantity, locale)} ·{" "}
                                  {t.receiving.splitCodes.rejected}{" "}
                                  {formatQuantityValue(chk.dockRejectedBaseQuantity, locale)}
                                </span>
                              </td>
                              <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-2 md:pr-3.5">
                                <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                  {t.receiving.colReasonAndActor}
                                </span>
                                <div className="sm:text-right md:text-left">
                                  <div className="font-medium text-[var(--text-primary)]">
                                    {t.receiving.discrepancyReasonLabels[chk.discrepancyReasonCode] ??
                                      chk.discrepancyReasonCode}
                                  </div>
                                  <div className="text-[var(--text-secondary)]">
                                    {chk.inspectionNotes ? `${chk.inspectionNotes} · ` : ""}
                                    {chk.checkedByEmail}
                                  </div>
                                </div>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            )}
          </section>

          {/* Gate 2 Valued Acceptance Sections (sibling sheets below Gate 1 Detail) */}
          {!detailLoading && selectedDetail ? (
            <>
              {selectedDetail.lines.some((line) => getLineRemainingReleasedScaled(line) > 0n) ? (
                <section
                  id="gate2-acceptance-panel"
                  className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
                    <div className="flex items-center gap-2">
                      <PackageCheck
                        className="h-4 w-4 text-[var(--accent-primary)]"
                        strokeWidth={1.75}
                        aria-hidden="true"
                      />
                      <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                        {t.receiving.gate2.sectionTitle}
                      </h2>
                    </div>
                  </div>

                  <div className="divide-y divide-[var(--border-hairline)]">
                    <div className="flex items-start gap-2 bg-[var(--bg-subtle)] px-4 py-2.5 text-xs text-[var(--text-secondary)]">
                      <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                      <span>{t.receiving.gate2.od04Notice}</span>
                    </div>

                    {!canAccept ? (
                      <div className="flex items-start gap-2 bg-[var(--status-warn-bg)] px-4 py-2.5 text-xs text-[var(--status-warn-text)]">
                        <Eye className="mt-0.5 h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.receiving.gate2.readOnlyPermissionNote}</span>
                      </div>
                    ) : null}

                    <div className="space-y-4 p-4">
                      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.gate2.acceptanceDateLabel}
                          </span>
                          <input
                            id="gate2-acceptance-date"
                            type="date"
                            value={gate2Date}
                            onChange={(e) => {
                              setGate2Date(e.target.value);
                              if (gate2Preview) setGate2PreviewStale(true);
                            }}
                            className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.gate2.stockStatusLabel}
                          </span>
                          <select
                            id="gate2-stock-status"
                            value={gate2StockStatus}
                            onChange={(e) => {
                              setGate2StockStatus(e.target.value);
                              setGate2LocInputs({});
                              if (gate2Preview) setGate2PreviewStale(true);
                            }}
                            className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          >
                            {STOCK_STATUS_OPTIONS.map((code) => (
                              <option key={code} value={code}>
                                {t.receiving.stockStatusLabels[code] ?? code}
                              </option>
                            ))}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.gate2.notesLabel}
                          </span>
                          <input
                            id="gate2-notes"
                            type="text"
                            value={gate2Notes}
                            placeholder={t.receiving.gate2.notesPlaceholder}
                            onChange={(e) => setGate2Notes(e.target.value)}
                            className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                          />
                        </label>
                      </div>

                      {/* Line acceptance qty inputs */}
                      <div className="-mx-4 divide-y divide-[var(--border-hairline)] border-y border-[var(--border-hairline)]">
                          {selectedDetail.lines
                            .filter((l) => getLineRemainingReleasedScaled(l) > 0n)
                            .map((line, gIdx) => {
                              const remScaled = getLineRemainingReleasedScaled(line);
                              const lotPolicy = line.itemLotTrackingPolicy ?? "NONE";
                              const showsLotInputs = lotPolicy !== "NONE";
                              return (
                                <div
                                  key={line.goodsReceiptLineId}
                                  className="space-y-2.5 bg-[var(--bg-subtle)] px-4 py-3"
                                >
                                  <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                                    <div className="space-y-0.5 text-xs">
                                      <div className="flex flex-wrap items-center gap-1.5 text-[var(--text-primary)]">
                                        <span className="font-code font-semibold tabular-nums">
                                          L{String(line.lineNumber)} · {line.itemCodeSnapshot}
                                        </span>{" "}
                                        <span className="font-medium text-[var(--text-secondary)]">
                                          — {line.itemNameSnapshot}
                                        </span>
                                        {lotPolicy === "REQUIRED" ? (
                                          <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--status-warn-text)]">
                                            <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                                            <span>{t.receiving.gate2.lotRequiredBadge}</span>
                                          </span>
                                        ) : null}
                                      </div>
                                      <div className="text-[11px] text-[var(--text-secondary)]">
                                        <span>{t.receiving.gate2.colReleasedRemaining}:</span>{" "}
                                        <span className="font-code tabular-nums">
                                          {formatQuantityValue(formatScaledPlainInput(remScaled), locale)}{" "}
                                          {line.baseUomCodeSnapshot}
                                        </span>
                                      </div>
                                    </div>

                                    <label className="block space-y-1 sm:w-44">
                                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                                        {t.receiving.gate2.colAcceptQty} ({line.baseUomCodeSnapshot})
                                      </span>
                                      <input
                                        id={`gate2-qty-${gIdx}`}
                                        type="text"
                                        inputMode="decimal"
                                        disabled={!canAccept}
                                        value={gate2QtyInputs[line.goodsReceiptLineId] ?? ""}
                                        onChange={(e) => {
                                          const val = e.target.value;
                                          setGate2QtyInputs((prev) => ({
                                            ...prev,
                                            [line.goodsReceiptLineId]: val,
                                          }));
                                          if (gate2Preview) setGate2PreviewStale(true);
                                        }}
                                        className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums text-[var(--text-primary)] disabled:opacity-60"
                                      />
                                    </label>
                                  </div>

                                  <div className="grid grid-cols-1 gap-2 border-t border-[var(--border-hairline)] pt-2.5 sm:grid-cols-3">
                                    <label className="block space-y-1">
                                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                                        {t.receiving.gate2.locationLabel}
                                      </span>
                                      <select
                                        id={`gate2-loc-${gIdx}`}
                                        disabled={!canAccept}
                                        value={gate2LocInputs[line.goodsReceiptLineId] ?? ""}
                                        onChange={(e) => {
                                          const val = e.target.value;
                                          setGate2LocInputs((prev) => ({
                                            ...prev,
                                            [line.goodsReceiptLineId]: val,
                                          }));
                                          if (gate2Preview) setGate2PreviewStale(true);
                                        }}
                                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] disabled:opacity-60"
                                      >
                                        <option value="">{t.receiving.gate2.locationAutoOption}</option>
                                        {compatibleGate2Locations.map((loc) => (
                                          <option key={loc.locationId} value={loc.locationId}>
                                            {loc.warehouseCode} · {loc.locationCode} — {loc.name}
                                          </option>
                                        ))}
                                      </select>
                                    </label>

                                    {showsLotInputs ? (
                                      <>
                                        <label className="block space-y-1">
                                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                                            {t.receiving.gate2.lotNumberLabel}
                                          </span>
                                          <input
                                            id={`gate2-lot-${gIdx}`}
                                            type="text"
                                            disabled={!canAccept}
                                            value={gate2LotInputs[line.goodsReceiptLineId] ?? ""}
                                            placeholder={t.receiving.gate2.lotNumberPlaceholder}
                                            onChange={(e) => {
                                              const val = e.target.value;
                                              setGate2LotInputs((prev) => ({
                                                ...prev,
                                                [line.goodsReceiptLineId]: val,
                                              }));
                                              if (gate2Preview) setGate2PreviewStale(true);
                                            }}
                                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] placeholder:text-[var(--text-muted)] disabled:opacity-60"
                                          />
                                        </label>

                                        <label className="block space-y-1">
                                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                                            {t.receiving.gate2.expiryDateLabel}
                                          </span>
                                          <input
                                            id={`gate2-exp-${gIdx}`}
                                            type="date"
                                            disabled={!canAccept}
                                            value={gate2ExpInputs[line.goodsReceiptLineId] ?? ""}
                                            onChange={(e) => {
                                              const val = e.target.value;
                                              setGate2ExpInputs((prev) => ({
                                                ...prev,
                                                [line.goodsReceiptLineId]: val,
                                              }));
                                              if (gate2Preview) setGate2PreviewStale(true);
                                            }}
                                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)] disabled:opacity-60"
                                          />
                                        </label>
                                      </>
                                    ) : (
                                      <div className="flex items-end pb-1 text-[11px] text-[var(--text-muted)] sm:col-span-2">
                                        {t.receiving.gate2.lotDisabledNote}
                                      </div>
                                    )}
                                  </div>
                                </div>
                              );
                            })}
                      </div>

                      {gate2PreviewStale && gate2Preview ? (
                        <div
                          role="status"
                          className="flex items-start gap-2 rounded-[var(--radius-sm)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]"
                        >
                          <Clock className="mt-0.5 h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>{t.receiving.gate2.previewStaleNotice}</span>
                        </div>
                      ) : null}

                      {gate2Error ? (
                        <div
                          id="gate2-error"
                          role="alert"
                          className="flex items-start gap-2 rounded-[var(--radius-sm)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                        >
                          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>{gate2Error}</span>
                        </div>
                      ) : null}

                      <div className="flex flex-wrap items-center justify-end gap-2">
                        <button
                          type="button"
                          id="gate2-preview-btn"
                          disabled={gate2PreviewBusy || gate2Submitting}
                          onClick={() => void handleGate2Preview()}
                          className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                        >
                          <Eye className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>
                            {gate2PreviewBusy ? t.receiving.gate2.previewBusy : t.receiving.gate2.previewButton}
                          </span>
                        </button>

                        {canAccept ? (
                          <button
                            type="button"
                            id="gate2-confirm-btn"
                            disabled={
                              gate2Submitting ||
                              gate2PreviewBusy ||
                              !gate2Preview?.canPost ||
                              gate2PreviewStale
                            }
                            onClick={() => void handleGate2Confirm()}
                            className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-50"
                          >
                            <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            <span>
                              {gate2Submitting
                                ? t.receiving.gate2.confirmBusy
                                : t.receiving.gate2.confirmButton}
                            </span>
                          </button>
                        ) : null}
                      </div>
                    </div>

                    {/* Preview Result Breakdown */}
                    {gate2Preview ? (
                      <div
                        id="gate2-preview-card"
                        className="divide-y divide-[var(--border-hairline)] bg-[var(--bg-subtle)]"
                      >
                        <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5">
                          <span className="text-xs font-semibold text-[var(--text-primary)]">
                            {t.receiving.gate2.previewCardTitle}
                          </span>
                          <span className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(gate2Preview.totalAcceptedAmount)} ({gate2Preview.currencyCode})
                          </span>
                        </div>

                        {!gate2Preview.canPost && gate2Preview.blockingReasons.length > 0 ? (
                          <div
                            id="gate2-preview-blocked"
                            role="alert"
                            className="flex items-start gap-2 bg-[var(--status-danger-bg)] px-4 py-2.5 text-xs text-[var(--status-danger-text)]"
                          >
                            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            <div className="space-y-1">
                              <div className="font-semibold">{t.receiving.gate2.blockedHeading}</div>
                              <ul className="list-inside list-disc space-y-0.5">
                                {gate2Preview.blockingReasons.map((reason, rIdx) => (
                                  <li key={rIdx}>{formatGate2BlockText(reason)}</li>
                                ))}
                              </ul>
                            </div>
                          </div>
                        ) : null}

                        {gate2Preview.lines.length > 0 ? (
                          <div className="md:overflow-x-auto">
                            <table className="w-full border-collapse bg-[var(--bg-surface)] text-left text-xs">
                              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                                  <tr>
                                    <th scope="col" className="py-2 md:pl-3 md:pr-2">
                                      {t.receiving.gate2.colItem}
                                    </th>
                                    <th scope="col" className="py-2 md:px-2 text-right">
                                      {t.receiving.gate2.colPrevCost}
                                    </th>
                                    <th scope="col" className="py-2 md:px-2 text-right">
                                      {t.receiving.gate2.colProjectedCost}
                                    </th>
                                    <th scope="col" className="py-2 md:pl-2 md:pr-3 text-right">
                                      {t.receiving.gate2.colAcceptedAmt}
                                    </th>
                                  </tr>
                                </thead>
                                <tbody className="divide-y divide-[var(--border-hairline)]">
                                  {gate2Preview.lines.map((pl) => (
                                    <tr key={pl.goodsReceiptLineId} className="block p-2.5 md:table-row md:p-0">
                                      <td className="flex flex-col gap-0.5 py-1 sm:flex-row sm:justify-between md:table-cell md:py-2 md:pl-3 md:pr-2">
                                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                          {t.receiving.gate2.colItem}
                                        </span>
                                        <div>
                                          <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                                            {pl.itemCode} · +{formatQuantityValue(pl.acceptedBaseQuantity, locale)}{" "}
                                            {pl.baseUomCode} @ {formatUnitCostIdr(pl.unitCostBase)}
                                          </div>
                                          {pl.destinationLocationCode || pl.lotNumber ? (
                                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                                              {pl.destinationLocationCode ?? ""}
                                              {pl.destinationLocationCode && pl.lotNumber ? " · " : ""}
                                              {pl.lotNumber ?? ""}
                                            </div>
                                          ) : null}
                                        </div>
                                      </td>
                                      <td className="flex flex-col gap-0.5 py-1 sm:flex-row sm:justify-between md:table-cell md:py-2 md:px-2 md:text-right">
                                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                          {t.receiving.gate2.colPrevCost}
                                        </span>
                                        <span className="font-code tabular-nums text-[var(--text-secondary)]">
                                          {formatQuantityValue(pl.previousPoolBaseQuantity, locale)} ·{" "}
                                          {formatUnitCostIdr(pl.previousMovingAverageCost)}
                                        </span>
                                      </td>
                                      <td className="flex flex-col gap-0.5 py-1 sm:flex-row sm:justify-between md:table-cell md:py-2 md:px-2 md:text-right">
                                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                          {t.receiving.gate2.colProjectedCost}
                                        </span>
                                        <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                                          {formatQuantityValue(pl.projectedPoolBaseQuantity, locale)} ·{" "}
                                          {formatUnitCostIdr(pl.projectedMovingAverageCost)}
                                        </span>
                                      </td>
                                      <td className="flex flex-col gap-0.5 py-1 sm:flex-row sm:justify-between md:table-cell md:py-2 md:pl-2 md:pr-3 md:text-right">
                                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                          {t.receiving.gate2.colAcceptedAmt}
                                        </span>
                                        <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                                          {formatIdr(pl.acceptedLineAmount)}
                                        </span>
                                      </td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          ) : null}

                        {gate2Preview.journalPreview.length > 0 ? (
                          <div>
                            <div className="px-4 py-2 text-[11px] font-semibold text-[var(--text-secondary)]">
                              {t.receiving.gate2.journalPreviewTitle}
                            </div>
                            <div className="grid grid-cols-1 gap-px border-t border-[var(--border-hairline)] bg-[var(--border-hairline)] sm:grid-cols-2">
                              {gate2Preview.journalPreview.map((jl, jIdx) => (
                                <div
                                  key={jIdx}
                                  className="flex items-center justify-between gap-2 bg-[var(--bg-surface)] px-4 py-2.5 text-xs"
                                >
                                  <div>
                                    <span className="font-semibold text-[var(--text-primary)]">
                                      {t.receiving.gate2.sideLabels[jl.side] ?? jl.side}{" "}
                                    </span>
                                    <span className="font-code tabular-nums text-[var(--text-secondary)]">
                                      {jl.accountCode}
                                    </span>
                                    <span className="text-[var(--text-secondary)]">
                                      {" · "}{jl.accountName}
                                    </span>
                                  </div>
                                  <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                                    {formatIdr(jl.amount)}
                                  </span>
                                </div>
                              ))}
                            </div>
                          </div>
                        ) : null}
                      </div>
                    ) : null}
                  </div>
                </section>
                ) : null}

                {gate2Success ? (
                  <div
                    id="gate2-success-banner"
                    role="status"
                    className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-md)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-4 py-3 text-xs font-medium text-[var(--status-ok-text)]"
                  >
                    <div className="flex items-center gap-2">
                      <CheckCircle2 className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                      <span>
                        {t.receiving.gate2.successPrefix}{" "}
                        <span className="font-code font-bold tabular-nums">
                          {gate2Success.acceptanceNumber} · {gate2Success.movementNumber} ·{" "}
                          {gate2Success.accrualNumber} · {gate2Success.glJournalNumber}
                        </span>
                        {gate2Success.wasIdempotentReplay ? ` ${t.receiving.gate2.replaySuffix}` : ""}
                      </span>
                    </div>
                    {workspace?.effectivePermissions.includes("inventory.read") ? (
                      <Link
                        href={`/w/${tenantId}/${entityId}/inventory`}
                        className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--accent-primary)] hover:bg-[var(--bg-chrome)]"
                      >
                        {t.receiving.gate2.openInventoryLink}
                      </Link>
                    ) : null}
                  </div>
                ) : null}

                {/* Linked Gate 2 Valued Acceptances */}
                {selectedDetail.valuedAcceptances.length > 0 ? (
                  <section
                    id="gate2-linked-acceptances"
                    className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
                      <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                        {t.receiving.gate2.linkedSectionTitle}
                      </h2>
                      {workspace?.effectivePermissions.includes("inventory.read") ? (
                        <Link
                          href={`/w/${tenantId}/${entityId}/inventory`}
                          className="text-xs font-semibold text-[var(--accent-primary)] hover:underline"
                        >
                          {t.receiving.gate2.openInventoryLink}
                        </Link>
                      ) : null}
                    </div>
                    <div className="md:overflow-x-auto">
                      <table className="w-full border-collapse text-left text-xs">
                        <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                          <tr>
                            <th scope="col" className="py-2 md:pl-3.5 md:pr-2">
                              {t.receiving.gate2.colAcceptanceRef}
                            </th>
                            <th scope="col" className="py-2 md:px-2">
                              {t.receiving.gate2.colLinkedRefs}
                            </th>
                            <th scope="col" className="py-2 md:pl-2 md:pr-3.5 text-right">
                              {t.receiving.gate2.colAcceptedTotal}
                            </th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--border-hairline)]">
                          {selectedDetail.valuedAcceptances.map((va) => (
                            <tr key={va.valuedAcceptanceId} className="block p-3 md:table-row md:p-0">
                              <td className="flex flex-col gap-0.5 py-1 sm:flex-row sm:justify-between md:table-cell md:py-2.5 md:pl-3.5 md:pr-2">
                                <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                  {t.receiving.gate2.colAcceptanceRef}
                                </span>
                                <div className="sm:text-right md:text-left">
                                  <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                                    {va.acceptanceNumber} · {va.acceptanceDate}
                                  </div>
                                  <div className="text-[11px] text-[var(--text-secondary)]">
                                    {t.receiving.stockStatusLabels[va.stockStatus] ?? va.stockStatus}
                                  </div>
                                </div>
                              </td>

                              <td className="flex flex-col gap-0.5 py-1 sm:flex-row sm:justify-between md:table-cell md:py-2.5 md:px-2">
                                <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                  {t.receiving.gate2.colLinkedRefs}
                                </span>
                                <span className="font-code tabular-nums text-[var(--text-primary)]">
                                  {va.movementNumber ?? "—"} · {va.accrualNumber ?? "—"} ·{" "}
                                  {va.glJournalNumber ?? "—"}
                                </span>
                              </td>

                              <td className="flex flex-col gap-0.5 py-1 sm:flex-row sm:justify-between md:table-cell md:py-2.5 md:pl-2 md:pr-3.5 md:text-right">
                                <span className="font-medium text-[var(--text-secondary)] md:hidden">
                                  {t.receiving.gate2.colAcceptedTotal}
                                </span>
                                <div className="font-code tabular-nums">
                                  <div className="font-semibold text-[var(--text-primary)]">
                                    {formatIdr(va.totalAcceptedAmount)}
                                  </div>
                                  <div className="text-[11px] text-[var(--text-secondary)]">
                                    {formatQuantityValue(va.totalAcceptedBaseQuantity, locale)}
                                  </div>
                                </div>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </section>
                ) : null}
            </>
          ) : null}
        </div>
      ) : null}

      {/* TAB 2: RECORD GATE 1 PHYSICAL ARRIVAL COMPOSER */}
      {canManage && activeTab === "arrival" ? (
        <section
          id="rcv-panel-arrival"
          role="tabpanel"
          aria-labelledby="rcv-tab-arrival"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <PlusCircle
              className="h-4 w-4 text-[var(--accent-primary)]"
              strokeWidth={1.75}
              aria-hidden="true"
            />
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">
              {t.receiving.arrivalCardTitle}
            </h2>
          </div>

          {eligiblePos.length === 0 ? (
            <div className="p-6 text-center">
              <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                {t.receiving.arrivalNoEligiblePosTitle}
              </h3>
              <p className="mx-auto mt-1 max-w-lg text-xs text-[var(--text-secondary)]">
                {t.receiving.arrivalNoEligiblePosBody}
              </p>
              <p className="mt-2 text-xs text-[var(--text-muted)]">
                {t.receiving.arrivalOpenPoModuleHint}
              </p>
            </div>
          ) : (
            <form onSubmit={handleSubmitArrival} className="space-y-5 p-4">
              <div className="grid grid-cols-1 gap-3.5 md:grid-cols-2">
                <label className="block space-y-1">
                  <span className="block text-xs font-semibold text-[var(--text-primary)]">
                    {t.receiving.poSelectorLabel}
                  </span>
                  <select
                    id="rcv-po-select"
                    value={selectedPoId}
                    onChange={(e) => handleSelectPo(e.target.value)}
                    className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
                  >
                    <option value="">{t.receiving.poSelectorPlaceholder}</option>
                    {eligiblePos.map((po) => (
                      <option key={po.purchaseOrderId} value={po.purchaseOrderId}>
                        {po.poNumber} · Rev {String(po.approvedRevisionNumber)} — {po.supplierCode}{" "}
                        {po.supplierName}
                      </option>
                    ))}
                  </select>
                </label>

                <label className="block space-y-1">
                  <span className="block text-xs font-semibold text-[var(--text-primary)]">
                    {t.receiving.deliveryNoteLabel}
                  </span>
                  <input
                    id="rcv-delivery-note"
                    type="text"
                    value={deliveryNote}
                    placeholder={t.receiving.deliveryNotePlaceholder}
                    onChange={(e) => setDeliveryNote(e.target.value)}
                    className="font-code min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                  />
                </label>

                <label className="block space-y-1">
                  <span className="block text-xs font-semibold text-[var(--text-primary)]">
                    {t.receiving.receivedDateLabel}
                  </span>
                  <input
                    id="rcv-received-date"
                    type="date"
                    value={receivedDate}
                    onChange={(e) => setReceivedDate(e.target.value)}
                    className="font-code min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)]"
                  />
                </label>

                <label className="block space-y-1">
                  <span className="block text-xs font-semibold text-[var(--text-primary)]">
                    {t.receiving.receivingStationLabel}
                  </span>
                  <input
                    id="rcv-station"
                    type="text"
                    value={receivingStation}
                    placeholder={t.receiving.receivingStationPlaceholder}
                    onChange={(e) => setReceivingStation(e.target.value)}
                    className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                  />
                </label>

                <label className="block space-y-1">
                  <span className="block text-xs font-medium text-[var(--text-secondary)]">
                    {t.receiving.vehicleRefLabel}
                  </span>
                  <input
                    id="rcv-vehicle-ref"
                    type="text"
                    value={vehicleRef}
                    placeholder={t.receiving.vehicleRefPlaceholder}
                    onChange={(e) => setVehicleRef(e.target.value)}
                    className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                  />
                </label>

                <label className="block space-y-1">
                  <span className="block text-xs font-medium text-[var(--text-secondary)]">
                    {t.receiving.documentRefLabel}
                  </span>
                  <input
                    id="rcv-doc-ref"
                    type="text"
                    value={documentRef}
                    placeholder={t.receiving.documentRefPlaceholder}
                    onChange={(e) => setDocumentRef(e.target.value)}
                    className="font-code min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                  />
                </label>
              </div>

              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-secondary)]">
                  {t.receiving.arrivalNotesLabel}
                </span>
                <input
                  id="rcv-notes"
                  type="text"
                  value={arrivalNotes}
                  placeholder={t.receiving.arrivalNotesPlaceholder}
                  onChange={(e) => setArrivalNotes(e.target.value)}
                  className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                />
              </label>

              {/* PO Lines Delivery Grid */}
              <div className="space-y-3">
                <h3 className="text-xs font-semibold text-[var(--text-primary)]">
                  {t.receiving.arrivalLinesHeading}
                </h3>

                {arrivalLines.map((lineDraft, idx) => {
                  const poLine = eligiblePoLineMap.get(lineDraft.poLineId);
                  if (!poLine) return null;

                  const selectedUom =
                    poLine.allowedUoms.find((u) => u.uomId === lineDraft.receivedUomId) ??
                    poLine.allowedUoms[0];
                  const conversion = selectedUom
                    ? convertArrivalQtyToBase({
                        qtyRaw: lineDraft.qtyText,
                        factorToBase: selectedUom.factorToBase,
                        remainingEligibleBase: poLine.remainingEligibleBaseQuantity,
                      })
                    : { validFormat: true, baseQty: null, exceedsRemainingBase: 0n };

                  return (
                    <div
                      key={lineDraft.poLineId}
                      className="space-y-3 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-4"
                    >
                      <div className="flex flex-wrap items-baseline justify-between gap-2">
                        <div>
                          <span className="font-code text-xs font-semibold tabular-nums text-[var(--text-secondary)]">
                            L{String(poLine.lineNumber)} · {poLine.itemCode}
                          </span>{" "}
                          <span className="text-sm font-semibold text-[var(--text-primary)]">
                            {poLine.itemName}
                          </span>
                        </div>
                        <div className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                          PO {formatQuantityValue(poLine.approvedOrderedBaseQuantity, locale)}{" "}
                          {poLine.baseUomCode} · {t.receiving.remainingCapPrefix}{" "}
                          <strong className="text-[var(--text-primary)]">
                            {formatQuantityValue(poLine.remainingEligibleBaseQuantity, locale)}{" "}
                            {poLine.baseUomCode}
                          </strong>
                        </div>
                      </div>

                      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.colDeliveredQty}
                          </span>
                          <input
                            id={`rcv-line-qty-${idx}`}
                            type="text"
                            inputMode="decimal"
                            value={lineDraft.qtyText}
                            placeholder={t.receiving.deliveredQtyPlaceholder}
                            onChange={(e) => {
                              const val = e.target.value;
                              setArrivalLines((prev) =>
                                prev.map((item, i) => (i === idx ? { ...item, qtyText: val } : item)),
                              );
                            }}
                            className="font-code min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.uomSelectLabel}
                          </span>
                          <select
                            id={`rcv-line-uom-${idx}`}
                            value={lineDraft.receivedUomId}
                            onChange={(e) => {
                              const val = e.target.value;
                              setArrivalLines((prev) =>
                                prev.map((item, i) =>
                                  i === idx ? { ...item, receivedUomId: val } : item,
                                ),
                              );
                            }}
                            className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
                          >
                            {poLine.allowedUoms.map((u) => (
                              <option key={u.uomId} value={u.uomId}>
                                {u.uomCode} — {u.name} (×{formatQuantityValue(u.factorToBase, locale)}{" "}
                                {poLine.baseUomCode})
                              </option>
                            ))}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.discrepancyReasonLabel}
                          </span>
                          <select
                            id={`rcv-line-reason-${idx}`}
                            value={lineDraft.discrepancyReasonCode}
                            onChange={(e) => {
                              const val = e.target.value;
                              setArrivalLines((prev) =>
                                prev.map((item, i) =>
                                  i === idx ? { ...item, discrepancyReasonCode: val } : item,
                                ),
                              );
                            }}
                            className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
                          >
                            {DISCREPANCY_REASON_CODES.map((code) => (
                              <option key={code} value={code}>
                                {t.receiving.discrepancyReasonLabels[code] ?? code}
                              </option>
                            ))}
                          </select>
                        </label>
                      </div>

                      {conversion.baseQty !== null && conversion.baseQty > 0n ? (
                        <div className="space-y-1.5 text-xs">
                          <div className="font-code tabular-nums text-[var(--text-secondary)]">
                            {t.receiving.arrivalConvertedBasePrefix}:{" "}
                            <strong className="text-[var(--text-primary)]">
                              {formatScaledBigInt(conversion.baseQty, 6, locale)} {poLine.baseUomCode}
                            </strong>
                          </div>
                          {conversion.exceedsRemainingBase > 0n ? (
                            <div
                              data-testid={`arrival-od01-warning-${idx}`}
                              className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-[var(--status-warn-text)]"
                            >
                              <AlertCircle
                                className="mt-0.5 h-4 w-4 shrink-0"
                                strokeWidth={1.75}
                                aria-hidden="true"
                              />
                              <span>
                                {t.receiving.arrivalOd01ExcessPreview
                                  .replace(
                                    "{excess}",
                                    formatScaledBigInt(conversion.exceedsRemainingBase, 6, locale),
                                  )
                                  .replace("{uom}", poLine.baseUomCode)}
                              </span>
                            </div>
                          ) : null}
                        </div>
                      ) : null}

                      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                        <label className="flex min-h-11 items-center gap-2 self-end text-xs font-medium text-[var(--text-primary)]">
                          <input
                            id={`rcv-line-flag-${idx}`}
                            type="checkbox"
                            checked={lineDraft.flagForInspection}
                            onChange={(e) => {
                              const checked = e.target.checked;
                              setArrivalLines((prev) =>
                                prev.map((item, i) =>
                                  i === idx ? { ...item, flagForInspection: checked } : item,
                                ),
                              );
                            }}
                            className="h-4 w-4 accent-[var(--accent-primary)]"
                          />
                          <span>{t.receiving.flagForInspectionLabel}</span>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.discrepancyNotesLabel}
                          </span>
                          <input
                            id={`rcv-line-notes-${idx}`}
                            type="text"
                            value={lineDraft.discrepancyNotes}
                            placeholder={t.receiving.discrepancyNotesPlaceholder}
                            onChange={(e) => {
                              const val = e.target.value;
                              setArrivalLines((prev) =>
                                prev.map((item, i) =>
                                  i === idx ? { ...item, discrepancyNotes: val } : item,
                                ),
                              );
                            }}
                            className="min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-xs font-medium text-[var(--text-secondary)]">
                            {t.receiving.conditionEvidenceLabel}
                          </span>
                          <input
                            id={`rcv-line-evidence-${idx}`}
                            type="text"
                            value={lineDraft.conditionEvidenceRef}
                            placeholder={t.receiving.conditionEvidencePlaceholder}
                            onChange={(e) => {
                              const val = e.target.value;
                              setArrivalLines((prev) =>
                                prev.map((item, i) =>
                                  i === idx ? { ...item, conditionEvidenceRef: val } : item,
                                ),
                              );
                            }}
                            className="font-code min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs text-[var(--text-primary)] placeholder:text-[var(--text-muted)]"
                          />
                        </label>
                      </div>
                    </div>
                  );
                })}
              </div>

              {arrivalError ? (
                <div
                  id="rcv-arrival-error"
                  role="alert"
                  className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-2.5 text-sm text-[var(--status-danger-text)]"
                >
                  <AlertCircle
                    className="mt-0.5 h-4 w-4 shrink-0"
                    strokeWidth={1.75}
                    aria-hidden="true"
                  />
                  <span>{arrivalError}</span>
                </div>
              ) : null}

              <div className="flex justify-end">
                <button
                  id="rcv-submit-arrival"
                  type="submit"
                  disabled={arrivalSubmitting}
                  className="inline-flex min-h-11 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-5 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                >
                  <PackageCheck className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>
                    {arrivalSubmitting
                      ? t.receiving.submitArrivalBusy
                      : t.receiving.submitArrivalButton}
                  </span>
                </button>
              </div>
            </form>
          )}
        </section>
      ) : null}
    </div>
  );
}

export default function ReceivingPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <ReceivingSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
