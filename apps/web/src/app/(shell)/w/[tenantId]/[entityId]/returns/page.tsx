"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Eye,
  Plus,
  RefreshCw,
  RotateCcw,
  ShieldCheck,
  X,
} from "lucide-react";
import {
  ApiProblemError,
  createSupplierReturnDraft,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchReturnsOverview,
  fetchSupplierReturnDetail,
  previewSupplierReturn,
  QueuedUnvaluedRejectionDto,
  QueuedValuedBilledLineDto,
  QueuedValuedUnbilledLineDto,
  receiveLinkedReplacement,
  ReplacementAuthorizationDto,
  ReturnsOverviewResponse,
  ReturnStockBalanceOptionDto,
  SupplierReturnDetailResponse,
  SupplierReturnPreviewResponse,
  updateSupplierReturnDraft,
} from "@/lib/api/client";
import { useSessionUser } from "@/components/AppShell";
import {
  DispositionBadge,
  ReturnStatusBadge,
  RpaStatusBadge,
  SupplierReturnDetailSheet,
} from "@/components/returns/SupplierReturnDetailSheet";
import { formatIdr, formatJakartaDateTime, formatUnitCostIdr } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatQuantityValue } from "@/lib/receivingMath";

type ReturnsTab = "queued" | "returns" | "replacements" | "credits";

interface ComposerSourceContext {
  returnCaseType: "UNVALUED_DOCK_REJECTION" | "VALUED_UNBILLED_RETURN" | "VALUED_BILLED_RETURN";
  sourceLabel: string;
  purchaseOrderId: string;
  goodsReceiptId: string | null;
  receiptAccrualId: string | null;
  supplierBillId: string | null;
  goodsReceiptLineId: string;
  valuedAcceptanceLineId: string | null;
  receiptAccrualLineId: string | null;
  supplierBillLineId: string | null;
  itemCode: string;
  itemName: string;
  baseUomCode: string;
  maxQty: number;
  stockLocations: ReturnStockBalanceOptionDto[];
}

function makeLocationKey(opt: ReturnStockBalanceOptionDto): string {
  return `${opt.locationId}|${opt.stockLotId ?? ""}|${opt.stockStatus}`;
}

function makeRpaIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `rpa-rcv-${crypto.randomUUID()}`;
  }
  return `rpa-rcv-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

export default function ReturnsPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <ReturnsSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}

function ReturnsSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const user = useSessionUser();
  const { locale, t } = useLocale();
  const rt = t.returns;

  const [workspace, setWorkspace] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [overview, setOverview] = useState<ReturnsOverviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const composerRef = useRef<HTMLElement | null>(null);
  const rpaModalRef = useRef<HTMLElement | null>(null);

  const [activeTab, setActiveTab] = useState<ReturnsTab>("queued");
  const [matrixOpen, setMatrixOpen] = useState(false);
  const [selectedDetail, setSelectedDetail] = useState<SupplierReturnDetailResponse | null>(null);

  // Composer state
  const [composerSource, setComposerSource] = useState<ComposerSourceContext | null>(null);
  const [editingReturnId, setEditingReturnId] = useState<string | null>(null);
  const [editingDraftCode, setEditingDraftCode] = useState<string | null>(null);
  const [resolutionMode, setResolutionMode] = useState("CLOSE_WITHOUT_REPLACEMENT");
  const [returnDate, setReturnDate] = useState("2026-11-10");
  const [reasonCode, setReasonCode] = useState("DAMAGED_OR_DEFECTIVE");
  const [carrierRef, setCarrierRef] = useState("");
  const [taxReturnNoteNumber, setTaxReturnNoteNumber] = useState("");
  const [notes, setNotes] = useState("");
  const [lineQty, setLineQty] = useState("");
  const [selectedLocationKey, setSelectedLocationKey] = useState("");
  const [lineNotes, setLineNotes] = useState("");
  const [previewResult, setPreviewResult] = useState<SupplierReturnPreviewResponse | null>(null);
  const [previewBusy, setPreviewBusy] = useState(false);
  const [saveBusy, setSaveBusy] = useState(false);
  const [composerError, setComposerError] = useState<string | null>(null);

  // Receive Replacement modal state
  const [activeRpa, setActiveRpa] = useState<ReplacementAuthorizationDto | null>(null);
  const [rpaQty, setRpaQty] = useState("");
  const [rpaDate, setRpaDate] = useState("2026-11-11");
  const [rpaDeliveryNote, setRpaDeliveryNote] = useState("");
  const [rpaStation, setRpaStation] = useState("Dermaga A — Pengganti");
  const [rpaVehicleRef, setRpaVehicleRef] = useState("");
  const [rpaNotes, setRpaNotes] = useState("");
  const [rpaFlagInspect, setRpaFlagInspect] = useState(false);
  const [rpaBusy, setRpaBusy] = useState(false);
  const [rpaError, setRpaError] = useState<string | null>(null);
  const [replacementReceiptNotice, setReplacementReceiptNotice] = useState<{
    receiptNumber: string;
    rpaNumber: string;
  } | null>(null);

  const loadSnapshot = useCallback(async () => {
    const [ws, ov] = await Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchReturnsOverview(tenantId, entityId),
    ]);
    return { ws, ov };
  }, [tenantId, entityId]);

  const reload = useCallback(async () => {
    setRefreshing(true);
    try {
      const { ws, ov } = await loadSnapshot();
      setWorkspace(ws);
      setOverview(ov);
    } catch {
      // Preserve current screen state if background reload fails
    } finally {
      setRefreshing(false);
    }
  }, [loadSnapshot]);

  useEffect(() => {
    if (!tenantId || !entityId) return;
    let active = true;

    loadSnapshot()
      .then(({ ws, ov }) => {
        if (!active) return;
        setWorkspace(ws);
        setOverview(ov);
        setLoadError(null);
        setAccessDenied(false);
      })
      .catch((err) => {
        if (!active) return;
        const problem = err as Partial<ApiProblemError>;
        if (problem.status === 401) {
          router.replace("/login");
          return;
        }
        if (problem.status === 403) {
          setAccessDenied(true);
          return;
        }
        setLoadError(problem.detail ?? t.login.errors.upstream_unavailable);
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, loadSnapshot, router, t.login.errors.upstream_unavailable]);

  const permissionSet = useMemo(
    () => new Set(workspace?.effectivePermissions ?? []),
    [workspace],
  );

  const canAuthorM1 = permissionSet.has("receiving.manage") || permissionSet.has("procurement.manage");
  const canAuthorValued = permissionSet.has("inventory.manage") || permissionSet.has("ap.manage");
  const canApproveM1M2 = permissionSet.has("approval.decide") && permissionSet.has("procurement.approve");
  const canApproveM3 = permissionSet.has("approval.decide") && permissionSet.has("ap.approve");
  const canExecuteM1 = permissionSet.has("receiving.manage");
  const canExecuteM2 = permissionSet.has("inventory.manage") || permissionSet.has("receiving.accept");
  const canExecuteM3 = permissionSet.has("ap.post");

  const isReadOnly =
    !canAuthorM1 &&
    !canAuthorValued &&
    !canApproveM1M2 &&
    !canApproveM3 &&
    !canExecuteM1 &&
    !canExecuteM2 &&
    !canExecuteM3;

  const canAuthorCase = useCallback(
    (caseType: string) => (caseType === "UNVALUED_DOCK_REJECTION" ? canAuthorM1 : canAuthorValued),
    [canAuthorM1, canAuthorValued],
  );

  const canApproveCase = useCallback(
    (caseType: string) => (caseType === "VALUED_BILLED_RETURN" ? canApproveM3 : canApproveM1M2),
    [canApproveM1M2, canApproveM3],
  );

  const canExecuteCase = useCallback(
    (caseType: string) => {
      if (caseType === "UNVALUED_DOCK_REJECTION") return canExecuteM1;
      if (caseType === "VALUED_UNBILLED_RETURN") return canExecuteM2;
      if (caseType === "VALUED_BILLED_RETURN") return canExecuteM3;
      return false;
    },
    [canExecuteM1, canExecuteM2, canExecuteM3],
  );

  function formatApiError(err: unknown): string {
    const problem = err as Partial<ApiProblemError>;
    if (problem.errorCode && rt.errors[problem.errorCode]) {
      return rt.errors[problem.errorCode];
    }
    return problem.detail ?? t.login.errors.unknown;
  }

  function resetComposer(source: ComposerSourceContext | null, defaultResolution: string) {
    setComposerSource(source);
    setEditingReturnId(null);
    setEditingDraftCode(null);
    setResolutionMode(defaultResolution);
    setReturnDate("2026-11-10");
    setReasonCode("DAMAGED_OR_DEFECTIVE");
    setCarrierRef("");
    setTaxReturnNoteNumber("");
    setNotes("");
    setLineQty(source ? String(source.maxQty) : "1");
    setSelectedLocationKey(
      source && source.stockLocations.length > 0 ? makeLocationKey(source.stockLocations[0]) : "",
    );
    setLineNotes("");
    setPreviewResult(null);
    setComposerError(null);
  }

  function openComposerFromM1(row: QueuedUnvaluedRejectionDto) {
    resetComposer(
      {
        returnCaseType: "UNVALUED_DOCK_REJECTION",
        sourceLabel: `${row.receiptNumber} · ${row.poNumber}`,
        purchaseOrderId: row.purchaseOrderId,
        goodsReceiptId: row.goodsReceiptId,
        receiptAccrualId: null,
        supplierBillId: null,
        goodsReceiptLineId: row.goodsReceiptLineId,
        valuedAcceptanceLineId: null,
        receiptAccrualLineId: null,
        supplierBillLineId: null,
        itemCode: row.itemCode,
        itemName: row.itemName,
        baseUomCode: row.baseUomCode,
        maxQty: Number(row.eligibleQuantity),
        stockLocations: [],
      },
      "REPLACEMENT_ONLY",
    );
  }

  function openComposerFromM2(row: QueuedValuedUnbilledLineDto) {
    resetComposer(
      {
        returnCaseType: "VALUED_UNBILLED_RETURN",
        sourceLabel: `${row.accrualNumber} · ${row.receiptNumber}`,
        purchaseOrderId: row.purchaseOrderId,
        goodsReceiptId: row.goodsReceiptId,
        receiptAccrualId: row.receiptAccrualId,
        supplierBillId: null,
        goodsReceiptLineId: row.goodsReceiptLineId,
        valuedAcceptanceLineId: row.valuedAcceptanceLineId,
        receiptAccrualLineId: row.receiptAccrualLineId,
        supplierBillLineId: null,
        itemCode: row.itemCode,
        itemName: row.itemName,
        baseUomCode: row.baseUomCode,
        maxQty: Number(row.eligibleQuantity),
        stockLocations: row.stockLocations ?? [],
      },
      "CLOSE_WITHOUT_REPLACEMENT",
    );
  }

  function openComposerFromM3(row: QueuedValuedBilledLineDto) {
    resetComposer(
      {
        returnCaseType: "VALUED_BILLED_RETURN",
        sourceLabel: `${row.billNumber} · ${row.supplierInvoiceNumber}`,
        purchaseOrderId: row.purchaseOrderId,
        goodsReceiptId: row.goodsReceiptId,
        receiptAccrualId: null,
        supplierBillId: row.supplierBillId,
        goodsReceiptLineId: row.goodsReceiptLineId,
        valuedAcceptanceLineId: null,
        receiptAccrualLineId: null,
        supplierBillLineId: row.supplierBillLineId,
        itemCode: row.itemCode,
        itemName: row.itemName,
        baseUomCode: row.baseUomCode,
        maxQty: Number(row.remainingCreditableQuantity),
        stockLocations: row.stockLocations ?? [],
      },
      "AP_CREDIT_NOTE",
    );
  }

  function handleOpenEditDraft(detail: SupplierReturnDetailResponse) {
    const hdr = detail.returnHeader;
    const firstLine = detail.lines[0];
    if (!firstLine) return;

    const queueStockOptions =
      overview?.queuedValuedUnbilledLines.find((q) => q.receiptAccrualLineId === firstLine.receiptAccrualLineId)
        ?.stockLocations ??
      overview?.queuedValuedBilledLines.find((q) => q.supplierBillLineId === firstLine.supplierBillLineId)
        ?.stockLocations ??
      [];

    const source: ComposerSourceContext = {
      returnCaseType: hdr.returnCaseType as ComposerSourceContext["returnCaseType"],
      sourceLabel: hdr.billNumberSnapshot ?? hdr.receiptNumberSnapshot ?? hdr.poNumberSnapshot,
      purchaseOrderId: hdr.purchaseOrderId,
      goodsReceiptId: hdr.goodsReceiptId,
      receiptAccrualId: hdr.receiptAccrualId,
      supplierBillId: hdr.supplierBillId,
      goodsReceiptLineId: firstLine.goodsReceiptLineId,
      valuedAcceptanceLineId: firstLine.valuedAcceptanceLineId,
      receiptAccrualLineId: firstLine.receiptAccrualLineId,
      supplierBillLineId: firstLine.supplierBillLineId,
      itemCode: firstLine.itemCode,
      itemName: firstLine.itemName,
      baseUomCode: firstLine.baseUomCode,
      maxQty: Number(firstLine.returnedBaseQuantity),
      stockLocations: queueStockOptions,
    };

    setComposerSource(source);
    setEditingReturnId(hdr.supplierReturnId);
    setEditingDraftCode(hdr.draftCode);
    setResolutionMode(hdr.resolutionMode);
    setReturnDate(hdr.returnDate);
    setReasonCode(hdr.reasonCode);
    setCarrierRef("");
    setTaxReturnNoteNumber(hdr.taxReturnNoteNumber ?? "");
    setNotes("");
    setLineQty(String(firstLine.returnedBaseQuantity));
    setSelectedLocationKey(
      firstLine.sourceLocationId
        ? `${firstLine.sourceLocationId}|${firstLine.sourceStockLotId ?? ""}|${firstLine.sourceStockStatus ?? "UNRESTRICTED"}`
        : queueStockOptions[0]
          ? makeLocationKey(queueStockOptions[0])
          : "",
    );
    setLineNotes(firstLine.lineNotes ?? "");
    setPreviewResult(detail.liveEvaluation);
    setComposerError(null);
  }

  function buildLineInput() {
    if (!composerSource) return null;
    const qty = Number(lineQty);
    const chosenLoc = composerSource.stockLocations.find(
      (opt) => makeLocationKey(opt) === selectedLocationKey,
    );
    return {
      goodsReceiptLineId: composerSource.goodsReceiptLineId,
      valuedAcceptanceLineId: composerSource.valuedAcceptanceLineId,
      receiptAccrualLineId: composerSource.receiptAccrualLineId,
      supplierBillLineId: composerSource.supplierBillLineId,
      returnedBaseQuantity: Number.isFinite(qty) && qty > 0 ? qty : 0,
      sourceLocationId: chosenLoc?.locationId ?? null,
      sourceStockLotId: chosenLoc?.stockLotId ?? null,
      sourceStockStatus: chosenLoc?.stockStatus ?? null,
      lineNotes: lineNotes.trim() || null,
    };
  }

  async function handleRunPreview() {
    if (!composerSource) return;
    const lineInput = buildLineInput();
    if (!lineInput || lineInput.returnedBaseQuantity <= 0) {
      setComposerError(rt.errors.return_quantity_exceeds_eligible);
      return;
    }

    setPreviewBusy(true);
    setComposerError(null);
    try {
      const result = await previewSupplierReturn(tenantId, entityId, {
        returnCaseType: composerSource.returnCaseType,
        resolutionMode,
        purchaseOrderId: composerSource.purchaseOrderId,
        goodsReceiptId: composerSource.goodsReceiptId,
        receiptAccrualId: composerSource.receiptAccrualId,
        supplierBillId: composerSource.supplierBillId,
        returnDate,
        taxReturnNoteNumber: taxReturnNoteNumber.trim() || null,
        lines: [lineInput],
        excludeSupplierReturnId: editingReturnId,
      });
      setPreviewResult(result);
    } catch (err) {
      setComposerError(formatApiError(err));
    } finally {
      setPreviewBusy(false);
    }
  }

  async function handleSaveDraft(e: React.FormEvent) {
    e.preventDefault();
    if (!composerSource) return;
    const lineInput = buildLineInput();
    if (!lineInput || lineInput.returnedBaseQuantity <= 0) {
      setComposerError(rt.errors.return_quantity_exceeds_eligible);
      return;
    }

    setSaveBusy(true);
    setComposerError(null);
    try {
      let detail: SupplierReturnDetailResponse;
      if (editingReturnId) {
        detail = await updateSupplierReturnDraft(tenantId, entityId, editingReturnId, {
          resolutionMode,
          returnDate,
          reasonCode,
          carrierOrDispatchRef: carrierRef.trim() || null,
          taxReturnNoteNumber: taxReturnNoteNumber.trim() || null,
          notes: notes.trim() || null,
          lines: [lineInput],
        });
      } else {
        detail = await createSupplierReturnDraft(tenantId, entityId, {
          returnCaseType: composerSource.returnCaseType,
          resolutionMode,
          purchaseOrderId: composerSource.purchaseOrderId,
          goodsReceiptId: composerSource.goodsReceiptId,
          receiptAccrualId: composerSource.receiptAccrualId,
          supplierBillId: composerSource.supplierBillId,
          returnDate,
          reasonCode,
          carrierOrDispatchRef: carrierRef.trim() || null,
          taxReturnNoteNumber: taxReturnNoteNumber.trim() || null,
          notes: notes.trim() || null,
          lines: [lineInput],
        });
      }
      setComposerSource(null);
      setEditingReturnId(null);
      setEditingDraftCode(null);
      setSelectedDetail(detail);
      setActiveTab("returns");
      await reload();
    } catch (err) {
      setComposerError(formatApiError(err));
    } finally {
      setSaveBusy(false);
    }
  }

  async function handleOpenDetail(returnId: string) {
    try {
      const detail = await fetchSupplierReturnDetail(tenantId, entityId, returnId);
      setSelectedDetail(detail);
    } catch (err) {
      setLoadError(formatApiError(err));
    }
  }

  async function handleDetailUpdated(updated: SupplierReturnDetailResponse) {
    setSelectedDetail(updated);
    await reload();
  }

  function openReceiveReplacement(authorizationId: string) {
    const rpa = overview?.replacementAuthorizations.find(
      (a) => a.replacementAuthorizationId === authorizationId,
    );
    if (!rpa) return;
    setActiveRpa(rpa);
    setRpaQty(String(rpa.remainingBaseQuantity));
    setRpaDate("2026-11-11");
    setRpaDeliveryNote(`SJ-${rpa.authorizationNumber}`);
    setRpaStation("Dermaga A — Pengganti");
    setRpaVehicleRef("");
    setRpaNotes("");
    setRpaFlagInspect(false);
    setRpaError(null);
  }

  async function handleSubmitReplacementArrival(e: React.FormEvent) {
    e.preventDefault();
    if (!activeRpa) return;
    const qty = Number(rpaQty);
    if (!Number.isFinite(qty) || qty <= 0) {
      setRpaError(rt.errors.replacement_entitlement_exceeded);
      return;
    }

    setRpaBusy(true);
    setRpaError(null);
    try {
      const grn = await receiveLinkedReplacement(
        tenantId,
        entityId,
        activeRpa.replacementAuthorizationId,
        {
          receivedUomId: activeRpa.baseUomId,
          physicallyDeliveredQuantity: qty,
          receivedDate: rpaDate,
          supplierDeliveryNoteNumber: rpaDeliveryNote.trim(),
          idempotencyKey: makeRpaIdempotencyKey(),
          receivingStation: rpaStation.trim() || "Dermaga A",
          vehicleOrSealRef: rpaVehicleRef.trim() || null,
          documentEvidenceRef: null,
          notes: rpaNotes.trim() || null,
          flagForInspection: rpaFlagInspect,
        },
      );
      setReplacementReceiptNotice({
        receiptNumber: grn.receiptNumber,
        rpaNumber: grn.replacementAuthorizationNumber ?? activeRpa.authorizationNumber,
      });
      setActiveRpa(null);
      await reload();
    } catch (err) {
      setRpaError(formatApiError(err));
    } finally {
      setRpaBusy(false);
    }
  }

  const handleScrollEffectComposer = Boolean(composerSource);
  useEffect(() => {
    if (handleScrollEffectComposer) {
      composerRef.current?.scrollIntoView?.({ behavior: "smooth", block: "nearest" });
    }
  }, [handleScrollEffectComposer]);

  const handleScrollEffectRpa = Boolean(activeRpa);
  useEffect(() => {
    if (handleScrollEffectRpa) {
      rpaModalRef.current?.scrollIntoView?.({ behavior: "smooth", block: "nearest" });
    }
  }, [handleScrollEffectRpa]);

  if (loading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-64 animate-pulse rounded-[var(--radius-sm)] bg-[var(--bg-chrome)]" />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {[0, 1, 2, 3].map((idx) => (
            <div
              key={idx}
              className="h-20 animate-pulse rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
            />
          ))}
        </div>
      </div>
    );
  }

  if (accessDenied) {
    return (
      <div
        role="alert"
        className="rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
      >
        <div className="flex items-start gap-3 text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div>
            <h1 className="text-base font-semibold text-[var(--text-primary)]">{rt.loadErrorTitle}</h1>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {rt.errors.insufficient_entity_permission}
            </p>
            <Link
              href={`/w/${tenantId}/${entityId}`}
              className="mt-4 inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>{rt.backToOverview}</span>
            </Link>
          </div>
        </div>
      </div>
    );
  }

  if (loadError || !overview) {
    return (
      <div
        role="alert"
        className="rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
      >
        <div className="flex items-start gap-3 text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div>
            <h1 className="text-base font-semibold text-[var(--text-primary)]">{rt.loadErrorTitle}</h1>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {loadError ?? t.login.errors.upstream_unavailable}
            </p>
            <button
              type="button"
              onClick={() => window.location.reload()}
              className="mt-4 inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <RefreshCw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>{rt.retryButton}</span>
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Direct Page Header */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 text-xs font-medium text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{rt.backToOverview}</span>
          </Link>
          <h1 className="mt-1 text-xl font-semibold tracking-tight text-[var(--text-primary)]">
            {rt.pageTitle}
          </h1>
          <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">{rt.lead}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            id="ret-matrix-toggle"
            aria-expanded={matrixOpen}
            aria-controls="ret-matrix-panel"
            onClick={() => setMatrixOpen((prev) => !prev)}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            {matrixOpen ? (
              <ChevronUp className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            ) : (
              <ChevronDown className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            )}
            <span>{matrixOpen ? rt.matrixToggleHide : rt.matrixToggleShow}</span>
          </button>
          <button
            type="button"
            onClick={reload}
            disabled={refreshing}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw
              className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`}
              strokeWidth={1.75}
              aria-hidden="true"
            />
            <span>{rt.refreshButton}</span>
          </button>
        </div>
      </div>

      {/* OD-05 & OD-07 Policy Governance Strip */}
      <div
        data-testid="ret-governance-banner"
        className="rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
      >
        <div className="flex items-start gap-2">
          <RotateCcw className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div className="space-y-1">
            <p>{rt.od05PolicyBanner}</p>
            <p>{rt.od07PolicyBanner}</p>
          </div>
        </div>
      </div>

      {/* Read-Only Mode Callout (e.g. NUSA-SBY) */}
      {isReadOnly ? (
        <div
          data-testid="returns-readonly-banner"
          role="status"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3.5 py-2.5 text-xs text-[var(--text-secondary)]"
        >
          <Eye className="mt-0.5 h-4 w-4 shrink-0 text-[var(--text-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <span>{rt.readOnlyBanner}</span>
        </div>
      ) : null}

      {/* Linked Replacement GRN Confirmation Notice */}
      {replacementReceiptNotice ? (
        <div
          data-testid="ret-replacement-success"
          role="status"
          className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-3.5 py-2.5 text-xs text-[var(--status-ok-text)]"
        >
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span className="font-code font-semibold tabular-nums">
              {replacementReceiptNotice.receiptNumber} · {replacementReceiptNotice.rpaNumber}
            </span>
          </div>
          <Link
            href={`/w/${tenantId}/${entityId}/receiving`}
            className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--status-ok-text)] hover:bg-[var(--bg-chrome)]"
          >
            {t.shell.navItems.receiving} →
          </Link>
        </div>
      ) : null}

      {/* Collapsible M1–M7 Policy Matrix */}
      {matrixOpen ? (
        <section
          id="ret-matrix-panel"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <ShieldCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">{rt.matrixTitle}</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-xs">
              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="py-2 pl-4 pr-2 font-semibold">{rt.colMatrixCode}</th>
                  <th scope="col" className="py-2 px-2 font-semibold">{rt.colMatrixCase}</th>
                  <th scope="col" className="py-2 px-2 font-semibold">{rt.colMatrixResolution}</th>
                  <th scope="col" className="py-2 px-2 font-semibold">{rt.colMatrixPrecondition}</th>
                  <th scope="col" className="py-2 px-2 font-semibold">{rt.colMatrixEffect}</th>
                  <th scope="col" className="py-2 pl-2 pr-4 font-semibold">{rt.colMatrixDisposition}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border-hairline)]">
                {overview.matrixRules.map((rule) => {
                  const localizedRule = rt.matrixRulesByCode[rule.matrixCode];
                  return (
                    <tr key={rule.matrixCode} className="block p-3 md:table-row md:p-0">
                      <td className="block py-1 md:table-cell md:py-2 md:pl-4 md:pr-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                          {rt.colMatrixCode}
                        </span>
                        <span className="font-code font-bold text-[var(--text-primary)]">{rule.matrixCode}</span>
                      </td>
                      <td className="block py-1 text-[11px] text-[var(--text-primary)] md:table-cell md:py-2 md:px-2">
                        <span className="block text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {rt.colMatrixCase}
                        </span>
                        {rt.caseTypeLabels[rule.returnCaseType] ?? rule.returnCaseType}
                      </td>
                      <td className="block py-1 text-[11px] text-[var(--text-secondary)] md:table-cell md:py-2 md:px-2">
                        <span className="block text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {rt.colMatrixResolution}
                        </span>
                        {localizedRule?.resolution ?? rule.resolutionMode}
                      </td>
                      <td className="block py-1 text-[var(--text-secondary)] md:table-cell md:py-2 md:px-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                          {rt.colMatrixPrecondition}
                        </span>
                        {localizedRule?.precondition ?? rule.precondition}
                      </td>
                      <td className="block py-1 text-[var(--text-primary)] md:table-cell md:py-2 md:px-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                          {rt.colMatrixEffect}
                        </span>
                        {localizedRule?.effect ?? rule.accountingAndStockEffect}
                      </td>
                      <td className="block py-1 md:table-cell md:py-2 md:pl-2 md:pr-4">
                        <span className="mb-1 block font-medium text-[var(--text-secondary)] md:hidden">
                          {rt.colMatrixDisposition}
                        </span>
                        <DispositionBadge
                          disposition={rule.disposition}
                          label={rt.dispositionLabels[rule.disposition] ?? rule.disposition}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}

      {/* 4-Metric Hairline KPI Strip */}
      <div className="grid grid-cols-1 gap-px overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--border-hairline)] sm:grid-cols-2 lg:grid-cols-4">
        <div className="bg-[var(--bg-surface)] p-4">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{rt.stats.pendingApprovalCount}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {String(overview.pendingApprovalCount)}
          </div>
        </div>
        <div className="bg-[var(--bg-surface)] p-4">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{rt.stats.openReplacementCount}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {String(overview.openReplacementCount)} <span className="font-sans text-xs font-medium text-[var(--text-secondary)]">{rt.kpiUnits.rpaShort}</span> ·{" "}
            <span className="text-sm font-semibold text-[var(--text-secondary)]">
              {formatQuantityValue(overview.openReplacementUnitsTotal, locale)}{" "}
              <span className="font-sans text-xs font-medium">{rt.kpiUnits.unitShort}</span>
            </span>
          </div>
        </div>
        <div className="bg-[var(--bg-surface)] p-4">
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {rt.stats.unbilledGrniReversedTotal}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(overview.unbilledGrniReversedTotalIdr)}
          </div>
        </div>
        <div className="bg-[var(--bg-surface)] p-4">
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {rt.stats.supplierCreditNotesTotal}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(overview.supplierCreditNotesTotalIdr)}
          </div>
        </div>
      </div>

      {/* Inline Supplier Return Composer Sheet */}
      {composerSource ? (
        <section
          id="ret-composer"
          ref={composerRef}
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <div className="flex items-center gap-2">
              <Plus className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">
                {rt.composer.title}
                {editingDraftCode ? ` · ${editingDraftCode}` : ""}
              </h2>
            </div>
            <button
              type="button"
              onClick={() => {
                setComposerSource(null);
                setEditingReturnId(null);
                setEditingDraftCode(null);
              }}
              className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              <X className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>{rt.composer.closeButton}</span>
            </button>
          </div>

          <form onSubmit={handleSaveDraft} className="divide-y divide-[var(--border-hairline)]">
            <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs">
              <div>
                <span className="font-medium text-[var(--text-secondary)]">{rt.composer.sourceSummaryLabel}: </span>
                <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                  {composerSource.sourceLabel} · {composerSource.itemCode}
                </span>
                <span className="text-[var(--text-primary)]"> — {composerSource.itemName}</span>
              </div>
              <span className="font-semibold text-[var(--text-primary)]">
                {rt.caseTypeLabels[composerSource.returnCaseType] ?? composerSource.returnCaseType}
              </span>
            </div>

            <div className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-2 lg:grid-cols-3">
              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.composer.resolutionModeLabel}
                </span>
                <select
                  id="ret-composer-resolution"
                  value={resolutionMode}
                  onChange={(e) => {
                    setResolutionMode(e.target.value);
                    setPreviewResult(null);
                  }}
                  className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="CLOSE_WITHOUT_REPLACEMENT">
                    {rt.resolutionModeLabels.CLOSE_WITHOUT_REPLACEMENT}
                  </option>
                  <option value="REPLACEMENT_ONLY">{rt.resolutionModeLabels.REPLACEMENT_ONLY}</option>
                  <option value="AP_CREDIT_NOTE">{rt.resolutionModeLabels.AP_CREDIT_NOTE}</option>
                </select>
              </label>

              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.composer.returnDateLabel}
                </span>
                <input
                  id="ret-composer-date"
                  type="date"
                  value={returnDate}
                  onChange={(e) => {
                    setReturnDate(e.target.value);
                    setPreviewResult(null);
                  }}
                  required
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.composer.reasonCodeLabel}
                </span>
                <select
                  id="ret-composer-reason"
                  value={reasonCode}
                  onChange={(e) => setReasonCode(e.target.value)}
                  className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  {Object.entries(rt.reasonCodeLabels).map(([code, label]) => (
                    <option key={code} value={code}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.composer.lineQtyLabel} ({composerSource.baseUomCode})
                </span>
                <input
                  id="ret-composer-qty"
                  type="number"
                  step="any"
                  min="0.000001"
                  max={composerSource.maxQty}
                  value={lineQty}
                  onChange={(e) => {
                    setLineQty(e.target.value);
                    setPreviewResult(null);
                  }}
                  required
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.composer.taxReturnNoteLabel}
                </span>
                <input
                  id="ret-composer-nota-retur"
                  type="text"
                  value={taxReturnNoteNumber}
                  onChange={(e) => {
                    setTaxReturnNoteNumber(e.target.value);
                    setPreviewResult(null);
                  }}
                  placeholder={rt.composer.taxReturnNotePlaceholder}
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
                <span className="block text-[11px] text-[var(--text-secondary)]">
                  {rt.composer.taxReturnNoteHint}
                </span>
              </label>

              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.composer.carrierRefLabel}
                </span>
                <input
                  id="ret-composer-carrier"
                  type="text"
                  value={carrierRef}
                  onChange={(e) => setCarrierRef(e.target.value)}
                  placeholder={rt.composer.carrierRefPlaceholder}
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>

              {composerSource.stockLocations.length > 0 ? (
                <label className="block space-y-1 sm:col-span-2">
                  <span className="block text-xs font-medium text-[var(--text-primary)]">
                    {rt.composer.stockLocationLabel}
                  </span>
                  <select
                    id="ret-composer-location"
                    value={selectedLocationKey}
                    onChange={(e) => {
                      setSelectedLocationKey(e.target.value);
                      setPreviewResult(null);
                    }}
                    className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                  >
                    {composerSource.stockLocations.map((loc) => (
                      <option key={makeLocationKey(loc)} value={makeLocationKey(loc)}>
                        {loc.locationCode} ({loc.locationName}) · {loc.stockStatus}
                        {loc.lotNumber ? ` · Lot ${loc.lotNumber}` : ""} —{" "}
                        {formatQuantityValue(loc.eligibleQuantity, locale)} {composerSource.baseUomCode}
                      </option>
                    ))}
                  </select>
                </label>
              ) : (
                <div className="flex items-center text-xs text-[var(--text-secondary)] sm:col-span-2">
                  {rt.composer.noLocationNeeded}
                </div>
              )}

              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.composer.notesLabel}
                </span>
                <input
                  id="ret-composer-notes"
                  type="text"
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder={rt.composer.notesPlaceholder}
                  className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
            </div>

            {composerError ? (
              <div className="px-4 py-3">
                <div
                  role="alert"
                  className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                >
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{composerError}</span>
                </div>
              </div>
            ) : null}

            {/* Live Preview Output */}
            {previewResult ? (
              <div data-testid="ret-live-preview" className="space-y-3 px-4 py-3.5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-semibold text-[var(--text-primary)]">
                      {rt.composer.previewTitle}
                    </span>
                    <span className="font-code text-xs font-bold text-[var(--text-primary)]">
                      {previewResult.matrixCode}
                    </span>
                    <DispositionBadge
                      disposition={previewResult.disposition}
                      label={rt.dispositionLabels[previewResult.disposition] ?? previewResult.disposition}
                    />
                  </div>
                  <span className="text-[11px] text-[var(--text-secondary)]">
                    {previewResult.isPeriodOpen ? rt.composer.previewPeriodOpen : rt.composer.previewPeriodClosed}
                  </span>
                </div>

                {previewResult.blockingReason ? (
                  <div
                    role="alert"
                    data-testid="ret-preview-blocked-banner"
                    className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                  >
                    <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    <div>
                      <span>
                        {(previewResult.blockingCode && rt.errors[previewResult.blockingCode]) ||
                          previewResult.blockingReason}
                      </span>
                    </div>
                  </div>
                ) : null}

                {(() => {
                  const previewBlocked = previewResult.disposition !== "SUPPORTED";
                  return (
                    <>
                      <div className="-mx-4 grid grid-cols-2 gap-px border-y border-[var(--border-hairline)] bg-[var(--border-hairline)] sm:grid-cols-3 lg:grid-cols-6">
                        <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                          <div className="text-[11px] text-[var(--text-secondary)]">{rt.composer.previewInventoryCredit}</div>
                          <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(previewBlocked ? 0 : previewResult.totalInventoryCreditAmount)}
                          </div>
                        </div>
                        <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                          <div className="text-[11px] text-[var(--text-secondary)]">{rt.composer.previewGrniReversal}</div>
                          <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(previewBlocked ? 0 : previewResult.totalGrniReversalAmount)}
                          </div>
                        </div>
                        <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                          <div className="text-[11px] text-[var(--text-secondary)]">{rt.composer.previewCreditSubtotal}</div>
                          <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(previewBlocked ? 0 : previewResult.totalCreditSubtotalAmount)}
                          </div>
                        </div>
                        <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                          <div className="text-[11px] text-[var(--text-secondary)]">{rt.composer.previewPpnReversal}</div>
                          <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(previewBlocked ? 0 : previewResult.totalPpnReversalAmount)}
                          </div>
                        </div>
                        <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                          <div className="text-[11px] text-[var(--text-secondary)]">{rt.composer.previewCreditTotal}</div>
                          <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(previewBlocked ? 0 : previewResult.totalCreditNoteAmount)}
                          </div>
                        </div>
                        <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                          <div className="text-[11px] text-[var(--text-secondary)]">{rt.composer.previewPpvDifference}</div>
                          <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(previewBlocked ? 0 : previewResult.totalPpvDifferenceAmount)}
                          </div>
                        </div>
                      </div>

                      {previewBlocked ? (
                        <p className="text-xs text-[var(--status-danger-text)]">{rt.composer.previewBlockedGlNotice}</p>
                      ) : previewResult.journalPreview.length > 0 ? (
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
                              {previewResult.journalPreview.map((j, idx) => (
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
                      ) : null}
                    </>
                  );
                })()}
              </div>
            ) : null}

            <div className="flex flex-wrap items-center gap-2.5 bg-[var(--bg-subtle)]/50 px-4 py-3">
              <button
                type="button"
                id="ret-preview-btn"
                disabled={previewBusy || saveBusy}
                onClick={handleRunPreview}
                className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
              >
                <span>{previewBusy ? rt.composer.previewBusy : rt.composer.previewButton}</span>
              </button>
              <button
                type="submit"
                id="ret-save-draft-btn"
                disabled={saveBusy || previewBusy}
                className={
                  previewResult && previewResult.disposition !== "SUPPORTED"
                    ? "inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-4 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                    : "inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                }
              >
                <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>
                  {saveBusy
                    ? rt.composer.saveDraftBusy
                    : editingReturnId
                      ? rt.composer.updateDraftButton
                      : rt.composer.saveDraftButton}
                </span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Linked Replacement Arrival Panel */}
      {activeRpa ? (
        <section
          id="ret-replacement-modal"
          ref={rpaModalRef}
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <div>
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">
                {rt.replacementModal.title} ·{" "}
                <span className="font-code tabular-nums">{activeRpa.authorizationNumber}</span>
              </h2>
              <p className="mt-0.5 text-[11px] text-[var(--text-secondary)]">{rt.replacementModal.subtitle}</p>
            </div>
            <button
              type="button"
              onClick={() => setActiveRpa(null)}
              className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              <X className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>{rt.replacementModal.cancelButton}</span>
            </button>
          </div>

          <form onSubmit={handleSubmitReplacementArrival} className="divide-y divide-[var(--border-hairline)]">
            <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs">
              <div>
                <span className="font-code font-bold text-[var(--text-primary)]">{activeRpa.itemCode}</span> —{" "}
                <span className="text-[var(--text-primary)]">{activeRpa.itemName}</span> ·{" "}
                {rt.replacementModal.remainingEntitlementLabel}:{" "}
                <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                  {formatQuantityValue(activeRpa.remainingBaseQuantity, locale)} {activeRpa.baseUomCode}
                </span>
              </div>
              <div className="text-[11px] text-[var(--text-secondary)]">
                {rt.replacementModal.authorizedCostLabel}:{" "}
                <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                  {formatUnitCostIdr(activeRpa.authorizedInventoryUnitCostBase)}
                </span>
                {" · "}
                {rt.replacementModal.commercialPriceNote}
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-2 lg:grid-cols-4">
              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.replacementModal.receivedQtyLabel} ({activeRpa.baseUomCode})
                </span>
                <input
                  id="ret-rpa-qty"
                  type="number"
                  step="any"
                  min="0.000001"
                  max={Number(activeRpa.remainingBaseQuantity)}
                  value={rpaQty}
                  onChange={(e) => setRpaQty(e.target.value)}
                  required
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.replacementModal.receivedDateLabel}
                </span>
                <input
                  id="ret-rpa-date"
                  type="date"
                  value={rpaDate}
                  onChange={(e) => setRpaDate(e.target.value)}
                  required
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.replacementModal.deliveryNoteLabel}
                </span>
                <input
                  id="ret-rpa-delivery-note"
                  type="text"
                  value={rpaDeliveryNote}
                  onChange={(e) => setRpaDeliveryNote(e.target.value)}
                  placeholder={rt.replacementModal.deliveryNotePlaceholder}
                  required
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.replacementModal.stationLabel}
                </span>
                <input
                  id="ret-rpa-station"
                  type="text"
                  value={rpaStation}
                  onChange={(e) => setRpaStation(e.target.value)}
                  placeholder={rt.replacementModal.stationPlaceholder}
                  required
                  className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 sm:col-span-2">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.replacementModal.vehicleRefLabel}
                </span>
                <input
                  id="ret-rpa-vehicle"
                  type="text"
                  value={rpaVehicleRef}
                  onChange={(e) => setRpaVehicleRef(e.target.value)}
                  className="font-code min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 sm:col-span-2">
                <span className="block text-xs font-medium text-[var(--text-primary)]">
                  {rt.replacementModal.notesLabel}
                </span>
                <input
                  id="ret-rpa-notes"
                  type="text"
                  value={rpaNotes}
                  onChange={(e) => setRpaNotes(e.target.value)}
                  className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="flex min-h-9 items-center gap-2 text-xs text-[var(--text-primary)] sm:col-span-4">
                <input
                  id="ret-rpa-inspect"
                  type="checkbox"
                  checked={rpaFlagInspect}
                  onChange={(e) => setRpaFlagInspect(e.target.checked)}
                  className="h-4 w-4 accent-[var(--accent-primary)]"
                />
                <span>{rt.replacementModal.flagInspectionLabel}</span>
              </label>
            </div>

            {rpaError ? (
              <div className="px-4 py-3">
                <div
                  role="alert"
                  className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                >
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{rpaError}</span>
                </div>
              </div>
            ) : null}

            <div className="flex flex-wrap items-center gap-2.5 bg-[var(--bg-subtle)]/50 px-4 py-3">
              <button
                type="submit"
                id="ret-rpa-submit-btn"
                disabled={rpaBusy}
                className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{rpaBusy ? rt.replacementModal.submitBusy : rt.replacementModal.submitButton}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Ledger Tab Navigation */}
      <div
        role="tablist"
        aria-label={rt.tabListLabel}
        className="flex flex-wrap items-center gap-1.5 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-1.5"
      >
        {(["queued", "returns", "replacements", "credits"] as ReturnsTab[]).map((tabKey) => {
          const isActive = activeTab === tabKey;
          const count =
            tabKey === "queued"
              ? overview.queuedUnvaluedRejections.length +
                overview.queuedValuedUnbilledLines.length +
                overview.queuedValuedBilledLines.length
              : tabKey === "returns"
                ? overview.returns.length
                : tabKey === "replacements"
                  ? overview.replacementAuthorizations.length
                  : overview.supplierCredits.length;
          return (
            <button
              key={tabKey}
              type="button"
              role="tab"
              aria-selected={isActive}
              aria-controls={`ret-tabpanel-${tabKey}`}
              id={`ret-tab-${tabKey}`}
              data-testid={`tab-${tabKey}`}
              onClick={() => setActiveTab(tabKey)}
              className={`inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] px-3 text-xs font-semibold ${
                isActive
                  ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                  : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
              }`}
            >
              <span>{rt.tabs[tabKey]}</span>
              <span className="font-code text-[11px] tabular-nums opacity-85">({String(count)})</span>
            </button>
          );
        })}
      </div>

      {/* Active Ledger Tab Panel */}
      <div
        id={`ret-tabpanel-${activeTab}`}
        role="tabpanel"
        aria-labelledby={`ret-tab-${activeTab}`}
      >
      {/* TAB 1: Queued Sources (M1 / M2 / M3) */}
      {activeTab === "queued" ? (
        <div className="space-y-4">
          {/* M1 Unvalued Dock Rejections */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {rt.subQueues.m1Unvalued}
            </div>
            {overview.queuedUnvaluedRejections.length === 0 ? (
              <div className="p-6 text-center">
                <p className="text-xs font-semibold text-[var(--text-primary)]">{rt.emptyUnvaluedTitle}</p>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">{rt.emptyUnvaluedBody}</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table id="ret-queue-m1-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2.5 pl-4 pr-2 font-semibold">{rt.colSourceDoc}</th>
                      <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colItemAndPo}</th>
                      <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colQtyEligible}</th>
                      <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colUnitCostPrice}</th>
                      <th scope="col" className="py-2.5 pl-2 pr-4 text-right font-semibold">{rt.colAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.queuedUnvaluedRejections.map((row) => (
                      <tr key={row.goodsReceiptLineId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colSourceDoc}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {row.receiptNumber}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {row.receivedDate} · {row.supplierCode}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colItemAndPo}</span>
                          <div className="font-semibold text-[var(--text-primary)]">
                            <span className="font-code">{row.itemCode}</span> — {row.itemName}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span className="font-code tabular-nums">{row.poNumber}</span> · {row.supplierLegalName}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colQtyEligible}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatQuantityValue(row.eligibleQuantity, locale)} {row.baseUomCode}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            / {formatQuantityValue(row.dockRejectedBaseQuantity, locale)} {row.baseUomCode}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colUnitCostPrice}</span>
                          <div className="font-code tabular-nums text-[var(--text-primary)]">
                            {formatUnitCostIdr(row.authorizedUnitCostBase)}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          {canAuthorM1 ? (
                            <button
                              type="button"
                              id={`ret-prepare-m1-${row.goodsReceiptLineId}`}
                              onClick={() => openComposerFromM1(row)}
                              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                            >
                              {rt.prepareReturnButton}
                            </button>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* M2 Valued Unbilled Receipts (GRNI) */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {rt.subQueues.m2Unbilled}
            </div>
            {overview.queuedValuedUnbilledLines.length === 0 ? (
              <div className="p-6 text-center">
                <p className="text-xs font-semibold text-[var(--text-primary)]">{rt.emptyUnbilledTitle}</p>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">{rt.emptyUnbilledBody}</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table id="ret-queue-m2-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2.5 pl-4 pr-2 font-semibold">{rt.colSourceDoc}</th>
                      <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colItemAndPo}</th>
                      <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colQtyEligible}</th>
                      <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colUnitCostPrice}</th>
                      <th scope="col" className="py-2.5 pl-2 pr-4 text-right font-semibold">{rt.colAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.queuedValuedUnbilledLines.map((row) => (
                      <tr key={row.receiptAccrualLineId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colSourceDoc}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {row.accrualNumber}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {row.receiptNumber} · {row.accrualPostingDate}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colItemAndPo}</span>
                          <div className="font-semibold text-[var(--text-primary)]">
                            <span className="font-code">{row.itemCode}</span> — {row.itemName}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span className="font-code tabular-nums">{row.poNumber}</span> · {row.supplierLegalName}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colQtyEligible}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatQuantityValue(row.eligibleQuantity, locale)} {row.baseUomCode}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {rt.micro.grniOpenShort} {formatQuantityValue(row.remainingUnbilledBaseQuantity, locale)} ·{" "}
                            {rt.micro.poolShort} {formatQuantityValue(row.poolValuedBaseQuantity, locale)}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colUnitCostPrice}</span>
                          <div className="font-code tabular-nums text-[var(--text-primary)]">
                            {formatUnitCostIdr(row.receiptUnitCostBase)}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            MAC {formatUnitCostIdr(row.poolMovingAverageCost)}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          {canAuthorValued ? (
                            <button
                              type="button"
                              id={`ret-prepare-m2-${row.receiptAccrualLineId}`}
                              disabled={Number(row.eligibleQuantity) <= 0}
                              onClick={() => openComposerFromM2(row)}
                              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:cursor-not-allowed disabled:opacity-50"
                            >
                              {rt.prepareReturnButton}
                            </button>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* M3 Valued Billed Invoices (AP Credit Note Queue + M5/M6 Blocks) */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {rt.subQueues.m3Billed}
            </div>
            {overview.queuedValuedBilledLines.length === 0 ? (
              <div className="p-6 text-center">
                <p className="text-xs font-semibold text-[var(--text-primary)]">{rt.emptyBilledTitle}</p>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">{rt.emptyBilledBody}</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table id="ret-queue-m3-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2.5 pl-4 pr-2 font-semibold">{rt.colSourceDoc}</th>
                      <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colItemAndPo}</th>
                      <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colQtyEligible}</th>
                      <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colMatrixState}</th>
                      <th scope="col" className="py-2.5 pl-2 pr-4 text-right font-semibold">{rt.colAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.queuedValuedBilledLines.map((row) => {
                      const isSupported = row.matrixStatusCode === "M3";
                      return (
                        <tr key={row.supplierBillLineId} className="block p-3 md:table-row md:p-0">
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colSourceDoc}</span>
                            <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                              {row.billNumber}
                            </div>
                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {row.supplierInvoiceNumber} · {row.payableNumber}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colItemAndPo}</span>
                            <div className="font-semibold text-[var(--text-primary)]">
                              <span className="font-code">{row.itemCode}</span> — {row.itemName}
                            </div>
                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              @ {formatUnitCostIdr(row.invoiceUnitPriceBase)} · {row.ppnTaxCodeSnapshot}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colQtyEligible}</span>
                            <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                              {formatQuantityValue(row.remainingCreditableQuantity, locale)} {row.baseUomCode}
                            </div>
                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              / {formatQuantityValue(row.billedBaseQuantity, locale)} {row.baseUomCode}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colMatrixState}</span>
                            <div className="flex flex-col items-start gap-1">
                              <DispositionBadge
                                disposition={isSupported ? "SUPPORTED" : "BLOCKED_POLICY_OD05"}
                                label={`${row.matrixStatusCode} · ${
                                  isSupported
                                    ? rt.dispositionLabels.SUPPORTED
                                    : rt.dispositionLabels.BLOCKED_POLICY_OD05
                                }`}
                              />
                              {row.matrixBlockReason ? (
                                <span className="text-[11px] text-[var(--status-danger-text)]">
                                  {rt.errors[row.matrixBlockReason] ?? row.matrixBlockReason}
                                </span>
                              ) : null}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                            {canAuthorValued ? (
                              <button
                                type="button"
                                id={`ret-prepare-m3-${row.supplierBillLineId}`}
                                disabled={!isSupported}
                                onClick={() => openComposerFromM3(row)}
                                className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:cursor-not-allowed disabled:opacity-50"
                              >
                                {rt.prepareReturnButton}
                              </button>
                            ) : null}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}

      {/* TAB 2: Return Authorizations (SRN) */}
      {activeTab === "returns" ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            {rt.tabs.returns}
          </div>
          {overview.returns.length === 0 ? (
            <div className="p-6 text-center">
              <p className="text-xs font-semibold text-[var(--text-primary)]">{rt.emptyReturnsTitle}</p>
              <p className="mt-1 text-xs text-[var(--text-secondary)]">{rt.emptyReturnsBody}</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table id="ret-returns-table" className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="py-2.5 pl-4 pr-2 font-semibold">{rt.colReturnNumber}</th>
                    <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colCaseAndMode}</th>
                    <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colAmounts}</th>
                    <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colStatus}</th>
                    <th scope="col" className="py-2.5 pl-2 pr-4 text-right font-semibold">{rt.colAction}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.returns.map((ret) => {
                    const isViewing = selectedDetail?.returnHeader.supplierReturnId === ret.supplierReturnId;
                    return (
                      <tr
                        key={ret.supplierReturnId}
                        className={`block p-3 md:table-row md:p-0 ${isViewing ? "bg-[var(--bg-subtle)]" : ""}`}
                      >
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colReturnNumber}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {ret.returnNumber ?? ret.draftCode}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {ret.returnDate} · {ret.poNumberSnapshot}
                            {ret.billNumberSnapshot ? ` · ${ret.billNumberSnapshot}` : ""}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colCaseAndMode}</span>
                          <div className="font-medium text-[var(--text-primary)]">
                            {rt.caseTypeLabels[ret.returnCaseType] ?? ret.returnCaseType}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            {rt.resolutionModeLabels[ret.resolutionMode] ?? ret.resolutionMode}
                            {ret.taxReturnNoteNumber ? ` · NR ${ret.taxReturnNoteNumber}` : ""}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colAmounts}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatQuantityValue(ret.totalReturnedBaseQuantity, locale)} {rt.micro.qtyShort} ·{" "}
                            {rt.micro.invShort} {formatIdr(ret.totalInventoryCreditAmount)}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {rt.micro.grniShort} {formatIdr(ret.totalGrniReversalAmount)} · {rt.micro.creditShort}{" "}
                            {formatIdr(ret.totalCreditNoteAmount)}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colStatus}</span>
                          <div className="flex flex-wrap items-center gap-1.5">
                            <ReturnStatusBadge
                              status={ret.status}
                              label={rt.statusLabels[ret.status] ?? ret.status}
                            />
                            {ret.creditNumber ? (
                              <span className="font-code text-[11px] font-semibold text-[var(--status-ok-text)]">
                                {ret.creditNumber}
                              </span>
                            ) : null}
                          </div>
                          <div className="font-code mt-0.5 text-[11px] text-[var(--text-secondary)]">
                            {ret.submittedByEmail ?? "—"}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          <button
                            type="button"
                            onClick={() => handleOpenDetail(ret.supplierReturnId)}
                            className={`inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border px-3 text-xs font-semibold ${
                              isViewing
                                ? "border-[var(--accent-primary)] bg-[var(--accent-tint)] text-[var(--accent-primary)]"
                                : "border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                            }`}
                          >
                            {isViewing ? rt.activeDetailButton : rt.openDetailButton}
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
      ) : null}

      {/* TAB 3: Replacement Entitlements (RPA) */}
      {activeTab === "replacements" ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            {rt.tabs.replacements}
          </div>
          {overview.replacementAuthorizations.length === 0 ? (
            <div className="p-6 text-center">
              <p className="text-xs font-semibold text-[var(--text-primary)]">{rt.emptyReplacementsTitle}</p>
              <p className="mt-1 text-xs text-[var(--text-secondary)]">{rt.emptyReplacementsBody}</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table id="ret-replacements-table" className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="py-2.5 pl-4 pr-2 font-semibold">{rt.colRpaNumber}</th>
                    <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colItemAndPo}</th>
                    <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colRpaEntitlement}</th>
                    <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colRpaCost}</th>
                    <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colStatus}</th>
                    <th scope="col" className="py-2.5 pl-2 pr-4 text-right font-semibold">{rt.colAction}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.replacementAuthorizations.map((rpa) => (
                    <tr key={rpa.replacementAuthorizationId} className="block p-3 md:table-row md:p-0">
                      <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colRpaNumber}</span>
                        <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                          {rpa.authorizationNumber}
                        </div>
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {rpa.returnNumber} · {formatJakartaDateTime(rpa.createdAtUtc, locale)}
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colItemAndPo}</span>
                        <div className="font-semibold text-[var(--text-primary)]">
                          <span className="font-code">{rpa.itemCode}</span> — {rpa.itemName}
                        </div>
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {rpa.poNumber}
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colRpaEntitlement}</span>
                        <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                          {formatQuantityValue(rpa.remainingBaseQuantity, locale)} /{" "}
                          {formatQuantityValue(rpa.authorizedBaseQuantity, locale)} {rpa.baseUomCode}
                        </div>
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {rt.micro.delivShort} {formatQuantityValue(rpa.deliveredBaseQuantity, locale)} ·{" "}
                          {rt.micro.accShort} {formatQuantityValue(rpa.acceptedBaseQuantity, locale)}
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colRpaCost}</span>
                        <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                          {formatUnitCostIdr(rpa.authorizedInventoryUnitCostBase)}
                        </div>
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {rt.micro.apDeltaShort}: {formatIdr(rpa.commercialIncrementalUnitPrice)}
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                        <span className="mb-1 block font-medium text-[var(--text-secondary)] md:hidden">{rt.colStatus}</span>
                        <RpaStatusBadge
                          status={rpa.status}
                          label={rt.rpaStatusLabels[rpa.status] ?? rpa.status}
                        />
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                        {canAuthorM1 &&
                        Number(rpa.remainingBaseQuantity) > 0 &&
                        (rpa.status === "ACTIVE" || rpa.status === "PARTIALLY_RECEIVED") ? (
                          <button
                            type="button"
                            id={`ret-receive-rpa-${rpa.replacementAuthorizationId}`}
                            onClick={() => openReceiveReplacement(rpa.replacementAuthorizationId)}
                            className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                          >
                            {rt.receiveReplacementButton}
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : null}

      {/* TAB 4: AP Supplier Credit Notes (SCN) */}
      {activeTab === "credits" ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            {rt.tabs.credits}
          </div>
          {overview.supplierCredits.length === 0 ? (
            <div className="p-6 text-center">
              <p className="text-xs font-semibold text-[var(--text-primary)]">{rt.emptyCreditsTitle}</p>
              <p className="mt-1 text-xs text-[var(--text-secondary)]">{rt.emptyCreditsBody}</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table id="ret-credits-table" className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="py-2.5 pl-4 pr-2 font-semibold">{rt.colCreditNumber}</th>
                    <th scope="col" className="py-2.5 px-2 font-semibold">{rt.colNotaRetur}</th>
                    <th scope="col" className="py-2.5 px-2 text-right font-semibold">{rt.colCreditBreakdown}</th>
                    <th scope="col" className="py-2.5 pl-2 pr-4 text-right font-semibold">{rt.colPostedRefs}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.supplierCredits.map((credit) => (
                    <tr key={credit.supplierCreditId} className="block p-3 md:table-row md:p-0">
                      <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colCreditNumber}</span>
                        <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                          {credit.creditNumber}
                        </div>
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {credit.returnNumber} · {rt.micro.billShort} {credit.billNumber} · {rt.micro.payableShort}{" "}
                          {credit.payableNumber}
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colNotaRetur}</span>
                        <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                          {credit.taxReturnNoteNumber ?? "—"}
                        </div>
                        <div className="text-[11px] text-[var(--text-secondary)]">
                          {credit.supplierLegalName}
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colCreditBreakdown}</span>
                        <div className="font-code font-bold tabular-nums text-[var(--status-ok-text)]">
                          Dr 2100: {formatIdr(credit.totalCreditAmount)}
                        </div>
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          <span>
                            {rt.micro.baseShort} {formatIdr(credit.subtotalCreditAmount)} · PPN{" "}
                            {formatIdr(credit.ppnReversalAmount)}
                          </span>
                          <br className="hidden md:inline" />
                          <span className="md:hidden"> · </span>
                          <span>
                            {rt.micro.invShort} {formatIdr(credit.inventoryCreditAmount)} · PPV{" "}
                            {formatIdr(credit.ppvDifferenceAmount)}
                          </span>
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.colPostedRefs}</span>
                        <div className="font-code whitespace-nowrap font-semibold tabular-nums text-[var(--text-primary)]">
                          {credit.journalNumber}
                        </div>
                        <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {credit.movementNumber} · {credit.postingDate}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : null}
      </div>

      {/* Contextual Return Detail Sheet */}
      <SupplierReturnDetailSheet
        key={selectedDetail?.returnHeader.supplierReturnId ?? "empty"}
        tenantId={tenantId}
        entityId={entityId}
        detail={selectedDetail}
        currentUserEmail={user?.email ?? ""}
        canAuthorCase={canAuthorCase}
        canApproveCase={canApproveCase}
        canExecuteCase={canExecuteCase}
        onUpdated={handleDetailUpdated}
        onEditDraft={handleOpenEditDraft}
        onOpenReceiveReplacement={(authId) => {
          setActiveTab("replacements");
          openReceiveReplacement(authId);
        }}
        onClose={() => setSelectedDetail(null)}
      />
    </div>
  );
}
