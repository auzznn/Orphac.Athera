"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock,
  Eye,
  RefreshCw,
  RotateCcw,
  ShieldAlert,
  ShieldCheck,
  Upload,
  Wallet,
  XCircle,
} from "lucide-react";
import {
  type ApiProblemError,
  type CustomerReceiptDto,
  type InboundBankCreditLineDto,
  type ReceivablesOverviewResponse,
  allocateCustomerReceipt,
  confirmCustomerReceipt,
  fetchReceivablesOverview,
  importInboundBankStatement,
  reverseCustomerReceipt,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatIdr, todayJakartaDate } from "@/lib/i18n/messages";

type TabKey = "bank" | "receipts" | "aging";

function StatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "MATCHED" || status === "ALLOCATED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "REVERSED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-danger-text)]">
        <XCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-warn-text)]">
      <Clock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

export default function ReceivablesPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = params.tenantId;
  const entityId = params.entityId;
  const { t } = useLocale();
  const rt = t.receivables;

  const [asOfDate, setAsOfDate] = useState(todayJakartaDate());
  const [data, setData] = useState<ReceivablesOverviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [accessDenied, setAccessDenied] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<TabKey>("bank");
  const [showPolicy, setShowPolicy] = useState(false);

  // Import CSV drawer
  const [showImportDrawer, setShowImportDrawer] = useState(false);
  const [importBankAccountId, setImportBankAccountId] = useState("");
  const [importStmtRef, setImportStmtRef] = useState("");
  const [importStmtDate, setImportStmtDate] = useState(todayJakartaDate());
  const [importCsv, setImportCsv] = useState("");
  const [importing, setImporting] = useState(false);

  // Confirm Receipt + Initial Allocation drawer
  const [confirmTarget, setConfirmTarget] = useState<InboundBankCreditLineDto | null>(null);
  const [selectedCustomerId, setSelectedCustomerId] = useState("");
  const [receiptDate, setReceiptDate] = useState(todayJakartaDate());
  const [receiptNotes, setReceiptNotes] = useState("");
  const [initAllocAmounts, setInitAllocAmounts] = useState<Record<string, string>>({});
  const [confirming, setConfirming] = useState(false);

  // Allocate Remaining Unapplied drawer
  const [allocTarget, setAllocTarget] = useState<CustomerReceiptDto | null>(null);
  const [extraAllocAmounts, setExtraAllocAmounts] = useState<Record<string, string>>({});
  const [allocating, setAllocating] = useState(false);

  // Reverse Receipt drawer
  const [reverseTarget, setReverseTarget] = useState<CustomerReceiptDto | null>(null);
  const [reverseDate, setReverseDate] = useState(todayJakartaDate());
  const [reverseReason, setReverseReason] = useState("");
  const [reversing, setReversing] = useState(false);

  const mapError = useCallback(
    (err: unknown): string => {
      const problem = err as Partial<ApiProblemError>;
      if (problem && typeof problem === "object") {
        const code = problem.errorCode ?? "";
        if (code && rt.errors[code]) {
          return rt.errors[code];
        }
        if (problem.detail) return problem.detail;
      }
      return err instanceof Error ? err.message : String(err);
    },
    [rt.errors],
  );

  const loadSnapshot = useCallback(
    async (targetAsOf?: string) => {
      const res = await fetchReceivablesOverview(tenantId, entityId, targetAsOf ?? asOfDate);
      setData(res);
      setAccessDenied(false);
      setLoadError(null);
    },
    [tenantId, entityId, asOfDate],
  );

  const handleRefresh = useCallback(
    async (targetAsOf?: string) => {
      setRefreshing(true);
      setActionError(null);
      try {
        await loadSnapshot(targetAsOf);
      } catch (err) {
        const problem = err as Partial<ApiProblemError>;
        if (problem?.status === 403 || problem?.status === 404) {
          setAccessDenied(true);
        } else {
          setLoadError(mapError(err));
        }
      } finally {
        setRefreshing(false);
      }
    },
    [loadSnapshot, mapError],
  );

  useEffect(() => {
    let active = true;
    fetchReceivablesOverview(tenantId, entityId, asOfDate)
      .then((res) => {
        if (!active) return;
        setData(res);
        setAccessDenied(false);
        setLoadError(null);
      })
      .catch((err: unknown) => {
        if (!active) return;
        const problem = err as Partial<ApiProblemError>;
        if (problem?.status === 403 || problem?.status === 404) {
          setAccessDenied(true);
        } else {
          setLoadError(mapError(err));
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [tenantId, entityId, asOfDate, mapError]);

  const canManage = Boolean(data?.canManage);
  const bankAccounts = useMemo(() => data?.bankAccounts ?? [], [data?.bankAccounts]);
  const inboundLines = data?.inboundBankLines ?? [];
  const customers = useMemo(() => data?.customers ?? [], [data?.customers]);
  const openReceivables = useMemo(() => data?.openReceivables ?? [], [data?.openReceivables]);
  const receipts = data?.receipts ?? [];
  const aging = data?.aging;

  const openImportDrawer = () => {
    setActionError(null);
    setConfirmTarget(null);
    setAllocTarget(null);
    setReverseTarget(null);
    setImportBankAccountId(bankAccounts[0]?.bankAccountId ?? "");
    setImportStmtRef("");
    setImportStmtDate(todayJakartaDate());
    setImportCsv("ValueDate,Direction,Amount,BankReference,Counterparty,Description\n");
    setShowImportDrawer(true);
  };

  const openConfirmDrawer = (line: InboundBankCreditLineDto) => {
    setActionError(null);
    setShowImportDrawer(false);
    setAllocTarget(null);
    setReverseTarget(null);
    setConfirmTarget(line);
    setSelectedCustomerId(customers[0]?.partnerId ?? "");
    setReceiptDate(line.valueDate);
    setReceiptNotes("");
    setInitAllocAmounts({});
  };

  const openAllocDrawer = (rct: CustomerReceiptDto) => {
    setActionError(null);
    setShowImportDrawer(false);
    setConfirmTarget(null);
    setReverseTarget(null);
    setAllocTarget(rct);
    setExtraAllocAmounts({});
  };

  const openReverseDrawer = (rct: CustomerReceiptDto) => {
    setActionError(null);
    setShowImportDrawer(false);
    setConfirmTarget(null);
    setAllocTarget(null);
    setReverseTarget(rct);
    setReverseDate(todayJakartaDate());
    setReverseReason("");
  };

  const confirmCustomerOpenReceivables = useMemo(
    () => openReceivables.filter((r) => r.customerPartnerId === selectedCustomerId),
    [openReceivables, selectedCustomerId],
  );

  const allocCustomerOpenReceivables = useMemo(
    () => (allocTarget ? openReceivables.filter((r) => r.customerPartnerId === allocTarget.customerPartnerId) : []),
    [openReceivables, allocTarget],
  );

  const confirmTotals = useMemo(() => {
    const received = confirmTarget ? Number(confirmTarget.unmatchedAmount) : 0;
    const allocated = Object.values(initAllocAmounts).reduce((acc, v) => {
      const n = Number(v);
      return Number.isFinite(n) && n > 0 ? acc + n : acc;
    }, 0);
    return {
      received,
      allocated: Math.round(allocated * 100) / 100,
      unapplied: Math.round((received - allocated) * 100) / 100,
    };
  }, [confirmTarget, initAllocAmounts]);

  const allocTotals = useMemo(() => {
    const available = allocTarget ? Number(allocTarget.unappliedAmount) : 0;
    const allocated = Object.values(extraAllocAmounts).reduce((acc, v) => {
      const n = Number(v);
      return Number.isFinite(n) && n > 0 ? acc + n : acc;
    }, 0);
    return {
      available,
      allocated: Math.round(allocated * 100) / 100,
      remaining: Math.round((available - allocated) * 100) / 100,
    };
  }, [allocTarget, extraAllocAmounts]);

  const handleImportSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!importBankAccountId || !importStmtRef.trim() || !importCsv.trim()) return;
    setImporting(true);
    setActionError(null);
    try {
      await importInboundBankStatement(tenantId, entityId, {
        bankAccountId: importBankAccountId,
        statementReference: importStmtRef.trim(),
        statementDate: importStmtDate,
        openingBalance: 0,
        closingBalance: 0,
        csvContent: importCsv,
      });
      setShowImportDrawer(false);
      await loadSnapshot();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setImporting(false);
    }
  };

  const handleConfirmSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!confirmTarget || !selectedCustomerId) return;
    const initialAllocations = Object.entries(initAllocAmounts)
      .map(([receivableId, val]) => ({ receivableId, allocatedAmount: Number(val) }))
      .filter((x) => Number.isFinite(x.allocatedAmount) && x.allocatedAmount > 0);

    setConfirming(true);
    setActionError(null);
    try {
      await confirmCustomerReceipt(tenantId, entityId, {
        bankStatementLineId: confirmTarget.bankStatementLineId,
        customerPartnerId: selectedCustomerId,
        receiptDate,
        idempotencyKey: `rct-${confirmTarget.bankStatementLineId.slice(0, 8)}-${Date.now()}`,
        notes: receiptNotes.trim() || null,
        initialAllocations: initialAllocations.length > 0 ? initialAllocations : null,
      });
      setConfirmTarget(null);
      await loadSnapshot();
      setActiveTab("receipts");
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setConfirming(false);
    }
  };

  const handleAllocateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!allocTarget) return;
    const allocations = Object.entries(extraAllocAmounts)
      .map(([receivableId, val]) => ({ receivableId, allocatedAmount: Number(val) }))
      .filter((x) => Number.isFinite(x.allocatedAmount) && x.allocatedAmount > 0);
    if (allocations.length === 0) return;

    setAllocating(true);
    setActionError(null);
    try {
      await allocateCustomerReceipt(tenantId, entityId, allocTarget.receiptId, { allocations });
      setAllocTarget(null);
      await loadSnapshot();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setAllocating(false);
    }
  };

  const handleReverseSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reverseTarget || !reverseReason.trim()) return;
    setReversing(true);
    setActionError(null);
    try {
      await reverseCustomerReceipt(tenantId, entityId, reverseTarget.receiptId, {
        reversalDate: reverseDate,
        reason: reverseReason.trim(),
      });
      setReverseTarget(null);
      await loadSnapshot();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setReversing(false);
    }
  };

  if (loading) {
    return (
      <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
        <p className="text-sm text-[var(--text-secondary)]">{rt.loading}</p>
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
          <ShieldAlert className="mt-0.5 h-5 w-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div>
            <h1 className="text-base font-semibold">{rt.accessDeniedHeading}</h1>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">{rt.accessDeniedBody}</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 flex-1 space-y-1">
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 text-xs font-medium text-[var(--accent-primary)] hover:underline"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{rt.backToEntityLink}</span>
          </Link>
          <h1 className="text-xl font-bold tracking-tight text-[var(--text-primary)]">{rt.heading}</h1>
          <p className="max-w-3xl text-xs text-[var(--text-secondary)]">{rt.lead}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2 lg:shrink-0">
          <button
            type="button"
            onClick={() => void handleRefresh()}
            disabled={refreshing}
            className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`} strokeWidth={1.75} aria-hidden="true" />
            <span>{refreshing ? rt.refreshing : rt.refreshButton}</span>
          </button>
          {canManage ? (
            <button
              type="button"
              id="ar-open-import-btn"
              onClick={openImportDrawer}
              className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <Upload className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>{rt.bankPanel.importCsvBtn}</span>
            </button>
          ) : null}
        </div>
      </div>

      {/* Read-only notice */}
      {!canManage ? (
        <div
          id="ar-readonly-banner"
          className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-subtle)] px-4 py-3 text-xs text-[var(--text-secondary)]"
        >
          <Eye className="mt-0.5 h-4 w-4 shrink-0 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <span>{rt.readOnlyBanner}</span>
        </div>
      ) : null}

      {/* OD-09 Governance Banner */}
      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)]">
        <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs text-[var(--status-warn-text)]">
          <div className="flex items-start gap-2">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span className="font-medium">{rt.od09PolicyBanner}</span>
          </div>
          <button
            type="button"
            onClick={() => setShowPolicy((v) => !v)}
            className="inline-flex min-h-9 shrink-0 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <span>{showPolicy ? rt.policyToggleHide : rt.policyToggleShow}</span>
            {showPolicy ? (
              <ChevronUp className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            ) : (
              <ChevronDown className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            )}
          </button>
        </div>
        {showPolicy ? (
          <div className="grid grid-cols-1 divide-y divide-[var(--status-warn-border)] border-t border-[var(--status-warn-border)] bg-[var(--bg-surface)] md:grid-cols-4 md:divide-x md:divide-y-0">
            <div className="p-3.5">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{rt.policyCards.rule1Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{rt.policyCards.rule1Body}</p>
            </div>
            <div className="p-3.5">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{rt.policyCards.rule2Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{rt.policyCards.rule2Body}</p>
            </div>
            <div className="p-3.5">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{rt.policyCards.rule3Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{rt.policyCards.rule3Body}</p>
            </div>
            <div className="p-3.5">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{rt.policyCards.rule4Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{rt.policyCards.rule4Body}</p>
            </div>
          </div>
        ) : null}
      </section>

      {loadError ? (
        <div role="alert" className="flex items-start gap-2 rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-xs text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{loadError}</span>
        </div>
      ) : null}
      {actionError ? (
        <div id="ar-action-error" role="alert" className="flex items-start gap-2 rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-xs text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{actionError}</span>
        </div>
      ) : null}

      {/* 4-Metric Hairline KPI Strip with GL 1200 Reconciliation Proof */}
      <section
        aria-label={rt.statsAriaLabel}
        className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] sm:grid-cols-2 md:grid-cols-4 md:divide-x md:divide-y-0"
      >
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">{rt.stats.totalOpenReceivables}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(aging?.totalOpenReceivables ?? 0)}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">{rt.stats.totalUnappliedReceipts}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--status-warn-text)]">
            {formatIdr(aging?.totalUnappliedReceipts ?? 0)}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">{rt.stats.netArSubledger}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(aging?.netArSubledger ?? 0)}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">
            {rt.stats.glArControlBalance}
          </div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(aging?.glArControlBalance ?? 0)}
          </div>
          <div className="mt-1.5">
            <span
              id="ar-gl-reconciled-badge"
              className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-1.5 py-0.5 text-[10px] font-semibold ${
                aging?.isGlReconciled
                  ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                  : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
              }`}
            >
              {aging?.isGlReconciled ? (
                <CheckCircle2 className="h-3 w-3" strokeWidth={1.75} aria-hidden="true" />
              ) : (
                <AlertCircle className="h-3 w-3" strokeWidth={1.75} aria-hidden="true" />
              )}
              <span>{aging?.isGlReconciled ? rt.stats.glReconciledBadge : rt.stats.glVarianceBadge}</span>
            </span>
          </div>
        </div>
      </section>

      {/* Inline CSV Import Drawer */}
      {showImportDrawer && canManage ? (
        <section id="ar-import-drawer" className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>{rt.bankPanel.importDrawerTitle}</span>
            <button
              type="button"
              id="ar-import-cancel-btn"
              onClick={() => setShowImportDrawer(false)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {rt.bankPanel.cancelBtn}
            </button>
          </div>
          <form onSubmit={handleImportSubmit} className="space-y-3 p-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-12">
              <label className="block space-y-1 md:col-span-5">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.bankPanel.bankAccountLabel}</span>
                <select
                  id="ar-import-bank"
                  value={importBankAccountId}
                  onChange={(e) => setImportBankAccountId(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  {bankAccounts.map((b) => (
                    <option key={b.bankAccountId} value={b.bankAccountId}>
                      {b.accountCode} · {b.bankName} ({b.accountNumberMasked})
                    </option>
                  ))}
                </select>
              </label>
              <label className="block space-y-1 md:col-span-4">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.bankPanel.stmtRefLabel}</span>
                <input
                  id="ar-import-ref"
                  type="text"
                  required
                  value={importStmtRef}
                  placeholder="STMT-IN-2026-11"
                  onChange={(e) => setImportStmtRef(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-3">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.bankPanel.stmtDateLabel}</span>
                <input
                  id="ar-import-date"
                  type="date"
                  required
                  value={importStmtDate}
                  onChange={(e) => setImportStmtDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-12">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.bankPanel.csvLabel}</span>
                <textarea
                  id="ar-import-csv"
                  rows={3}
                  required
                  value={importCsv}
                  onChange={(e) => setImportCsv(e.target.value)}
                  className="font-code w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] p-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
            </div>
            <div className="flex justify-end border-t border-[var(--border-hairline)] pt-3">
              <button
                type="submit"
                id="ar-import-submit-btn"
                disabled={importing || !importBankAccountId}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <Upload className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{importing ? rt.bankPanel.importing : rt.bankPanel.submitImportCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Inline Confirm Receipt + Initial Multi-Invoice Allocation Drawer */}
      {confirmTarget && canManage ? (
        <section id="ar-confirm-drawer" className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {rt.bankPanel.confirmDrawerTitlePrefix}{" "}
              <span className="font-code whitespace-nowrap">{confirmTarget.bankReference}</span> (
              <span className="font-code tabular-nums">{formatIdr(confirmTarget.unmatchedAmount)}</span>)
            </span>
            <button
              type="button"
              id="ar-confirm-cancel-btn"
              onClick={() => setConfirmTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {rt.bankPanel.cancelBtn}
            </button>
          </div>
          <form onSubmit={handleConfirmSubmit} className="space-y-3 p-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-12">
              <label className="block space-y-1 md:col-span-5">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.bankPanel.customerLabel}</span>
                <select
                  id="ar-confirm-customer"
                  value={selectedCustomerId}
                  onChange={(e) => {
                    setSelectedCustomerId(e.target.value);
                    setInitAllocAmounts({});
                  }}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  {customers.map((c) => (
                    <option key={c.partnerId} value={c.partnerId}>
                      {c.partnerCode} · {c.legalName}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block space-y-1 md:col-span-3">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.bankPanel.receiptDateLabel}</span>
                <input
                  id="ar-confirm-date"
                  type="date"
                  required
                  value={receiptDate}
                  onChange={(e) => setReceiptDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-4">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.bankPanel.notesLabel}</span>
                <input
                  id="ar-confirm-notes"
                  type="text"
                  value={receiptNotes}
                  placeholder={rt.bankPanel.notesPlaceholder}
                  onChange={(e) => setReceiptNotes(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
            </div>

            {confirmCustomerOpenReceivables.length > 0 ? (
              <div className="divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)]">
                <div className="py-2 text-[11px] font-semibold text-[var(--text-secondary)]">
                  {rt.bankPanel.initialAllocHeader}
                </div>
                {confirmCustomerOpenReceivables.map((rec, idx) => (
                  <div key={rec.receivableId} className="flex flex-wrap items-center justify-between gap-2 py-2 text-xs">
                    <div>
                      <span className="font-code font-bold text-[var(--text-primary)]">{rec.invoiceNumber}</span>{" "}
                      <span className="font-code text-[var(--text-secondary)]">
                        ({rec.receivableNumber} · {rt.bankPanel.openInvoicePrefix} {formatIdr(rec.openAmount)})
                      </span>
                    </div>
                    <input
                      id={`ar-init-alloc-${idx}`}
                      type="number"
                      step="0.01"
                      min="0"
                      max={Number(rec.openAmount)}
                      placeholder="0"
                      value={initAllocAmounts[rec.receivableId] ?? ""}
                      onChange={(e) =>
                        setInitAllocAmounts((prev) => ({ ...prev, [rec.receivableId]: e.target.value }))
                      }
                      className="font-code min-h-9 w-36 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums text-[var(--text-primary)]"
                    />
                  </div>
                ))}
              </div>
            ) : (
              <div className="border-t border-[var(--border-hairline)] pt-2.5 text-xs text-[var(--text-secondary)]">
                {rt.bankPanel.noOpenInvoicesForCustomer}
              </div>
            )}

            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-hairline)] pt-3">
              <div className="flex flex-wrap items-center gap-4 text-xs">
                <span>
                  <span className="text-[var(--text-secondary)]">{rt.bankPanel.receivedPrefix} </span>
                  <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                    {formatIdr(confirmTotals.received)}
                  </span>
                </span>
                <span>
                  <span className="text-[var(--text-secondary)]">{rt.bankPanel.allocatedPrefix} </span>
                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatIdr(confirmTotals.allocated)}
                  </span>
                </span>
                <span>
                  <span className="text-[var(--text-secondary)]">{rt.bankPanel.unappliedPrefix} </span>
                  <span className="font-code font-semibold tabular-nums text-[var(--status-warn-text)]">
                    {formatIdr(confirmTotals.unapplied)}
                  </span>
                </span>
              </div>
              <button
                type="submit"
                id="ar-confirm-submit-btn"
                disabled={confirming || !selectedCustomerId || confirmTotals.unapplied < 0}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <Wallet className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{confirming ? rt.bankPanel.confirming : rt.bankPanel.confirmReceiptCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Inline Allocate Remaining Unapplied Funds Drawer */}
      {allocTarget && canManage ? (
        <section id="ar-alloc-drawer" className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {rt.receiptsPanel.allocateDrawerTitlePrefix}{" "}
              <span className="font-code whitespace-nowrap">{allocTarget.receiptNumber}</span> (
              {rt.bankPanel.unappliedPrefix}{" "}
              <span className="font-code tabular-nums">{formatIdr(allocTarget.unappliedAmount)}</span>)
            </span>
            <button
              type="button"
              id="ar-alloc-cancel-btn"
              onClick={() => setAllocTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {rt.bankPanel.cancelBtn}
            </button>
          </div>
          <form onSubmit={handleAllocateSubmit} className="space-y-3 p-4">
            {allocCustomerOpenReceivables.length === 0 ? (
              <p className="text-xs text-[var(--text-secondary)]">{rt.receiptsPanel.noOpenInvoicesToAllocate}</p>
            ) : (
              <div className="divide-y divide-[var(--border-hairline)]">
                {allocCustomerOpenReceivables.map((rec, idx) => (
                  <div key={rec.receivableId} className="flex flex-wrap items-center justify-between gap-2 py-2 text-xs">
                    <div>
                      <span className="font-code font-bold text-[var(--text-primary)]">{rec.invoiceNumber}</span>{" "}
                      <span className="font-code text-[var(--text-secondary)]">
                        ({rt.bankPanel.openInvoicePrefix} {formatIdr(rec.openAmount)})
                      </span>
                    </div>
                    <input
                      id={`ar-extra-alloc-${idx}`}
                      type="number"
                      step="0.01"
                      min="0"
                      max={Math.min(Number(rec.openAmount), Number(allocTarget.unappliedAmount))}
                      placeholder="0"
                      value={extraAllocAmounts[rec.receivableId] ?? ""}
                      onChange={(e) =>
                        setExtraAllocAmounts((prev) => ({ ...prev, [rec.receivableId]: e.target.value }))
                      }
                      className="font-code min-h-9 w-36 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums text-[var(--text-primary)]"
                    />
                  </div>
                ))}
              </div>
            )}
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-hairline)] pt-3">
              <div className="flex flex-wrap items-center gap-4 text-xs">
                <span>
                  <span className="text-[var(--text-secondary)]">{rt.bankPanel.unappliedPrefix} </span>
                  <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                    {formatIdr(allocTotals.available)}
                  </span>
                </span>
                <span>
                  <span className="text-[var(--text-secondary)]">{rt.bankPanel.allocatedPrefix} </span>
                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatIdr(allocTotals.allocated)}
                  </span>
                </span>
                <span>
                  <span className="text-[var(--text-secondary)]">{rt.receiptsPanel.unappliedPrefix} </span>
                  <span
                    className={`font-code font-semibold tabular-nums ${
                      allocTotals.remaining < 0 ? "text-[var(--status-danger-text)]" : "text-[var(--status-warn-text)]"
                    }`}
                  >
                    {formatIdr(allocTotals.remaining)}
                  </span>
                </span>
              </div>
              <button
                type="submit"
                id="ar-alloc-submit-btn"
                disabled={allocating || allocTotals.allocated <= 0 || allocTotals.remaining < 0}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{allocating ? rt.receiptsPanel.allocating : rt.receiptsPanel.confirmAllocateCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Inline Reversal Drawer */}
      {reverseTarget && canManage ? (
        <section id="ar-reverse-drawer" className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {rt.receiptsPanel.reverseDrawerTitlePrefix}{" "}
              <span className="font-code whitespace-nowrap">{reverseTarget.receiptNumber}</span>
            </span>
            <button
              type="button"
              id="ar-reverse-cancel-btn"
              onClick={() => setReverseTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {rt.bankPanel.cancelBtn}
            </button>
          </div>
          <form onSubmit={handleReverseSubmit} className="grid grid-cols-1 gap-3 p-4 md:grid-cols-12">
            <label className="block space-y-1 md:col-span-3">
              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.receiptsPanel.reverseDateLabel}</span>
              <input
                id="ar-reverse-date"
                type="date"
                required
                value={reverseDate}
                onChange={(e) => setReverseDate(e.target.value)}
                className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
              />
            </label>
            <label className="block space-y-1 md:col-span-6">
              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{rt.receiptsPanel.reverseReasonLabel}</span>
              <input
                id="ar-reverse-reason"
                type="text"
                required
                value={reverseReason}
                placeholder={rt.receiptsPanel.reverseReasonPlaceholder}
                onChange={(e) => setReverseReason(e.target.value)}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
              />
            </label>
            <div className="flex items-end justify-end md:col-span-3">
              <button
                type="submit"
                id="ar-reverse-submit-btn"
                disabled={reversing || !reverseReason.trim()}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <RotateCcw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{reversing ? rt.receiptsPanel.reversing : rt.receiptsPanel.confirmReverseCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Responsive Tablist (unified neutral segmented tray) */}
      <div
        role="tablist"
        aria-label={rt.tabListLabel}
        className="grid grid-cols-1 gap-1.5 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-1 sm:flex sm:flex-wrap"
      >
        <button
          type="button"
          role="tab"
          id="ar-tab-bank"
          aria-selected={activeTab === "bank"}
          aria-controls="ar-panel-bank"
          onClick={() => setActiveTab("bank")}
          className={`min-h-9 rounded-[var(--radius-sm)] px-3 py-1.5 text-left text-xs font-medium transition-colors sm:text-center ${
            activeTab === "bank"
              ? "border border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-primary)]"
              : "border border-transparent text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
          }`}
        >
          {rt.tabs.bankAndReceipts} ({inboundLines.length})
        </button>
        <button
          type="button"
          role="tab"
          id="ar-tab-receipts"
          aria-selected={activeTab === "receipts"}
          aria-controls="ar-panel-receipts"
          onClick={() => setActiveTab("receipts")}
          className={`min-h-9 rounded-[var(--radius-sm)] px-3 py-1.5 text-left text-xs font-medium transition-colors sm:text-center ${
            activeTab === "receipts"
              ? "border border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-primary)]"
              : "border border-transparent text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
          }`}
        >
          {rt.tabs.allocations} ({receipts.length})
        </button>
        <button
          type="button"
          role="tab"
          id="ar-tab-aging"
          aria-selected={activeTab === "aging"}
          aria-controls="ar-panel-aging"
          onClick={() => setActiveTab("aging")}
          className={`min-h-9 rounded-[var(--radius-sm)] px-3 py-1.5 text-left text-xs font-medium transition-colors sm:text-center ${
            activeTab === "aging"
              ? "border border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-primary)]"
              : "border border-transparent text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)] hover:text-[var(--text-primary)]"
          }`}
        >
          {rt.tabs.aging} ({aging?.customers.length ?? 0})
        </button>
      </div>

      {/* TAB 1: INBOUND BANK CREDIT LINES */}
      {activeTab === "bank" ? (
        <div id="ar-panel-bank" role="tabpanel" aria-labelledby="ar-tab-bank">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {rt.bankPanel.title}
            </div>
            {inboundLines.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{rt.bankPanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ar-bank-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{rt.bankPanel.colDateRef}</th>
                      <th scope="col" className="py-2 md:px-2">{rt.bankPanel.colBankCounterparty}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.bankPanel.colAmount}</th>
                      <th scope="col" className="py-2 md:px-2">{rt.bankPanel.colStatus}</th>
                      {canManage ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{rt.bankPanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {inboundLines.map((line) => {
                      const canConfirm = canManage && line.status === "UNMATCHED" && Number(line.unmatchedAmount) > 0;
                      return (
                        <tr key={line.bankStatementLineId} className="block p-3 md:table-row md:p-0">
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.bankPanel.colDateRef}</span>
                            <div className="font-code whitespace-nowrap font-bold text-[var(--text-primary)]">{line.bankReference}</div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">{line.valueDate}</div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.bankPanel.colBankCounterparty}</span>
                            <div className="font-medium text-[var(--text-primary)]">
                              <span className="font-code">{line.bankAccountCode}</span> · {line.counterpartyName || "—"}
                            </div>
                            <div className="text-[11px] text-[var(--text-secondary)]">{line.description}</div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.bankPanel.colAmount}</span>
                            <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                              {formatIdr(line.amount)}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.bankPanel.colStatus}</span>
                            <StatusBadge status={line.status} label={rt.bankStatusLabels[line.status] ?? line.status} />
                          </td>
                          {canManage ? (
                            <td className={`${canConfirm ? "block py-1" : "hidden"} md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right`}>
                              {canConfirm ? (
                                <>
                                  <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.bankPanel.colAction}</span>
                                  <div className="mt-1 flex flex-wrap items-center justify-start gap-1.5 md:mt-0 md:justify-end">
                                    <button
                                      type="button"
                                      data-testid={`confirm-bank-${line.bankReference}`}
                                      onClick={() => openConfirmDrawer(line)}
                                      className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                                    >
                                      <Wallet className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                      <span>{rt.bankPanel.confirmReceiptBtn}</span>
                                    </button>
                                  </div>
                                </>
                              ) : null}
                            </td>
                          ) : null}
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

      {/* TAB 2: RECEIPTS & ALLOCATIONS */}
      {activeTab === "receipts" ? (
        <div id="ar-panel-receipts" role="tabpanel" aria-labelledby="ar-tab-receipts">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {rt.receiptsPanel.title}
            </div>
            {receipts.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{rt.receiptsPanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ar-receipts-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{rt.receiptsPanel.colReceiptDoc}</th>
                      <th scope="col" className="py-2 md:px-2">{rt.receiptsPanel.colCustomerBank}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.receiptsPanel.colAmounts}</th>
                      <th scope="col" className="py-2 md:px-2">{rt.receiptsPanel.colAllocations}</th>
                      <th scope="col" className="py-2 md:px-2">{rt.receiptsPanel.colStatusGl}</th>
                      {canManage ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{rt.receiptsPanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {receipts.map((rct) => {
                      const canAllocRow = canManage && rct.status !== "REVERSED" && Number(rct.unappliedAmount) > 0;
                      const canRevRow = canManage && rct.status !== "REVERSED";
                      const hasRowAction = canAllocRow || canRevRow;
                      return (
                        <tr key={rct.receiptId} className="block p-3 md:table-row md:p-0">
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.receiptsPanel.colReceiptDoc}</span>
                            <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">{rct.receiptNumber}</div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">{rct.receiptDate}</div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.receiptsPanel.colCustomerBank}</span>
                            <div className="font-medium text-[var(--text-primary)]">
                              <span className="font-code">{rct.customerCode}</span> · {rct.customerName}
                            </div>
                            <div className="font-code text-[11px] text-[var(--text-secondary)]">
                              {rt.receiptsPanel.bankRefPrefix} {rct.bankReference}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.receiptsPanel.colAmounts}</span>
                            <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                              {formatIdr(rct.receiptAmount)}
                            </div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {rt.receiptsPanel.allocatedPrefix} {formatIdr(rct.allocatedAmount)}
                            </div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--status-warn-text)]">
                              {rt.receiptsPanel.unappliedPrefix} {formatIdr(rct.unappliedAmount)}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.receiptsPanel.colAllocations}</span>
                            {rct.allocations.length === 0 ? (
                              <span className="text-[11px] text-[var(--text-muted)]">—</span>
                            ) : (
                              <div className="space-y-0.5">
                                {rct.allocations.map((a) => (
                                  <div key={a.allocationId} className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-primary)]">
                                    <span className="font-semibold">{a.invoiceNumber}</span>: {formatIdr(a.allocatedAmount)}
                                  </div>
                                ))}
                              </div>
                            )}
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.receiptsPanel.colStatusGl}</span>
                            <div>
                              <StatusBadge status={rct.status} label={rt.receiptStatusLabels[rct.status] ?? rct.status} />
                            </div>
                            <div className="font-code mt-0.5 whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {rt.receiptsPanel.journalPrefix} {rct.glJournalNumber}
                            </div>
                            {rct.reversalGlJournalNumber ? (
                              <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--status-danger-text)]">
                                {rt.receiptsPanel.reversalJournalPrefix} {rct.reversalGlJournalNumber}
                              </div>
                            ) : null}
                          </td>
                          {canManage ? (
                            <td className={`${hasRowAction ? "block py-1" : "hidden"} md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right`}>
                              {hasRowAction ? (
                                <>
                                  <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.receiptsPanel.colAction}</span>
                                  <div className="mt-1 flex flex-wrap items-center justify-start gap-1.5 md:mt-0 md:justify-end">
                                    {canAllocRow ? (
                                      <button
                                        type="button"
                                        data-testid={`alloc-rct-${rct.receiptNumber}`}
                                        onClick={() => openAllocDrawer(rct)}
                                        className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-2.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                                      >
                                        <span>{rt.receiptsPanel.allocateBtn}</span>
                                      </button>
                                    ) : null}
                                    {canRevRow ? (
                                      <button
                                        type="button"
                                        data-testid={`reverse-rct-${rct.receiptNumber}`}
                                        onClick={() => openReverseDrawer(rct)}
                                        className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                                      >
                                        <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                        <span>{rt.receiptsPanel.reverseBtn}</span>
                                      </button>
                                    ) : null}
                                  </div>
                                </>
                              ) : null}
                            </td>
                          ) : null}
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

      {/* TAB 3: AR AGING SCHEDULE */}
      {activeTab === "aging" ? (
        <div id="ar-panel-aging" role="tabpanel" aria-labelledby="ar-tab-aging">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              <span>{rt.agingPanel.title}</span>
              <label className="inline-flex items-center gap-2 font-normal text-[var(--text-secondary)]">
                <span>{rt.agingPanel.asOfLabel}</span>
                <input
                  id="ar-aging-asof"
                  type="date"
                  value={asOfDate}
                  onChange={(e) => setAsOfDate(e.target.value)}
                  className="font-code min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
            </div>
            {!aging || aging.customers.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{rt.agingPanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ar-aging-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{rt.agingPanel.colCustomer}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.agingPanel.colCurrent}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.agingPanel.col1To30}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.agingPanel.col31To60}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.agingPanel.col61To90}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.agingPanel.colOver90}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{rt.agingPanel.colUnapplied}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{rt.agingPanel.colNetAr}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {aging.customers.map((row) => (
                      <tr key={row.customerPartnerId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colCustomer}</span>
                          <div className="font-medium text-[var(--text-primary)]">
                            <span className="font-code">{row.customerCode}</span> · {row.customerName}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right font-code whitespace-nowrap tabular-nums">
                          <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colCurrent}:</span>
                          {formatIdr(row.currentAmount)}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right font-code whitespace-nowrap tabular-nums">
                          <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.col1To30}:</span>
                          {formatIdr(row.days1To30Amount)}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right font-code whitespace-nowrap tabular-nums">
                          <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.col31To60}:</span>
                          {formatIdr(row.days31To60Amount)}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right font-code whitespace-nowrap tabular-nums">
                          <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.col61To90}:</span>
                          {formatIdr(row.days61To90Amount)}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right font-code whitespace-nowrap tabular-nums">
                          <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colOver90}:</span>
                          {formatIdr(row.over90Amount)}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right font-code whitespace-nowrap tabular-nums text-[var(--status-warn-text)]">
                          <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colUnapplied}:</span>
                          {formatIdr(row.unappliedReceiptsAmount)}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                          <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colNetAr}:</span>
                          {formatIdr(row.netReceivableBalance)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot className="block border-t border-[var(--border-strong)] bg-[var(--bg-subtle)] font-semibold md:table-footer-group">
                    <tr className="block p-3 md:table-row md:p-0">
                      <td className="block py-1 font-semibold text-[var(--text-primary)] md:table-cell md:py-2.5 md:pl-4 md:pr-2">{rt.agingPanel.totalRowLabel}</td>
                      <td className="block py-1 font-code whitespace-nowrap tabular-nums md:table-cell md:px-2 md:py-2.5 md:text-right">
                        <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colCurrent}:</span>
                        {formatIdr(aging.currentTotal)}
                      </td>
                      <td className="block py-1 font-code whitespace-nowrap tabular-nums md:table-cell md:px-2 md:py-2.5 md:text-right">
                        <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.col1To30}:</span>
                        {formatIdr(aging.days1To30Total)}
                      </td>
                      <td className="block py-1 font-code whitespace-nowrap tabular-nums md:table-cell md:px-2 md:py-2.5 md:text-right">
                        <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.col31To60}:</span>
                        {formatIdr(aging.days31To60Total)}
                      </td>
                      <td className="block py-1 font-code whitespace-nowrap tabular-nums md:table-cell md:px-2 md:py-2.5 md:text-right">
                        <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.col61To90}:</span>
                        {formatIdr(aging.days61To90Total)}
                      </td>
                      <td className="block py-1 font-code whitespace-nowrap tabular-nums md:table-cell md:px-2 md:py-2.5 md:text-right">
                        <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colOver90}:</span>
                        {formatIdr(aging.over90Total)}
                      </td>
                      <td className="block py-1 font-code whitespace-nowrap tabular-nums text-[var(--status-warn-text)] md:table-cell md:px-2 md:py-2.5 md:text-right">
                        <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colUnapplied}:</span>
                        {formatIdr(aging.totalUnappliedReceipts)}
                      </td>
                      <td className="block py-1 font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)] md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                        <span className="mr-2 font-sans font-medium text-[var(--text-secondary)] md:hidden">{rt.agingPanel.colNetAr}:</span>
                        {formatIdr(aging.netArSubledger)}
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}
    </div>
  );
}
