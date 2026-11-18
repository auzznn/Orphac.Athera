"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
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
  ShieldCheck,
  Trash2,
} from "lucide-react";
import {
  ApiProblemError,
  createSalesOrder,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchSalesOrderDetail,
  fetchSalesOrders,
  fetchSalesOrdersReferenceData,
  SalesOrderDetailDto,
  SalesOrderListResponse,
  SalesOrderRevisionDto,
  SalesOrdersReferenceDataResponse,
  updateSalesOrderRevision,
} from "@/lib/api/client";
import { useSessionUser } from "@/components/AppShell";
import { SalesOrderDetailSheet, SalesStatusBadge } from "@/components/sales/SalesOrderDetailSheet";
import { formatIdr, formatJakartaDateTime, todayJakartaDate } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";

type SalesTab = "orders" | "editor" | "queue";

interface DraftLineRow {
  lineageLineKey: string | null;
  itemId: string;
  orderedUomId: string;
  quantity: string;
  unitPrice: string;
  taxCodeId: string;
}

function emptyDraftLine(ref: SalesOrdersReferenceDataResponse | null): DraftLineRow {
  const firstItem = ref?.items[0];
  const defaultUom =
    firstItem?.allowedUoms.find((u) => u.uomId === firstItem.defaultSalesUomId) ??
    firstItem?.allowedUoms[0];
  const firstTax = ref?.outputTaxCodes[0];
  return {
    lineageLineKey: null,
    itemId: firstItem?.itemId ?? "",
    orderedUomId: defaultUom?.uomId ?? "",
    quantity: "",
    unitPrice: "",
    taxCodeId: firstTax?.taxCodeId ?? "",
  };
}

export default function SalesOrdersPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <SalesOrdersSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}

function SalesOrdersSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const user = useSessionUser();
  const { locale, t } = useLocale();
  const st = t.salesOrders;

  const [workspace, setWorkspace] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [refData, setRefData] = useState<SalesOrdersReferenceDataResponse | null>(null);
  const [ordersData, setOrdersData] = useState<SalesOrderListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const [activeTab, setActiveTab] = useState<SalesTab>("orders");
  const [policyOpen, setPolicyOpen] = useState(false);
  const [selectedDetail, setSelectedDetail] = useState<SalesOrderDetailDto | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [statusFilter, setStatusFilter] = useState("");
  const [searchFilter, setSearchFilter] = useState("");

  // Editor state
  const [editingOrderId, setEditingOrderId] = useState<string | null>(null);
  const [editingRevisionId, setEditingRevisionId] = useState<string | null>(null);
  const [editingRevNumber, setEditingRevNumber] = useState<number>(1);
  const [customerId, setCustomerId] = useState("");
  const [orderDate, setOrderDate] = useState(todayJakartaDate);
  const [requestedDate, setRequestedDate] = useState("");
  const [termsDays, setTermsDays] = useState("14");
  const [customerRef, setCustomerRef] = useState("");
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [changeReason, setChangeReason] = useState("");
  const [notes, setNotes] = useState("");
  const [draftLines, setDraftLines] = useState<DraftLineRow[]>([]);
  const [savingDraft, setSavingDraft] = useState(false);

  const mapError = useCallback(
    (err: unknown): string => {
      const problem = err as Partial<ApiProblemError>;
      if (problem && typeof problem === "object" && (typeof problem.errorCode === "string" || typeof problem.detail === "string")) {
        const mapped = problem.errorCode ? st.errors[problem.errorCode] : undefined;
        if (mapped) return mapped;
        return problem.detail || problem.title || t.login.errors.upstream_unavailable;
      }
      return t.login.errors.upstream_unavailable;
    },
    [st.errors, t.login.errors.upstream_unavailable],
  );

  const applyDefaults = useCallback(
    (ws: EntityWorkspaceDetailResponse, rf: SalesOrdersReferenceDataResponse, list: SalesOrderListResponse) => {
      setWorkspace(ws);
      setRefData(rf);
      setOrdersData(list);
      if (rf.customers.length > 0) {
        setCustomerId((prev) => {
          if (prev) return prev;
          setTermsDays(String(rf.customers[0].defaultPaymentTermsDays));
          return rf.customers[0].partnerId;
        });
      }
      setDraftLines((prev) => (prev.length > 0 ? prev : [emptyDraftLine(rf)]));
    },
    [],
  );

  const loadSnapshot = useCallback(async () => {
    const [ws, rf, list] = await Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchSalesOrdersReferenceData(tenantId, entityId),
      fetchSalesOrders(tenantId, entityId, {
        status: statusFilter || undefined,
        search: searchFilter.trim() || undefined,
      }),
    ]);
    return { ws, rf, list };
  }, [tenantId, entityId, statusFilter, searchFilter]);

  const loadData = useCallback(
    async (silent = false) => {
      if (!tenantId || !entityId) return;
      if (silent) setRefreshing(true);
      else setLoading(true);
      setLoadError(null);
      setAccessDenied(false);
      try {
        const { ws, rf, list } = await loadSnapshot();
        applyDefaults(ws, rf, list);
      } catch (err) {
        const problem = err as Partial<ApiProblemError>;
        if (typeof problem.status === "number") {
          if (problem.status === 401) {
            router.replace("/login");
            return;
          }
          if (problem.status === 403 || problem.status === 404) {
            setAccessDenied(true);
            return;
          }
          setLoadError(problem.detail || problem.title || t.login.errors.upstream_unavailable);
        } else {
          setLoadError(t.login.errors.upstream_unavailable);
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [tenantId, entityId, router, loadSnapshot, applyDefaults, t.login.errors.upstream_unavailable],
  );

  useEffect(() => {
    if (!tenantId || !entityId) return;
    let active = true;
    loadSnapshot()
      .then(({ ws, rf, list }) => {
        if (!active) return;
        applyDefaults(ws, rf, list);
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
        if (problem.status === 403 || problem.status === 404) {
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
  }, [tenantId, entityId, loadSnapshot, applyDefaults, router, t.login.errors.upstream_unavailable]);

  const perms = useMemo(() => new Set(workspace?.effectivePermissions ?? []), [workspace]);
  const canManage = perms.has("sales.manage");
  const canApprove = perms.has("sales.approve");
  const isReadOnly = !canManage && !canApprove;

  const orders = useMemo(() => ordersData?.items ?? [], [ordersData]);
  const queueOrders = useMemo(
    () => orders.filter((o) => Boolean(o.pendingApprovalRevisionId)),
    [orders],
  );

  const kpis = useMemo(() => {
    const approvedOrders = orders.filter((o) => o.status === "APPROVED");
    const approvedTotalValue = approvedOrders.reduce((s, o) => s + Number(o.currentGrandTotalAmount || 0), 0);
    const totalOrderedBaseQty = approvedOrders.reduce((s, o) => s + Number(o.currentOrderedBaseQuantity || 0), 0);
    return {
      approvedTotalValue,
      pendingCount: queueOrders.length,
      approvedCount: approvedOrders.length,
      totalOrderedBaseQty,
    };
  }, [orders, queueOrders]);

  const itemMap = useMemo(() => {
    const m = new Map<string, SalesOrdersReferenceDataResponse["items"][number]>();
    for (const i of refData?.items ?? []) m.set(i.itemId, i);
    return m;
  }, [refData]);

  const taxMap = useMemo(() => {
    const m = new Map<string, SalesOrdersReferenceDataResponse["outputTaxCodes"][number]>();
    for (const tx of refData?.outputTaxCodes ?? []) m.set(tx.taxCodeId, tx);
    return m;
  }, [refData]);

  const previewTotals = useMemo(() => {
    let subtotal = 0;
    let taxTotal = 0;
    let totalBaseQty = 0;
    for (const row of draftLines) {
      const qty = Number(row.quantity);
      const price = Number(row.unitPrice);
      const item = itemMap.get(row.itemId);
      const uom = item?.allowedUoms.find((u) => u.uomId === row.orderedUomId);
      const factor = Number(uom?.factorToBase ?? 1);
      if (Number.isFinite(qty) && qty > 0) {
        totalBaseQty += qty * factor;
        if (Number.isFinite(price) && price > 0) {
          const lineSub = Math.round(qty * price);
          subtotal += lineSub;
          const tax = row.taxCodeId ? taxMap.get(row.taxCodeId) : undefined;
          if (tax) {
            const dpp = Math.round(
              (lineSub * Number(tax.taxableBaseRatioNumerator)) / Number(tax.taxableBaseRatioDenominator),
            );
            taxTotal += Math.round((dpp * Number(tax.nominalRatePercent)) / 100);
          }
        }
      }
    }
    return { subtotal, taxTotal, grandTotal: subtotal + taxTotal, totalBaseQty };
  }, [draftLines, itemMap, taxMap]);

  const openOrderDetail = async (salesOrderId: string) => {
    setActionError(null);
    try {
      const d = await fetchSalesOrderDetail(tenantId, entityId, salesOrderId);
      setSelectedDetail(d);
    } catch (err) {
      setActionError(mapError(err));
    }
  };

  const handleDetailUpdated = async (updated: SalesOrderDetailDto) => {
    setSelectedDetail(updated);
    await loadData(true);
  };

  const resetEditor = useCallback(() => {
    setEditingOrderId(null);
    setEditingRevisionId(null);
    setEditingRevNumber(1);
    setOrderDate(todayJakartaDate);
    setRequestedDate("");
    setCustomerRef("");
    setDeliveryAddress("");
    setChangeReason("");
    setNotes("");
    if (refData?.customers[0]) {
      setCustomerId(refData.customers[0].partnerId);
      setTermsDays(String(refData.customers[0].defaultPaymentTermsDays));
    }
    setDraftLines([emptyDraftLine(refData)]);
  }, [refData]);

  const beginEditDraft = (detail: SalesOrderDetailDto, rev: SalesOrderRevisionDto) => {
    setEditingOrderId(detail.salesOrderId);
    setEditingRevisionId(rev.revisionId);
    setEditingRevNumber(Number(rev.revisionNumber));
    setCustomerId(rev.customerPartnerId);
    setOrderDate(rev.orderDate);
    setRequestedDate(rev.requestedDeliveryDate ?? "");
    setTermsDays(String(rev.paymentTermsDays));
    setCustomerRef(rev.customerReference ?? "");
    setDeliveryAddress(rev.deliveryAddressSnapshot);
    setChangeReason(rev.changeReason ?? "");
    setNotes(rev.notes ?? "");
    setDraftLines(
      rev.lines.map((l) => ({
        lineageLineKey: l.lineageLineKey,
        itemId: l.itemId,
        orderedUomId: l.orderedUomId,
        quantity: String(l.orderedQuantity),
        unitPrice: String(l.unitPrice),
        taxCodeId: l.taxCodeId ?? "",
      })),
    );
    setActiveTab("editor");
  };

  const handleSaveDraft = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!customerId || !deliveryAddress.trim()) return;
    const mappedLines = draftLines.map((row) => ({
      lineageLineKey: row.lineageLineKey,
      itemId: row.itemId,
      orderedUomId: row.orderedUomId,
      orderedQuantity: Number(row.quantity),
      unitPrice: Number(row.unitPrice),
      discountAmount: 0,
      taxCodeId: row.taxCodeId || null,
      requestedDeliveryDate: requestedDate || null,
      lineDescription: null,
    }));

    setSavingDraft(true);
    setActionError(null);
    try {
      const saved =
        editingOrderId && editingRevisionId
          ? await updateSalesOrderRevision(tenantId, entityId, editingOrderId, editingRevisionId, {
              customerPartnerId: customerId,
              orderDate,
              requestedDeliveryDate: requestedDate || null,
              deliveryAddressSnapshot: deliveryAddress.trim(),
              paymentTermsDays: Number(termsDays || 0),
              customerReference: customerRef.trim() || null,
              creditLimitOverrideRequested: false,
              changeReason: changeReason.trim() || null,
              notes: notes.trim() || null,
              lines: mappedLines,
            })
          : await createSalesOrder(tenantId, entityId, {
              customerPartnerId: customerId,
              currencyCode: workspace?.entity.functionalCurrencyCode ?? "IDR",
              orderDate,
              requestedDeliveryDate: requestedDate || null,
              deliveryAddressSnapshot: deliveryAddress.trim(),
              paymentTermsDays: Number(termsDays || 0),
              customerReference: customerRef.trim() || null,
              creditLimitOverrideRequested: false,
              notes: notes.trim() || null,
              lines: mappedLines,
            });

      resetEditor();
      setSelectedDetail(saved);
      setActiveTab("orders");
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setSavingDraft(false);
    }
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-5xl space-y-4">
        <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
          <div className="h-5 w-64 animate-pulse rounded bg-[var(--bg-chrome)]" />
          <div className="mt-3 h-4 w-96 animate-pulse rounded bg-[var(--bg-subtle)]" />
        </div>
      </div>
    );
  }

  if (accessDenied) {
    return (
      <div className="mx-auto max-w-3xl">
        <div
          role="alert"
          className="rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
        >
          <div className="flex items-start gap-2.5">
            <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-[var(--status-danger-text)]" strokeWidth={1.75} aria-hidden="true" />
            <div>
              <h1 className="text-base font-semibold text-[var(--text-primary)]">{t.entitySheet.forbiddenTitle}</h1>
              <p className="mt-1 text-sm text-[var(--text-secondary)]">{t.entitySheet.forbiddenBody}</p>
              <Link
                href={`/w/${tenantId}/${entityId}`}
                className="mt-4 inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
              >
                <ArrowLeft className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{st.backToOverview}</span>
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (loadError || !workspace || !refData || !ordersData) {
    return (
      <div className="mx-auto max-w-3xl">
        <div
          role="alert"
          className="rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
        >
          <h1 className="text-base font-semibold text-[var(--text-primary)]">{st.pageTitle}</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">{loadError ?? t.login.errors.upstream_unavailable}</p>
          <button
            type="button"
            onClick={() => loadData(false)}
            className="mt-4 inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
          >
            <RefreshCw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            <span>{st.refreshButton}</span>
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      {/* Header */}
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">{st.pageTitle}</h1>
          <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">{st.lead}</p>
          <div className="mt-2 font-code text-xs tabular-nums text-[var(--text-muted)]">
            {workspace.tenant.code} · {workspace.entity.code} ({workspace.entity.functionalCurrencyCode}) ·{" "}
            {formatJakartaDateTime(workspace.serverTimestampUtc, locale)}
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{st.backToOverview}</span>
          </Link>
          <button
            type="button"
            id="so-refresh-btn"
            onClick={() => loadData(true)}
            disabled={refreshing}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`} strokeWidth={1.75} aria-hidden="true" />
            <span>{st.refreshButton}</span>
          </button>
        </div>
      </header>

      {/* Read-only banner */}
      {isReadOnly ? (
        <div
          id="so-readonly-banner"
          role="status"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
        >
          <Eye className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{st.readOnlyBanner}</span>
        </div>
      ) : null}

      {/* OD-09 & OD-02 Governance Strip */}
      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex flex-wrap items-center justify-between gap-2 bg-[var(--status-warn-bg)] px-4 py-2.5 text-xs text-[var(--status-warn-text)] md:flex-nowrap">
          <div className="flex items-start gap-2">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{st.od09PolicyBanner}</span>
          </div>
          <button
            type="button"
            id="so-policy-toggle"
            aria-expanded={policyOpen}
            onClick={() => setPolicyOpen((v) => !v)}
            className="inline-flex min-h-9 shrink-0 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <span>{policyOpen ? st.policyToggleHide : st.policyToggleShow}</span>
            {policyOpen ? (
              <ChevronUp className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            ) : (
              <ChevronDown className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            )}
          </button>
        </div>

        {policyOpen ? (
          <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)] md:grid-cols-4 md:divide-x md:divide-y-0">
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{st.policyCards.rule1Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{st.policyCards.rule1Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{st.policyCards.rule2Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{st.policyCards.rule2Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{st.policyCards.rule3Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{st.policyCards.rule3Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{st.policyCards.rule4Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{st.policyCards.rule4Body}</p>
            </div>
          </div>
        ) : null}
      </section>

      {/* 4-Metric Hairline KPI Strip */}
      <section
        aria-label={st.statsAriaLabel}
        className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] md:grid-cols-4 md:divide-x md:divide-y-0"
      >
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{st.stats.approvedTotalValue}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(kpis.approvedTotalValue)}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{st.stats.pendingApprovalCount}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {kpis.pendingCount}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{st.stats.approvedCount}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {kpis.approvedCount}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{st.stats.totalOrderedBaseQty}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {kpis.totalOrderedBaseQty}
          </div>
        </div>
      </section>

      {actionError ? (
        <div
          id="so-action-error"
          role="alert"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-2.5 text-xs text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{actionError}</span>
        </div>
      ) : null}

      {/* Detail Sheet */}
      {selectedDetail ? (
        <SalesOrderDetailSheet
          key={selectedDetail.salesOrderId}
          tenantId={tenantId}
          entityId={entityId}
          detail={selectedDetail}
          currentUserId={user.userId}
          canManage={canManage}
          canApprove={canApprove}
          onClose={() => setSelectedDetail(null)}
          onUpdated={handleDetailUpdated}
          onEditDraft={beginEditDraft}
          mapError={mapError}
        />
      ) : null}

      {/* Tabs & Primary CTA */}
      <div className="space-y-2">
        <div role="tablist" aria-label={st.tabListLabel} className="flex flex-wrap items-center gap-1 border-b border-[var(--border-hairline)]">
          <button
            type="button"
            role="tab"
            id="so-tab-orders"
            aria-selected={activeTab === "orders"}
            aria-controls="so-panel-orders"
            onClick={() => setActiveTab("orders")}
            className={`min-h-10 rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
              activeTab === "orders"
                ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {st.tabs.orders}
          </button>
          {canManage ? (
          <button
            type="button"
            role="tab"
            id="so-tab-editor"
            aria-selected={activeTab === "editor"}
            aria-controls="so-panel-editor"
            onClick={() => setActiveTab("editor")}
            className={`min-h-10 rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
              activeTab === "editor"
                ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {st.tabs.editor}
          </button>
          ) : null}
          <button
            type="button"
            role="tab"
            id="so-tab-queue"
            aria-selected={activeTab === "queue"}
            aria-controls="so-panel-queue"
            onClick={() => setActiveTab("queue")}
            className={`min-h-10 rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
              activeTab === "queue"
                ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {st.tabs.queue} ({queueOrders.length})
          </button>
        </div>

        {canManage && activeTab === "orders" ? (
          <div className="flex justify-end">
            <button
              type="button"
              id="so-new-draft-btn"
              onClick={() => {
                resetEditor();
                setActiveTab("editor");
              }}
              className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <Plus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>{st.composer.newDraftCta}</span>
            </button>
          </div>
        ) : null}
      </div>

      {/* TAB 1: ORDERS LEDGER */}
      {activeTab === "orders" ? (
        <div id="so-panel-orders" role="tabpanel" aria-labelledby="so-tab-orders" className="space-y-4">
          <div className="flex flex-wrap items-center gap-3 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3">
            <input
              id="so-search-input"
              type="search"
              value={searchFilter}
              placeholder={st.tables.searchPlaceholder}
              onChange={(e) => setSearchFilter(e.target.value)}
              className="min-h-9 min-w-[220px] flex-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs text-[var(--text-primary)]"
            />
            <select
              id="so-status-filter"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs text-[var(--text-primary)]"
            >
              <option value="">{st.tables.statusFilterAll}</option>
              <option value="DRAFT">{st.statusLabels.DRAFT}</option>
              <option value="PENDING_APPROVAL">{st.statusLabels.PENDING_APPROVAL}</option>
              <option value="APPROVED">{st.statusLabels.APPROVED}</option>
              <option value="CLOSED">{st.statusLabels.CLOSED}</option>
              <option value="CANCELLED">{st.statusLabels.CANCELLED}</option>
            </select>
          </div>

          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {st.tables.ordersTitle}
            </div>
            {orders.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{st.tables.emptyOrders}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="so-orders-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{st.tables.colOrderDoc}</th>
                      <th scope="col" className="py-2 md:px-2">{st.tables.colCustomerRef}</th>
                      <th scope="col" className="py-2 md:px-2">{st.tables.colDatesTerms}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{st.tables.colRevisionAmount}</th>
                      <th scope="col" className="py-2 md:px-2">{st.tables.colStatus}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{st.tables.colAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {orders.map((so) => (
                      <tr key={so.salesOrderId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colOrderDoc}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {so.soNumber ?? so.draftCode}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {so.orderDate}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colCustomerRef}</span>
                          <div className="font-medium text-[var(--text-primary)]">
                            <span className="font-code">{so.customerCode}</span> · {so.customerName}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">{so.deliveryAddressSnapshot}</div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colDatesTerms}</span>
                          <div className="font-code tabular-nums text-[var(--text-primary)]">
                            {so.requestedDeliveryDate ?? "—"}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span className="font-code tabular-nums">{so.paymentTermsDays}</span> {st.detail.termsSuffix}
                            {so.customerReference ? (
                              <span>
                                {" · "}
                                <span className="font-code">{so.customerReference}</span>
                              </span>
                            ) : null}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colRevisionAmount}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(so.currentGrandTotalAmount)}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span>
                              {st.tables.revPrefix}{" "}
                              <span className="font-code tabular-nums">
                                {Number(so.currentApprovedRevisionNumber) > 0
                                  ? so.currentApprovedRevisionNumber
                                  : so.latestRevisionNumber}
                              </span>{" "}
                              · {st.tables.baseQtyShort}{" "}
                              <span className="font-code tabular-nums">{so.currentOrderedBaseQuantity}</span>
                            </span>
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colStatus}</span>
                          <div className="flex flex-wrap items-center gap-1.5">
                            <SalesStatusBadge status={so.status} label={st.statusLabels[so.status] ?? so.status} />
                            {so.hasOpenRevision && so.status === "APPROVED" ? (
                              <span className="rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--status-warn-text)]">
                                {st.tables.revPrefix} {so.latestRevisionNumber} · {st.tables.openRevTag}
                              </span>
                            ) : null}
                          </div>
                          <div className="mt-0.5 text-[11px] text-[var(--text-secondary)]">
                            {st.fulfillmentLabels[so.fulfillmentProgress] ?? so.fulfillmentProgress}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colAction}</span>
                          <button
                            type="button"
                            data-testid={`open-so-${so.soNumber ?? so.draftCode}`}
                            onClick={() => openOrderDetail(so.salesOrderId)}
                            className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                          >
                            <Eye className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{st.tables.detailCta}</span>
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}

      {/* TAB 2: ORDER & REVISION EDITOR */}
      {activeTab === "editor" ? (
        <div id="so-panel-editor" role="tabpanel" aria-labelledby="so-tab-editor">
          {!canManage ? (
            <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6 text-xs text-[var(--text-secondary)]">
              {st.readOnlyBanner}
            </div>
          ) : refData.customers.length === 0 || refData.items.length === 0 ? (
            <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6 text-xs text-[var(--text-secondary)]">
              {st.composer.noEligibleMaster}
            </div>
          ) : (
            <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
              <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
                {editingOrderId
                  ? editingRevNumber > 1
                    ? `${st.composer.titleEditAmendment} (${st.tables.revPrefix} ${editingRevNumber})`
                    : st.composer.titleEditDraft
                  : st.composer.titleCreate}
              </div>

              <form onSubmit={handleSaveDraft} className="space-y-4 p-4">
                <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.customerLabel}</span>
                    <select
                      id="so-ed-customer"
                      value={customerId}
                      onChange={(e) => {
                        const cid = e.target.value;
                        setCustomerId(cid);
                        const match = refData.customers.find((c) => c.partnerId === cid);
                        if (match) setTermsDays(String(match.defaultPaymentTermsDays));
                      }}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    >
                      {refData.customers.map((c) => (
                        <option key={c.partnerId} value={c.partnerId}>
                          {c.partnerCode} · {c.displayName}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.orderDateLabel}</span>
                    <input
                      id="so-ed-order-date"
                      type="date"
                      required
                      value={orderDate}
                      onChange={(e) => setOrderDate(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.requestedDateLabel}</span>
                    <input
                      id="so-ed-req-date"
                      type="date"
                      value={requestedDate}
                      onChange={(e) => setRequestedDate(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                    />
                  </label>
                </div>

                <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.termsDaysLabel}</span>
                    <input
                      id="so-ed-terms"
                      type="number"
                      min="0"
                      max="365"
                      required
                      value={termsDays}
                      onChange={(e) => setTermsDays(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.customerRefLabel}</span>
                    <input
                      id="so-ed-cust-ref"
                      type="text"
                      value={customerRef}
                      placeholder={st.composer.customerRefPlaceholder}
                      onChange={(e) => setCustomerRef(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.deliveryAddressLabel}</span>
                    <input
                      id="so-ed-address"
                      type="text"
                      required
                      value={deliveryAddress}
                      placeholder={st.composer.deliveryAddressPlaceholder}
                      onChange={(e) => setDeliveryAddress(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                </div>

                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  {editingRevNumber > 1 ? (
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.changeReasonLabel}</span>
                      <input
                        id="so-ed-change-reason"
                        type="text"
                        value={changeReason}
                        placeholder={st.composer.changeReasonPlaceholder}
                        onChange={(e) => setChangeReason(e.target.value)}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </label>
                  ) : null}
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.notesLabel}</span>
                    <input
                      id="so-ed-notes"
                      type="text"
                      value={notes}
                      placeholder={st.composer.notesPlaceholder}
                      onChange={(e) => setNotes(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                </div>

                {/* Lines Table Editor */}
                <div className="space-y-2 border-t border-[var(--border-hairline)] pt-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h3 className="text-xs font-semibold text-[var(--text-primary)]">{st.composer.linesTitle}</h3>
                    <button
                      type="button"
                      id="so-add-line-btn"
                      onClick={() => setDraftLines((prev) => [...prev, emptyDraftLine(refData)])}
                      className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                    >
                      <Plus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                      <span>{st.composer.addLineBtn}</span>
                    </button>
                  </div>

                  <div className="space-y-2.5">
                    {draftLines.map((row, idx) => {
                      const itemObj = itemMap.get(row.itemId);
                      const uomOpts = itemObj?.allowedUoms ?? [];
                      const avail = itemObj?.stockAvailability;
                      return (
                        <div
                          key={`line-${idx}`}
                          className="grid grid-cols-1 gap-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-3 md:grid-cols-12"
                        >
                          <label className="block space-y-1 md:col-span-4">
                            <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.colItem}</span>
                            <select
                              id={`so-line-item-${idx}`}
                              value={row.itemId}
                              onChange={(e) => {
                                const nextItemId = e.target.value;
                                const nextItem = itemMap.get(nextItemId);
                                const nextUom =
                                  nextItem?.allowedUoms.find((u) => u.uomId === nextItem.defaultSalesUomId) ??
                                  nextItem?.allowedUoms[0];
                                setDraftLines((prev) =>
                                  prev.map((l, i) =>
                                    i === idx
                                      ? { ...l, itemId: nextItemId, orderedUomId: nextUom?.uomId ?? "" }
                                      : l,
                                  ),
                                );
                              }}
                              className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                            >
                              {refData.items.map((it) => (
                                <option key={it.itemId} value={it.itemId}>
                                  {it.itemCode} · {it.name}
                                </option>
                              ))}
                            </select>
                            {avail ? (
                              <div className="text-[11px] text-[var(--text-secondary)]">
                                <span>{st.composer.stockOnHandPrefix} </span>
                                <span className="font-code tabular-nums">{avail.totalValuedOnHandBaseQuantity}</span>
                                <span> · {st.composer.stockAvailablePrefix} </span>
                                <span className="font-code font-semibold tabular-nums text-[var(--status-ok-text)]">
                                  {avail.availableBaseQuantity} {itemObj?.baseUomCode}
                                </span>
                              </div>
                            ) : null}
                          </label>

                          <label className="block space-y-1 md:col-span-2">
                            <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.colUom}</span>
                            <select
                              id={`so-line-uom-${idx}`}
                              value={row.orderedUomId}
                              onChange={(e) =>
                                setDraftLines((prev) =>
                                  prev.map((l, i) => (i === idx ? { ...l, orderedUomId: e.target.value } : l)),
                                )
                              }
                              className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                            >
                              {uomOpts.map((u) => (
                                <option key={u.uomId} value={u.uomId}>
                                  {u.uomCode} (×{u.factorToBase})
                                </option>
                              ))}
                            </select>
                          </label>

                          <label className="block space-y-1 md:col-span-2">
                            <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.colQty}</span>
                            <input
                              id={`so-line-qty-${idx}`}
                              type="number"
                              step="0.001"
                              min="0.001"
                              required
                              value={row.quantity}
                              placeholder="10"
                              onChange={(e) =>
                                setDraftLines((prev) =>
                                  prev.map((l, i) => (i === idx ? { ...l, quantity: e.target.value } : l)),
                                )
                              }
                              className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)]"
                            />
                          </label>

                          <label className="block space-y-1 md:col-span-2">
                            <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.colPrice}</span>
                            <input
                              id={`so-line-price-${idx}`}
                              type="number"
                              step="1"
                              min="1"
                              required
                              value={row.unitPrice}
                              placeholder="95000"
                              onChange={(e) =>
                                setDraftLines((prev) =>
                                  prev.map((l, i) => (i === idx ? { ...l, unitPrice: e.target.value } : l)),
                                )
                              }
                              className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)]"
                            />
                          </label>

                          <div className="flex items-end gap-1.5 md:col-span-2">
                            <label className="block flex-1 space-y-1">
                              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{st.composer.colTax}</span>
                              <select
                                id={`so-line-tax-${idx}`}
                                value={row.taxCodeId}
                                onChange={(e) =>
                                  setDraftLines((prev) =>
                                    prev.map((l, i) => (i === idx ? { ...l, taxCodeId: e.target.value } : l)),
                                  )
                                }
                                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                              >
                                <option value="">{st.composer.noTaxOption}</option>
                                {refData.outputTaxCodes.map((tc) => (
                                  <option key={tc.taxCodeId} value={tc.taxCodeId}>
                                    {tc.code} ({tc.nominalRatePercent}%)
                                  </option>
                                ))}
                              </select>
                            </label>
                            {draftLines.length > 1 ? (
                              <button
                                type="button"
                                aria-label={st.composer.removeLineBtn}
                                onClick={() => setDraftLines((prev) => prev.filter((_, i) => i !== idx))}
                                className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)]"
                              >
                                <Trash2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                              </button>
                            ) : null}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* Live Totals Strip + Submit */}
                <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] overflow-hidden rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] md:grid-cols-4 md:divide-x md:divide-y-0">
                  <div className="p-2.5">
                    <div className="text-[11px] text-[var(--text-secondary)]">{st.composer.totalBaseQtyLabel}</div>
                    <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">{previewTotals.totalBaseQty}</div>
                  </div>
                  <div className="p-2.5">
                    <div className="text-[11px] text-[var(--text-secondary)]">{st.composer.subtotalLabel}</div>
                    <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">{formatIdr(previewTotals.subtotal)}</div>
                  </div>
                  <div className="p-2.5">
                    <div className="text-[11px] text-[var(--text-secondary)]">{st.composer.taxTotalLabel}</div>
                    <div className="font-code text-xs font-bold tabular-nums text-[var(--text-primary)]">{formatIdr(previewTotals.taxTotal)}</div>
                  </div>
                  <div className="p-2.5">
                    <div className="text-[11px] text-[var(--text-secondary)]">{st.composer.grandTotalLabel}</div>
                    <div className="font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">{formatIdr(previewTotals.grandTotal)}</div>
                  </div>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border-hairline)] pt-3 text-xs text-[var(--text-secondary)]">
                  <span>{st.composer.od09BannerNote}</span>
                  <div className="flex flex-wrap items-center gap-2">
                    <button
                      type="button"
                      onClick={() => {
                        resetEditor();
                        setActiveTab("orders");
                      }}
                      className="inline-flex min-h-10 items-center whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                    >
                      {st.composer.resetEditorBtn}
                    </button>
                    <button
                      type="submit"
                      id="so-save-draft-btn"
                      disabled={savingDraft}
                      className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                    >
                      <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                      <span>{savingDraft ? st.composer.saving : st.composer.saveDraftCta}</span>
                    </button>
                  </div>
                </div>
              </form>
            </section>
          )}
        </div>
      ) : null}

      {/* TAB 3: FOUR-EYES QUEUE */}
      {activeTab === "queue" ? (
        <div id="so-panel-queue" role="tabpanel" aria-labelledby="so-tab-queue">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {st.tables.queueTitle}
            </div>
            {queueOrders.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{st.tables.emptyQueue}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="so-queue-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{st.tables.colOrderDoc}</th>
                      <th scope="col" className="py-2 md:px-2">{st.tables.colCustomerRef}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{st.tables.colRevisionAmount}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{st.tables.colAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {queueOrders.map((so) => (
                      <tr key={so.salesOrderId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colOrderDoc}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {so.soNumber ?? so.draftCode}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">{so.orderDate}</div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colCustomerRef}</span>
                          <div className="font-medium text-[var(--text-primary)]">
                            <span className="font-code">{so.customerCode}</span> · {so.customerName}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">{so.deliveryAddressSnapshot}</div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colRevisionAmount}</span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(so.pendingGrandTotalAmount ?? so.currentGrandTotalAmount)}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            {st.tables.revPrefix} <span className="font-code tabular-nums">{so.latestRevisionNumber}</span>
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{st.tables.colAction}</span>
                          <button
                            type="button"
                            data-testid={`queue-open-${so.soNumber ?? so.draftCode}`}
                            onClick={() => openOrderDetail(so.salesOrderId)}
                            className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                          >
                            <ShieldCheck className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{st.tables.detailCta}</span>
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}
    </div>
  );
}
