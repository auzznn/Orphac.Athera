"use client";

import React, { useMemo, useState } from "react";
import {
  AlertCircle,
  ArrowRightLeft,
  CheckCircle2,
  ClipboardCheck,
  Clock,
  Eye,
  Lock,
  MapPin,
  Package,
} from "lucide-react";
import {
  ApiProblemError,
  createStockReservation,
  ItemStockDetailResponse,
  postInventoryIssue,
  recordInspectionDisposition,
  releaseStockReservation,
  transferInventoryLocation,
  transitionInventoryStockStatus,
  WarehouseLocationDto,
} from "@/lib/api/client";
import { formatIdr, formatJakartaDateTime, formatUnitCostIdr } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatQuantityValue, formatSignedQuantityValue } from "@/lib/receivingMath";

const QUALITY_REASON_CODES = [
  "PASSED_SPECIFICATION",
  "DAMAGED_OR_CONTAMINATED",
  "SPEC_DEVIATION_HOLD",
  "MISSING_CERTIFICATE",
  "SUPERVISOR_REEVALUATION",
] as const;

const RESERVATION_SOURCE_TYPES = ["SALES_ORDER", "MANUAL_HOLD", "FULFILLMENT"] as const;
const STOCK_STATUSES = ["UNRESTRICTED", "QUALITY_INSPECTION", "BLOCKED"] as const;

type OpSuccessState =
  | { kind: "disposition" | "transfer" | "status" | "reserve" | "issue"; ref: string }
  | { kind: "release"; ref: string; status: string };

type OpErrorState =
  | { kind: "invalid_disposition_qty" }
  | { kind: "invalid_positive_qty" }
  | { kind: "api"; errorCode?: string; fallback?: string };

function makeKey(prefix: string): string {
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
    // Fallback to open demo period date.
  }
  return "2026-11-18";
}

function isZoneCompatible(zoneType: string, stockStatus: string): boolean {
  if (stockStatus === "QUALITY_INSPECTION") return zoneType === "QUARANTINE";
  if (stockStatus === "BLOCKED") return zoneType === "BLOCKED_HOLD";
  return zoneType === "STORAGE" || zoneType === "RECEIVING_STAGING" || zoneType === "DISPATCH";
}

export function StockStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "UNRESTRICTED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "QUALITY_INSPECTION") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-warn-text)]">
        <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
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

export function InspectionStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "COMPLETED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
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

interface InventoryStockDetailSheetProps {
  tenantId: string;
  entityId: string;
  detail: ItemStockDetailResponse | null;
  detailLoading: boolean;
  locations: WarehouseLocationDto[];
  canManageInventory: boolean;
  canInspectQuality: boolean;
  onAuthExpired: () => void;
  onOperationCompleted: () => Promise<void>;
}

export function InventoryStockDetailSheet({
  tenantId,
  entityId,
  detail,
  detailLoading,
  locations,
  canManageInventory,
  canInspectQuality,
  onAuthExpired,
  onOperationCompleted,
}: InventoryStockDetailSheetProps) {
  const { locale, t } = useLocale();
  const ds = t.inventory.detailSheet;

  const [subTab, setSubTab] = useState<"operations" | "timeline">("operations");
  const [opError, setOpError] = useState<OpErrorState | null>(null);
  const [opSuccess, setOpSuccess] = useState<OpSuccessState | null>(null);

  // Quality Disposition form state
  const openInspections = useMemo(
    () => (detail?.inspections ?? []).filter((i) => i.status !== "COMPLETED" && Number(i.pendingBaseQuantity) > 0),
    [detail],
  );
  const [dispInspectionId, setDispInspectionId] = useState("");
  const [dispDate, setDispDate] = useState(() => todayJakartaDate());
  const [dispReleaseQty, setDispReleaseQty] = useState("");
  const [dispReleaseLocId, setDispReleaseLocId] = useState("");
  const [dispBlockQty, setDispBlockQty] = useState("");
  const [dispBlockLocId, setDispBlockLocId] = useState("");
  const [dispReasonCode, setDispReasonCode] = useState("PASSED_SPECIFICATION");
  const [dispEvidenceRef, setDispEvidenceRef] = useState("");
  const [dispNotes, setDispNotes] = useState("");
  const [dispBusy, setDispBusy] = useState(false);

  // Intra-warehouse Transfer form state
  const movableBalances = useMemo(
    () => (detail?.locationBalances ?? []).filter((b) => Number(b.onHandBaseQuantity) > 0),
    [detail],
  );
  const [xferBalanceId, setXferBalanceId] = useState("");
  const [xferToLocId, setXferToLocId] = useState("");
  const [xferQty, setXferQty] = useState("");
  const [xferDate, setXferDate] = useState(() => todayJakartaDate());
  const [xferNotes, setXferNotes] = useState("");
  const [xferBusy, setXferBusy] = useState(false);

  // Manual Status Transition form state
  const [stFromStatus, setStFromStatus] = useState("UNRESTRICTED");
  const [stToStatus, setStToStatus] = useState("BLOCKED");
  const [stFromLocId, setStFromLocId] = useState("");
  const [stToLocId, setStToLocId] = useState("");
  const [stQty, setStQty] = useState("");
  const [stReason, setStReason] = useState("SPEC_DEVIATION_HOLD");
  const [stBusy, setStBusy] = useState(false);

  // Reservation Create & Release form state
  const unrestrictedBalances = useMemo(
    () =>
      (detail?.locationBalances ?? []).filter(
        (b) => b.stockStatus === "UNRESTRICTED" && Number(b.onHandBaseQuantity) > 0,
      ),
    [detail],
  );
  const [rsvBalanceId, setRsvBalanceId] = useState("");
  const [rsvSourceType, setRsvSourceType] = useState("SALES_ORDER");
  const [rsvSourceDoc, setRsvSourceDoc] = useState("");
  const [rsvQty, setRsvQty] = useState("");
  const [rsvNotes, setRsvNotes] = useState("");
  const [rsvBusy, setRsvBusy] = useState(false);
  const [issBalanceId, setIssBalanceId] = useState("");
  const [issSourceRef, setIssSourceRef] = useState("");
  const [issQty, setIssQty] = useState("");
  const [issPostingDate, setIssPostingDate] = useState(() => todayJakartaDate());
  const [issNotes, setIssNotes] = useState("");
  const [issBusy, setIssBusy] = useState(false);
  const [releaseInputs, setReleaseInputs] = useState<Record<string, string>>({});
  const [releasingId, setReleasingId] = useState<string | null>(null);

  const activeLocations = useMemo(() => locations.filter((l) => l.status === "ACTIVE"), [locations]);
  const storageLocations = useMemo(
    () => activeLocations.filter((l) => isZoneCompatible(l.zoneType, "UNRESTRICTED")),
    [activeLocations],
  );
  const blockedLocations = useMemo(
    () => activeLocations.filter((l) => isZoneCompatible(l.zoneType, "BLOCKED")),
    [activeLocations],
  );

  const defaultStorageId =
    storageLocations.find((l) => l.locationCode === "LOC-MAIN")?.locationId ?? storageLocations[0]?.locationId ?? "";
  const defaultBlockedId =
    blockedLocations.find((l) => l.locationCode === "LOC-BLK")?.locationId ?? blockedLocations[0]?.locationId ?? "";

  const effectiveDispInspectionId = openInspections.some((i) => i.receiptInspectionId === dispInspectionId)
    ? dispInspectionId
    : (openInspections[0]?.receiptInspectionId ?? "");
  const effectiveDispReleaseLocId = storageLocations.some((l) => l.locationId === dispReleaseLocId)
    ? dispReleaseLocId
    : defaultStorageId;
  const effectiveDispBlockLocId = blockedLocations.some((l) => l.locationId === dispBlockLocId)
    ? dispBlockLocId
    : defaultBlockedId;

  const selectedXferBalance = useMemo(
    () => movableBalances.find((b) => b.stockLocationBalanceId === xferBalanceId) ?? movableBalances[0] ?? null,
    [movableBalances, xferBalanceId],
  );

  const xferTargetOptions = useMemo(() => {
    if (!selectedXferBalance) return [];
    return activeLocations.filter(
      (l) =>
        l.warehouseId === selectedXferBalance.warehouseId &&
        l.locationId !== selectedXferBalance.locationId &&
        isZoneCompatible(l.zoneType, selectedXferBalance.stockStatus),
    );
  }, [activeLocations, selectedXferBalance]);

  const effectiveXferToLocId = xferTargetOptions.some((l) => l.locationId === xferToLocId)
    ? xferToLocId
    : (xferTargetOptions[0]?.locationId ?? "");

  const effectiveStFromLocId = activeLocations.some((l) => l.locationId === stFromLocId)
    ? stFromLocId
    : (movableBalances[0]?.locationId ?? defaultStorageId);
  const effectiveStToLocId = activeLocations.some((l) => l.locationId === stToLocId)
    ? stToLocId
    : defaultBlockedId;

  const chosenRsvBalance =
    unrestrictedBalances.find((b) => b.stockLocationBalanceId === rsvBalanceId) ?? unrestrictedBalances[0] ?? null;
  const effectiveRsvBalanceId = chosenRsvBalance?.stockLocationBalanceId ?? "";

  const issuableBalances = useMemo(
    () => unrestrictedBalances.filter((b) => Number(b.availableBaseQuantity) > 0),
    [unrestrictedBalances],
  );
  const chosenIssBalance =
    issuableBalances.find((b) => b.stockLocationBalanceId === issBalanceId) ?? issuableBalances[0] ?? null;
  const effectiveIssBalanceId = chosenIssBalance?.stockLocationBalanceId ?? "";

  const toErrorState = (err: unknown): OpErrorState | null => {
    const apiErr = err as ApiProblemError;
    if (apiErr?.status === 401) {
      onAuthExpired();
      return null;
    }
    return {
      kind: "api",
      errorCode: apiErr?.errorCode,
      fallback: apiErr?.detail || apiErr?.title,
    };
  };

  const handleSubmitDisposition = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail || !effectiveDispInspectionId) return;
    const rel = Number(dispReleaseQty.trim() || "0");
    const blk = Number(dispBlockQty.trim() || "0");
    if (Number.isNaN(rel) || Number.isNaN(blk) || rel < 0 || blk < 0 || rel + blk <= 0) {
      setOpError({ kind: "invalid_disposition_qty" });
      return;
    }

    setDispBusy(true);
    setOpError(null);
    setOpSuccess(null);
    try {
      const outcome = await recordInspectionDisposition(tenantId, entityId, effectiveDispInspectionId, {
        dispositionDate: dispDate,
        releasedBaseQuantity: rel,
        targetReleaseLocationId: rel > 0 ? effectiveDispReleaseLocId || null : null,
        blockedBaseQuantity: blk,
        targetBlockLocationId: blk > 0 ? effectiveDispBlockLocId || null : null,
        reasonCode: dispReasonCode,
        evidenceReference: dispEvidenceRef.trim() || null,
        notes: dispNotes.trim() || null,
        idempotencyKey: makeKey("disp"),
      });
      setDispReleaseQty("");
      setDispBlockQty("");
      setDispNotes("");
      setOpSuccess({ kind: "disposition", ref: outcome.movementNumber });
      await onOperationCompleted();
    } catch (err) {
      const errState = toErrorState(err);
      if (errState) setOpError(errState);
    } finally {
      setDispBusy(false);
    }
  };

  const handleSubmitTransfer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail || !selectedXferBalance || !effectiveXferToLocId) return;
    const qty = Number(xferQty.trim() || "0");
    if (Number.isNaN(qty) || qty <= 0) {
      setOpError({ kind: "invalid_positive_qty" });
      return;
    }

    setXferBusy(true);
    setOpError(null);
    setOpSuccess(null);
    try {
      const outcome = await transferInventoryLocation(tenantId, entityId, {
        itemId: detail.itemId,
        fromLocationId: selectedXferBalance.locationId,
        toLocationId: effectiveXferToLocId,
        stockLotId: selectedXferBalance.stockLotId ?? null,
        stockStatus: selectedXferBalance.stockStatus,
        quantity: qty,
        postingDate: xferDate,
        idempotencyKey: makeKey("xfer"),
        notes: xferNotes.trim() || null,
      });
      setXferQty("");
      setXferNotes("");
      setOpSuccess({ kind: "transfer", ref: outcome.movementNumber });
      await onOperationCompleted();
    } catch (err) {
      const errState = toErrorState(err);
      if (errState) setOpError(errState);
    } finally {
      setXferBusy(false);
    }
  };

  const handleSubmitStatusTransition = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail || !effectiveStFromLocId) return;
    const qty = Number(stQty.trim() || "0");
    if (Number.isNaN(qty) || qty <= 0) {
      setOpError({ kind: "invalid_positive_qty" });
      return;
    }

    const matchingBal = movableBalances.find(
      (b) => b.locationId === effectiveStFromLocId && b.stockStatus === stFromStatus,
    );

    setStBusy(true);
    setOpError(null);
    setOpSuccess(null);
    try {
      const outcome = await transitionInventoryStockStatus(tenantId, entityId, {
        itemId: detail.itemId,
        fromLocationId: effectiveStFromLocId,
        toLocationId: effectiveStToLocId || null,
        stockLotId: matchingBal?.stockLotId ?? null,
        fromStatus: stFromStatus,
        toStatus: stToStatus,
        quantity: qty,
        postingDate: xferDate,
        reasonCode: stReason,
        evidenceReference: null,
        notes: null,
        idempotencyKey: makeKey("stat"),
      });
      setStQty("");
      setOpSuccess({ kind: "status", ref: outcome.movementNumber });
      await onOperationCompleted();
    } catch (err) {
      const errState = toErrorState(err);
      if (errState) setOpError(errState);
    } finally {
      setStBusy(false);
    }
  };

  const handleSubmitReservation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail || !chosenRsvBalance) return;
    const chosenBal = chosenRsvBalance;
    if (!chosenBal) return;
    const qty = Number(rsvQty.trim() || "0");
    if (Number.isNaN(qty) || qty <= 0) {
      setOpError({ kind: "invalid_positive_qty" });
      return;
    }

    setRsvBusy(true);
    setOpError(null);
    setOpSuccess(null);
    try {
      const outcome = await createStockReservation(tenantId, entityId, {
        itemId: detail.itemId,
        locationId: chosenBal.locationId,
        stockLotId: chosenBal.stockLotId ?? null,
        sourceType: rsvSourceType,
        sourceDocumentId: null,
        sourceDocumentNumber: rsvSourceDoc.trim() || `RSV-DOC-${Date.now().toString().slice(-5)}`,
        sourceLineId: null,
        quantity: qty,
        expiresAtUtc: null,
        idempotencyKey: makeKey("rsv"),
        notes: rsvNotes.trim() || null,
      });
      setRsvQty("");
      setRsvSourceDoc("");
      setRsvNotes("");
      setOpSuccess({ kind: "reserve", ref: outcome.reservation.reservationNumber });
      await onOperationCompleted();
    } catch (err) {
      const errState = toErrorState(err);
      if (errState) setOpError(errState);
    } finally {
      setRsvBusy(false);
    }
  };

  const handleReleaseReservation = async (reservationId: string, full: boolean) => {
    const rawQty = (releaseInputs[reservationId] ?? "").trim();
    const qty = full || !rawQty ? null : Number(rawQty);
    if (qty !== null && (Number.isNaN(qty) || qty <= 0)) {
      setOpError({ kind: "invalid_positive_qty" });
      return;
    }

    setReleasingId(reservationId);
    setOpError(null);
    setOpSuccess(null);
    try {
      const outcome = await releaseStockReservation(tenantId, entityId, reservationId, {
        releaseBaseQuantity: qty,
      });
      setReleaseInputs((prev) => ({ ...prev, [reservationId]: "" }));
      setOpSuccess({
        kind: "release",
        ref: outcome.reservation.reservationNumber,
        status: outcome.reservation.status,
      });
      await onOperationCompleted();
    } catch (err) {
      const errState = toErrorState(err);
      if (errState) setOpError(errState);
    } finally {
      setReleasingId(null);
    }
  };

  const handleSubmitOperationalIssue = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail || !chosenIssBalance) return;
    const qty = Number(issQty.trim() || "0");
    if (Number.isNaN(qty) || qty <= 0) {
      setOpError({ kind: "invalid_positive_qty" });
      return;
    }

    setIssBusy(true);
    setOpError(null);
    setOpSuccess(null);
    try {
      const outcome = await postInventoryIssue(tenantId, entityId, {
        itemId: detail.itemId,
        locationId: chosenIssBalance.locationId,
        stockLotId: chosenIssBalance.stockLotId ?? null,
        quantity: qty,
        postingDate: issPostingDate,
        sourceReference: issSourceRef.trim() || `ISS-OPS-${Date.now().toString().slice(-5)}`,
        idempotencyKey: makeKey("iss"),
        notes: issNotes.trim() || null,
      });
      setIssQty("");
      setIssSourceRef("");
      setIssNotes("");
      setOpSuccess({ kind: "issue", ref: outcome.movementNumber });
      await onOperationCompleted();
    } catch (err) {
      const errState = toErrorState(err);
      if (errState) setOpError(errState);
    } finally {
      setIssBusy(false);
    }
  };

  const opErrorText = !opError
    ? null
    : opError.kind === "invalid_disposition_qty"
      ? ds.invalidDispositionQtyError
      : opError.kind === "invalid_positive_qty"
        ? ds.invalidPositiveQtyError
        : (opError.errorCode && t.inventory.errors[opError.errorCode]) ||
          opError.fallback ||
          t.inventory.loadErrorTitle;

  const opSuccessText = !opSuccess
    ? null
    : opSuccess.kind === "release"
      ? `${opSuccess.ref} — ${t.inventory.reservationStatusLabels[opSuccess.status] ?? opSuccess.status}`
      : opSuccess.kind === "disposition"
        ? `${ds.dispositionSuccessPrefix} ${opSuccess.ref}`
        : opSuccess.kind === "transfer"
          ? `${ds.transferSuccessPrefix} ${opSuccess.ref}`
          : opSuccess.kind === "status"
            ? `${ds.statusSuccessPrefix} ${opSuccess.ref}`
            : opSuccess.kind === "issue"
              ? `${ds.issueSuccessPrefix} ${opSuccess.ref}`
              : `${ds.reserveSuccessPrefix} ${opSuccess.ref}`;

  return (
    <section
      id="inv-stock-detail-sheet"
      className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
    >
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
        <div className="flex items-center gap-2">
          <MapPin className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-sm font-semibold text-[var(--text-primary)]">{ds.sectionTitle}</h2>
        </div>

        {detail ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-code text-xs font-semibold tabular-nums text-[var(--text-secondary)]">
              {detail.itemCode}
            </span>
            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-2 py-0.5 text-xs font-medium text-[var(--text-primary)]">
              <Package className="h-3.5 w-3.5 shrink-0 text-[var(--text-secondary)]" strokeWidth={1.75} aria-hidden="true" />
              <span>
                {ds.lotPolicyPrefix}: {t.inventory.lotPolicyLabels[detail.lotTrackingPolicy] ?? detail.lotTrackingPolicy}
              </span>
            </span>
            {detail.reconciliation.isReconciled ? (
              <span
                data-testid="inv-reconciled-badge"
                className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]"
              >
                <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{ds.reconciledBadge}</span>
              </span>
            ) : (
              <span
                data-testid="inv-reconciled-badge"
                className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-danger-text)]"
              >
                <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{ds.unreconciledBadge}</span>
              </span>
            )}
          </div>
        ) : null}
      </div>

      {detailLoading ? (
        <div className="p-6 text-xs text-[var(--text-secondary)]">{ds.loadingDetail}</div>
      ) : !detail ? (
        <div className="p-6 text-center text-xs text-[var(--text-secondary)]">{ds.selectPrompt}</div>
      ) : (
        <div>
          {/* 6-metric tabular availability strip */}
          <div className="grid grid-cols-2 gap-px border-b border-[var(--border-hairline)] bg-[var(--border-hairline)] sm:grid-cols-3 lg:grid-cols-6">
            {[
              { key: "onhand", label: ds.metricTotalValued, val: detail.reconciliation.positionOnHandTotal, tone: "normal" },
              { key: "unrest", label: ds.metricUnrestricted, val: detail.reconciliation.positionUnrestricted, tone: "normal" },
              {
                key: "qi",
                label: ds.metricQualityInspection,
                val: detail.reconciliation.positionQualityInspection,
                tone: Number(detail.reconciliation.positionQualityInspection) > 0 ? "warn" : "normal",
              },
              {
                key: "blk",
                label: ds.metricBlocked,
                val: detail.reconciliation.positionBlocked,
                tone: Number(detail.reconciliation.positionBlocked) > 0 ? "danger" : "normal",
              },
              { key: "rsv", label: ds.metricReserved, val: detail.reconciliation.positionReserved, tone: "normal" },
              { key: "avail", label: ds.metricAvailable, val: detail.reconciliation.positionAvailable, tone: "normal" },
            ].map((m) => (
              <div key={m.key} data-testid={`inv-metric-${m.key}`} className="bg-[var(--bg-surface)] px-3.5 py-2.5">
                <div className="text-[11px] font-medium text-[var(--text-secondary)]">{m.label}</div>
                <div
                  className={`font-code mt-0.5 text-sm font-bold tabular-nums ${
                    m.tone === "warn"
                      ? "text-[var(--status-warn-text)]"
                      : m.tone === "danger"
                        ? "text-[var(--status-danger-text)]"
                        : "text-[var(--text-primary)]"
                  }`}
                >
                  {formatQuantityValue(m.val, locale)} {detail.baseUomCode}
                </div>
              </div>
            ))}
          </div>

          {/* Detail Sub-tabs */}
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2">
            <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-0.5">
              <button
                type="button"
                id="inv-detail-tab-ops"
                role="tab"
                aria-selected={subTab === "operations"}
                onClick={() => setSubTab("operations")}
                className={`inline-flex min-h-9 items-center gap-1.5 rounded-[3px] px-3 text-xs font-semibold ${
                  subTab === "operations"
                    ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                    : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
                }`}
              >
                <ArrowRightLeft className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{ds.subTabOperations}</span>
              </button>
              <button
                type="button"
                id="inv-detail-tab-timeline"
                role="tab"
                aria-selected={subTab === "timeline"}
                onClick={() => setSubTab("timeline")}
                className={`inline-flex min-h-9 items-center gap-1.5 rounded-[3px] px-3 text-xs font-semibold ${
                  subTab === "timeline"
                    ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                    : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
                }`}
              >
                <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{ds.subTabTimeline}</span>
              </button>
            </div>
          </div>

          {opErrorText ? (
            <div
              id="inv-op-error"
              role="alert"
              className="flex items-start gap-2 border-b border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-2.5 text-xs text-[var(--status-danger-text)]"
            >
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{opErrorText}</span>
            </div>
          ) : null}

          {opSuccessText ? (
            <div
              id="inv-op-success"
              role="status"
              className="flex items-start gap-2 border-b border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-4 py-2.5 text-xs text-[var(--status-ok-text)]"
            >
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{opSuccessText}</span>
            </div>
          ) : null}

          {subTab === "operations" ? (
            <div className="divide-y divide-[var(--border-hairline)]">
              {/* 1. Location & Lot Balances Table */}
              <div>
                <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
                  {ds.balancesTitle}
                </div>
                {detail.locationBalances.length === 0 ? (
                  <div className="px-4 py-4 text-xs text-[var(--text-secondary)]">{ds.emptyBalances}</div>
                ) : (
                  <div className="md:overflow-x-auto">
                    <table className="w-full border-collapse text-left text-xs">
                      <thead className="hidden border-y border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                        <tr>
                          <th scope="col" className="py-2 md:pl-4 md:pr-2">{ds.colLocationZone}</th>
                          <th scope="col" className="py-2 md:px-2">{ds.colLotExpiry}</th>
                          <th scope="col" className="py-2 md:px-2">{ds.colStockStatus}</th>
                          <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{ds.colOnHandReservedAvailable}</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[var(--border-hairline)]">
                        {detail.locationBalances.map((bal) => (
                          <tr key={bal.stockLocationBalanceId} className="block p-3 md:table-row md:p-0">
                            <td className="flex flex-col gap-0.5 py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">{ds.colLocationZone}</span>
                              <div>
                                <span className="font-code font-bold text-[var(--text-primary)]">
                                  {bal.warehouseCode} · {bal.locationCode}
                                </span>
                                <div className="text-[11px] text-[var(--text-secondary)]">
                                  {bal.locationName} ({t.inventory.zoneTypeLabels[bal.zoneType] ?? bal.zoneType})
                                </div>
                              </div>
                            </td>
                            <td className="flex flex-col gap-0.5 py-1 md:table-cell md:py-2.5 md:px-2">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">{ds.colLotExpiry}</span>
                              <div className="font-code tabular-nums text-[var(--text-primary)]">
                                {bal.lotNumber ?? "—"}
                                {bal.lotExpiryDate ? (
                                  <span className="ml-1.5 text-[11px] text-[var(--text-secondary)]">({bal.lotExpiryDate})</span>
                                ) : null}
                              </div>
                            </td>
                            <td className="flex flex-col gap-0.5 py-1 md:table-cell md:py-2.5 md:px-2">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">{ds.colStockStatus}</span>
                              <div>
                                <StockStatusBadge
                                  status={bal.stockStatus}
                                  label={t.inventory.stockStatusLabels[bal.stockStatus] ?? bal.stockStatus}
                                />
                              </div>
                            </td>
                            <td className="flex flex-col gap-0.5 py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                              <span className="font-medium text-[var(--text-secondary)] md:hidden">{ds.colOnHandReservedAvailable}</span>
                              <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                                {formatQuantityValue(bal.onHandBaseQuantity, locale)} ·{" "}
                                {formatQuantityValue(bal.reservedBaseQuantity, locale)} ·{" "}
                                {formatQuantityValue(bal.availableBaseQuantity, locale)} {detail.baseUomCode}
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* 2. Quality Inspection Disposition & Manual Hold Band */}
              <div className="p-4 space-y-4">
                <div className="flex items-center gap-2">
                  <ClipboardCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                  <h3 className="text-xs font-semibold text-[var(--text-primary)]">{ds.dispositionCardTitle}</h3>
                </div>

                {!canInspectQuality ? (
                  <div
                    data-testid="inv-quality-readonly-banner"
                    role="status"
                    className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]"
                  >
                    <Eye className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    <span>{ds.qualityReadOnlyNotice}</span>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {openInspections.length === 0 ? (
                      <p className="text-xs text-[var(--text-secondary)]">{ds.noPendingInspections}</p>
                    ) : (
                      <form onSubmit={handleSubmitDisposition} className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                        <label className="block space-y-1 sm:col-span-2">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.inspectionSelectLabel}</span>
                          <select
                            id="inv-disp-inspection"
                            value={effectiveDispInspectionId}
                            onChange={(e) => setDispInspectionId(e.target.value)}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          >
                            {openInspections.map((insp) => (
                              <option key={insp.receiptInspectionId} value={insp.receiptInspectionId}>
                                {insp.inspectionNumber} · {insp.acceptanceNumber}
                                {insp.lotNumber ? ` (${insp.lotNumber})` : ""} — {t.inventory.inspectionsTab.pendingPrefix}{" "}
                                {formatQuantityValue(insp.pendingBaseQuantity, locale)} {insp.baseUomCode}
                              </option>
                            ))}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.postingDateLabel}</span>
                          <input
                            id="inv-disp-date"
                            type="date"
                            value={dispDate}
                            onChange={(e) => setDispDate(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.releaseQtyLabel}</span>
                          <input
                            id="inv-disp-release-qty"
                            type="text"
                            inputMode="decimal"
                            placeholder="0"
                            value={dispReleaseQty}
                            onChange={(e) => setDispReleaseQty(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.releaseLocationLabel}</span>
                          <select
                            id="inv-disp-release-loc"
                            value={effectiveDispReleaseLocId}
                            onChange={(e) => setDispReleaseLocId(e.target.value)}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          >
                            {storageLocations.map((loc) => (
                              <option key={loc.locationId} value={loc.locationId}>
                                {loc.locationCode} — {loc.name}
                              </option>
                            ))}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.blockQtyLabel}</span>
                          <input
                            id="inv-disp-block-qty"
                            type="text"
                            inputMode="decimal"
                            placeholder="0"
                            value={dispBlockQty}
                            onChange={(e) => setDispBlockQty(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.blockLocationLabel}</span>
                          <select
                            id="inv-disp-block-loc"
                            value={effectiveDispBlockLocId}
                            onChange={(e) => setDispBlockLocId(e.target.value)}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          >
                            {blockedLocations.map((loc) => (
                              <option key={loc.locationId} value={loc.locationId}>
                                {loc.locationCode} — {loc.name}
                              </option>
                            ))}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.reasonCodeLabel}</span>
                          <select
                            id="inv-disp-reason"
                            value={dispReasonCode}
                            onChange={(e) => setDispReasonCode(e.target.value)}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          >
                            {QUALITY_REASON_CODES.map((rc) => (
                              <option key={rc} value={rc}>
                                {t.inventory.qualityReasonLabels[rc] ?? rc}
                              </option>
                            ))}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.evidenceRefLabel}</span>
                          <input
                            id="inv-disp-evidence"
                            type="text"
                            value={dispEvidenceRef}
                            onChange={(e) => setDispEvidenceRef(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          />
                        </label>

                        <div className="flex flex-wrap items-end justify-between gap-2 sm:col-span-3">
                          <label className="block flex-1 space-y-1">
                            <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.notesLabel}</span>
                            <input
                              id="inv-disp-notes"
                              type="text"
                              value={dispNotes}
                              onChange={(e) => setDispNotes(e.target.value)}
                              className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                            />
                          </label>
                          <button
                            type="submit"
                            id="inv-disp-submit"
                            disabled={dispBusy}
                            className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                          >
                            {dispBusy ? ds.submitDispositionBusy : ds.submitDispositionButton}
                          </button>
                        </div>
                      </form>
                    )}

                    {/* Manual Hold / Unblock (Quality) */}
                    <form
                      onSubmit={handleSubmitStatusTransition}
                      className="grid grid-cols-1 gap-2.5 border-t border-[var(--border-hairline)] pt-3 sm:grid-cols-2 lg:grid-cols-3"
                    >
                      <div className="text-xs font-semibold text-[var(--text-primary)] sm:col-span-2 lg:col-span-3">
                        {ds.statusTransitionCardTitle}
                      </div>
                      <label className="block min-w-0 space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.fromStatusLabel}</span>
                        <select
                          id="inv-status-from"
                          value={stFromStatus}
                          onChange={(e) => setStFromStatus(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {STOCK_STATUSES.map((s) => (
                            <option key={s} value={s}>{t.inventory.stockStatusLabels[s] ?? s}</option>
                          ))}
                        </select>
                      </label>
                      <label className="block min-w-0 space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.toStatusLabel}</span>
                        <select
                          id="inv-status-to"
                          value={stToStatus}
                          onChange={(e) => setStToStatus(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {STOCK_STATUSES.map((s) => (
                            <option key={s} value={s}>{t.inventory.stockStatusLabels[s] ?? s}</option>
                          ))}
                        </select>
                      </label>
                      <label className="block min-w-0 space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.fromLocationLabel}</span>
                        <select
                          id="inv-status-from-loc"
                          value={effectiveStFromLocId}
                          onChange={(e) => setStFromLocId(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {activeLocations.map((loc) => (
                            <option key={loc.locationId} value={loc.locationId}>
                              {loc.locationCode} — {loc.name}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="block min-w-0 space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.toLocationLabel}</span>
                        <select
                          id="inv-status-to-loc"
                          value={effectiveStToLocId}
                          onChange={(e) => setStToLocId(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {activeLocations.map((loc) => (
                            <option key={loc.locationId} value={loc.locationId}>
                              {loc.locationCode} — {loc.name}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="block min-w-0 space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.statusQtyLabel}</span>
                        <input
                          id="inv-status-qty"
                          type="text"
                          inputMode="decimal"
                          value={stQty}
                          onChange={(e) => setStQty(e.target.value)}
                          className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)]"
                        />
                      </label>
                      <label className="block min-w-0 space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.reasonCodeLabel}</span>
                        <select
                          id="inv-status-reason"
                          value={stReason}
                          onChange={(e) => setStReason(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {QUALITY_REASON_CODES.map((rc) => (
                            <option key={rc} value={rc}>{t.inventory.qualityReasonLabels[rc] ?? rc}</option>
                          ))}
                        </select>
                      </label>
                      <div className="flex justify-end sm:col-span-2 lg:col-span-3">
                        <button
                          type="submit"
                          id="inv-status-submit"
                          disabled={stBusy}
                          className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                        >
                          {stBusy ? ds.submitStatusBusy : ds.submitStatusButton}
                        </button>
                      </div>
                    </form>
                  </div>
                )}
              </div>

              {/* 3. Warehouse Transfer & Reservations Band */}
              <div className="p-4 space-y-5">
                {!canManageInventory ? (
                  <div
                    role="status"
                    className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]"
                  >
                    <Eye className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    <span>{ds.warehouseManageReadOnlyNotice}</span>
                  </div>
                ) : (
                  <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                    {/* Transfer Form */}
                    <form onSubmit={handleSubmitTransfer} className="space-y-3">
                      <h3 className="text-xs font-semibold text-[var(--text-primary)]">{ds.transferCardTitle}</h3>
                      <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                        <label className="block space-y-1 sm:col-span-2">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.sourceBalanceLabel}</span>
                          <select
                            id="inv-xfer-balance"
                            value={selectedXferBalance?.stockLocationBalanceId ?? ""}
                            disabled={movableBalances.length === 0}
                            onChange={(e) => {
                              const nextId = e.target.value;
                              setXferBalanceId(nextId);
                              const bal = movableBalances.find((b) => b.stockLocationBalanceId === nextId);
                              if (bal) {
                                const compat = locations.find(
                                  (l) =>
                                    l.status === "ACTIVE" &&
                                    l.warehouseId === bal.warehouseId &&
                                    l.locationId !== bal.locationId &&
                                    isZoneCompatible(l.zoneType, bal.stockStatus),
                                );
                                setXferToLocId(compat?.locationId ?? "");
                              }
                            }}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] disabled:bg-[var(--bg-subtle)] disabled:text-[var(--text-secondary)]"
                          >
                            {movableBalances.length === 0 ? (
                              <option value="">{ds.emptyMovableBalancesOption}</option>
                            ) : (
                              movableBalances.map((b) => (
                                <option key={b.stockLocationBalanceId} value={b.stockLocationBalanceId}>
                                  {b.locationCode} · {t.inventory.stockStatusLabels[b.stockStatus] ?? b.stockStatus}
                                  {b.lotNumber ? ` · ${b.lotNumber}` : ""} ({formatQuantityValue(b.onHandBaseQuantity, locale)} {detail.baseUomCode})
                                </option>
                              ))
                            )}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.targetLocationLabel}</span>
                          <select
                            id="inv-xfer-to-loc"
                            value={effectiveXferToLocId}
                            disabled={xferTargetOptions.length === 0}
                            onChange={(e) => setXferToLocId(e.target.value)}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] disabled:bg-[var(--bg-subtle)] disabled:text-[var(--text-secondary)]"
                          >
                            {xferTargetOptions.length === 0 ? (
                              <option value="">{ds.emptyTransferTargetOption}</option>
                            ) : (
                              xferTargetOptions.map((loc) => (
                                <option key={loc.locationId} value={loc.locationId}>
                                  {loc.locationCode} — {loc.name}
                                </option>
                              ))
                            )}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.transferQtyLabel}</span>
                          <input
                            id="inv-xfer-qty"
                            type="text"
                            inputMode="decimal"
                            value={xferQty}
                            onChange={(e) => setXferQty(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.postingDateLabel}</span>
                          <input
                            id="inv-xfer-date"
                            type="date"
                            value={xferDate}
                            onChange={(e) => setXferDate(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <div className="flex items-end">
                          <button
                            type="submit"
                            id="inv-xfer-submit"
                            disabled={xferBusy || !effectiveXferToLocId || movableBalances.length === 0}
                            className="inline-flex min-h-9 w-full items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                          >
                            {xferBusy ? ds.submitTransferBusy : ds.submitTransferButton}
                          </button>
                        </div>
                      </div>
                    </form>

                    {/* Reservation Form */}
                    <form onSubmit={handleSubmitReservation} className="space-y-3">
                      <h3 className="text-xs font-semibold text-[var(--text-primary)]">{ds.reservationCardTitle}</h3>
                      <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                        <label className="block space-y-1 sm:col-span-2">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.resLocationLabel}</span>
                          <select
                            id="inv-rsv-balance"
                            value={effectiveRsvBalanceId}
                            disabled={unrestrictedBalances.length === 0}
                            onChange={(e) => setRsvBalanceId(e.target.value)}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] disabled:bg-[var(--bg-subtle)] disabled:text-[var(--text-secondary)]"
                          >
                            {unrestrictedBalances.length === 0 ? (
                              <option value="">{ds.emptyReserveBalanceOption}</option>
                            ) : (
                              unrestrictedBalances.map((b) => (
                                <option key={b.stockLocationBalanceId} value={b.stockLocationBalanceId}>
                                  {b.locationCode}{b.lotNumber ? ` · ${b.lotNumber}` : ""} — {ds.metricAvailable}:{" "}
                                  {formatQuantityValue(b.availableBaseQuantity, locale)} {detail.baseUomCode}
                                </option>
                              ))
                            )}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.resSourceTypeLabel}</span>
                          <select
                            id="inv-rsv-source-type"
                            value={rsvSourceType}
                            onChange={(e) => setRsvSourceType(e.target.value)}
                            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          >
                            {RESERVATION_SOURCE_TYPES.map((st) => (
                              <option key={st} value={st}>{t.inventory.reservationSourceLabels[st] ?? st}</option>
                            ))}
                          </select>
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.resSourceDocLabel}</span>
                          <input
                            id="inv-rsv-source-doc"
                            type="text"
                            placeholder="SO-2026-0001"
                            value={rsvSourceDoc}
                            onChange={(e) => setRsvSourceDoc(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                          />
                        </label>

                        <label className="block space-y-1">
                          <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{ds.resQtyLabel}</span>
                          <input
                            id="inv-rsv-qty"
                            type="text"
                            inputMode="decimal"
                            value={rsvQty}
                            onChange={(e) => setRsvQty(e.target.value)}
                            className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                          />
                        </label>

                        <div className="flex items-end">
                          <button
                            type="submit"
                            id="inv-rsv-submit"
                            disabled={rsvBusy || unrestrictedBalances.length === 0}
                            className="inline-flex min-h-9 w-full items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                          >
                            {rsvBusy ? ds.submitReserveBusy : ds.submitReserveButton}
                          </button>
                        </div>
                      </div>
                    </form>
                  </div>
                )}

                {canManageInventory ? (
                  <form
                    onSubmit={handleSubmitOperationalIssue}
                    data-testid="inv-operational-issue-form"
                    className="space-y-3 border-t border-[var(--border-hairline)] pt-4"
                  >
                    <div>
                      <h3 className="text-xs font-semibold text-[var(--text-primary)]">{ds.issueCardTitle}</h3>
                      <p className="mt-0.5 text-[11px] text-[var(--text-secondary)]">{ds.issueHint}</p>
                    </div>
                    <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2 lg:grid-cols-4">
                      <label className="block space-y-1 sm:col-span-2">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                          {ds.issueLocationLabel}
                        </span>
                        <select
                          id="inv-iss-balance"
                          value={effectiveIssBalanceId}
                          disabled={issuableBalances.length === 0}
                          onChange={(e) => setIssBalanceId(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] disabled:bg-[var(--bg-subtle)] disabled:text-[var(--text-secondary)]"
                        >
                          {issuableBalances.length === 0 ? (
                            <option value="">{ds.emptyIssueBalanceOption}</option>
                          ) : (
                            issuableBalances.map((b) => (
                              <option key={b.stockLocationBalanceId} value={b.stockLocationBalanceId}>
                                {b.locationCode}
                                {b.lotNumber ? ` · ${b.lotNumber}` : ""} — {ds.metricAvailable}:{" "}
                                {formatQuantityValue(b.availableBaseQuantity, locale)} {detail.baseUomCode}
                              </option>
                            ))
                          )}
                        </select>
                      </label>

                      <label className="block space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                          {ds.postingDateLabel}
                        </span>
                        <input
                          id="inv-iss-date"
                          type="date"
                          value={issPostingDate}
                          onChange={(e) => setIssPostingDate(e.target.value)}
                          className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                        />
                      </label>

                      <label className="block space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                          {ds.issueQtyLabel}
                        </span>
                        <input
                          id="inv-iss-qty"
                          type="text"
                          inputMode="decimal"
                          placeholder="0"
                          value={issQty}
                          onChange={(e) => setIssQty(e.target.value)}
                          className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                        />
                      </label>

                      <label className="block space-y-1">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                          {ds.issueSourceLabel}
                        </span>
                        <input
                          id="inv-iss-source"
                          type="text"
                          placeholder="ISS-OPS-2026-01"
                          value={issSourceRef}
                          onChange={(e) => setIssSourceRef(e.target.value)}
                          className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                        />
                      </label>

                      <label className="block space-y-1 sm:col-span-2">
                        <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                          {ds.issueNotesLabel}
                        </span>
                        <input
                          id="inv-iss-notes"
                          type="text"
                          value={issNotes}
                          onChange={(e) => setIssNotes(e.target.value)}
                          className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                        />
                      </label>

                      <div className="flex items-end">
                        <button
                          type="submit"
                          id="inv-iss-submit"
                          disabled={issBusy || issuableBalances.length === 0}
                          className="inline-flex min-h-9 w-full items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                        >
                          {issBusy ? ds.issueSubmitBusy : ds.issueSubmitButton}
                        </button>
                      </div>
                    </div>
                  </form>
                ) : null}

                {/* Active Reservations & Release History */}
                <div className="border-t border-[var(--border-hairline)] pt-4">
                  <h3 className="mb-2 text-xs font-semibold text-[var(--text-primary)]">{ds.reservationsTableTitle}</h3>
                  {detail.reservations.length === 0 ? (
                    <p className="text-xs text-[var(--text-secondary)]">{ds.emptyReservations}</p>
                  ) : (
                    <div className="md:overflow-x-auto">
                      <table className="w-full border-collapse text-left text-xs">
                        <thead className="hidden border-y border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                          <tr>
                            <th scope="col" className="py-2 md:pl-3 md:pr-2">{ds.colResRef}</th>
                            <th scope="col" className="py-2 md:px-2">{ds.colResSource}</th>
                            <th scope="col" className="py-2 md:px-2 text-right">{ds.colResQty}</th>
                            <th scope="col" className="py-2 md:pl-2 md:pr-3 text-right">{ds.colResStatus}</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--border-hairline)]">
                          {detail.reservations.map((rsv, idx) => {
                            const remaining = Number(rsv.remainingReservedBaseQuantity);
                            return (
                              <tr key={rsv.stockReservationId} className="block p-3 md:table-row md:p-0">
                                <td className="block py-1 md:table-cell md:py-2.5 md:pl-3 md:pr-2">
                                  <div className="font-code font-bold text-[var(--text-primary)]">{rsv.reservationNumber}</div>
                                  <div className="font-code text-[11px] text-[var(--text-secondary)]">
                                    {formatJakartaDateTime(rsv.createdAtUtc, locale)}
                                  </div>
                                </td>
                                <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                                  <div className="text-[11px] font-medium text-[var(--text-secondary)] md:hidden">
                                    {ds.colResSource}
                                  </div>
                                  <div className="text-[var(--text-primary)]">
                                    {t.inventory.reservationSourceLabels[rsv.sourceType] ?? rsv.sourceType} ·{" "}
                                    <span className="font-code font-semibold">{rsv.sourceDocumentNumber}</span>
                                  </div>
                                  <div className="font-code text-[11px] text-[var(--text-secondary)]">
                                    {rsv.locationCode}{rsv.lotNumber ? ` · ${rsv.lotNumber}` : ""}
                                  </div>
                                </td>
                                <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                                  <div className="text-[11px] font-medium text-[var(--text-secondary)] md:hidden">
                                    {ds.colResQty}
                                  </div>
                                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                                    {formatQuantityValue(rsv.reservedBaseQuantity, locale)} ·{" "}
                                    {formatQuantityValue(rsv.releasedBaseQuantity, locale)} ·{" "}
                                    {formatQuantityValue(rsv.remainingReservedBaseQuantity, locale)} {detail.baseUomCode}
                                  </span>
                                </td>
                                <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-3 md:text-right">
                                  <div className="flex flex-wrap items-center gap-1.5 md:justify-end">
                                    <span
                                      className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-medium ${
                                        rsv.status === "ACTIVE"
                                          ? "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                                          : "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                                      }`}
                                    >
                                      {rsv.status === "ACTIVE" ? (
                                        <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                                      ) : (
                                        <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                                      )}
                                      <span>{t.inventory.reservationStatusLabels[rsv.status] ?? rsv.status}</span>
                                    </span>

                                    {canManageInventory && remaining > 0 ? (
                                      <>
                                        <input
                                          id={`inv-rsv-release-qty-${idx}`}
                                          type="text"
                                          inputMode="decimal"
                                          aria-label={ds.releaseQtyPlaceholder}
                                          placeholder={ds.releaseQtyPlaceholder}
                                          value={releaseInputs[rsv.stockReservationId] ?? ""}
                                          onChange={(e) =>
                                            setReleaseInputs((prev) => ({
                                              ...prev,
                                              [rsv.stockReservationId]: e.target.value,
                                            }))
                                          }
                                          className="font-code h-9 w-44 max-w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)]"
                                        />
                                        <button
                                          type="button"
                                          id={`inv-rsv-release-${idx}`}
                                          disabled={releasingId === rsv.stockReservationId}
                                          onClick={() => void handleReleaseReservation(rsv.stockReservationId, false)}
                                          className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                                        >
                                          {releasingId === rsv.stockReservationId
                                            ? ds.releaseBusy
                                            : (releaseInputs[rsv.stockReservationId] ?? "").trim()
                                              ? ds.releaseButton
                                              : ds.releaseAllButton}
                                        </button>
                                      </>
                                    ) : null}
                                  </div>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            </div>
          ) : (
            /* Sub-Tab 2: Movement & Valuation Trace Timeline */
            <div>
              <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
                {ds.timelineTitle}
              </div>
              {detail.movementTimeline.length === 0 ? (
                <div className="p-6 text-center text-xs text-[var(--text-secondary)]">{ds.emptyTimeline}</div>
              ) : (
                <div className="md:overflow-x-auto">
                  <table className="w-full border-collapse text-left text-xs">
                    <thead className="hidden border-y border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                      <tr>
                        <th scope="col" className="py-2 md:pl-4 md:pr-2">{ds.colTimelineRef}</th>
                        <th scope="col" className="py-2 md:px-2">{ds.colTimelineLocationLot}</th>
                        <th scope="col" className="py-2 md:px-2 text-right">{ds.colTimelineDelta}</th>
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{ds.colTimelineCost}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--border-hairline)]">
                      {detail.movementTimeline.map((mv) => (
                          <tr key={mv.movementLineId} className="block p-3 md:table-row md:p-0">
                            <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                              <div className="font-code font-bold text-[var(--text-primary)]">
                                {mv.movementNumber} · {mv.postingDate}
                              </div>
                              <div className="text-[11px] text-[var(--text-secondary)]">
                                {t.inventory.movementTypeLabels[mv.movementType] ?? mv.movementType} ·{" "}
                                <span className="font-code">{mv.sourceDocumentNumber}</span>
                              </div>
                            </td>
                            <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                              <div className="text-[11px] font-medium text-[var(--text-secondary)] md:hidden">
                                {ds.colTimelineLocationLot}
                              </div>
                              <div className="font-code text-[var(--text-primary)]">
                                {mv.locationCode ?? "—"}
                                {mv.lotNumber ? ` · ${mv.lotNumber}` : ""}
                              </div>
                              <div className="text-[11px] text-[var(--text-secondary)]">
                                {mv.zoneType ? (t.inventory.zoneTypeLabels[mv.zoneType] ?? mv.zoneType) : "—"}
                              </div>
                            </td>
                            <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                              <div className="text-[11px] font-medium text-[var(--text-secondary)] md:hidden">
                                {ds.colTimelineDelta}
                              </div>
                              <div className="flex flex-wrap items-center gap-2 md:flex-col md:items-end md:gap-1">
                                <StockStatusBadge
                                  status={mv.resultingStockStatus}
                                  label={t.inventory.stockStatusLabels[mv.resultingStockStatus] ?? mv.resultingStockStatus}
                                />
                                <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                                  {formatSignedQuantityValue(mv.baseQuantityDelta, locale)} {detail.baseUomCode}
                                </span>
                              </div>
                            </td>
                            <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                              <div className="text-[11px] font-medium text-[var(--text-secondary)] md:hidden">
                                {ds.colTimelineCost}
                              </div>
                              <div className="font-code tabular-nums">
                                <div className="font-semibold text-[var(--text-primary)]">
                                  {formatUnitCostIdr(mv.unitValuationCost)} / {detail.baseUomCode}
                                </div>
                                <div className="text-[11px] text-[var(--text-secondary)]">
                                  {formatIdr(mv.lineValuationAmount)}
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
          )}
        </div>
      )}
    </section>
  );
}
