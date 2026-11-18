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
  CreditCard,
  Eye,
  FileText,
  Plus,
  RefreshCw,
  RotateCcw,
  ShieldCheck,
  Upload,
} from "lucide-react";
import {
  ApiProblemError,
  BankStatementLineDto,
  createSupplierPaymentProposal,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchPaymentsOverview,
  fetchSupplierPaymentDetail,
  importBankStatement,
  PaymentsOverviewResponse,
  reconcileBankStatementLine,
  reverseBankPaymentMatch,
  SupplierPaymentDetailResponse,
  upsertBankAccount,
} from "@/lib/api/client";
import { useSessionUser } from "@/components/AppShell";
import { PaymentStatusBadge, SupplierPaymentDetailSheet, todayJakartaDate } from "@/components/payments/SupplierPaymentDetailSheet";
import { formatIdr, formatJakartaDateTime } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";

type PaymentsTab = "payments" | "reconciliation" | "accounts";

function makeIdempotencyKey(prefix: string): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

export default function PaymentsPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <PaymentsSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}

function PaymentsSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const user = useSessionUser();
  const { locale, t } = useLocale();
  const pt = t.payments;

  const [workspace, setWorkspace] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [overview, setOverview] = useState<PaymentsOverviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const [activeTab, setActiveTab] = useState<PaymentsTab>("payments");
  const [stagesOpen, setStagesOpen] = useState(false);
  const [composerOpen, setComposerOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);

  const [selectedDetail, setSelectedDetail] = useState<SupplierPaymentDetailResponse | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  // Composer state
  const [compBankId, setCompBankId] = useState("");
  const [compPayableId, setCompPayableId] = useState("");
  const [compAmount, setCompAmount] = useState("");
  const [compBenBank, setCompBenBank] = useState("");
  const [compBenAccount, setCompBenAccount] = useState("");
  const [compBenHolder, setCompBenHolder] = useState("");
  const [compMethod, setCompMethod] = useState("BANK_TRANSFER_IDR");
  const [compDate, setCompDate] = useState(todayJakartaDate);
  const [compNotes, setCompNotes] = useState("");
  const [creatingProposal, setCreatingProposal] = useState(false);

  // Statement import state
  const [stmtBankId, setStmtBankId] = useState("");
  const [stmtRef, setStmtRef] = useState("");
  const [stmtDate, setStmtDate] = useState(todayJakartaDate);
  const [stmtOpenBal, setStmtOpenBal] = useState("");
  const [stmtCloseBal, setStmtCloseBal] = useState("");
  const [stmtCsv, setStmtCsv] = useState("");
  const [importingStmt, setImportingStmt] = useState(false);

  // Reconcile inline drawer state
  const [matchingLine, setMatchingLine] = useState<BankStatementLineDto | null>(null);
  const [recPaymentId, setRecPaymentId] = useState("");
  const [recDate, setRecDate] = useState(todayJakartaDate);
  const [recAmount, setRecAmount] = useState("");
  const [reconciling, setReconciling] = useState(false);
  const [reversingId, setReversingId] = useState<string | null>(null);

  // Bank account form state
  const [bankCode, setBankCode] = useState("");
  const [bankName, setBankName] = useState("");
  const [branchName, setBranchName] = useState("");
  const [accountNumber, setAccountNumber] = useState("");
  const [holderName, setHolderName] = useState("");
  const [glCashId, setGlCashId] = useState("");
  const [glClearingId, setGlClearingId] = useState("");
  const [savingBank, setSavingBank] = useState(false);

  const mapError = useCallback(
    (err: unknown): string => {
      const problem = err as Partial<ApiProblemError>;
      if (problem && typeof problem === "object" && (typeof problem.errorCode === "string" || typeof problem.detail === "string")) {
        const mapped = problem.errorCode ? pt.errors[problem.errorCode] : undefined;
        if (mapped) return mapped;
        return problem.detail || problem.title || t.login.errors.upstream_unavailable;
      }
      return t.login.errors.upstream_unavailable;
    },
    [pt.errors, t.login.errors.upstream_unavailable],
  );

  const applyOverviewDefaults = useCallback((ws: EntityWorkspaceDetailResponse, ov: PaymentsOverviewResponse) => {
    setWorkspace(ws);
    setOverview(ov);
    const activeBanks = ov.bankAccounts.filter((b) => b.status === "ACTIVE");
    if (activeBanks.length > 0) {
      setCompBankId((prev) => prev || activeBanks[0].bankAccountId);
      setStmtBankId((prev) => prev || activeBanks[0].bankAccountId);
    }

    const eligiblePayables = ov.openPayables.filter(
      (p) => p.paymentBlockStatus !== "PAYMENT_BLOCKED" && Number(p.remainingOpenAmount) > 0,
    );
    if (eligiblePayables.length > 0) {
      setCompPayableId((prev) => prev || eligiblePayables[0].payableItemId);
      setCompAmount((prev) => prev || String(eligiblePayables[0].remainingOpenAmount));
    }

    const cashOpts = ov.cashAndClearingAccounts.filter((a) => a.accountCategory === "CASH_BANK");
    const clrOpts = ov.cashAndClearingAccounts.filter((a) => a.accountCategory === "BANK_OUTBOUND_CLEARING");
    if (cashOpts.length > 0) setGlCashId((prev) => prev || cashOpts[0].accountId);
    if (clrOpts.length > 0) setGlClearingId((prev) => prev || clrOpts[0].accountId);
  }, []);

  const loadSnapshot = useCallback(async () => {
    const [ws, ov] = await Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchPaymentsOverview(tenantId, entityId),
    ]);
    return { ws, ov };
  }, [tenantId, entityId]);

  const loadData = useCallback(
    async (silent = false) => {
      if (!tenantId || !entityId) return;
      if (silent) setRefreshing(true);
      else setLoading(true);
      setLoadError(null);
      setAccessDenied(false);

      try {
        const { ws, ov } = await loadSnapshot();
        applyOverviewDefaults(ws, ov);
      } catch (err) {
        const problem = err as Partial<ApiProblemError>;
        if (problem && typeof problem.status === "number") {
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
    [tenantId, entityId, router, loadSnapshot, applyOverviewDefaults, t.login.errors.upstream_unavailable],
  );

  useEffect(() => {
    if (!tenantId || !entityId) return;
    let active = true;

    loadSnapshot()
      .then(({ ws, ov }) => {
        if (!active) return;
        applyOverviewDefaults(ws, ov);
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
  }, [tenantId, entityId, loadSnapshot, applyOverviewDefaults, router, t.login.errors.upstream_unavailable]);

  const perms = useMemo(() => new Set(workspace?.effectivePermissions ?? []), [workspace]);
  const canPrepare = perms.has("ap.manage") || perms.has("ap.post") || perms.has("finance.journal.post");
  const canRelease = perms.has("ap.approve") || perms.has("ap.post") || perms.has("finance.journal.post");
  const canReconcile = perms.has("finance.journal.post") || perms.has("ap.post");
  const canConfigureBank = perms.has("finance.config.manage");
  const isReadOnly = !canPrepare && !canRelease && !canReconcile && !canConfigureBank;

  const eligibleOpenPayables = useMemo(() => {
    if (!overview) return [];
    return overview.openPayables.filter(
      (p) => p.paymentBlockStatus !== "PAYMENT_BLOCKED" && Number(p.remainingOpenAmount) > 0,
    );
  }, [overview]);

  const selectedPayable = useMemo(
    () => eligibleOpenPayables.find((p) => p.payableItemId === compPayableId) ?? null,
    [eligibleOpenPayables, compPayableId],
  );

  const kpis = useMemo(() => {
    if (!overview) {
      return { openPayablesTotal: 0, pendingReleaseTotal: 0, outboundClearingOpen: 0, reconciledTotal: 0 };
    }
    const openPayablesTotal = overview.openPayables.reduce((s, p) => s + Number(p.remainingOpenAmount || 0), 0);
    const pendingReleaseTotal = overview.payments
      .filter((p) => p.status === "DRAFT" || p.status === "PENDING_RELEASE")
      .reduce((s, p) => s + Number(p.totalAllocatedAmount || 0), 0);
    const outboundClearingOpen = Number(overview.clearingProof.subledgerOpenClearingBalance || 0);
    const reconciledTotal = overview.matches
      .filter((m) => m.status === "POSTED")
      .reduce((s, m) => s + Number(m.matchedAmount || 0), 0);
    return { openPayablesTotal, pendingReleaseTotal, outboundClearingOpen, reconciledTotal };
  }, [overview]);

  const openPaymentDetail = async (paymentId: string) => {
    setActionError(null);
    try {
      const d = await fetchSupplierPaymentDetail(tenantId, entityId, paymentId);
      setSelectedDetail(d);
    } catch (err) {
      setActionError(mapError(err));
    }
  };

  const handleDetailUpdated = async (updated: SupplierPaymentDetailResponse) => {
    setSelectedDetail(updated);
    await loadData(true);
  };

  const handleCreateProposal = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPayable || !compBankId) return;
    const amt = Number(compAmount);
    if (!Number.isFinite(amt) || amt <= 0) {
      setActionError(pt.errors.allocation_exceeds_open_payable);
      return;
    }

    setCreatingProposal(true);
    setActionError(null);
    try {
      const created = await createSupplierPaymentProposal(tenantId, entityId, {
        bankAccountId: compBankId,
        supplierPartnerId: "00000000-0000-0000-0000-000000000000",
        beneficiaryBankName: compBenBank.trim(),
        beneficiaryAccountNumber: compBenAccount.trim(),
        beneficiaryAccountHolder: compBenHolder.trim(),
        paymentMethod: compMethod,
        paymentDate: compDate,
        notes: compNotes.trim() || null,
        allocations: [
          {
            payableItemId: selectedPayable.payableItemId,
            allocatedAmount: amt,
            notes: null,
          },
        ],
      });
      setComposerOpen(false);
      setSelectedDetail(created);
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setCreatingProposal(false);
    }
  };

  const handleImportStatement = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!stmtBankId || !stmtRef.trim() || !stmtCsv.trim()) return;
    setImportingStmt(true);
    setActionError(null);
    try {
      await importBankStatement(tenantId, entityId, {
        bankAccountId: stmtBankId,
        statementReference: stmtRef.trim(),
        statementDate: stmtDate,
        openingBalance: Number(stmtOpenBal || 0),
        closingBalance: Number(stmtCloseBal || 0),
        csvContent: stmtCsv,
        lines: null,
      });
      setImportOpen(false);
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setImportingStmt(false);
    }
  };

  const beginReconcileLine = (line: BankStatementLineDto) => {
    if (!overview) return;
    setActionError(null);
    setMatchingLine(line);
    const candidates = overview.payments.filter(
      (p) =>
        p.bankAccountId === line.bankAccountId &&
        (p.status === "RELEASED" || p.status === "PARTIALLY_RECONCILED") &&
        Number(p.remainingClearingAmount) > 0,
    );
    const exact = candidates.find((p) => Number(p.remainingClearingAmount) === Number(line.unmatchedAmount)) ?? candidates[0];
    setRecPaymentId(exact?.supplierPaymentId ?? "");
    const suggestedAmt = exact
      ? Math.min(Number(line.unmatchedAmount), Number(exact.remainingClearingAmount))
      : Number(line.unmatchedAmount);
    setRecAmount(String(suggestedAmt));
    setRecDate(line.valueDate);
  };

  const handleConfirmReconcile = async () => {
    if (!matchingLine || !recPaymentId) return;
    setReconciling(true);
    setActionError(null);
    try {
      await reconcileBankStatementLine(tenantId, entityId, {
        bankStatementLineId: matchingLine.bankStatementLineId,
        supplierPaymentId: recPaymentId,
        postingDate: recDate,
        matchedAmount: recAmount ? Number(recAmount) : null,
        idempotencyKey: makeIdempotencyKey("bank-rec"),
      });
      setMatchingLine(null);
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setReconciling(false);
    }
  };

  const handleReverseMatch = async (matchId: string) => {
    setReversingId(matchId);
    setActionError(null);
    try {
      await reverseBankPaymentMatch(tenantId, entityId, matchId, {
        reversalPostingDate: todayJakartaDate(),
        reason: pt.reconcilePanel.defaultReverseReason,
      });
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setReversingId(null);
    }
  };

  const handleSaveBankAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!glCashId || !glClearingId) return;
    setSavingBank(true);
    setActionError(null);
    try {
      await upsertBankAccount(tenantId, entityId, {
        accountCode: bankCode.trim(),
        bankName: bankName.trim(),
        branchName: branchName.trim() || null,
        accountNumber: accountNumber.trim(),
        accountHolderName: holderName.trim(),
        glAccountId: glCashId,
        clearingGlAccountId: glClearingId,
        status: "ACTIVE",
      });
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setSavingBank(false);
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
                <span>{pt.backToOverview}</span>
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (loadError || !overview || !workspace) {
    return (
      <div className="mx-auto max-w-3xl">
        <div
          role="alert"
          className="rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
        >
          <h1 className="text-base font-semibold text-[var(--text-primary)]">{pt.loadErrorTitle}</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">{loadError ?? t.login.errors.upstream_unavailable}</p>
          <button
            type="button"
            onClick={() => loadData(false)}
            className="mt-4 inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
          >
            <RefreshCw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.retryButton}</span>
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
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">{pt.pageTitle}</h1>
          <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">{pt.lead}</p>
          <div className="mt-2 font-code text-xs tabular-nums text-[var(--text-muted)]">
            {workspace.tenant.code} · {workspace.entity.code} ({workspace.entity.functionalCurrencyCode}) ·{" "}
            {formatJakartaDateTime(overview.serverTimestampUtc, locale)}
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.backToOverview}</span>
          </Link>
          <Link
            href={`/w/${tenantId}/${entityId}/payables`}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <CreditCard className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.openPayablesLink}</span>
          </Link>
          <button
            type="button"
            id="pay-refresh-btn"
            onClick={() => loadData(true)}
            disabled={refreshing}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`} strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.refreshButton}</span>
          </button>
        </div>
      </header>

      {/* Read-only banner */}
      {isReadOnly ? (
        <div
          id="pay-readonly-banner"
          role="status"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
        >
          <Eye className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{pt.readOnlyBanner}</span>
        </div>
      ) : null}

      {/* OD-06 & OD-02 Governance Strip (flat, collapsible) */}
      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex flex-wrap items-center justify-between gap-2 bg-[var(--status-warn-bg)] px-4 py-2.5 text-xs text-[var(--status-warn-text)] md:flex-nowrap">
          <div className="flex items-start gap-2">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.od06PolicyBanner}</span>
          </div>
          <button
            type="button"
            id="pay-stages-toggle"
            aria-expanded={stagesOpen}
            onClick={() => setStagesOpen((v) => !v)}
            className="inline-flex min-h-9 shrink-0 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <span>{stagesOpen ? pt.policyToggleHide : pt.policyToggleShow}</span>
            {stagesOpen ? (
              <ChevronUp className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            ) : (
              <ChevronDown className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            )}
          </button>
        </div>

        {stagesOpen ? (
          <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)] md:grid-cols-4 md:divide-x md:divide-y-0">
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{pt.stageCards.stage1Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{pt.stageCards.stage1Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{pt.stageCards.stage2Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{pt.stageCards.stage2Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{pt.stageCards.stage3Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{pt.stageCards.stage3Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{pt.stageCards.stage4Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{pt.stageCards.stage4Body}</p>
            </div>
          </div>
        ) : null}
      </section>

      {/* 4-Metric Hairline KPI Strip */}
      <section
        aria-label={pt.statsAriaLabel}
        className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] md:grid-cols-4 md:divide-x md:divide-y-0"
      >
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{pt.stats.openPayablesTotal}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(kpis.openPayablesTotal)}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{pt.stats.pendingReleaseTotal}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(kpis.pendingReleaseTotal)}
          </div>
        </div>
        <div className="p-3.5">
          <div className="flex items-center justify-between gap-2 text-xs font-medium text-[var(--text-secondary)]">
            <span>{pt.stats.outboundClearingOpen}</span>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <span className="font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
              {formatIdr(kpis.outboundClearingOpen)}
            </span>
            <span
              className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-1.5 py-0.5 text-[11px] font-medium ${
                overview.clearingProof.isReconciled
                  ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                  : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
              }`}
            >
              {overview.clearingProof.isReconciled ? (
                <CheckCircle2 className="h-3 w-3" strokeWidth={1.75} aria-hidden="true" />
              ) : (
                <AlertCircle className="h-3 w-3" strokeWidth={1.75} aria-hidden="true" />
              )}
              <span>{overview.clearingProof.isReconciled ? pt.proofReconciled : pt.proofMismatch}</span>
            </span>
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{pt.stats.reconciledBankTotal}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(kpis.reconciledTotal)}
          </div>
        </div>
      </section>

      {actionError ? (
        <div
          id="pay-action-error"
          role="alert"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-2.5 text-xs text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{actionError}</span>
        </div>
      ) : null}

      {/* Selected Payment Detail Sheet */}
      {selectedDetail ? (
        <SupplierPaymentDetailSheet
          tenantId={tenantId}
          entityId={entityId}
          detail={selectedDetail}
          currentUserEmail={user.email}
          canPrepare={canPrepare}
          canRelease={canRelease}
          canReconcile={canReconcile}
          onClose={() => setSelectedDetail(null)}
          onUpdated={handleDetailUpdated}
          mapError={mapError}
        />
      ) : null}

      {/* Tabs Strip */}
      <div className="space-y-2">
        <div role="tablist" aria-label={pt.tabListLabel} className="flex flex-wrap items-center gap-1 border-b border-[var(--border-hairline)]">
          <button
            type="button"
            role="tab"
            id="pay-tab-payments"
            aria-selected={activeTab === "payments"}
            aria-controls="pay-panel-payments"
            onClick={() => setActiveTab("payments")}
            className={`min-h-10 rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
              activeTab === "payments"
                ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {pt.tabs.payments}
          </button>
          <button
            type="button"
            role="tab"
            id="pay-tab-reconciliation"
            aria-selected={activeTab === "reconciliation"}
            aria-controls="pay-panel-reconciliation"
            onClick={() => setActiveTab("reconciliation")}
            className={`min-h-10 rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
              activeTab === "reconciliation"
                ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {pt.tabs.reconciliation}
          </button>
          <button
            type="button"
            role="tab"
            id="pay-tab-accounts"
            aria-selected={activeTab === "accounts"}
            aria-controls="pay-panel-accounts"
            onClick={() => setActiveTab("accounts")}
            className={`min-h-10 rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
              activeTab === "accounts"
                ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {pt.tabs.accounts}
          </button>
        </div>

        {(canPrepare && activeTab === "payments") || (canReconcile && activeTab === "reconciliation") ? (
          <div className="flex flex-wrap items-center justify-end gap-2">
            {canPrepare && activeTab === "payments" ? (
              <button
                type="button"
                id="pay-composer-toggle"
                aria-expanded={composerOpen}
                onClick={() => setComposerOpen((v) => !v)}
                className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
              >
                <Plus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{composerOpen ? pt.composer.toggleClose : pt.composer.toggleOpen}</span>
              </button>
            ) : null}
            {canReconcile && activeTab === "reconciliation" ? (
              <button
                type="button"
                id="pay-import-toggle"
                aria-expanded={importOpen}
                onClick={() => setImportOpen((v) => !v)}
                className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
              >
                <Upload className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{importOpen ? pt.statementImport.toggleClose : pt.statementImport.toggleOpen}</span>
              </button>
            ) : null}
          </div>
        ) : null}
      </div>

      {/* TAB 1: PAYMENTS & CLEARING */}
      {activeTab === "payments" ? (
        <div id="pay-panel-payments" role="tabpanel" aria-labelledby="pay-tab-payments" className="space-y-5">
          {composerOpen && canPrepare ? (
            <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
              <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
                {pt.composer.title}
              </div>
              {eligibleOpenPayables.length === 0 ? (
                <p className="p-4 text-xs text-[var(--text-secondary)]">{pt.composer.noOpenPayables}</p>
              ) : overview.bankAccounts.filter((b) => b.status === "ACTIVE").length === 0 ? (
                <p className="p-4 text-xs text-[var(--text-secondary)]">{pt.composer.noActiveBankAccounts}</p>
              ) : (
                <form onSubmit={handleCreateProposal} className="space-y-3 p-4">
                  <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.bankAccountLabel}
                      </span>
                      <select
                        id="pay-comp-bank"
                        value={compBankId}
                        onChange={(e) => setCompBankId(e.target.value)}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      >
                        {overview.bankAccounts
                          .filter((b) => b.status === "ACTIVE")
                          .map((b) => (
                            <option key={b.bankAccountId} value={b.bankAccountId}>
                              {b.accountCode} · {b.bankName} ({b.accountNumber})
                            </option>
                          ))}
                      </select>
                    </label>

                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.payableLabel}
                      </span>
                      <select
                        id="pay-comp-payable"
                        value={compPayableId}
                        onChange={(e) => {
                          const nextId = e.target.value;
                          setCompPayableId(nextId);
                          const hit = eligibleOpenPayables.find((x) => x.payableItemId === nextId);
                          if (hit) setCompAmount(String(hit.remainingOpenAmount));
                        }}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      >
                        {eligibleOpenPayables.map((pay) => (
                          <option key={pay.payableItemId} value={pay.payableItemId}>
                            {pay.payableNumber} · {pay.supplierInvoiceNumber} ({formatIdr(pay.remainingOpenAmount)})
                          </option>
                        ))}
                      </select>
                    </label>

                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.allocatedAmountLabel}
                      </span>
                      <input
                        id="pay-comp-amount"
                        type="number"
                        step="0.01"
                        min="1"
                        value={compAmount}
                        onChange={(e) => setCompAmount(e.target.value)}
                        className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                      />
                    </label>
                  </div>

                  <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.beneficiaryBankLabel}
                      </span>
                      <input
                        id="pay-comp-ben-bank"
                        type="text"
                        required
                        value={compBenBank}
                        placeholder="PT Bank Mandiri (Persero) Tbk"
                        onChange={(e) => setCompBenBank(e.target.value)}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </label>
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.beneficiaryAccountLabel}
                      </span>
                      <input
                        id="pay-comp-ben-acct"
                        type="text"
                        required
                        value={compBenAccount}
                        placeholder="122-000-4455-889"
                        onChange={(e) => setCompBenAccount(e.target.value)}
                        className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </label>
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.beneficiaryHolderLabel}
                      </span>
                      <input
                        id="pay-comp-ben-holder"
                        type="text"
                        required
                        value={compBenHolder}
                        placeholder="PT Sumber Makmur Kemasan"
                        onChange={(e) => setCompBenHolder(e.target.value)}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </label>
                  </div>

                  <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.paymentMethodLabel}
                      </span>
                      <select
                        id="pay-comp-method"
                        value={compMethod}
                        onChange={(e) => setCompMethod(e.target.value)}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      >
                        <option value="BANK_TRANSFER_IDR">{pt.methodLabels.BANK_TRANSFER_IDR}</option>
                        <option value="CLEARING_RTGS">{pt.methodLabels.CLEARING_RTGS}</option>
                        <option value="BIFAST_IDR">{pt.methodLabels.BIFAST_IDR}</option>
                      </select>
                    </label>
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.paymentDateLabel}
                      </span>
                      <input
                        id="pay-comp-date"
                        type="date"
                        value={compDate}
                        onChange={(e) => setCompDate(e.target.value)}
                        className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                      />
                    </label>
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.composer.notesLabel}
                      </span>
                      <input
                        id="pay-comp-notes"
                        type="text"
                        value={compNotes}
                        onChange={(e) => setCompNotes(e.target.value)}
                        className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </label>
                  </div>

                  <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border-hairline)] pt-3 text-xs text-[var(--text-secondary)]">
                    <span>{pt.composer.previewSimulationWarning}</span>
                    <button
                      type="submit"
                      id="pay-create-submit"
                      disabled={creatingProposal || !selectedPayable}
                      className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                    >
                      <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                      <span>{creatingProposal ? pt.composer.creating : pt.composer.submitCreate}</span>
                    </button>
                  </div>
                </form>
              )}
            </section>
          ) : null}

          {/* Payments Table */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.tables.paymentsTitle}
            </div>
            {overview.payments.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{pt.tables.emptyPayments}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="pay-payments-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.tables.colPaymentRef}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.tables.colSupplierBank}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{pt.tables.colStageAmount}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.tables.colClearingJournal}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.tables.colActions}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.payments.map((pay) => (
                      <tr key={pay.supplierPaymentId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colPaymentRef}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {pay.paymentNumber ?? pay.draftCode}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {pay.paymentDate}
                            {pay.latestInstructionCode ? ` · ${pay.latestInstructionCode}` : ""}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colSupplierBank}
                          </span>
                          <div className="font-medium text-[var(--text-primary)]">{pay.supplierLegalName}</div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span className="font-code">{pay.bankAccountCode}</span> → {pay.beneficiaryBankName} (
                            <span className="font-code tabular-nums">{pay.beneficiaryAccountNumber}</span>)
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colStageAmount}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(pay.totalAllocatedAmount)}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span>{pt.clearingOpenShort} </span>
                            <span className="font-code tabular-nums">{formatIdr(pay.remainingClearingAmount)}</span>
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colClearingJournal}
                          </span>
                          <div className="flex flex-wrap items-center gap-1.5">
                            <PaymentStatusBadge status={pay.status} label={pt.statusLabels[pay.status] ?? pay.status} />
                          </div>
                          {pay.releaseJournalNumber ? (
                            <div className="mt-0.5 font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {pay.releaseJournalNumber}
                            </div>
                          ) : null}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          <button
                            type="button"
                            data-testid={`open-payment-${pay.paymentNumber ?? pay.draftCode}`}
                            onClick={() => openPaymentDetail(pay.supplierPaymentId)}
                            className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                          >
                            <FileText className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{pt.tables.openDetailCta}</span>
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* Open Payables Summary */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.tables.openPayablesTitle}
            </div>
            {overview.openPayables.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{pt.tables.emptyOpenPayables}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="pay-open-payables-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.tables.colPayable}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.tables.colInvoiceDue}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.tables.colBalance}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.openPayables.map((apItem) => (
                      <tr key={apItem.payableItemId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colPayable}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {apItem.payableNumber}
                          </div>
                          <div className="text-xs text-[var(--text-secondary)]">
                            {apItem.supplierCode} · {apItem.supplierLegalName}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colInvoiceDue}
                          </span>
                          <div className="font-code tabular-nums text-[var(--text-primary)]">
                            {apItem.supplierInvoiceNumber}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {apItem.dueDate}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colBalance}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(apItem.remainingOpenAmount)}
                          </div>
                          {Number(apItem.paidAmount) > 0 ? (
                            <div className="text-[11px] text-[var(--status-ok-text)]">
                              <span>{pt.paidShort} </span>
                              <span className="font-code tabular-nums">{formatIdr(apItem.paidAmount)}</span>
                            </div>
                          ) : null}
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

      {/* TAB 2: BANK STATEMENTS & RECONCILIATION */}
      {activeTab === "reconciliation" ? (
        <div id="pay-panel-reconciliation" role="tabpanel" aria-labelledby="pay-tab-reconciliation" className="space-y-5">
          {importOpen && canReconcile ? (
            <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
              <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
                {pt.statementImport.title}
              </div>
              <form onSubmit={handleImportStatement} className="space-y-3 p-4">
                <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                      {pt.statementImport.bankAccountLabel}
                    </span>
                    <select
                      id="pay-import-bank"
                      value={stmtBankId}
                      onChange={(e) => setStmtBankId(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    >
                      {overview.bankAccounts.map((b) => (
                        <option key={b.bankAccountId} value={b.bankAccountId}>
                          {b.accountCode} · {b.bankName}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                      {pt.statementImport.statementRefLabel}
                    </span>
                    <input
                      id="pay-import-ref"
                      type="text"
                      required
                      value={stmtRef}
                      placeholder="BCA-STMT-2026-11-19"
                      onChange={(e) => setStmtRef(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                      {pt.statementImport.statementDateLabel}
                    </span>
                    <input
                      id="pay-import-date"
                      type="date"
                      value={stmtDate}
                      onChange={(e) => setStmtDate(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                    />
                  </label>
                  <div className="grid grid-cols-2 gap-2">
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.statementImport.openingBalLabel}
                      </span>
                      <input
                        id="pay-import-open"
                        type="number"
                        value={stmtOpenBal}
                        placeholder="500000000"
                        onChange={(e) => setStmtOpenBal(e.target.value)}
                        className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)]"
                      />
                    </label>
                    <label className="block space-y-1">
                      <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                        {pt.statementImport.closingBalLabel}
                      </span>
                      <input
                        id="pay-import-close"
                        type="number"
                        value={stmtCloseBal}
                        placeholder="498890000"
                        onChange={(e) => setStmtCloseBal(e.target.value)}
                        className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)]"
                      />
                    </label>
                  </div>
                </div>

                <label className="block space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                    {pt.statementImport.csvLabel}
                  </span>
                  <textarea
                    id="pay-import-csv"
                    rows={3}
                    required
                    value={stmtCsv}
                    placeholder={"ValueDate,Direction,Amount,BankReference,Counterparty,Description\n2026-11-19,DEBIT,1110000,TRF-BCA-20261119-01,PT SUMBER MAKMUR KEMASAN,TRANSFER KELUAR INV-JKT-S11-01"}
                    onChange={(e) => setStmtCsv(e.target.value)}
                    className="font-code w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] p-2.5 text-xs leading-relaxed text-[var(--text-primary)]"
                  />
                  <span className="block text-[11px] text-[var(--text-muted)]">{pt.statementImport.csvHelper}</span>
                </label>

                <div className="flex justify-end">
                  <button
                    type="submit"
                    id="pay-import-submit"
                    disabled={importingStmt}
                    className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    <Upload className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                    <span>{importingStmt ? pt.statementImport.importing : pt.statementImport.importCta}</span>
                  </button>
                </div>
              </form>
            </section>
          ) : null}

          {/* Reconcile Inline Drawer */}
          {matchingLine && canReconcile ? (
            <section
              id="pay-reconcile-drawer"
              className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
            >
              <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
                {pt.reconcilePanel.matchAction}:{" "}
                <span className="font-code tabular-nums">
                  {matchingLine.bankReference} · {formatIdr(matchingLine.unmatchedAmount)}
                </span>
              </div>
              <div className="grid grid-cols-1 gap-3 p-4 md:grid-cols-3">
                <label className="block space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                    {pt.reconcilePanel.targetPaymentLabel}
                  </span>
                  <select
                    id="pay-reconcile-payment-select"
                    value={recPaymentId}
                    onChange={(e) => {
                      const nextId = e.target.value;
                      setRecPaymentId(nextId);
                      const payObj = overview.payments.find((p) => p.supplierPaymentId === nextId);
                      if (payObj) {
                        setRecAmount(String(Math.min(Number(matchingLine.unmatchedAmount), Number(payObj.remainingClearingAmount))));
                      }
                    }}
                    className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                  >
                    {overview.payments
                      .filter(
                        (p) =>
                          p.bankAccountId === matchingLine.bankAccountId &&
                          (p.status === "RELEASED" || p.status === "PARTIALLY_RECONCILED") &&
                          Number(p.remainingClearingAmount) > 0,
                      )
                      .map((p) => (
                        <option key={p.supplierPaymentId} value={p.supplierPaymentId}>
                          {p.paymentNumber ?? p.draftCode} · {p.supplierCode} ({formatIdr(p.remainingClearingAmount)})
                        </option>
                      ))}
                  </select>
                </label>

                <label className="block space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                    {pt.reconcilePanel.matchAmountLabel}
                  </span>
                  <input
                    id="pay-reconcile-amount"
                    type="number"
                    step="0.01"
                    min="1"
                    value={recAmount}
                    onChange={(e) => setRecAmount(e.target.value)}
                    className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                  />
                </label>

                <label className="block space-y-1">
                  <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                    {pt.reconcilePanel.postingDateLabel}
                  </span>
                  <input
                    id="pay-reconcile-date"
                    type="date"
                    value={recDate}
                    onChange={(e) => setRecDate(e.target.value)}
                    className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                  />
                </label>
              </div>
              <div className="flex flex-wrap items-center justify-end gap-2 border-t border-[var(--border-hairline)] px-4 py-3">
                <button
                  type="button"
                  onClick={() => setMatchingLine(null)}
                  className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                >
                  {pt.reconcilePanel.cancelMatch}
                </button>
                <button
                  type="button"
                  id="pay-reconcile-confirm-btn"
                  disabled={reconciling || !recPaymentId}
                  onClick={handleConfirmReconcile}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                >
                  <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                  <span>{reconciling ? pt.reconcilePanel.matching : pt.reconcilePanel.confirmMatch}</span>
                </button>
              </div>
            </section>
          ) : null}

          {/* Bank Statement Lines Staging Table */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.tables.statementLinesTitle}
            </div>
            {overview.statementLines.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{pt.tables.emptyStatementLines}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="pay-statement-lines-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.tables.colLineRef}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{pt.tables.colDirectionAmt}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.tables.colMatchStatus}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.tables.colReconcileAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.statementLines.map((line) => (
                      <tr key={line.bankStatementLineId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colLineRef}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {line.bankReference} · {line.valueDate}
                          </div>
                          <div className="text-xs text-[var(--text-secondary)]">
                            {line.statementNumber} ({line.bankAccountCode}) · {line.description}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colDirectionAmt}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {line.direction} · {formatIdr(line.amount)}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span>{pt.openShort} </span>
                            <span className="font-code tabular-nums">{formatIdr(line.unmatchedAmount)}</span>
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colMatchStatus}
                          </span>
                          <PaymentStatusBadge
                            status={line.status}
                            label={pt.statementLineStatusLabels[line.status] ?? line.status}
                          />
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          {canReconcile &&
                          line.direction === "DEBIT" &&
                          line.status !== "MATCHED" &&
                          Number(line.unmatchedAmount) > 0 ? (
                            <button
                              type="button"
                              data-testid={`reconcile-line-${line.bankReference}`}
                              onClick={() => beginReconcileLine(line)}
                              className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                            >
                              <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                              <span>{pt.reconcilePanel.matchAction}</span>
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

          {/* Posted Matches Table */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.tables.matchesTitle}
            </div>
            {overview.matches.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{pt.tables.emptyMatches}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="pay-matches-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.tables.colMatchNo}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.tables.colMatchedDocs}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{pt.tables.colMatchGl}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.tables.colReverseAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.matches.map((m) => (
                      <tr key={m.bankPaymentMatchId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colMatchNo}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">{m.matchNumber}</div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">{m.postingDate}</div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colMatchedDocs}
                          </span>
                          <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                            {m.paymentDisplayNumber} ↔ {m.bankReference}
                          </div>
                          <div className="font-code text-[11px] text-[var(--text-secondary)]">{m.statementNumber}</div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.tables.colMatchGl}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(m.matchedAmount)}
                          </div>
                          <div className="mt-0.5 flex flex-wrap items-center gap-1.5 md:justify-end">
                            <span className="font-code text-[11px] text-[var(--text-secondary)]">
                              {m.clearingGlJournalNumber}
                            </span>
                            <PaymentStatusBadge status={m.status} label={pt.matchStatusLabels[m.status] ?? m.status} />
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          {canReconcile && m.status === "POSTED" ? (
                            <button
                              type="button"
                              id={`pay-reverse-match-${m.matchNumber}`}
                              disabled={reversingId === m.bankPaymentMatchId}
                              onClick={() => handleReverseMatch(m.bankPaymentMatchId)}
                              className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                            >
                              <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                              <span>
                                {reversingId === m.bankPaymentMatchId
                                  ? pt.reconcilePanel.reversing
                                  : pt.reconcilePanel.reverseCta}
                              </span>
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
        </div>
      ) : null}

      {/* TAB 3: BANK ACCOUNTS & 1120 PROOF */}
      {activeTab === "accounts" ? (
        <div id="pay-panel-accounts" role="tabpanel" aria-labelledby="pay-tab-accounts" className="space-y-5">
          {/* Live 1120 Proof Table */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">{pt.bankConfig.clearingProofTitle}</h2>
              <PaymentStatusBadge
                status={overview.clearingProof.isReconciled ? "RECONCILED" : "BANK_REJECTED"}
                label={overview.clearingProof.isReconciled ? pt.proofReconciled : pt.proofMismatch}
              />
            </div>
            <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] md:grid-cols-4 md:divide-x md:divide-y-0">
              <div className="p-3.5">
                <div className="text-xs text-[var(--text-secondary)]">{pt.bankConfig.glClearingBalance}</div>
                <div className="mt-1 font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">
                  {formatIdr(overview.clearingProof.glOutboundClearingCreditNetBalance)}
                </div>
              </div>
              <div className="p-3.5">
                <div className="text-xs text-[var(--text-secondary)]">{pt.bankConfig.subledgerClearingBalance}</div>
                <div className="mt-1 font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">
                  {formatIdr(overview.clearingProof.subledgerOpenClearingBalance)}
                </div>
              </div>
              <div className="p-3.5">
                <div className="text-xs text-[var(--text-secondary)]">{pt.bankConfig.discrepancyLabel}</div>
                <div className="mt-1 font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">
                  {formatIdr(overview.clearingProof.discrepancyAmount)}
                </div>
              </div>
              <div className="p-3.5">
                <div className="text-xs text-[var(--text-secondary)]">{pt.bankConfig.glCashBalance}</div>
                <div className="mt-1 font-code text-sm font-bold tabular-nums text-[var(--text-primary)]">
                  {formatIdr(overview.clearingProof.glMainOperatingBankNetBalance)}
                </div>
              </div>
            </div>
          </section>

          {/* Bank Accounts Table */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.tables.bankAccountsTitle}
            </div>
            {overview.bankAccounts.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{pt.tables.emptyBankAccounts}</p>
            ) : (
              <div className="divide-y divide-[var(--border-hairline)]">
                {overview.bankAccounts.map((b) => (
                  <div key={b.bankAccountId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-xs">
                    <div>
                      <div className="font-semibold text-[var(--text-primary)]">
                        <span className="font-code font-bold">{b.accountCode}</span> · {b.bankName} (
                        <span className="font-code tabular-nums">{b.accountNumber}</span>)
                      </div>
                      <div className="text-[var(--text-secondary)]">
                        {b.branchName ? `${b.branchName} · ` : ""}
                        {b.accountHolderName}
                      </div>
                    </div>
                    <div className="md:text-right">
                      <div className="text-[11px] text-[var(--text-secondary)]">
                        <span>{pt.glCashPrefix} </span>
                        <span className="font-code">{b.glAccountCode}</span>
                        <span> · {pt.glClearingPrefix} </span>
                        <span className="font-code">{b.clearingGlAccountCode}</span>
                      </div>
                      <div className="mt-1">
                        <PaymentStatusBadge
                          status={b.status === "ACTIVE" ? "POSTED" : "CANCELLED"}
                          label={b.status === "ACTIVE" ? pt.statusActiveLabel : pt.statusInactiveLabel}
                        />
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Add/Configure Bank Account Form */}
          {canConfigureBank ? (
            <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
              <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
                {pt.bankConfig.title}
              </div>
              <form onSubmit={handleSaveBankAccount} className="space-y-3 p-4">
                <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{pt.bankConfig.accountCodeLabel}</span>
                    <input
                      type="text"
                      required
                      value={bankCode}
                      placeholder="BRI-IDR-002"
                      onChange={(e) => setBankCode(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{pt.bankConfig.bankNameLabel}</span>
                    <input
                      type="text"
                      required
                      value={bankName}
                      placeholder="PT Bank Rakyat Indonesia (Persero) Tbk"
                      onChange={(e) => setBankName(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{pt.bankConfig.accountNumberLabel}</span>
                    <input
                      type="text"
                      required
                      value={accountNumber}
                      placeholder="020-60100-4455-301"
                      onChange={(e) => setAccountNumber(e.target.value)}
                      className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                </div>

                <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{pt.bankConfig.branchNameLabel}</span>
                    <input
                      type="text"
                      value={branchName}
                      placeholder="KC Jakarta Gatot Subroto"
                      onChange={(e) => setBranchName(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{pt.bankConfig.holderNameLabel}</span>
                    <input
                      type="text"
                      required
                      value={holderName}
                      placeholder="PT Nusantara Distribusi Jakarta"
                      onChange={(e) => setHolderName(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    />
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{pt.bankConfig.glCashLabel}</span>
                    <select
                      value={glCashId}
                      onChange={(e) => setGlCashId(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    >
                      {overview.cashAndClearingAccounts
                        .filter((a) => a.accountCategory === "CASH_BANK")
                        .map((a) => (
                          <option key={a.accountId} value={a.accountId}>
                            {a.accountCode} · {a.accountName}
                          </option>
                        ))}
                    </select>
                  </label>
                  <label className="block space-y-1">
                    <span className="block text-[11px] font-medium text-[var(--text-secondary)]">{pt.bankConfig.glClearingLabel}</span>
                    <select
                      value={glClearingId}
                      onChange={(e) => setGlClearingId(e.target.value)}
                      className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                    >
                      {overview.cashAndClearingAccounts
                        .filter((a) => a.accountCategory === "BANK_OUTBOUND_CLEARING")
                        .map((a) => (
                          <option key={a.accountId} value={a.accountId}>
                            {a.accountCode} · {a.accountName}
                          </option>
                        ))}
                    </select>
                  </label>
                </div>

                <div className="flex justify-end">
                  <button
                    type="submit"
                    disabled={savingBank}
                    className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                  >
                    {savingBank ? pt.bankConfig.saving : pt.bankConfig.saveCta}
                  </button>
                </div>
              </form>
            </section>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
