"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  ArrowRightLeft,
  CheckCircle2,
  Clock,
  Eye,
  FileSpreadsheet,
  Package,
  RefreshCw,
  ShieldCheck,
} from "lucide-react";
import {
  ApiProblemError,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchInventoryOverview,
  fetchItemValuationLedger,
  InventoryOverviewResponse,
  ItemValuationLedgerResponse,
} from "@/lib/api/client";
import { formatIdr, formatUnitCostIdr, formatJakartaDateTime } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatQuantityValue, parseScaledBigInt } from "@/lib/receivingMath";

function PoolStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "ACTIVE") {
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

function AccrualStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "CLEARED") {
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

function InventoryErrorCard({
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
      : t.inventory.loadErrorTitle;
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
        <AlertCircle
          className="mt-0.5 h-5 w-5 shrink-0 text-[var(--status-danger-text)]"
          strokeWidth={1.75}
          aria-hidden="true"
        />
        <div className="space-y-2">
          <h1 className="font-heading text-lg font-bold text-[var(--text-primary)]">{title}</h1>
          <p className="text-sm text-[var(--text-secondary)]">{body}</p>
          <div className="flex flex-wrap gap-2 pt-2">
            {!isForbidden && !isNotFound ? (
              <button
                type="button"
                onClick={onRetry}
                className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
              >
                <RefreshCw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{t.inventory.retryButton}</span>
              </button>
            ) : null}
            <Link
              href="/workspaces"
              className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-4 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {t.entitySheet.returnWorkspaces}
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

export function InventorySheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const { locale, t } = useLocale();

  const [workspace, setWorkspace] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [overview, setOverview] = useState<InventoryOverviewResponse | null>(null);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loading, setLoading] = useState(true);
  const [retryNonce, setRetryNonce] = useState(0);

  const [activeTab, setActiveTab] = useState<"positions" | "accruals" | "movements">("positions");
  const [searchFilter, setSearchFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [refreshing, setRefreshing] = useState(false);

  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [ledgerData, setLedgerData] = useState<ItemValuationLedgerResponse | null>(null);
  const [ledgerLoading, setLedgerLoading] = useState(false);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchInventoryOverview(tenantId, entityId),
    ])
      .then(([ws, ov]) => {
        if (!active) return;
        setWorkspace(ws);
        setOverview(ov);
        setProblem(null);
        setLoading(false);

        const firstItem = ov.positions[0]?.itemId ?? null;
        if (firstItem) {
          setSelectedItemId(firstItem);
          setLedgerLoading(true);
          fetchItemValuationLedger(tenantId, entityId, firstItem)
            .then((ledger) => {
              if (!active) return;
              setLedgerData(ledger);
            })
            .catch(() => {
              // Non-fatal detail fetch; user can retry on item click.
            })
            .finally(() => {
              if (active) setLedgerLoading(false);
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

  const handleRefresh = useCallback(
    async (opts?: { search?: string; stockStatus?: string }) => {
      setRefreshing(true);
      try {
        const s = opts?.search !== undefined ? opts.search : searchFilter;
        const st = opts?.stockStatus !== undefined ? opts.stockStatus : statusFilter;
        const nextOv = await fetchInventoryOverview(tenantId, entityId, {
          search: s || undefined,
          stockStatus: st || undefined,
        });
        setOverview(nextOv);
        const targetItem =
          selectedItemId && nextOv.positions.some((p) => p.itemId === selectedItemId)
            ? selectedItemId
            : nextOv.positions[0]?.itemId ?? null;
        setSelectedItemId(targetItem);
        if (targetItem) {
          const ledger = await fetchItemValuationLedger(tenantId, entityId, targetItem);
          setLedgerData(ledger);
        } else {
          setLedgerData(null);
        }
      } catch (err) {
        const apiErr = err as ApiProblemError;
        if (apiErr.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
        setProblem(apiErr);
      } finally {
        setRefreshing(false);
      }
    },
    [tenantId, entityId, searchFilter, statusFilter, selectedItemId],
  );

  const handleInspectItem = async (itemId: string) => {
    setSelectedItemId(itemId);
    setLedgerLoading(true);
    try {
      const ledger = await fetchItemValuationLedger(tenantId, entityId, itemId);
      setLedgerData(ledger);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr.status === 401) {
        routerRef.current.replace("/login");
        return;
      }
      setProblem(apiErr);
    } finally {
      setLedgerLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-5xl space-y-4">
        <div className="h-8 w-72 rounded-[var(--radius-sm)] bg-[var(--bg-chrome)]" />
        <div className="h-44 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]" />
      </div>
    );
  }

  if (problem && !workspace) {
    return <InventoryErrorCard problem={problem} onRetry={() => setRetryNonce((n) => n + 1)} />;
  }

  const canAccessReceiving = Boolean(
    workspace?.effectivePermissions.some(
      (p) => p === "receiving.read" || p === "receiving.manage" || p === "receiving.accept",
    ),
  );

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
            <span>{t.inventory.backToOverview}</span>
          </Link>
          <h1 className="font-heading text-xl font-bold tracking-tight text-[var(--text-primary)] md:text-2xl">
            {t.inventory.pageTitle}
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

        {canAccessReceiving ? (
          <Link
            href={`/w/${tenantId}/${entityId}/receiving`}
            className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <Eye className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.inventory.openReceivingLink}</span>
          </Link>
        ) : null}
      </div>

      {problem ? (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-sm text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{problem.detail || problem.title}</span>
        </div>
      ) : null}

      {/* OD-04 Safe Default Banner */}
      <div className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-4 py-3 text-xs leading-5 text-[var(--text-secondary)]">
        <ShieldCheck
          className="mt-0.5 h-4 w-4 shrink-0 text-[var(--text-secondary)]"
          strokeWidth={1.75}
          aria-hidden="true"
        />
        <span>{t.inventory.od04PolicyBanner}</span>
      </div>

      {/* 4 KPI Summary Cards (Neutral #151412 counters) */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <div
          data-testid="inv-kpi-items"
          className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5"
        >
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.inventory.stats.stockedItemsWithBalance}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {String(overview?.totalStockedItemsWithBalance ?? 0)}
          </div>
        </div>

        <div
          data-testid="inv-kpi-carrying"
          className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5"
        >
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.inventory.stats.carryingValueTotal}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(overview?.totalCarryingValueIdr ?? 0)}
          </div>
        </div>

        <div
          data-testid="inv-kpi-grni"
          className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5"
        >
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.inventory.stats.openGrniTotal}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(overview?.totalOpenGrniAccrualIdr ?? 0)}
          </div>
        </div>

        <div
          data-testid="inv-kpi-restricted"
          className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-3.5"
        >
          <div className="text-xs font-medium text-[var(--text-secondary)]">
            {t.inventory.stats.restrictedItemCount}
          </div>
          <div
            className={`font-code mt-1 text-lg font-bold tabular-nums ${
              Number(overview?.restrictedStockItemCount ?? 0) > 0
                ? "text-[var(--status-warn-text)]"
                : "text-[var(--text-primary)]"
            }`}
          >
            {String(overview?.restrictedStockItemCount ?? 0)}
          </div>
        </div>
      </div>

      {/* Tabs + Filters */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div
          role="tablist"
          aria-label={t.inventory.tabListLabel}
          className="inline-flex flex-wrap gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-1"
        >
          <button
            type="button"
            id="inv-tab-positions"
            role="tab"
            aria-selected={activeTab === "positions"}
            onClick={() => setActiveTab("positions")}
            className={`inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] px-3 text-xs font-semibold sm:text-sm ${
              activeTab === "positions"
                ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
            }`}
          >
            <Package className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.inventory.tabs.positions}</span>
          </button>

          <button
            type="button"
            id="inv-tab-accruals"
            role="tab"
            aria-selected={activeTab === "accruals"}
            onClick={() => setActiveTab("accruals")}
            className={`inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] px-3 text-xs font-semibold sm:text-sm ${
              activeTab === "accruals"
                ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
            }`}
          >
            <FileSpreadsheet className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.inventory.tabs.accruals}</span>
          </button>

          <button
            type="button"
            id="inv-tab-movements"
            role="tab"
            aria-selected={activeTab === "movements"}
            onClick={() => setActiveTab("movements")}
            className={`inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] px-3 text-xs font-semibold sm:text-sm ${
              activeTab === "movements"
                ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
            }`}
          >
            <ArrowRightLeft className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.inventory.tabs.movements}</span>
          </button>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <input
            type="search"
            aria-label={t.inventory.filterSearchLabel}
            placeholder={t.inventory.filterSearchPlaceholder}
            value={searchFilter}
            onChange={(e) => setSearchFilter(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") void handleRefresh({ search: searchFilter, stockStatus: statusFilter });
            }}
            className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] placeholder:text-[var(--text-muted)] sm:w-56"
          />

          {activeTab === "positions" ? (
            <select
              aria-label={t.inventory.filterStatusLabel}
              value={statusFilter}
              onChange={(e) => {
                const nextSt = e.target.value;
                setStatusFilter(nextSt);
                void handleRefresh({ search: searchFilter, stockStatus: nextSt });
              }}
              className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
            >
              <option value="">{t.inventory.allStockStatuses}</option>
              {(["UNRESTRICTED", "QUALITY_INSPECTION", "BLOCKED"] as const).map((code) => (
                <option key={code} value={code}>
                  {t.inventory.stockStatusLabels[code] ?? code}
                </option>
              ))}
            </select>
          ) : null}

          <button
            type="button"
            disabled={refreshing}
            onClick={() => void handleRefresh({ search: searchFilter, stockStatus: statusFilter })}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.inventory.refreshButton}</span>
          </button>
        </div>
      </div>

      {/* TAB 1: POSITIONS & VALUATION LEDGER */}
      {activeTab === "positions" ? (
        <div className="space-y-5">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <Package className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                {t.inventory.positionsCardTitle}
              </h2>
            </div>

            {!overview || overview.positions.length === 0 ? (
              <div className="p-8 text-center">
                <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                  {t.inventory.emptyPositionsTitle}
                </h3>
                <p className="mx-auto mt-1 max-w-lg text-xs text-[var(--text-secondary)]">
                  {t.inventory.emptyPositionsBody}
                </p>
              </div>
            ) : (
              <div className="md:overflow-x-auto">
                <table className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2.5 md:pl-3 md:pr-2.5">
                        {t.inventory.colItem}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5 text-right">
                        {t.inventory.colBuckets}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5 text-right">
                        {t.inventory.colValuedQty}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5 text-right">
                        {t.inventory.colMovingAverageCost}
                      </th>
                      <th scope="col" className="py-2.5 md:px-2.5">
                        {t.inventory.colPoolStatus}
                      </th>
                      <th scope="col" className="py-2.5 md:pl-2.5 md:pr-3 text-right">
                        {t.inventory.colAction}
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.positions.map((pos) => {
                      const isSelected = pos.itemId === selectedItemId;
                      const qiScaled = parseScaledBigInt(pos.qualityInspectionBaseQuantity) ?? 0n;
                      const blkScaled = parseScaledBigInt(pos.blockedBaseQuantity) ?? 0n;
                      return (
                        <tr
                          key={pos.stockPositionId}
                          className={`block p-3 md:table-row md:p-0 ${
                            isSelected ? "bg-[var(--bg-subtle)]" : ""
                          }`}
                        >
                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-3 md:pr-2.5">
                            <span className="font-medium text-[var(--text-secondary)] md:hidden">
                              {t.inventory.colItem}
                            </span>
                            <div className="sm:text-right md:text-left">
                              <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                                {pos.itemCode}
                              </div>
                              <div className="text-[var(--text-secondary)]">
                                {pos.itemName} ({pos.baseUomCode})
                              </div>
                            </div>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5 md:text-right">
                            <span className="font-medium text-[var(--text-secondary)] md:hidden">
                              {t.inventory.colBuckets}
                            </span>
                            <div className="tabular-nums">
                              <div className="text-[var(--text-primary)]">
                                <span className="text-[var(--text-secondary)]">
                                  {t.inventory.bucketCodes.unrestricted}{" "}
                                </span>
                                <span className="font-code">
                                  {formatQuantityValue(pos.unrestrictedBaseQuantity, locale)}
                                </span>
                                {qiScaled > 0n ? (
                                  <>
                                    {" · "}
                                    <span className="text-[var(--text-secondary)]">
                                      {t.inventory.bucketCodes.qi}{" "}
                                    </span>
                                    <span className="font-code">
                                      {formatQuantityValue(pos.qualityInspectionBaseQuantity, locale)}
                                    </span>
                                  </>
                                ) : null}
                                {blkScaled > 0n ? (
                                  <>
                                    {" · "}
                                    <span className="text-[var(--text-secondary)]">
                                      {t.inventory.bucketCodes.blocked}{" "}
                                    </span>
                                    <span className="font-code">
                                      {formatQuantityValue(pos.blockedBaseQuantity, locale)}
                                    </span>
                                  </>
                                ) : null}
                              </div>
                              <div className="text-[11px] text-[var(--text-muted)]">
                                {t.inventory.bucketCodes.available}:{" "}
                                <span className="font-code">
                                  {formatQuantityValue(pos.availableBaseQuantity, locale)} {pos.baseUomCode}
                                </span>
                              </div>
                            </div>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5 md:text-right">
                            <span className="font-medium text-[var(--text-secondary)] md:hidden">
                              {t.inventory.colValuedQty}
                            </span>
                            <span className="font-code font-semibold tabular-nums md:whitespace-nowrap text-[var(--text-primary)]">
                              {formatQuantityValue(pos.totalValuedOnHandBaseQuantity, locale)} {pos.baseUomCode}
                            </span>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5 md:text-right">
                            <span className="font-medium text-[var(--text-secondary)] md:hidden">
                              {t.inventory.colMovingAverageCost}
                            </span>
                            <div className="font-code tabular-nums">
                              <div className="font-semibold text-[var(--text-primary)]">
                                {formatUnitCostIdr(pos.movingAverageUnitCost)} / {pos.baseUomCode}
                              </div>
                              <div className="text-[11px] text-[var(--text-secondary)]">
                                {formatIdr(pos.carryingValueAmount)}
                              </div>
                            </div>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5">
                            <span className="font-medium text-[var(--text-secondary)] md:hidden">
                              {t.inventory.colPoolStatus}
                            </span>
                            <div>
                              <PoolStatusBadge
                                status={pos.poolStatus}
                                label={t.inventory.poolStatusLabels[pos.poolStatus] ?? pos.poolStatus}
                              />
                            </div>
                          </td>

                          <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-center sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-2.5 md:pr-3 md:text-right">
                            <span className="font-medium text-[var(--text-secondary)] md:hidden">
                              {t.inventory.colAction}
                            </span>
                            <button
                              type="button"
                              onClick={() => void handleInspectItem(pos.itemId)}
                              className={`inline-flex min-h-9 self-start items-center justify-center whitespace-nowrap rounded-[var(--radius-sm)] border px-3 text-xs font-semibold sm:self-auto ${
                                isSelected
                                  ? "border-[var(--border-strong)] bg-[var(--bg-chrome)] text-[var(--text-primary)]"
                                  : "border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                              }`}
                            >
                              {isSelected ? t.inventory.activeLedgerButton : t.inventory.inspectLedgerButton}
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

          {/* Item Sequential Moving-Average Ledger Sheet */}
          <section
            id="inv-item-ledger-sheet"
            className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
          >
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <div className="flex items-center gap-2">
                <ShieldCheck
                  className="h-4 w-4 text-[var(--accent-primary)]"
                  strokeWidth={1.75}
                  aria-hidden="true"
                />
                <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                  {t.inventory.ledgerCardTitle}
                </h2>
              </div>
              {ledgerData ? (
                <span className="font-code text-xs font-semibold tabular-nums text-[var(--text-secondary)]">
                  {ledgerData.itemCode} — {formatQuantityValue(ledgerData.currentValuedBaseQuantity, locale)}{" "}
                  {ledgerData.baseUomCode} · {formatUnitCostIdr(ledgerData.currentMovingAverageUnitCost)} /{" "}
                  {ledgerData.baseUomCode}
                </span>
              ) : null}
            </div>

            {ledgerLoading ? (
              <div className="p-6 text-xs text-[var(--text-secondary)]">{t.inventory.loadingLedger}</div>
            ) : !ledgerData ? (
              <div className="p-6 text-center text-xs text-[var(--text-secondary)]">
                {t.inventory.selectItemPrompt}
              </div>
            ) : ledgerData.entries.length === 0 ? (
              <div className="p-6 text-center text-xs text-[var(--text-secondary)]">
                {t.inventory.emptyLedgerEntries}
              </div>
            ) : (
              <div className="md:overflow-x-auto">
                <table className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-3 md:pr-2">
                        {t.inventory.colLedgerSeq}
                      </th>
                      <th scope="col" className="py-2 md:px-2">
                        {t.inventory.colLedgerSource}
                      </th>
                      <th scope="col" className="py-2 md:px-2 text-right">
                        {t.inventory.colLedgerDelta}
                      </th>
                      <th scope="col" className="py-2 md:px-2 text-right">
                        {t.inventory.colLedgerBeforeAfterCost}
                      </th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-3 text-right">
                        {t.inventory.colLedgerCarrying}
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {ledgerData.entries.map((entry) => (
                      <tr key={entry.valuationEntryId} className="block p-3 md:table-row md:p-0">
                        <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-3 md:pr-2">
                          <span className="font-medium text-[var(--text-secondary)] md:hidden">
                            {t.inventory.colLedgerSeq}
                          </span>
                          <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            #{String(entry.poolSequenceNumber)}
                          </span>
                        </td>

                        <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2">
                          <span className="font-medium text-[var(--text-secondary)] md:hidden">
                            {t.inventory.colLedgerSource}
                          </span>
                          <div className="sm:text-right md:text-left">
                            <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                              {entry.postingDate} · {entry.sourceDocumentNumber}
                            </div>
                            <div className="text-[11px] text-[var(--text-secondary)]">
                              <span>{t.inventory.entryTypeLabels[entry.entryType] ?? entry.entryType}</span>
                              {" · "}
                              <span className="font-code tabular-nums">
                                {entry.movementNumber}
                                {entry.journalNumber ? ` · ${entry.journalNumber}` : ""}
                              </span>
                            </div>
                          </div>
                        </td>

                        <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="font-medium text-[var(--text-secondary)] md:hidden">
                            {t.inventory.colLedgerDelta}
                          </span>
                          <div className="font-code tabular-nums">
                            <div className="text-[var(--text-primary)]">
                              +{formatQuantityValue(entry.quantityDelta, locale)} @{" "}
                              {formatUnitCostIdr(entry.unitCostApplied)}
                            </div>
                            <div className="text-[11px] text-[var(--text-secondary)]">
                              {formatIdr(entry.valueDelta)}
                            </div>
                          </div>
                        </td>

                        <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="font-medium text-[var(--text-secondary)] md:hidden">
                            {t.inventory.colLedgerBeforeAfterCost}
                          </span>
                          <div className="font-code tabular-nums">
                            <div className="font-semibold text-[var(--text-primary)]">
                              {formatUnitCostIdr(entry.previousMovingAverageCost)} →{" "}
                              {formatUnitCostIdr(entry.newMovingAverageCost)}
                            </div>
                          </div>
                        </td>

                        <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-2 md:pr-3 md:text-right">
                          <span className="font-medium text-[var(--text-secondary)] md:hidden">
                            {t.inventory.colLedgerCarrying}
                          </span>
                          <div className="font-code tabular-nums">
                            <div className="font-semibold text-[var(--text-primary)]">
                              {formatIdr(entry.newPoolCarryingValue)}
                            </div>
                            <div className="text-[11px] text-[var(--text-secondary)]">
                              {formatQuantityValue(entry.newPoolBaseQuantity, locale)} {ledgerData.baseUomCode}
                            </div>
                          </div>
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

      {/* TAB 2: OPEN GRNI ACCRUALS */}
      {activeTab === "accruals" ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <FileSpreadsheet className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">
              {t.inventory.accrualsCardTitle}
            </h2>
          </div>

          {!overview || overview.openAccruals.length === 0 ? (
            <div className="p-8 text-center">
              <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                {t.inventory.emptyAccrualsTitle}
              </h3>
              <p className="mx-auto mt-1 max-w-lg text-xs text-[var(--text-secondary)]">
                {t.inventory.emptyAccrualsBody}
              </p>
            </div>
          ) : (
            <div className="md:overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="py-2.5 md:pl-3 md:pr-2.5">
                      {t.inventory.colAccrualRef}
                    </th>
                    <th scope="col" className="py-2.5 md:px-2.5">
                      {t.inventory.colSourceChain}
                    </th>
                    <th scope="col" className="py-2.5 md:px-2.5">
                      {t.inventory.colSupplier}
                    </th>
                    <th scope="col" className="py-2.5 md:px-2.5 text-right">
                      {t.inventory.colOpenVsAccrued}
                    </th>
                    <th scope="col" className="py-2.5 md:pl-2.5 md:pr-3">
                      {t.inventory.colAccrualStatus}
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.openAccruals.map((acc) => (
                    <tr key={acc.receiptAccrualId} className="block p-3 md:table-row md:p-0">
                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-3 md:pr-2.5">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colAccrualRef}
                        </span>
                        <div className="font-code tabular-nums sm:text-right md:text-left">
                          <div className="font-bold text-[var(--text-primary)]">{acc.accrualNumber}</div>
                          <div className="text-[11px] text-[var(--text-secondary)]">{acc.postingDate}</div>
                        </div>
                      </td>

                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colSourceChain}
                        </span>
                        <div className="font-code tabular-nums sm:text-right md:text-left">
                          <div className="font-semibold text-[var(--text-primary)]">
                            {acc.acceptanceNumber} · {acc.receiptNumber}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            {acc.poNumber} · {acc.journalNumber}
                          </div>
                        </div>
                      </td>

                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colSupplier}
                        </span>
                        <div className="sm:text-right md:text-left">
                          <div className="font-medium text-[var(--text-primary)]">{acc.supplierName}</div>
                          <div className="font-code text-[11px] text-[var(--text-secondary)]">{acc.supplierCode}</div>
                        </div>
                      </td>

                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5 md:text-right">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colOpenVsAccrued}
                        </span>
                        <div className="font-code tabular-nums">
                          <div className="font-semibold text-[var(--text-primary)]">
                            {formatIdr(acc.remainingOpenAmount)}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            / {formatIdr(acc.accruedAmount)}
                          </div>
                        </div>
                      </td>

                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-2.5 md:pr-3">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colAccrualStatus}
                        </span>
                        <div>
                          <AccrualStatusBadge
                            status={acc.status}
                            label={t.inventory.accrualStatusLabels[acc.status] ?? acc.status}
                          />
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

      {/* TAB 3: RECENT STOCK MOVEMENTS */}
      {activeTab === "movements" ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <ArrowRightLeft className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">
              {t.inventory.movementsCardTitle}
            </h2>
          </div>

          {!overview || overview.recentMovements.length === 0 ? (
            <div className="p-8 text-center">
              <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                {t.inventory.emptyMovementsTitle}
              </h3>
              <p className="mx-auto mt-1 max-w-lg text-xs text-[var(--text-secondary)]">
                {t.inventory.emptyMovementsBody}
              </p>
            </div>
          ) : (
            <div className="md:overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="py-2.5 md:pl-3 md:pr-2.5">
                      {t.inventory.colMovementRef}
                    </th>
                    <th scope="col" className="py-2.5 md:px-2.5">
                      {t.inventory.colMovementType}
                    </th>
                    <th scope="col" className="py-2.5 md:px-2.5 text-right">
                      {t.inventory.colMovementQtyVal}
                    </th>
                    <th scope="col" className="py-2.5 md:pl-2.5 md:pr-3">
                      {t.inventory.colMovementActor}
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.recentMovements.map((mov) => (
                    <tr key={mov.stockMovementId} className="block p-3 md:table-row md:p-0">
                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-3 md:pr-2.5">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colMovementRef}
                        </span>
                        <div className="font-code tabular-nums sm:text-right md:text-left">
                          <div className="font-bold text-[var(--text-primary)]">{mov.movementNumber}</div>
                          <div className="text-[11px] text-[var(--text-secondary)]">{mov.postingDate}</div>
                        </div>
                      </td>

                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colMovementType}
                        </span>
                        <div className="sm:text-right md:text-left">
                          <div className="font-medium text-[var(--text-primary)]">
                            {t.inventory.movementTypeLabels[mov.movementType] ?? mov.movementType}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span className="font-code tabular-nums">{mov.sourceDocumentNumber}</span>
                            {" · "}
                            <span className="font-code tabular-nums">{String(mov.lineCount)}</span>{" "}
                            {locale === "en-US" ? "lines" : "baris"}
                          </div>
                        </div>
                      </td>

                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:px-2.5 md:text-right">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colMovementQtyVal}
                        </span>
                        <div className="font-code tabular-nums">
                          <div className="font-semibold text-[var(--text-primary)]">
                            +{formatQuantityValue(mov.totalBaseQuantity, locale)}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            {formatIdr(mov.totalValuationAmount)}
                          </div>
                        </div>
                      </td>

                      <td className="flex flex-col gap-0.5 py-1.5 sm:flex-row sm:items-baseline sm:justify-between sm:gap-3 md:table-cell md:py-2.5 md:pl-2.5 md:pr-3">
                        <span className="font-medium text-[var(--text-secondary)] md:hidden">
                          {t.inventory.colMovementActor}
                        </span>
                        <div className="sm:text-right md:text-left">
                          <div className="font-code break-words text-[var(--text-primary)]">
                            {mov.createdByEmail.replace("@", "\u200B@")}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {formatJakartaDateTime(mov.createdAtUtc, locale)}
                          </div>
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
  );
}

export default function InventoryPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <InventorySheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
