"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  Clock,
  Download,
  FileSearch,
  Lock,
  RefreshCw,
  RotateCcw,
  ShieldAlert,
  X,
} from "lucide-react";
import {
  AccountingReportsOverviewResponse,
  AccountLedgerDrilldownResponse,
  AgingBucketTotalsDto,
  ApiProblemError,
  closeReportPeriod,
  fetchAccountingReportsOverview,
  fetchAccountLedgerDrilldown,
  getTrialBalanceCsvUrl,
  lockReportPeriod,
  reopenReportPeriod,
  ReportPeriodDto,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatIdr, formatJakartaDateTime } from "@/lib/i18n/messages";

type ActiveTab = "reconciliation-close" | "trial-balance" | "statements" | "aging";
type PeriodDrawerAction = "close" | "lock" | "reopen";

function formatNumber(n: number | string | undefined): number {
  const v = typeof n === "number" ? n : Number(n ?? 0);
  return Number.isFinite(v) ? v : 0;
}

function humanizeCode(code: string): string {
  return code
    .toLowerCase()
    .split("_")
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

export default function AccountingReportsPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = params?.tenantId ?? "";
  const entityId = params?.entityId ?? "";
  const { locale, t } = useLocale();
  const tr = t.reports;

  const [overview, setOverview] = useState<AccountingReportsOverviewResponse | null>(null);
  const [selectedPeriodId, setSelectedPeriodId] = useState<string>("");
  const [activeTab, setActiveTab] = useState<ActiveTab>("reconciliation-close");
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Drilldown state
  const [drilldownAccountId, setDrilldownAccountId] = useState<string | null>(null);
  const [drilldownData, setDrilldownData] = useState<AccountLedgerDrilldownResponse | null>(null);
  const [drilldownLoading, setDrilldownLoading] = useState(false);

  // Period action drawer state
  const [drawerAction, setDrawerAction] = useState<PeriodDrawerAction | null>(null);
  const [drawerPeriod, setDrawerPeriod] = useState<ReportPeriodDto | null>(null);
  const [drawerNote, setDrawerNote] = useState("");
  const [drawerBlockOnVariance, setDrawerBlockOnVariance] = useState(true);
  const [drawerBusy, setDrawerBusy] = useState(false);

  const resolveError = useCallback(
    (err: unknown): string => {
      const apiErr = err as Partial<ApiProblemError> | null;
      if (apiErr?.errorCode && tr.errors[apiErr.errorCode]) {
        return tr.errors[apiErr.errorCode];
      }
      if (apiErr?.detail) {
        return apiErr.detail;
      }
      if (err instanceof Error) {
        return err.message;
      }
      return t.login.errors.unknown;
    },
    [t.login.errors.unknown, tr.errors],
  );

  const loadOverview = useCallback(
    async (periodId?: string) => {
      if (!tenantId || !entityId) return;
      setLoading(true);
      setErrorMessage(null);
      try {
        const data = await fetchAccountingReportsOverview(
          tenantId,
          entityId,
          periodId ? { periodId } : undefined,
        );
        setOverview(data);
      } catch (err) {
        setErrorMessage(resolveError(err));
      } finally {
        setLoading(false);
      }
    },
    [entityId, resolveError, tenantId],
  );

  useEffect(() => {
    if (!tenantId || !entityId) return;
    let cancelled = false;
    fetchAccountingReportsOverview(
      tenantId,
      entityId,
      selectedPeriodId ? { periodId: selectedPeriodId } : undefined,
    )
      .then((data) => {
        if (cancelled) return;
        setOverview(data);
        setErrorMessage(null);
      })
      .catch((err) => {
        if (!cancelled) setErrorMessage(resolveError(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [entityId, resolveError, selectedPeriodId, tenantId]);

  const activePeriodId = selectedPeriodId || overview?.trialBalance.periodId || "";

  const openDrilldown = useCallback(
    async (accountId: string) => {
      if (!tenantId || !entityId) return;
      setDrilldownAccountId(accountId);
      setDrilldownLoading(true);
      setDrilldownData(null);
      setErrorMessage(null);
      try {
        const range = overview?.trialBalance
          ? { fromDate: overview.trialBalance.startDate, toDate: overview.trialBalance.endDate }
          : undefined;
        const result = await fetchAccountLedgerDrilldown(tenantId, entityId, accountId, range);
        setDrilldownData(result);
      } catch (err) {
        setErrorMessage(resolveError(err));
      } finally {
        setDrilldownLoading(false);
      }
    },
    [entityId, overview, resolveError, tenantId],
  );

  const openPeriodDrawer = (action: PeriodDrawerAction, period: ReportPeriodDto) => {
    setDrawerAction(action);
    setDrawerPeriod(period);
    setDrawerNote("");
    setDrawerBlockOnVariance(true);
    setSuccessMessage(null);
    setErrorMessage(null);
  };

  const handleDrawerSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!drawerAction || !drawerPeriod) return;
    setDrawerBusy(true);
    setErrorMessage(null);
    setSuccessMessage(null);
    try {
      if (drawerAction === "close") {
        await closeReportPeriod(tenantId, entityId, drawerPeriod.periodId, {
          note: drawerNote.trim() || null,
          blockOnVariance: drawerBlockOnVariance,
        });
        setSuccessMessage(tr.successClosed);
      } else if (drawerAction === "lock") {
        await lockReportPeriod(tenantId, entityId, drawerPeriod.periodId, {
          note: drawerNote.trim() || null,
        });
        setSuccessMessage(tr.successLocked);
      } else {
        await reopenReportPeriod(tenantId, entityId, drawerPeriod.periodId, {
          reason: drawerNote.trim(),
        });
        setSuccessMessage(tr.successReopened);
      }
      setDrawerAction(null);
      setDrawerPeriod(null);
      setDrawerNote("");
      await loadOverview(activePeriodId || undefined);
    } catch (err) {
      setErrorMessage(resolveError(err));
    } finally {
      setDrawerBusy(false);
    }
  };

  const varianceCount = useMemo(
    () => (overview?.reconciliations ?? []).filter((r) => r.status !== "RECONCILED").length,
    [overview?.reconciliations],
  );

  const canAnyPeriodAction = Boolean(overview?.canClosePeriod || overview?.canLockOrReopen);
  const csvHref = getTrialBalanceCsvUrl(tenantId, entityId, activePeriodId || undefined);

  const formatSourceLabel = (moduleCode: string, docTypeCode: string) => {
    const mod = tr.sourceModuleLabels[moduleCode] ?? humanizeCode(moduleCode);
    const doc = tr.sourceDocumentTypeLabels[docTypeCode] ?? humanizeCode(docTypeCode);
    return `${mod} · ${doc}`;
  };

  const renderPeriodStatusBadge = (status: string) => {
    const label = tr.statusLabels[status] ?? status;
    if (status === "OPEN") {
      return (
        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)] whitespace-nowrap">
          <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          {label}
        </span>
      );
    }
    if (status === "CLOSED") {
      return (
        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-warn-text)] whitespace-nowrap">
          <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          {label}
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-danger-text)] whitespace-nowrap">
        <Lock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        {label}
      </span>
    );
  };

  const renderAgingCard = (title: string, aging: AgingBucketTotalsDto) => (
    <div className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
      <div className="flex items-baseline justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
        <h3 className="text-sm font-semibold text-[var(--text-primary)]">{title}</h3>
        <span className="font-code text-xs tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
          {aging.openItemCount} {tr.agingOpenCountSuffix}
        </span>
      </div>
      <dl className="divide-y divide-[var(--border-hairline)] px-4 py-2 text-sm">
        {[
          [tr.agingCurrent, aging.currentAmount],
          [tr.aging1To30, aging.days1To30Amount],
          [tr.aging31To60, aging.days31To60Amount],
          [tr.aging61To90, aging.days61To90Amount],
          [tr.agingOver90, aging.over90Amount],
        ].map(([label, amount]) => (
          <div key={String(label)} className="flex items-center justify-between gap-2 py-2">
            <dt className="text-[var(--text-secondary)]">{label}</dt>
            <dd className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
              {formatIdr(formatNumber(amount as number | string))}
            </dd>
          </div>
        ))}
        <div className="flex items-center justify-between gap-2 pt-3 pb-1 font-semibold">
          <dt className="text-[var(--text-primary)]">{tr.agingTotalOpen}</dt>
          <dd className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
            {formatIdr(formatNumber(aging.totalOpenAmount))}
          </dd>
        </div>
      </dl>
    </div>
  );

  return (
    <div className="mx-auto max-w-7xl space-y-5 px-4 py-5 sm:px-6">
      {/* Header */}
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 space-y-1">
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 text-xs font-medium text-[var(--accent-primary)] hover:underline"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            {tr.backToEntity}
          </Link>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)] sm:text-2xl">
            {tr.heading}
          </h1>
          <p className="text-sm text-[var(--text-secondary)]">{tr.lead}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 lg:shrink-0">
          {overview && overview.periods.length > 0 && (
            <label className="flex items-center gap-2 text-xs text-[var(--text-secondary)]">
              <span className="sr-only">{tr.periodLabel}</span>
              <select
                aria-label={tr.periodLabel}
                value={activePeriodId}
                onChange={(e) => setSelectedPeriodId(e.target.value)}
                className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs font-medium text-[var(--text-primary)]"
              >
                {overview.periods.map((p) => (
                  <option key={p.periodId} value={p.periodId}>
                    {p.periodCode} ({tr.statusLabels[p.status] ?? p.status})
                  </option>
                ))}
              </select>
            </label>
          )}

          <a
            href={csvHref}
            download
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-1.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <Download className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            {tr.exportCsvButton}
          </a>

          <button
            type="button"
            onClick={() => void loadOverview(activePeriodId || undefined)}
            disabled={loading}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-1.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-50"
          >
            <RefreshCw
              className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`}
              strokeWidth={1.75}
              aria-hidden="true"
            />
            {tr.refreshButton}
          </button>
        </div>
      </div>

      {/* Read-only notice */}
      {overview && !canAnyPeriodAction && (
        <div
          role="status"
          className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
        >
          <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <p>{tr.readOnlyNotice}</p>
        </div>
      )}

      {/* Collapsible OD-11 / OD-13 governance notice */}
      <details className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3.5 py-2.5 text-xs">
        <summary className="flex cursor-pointer list-none items-center justify-between gap-2 font-medium text-[var(--text-primary)]">
          <span>{tr.governanceTitle}</span>
          <ChevronDown
            className="h-4 w-4 shrink-0 text-[var(--text-secondary)]"
            strokeWidth={1.75}
            aria-hidden="true"
          />
        </summary>
        <p className="mt-2 leading-relaxed text-[var(--text-secondary)]">{tr.governanceBody}</p>
      </details>

      {/* Feedback alerts */}
      {errorMessage && (
        <div
          role="alert"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-2.5 text-sm text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{errorMessage}</span>
        </div>
      )}

      {successMessage && (
        <div
          role="status"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-3.5 py-2.5 text-sm text-[var(--status-ok-text)]"
        >
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{successMessage}</span>
        </div>
      )}

      {/* 4 Baseline-Aligned KPI Cards */}
      {overview && (
        <section
          aria-label={tr.kpiAriaLabel}
          className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4"
        >
          <div className="flex flex-col justify-between rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{tr.kpiTbBalanceLabel}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)] whitespace-nowrap">
              {formatIdr(formatNumber(overview.trialBalance.totalEndingDebit))}
            </p>
            <div className="mt-2 flex items-center gap-1.5 text-xs">
              {overview.trialBalance.isBalanced ? (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)]">
                  <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {tr.kpiTbBalancedBadge}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-danger-text)]">
                  <AlertCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {tr.kpiTbUnbalancedBadge}
                </span>
              )}
            </div>
          </div>

          <div className="flex flex-col justify-between rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{tr.kpiNetIncomeLabel}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)] whitespace-nowrap">
              {formatIdr(formatNumber(overview.profitAndLoss.netIncome))}
            </p>
            <p className="mt-2 font-code text-xs tabular-nums text-[var(--text-secondary)]">
              {overview.trialBalance.periodCode}
            </p>
          </div>

          <div className="flex flex-col justify-between rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{tr.kpiTotalAssetsLabel}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)] whitespace-nowrap">
              {formatIdr(formatNumber(overview.balanceSheet.totalAssets))}
            </p>
            <div className="mt-2 flex items-center gap-1.5 text-xs">
              {overview.balanceSheet.isBalanced ? (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)]">
                  <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {tr.bsBalancedBadge}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-danger-text)]">
                  <AlertCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {tr.bsUnbalancedBadge}
                </span>
              )}
            </div>
          </div>

          <div className="flex flex-col justify-between rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{tr.kpiControlStatusLabel}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)] whitespace-nowrap">
              {overview.reconciliations.length - varianceCount}/{overview.reconciliations.length}
            </p>
            <div className="mt-2 flex items-center gap-1.5 text-xs">
              {varianceCount === 0 ? (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)]">
                  <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {tr.kpiAllReconciled}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-warn-text)]">
                  <AlertCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {varianceCount} {tr.kpiVariancesFound}
                </span>
              )}
            </div>
          </div>
        </section>
      )}

      {/* Segmented Tabs */}
      <div
        role="tablist"
        aria-label={tr.heading}
        className="grid grid-cols-1 gap-1.5 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-1 sm:flex sm:flex-wrap"
      >
        {(
          [
            ["reconciliation-close", tr.tabReconciliationClose],
            ["trial-balance", tr.tabTrialBalance],
            ["statements", tr.tabStatements],
            ["aging", tr.tabAgingSummary],
          ] as [ActiveTab, string][]
        ).map(([key, label]) => {
          const active = activeTab === key;
          return (
            <button
              key={key}
              id={`reports-tab-${key}`}
              type="button"
              role="tab"
              aria-selected={active}
              aria-controls={`reports-panel-${key}`}
              onClick={() => setActiveTab(key)}
              className={`min-h-9 rounded-[var(--radius-sm)] px-3 py-1.5 text-left text-xs font-medium transition-colors sm:text-center ${
                active
                  ? "border border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-primary)]"
                  : "border border-transparent text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
              }`}
            >
              {label}
            </button>
          );
        })}
      </div>

      {/* Period Close / Lock / Reopen Inline Drawer */}
      {drawerAction && drawerPeriod && (
        <form
          onSubmit={handleDrawerSubmit}
          className="space-y-3 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4"
        >
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">
              {drawerAction === "close"
                ? `${tr.drawerCloseTitle}: ${drawerPeriod.periodCode}`
                : drawerAction === "lock"
                  ? `${tr.drawerLockTitle}: ${drawerPeriod.periodCode}`
                  : `${tr.drawerReopenTitle}: ${drawerPeriod.periodCode}`}
            </h2>
            <button
              type="button"
              onClick={() => {
                setDrawerAction(null);
                setDrawerPeriod(null);
              }}
              aria-label={tr.drawerCancel}
              className="inline-flex h-9 w-9 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            >
              <X className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </button>
          </div>

          <div className="space-y-1.5">
            <label
              htmlFor="period-action-note"
              className="block text-xs font-medium text-[var(--text-primary)]"
            >
              {drawerAction === "reopen" ? tr.drawerReasonLabel : tr.drawerNoteLabel}
            </label>
            <textarea
              id="period-action-note"
              rows={2}
              required={drawerAction === "reopen"}
              value={drawerNote}
              onChange={(e) => setDrawerNote(e.target.value)}
              placeholder={
                drawerAction === "reopen" ? tr.drawerReasonPlaceholder : tr.drawerNotePlaceholder
              }
              className="w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-2 text-sm text-[var(--text-primary)]"
            />
          </div>

          {drawerAction === "close" && (
            <label className="flex items-center gap-2 text-xs text-[var(--text-secondary)]">
              <input
                type="checkbox"
                checked={drawerBlockOnVariance}
                onChange={(e) => setDrawerBlockOnVariance(e.target.checked)}
                className="h-4 w-4 rounded border-[var(--border-strong)] accent-[var(--accent-primary)]"
              />
              {tr.drawerBlockOnVarianceLabel}
            </label>
          )}

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="submit"
              disabled={drawerBusy}
              className="min-h-9 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 py-1.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-50"
            >
              {drawerBusy
                ? tr.drawerBusy
                : drawerAction === "close"
                  ? tr.drawerSubmitClose
                  : drawerAction === "lock"
                    ? tr.drawerSubmitLock
                    : tr.drawerSubmitReopen}
            </button>
            <button
              type="button"
              onClick={() => {
                setDrawerAction(null);
                setDrawerPeriod(null);
              }}
              className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-1.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {tr.drawerCancel}
            </button>
          </div>
        </form>
      )}

      {/* TAB 1: Reconciliation & Period Close */}
      {overview && activeTab === "reconciliation-close" && (
        <div
          id="reports-panel-reconciliation-close"
          role="tabpanel"
          aria-labelledby="reports-tab-reconciliation-close"
          className="space-y-5"
        >
          {/* 5-Control Account Reconciliations */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                {tr.reconciliationsTitle}
              </h2>
              <p className="mt-0.5 text-xs text-[var(--text-secondary)]">
                {tr.reconciliationsSubtitle}
              </p>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th className="px-4 py-2.5 font-medium">{tr.colControlAccount}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colGlBalance}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colSubledgerBalance}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colVariance}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colOpenItems}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{tr.colStatus}</th>
                    <th className="px-4 py-2.5 text-right font-medium whitespace-nowrap">{tr.colDrilldown}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.reconciliations.map((row) => (
                    <tr
                      key={row.purposeCode}
                      className="block p-3.5 hover:bg-[var(--bg-subtle)]/50 md:table-row md:p-0"
                    >
                      <td className="block pb-2 md:table-cell md:px-4 md:py-2.5">
                        <div className="font-medium text-[var(--text-primary)]">{row.controlLabel}</div>
                        <div className="font-code text-[11px] text-[var(--text-secondary)]">
                          {row.accountCode ? `${row.accountCode} · ${row.accountName}` : "—"}
                        </div>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colGlBalance}</span>
                        <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                          {formatIdr(formatNumber(row.glBalance))}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colSubledgerBalance}</span>
                        <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                          {formatIdr(formatNumber(row.subledgerBalance))}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colVariance}</span>
                        <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                          {formatIdr(formatNumber(row.varianceAmount))}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colOpenItems}</span>
                        <span className="font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                          {row.openItemCount}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colStatus}</span>
                        <span
                          className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-medium whitespace-nowrap ${
                            row.status === "RECONCILED"
                              ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                              : "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                          }`}
                        >
                          {row.status === "RECONCILED" ? (
                            <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          ) : (
                            <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          )}
                          {tr.reconciliationStatusLabels[row.status] ?? row.status}
                        </span>
                      </td>
                      <td className="block pt-2 md:table-cell md:px-4 md:py-2.5 md:text-right">
                        {row.accountId ? (
                          <button
                            type="button"
                            onClick={() => void openDrilldown(row.accountId!)}
                            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1 text-xs font-medium text-[var(--accent-primary)] hover:bg-[var(--bg-chrome)] whitespace-nowrap"
                          >
                            <FileSearch className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            {tr.drilldownButton}
                          </button>
                        ) : (
                          <span className="text-[var(--text-secondary)]">—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* Financial Periods & Close Controls */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                {tr.periodCloseTitle}
              </h2>
              <p className="mt-0.5 text-xs text-[var(--text-secondary)]">
                {tr.periodCloseSubtitle}
              </p>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th className="px-4 py-2.5 font-medium whitespace-nowrap">{tr.colPeriodCode}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{tr.colDateRange}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{tr.colPeriodStatus}</th>
                    <th className="px-3 py-2.5 font-medium">{tr.colCloseNote}</th>
                    {canAnyPeriodAction && (
                      <th className="px-4 py-2.5 text-right font-medium whitespace-nowrap">{tr.colActions}</th>
                    )}
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.periods.map((period) => (
                    <tr
                      key={period.periodId}
                      className="block p-3.5 hover:bg-[var(--bg-subtle)]/50 md:table-row md:p-0"
                    >
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colPeriodCode}</span>
                        <span className="font-code font-medium tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                          {period.periodCode}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colDateRange}</span>
                        <span className="font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                          {period.startDate} – {period.endDate}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colPeriodStatus}</span>
                        {renderPeriodStatusBadge(period.status)}
                      </td>
                      <td className="flex items-center justify-between gap-2 py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{tr.colCloseNote}</span>
                        <span className="text-right text-[var(--text-secondary)] md:text-left">
                          {period.closeNote || "—"}
                        </span>
                      </td>
                      {canAnyPeriodAction && (
                        <td className="block pt-2 md:table-cell md:px-4 md:py-2.5">
                          <div className="mt-1 flex flex-wrap items-center justify-start gap-1.5 md:mt-0 md:justify-end">
                            {period.status === "OPEN" && overview.canClosePeriod && (
                              <button
                                type="button"
                                onClick={() => openPeriodDrawer("close", period)}
                                className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 py-1 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] whitespace-nowrap"
                              >
                                {tr.closePeriodButton}
                              </button>
                            )}
                            {period.status === "CLOSED" && overview.canLockOrReopen && (
                              <>
                                <button
                                  type="button"
                                  onClick={() => openPeriodDrawer("reopen", period)}
                                  className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] whitespace-nowrap"
                                >
                                  <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                  {tr.reopenPeriodButton}
                                </button>
                                <button
                                  type="button"
                                  onClick={() => openPeriodDrawer("lock", period)}
                                  className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2.5 py-1 text-xs font-medium text-[var(--status-danger-text)] hover:opacity-90 whitespace-nowrap"
                                >
                                  <Lock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                  {tr.lockPeriodButton}
                                </button>
                              </>
                            )}
                            {period.status === "LOCKED" && (
                              <span className="inline-flex items-center gap-1 text-[11px] text-[var(--text-secondary)] whitespace-nowrap">
                                <Lock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                {tr.statusLabels.LOCKED}
                              </span>
                            )}
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* Period Close Audit History */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                {tr.closeHistoryTitle}
              </h2>
            </div>
            {overview.closeReviews.length === 0 ? (
              <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">
                {tr.closeHistoryEmpty}
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th className="px-4 py-2.5 font-medium whitespace-nowrap">{tr.colPeriodCode}</th>
                      <th className="px-3 py-2.5 font-medium whitespace-nowrap">{tr.colReviewAction}</th>
                      <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colReviewTb}</th>
                      <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colReviewVariances}</th>
                      <th className="px-3 py-2.5 font-medium">{tr.colReviewReason}</th>
                      <th className="px-4 py-2.5 text-right font-medium whitespace-nowrap">{tr.colReviewTime}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.closeReviews.map((rev) => (
                      <tr key={rev.closeReviewId} className="block p-3.5 md:table-row md:p-0">
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-4 md:py-2.5">
                          <span className="text-[var(--text-secondary)] md:hidden">{tr.colPeriodCode}</span>
                          <span className="font-code font-medium tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                            {rev.periodCode}
                          </span>
                        </td>
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                          <span className="text-[var(--text-secondary)] md:hidden">{tr.colReviewAction}</span>
                          <span className="font-medium text-[var(--text-primary)] whitespace-nowrap">
                            {tr.actionTypeLabels[rev.actionType] ?? rev.actionType}
                          </span>
                        </td>
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                          <span className="text-[var(--text-secondary)] md:hidden">{tr.colReviewTb}</span>
                          <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                            {formatIdr(formatNumber(rev.trialBalanceDebits))}
                          </span>
                        </td>
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                          <span className="text-[var(--text-secondary)] md:hidden">{tr.colReviewVariances}</span>
                          <span className="font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                            {rev.reconciliationVarianceCount}
                          </span>
                        </td>
                        <td className="flex items-center justify-between gap-2 py-1 md:table-cell md:px-3 md:py-2.5">
                          <span className="text-[var(--text-secondary)] md:hidden">{tr.colReviewReason}</span>
                          <span className="text-right text-[var(--text-secondary)] md:text-left">
                            {rev.reason || "—"}
                          </span>
                        </td>
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-4 md:py-2.5 md:text-right">
                          <span className="text-[var(--text-secondary)] md:hidden">{tr.colReviewTime}</span>
                          <span className="font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                            {formatJakartaDateTime(rev.createdAtUtc, locale)}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}

      {/* TAB 2: Trial Balance */}
      {overview && activeTab === "trial-balance" && (
        <section
          id="reports-panel-trial-balance"
          role="tabpanel"
          aria-labelledby="reports-tab-trial-balance"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">
              {tr.tbTitle} · {overview.trialBalance.periodCode}
            </h2>
            <p className="mt-0.5 text-xs text-[var(--text-secondary)]">{tr.tbSubtitle}</p>
          </div>
          {overview.trialBalance.rows.length === 0 ? (
            <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">{tr.tbEmpty}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)]">
                  <tr>
                    <th className="px-4 py-2.5 font-medium">{tr.colAccount}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{tr.colType}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colOpeningDr}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colOpeningCr}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colPeriodDr}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colPeriodCr}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colEndingDr}</th>
                    <th className="px-4 py-2.5 text-right font-medium whitespace-nowrap">{tr.colEndingCr}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.trialBalance.rows.map((row) => (
                    <tr key={row.accountId} className="hover:bg-[var(--bg-subtle)]/50">
                      <td className="px-4 py-2.5">
                        <button
                          type="button"
                          onClick={() => void openDrilldown(row.accountId)}
                          className="min-h-9 text-left font-medium text-[var(--accent-primary)] hover:underline"
                        >
                          <span className="font-code">{row.accountCode}</span> · {row.accountName}
                        </button>
                      </td>
                      <td className="px-3 py-2.5 text-[var(--text-secondary)] whitespace-nowrap">
                        {tr.accountTypeLabels[row.accountType] ?? row.accountType}
                      </td>
                      <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(row.openingDebit))}
                      </td>
                      <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(row.openingCredit))}
                      </td>
                      <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(row.periodDebit))}
                      </td>
                      <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(row.periodCredit))}
                      </td>
                      <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(row.endingDebit))}
                      </td>
                      <td className="px-4 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(row.endingCredit))}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot className="border-t-2 border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold">
                  <tr>
                    <td colSpan={2} className="px-4 py-2.5 text-[var(--text-primary)]">
                      {tr.tbTotalLabel}
                    </td>
                    <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                      {formatIdr(formatNumber(overview.trialBalance.totalOpeningDebit))}
                    </td>
                    <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                      {formatIdr(formatNumber(overview.trialBalance.totalOpeningCredit))}
                    </td>
                    <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                      {formatIdr(formatNumber(overview.trialBalance.totalPeriodDebit))}
                    </td>
                    <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                      {formatIdr(formatNumber(overview.trialBalance.totalPeriodCredit))}
                    </td>
                    <td className="px-3 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                      {formatIdr(formatNumber(overview.trialBalance.totalEndingDebit))}
                    </td>
                    <td className="px-4 py-2.5 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                      {formatIdr(formatNumber(overview.trialBalance.totalEndingCredit))}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          )}
        </section>
      )}

      {/* TAB 3: Profit & Loss and Balance Sheet */}
      {overview && activeTab === "statements" && (
        <div
          id="reports-panel-statements"
          role="tabpanel"
          aria-labelledby="reports-tab-statements"
          className="grid grid-cols-1 gap-4 lg:grid-cols-2"
        >
          {/* P&L */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">{tr.pnlTitle}</h2>
            </div>
            <div className="space-y-4 p-4 text-xs">
              <div>
                <h3 className="mb-1.5 font-semibold text-[var(--text-secondary)]">
                  {tr.pnlRevenueHeading}
                </h3>
                <div className="divide-y divide-[var(--border-hairline)]">
                  {overview.profitAndLoss.revenueLines.map((line) => (
                    <div key={line.accountId} className="flex items-center justify-between gap-2 py-1.5">
                      <button
                        type="button"
                        onClick={() => void openDrilldown(line.accountId)}
                        className="min-h-9 text-left text-[var(--accent-primary)] hover:underline"
                      >
                        <span className="font-code">{line.accountCode}</span> · {line.accountName}
                      </button>
                      <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(line.amount))}
                      </span>
                    </div>
                  ))}
                  <div className="flex items-center justify-between gap-2 pt-2 font-semibold text-[var(--text-primary)]">
                    <span>{tr.pnlTotalRevenue}</span>
                    <span className="font-code tabular-nums whitespace-nowrap">
                      {formatIdr(formatNumber(overview.profitAndLoss.totalRevenue))}
                    </span>
                  </div>
                </div>
              </div>

              <div>
                <h3 className="mb-1.5 font-semibold text-[var(--text-secondary)]">
                  {tr.pnlExpenseHeading}
                </h3>
                <div className="divide-y divide-[var(--border-hairline)]">
                  {overview.profitAndLoss.expenseLines.map((line) => (
                    <div key={line.accountId} className="flex items-center justify-between gap-2 py-1.5">
                      <button
                        type="button"
                        onClick={() => void openDrilldown(line.accountId)}
                        className="min-h-9 text-left text-[var(--accent-primary)] hover:underline"
                      >
                        <span className="font-code">{line.accountCode}</span> · {line.accountName}
                      </button>
                      <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(line.amount))}
                      </span>
                    </div>
                  ))}
                  <div className="flex items-center justify-between gap-2 pt-2 font-semibold text-[var(--text-primary)]">
                    <span>{tr.pnlTotalExpense}</span>
                    <span className="font-code tabular-nums whitespace-nowrap">
                      {formatIdr(formatNumber(overview.profitAndLoss.totalExpense))}
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex items-center justify-between gap-2 border-t-2 border-[var(--border-hairline)] pt-3 text-sm font-semibold text-[var(--text-primary)]">
                <span>{tr.pnlNetIncome}</span>
                <span className="font-code tabular-nums whitespace-nowrap">
                  {formatIdr(formatNumber(overview.profitAndLoss.netIncome))}
                </span>
              </div>
            </div>
          </section>

          {/* Balance Sheet */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">{tr.bsTitle}</h2>
              <span
                className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-medium whitespace-nowrap ${
                  overview.balanceSheet.isBalanced
                    ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                    : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
                }`}
              >
                {overview.balanceSheet.isBalanced ? (
                  <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                ) : (
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                )}
                {overview.balanceSheet.isBalanced ? tr.bsBalancedBadge : tr.bsUnbalancedBadge}
              </span>
            </div>
            <div className="space-y-4 p-4 text-xs">
              <div>
                <h3 className="mb-1.5 font-semibold text-[var(--text-secondary)]">
                  {tr.bsAssetHeading}
                </h3>
                <div className="divide-y divide-[var(--border-hairline)]">
                  {overview.balanceSheet.assetLines.map((line) => (
                    <div key={line.accountId} className="flex items-center justify-between gap-2 py-1.5">
                      <button
                        type="button"
                        onClick={() => void openDrilldown(line.accountId)}
                        className="min-h-9 text-left text-[var(--accent-primary)] hover:underline"
                      >
                        <span className="font-code">{line.accountCode}</span> · {line.accountName}
                      </button>
                      <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(line.amount))}
                      </span>
                    </div>
                  ))}
                  <div className="flex items-center justify-between gap-2 pt-2 font-semibold text-[var(--text-primary)]">
                    <span>{tr.bsTotalAssets}</span>
                    <span className="font-code tabular-nums whitespace-nowrap">
                      {formatIdr(formatNumber(overview.balanceSheet.totalAssets))}
                    </span>
                  </div>
                </div>
              </div>

              <div>
                <h3 className="mb-1.5 font-semibold text-[var(--text-secondary)]">
                  {tr.bsLiabilityHeading} &amp; {tr.bsEquityHeading}
                </h3>
                <div className="divide-y divide-[var(--border-hairline)]">
                  {overview.balanceSheet.liabilityLines.map((line) => (
                    <div key={line.accountId} className="flex items-center justify-between gap-2 py-1.5">
                      <button
                        type="button"
                        onClick={() => void openDrilldown(line.accountId)}
                        className="min-h-9 text-left text-[var(--accent-primary)] hover:underline"
                      >
                        <span className="font-code">{line.accountCode}</span> · {line.accountName}
                      </button>
                      <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(line.amount))}
                      </span>
                    </div>
                  ))}
                  {overview.balanceSheet.equityLines.map((line) => (
                    <div key={line.accountId} className="flex items-center justify-between gap-2 py-1.5">
                      <button
                        type="button"
                        onClick={() => void openDrilldown(line.accountId)}
                        className="min-h-9 text-left text-[var(--accent-primary)] hover:underline"
                      >
                        <span className="font-code">{line.accountCode}</span> · {line.accountName}
                      </button>
                      <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(line.amount))}
                      </span>
                    </div>
                  ))}
                  <div className="flex items-center justify-between gap-2 py-2 text-[var(--text-secondary)]">
                    <span>{tr.bsCurrentEarnings}</span>
                    <span className="font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                      {formatIdr(formatNumber(overview.balanceSheet.currentEarnings))}
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-2 pt-2 font-semibold text-[var(--text-primary)]">
                    <span>{tr.bsTotalLiabEquity}</span>
                    <span className="font-code tabular-nums whitespace-nowrap">
                      {formatIdr(formatNumber(overview.balanceSheet.totalLiabilitiesAndEquity))}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </section>
        </div>
      )}

      {/* TAB 4: AP & AR Aging Summary */}
      {overview && activeTab === "aging" && (
        <section
          id="reports-panel-aging"
          role="tabpanel"
          aria-labelledby="reports-tab-aging"
          className="space-y-3"
        >
          <div>
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">{tr.agingTitle}</h2>
            <p className="text-xs text-[var(--text-secondary)]">{tr.agingSubtitle}</p>
          </div>
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            {renderAgingCard(tr.agingApHeading, overview.apAging)}
            {renderAgingCard(tr.agingArHeading, overview.arAging)}
          </div>
        </section>
      )}

      {/* Source-Linked GL Account Ledger Drilldown Panel */}
      {drilldownAccountId && (
        <section
          aria-label={tr.drilldownTitle}
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
            <div>
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                {tr.drilldownTitle}
                {drilldownData ? ` · ${drilldownData.accountCode} ${drilldownData.accountName}` : ""}
              </h2>
            </div>
            <button
              type="button"
              onClick={() => {
                setDrilldownAccountId(null);
                setDrilldownData(null);
              }}
              className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-1 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              <X className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              {tr.drilldownCloseButton}
            </button>
          </div>

          {drilldownLoading ? (
            <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">{tr.drilldownLoading}</p>
          ) : !drilldownData || drilldownData.entries.length === 0 ? (
            <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">{tr.drilldownEmpty}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)]">
                  <tr>
                    <th className="px-4 py-2.5 font-medium whitespace-nowrap">{tr.colJournalNumber}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{tr.colPostingDate}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{tr.colSource}</th>
                    <th className="px-3 py-2.5 font-medium">{tr.colDescription}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colDebit}</th>
                    <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{tr.colCredit}</th>
                    <th className="px-4 py-2.5 text-right font-medium whitespace-nowrap">{tr.colRunningBalance}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {drilldownData.entries.map((entry) => (
                    <tr key={`${entry.journalId}-${entry.debitAmount}-${entry.creditAmount}`}>
                      <td className="px-4 py-2 font-code font-medium tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {entry.journalNumber}
                      </td>
                      <td className="px-3 py-2 font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                        {entry.postingDate}
                      </td>
                      <td className="px-3 py-2 text-[var(--text-secondary)] whitespace-nowrap">
                        {formatSourceLabel(entry.sourceModule, entry.sourceDocumentType)}
                      </td>
                      <td className="px-3 py-2 text-[var(--text-primary)]">{entry.description}</td>
                      <td className="px-3 py-2 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(entry.debitAmount))}
                      </td>
                      <td className="px-3 py-2 text-right font-code tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(entry.creditAmount))}
                      </td>
                      <td className="px-4 py-2 text-right font-code tabular-nums font-medium text-[var(--text-primary)] whitespace-nowrap">
                        {formatIdr(formatNumber(entry.runningBalance))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}
    </div>
  );
}
