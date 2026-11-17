"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  Ban,
  CheckCircle2,
  Clock,
  FileCheck2,
  GitCompare,
  Info,
  Lock,
  Plus,
  Search,
  ShoppingCart,
  Trash2,
} from "lucide-react";
import { useSessionUser } from "@/components/AppShell";
import { ScopedBoundaryError, ScopedLoadingSheet } from "@/components/ScopedBoundaryState";
import {
  ApiProblemError,
  cancelPurchaseOrder,
  createPurchaseOrder,
  createPurchaseOrderAmendment,
  decidePurchaseOrderRevision,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchProcurementReferenceData,
  fetchPurchaseOrderDetail,
  fetchPurchaseOrders,
  PoLineInputDto,
  ProcurementReferenceDataResponse,
  PurchaseOrderDetailDto,
  PurchaseOrderRevisionDto,
  PurchaseOrderSummaryDto,
  shortClosePurchaseOrder,
  submitPurchaseOrderRevision,
  updatePurchaseOrderDraftRevision,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

type ProcurementTab = "list" | "editor";

interface DraftPoLine {
  key: string;
  lineageLineKey: string | null;
  itemId: string;
  orderedUomId: string;
  orderedQuantity: string;
  unitPrice: string;
  taxCodeId: string;
  lineDescription: string;
}

/** Parses decimal input cleanly into BigInt scaled by 10^scaleDigits without IEEE-754 drift. */
function parseScaledBigInt(raw: number | string | null | undefined, scaleDigits: number): bigint {
  if (raw === null || raw === undefined) return 0n;
  const text = String(raw).trim();
  if (!text || text.startsWith("-")) return 0n;
  const match = /^(\d+)(?:\.(\d*))?$/.exec(text);
  if (!match) return 0n;
  const whole = BigInt(match[1] ?? "0");
  const fracPadded = (match[2] ?? "").slice(0, scaleDigits).padEnd(scaleDigits, "0");
  const frac = fracPadded.length > 0 ? BigInt(fracPadded) : 0n;
  return whole * (10n ** BigInt(scaleDigits)) + frac;
}

function roundScaledDivHalfAway(numerator: bigint, denominator: bigint): bigint {
  if (denominator <= 0n || numerator <= 0n) return 0n;
  return (numerator + denominator / 2n) / denominator;
}

function formatMoney(raw: number | string | bigint | null | undefined, locale: "id-ID" | "en-US"): React.ReactNode {
  const num =
    typeof raw === "bigint"
      ? Number(raw)
      : raw === null || raw === undefined
        ? 0
        : typeof raw === "number"
          ? raw
          : Number.parseFloat(raw);
  const safe = Number.isFinite(num) ? num : 0;
  const formatted = new Intl.NumberFormat(locale === "id-ID" ? "id-ID" : "en-US", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(safe);
  return <span className="whitespace-nowrap">{"Rp\u00A0"}{formatted}</span>;
}

function formatSignedMoneyDelta(raw: number | string | null | undefined, locale: "id-ID" | "en-US"): React.ReactNode {
  const num =
    raw === null || raw === undefined
      ? 0
      : typeof raw === "number"
        ? raw
        : Number.parseFloat(raw);
  const safe = Number.isFinite(num) ? num : 0;
  const sign = safe > 0 ? "+" : safe < 0 ? "-" : "";
  const formatted = new Intl.NumberFormat(locale === "id-ID" ? "id-ID" : "en-US", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(Math.abs(safe));
  return <span className="whitespace-nowrap">{sign}{"Rp\u00A0"}{formatted}</span>;
}

function formatQty(raw: number | string | null | undefined, locale: "id-ID" | "en-US", maxDecimals = 4): string {
  const num = raw === null || raw === undefined ? 0 : typeof raw === "number" ? raw : Number.parseFloat(raw);
  if (!Number.isFinite(num)) return "0";
  return new Intl.NumberFormat(locale === "id-ID" ? "id-ID" : "en-US", {
    minimumFractionDigits: 0,
    maximumFractionDigits: maxDecimals,
  }).format(num);
}

function formatScaled6Qty(scaled6: bigint, locale: "id-ID" | "en-US", maxDecimals = 4): string {
  const whole = scaled6 / 1_000_000n;
  const frac = Number(scaled6 % 1_000_000n) / 1_000_000;
  return formatQty(Number(whole) + frac, locale, maxDecimals);
}

function StatusBadge({ status, label }: { status: string; label: string }) {
  const style =
    status === "APPROVED" || status === "FULLY_RECEIVED" || status === "ADDED"
      ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
      : status === "REJECTED" || status === "CANCELLED" || status === "REMOVED"
        ? "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
        : status === "SUPERSEDED" || status === "NOT_APPLICABLE" || status === "NOT_RECEIVED" || status === "UNCHANGED"
          ? "border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)]"
          : "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]";

  return (
    <span className={`inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-semibold ${style}`}>
      {status === "APPROVED" || status === "FULLY_RECEIVED" || status === "ADDED" ? (
        <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      ) : status === "REJECTED" || status === "CANCELLED" || status === "REMOVED" ? (
        <AlertCircle className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      ) : status === "SUPERSEDED" || status === "UNCHANGED" ? (
        <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      ) : (
        <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      )}
      <span>{label}</span>
    </span>
  );
}

function ProcurementSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const currentUser = useSessionUser();
  const { locale, t } = useLocale();
  const pt = t.procurement;

  const [detail, setDetail] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [refData, setRefData] = useState<ProcurementReferenceDataResponse | null>(null);
  const [poList, setPoList] = useState<PurchaseOrderSummaryDto[]>([]);
  const [selectedPo, setSelectedPo] = useState<PurchaseOrderDetailDto | null>(null);
  const [selectedPoId, setSelectedPoId] = useState<string | null>(null);
  const [selectedRevisionId, setSelectedRevisionId] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<ProcurementTab>("list");
  const [loading, setLoading] = useState(true);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  // Filters
  const [searchText, setSearchText] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  // Editor state (Create / Edit Draft)
  const [editingPoId, setEditingPoId] = useState<string | null>(null);
  const [editingRevisionId, setEditingRevisionId] = useState<string | null>(null);
  const [editingPoNumber, setEditingPoNumber] = useState<string | null>(null);
  const [supplierId, setSupplierId] = useState("");
  const [orderDate, setOrderDate] = useState("2026-11-18");
  const [expectedDate, setExpectedDate] = useState("2026-11-28");
  const [destination, setDestination] = useState("");
  const [termsDays, setTermsDays] = useState("30");
  const [toleranceId, setToleranceId] = useState("");
  const [changeReason, setChangeReason] = useState("");
  const [editorNotes, setEditorNotes] = useState("");
  const [draftLines, setDraftLines] = useState<DraftPoLine[]>([
    { key: "po-line-1", lineageLineKey: "L001", itemId: "", orderedUomId: "", orderedQuantity: "1", unitPrice: "100000", taxCodeId: "", lineDescription: "" },
  ]);
  const [editorError, setEditorError] = useState<{
    field: "supplier" | "orderDate" | "expectedDate" | "destination" | "terms" | "form";
    message: string;
  } | null>(null);
  const [editorBusy, setEditorBusy] = useState(false);

  // Sticky Detail Action states
  const [decisionReason, setDecisionReason] = useState("");
  const [decisionError, setDecisionError] = useState<string | null>(null);
  const [decisionBusy, setDecisionBusy] = useState<"APPROVED" | "REJECTED" | null>(null);

  const [amendReason, setAmendReason] = useState("");
  const [amendError, setAmendError] = useState<string | null>(null);
  const [amendBusy, setAmendBusy] = useState(false);

  const [closeReason, setCloseReason] = useState("");
  const [closeError, setCloseError] = useState<string | null>(null);
  const [closeBusy, setCloseBusy] = useState(false);

  const [cancelReason, setCancelReason] = useState("");
  const [cancelError, setCancelError] = useState<string | null>(null);
  const [cancelBusy, setCancelBusy] = useState(false);

  const [submitBusy, setSubmitBusy] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const tabButtonRefs = useRef<Record<ProcurementTab, HTMLButtonElement | null>>({
    list: null,
    editor: null,
  });

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  const mapServerError = useCallback(
    (err: unknown, fallback: string) => {
      const apiErr = err as ApiProblemError;
      if (apiErr.status === 401) {
        routerRef.current.replace("/login");
        return fallback;
      }
      if (apiErr.errorCode && pt.errors[apiErr.errorCode]) {
        return pt.errors[apiErr.errorCode];
      }
      return apiErr.detail || fallback;
    },
    [pt.errors],
  );

  const pickDefaultRevisionId = useCallback((po: PurchaseOrderDetailDto): string | null => {
    const pending = po.revisions.find((r) => r.status === "PENDING_APPROVAL");
    if (pending) return pending.revisionId;
    const draft = po.revisions.find((r) => r.status === "DRAFT");
    if (draft) return draft.revisionId;
    if (po.currentApprovedRevisionId) return po.currentApprovedRevisionId;
    return po.revisions[0]?.revisionId ?? null;
  }, []);

  const applyLoadedPoDetail = useCallback(
    (po: PurchaseOrderDetailDto) => {
      setSelectedPo(po);
      setSelectedPoId(po.purchaseOrderId);
      setSelectedRevisionId(pickDefaultRevisionId(po));
    },
    [pickDefaultRevisionId],
  );

  const fetchSnapshot = useCallback(
    async (opts?: { q?: string; st?: string; preservePoId?: string | null }) => {
      const [entityDetail, refResp, listResp] = await Promise.all([
        fetchEntityWorkspaceDetail(tenantId, entityId),
        fetchProcurementReferenceData(tenantId, entityId),
        fetchPurchaseOrders(tenantId, entityId, {
          search: opts?.q ?? (searchText.trim() || undefined),
          status: opts?.st ?? (statusFilter || undefined),
        }),
      ]);

      const targetPoId = opts?.preservePoId ?? selectedPoId;
      const detailResp =
        targetPoId && listResp.items.some((p) => p.purchaseOrderId === targetPoId)
          ? await fetchPurchaseOrderDetail(tenantId, entityId, targetPoId)
          : null;

      return { entityDetail, refResp, listResp, detailResp };
    },
    [tenantId, entityId, searchText, statusFilter, selectedPoId],
  );

  const applySnapshot = useCallback(
    (snap: Awaited<ReturnType<typeof fetchSnapshot>>) => {
      setDetail(snap.entityDetail);
      setRefData(snap.refResp);
      setPoList(snap.listResp.items);

      if (snap.refResp.suppliers.length > 0 && !supplierId) {
        const firstSup = snap.refResp.suppliers[0];
        setSupplierId(firstSup.partnerId);
        setTermsDays(String(firstSup.defaultPaymentTermsDays ?? 30));
      }

      if (snap.refResp.tolerancePolicies.length > 0 && !toleranceId) {
        const defPol = snap.refResp.tolerancePolicies.find((p) => p.isDefault) ?? snap.refResp.tolerancePolicies[0];
        setToleranceId(defPol.policyId);
      }

      if (snap.refResp.items.length > 0) {
        setDraftLines((prev) =>
          prev.map((line, i) => {
            if (line.itemId || i > 0) return line;
            const firstItem = snap.refResp.items[0];
            return {
              ...line,
              itemId: firstItem.itemId,
              orderedUomId: firstItem.defaultPurchaseUomId || firstItem.baseUomId,
            };
          }),
        );
      }

      if (snap.detailResp) {
        applyLoadedPoDetail(snap.detailResp);
      }
    },
    [supplierId, toleranceId, applyLoadedPoDetail],
  );

  const loadWorkspaceData = useCallback(
    async (opts?: { q?: string; st?: string; preservePoId?: string | null }) => {
      const snap = await fetchSnapshot(opts);
      applySnapshot(snap);
    },
    [fetchSnapshot, applySnapshot],
  );

  const handleRetryLoad = () => {
    setLoading(true);
    setProblem(null);
    setLoadError(null);
    setReloadNonce((n) => n + 1);
  };

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchProcurementReferenceData(tenantId, entityId),
      fetchPurchaseOrders(tenantId, entityId, {}),
    ])
      .then(([entityDetail, refResp, listResp]) => {
        if (!active) return;
        applySnapshot({ entityDetail, refResp, listResp, detailResp: null });
        setProblem(null);
        setLoadError(null);
      })
      .catch((err) => {
        if (!active) return;
        const apiErr = err as ApiProblemError;
        if (apiErr.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
        if (apiErr.status === 403 || apiErr.status === 404) {
          setProblem(apiErr);
        } else {
          setLoadError(apiErr.detail || t.login.errors.upstream_unavailable);
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId, entityId, reloadNonce]);

  const itemById = useMemo(() => {
    const map = new Map<string, ProcurementReferenceDataResponse["items"][number]>();
    for (const item of refData?.items ?? []) {
      map.set(item.itemId, item);
    }
    return map;
  }, [refData]);

  const taxById = useMemo(() => {
    const map = new Map<string, ProcurementReferenceDataResponse["taxCodes"][number]>();
    for (const tc of refData?.taxCodes ?? []) {
      map.set(tc.taxCodeId, tc);
    }
    return map;
  }, [refData]);

  const editorTotals = useMemo(() => {
    let subtotalIdr = 0n;
    let ppnIdr = 0n;
    let whtIdr = 0n;
    let totalBaseScaled6 = 0n;
    const linePreviews: Array<{
      key: string;
      baseQtyScaled6: bigint;
      baseUomCode: string;
      subtotalIdr: bigint;
      dppIdr: bigint;
      taxIdr: bigint;
      taxFamily: string | null;
    }> = [];

    for (const line of draftLines) {
      const item = itemById.get(line.itemId);
      const uomOpt = item?.allowedUoms.find((u) => u.uomId === line.orderedUomId) ?? item?.allowedUoms[0];
      const qtyScaled6 = parseScaledBigInt(line.orderedQuantity, 6);
      const factorScaled6 = parseScaledBigInt(uomOpt?.factorToBase ?? "1", 6);
      const priceScaled4 = parseScaledBigInt(line.unitPrice, 4);

      const baseQtyScaled6 = roundScaledDivHalfAway(qtyScaled6 * factorScaled6, 1_000_000n);
      const subScaled4 = roundScaledDivHalfAway(qtyScaled6 * priceScaled4, 1_000_000n);
      const subWhole = roundScaledDivHalfAway(subScaled4, 10_000n);

      let taxWhole = 0n;
      let dppWhole = subWhole;
      let taxFamily: string | null = null;
      const tc = line.taxCodeId ? taxById.get(line.taxCodeId) : undefined;
      if (tc && subWhole > 0n) {
        const num = BigInt(Number(tc.taxableBaseRatioNumerator) || 1);
        const den = BigInt(Number(tc.taxableBaseRatioDenominator) || 1);
        dppWhole = roundScaledDivHalfAway(subWhole * num, den);
        const rateScaled4 = parseScaledBigInt(tc.nominalRatePercent, 4);
        taxWhole = roundScaledDivHalfAway(dppWhole * rateScaled4, 1_000_000n);
        taxFamily = tc.taxFamily;
        if (taxFamily === "PPN") ppnIdr += taxWhole;
        else if (taxFamily === "PPH") whtIdr += taxWhole;
      }

      subtotalIdr += subWhole;
      totalBaseScaled6 += baseQtyScaled6;

      linePreviews.push({
        key: line.key,
        baseQtyScaled6,
        baseUomCode: item?.baseUomCode ?? "",
        subtotalIdr: subWhole,
        dppIdr: dppWhole,
        taxIdr: taxWhole,
        taxFamily,
      });
    }

    return {
      linePreviews,
      subtotalIdr,
      ppnIdr,
      whtIdr,
      grandTotalIdr: subtotalIdr + ppnIdr,
      totalBaseScaled6,
    };
  }, [draftLines, itemById, taxById]);

  if (loading && !detail) {
    return <ScopedLoadingSheet />;
  }

  if (problem || !detail) {
    return <ScopedBoundaryError problem={problem} onRetry={handleRetryLoad} />;
  }

  const perms = new Set(detail.effectivePermissions ?? []);
  const canManage = perms.has("procurement.manage");
  const canApprove = perms.has("procurement.approve");
  const isReadOnly = !canManage && !canApprove;

  const visibleTabs: ProcurementTab[] = canManage ? ["list", "editor"] : ["list"];

  const selectedRevision: PurchaseOrderRevisionDto | null =
    selectedPo?.revisions.find((r) => r.revisionId === selectedRevisionId) ??
    selectedPo?.revisions[0] ??
    null;

  const hasOpenRevision =
    selectedPo?.revisions.some((r) => r.status === "DRAFT" || r.status === "PENDING_APPROVAL") ?? false;

  const handleTabKeyDown = (e: React.KeyboardEvent<HTMLButtonElement>, current: ProcurementTab) => {
    const idx = visibleTabs.indexOf(current);
    if (idx < 0) return;
    let nextIdx = -1;
    if (e.key === "ArrowRight") nextIdx = (idx + 1) % visibleTabs.length;
    else if (e.key === "ArrowLeft") nextIdx = (idx - 1 + visibleTabs.length) % visibleTabs.length;
    else if (e.key === "Home") nextIdx = 0;
    else if (e.key === "End") nextIdx = visibleTabs.length - 1;
    if (nextIdx >= 0) {
      e.preventDefault();
      const targetTab = visibleTabs[nextIdx];
      setActiveTab(targetTab);
      tabButtonRefs.current[targetTab]?.focus();
    }
  };

  function resetEditorForNewDraft() {
    const firstSup = refData?.suppliers[0];
    const firstItem = refData?.items[0];
    const defPol = refData?.tolerancePolicies.find((p) => p.isDefault) ?? refData?.tolerancePolicies[0];
    setEditingPoId(null);
    setEditingRevisionId(null);
    setEditingPoNumber(null);
    setSupplierId(firstSup?.partnerId ?? "");
    setOrderDate("2026-11-18");
    setExpectedDate("2026-11-28");
    setDestination("");
    setTermsDays(String(firstSup?.defaultPaymentTermsDays ?? 30));
    setToleranceId(defPol?.policyId ?? "");
    setChangeReason("");
    setEditorNotes("");
    setDraftLines([
      {
        key: `po-line-${Date.now()}`,
        lineageLineKey: "L001",
        itemId: firstItem?.itemId ?? "",
        orderedUomId: firstItem?.defaultPurchaseUomId ?? firstItem?.baseUomId ?? "",
        orderedQuantity: "1",
        unitPrice: "100000",
        taxCodeId: "",
        lineDescription: "",
      },
    ]);
    setEditorError(null);
  }

  function loadRevisionIntoEditor(po: PurchaseOrderDetailDto, rev: PurchaseOrderRevisionDto) {
    setEditingPoId(po.purchaseOrderId);
    setEditingRevisionId(rev.revisionId);
    setEditingPoNumber(po.poNumber ?? po.draftCode);
    setSupplierId(rev.supplierPartnerId);
    setOrderDate(rev.orderDate);
    setExpectedDate(rev.expectedDeliveryDate ?? "");
    setDestination(rev.deliveryDestination);
    setTermsDays(String(rev.paymentTermsDays));
    setToleranceId(rev.tolerancePolicyId);
    setChangeReason(rev.changeReason ?? "");
    setEditorNotes(rev.notes ?? "");
    setDraftLines(
      rev.lines.map((l, idx) => ({
        key: `edit-${l.poLineId}-${idx}`,
        lineageLineKey: l.lineageLineKey,
        itemId: l.itemId,
        orderedUomId: l.orderedUomId,
        orderedQuantity: String(l.orderedQuantity),
        unitPrice: String(l.unitPrice),
        taxCodeId: l.taxCodeId ?? "",
        lineDescription: l.lineDescription ?? "",
      })),
    );
    setEditorError(null);
    setActiveTab("editor");
  }

  async function handleInspectPo(poId: string) {
    setDecisionError(null);
    setAmendError(null);
    setCloseError(null);
    setCancelError(null);
    setSubmitError(null);
    try {
      const poDetail = await fetchPurchaseOrderDetail(tenantId, entityId, poId);
      applyLoadedPoDetail(poDetail);
    } catch (err) {
      setLoadError(mapServerError(err, pt.loadErrorTitle));
    }
  }

  async function handleFilterChange(nextSearch: string, nextStatus: string) {
    setSearchText(nextSearch);
    setStatusFilter(nextStatus);
    try {
      const list = await fetchPurchaseOrders(tenantId, entityId, {
        search: nextSearch.trim() || undefined,
        status: nextStatus || undefined,
      });
      setPoList(list.items);
    } catch (err) {
      setLoadError(mapServerError(err, pt.loadErrorTitle));
    }
  }

  async function handleSaveOrSubmitEditor(alsoSubmit: boolean) {
    setEditorError(null);
    if (!supplierId) {
      setEditorError({ field: "supplier", message: pt.validation.supplierRequired });
      return;
    }
    if (!orderDate) {
      setEditorError({ field: "orderDate", message: pt.validation.orderDateRequired });
      return;
    }
    if (expectedDate && expectedDate < orderDate) {
      setEditorError({ field: "expectedDate", message: pt.validation.expectedDateBeforeOrder });
      return;
    }
    if (!destination.trim()) {
      setEditorError({ field: "destination", message: pt.validation.destinationRequired });
      return;
    }
    const parsedTerms = Number(termsDays);
    if (!Number.isInteger(parsedTerms) || parsedTerms < 0 || parsedTerms > 365) {
      setEditorError({ field: "terms", message: pt.validation.paymentTermsInvalid });
      return;
    }
    if (draftLines.length === 0) {
      setEditorError({ field: "form", message: pt.validation.linesRequired });
      return;
    }

    const linePayload: PoLineInputDto[] = [];
    for (let i = 0; i < draftLines.length; i += 1) {
      const row = draftLines[i];
      if (!row.itemId) {
        setEditorError({ field: "form", message: pt.validation.lineItemRequired });
        return;
      }
      const qtyScaled = parseScaledBigInt(row.orderedQuantity, 6);
      const priceScaled = parseScaledBigInt(row.unitPrice, 4);
      if (qtyScaled <= 0n) {
        setEditorError({ field: "form", message: pt.validation.lineQtyInvalid });
        return;
      }
      if (priceScaled <= 0n) {
        setEditorError({ field: "form", message: pt.validation.linePriceInvalid });
        return;
      }
      linePayload.push({
        lineageLineKey: row.lineageLineKey,
        itemId: row.itemId,
        orderedUomId: row.orderedUomId,
        orderedQuantity: row.orderedQuantity.trim(),
        unitPrice: row.unitPrice.trim(),
        taxCodeId: row.taxCodeId || null,
        overdeliveryTolerancePercent: 0,
        overdeliveryToleranceBaseQty: 0,
        allowUnlimitedOverdelivery: false,
        lineDescription: row.lineDescription.trim() || null,
      });
    }

    setEditorBusy(true);
    try {
      let savedPo: PurchaseOrderDetailDto;
      if (editingPoId && editingRevisionId) {
        savedPo = await updatePurchaseOrderDraftRevision(tenantId, entityId, editingPoId, editingRevisionId, {
          supplierPartnerId: supplierId,
          orderDate,
          expectedDeliveryDate: expectedDate || null,
          deliveryDestination: destination.trim(),
          paymentTermsDays: parsedTerms,
          tolerancePolicyId: toleranceId || null,
          changeReason: changeReason.trim() || null,
          notes: editorNotes.trim() || null,
          lines: linePayload,
        });
      } else {
        savedPo = await createPurchaseOrder(tenantId, entityId, {
          supplierPartnerId: supplierId,
          currencyCode: detail?.entity.functionalCurrencyCode ?? "IDR",
          orderDate,
          expectedDeliveryDate: expectedDate || null,
          deliveryDestination: destination.trim(),
          paymentTermsDays: parsedTerms,
          tolerancePolicyId: toleranceId || null,
          notes: editorNotes.trim() || null,
          lines: linePayload,
        });
      }

      if (alsoSubmit) {
        const revToSubmit =
          editingRevisionId ??
          savedPo.revisions.find((r) => r.status === "DRAFT")?.revisionId ??
          savedPo.revisions[0].revisionId;
        savedPo = await submitPurchaseOrderRevision(tenantId, entityId, savedPo.purchaseOrderId, revToSubmit, {
          poNumberOverride: null,
        });
      }

      resetEditorForNewDraft();
      setActiveTab("list");
      await loadWorkspaceData({ preservePoId: savedPo.purchaseOrderId });
      applyLoadedPoDetail(savedPo);
    } catch (err) {
      setEditorError({ field: "form", message: mapServerError(err, pt.loadErrorTitle) });
    } finally {
      setEditorBusy(false);
    }
  }

  async function handleSubmitExistingDraft(poId: string, revisionId: string) {
    setSubmitError(null);
    setSubmitBusy(true);
    try {
      const updated = await submitPurchaseOrderRevision(tenantId, entityId, poId, revisionId, {
        poNumberOverride: null,
      });
      await loadWorkspaceData({ preservePoId: updated.purchaseOrderId });
      applyLoadedPoDetail(updated);
    } catch (err) {
      setSubmitError(mapServerError(err, pt.loadErrorTitle));
    } finally {
      setSubmitBusy(false);
    }
  }

  async function handleDecideRevision(decision: "APPROVED" | "REJECTED") {
    if (!selectedPo || !selectedRevision) return;
    setDecisionError(null);
    if (!decisionReason.trim()) {
      setDecisionError(pt.validation.decisionReasonRequired);
      return;
    }

    setDecisionBusy(decision);
    try {
      const updated = await decidePurchaseOrderRevision(
        tenantId,
        entityId,
        selectedPo.purchaseOrderId,
        selectedRevision.revisionId,
        { decision, reason: decisionReason.trim() },
      );
      setDecisionReason("");
      await loadWorkspaceData({ preservePoId: updated.purchaseOrderId });
      applyLoadedPoDetail(updated);
    } catch (err) {
      setDecisionError(mapServerError(err, pt.loadErrorTitle));
    } finally {
      setDecisionBusy(null);
    }
  }

  async function handleCreateAmendment() {
    if (!selectedPo) return;
    setAmendError(null);
    if (!amendReason.trim()) {
      setAmendError(pt.validation.amendmentReasonRequired);
      return;
    }

    setAmendBusy(true);
    try {
      const updated = await createPurchaseOrderAmendment(tenantId, entityId, selectedPo.purchaseOrderId, {
        changeReason: amendReason.trim(),
      });
      setAmendReason("");
      await loadWorkspaceData({ preservePoId: updated.purchaseOrderId });
      applyLoadedPoDetail(updated);
      const draftRev = updated.revisions.find((r) => r.status === "DRAFT");
      if (draftRev) {
        loadRevisionIntoEditor(updated, draftRev);
      }
    } catch (err) {
      setAmendError(mapServerError(err, pt.loadErrorTitle));
    } finally {
      setAmendBusy(false);
    }
  }

  async function handleShortClose() {
    if (!selectedPo) return;
    setCloseError(null);
    if (!closeReason.trim()) {
      setCloseError(pt.validation.closeReasonRequired);
      return;
    }

    setCloseBusy(true);
    try {
      const updated = await shortClosePurchaseOrder(tenantId, entityId, selectedPo.purchaseOrderId, {
        reason: closeReason.trim(),
      });
      setCloseReason("");
      await loadWorkspaceData({ preservePoId: updated.purchaseOrderId });
      applyLoadedPoDetail(updated);
    } catch (err) {
      setCloseError(mapServerError(err, pt.loadErrorTitle));
    } finally {
      setCloseBusy(false);
    }
  }

  async function handleCancelPo() {
    if (!selectedPo) return;
    setCancelError(null);
    if (!cancelReason.trim()) {
      setCancelError(pt.validation.cancelReasonRequired);
      return;
    }

    setCancelBusy(true);
    try {
      const updated = await cancelPurchaseOrder(tenantId, entityId, selectedPo.purchaseOrderId, {
        reason: cancelReason.trim(),
      });
      setCancelReason("");
      await loadWorkspaceData({ preservePoId: updated.purchaseOrderId });
      applyLoadedPoDetail(updated);
    } catch (err) {
      setCancelError(mapServerError(err, pt.loadErrorTitle));
    } finally {
      setCancelBusy(false);
    }
  }

  const isSubmitterSelf =
    selectedRevision?.submittedByUserId != null &&
    selectedRevision.submittedByUserId.toLowerCase() === currentUser.userId.toLowerCase();

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">{pt.pageTitle}</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
              {detail.tenant.code} · {detail.entity.code} · {detail.entity.functionalCurrencyCode}
            </span>
          </p>
        </div>

        <Link
          href={`/w/${tenantId}/${entityId}`}
          className="inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
          <span>
            {pt.backToOverview} · <span className="font-code tabular-nums">{detail.entity.code}</span>
          </span>
        </Link>
      </div>

      {isReadOnly ? (
        <div
          role="status"
          className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-4 py-3 text-sm text-[var(--status-warn-text)]"
        >
          <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{pt.readOnlyBanner}</span>
        </div>
      ) : null}

      <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs text-[var(--text-secondary)]">
        <Info className="mt-0.5 h-4 w-4 shrink-0 text-[var(--text-secondary)]" strokeWidth={1.75} aria-hidden="true" />
        <span>{pt.governanceNotice}</span>
      </div>

      {loadError ? (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-sm text-[var(--status-danger-text)]"
        >
          <div className="flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{loadError}</span>
          </div>
          <button
            type="button"
            onClick={handleRetryLoad}
            className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3 text-xs font-semibold"
          >
            {pt.retryButton}
          </button>
        </div>
      ) : null}

      {canManage ? (
        <div role="tablist" aria-label={pt.tabListLabel} className="flex flex-wrap gap-2">
          <button
            ref={(el) => {
              tabButtonRefs.current.list = el;
            }}
            id="tab-po-list"
            type="button"
            role="tab"
            aria-selected={activeTab === "list"}
            tabIndex={activeTab === "list" ? 0 : -1}
            onClick={() => setActiveTab("list")}
            onKeyDown={(e) => handleTabKeyDown(e, "list")}
            className={`inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border px-3 text-xs font-semibold ${
              activeTab === "list"
                ? "border-[var(--accent-primary)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            <ShoppingCart className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.tabs.list}</span>
          </button>

          <button
            ref={(el) => {
              tabButtonRefs.current.editor = el;
            }}
            id="tab-po-editor"
            type="button"
            role="tab"
            aria-selected={activeTab === "editor"}
            tabIndex={activeTab === "editor" ? 0 : -1}
            onClick={() => {
              if (activeTab !== "editor" && !editingPoId) {
                resetEditorForNewDraft();
              }
              setActiveTab("editor");
            }}
            onKeyDown={(e) => handleTabKeyDown(e, "editor")}
            className={`inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border px-3 text-xs font-semibold ${
              activeTab === "editor"
                ? "border-[var(--accent-primary)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            <Plus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.tabs.editor}</span>
          </button>
        </div>
      ) : null}

      {activeTab === "list" ? (
        <div role="tabpanel" aria-labelledby="tab-po-list" className="space-y-6">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <div className="flex items-center gap-2">
                <ShoppingCart className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                <h2 className="text-xs font-semibold text-[var(--text-primary)]">{pt.poListHeading}</h2>
              </div>
              <span className="font-code text-xs tabular-nums text-[var(--text-secondary)]">{poList.length}</span>
            </div>

            <div className="grid grid-cols-1 gap-3 border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4 sm:grid-cols-12">
              <div className="relative sm:col-span-8">
                <Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-[var(--text-muted)]" strokeWidth={1.75} aria-hidden="true" />
                <input
                  id="po-search"
                  type="text"
                  value={searchText}
                  onChange={(e) => void handleFilterChange(e.target.value, statusFilter)}
                  placeholder={pt.searchPlaceholder}
                  aria-label={pt.searchPlaceholder}
                  className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] pl-9 pr-3 text-sm text-[var(--text-primary)]"
                />
              </div>
              <div className="sm:col-span-4">
                <select
                  id="po-status-filter"
                  value={statusFilter}
                  onChange={(e) => void handleFilterChange(searchText, e.target.value)}
                  aria-label={pt.allStatusesOption}
                  className="min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
                >
                  <option value="">{pt.allStatusesOption}</option>
                  {["DRAFT", "PENDING_APPROVAL", "APPROVED", "CLOSED", "CANCELLED"].map((st) => (
                    <option key={st} value={st}>
                      {pt.poStatusLabels[st] ?? st}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {poList.length === 0 ? (
              <p className="px-4 py-8 text-center text-sm text-[var(--text-secondary)]">{pt.emptyList}</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-sm">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="px-2.5 py-2.5 pl-4">{pt.colPoNumber}</th>
                      <th scope="col" className="px-2.5 py-2.5">{pt.colSupplier}</th>
                      <th scope="col" className="px-2.5 py-2.5">{pt.colDates}</th>
                      <th scope="col" className="px-2.5 py-2.5">{pt.colRevision}</th>
                      <th scope="col" className="px-2.5 py-2.5 text-right">{pt.colTotals}</th>
                      <th scope="col" className="px-2.5 py-2.5">{pt.colStatus}</th>
                      <th scope="col" className="px-2.5 py-2.5 pr-4 text-right">{pt.colAction}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {poList.map((po) => {
                      const isSelected = selectedPoId === po.purchaseOrderId;
                      const isPendingAndSelf =
                        po.pendingApprovalSubmittedByUserId?.toLowerCase() === currentUser.userId.toLowerCase();
                      const actionText = isReadOnly
                        ? pt.readOnlyActionButton
                        : canApprove && po.pendingApprovalRevisionId && !isPendingAndSelf
                          ? pt.reviewActionButton
                          : po.pendingApprovalRevisionId && isPendingAndSelf
                            ? pt.pendingSelfActionButton
                            : pt.manageActionButton;

                      return (
                        <tr
                          key={po.purchaseOrderId}
                          className={`block border-b border-[var(--border-hairline)] px-4 py-3 last:border-b-0 md:table-row md:px-0 md:py-0 ${
                            isSelected ? "bg-[var(--bg-subtle)]" : ""
                          }`}
                        >
                          <td className="block py-1 md:table-cell md:px-2.5 md:py-3 md:pl-4">
                            <span className="block text-[11px] text-[var(--text-muted)] md:hidden">{pt.colPoNumber}</span>
                            <div className="font-code whitespace-nowrap text-xs font-semibold tabular-nums text-[var(--text-primary)]">
                              {po.poNumber ?? po.draftCode}
                            </div>
                            {po.poNumber ? (
                              <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-muted)]">
                                {po.draftCode}
                              </div>
                            ) : null}
                          </td>
                          <td className="block py-1 md:table-cell md:px-2.5 md:py-3">
                            <span className="block text-[11px] text-[var(--text-muted)] md:hidden">{pt.colSupplier}</span>
                            <div className="font-medium text-[var(--text-primary)]">
                              <span className="font-code whitespace-nowrap text-xs tabular-nums">{po.supplierCode}</span> ·{" "}
                              {po.supplierName}
                            </div>
                            <div className="text-xs text-[var(--text-secondary)]">{po.deliveryDestination}</div>
                          </td>
                          <td className="block py-1 md:table-cell md:px-2.5 md:py-3">
                            <span className="block text-[11px] text-[var(--text-muted)] md:hidden">{pt.colDates}</span>
                            <div className="font-code whitespace-nowrap text-xs tabular-nums text-[var(--text-primary)]">{po.orderDate}</div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-muted)]">
                              {po.paymentTermsDays} {pt.termsDaysSuffix}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:px-2.5 md:py-3">
                            <span className="block text-[11px] text-[var(--text-muted)] md:hidden">{pt.colRevision}</span>
                            <div className="font-code whitespace-nowrap text-xs tabular-nums text-[var(--text-primary)]">
                              {pt.revisionPrefix} {Number(po.currentApprovedRevisionNumber) > 0 ? po.currentApprovedRevisionNumber : po.latestRevisionNumber}/{po.latestRevisionNumber}
                            </div>
                            {po.hasOpenRevision && Number(po.currentApprovedRevisionNumber) > 0 ? (
                              <div className="whitespace-nowrap text-[11px] font-medium text-[var(--status-warn-text)]">{pt.openDraftBadge}</div>
                            ) : null}
                          </td>
                          <td className="block py-1 md:table-cell md:px-2.5 md:py-3 md:text-right">
                            <span className="block text-[11px] text-[var(--text-muted)] md:hidden">{pt.colTotals}</span>
                            <div className="font-code text-xs font-semibold tabular-nums text-[var(--text-primary)]">
                              {formatMoney(po.currentGrandTotalAmount, locale)}
                            </div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-muted)]">
                              {formatQty(po.currentOrderedBaseQuantity, locale)} · {formatQty(po.remainingOpenBaseQuantity, locale)} {pt.openQtySuffix}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:px-2.5 md:py-3">
                            <span className="block text-[11px] text-[var(--text-muted)] md:hidden">{pt.colStatus}</span>
                            <div className="flex flex-wrap gap-1">
                              <StatusBadge status={po.status} label={pt.poStatusLabels[po.status] ?? po.status} />
                              <StatusBadge
                                status={po.receivingProgress}
                                label={pt.receivingProgressLabels[po.receivingProgress] ?? po.receivingProgress}
                              />
                            </div>
                          </td>
                          <td className="block pb-1 pt-2 md:table-cell md:px-2.5 md:py-3 md:pr-4 md:text-right">
                            <button
                              type="button"
                              onClick={() => void handleInspectPo(po.purchaseOrderId)}
                              className="inline-flex min-h-9 items-center justify-center whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                            >
                              {actionText}
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

          {!selectedPo ? (
            <div className="rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-4 text-xs text-[var(--text-secondary)]">
              {pt.selectPoPrompt}
            </div>
          ) : selectedRevision ? (
            <section
              id="po-detail-sheet"
              className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
            >
              <div className="flex flex-wrap items-start justify-between gap-3 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
                <div>
                  <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                    {pt.detailHeadingPrefix} —{" "}
                    <span className="font-code tabular-nums">{selectedPo.poNumber ?? selectedPo.draftCode}</span>
                  </h2>
                  <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-[var(--text-secondary)]">
                    <span>
                      <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{selectedPo.supplierCode}</span> ·{" "}
                      {selectedPo.supplierName}
                    </span>
                    <span>·</span>
                    <StatusBadge status={selectedPo.status} label={pt.poStatusLabels[selectedPo.status] ?? selectedPo.status} />
                  </div>
                </div>
                <div className="space-y-0.5 text-left text-xs text-[var(--text-secondary)] sm:text-right">
                  <div>
                    <span>{pt.snapshotHashLabel} </span>
                    <span className="font-code tabular-nums text-[var(--text-primary)]">
                      {selectedRevision.snapshotHash.slice(0, 16)}…
                    </span>
                  </div>
                  <div className="font-code text-[11px] tabular-nums text-[var(--text-muted)]">
                    {formatJakartaDateTime(selectedPo.updatedAtUtc, locale)} WIB
                  </div>
                </div>
              </div>

              {/* Commitment Summary (Derived in S05; Zero Valued Stock) */}
              <div className="grid grid-cols-2 gap-3 border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] px-4 py-3 sm:grid-cols-4">
                <div>
                  <div className="text-[11px] text-[var(--text-muted)]">{pt.orderedBaseLabel}</div>
                  <div className="font-code mt-0.5 text-sm font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatQty(selectedPo.approvedOrderedBaseQuantity, locale)}
                  </div>
                </div>
                <div>
                  <div className="text-[11px] text-[var(--text-muted)]">{pt.acceptedBaseLabel}</div>
                  <div className="font-code mt-0.5 text-sm font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatQty(selectedPo.acceptedBaseQuantityTotal, locale)}
                  </div>
                </div>
                <div>
                  <div className="text-[11px] text-[var(--text-muted)]">{pt.remainingOpenLabel}</div>
                  <div className="font-code mt-0.5 text-sm font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatQty(selectedPo.remainingOpenBaseQuantity, locale)}
                  </div>
                </div>
                <div>
                  <div className="text-[11px] text-[var(--text-muted)]">{pt.receivingProgressLabel}</div>
                  <div className="mt-1">
                    <StatusBadge
                      status={selectedPo.receivingProgress}
                      label={pt.receivingProgressLabels[selectedPo.receivingProgress] ?? selectedPo.receivingProgress}
                    />
                  </div>
                </div>
              </div>

              {/* Revision Switcher */}
              <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] px-4 py-2.5">
                <span className="text-xs font-semibold text-[var(--text-secondary)]">{pt.revisionHistoryLabel}</span>
                <div className="flex flex-wrap gap-2">
                  {selectedPo.revisions.map((rev) => {
                    const isRevActive = rev.revisionId === selectedRevision.revisionId;
                    return (
                      <button
                        key={rev.revisionId}
                        type="button"
                        aria-pressed={isRevActive}
                        onClick={() => setSelectedRevisionId(rev.revisionId)}
                        className={`inline-flex min-h-9 items-center gap-2 whitespace-nowrap rounded-[var(--radius-sm)] border px-3 text-xs font-semibold ${
                          isRevActive
                            ? "border-[var(--border-strong)] bg-[var(--bg-subtle)] text-[var(--text-primary)] ring-1 ring-[var(--border-strong)]"
                            : "border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
                        }`}
                      >
                        <span className="font-code tabular-nums">
                          {pt.revisionPrefix} {rev.revisionNumber}
                        </span>
                        <StatusBadge
                          status={rev.status}
                          label={pt.revisionStatusLabels[rev.status] ?? rev.status}
                        />
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Commercial Snapshot Metadata */}
              <div className="grid grid-cols-1 gap-3 border-b border-[var(--border-hairline)] px-4 py-3 text-xs sm:grid-cols-3">
                <div>
                  <span className="text-[var(--text-muted)]">{pt.destinationLabel} </span>
                  <span className="font-medium text-[var(--text-primary)]">{selectedRevision.deliveryDestination}</span>
                </div>
                <div>
                  <span className="text-[var(--text-muted)]">{pt.termsLabel} </span>
                  <span className="font-code font-medium tabular-nums text-[var(--text-primary)]">
                    {selectedRevision.paymentTermsDays} {pt.termsDaysSuffix}
                  </span>
                </div>
                <div>
                  <StatusBadge
                    status={selectedRevision.isFrozen ? "APPROVED" : "DRAFT"}
                    label={selectedRevision.isFrozen ? pt.frozenBadge : pt.editableBadge}
                  />
                </div>
                {selectedRevision.changeReason ? (
                  <div className="sm:col-span-3">
                    <span className="text-[var(--text-muted)]">{pt.changeReasonLabel} </span>
                    <span className="font-medium text-[var(--text-primary)]">{selectedRevision.changeReason}</span>
                  </div>
                ) : null}
                {selectedRevision.approval ? (
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-[var(--text-secondary)] sm:col-span-3">
                    <span>
                      {pt.submittedByLabel}{" "}
                      <span className="font-code tabular-nums text-[var(--text-primary)]">{selectedRevision.approval.requestedByEmail}</span>
                    </span>
                    {selectedRevision.approval.decidedByEmail ? (
                      <span>
                        {pt.decidedByLabel}{" "}
                        <span className="font-code tabular-nums text-[var(--text-primary)]">{selectedRevision.approval.decidedByEmail}</span>
                        {selectedRevision.approval.decisionReason ? ` — “${selectedRevision.approval.decisionReason}”` : ""}
                      </span>
                    ) : null}
                  </div>
                ) : null}
              </div>

              {/* Revision Lines Table */}
              <div className="border-b border-[var(--border-hairline)]">
                <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
                  {pt.lineTableHeading}
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full border-collapse text-left text-xs">
                    <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[11px] font-semibold text-[var(--text-secondary)] md:table-header-group">
                      <tr>
                        <th scope="col" className="px-4 py-2">{pt.colLineNo}</th>
                        <th scope="col" className="px-4 py-2">{pt.colItem}</th>
                        <th scope="col" className="px-4 py-2 text-right">{pt.colQtyUom}</th>
                        <th scope="col" className="px-4 py-2 text-right">{pt.colBaseConversion}</th>
                        <th scope="col" className="px-4 py-2 text-right">{pt.colUnitPrice}</th>
                        <th scope="col" className="px-4 py-2 text-right">{pt.colTax}</th>
                        <th scope="col" className="px-4 py-2 text-right">{pt.colLineTotal}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedRevision.lines.map((line) => (
                        <tr
                          key={line.poLineId}
                          className="block border-b border-[var(--border-hairline)] px-4 py-2.5 last:border-b-0 md:table-row md:px-0 md:py-0"
                        >
                          <td className="font-code block py-0.5 tabular-nums text-[var(--text-muted)] md:table-cell md:px-4 md:py-2.5">
                            {line.lineNumber} ({line.lineageLineKey})
                          </td>
                          <td className="block py-0.5 md:table-cell md:px-4 md:py-2.5">
                            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{line.itemCodeSnapshot}</span>{" "}
                            <span className="text-[var(--text-primary)]">{line.itemNameSnapshot}</span>
                            <div className="text-[11px] text-[var(--text-muted)]">
                              {pt.itemKindLabels[line.itemKindSnapshot] ?? line.itemKindSnapshot}
                              {line.lineDescription ? ` · ${line.lineDescription}` : ""}
                            </div>
                          </td>
                          <td className="font-code block py-0.5 tabular-nums md:table-cell md:px-4 md:py-2.5 md:text-right">
                            <span className="mr-1 font-sans text-[11px] text-[var(--text-muted)] md:hidden">{pt.colQtyUom}:</span>
                            <span className="whitespace-nowrap">
                              {formatQty(line.orderedQuantity, locale)} {line.orderedUomCodeSnapshot}
                            </span>
                          </td>
                          <td className="font-code block py-0.5 tabular-nums text-[var(--text-secondary)] md:table-cell md:px-4 md:py-2.5 md:text-right">
                            <span className="mr-1 font-sans text-[11px] text-[var(--text-muted)] md:hidden">{pt.colBaseConversion}:</span>
                            <span className="whitespace-nowrap">
                              {formatQty(line.orderedBaseQuantity, locale)} {line.baseUomCodeSnapshot}
                            </span>
                          </td>
                          <td className="font-code block py-0.5 tabular-nums md:table-cell md:px-4 md:py-2.5 md:text-right">
                            <span className="mr-1 font-sans text-[11px] text-[var(--text-muted)] md:hidden">{pt.colUnitPrice}:</span>
                            {formatMoney(line.unitPrice, locale)}
                          </td>
                          <td className="font-code block py-0.5 tabular-nums text-[var(--text-secondary)] md:table-cell md:px-4 md:py-2.5 md:text-right">
                            <span className="mr-1 font-sans text-[11px] text-[var(--text-muted)] md:hidden">{pt.colTax}:</span>
                            {line.taxCodeSnapshot ? (
                              <span className="inline-flex flex-col md:items-end">
                                <span className="whitespace-nowrap">{line.taxCodeSnapshot}</span>
                                <span className="whitespace-nowrap">{formatMoney(line.taxAmount, locale)}</span>
                              </span>
                            ) : (
                              "—"
                            )}
                          </td>
                          <td className="font-code block py-0.5 font-semibold tabular-nums text-[var(--text-primary)] md:table-cell md:px-4 md:py-2.5 md:text-right">
                            <span className="mr-1 font-sans text-[11px] text-[var(--text-muted)] md:hidden">{pt.colLineTotal}:</span>
                            {formatMoney(line.lineSubtotalAmount, locale)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="grid grid-cols-1 gap-1.5 border-t border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs sm:flex sm:flex-wrap sm:items-center sm:justify-end sm:gap-x-6 sm:gap-y-1">
                  <div className="flex items-center justify-between gap-3 sm:block">
                    <span className="text-[var(--text-secondary)]">{pt.subtotalLabel} </span>
                    <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{formatMoney(selectedRevision.subtotalAmount, locale)}</span>
                  </div>
                  <div className="flex items-center justify-between gap-3 sm:block">
                    <span className="text-[var(--text-secondary)]">{pt.ppnTotalLabel} </span>
                    <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{formatMoney(selectedRevision.ppnTaxTotalAmount, locale)}</span>
                  </div>
                  {Number(selectedRevision.withholdingTaxTotalAmount) > 0 ? (
                    <div className="flex items-center justify-between gap-3 sm:block">
                      <span className="text-[var(--text-secondary)]">{pt.whtTotalLabel} </span>
                      <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{formatMoney(selectedRevision.withholdingTaxTotalAmount, locale)}</span>
                    </div>
                  ) : null}
                  <div className="flex items-center justify-between gap-3 border-t border-[var(--border-hairline)] pt-1.5 sm:block sm:border-t-0 sm:pt-0">
                    <span className="font-semibold text-[var(--text-primary)]">{pt.grandTotalLabel} </span>
                    <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                      {formatMoney(selectedRevision.grandTotalAmount, locale)}
                    </span>
                  </div>
                </div>
              </div>

              {/* Approval Comparison Diff vs Baseline Revision */}
              {selectedRevision.diffFromBaseline && selectedRevision.diffFromBaseline.baselineRevisionNumber != null ? (
                <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3 text-xs">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-1.5 font-semibold text-[var(--text-primary)]">
                      <GitCompare className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                      <span>
                        {pt.diffHeading} (<span className="font-code tabular-nums">{pt.revisionPrefix} {selectedRevision.diffFromBaseline.baselineRevisionNumber}</span>
                      </span>
                      <ArrowRight className="h-3.5 w-3.5 text-[var(--text-muted)]" strokeWidth={1.75} aria-hidden="true" />
                      <span className="font-code tabular-nums">
                        {pt.revisionPrefix} {selectedRevision.diffFromBaseline.targetRevisionNumber})
                      </span>
                    </div>
                    <div className="flex flex-wrap gap-4">
                      <span>
                        {pt.diffTotalDeltaLabel}{" "}
                        <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                          {formatSignedMoneyDelta(selectedRevision.diffFromBaseline.grandTotalDelta, locale)}
                        </span>
                      </span>
                      <span>
                        {pt.diffBaseQtyDeltaLabel}{" "}
                        <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                          {Number(selectedRevision.diffFromBaseline.orderedBaseQtyDelta) > 0 ? "+" : ""}
                          {formatQty(selectedRevision.diffFromBaseline.orderedBaseQtyDelta, locale)}
                        </span>
                      </span>
                    </div>
                  </div>

                  {selectedRevision.diffFromBaseline.headerChanges.length === 0 ? (
                    <p className="mt-2 text-[var(--text-secondary)]">{pt.diffNoHeaderChanges}</p>
                  ) : (
                    <ul className="mt-2 space-y-1">
                      {selectedRevision.diffFromBaseline.headerChanges.map((hc) => (
                        <li key={hc.fieldKey} className="flex flex-wrap items-center gap-1.5">
                          <span className="font-semibold text-[var(--text-primary)]">
                            {pt.headerFieldLabels[hc.fieldKey] ?? hc.fieldKey}:
                          </span>
                          <span className="font-code tabular-nums text-[var(--text-muted)] line-through">{hc.previousValue ?? "—"}</span>
                          <ArrowRight className="h-3 w-3 text-[var(--text-muted)]" strokeWidth={1.75} aria-hidden="true" />
                          <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{hc.currentValue ?? "—"}</span>
                        </li>
                      ))}
                    </ul>
                  )}

                  <div className="mt-2.5 overflow-x-auto rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
                    <table className="w-full border-collapse text-left text-xs">
                      <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[11px] text-[var(--text-secondary)] md:table-header-group">
                        <tr>
                          <th scope="col" className="px-2.5 py-1.5 pl-3">{pt.colDiffLineKey}</th>
                          <th scope="col" className="px-2.5 py-1.5">{pt.colDiffChange}</th>
                          <th scope="col" className="px-2.5 py-1.5">{pt.colDiffPrevious}</th>
                          <th scope="col" className="px-2.5 py-1.5">{pt.colDiffCurrent}</th>
                          <th scope="col" className="px-2.5 py-1.5 pr-3 text-right">{pt.colDiffSubtotal}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {selectedRevision.diffFromBaseline.lineChanges.map((lc) => {
                          const rowHighlight =
                            lc.changeKind === "MODIFIED"
                              ? "bg-[var(--status-warn-bg)]/45"
                              : lc.changeKind === "ADDED"
                                ? "bg-[var(--status-ok-bg)]/45"
                                : lc.changeKind === "REMOVED"
                                  ? "bg-[var(--status-danger-bg)]/45"
                                  : "";
                          return (
                            <tr
                              key={lc.lineageLineKey}
                              className={`block border-b border-[var(--border-hairline)] px-3 py-2 last:border-b-0 md:table-row md:px-0 md:py-0 ${rowHighlight}`}
                            >
                              <td className="font-code block py-0.5 font-semibold tabular-nums md:table-cell md:px-2.5 md:py-2 md:pl-3">
                                {lc.lineageLineKey}
                              </td>
                              <td className="block py-0.5 md:table-cell md:px-2.5 md:py-2">
                                <span className="mr-1 font-normal text-[var(--text-muted)] md:hidden">{pt.colDiffChange}:</span>
                                <StatusBadge
                                  status={lc.changeKind}
                                  label={pt.diffChangeLabels[lc.changeKind] ?? lc.changeKind}
                                />
                              </td>
                              <td className="font-code block py-0.5 tabular-nums text-[var(--text-secondary)] md:table-cell md:px-2.5 md:py-2">
                                <span className="mr-1 font-sans text-[11px] text-[var(--text-muted)] md:hidden">{pt.colDiffPrevious}:</span>
                                {lc.previousItemCode ? (
                                  <span>
                                    <span className="whitespace-nowrap">{lc.previousItemCode}</span> ·{" "}
                                    <span className="whitespace-nowrap">
                                      {formatQty(lc.previousQuantity, locale)} {lc.previousUomCode ?? ""}
                                    </span>{" "}
                                    <span className="whitespace-nowrap">@ {formatMoney(lc.previousUnitPrice, locale)}</span>
                                  </span>
                                ) : (
                                  "—"
                                )}
                              </td>
                              <td className="font-code block py-0.5 tabular-nums text-[var(--text-primary)] md:table-cell md:px-2.5 md:py-2">
                                <span className="mr-1 font-sans text-[11px] text-[var(--text-muted)] md:hidden">{pt.colDiffCurrent}:</span>
                                {lc.currentItemCode ? (
                                  <span>
                                    <span className="whitespace-nowrap">{lc.currentItemCode}</span> ·{" "}
                                    <span className="whitespace-nowrap">
                                      {formatQty(lc.currentQuantity, locale)} {lc.currentUomCode ?? ""}
                                    </span>{" "}
                                    <span className="whitespace-nowrap">@ {formatMoney(lc.currentUnitPrice, locale)}</span>
                                  </span>
                                ) : (
                                  "—"
                                )}
                              </td>
                              <td className="font-code block py-0.5 font-semibold tabular-nums text-[var(--text-primary)] md:table-cell md:px-2.5 md:py-2 md:pr-3 md:text-right">
                                <span className="mr-1 font-sans text-[11px] font-normal text-[var(--text-muted)] md:hidden">{pt.colDiffSubtotal}:</span>
                                {lc.previousSubtotal != null && lc.currentSubtotal != null ? (
                                  <span className="inline-flex flex-col md:items-end">
                                    <span className="text-[11px] font-normal text-[var(--text-muted)]">{formatMoney(lc.previousSubtotal, locale)} →</span>
                                    <span>{formatMoney(lc.currentSubtotal, locale)}</span>
                                  </span>
                                ) : lc.currentSubtotal != null ? (
                                  formatMoney(lc.currentSubtotal, locale)
                                ) : lc.previousSubtotal != null ? (
                                  formatMoney(lc.previousSubtotal, locale)
                                ) : (
                                  "—"
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : null}

              {/* Four-Eyes & Commitment Control Panel */}
              {!isReadOnly ? (
                <div className="space-y-4 border-t border-[var(--border-strong)] bg-[var(--bg-surface)] px-4 py-4">
                  <div className="flex items-center gap-2 text-xs font-semibold text-[var(--text-primary)]">
                    <FileCheck2 className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                    <span>{pt.stickyBarHeading}</span>
                  </div>

                  {/* Pending Approval Actions & Four-Eyes Enforcement */}
                  {selectedRevision.status === "PENDING_APPROVAL" ? (
                    isSubmitterSelf ? (
                      <div
                        role="status"
                        data-testid="po-self-approval-blocked"
                        className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2.5 text-xs text-[var(--status-warn-text)]"
                      >
                        <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{pt.selfApprovalBlockedBanner}</span>
                      </div>
                    ) : canApprove ? (
                      <form
                        noValidate
                        onSubmit={(e) => {
                          e.preventDefault();
                          void handleDecideRevision("APPROVED");
                        }}
                        className="space-y-2.5 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-3"
                      >
                        <p className="text-xs text-[var(--text-secondary)]">{pt.pendingApprovalNote}</p>
                        <div>
                          <label htmlFor="po-decision-reason" className="block text-xs font-medium text-[var(--text-primary)]">
                            {pt.decisionReasonLabel}
                          </label>
                          <input
                            id="po-decision-reason"
                            type="text"
                            value={decisionReason}
                            onChange={(e) => setDecisionReason(e.target.value)}
                            placeholder={pt.decisionReasonPlaceholder}
                            aria-invalid={Boolean(decisionError)}
                            aria-describedby={decisionError ? "po-decision-error" : undefined}
                            className={`mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm ${
                              decisionError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                            }`}
                          />
                        </div>
                        {decisionError ? (
                          <div
                            id="po-decision-error"
                            role="alert"
                            className="flex items-center gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                          >
                            <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            <span>{decisionError}</span>
                          </div>
                        ) : null}
                        <div className="flex flex-wrap gap-2">
                          <button
                            type="submit"
                            disabled={decisionBusy !== null}
                            className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                          >
                            <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{decisionBusy === "APPROVED" ? pt.approveRevisionBusy : pt.approveRevisionButton}</span>
                          </button>
                          <button
                            type="button"
                            disabled={decisionBusy !== null}
                            onClick={() => void handleDecideRevision("REJECTED")}
                            className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                          >
                            <AlertCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{decisionBusy === "REJECTED" ? pt.rejectRevisionBusy : pt.rejectRevisionButton}</span>
                          </button>
                        </div>
                      </form>
                    ) : (
                      <div
                        role="status"
                        className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2.5 text-xs text-[var(--status-warn-text)]"
                      >
                        <Clock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{pt.pendingApprovalNote}</span>
                      </div>
                    )
                  ) : null}

                  {/* Editable Draft Actions */}
                  {canManage && selectedRevision.status === "DRAFT" ? (
                    <div className="space-y-2">
                      {submitError ? (
                        <div
                          id="po-submit-error"
                          role="alert"
                          className="flex items-center gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                        >
                          <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>{submitError}</span>
                        </div>
                      ) : null}
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => loadRevisionIntoEditor(selectedPo, selectedRevision)}
                          className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                        >
                          <span>{pt.editDraftButton}</span>
                        </button>
                        <button
                          type="button"
                          disabled={submitBusy}
                          onClick={() => void handleSubmitExistingDraft(selectedPo.purchaseOrderId, selectedRevision.revisionId)}
                          className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                        >
                          <FileCheck2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                          <span>{submitBusy ? pt.submitRevisionBusy : pt.submitRevisionButton}</span>
                        </button>
                      </div>
                    </div>
                  ) : null}

                  {/* Revision Amendment + Short-Close + Cancel Controls */}
                  {canManage && (selectedPo.status === "APPROVED" || selectedPo.status === "DRAFT") && !hasOpenRevision ? (
                    <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                      <form
                        noValidate
                        onSubmit={(e) => {
                          e.preventDefault();
                          void handleCreateAmendment();
                        }}
                        className="space-y-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-3"
                      >
                        <label htmlFor="po-amendment-reason" className="block text-xs font-semibold text-[var(--text-primary)]">
                          {pt.amendmentHeading}
                        </label>
                        <input
                          id="po-amendment-reason"
                          type="text"
                          value={amendReason}
                          onChange={(e) => setAmendReason(e.target.value)}
                          placeholder={pt.amendmentReasonPlaceholder}
                          aria-label={pt.amendmentReasonLabel}
                          aria-invalid={Boolean(amendError)}
                          aria-describedby={amendError ? "po-amendment-error" : undefined}
                          className={`min-h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-xs ${
                            amendError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                          }`}
                        />
                        {amendError ? (
                          <div id="po-amendment-error" role="alert" className="text-xs text-[var(--status-danger-text)]">
                            {amendError}
                          </div>
                        ) : null}
                        <button
                          type="submit"
                          disabled={amendBusy}
                          className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                        >
                          <GitCompare className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                          <span>{amendBusy ? pt.createAmendmentBusy : pt.createAmendmentButton}</span>
                        </button>
                      </form>

                      {selectedPo.status === "APPROVED" ? (
                        <form
                          noValidate
                          onSubmit={(e) => {
                            e.preventDefault();
                            void handleShortClose();
                          }}
                          className="space-y-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-3"
                        >
                          <label htmlFor="po-close-reason" className="block text-xs font-semibold text-[var(--text-primary)]">
                            {pt.shortCloseHeading}
                          </label>
                          <input
                            id="po-close-reason"
                            type="text"
                            value={closeReason}
                            onChange={(e) => setCloseReason(e.target.value)}
                            placeholder={pt.closeReasonPlaceholder}
                            aria-label={pt.closeReasonLabel}
                            aria-invalid={Boolean(closeError)}
                            aria-describedby={closeError ? "po-close-error" : undefined}
                            className={`min-h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-xs ${
                              closeError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                            }`}
                          />
                          {closeError ? (
                            <div id="po-close-error" role="alert" className="text-xs text-[var(--status-danger-text)]">
                              {closeError}
                            </div>
                          ) : null}
                          <button
                            type="submit"
                            disabled={closeBusy}
                            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 text-xs font-semibold text-[var(--status-warn-text)] disabled:opacity-60"
                          >
                            <Lock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{closeBusy ? pt.shortCloseBusy : pt.shortCloseButton}</span>
                          </button>
                        </form>
                      ) : null}
                    </div>
                  ) : null}

                  {canManage && selectedPo.status !== "CLOSED" && selectedPo.status !== "CANCELLED" ? (
                    Number(selectedPo.downstreamReferenceCount) > 0 || Number(selectedPo.acceptedBaseQuantityTotal) > 0 ? (
                      <p className="text-xs text-[var(--text-secondary)]">{pt.cancelBlockedNote}</p>
                    ) : (
                      <form
                        noValidate
                        onSubmit={(e) => {
                          e.preventDefault();
                          void handleCancelPo();
                        }}
                        className="space-y-2 border-t border-[var(--border-hairline)] pt-3"
                      >
                        <h3 className="text-xs font-semibold text-[var(--text-primary)]">{pt.cancelPoHeading}</h3>
                        <div className="flex flex-wrap items-end gap-2">
                          <div className="min-w-[220px] flex-1">
                            <label htmlFor="po-cancel-reason" className="block text-xs font-medium text-[var(--text-secondary)]">
                              {pt.cancelReasonLabel}
                            </label>
                            <input
                              id="po-cancel-reason"
                              type="text"
                              value={cancelReason}
                              onChange={(e) => setCancelReason(e.target.value)}
                              placeholder={pt.cancelReasonPlaceholder}
                              aria-invalid={Boolean(cancelError)}
                              aria-describedby={cancelError ? "po-cancel-error" : undefined}
                              className={`mt-1 min-h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-xs ${
                                cancelError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                              }`}
                            />
                          </div>
                          <button
                            type="submit"
                            disabled={cancelBusy}
                            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                          >
                            <Ban className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{cancelBusy ? pt.cancelPoBusy : pt.cancelPoButton}</span>
                          </button>
                        </div>
                        {cancelError ? (
                          <div id="po-cancel-error" role="alert" className="w-full text-xs text-[var(--status-danger-text)]">
                            {cancelError}
                          </div>
                        ) : null}
                      </form>
                    )
                  ) : null}

                  {selectedPo.status === "CLOSED" || selectedPo.status === "CANCELLED" ? (
                    <p className="rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3 py-2 text-xs text-[var(--text-secondary)]">
                      {pt.terminalPoControlNote}
                    </p>
                  ) : null}
                </div>
              ) : null}
            </section>
          ) : null}
        </div>
      ) : canManage ? (
        <div role="tabpanel" aria-labelledby="tab-po-editor" className="space-y-6">
          <form
            noValidate
            onSubmit={(e) => {
              e.preventDefault();
              void handleSaveOrSubmitEditor(true);
            }}
            className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
          >
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">
                {editingPoId
                  ? `${pt.editorEditDraftTitle} · ${editingPoNumber ?? ""}`
                  : pt.editorCreateTitle}
              </h2>
              <button
                type="button"
                onClick={() => {
                  resetEditorForNewDraft();
                  setActiveTab("list");
                }}
                className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
              >
                {pt.cancelEditorButton}
              </button>
            </div>

            <div className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-12">
              <div className="sm:col-span-6">
                <label htmlFor="po-supplier" className="block text-xs font-medium text-[var(--text-secondary)]">
                  {pt.supplierLabel}
                </label>
                <select
                  id="po-supplier"
                  value={supplierId}
                  onChange={(e) => {
                    const nextId = e.target.value;
                    setSupplierId(nextId);
                    const sup = refData?.suppliers.find((s) => s.partnerId === nextId);
                    if (sup && !editingPoId) {
                      setTermsDays(String(sup.defaultPaymentTermsDays ?? 30));
                    }
                  }}
                  aria-invalid={editorError?.field === "supplier"}
                  aria-describedby={editorError?.field === "supplier" ? "po-editor-error" : undefined}
                  className={`mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm ${
                    editorError?.field === "supplier" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                  }`}
                >
                  <option value="">{pt.selectSupplierOption}</option>
                  {(refData?.suppliers ?? []).map((sup) => (
                    <option key={sup.partnerId} value={sup.partnerId}>
                      {sup.partnerCode} — {sup.legalName}
                    </option>
                  ))}
                </select>
              </div>

              <div className="sm:col-span-3">
                <label htmlFor="po-order-date" className="block text-xs font-medium text-[var(--text-secondary)]">
                  {pt.orderDateLabel}
                </label>
                <input
                  id="po-order-date"
                  type="date"
                  value={orderDate}
                  onChange={(e) => setOrderDate(e.target.value)}
                  aria-invalid={editorError?.field === "orderDate"}
                  aria-describedby={editorError?.field === "orderDate" ? "po-editor-error" : undefined}
                  className={`font-code mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm ${
                    editorError?.field === "orderDate" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                  }`}
                />
              </div>

              <div className="sm:col-span-3">
                <label htmlFor="po-expected-date" className="block text-xs font-medium text-[var(--text-secondary)]">
                  {pt.expectedDateLabel}
                </label>
                <input
                  id="po-expected-date"
                  type="date"
                  value={expectedDate}
                  onChange={(e) => setExpectedDate(e.target.value)}
                  aria-invalid={editorError?.field === "expectedDate"}
                  aria-describedby={editorError?.field === "expectedDate" ? "po-editor-error" : undefined}
                  className={`font-code mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm ${
                    editorError?.field === "expectedDate" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                  }`}
                />
              </div>

              <div className="sm:col-span-5">
                <label htmlFor="po-destination" className="block text-xs font-medium text-[var(--text-secondary)]">
                  {pt.destinationInputLabel}
                </label>
                <input
                  id="po-destination"
                  type="text"
                  value={destination}
                  onChange={(e) => setDestination(e.target.value)}
                  placeholder={pt.destinationPlaceholder}
                  aria-invalid={editorError?.field === "destination"}
                  aria-describedby={editorError?.field === "destination" ? "po-editor-error" : undefined}
                  className={`mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm ${
                    editorError?.field === "destination" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                  }`}
                />
              </div>

              <div className="sm:col-span-3">
                <label htmlFor="po-terms" className="block whitespace-nowrap text-xs font-medium text-[var(--text-secondary)]">
                  {pt.termsInputLabel}
                </label>
                <input
                  id="po-terms"
                  type="number"
                  min={0}
                  max={365}
                  value={termsDays}
                  onChange={(e) => setTermsDays(e.target.value)}
                  aria-invalid={editorError?.field === "terms"}
                  aria-describedby={editorError?.field === "terms" ? "po-editor-error" : undefined}
                  className={`font-code mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-right text-sm tabular-nums ${
                    editorError?.field === "terms" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                  }`}
                />
              </div>

              <div className="sm:col-span-4">
                <label htmlFor="po-tolerance" className="block text-xs font-medium text-[var(--text-secondary)]">
                  {pt.toleranceSelectLabel}
                </label>
                <select
                  id="po-tolerance"
                  value={toleranceId}
                  onChange={(e) => setToleranceId(e.target.value)}
                  className="mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm"
                >
                  {(refData?.tolerancePolicies ?? []).map((pol) => (
                    <option key={pol.policyId} value={pol.policyId}>
                      {pol.code} — {pol.name}
                    </option>
                  ))}
                </select>
              </div>

              {editingPoId ? (
                <div className="sm:col-span-6">
                  <label htmlFor="po-change-reason" className="block text-xs font-medium text-[var(--text-secondary)]">
                    {pt.editorChangeReasonLabel}
                  </label>
                  <input
                    id="po-change-reason"
                    type="text"
                    value={changeReason}
                    onChange={(e) => setChangeReason(e.target.value)}
                    placeholder={pt.editorChangeReasonPlaceholder}
                    className="mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm"
                  />
                </div>
              ) : null}

              <div className={editingPoId ? "sm:col-span-6" : "sm:col-span-12"}>
                <label htmlFor="po-notes" className="block text-xs font-medium text-[var(--text-secondary)]">
                  {pt.editorNotesLabel}
                </label>
                <input
                  id="po-notes"
                  type="text"
                  value={editorNotes}
                  onChange={(e) => setEditorNotes(e.target.value)}
                  placeholder={pt.editorNotesPlaceholder}
                  className="mt-1 min-h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm"
                />
              </div>
            </div>

            <div className="space-y-3 border-t border-[var(--border-hairline)] p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-xs font-semibold text-[var(--text-primary)]">{pt.linesSectionHeading}</h3>
                <button
                  type="button"
                  onClick={() => {
                    const firstItem = refData?.items[0];
                    setDraftLines((prev) => [
                      ...prev,
                      {
                        key: `po-line-${Date.now()}-${prev.length + 1}`,
                        lineageLineKey: null,
                        itemId: firstItem?.itemId ?? "",
                        orderedUomId: firstItem?.defaultPurchaseUomId ?? firstItem?.baseUomId ?? "",
                        orderedQuantity: "1",
                        unitPrice: "100000",
                        taxCodeId: "",
                        lineDescription: "",
                      },
                    ]);
                  }}
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                >
                  <Plus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  <span>{pt.addLineButton}</span>
                </button>
              </div>

              {draftLines.map((line, idx) => {
                const selectedItem = itemById.get(line.itemId);
                const uoms = selectedItem?.allowedUoms ?? [];
                const preview = editorTotals.linePreviews[idx];

                return (
                  <div
                    key={line.key}
                    className="grid grid-cols-1 gap-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-3 sm:grid-cols-12"
                  >
                    <div className="sm:col-span-4">
                      <label htmlFor={`po-line-item-${idx}`} className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.lineItemLabel} #{idx + 1}
                      </label>
                      <select
                        id={`po-line-item-${idx}`}
                        value={line.itemId}
                        onChange={(e) => {
                          const nextItemId = e.target.value;
                          const item = itemById.get(nextItemId);
                          const defaultUom = item?.defaultPurchaseUomId || item?.baseUomId || "";
                          setDraftLines((prev) =>
                            prev.map((row, rIdx) =>
                              rIdx === idx ? { ...row, itemId: nextItemId, orderedUomId: defaultUom } : row,
                            ),
                          );
                        }}
                        className="mt-1 min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs"
                      >
                        <option value="">{pt.selectItemOption}</option>
                        {(refData?.items ?? []).map((item) => (
                          <option key={item.itemId} value={item.itemId}>
                            {item.itemCode} — {item.name}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="sm:col-span-2">
                      <label htmlFor={`po-line-uom-${idx}`} className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.lineUomLabel}
                      </label>
                      <select
                        id={`po-line-uom-${idx}`}
                        value={line.orderedUomId}
                        onChange={(e) => {
                          const nextUom = e.target.value;
                          setDraftLines((prev) =>
                            prev.map((row, rIdx) => (rIdx === idx ? { ...row, orderedUomId: nextUom } : row)),
                          );
                        }}
                        className="font-code mt-1 min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs"
                      >
                        {uoms.map((u) => (
                          <option key={u.uomId} value={u.uomId}>
                            {u.uomCode} (x{u.factorToBase})
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="sm:col-span-2">
                      <label htmlFor={`po-line-qty-${idx}`} className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.lineQtyLabel}
                      </label>
                      <input
                        id={`po-line-qty-${idx}`}
                        type="text"
                        inputMode="decimal"
                        value={line.orderedQuantity}
                        onChange={(e) => {
                          const val = e.target.value;
                          setDraftLines((prev) =>
                            prev.map((row, rIdx) => (rIdx === idx ? { ...row, orderedQuantity: val } : row)),
                          );
                        }}
                        className="font-code mt-1 min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums"
                      />
                    </div>

                    <div className="sm:col-span-2">
                      <label htmlFor={`po-line-price-${idx}`} className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.linePriceLabel}
                      </label>
                      <input
                        id={`po-line-price-${idx}`}
                        type="text"
                        inputMode="decimal"
                        value={line.unitPrice}
                        onChange={(e) => {
                          const val = e.target.value;
                          setDraftLines((prev) =>
                            prev.map((row, rIdx) => (rIdx === idx ? { ...row, unitPrice: val } : row)),
                          );
                        }}
                        className="font-code mt-1 min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums"
                      />
                    </div>

                    <div className="sm:col-span-2">
                      <label htmlFor={`po-line-tax-${idx}`} className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.lineTaxLabel}
                      </label>
                      <select
                        id={`po-line-tax-${idx}`}
                        value={line.taxCodeId}
                        onChange={(e) => {
                          const nextTax = e.target.value;
                          setDraftLines((prev) =>
                            prev.map((row, rIdx) => (rIdx === idx ? { ...row, taxCodeId: nextTax } : row)),
                          );
                        }}
                        className="font-code mt-1 min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs"
                      >
                        <option value="">{pt.noTaxOption}</option>
                        {(refData?.taxCodes ?? []).map((tc) => (
                          <option key={tc.taxCodeId} value={tc.taxCodeId}>
                            {tc.code}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="sm:col-span-9">
                      <input
                        id={`po-line-desc-${idx}`}
                        type="text"
                        value={line.lineDescription}
                        onChange={(e) => {
                          const val = e.target.value;
                          setDraftLines((prev) =>
                            prev.map((row, rIdx) => (rIdx === idx ? { ...row, lineDescription: val } : row)),
                          );
                        }}
                        placeholder={pt.lineDescPlaceholder}
                        aria-label={`${pt.lineDescLabel} #${idx + 1}`}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs"
                      />
                    </div>

                    <div className="flex flex-wrap items-center justify-between gap-2 text-xs sm:col-span-3 sm:justify-end">
                      <span className="font-code tabular-nums text-[var(--text-secondary)]">
                        {formatScaled6Qty(preview?.baseQtyScaled6 ?? 0n, locale)} {preview?.baseUomCode ?? ""} ·{" "}
                        {formatMoney(preview?.subtotalIdr ?? 0n, locale)}
                      </span>
                      {draftLines.length > 1 ? (
                        <button
                          type="button"
                          onClick={() => setDraftLines((prev) => prev.filter((_, rIdx) => rIdx !== idx))}
                          aria-label={`${pt.removeLineButton} #${idx + 1}`}
                          className="inline-flex h-9 w-9 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)]"
                        >
                          <Trash2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                        </button>
                      ) : null}
                    </div>
                  </div>
                );
              })}

              <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3 py-2.5 text-xs">
                <span className="text-[var(--text-secondary)]">{pt.od01NoticeLine}</span>
                <div className="flex flex-wrap gap-4" data-testid="po-editor-totals">
                  <span>
                    {pt.subtotalLabel}{" "}
                    <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{formatMoney(editorTotals.subtotalIdr, locale)}</span>
                  </span>
                  <span>
                    {pt.ppnTotalLabel}{" "}
                    <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{formatMoney(editorTotals.ppnIdr, locale)}</span>
                  </span>
                  {editorTotals.whtIdr > 0n ? (
                    <span>
                      {pt.whtTotalLabel}{" "}
                      <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">{formatMoney(editorTotals.whtIdr, locale)}</span>
                    </span>
                  ) : null}
                  <span>
                    {pt.grandTotalLabel}{" "}
                    <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                      {formatMoney(editorTotals.grandTotalIdr, locale)}
                    </span>
                  </span>
                </div>
              </div>

              {editorError ? (
                <div
                  id="po-editor-error"
                  role="alert"
                  className="flex items-center gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                >
                  <AlertCircle className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{editorError.message}</span>
                </div>
              ) : null}

              <div className="flex flex-wrap items-center gap-2 pt-1">
                <button
                  type="submit"
                  disabled={editorBusy}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                >
                  <FileCheck2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                  <span>{editorBusy ? pt.editorBusy : pt.saveAndSubmitButton}</span>
                </button>
                <button
                  type="button"
                  disabled={editorBusy}
                  onClick={() => void handleSaveOrSubmitEditor(false)}
                  className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                >
                  <span>{pt.saveDraftOnlyButton}</span>
                </button>
              </div>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  );
}

export default function ProcurementPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <ProcurementSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
