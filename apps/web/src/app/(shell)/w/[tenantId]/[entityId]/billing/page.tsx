"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  AlertCircle,
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock,
  Eye,
  FileSpreadsheet,
  RefreshCw,
  RotateCcw,
  Send,
  ShieldAlert,
  ShieldCheck,
  XCircle,
} from "lucide-react";
import {
  type ApiProblemError,
  type BillableShipmentLineDto,
  type BillingOverviewResponse,
  type CustomerInvoiceDto,
  cancelCustomerInvoice,
  createAndPostCreditNote,
  createCustomerInvoiceDraft,
  fetchBillingOverview,
  postCustomerInvoice,
  submitCustomerInvoice,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatIdr, formatUnitCostIdr, todayJakartaDate } from "@/lib/i18n/messages";

type TabKey = "billable" | "invoices" | "receivables";

function BillingStatusBadge({ status, label }: { status: string; label: string }) {
  if (status === "POSTED" || status === "SETTLED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "SUBMITTED" || status === "OPEN" || status === "PARTIALLY_SETTLED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-warn-text)]">
        <Clock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "CANCELLED" || status === "CREDITED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-danger-text)]">
        <XCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-subtle)] px-2 py-0.5 text-[11px] font-semibold text-[var(--text-secondary)]">
      <FileSpreadsheet className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

export default function BillingPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = params.tenantId;
  const entityId = params.entityId;
  const { t } = useLocale();
  const bt = t.billing;

  const [data, setData] = useState<BillingOverviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [accessDenied, setAccessDenied] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<TabKey>("billable");
  const [showPolicy, setShowPolicy] = useState(false);

  // Draft invoice composer drawer
  const [draftTarget, setDraftTarget] = useState<BillableShipmentLineDto | null>(null);
  const [invoiceDate, setInvoiceDate] = useState(todayJakartaDate());
  const [dueDate, setDueDate] = useState(todayJakartaDate());
  const [selectedTaxCodeId, setSelectedTaxCodeId] = useState("");
  const [draftEvidenceRef, setDraftEvidenceRef] = useState("");
  const [draftQty, setDraftQty] = useState("");
  const [draftNotes, setDraftNotes] = useState("");
  const [creatingDraft, setCreatingDraft] = useState(false);

  // Irreversible Post Invoice drawer
  const [postTarget, setPostTarget] = useState<CustomerInvoiceDto | null>(null);
  const [postEvidenceRef, setPostEvidenceRef] = useState("");
  const [postingInvoice, setPostingInvoice] = useState(false);

  // Credit Note drawer
  const [creditTarget, setCreditTarget] = useState<CustomerInvoiceDto | null>(null);
  const [creditDate, setCreditDate] = useState(todayJakartaDate());
  const [creditEvidenceRef, setCreditEvidenceRef] = useState("");
  const [creditQtys, setCreditQtys] = useState<Record<string, string>>({});
  const [creditNotes, setCreditNotes] = useState("");
  const [postingCredit, setPostingCredit] = useState(false);

  const [busyInvoiceId, setBusyInvoiceId] = useState<string | null>(null);

  const mapError = useCallback(
    (err: unknown): string => {
      const problem = err as Partial<ApiProblemError>;
      if (problem && typeof problem === "object") {
        const code = problem.errorCode ?? "";
        if (code && bt.errors[code]) {
          return bt.errors[code];
        }
        if (problem.detail) {
          return problem.detail;
        }
      }
      return err instanceof Error ? err.message : String(err);
    },
    [bt.errors],
  );

  const loadSnapshot = useCallback(async () => {
    const res = await fetchBillingOverview(tenantId, entityId);
    setData(res);
    setAccessDenied(false);
    setLoadError(null);
  }, [tenantId, entityId]);

  const handleRefresh = useCallback(async () => {
    setRefreshing(true);
    setActionError(null);
    try {
      await loadSnapshot();
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
  }, [loadSnapshot, mapError]);

  useEffect(() => {
    let active = true;
    fetchBillingOverview(tenantId, entityId)
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
  }, [tenantId, entityId, mapError]);

  const canDraft = Boolean(data?.canDraft);
  const canPost = Boolean(data?.canPost);
  const hasAnyAction = canDraft || canPost;
  const taxCodes = useMemo(() => data?.outputTaxCodes ?? [], [data?.outputTaxCodes]);
  const billableLines = data?.billableShipmentLines ?? [];
  const invoices = data?.invoices ?? [];
  const receivables = data?.receivables ?? [];

  const openDraftDrawer = (line: BillableShipmentLineDto) => {
    setActionError(null);
    setPostTarget(null);
    setCreditTarget(null);
    setDraftTarget(line);
    const today = todayJakartaDate();
    setInvoiceDate(today);
    const due = new Date(`${today}T00:00:00Z`);
    due.setUTCDate(due.getUTCDate() + Math.max(0, Number(line.paymentTermsDays ?? 30)));
    setDueDate(due.toISOString().slice(0, 10));
    const defaultTax =
      taxCodes.find((tc) => tc.taxCodeId === line.defaultTaxCodeId)?.taxCodeId ??
      taxCodes[0]?.taxCodeId ??
      "";
    setSelectedTaxCodeId(defaultTax);
    setDraftEvidenceRef("");
    setDraftQty(String(line.remainingBillableBaseQuantity));
    setDraftNotes("");
  };

  const openPostDrawer = (inv: CustomerInvoiceDto) => {
    setActionError(null);
    setDraftTarget(null);
    setCreditTarget(null);
    setPostTarget(inv);
    setPostEvidenceRef(inv.taxEvidenceReference ?? "");
  };

  const openCreditDrawer = (inv: CustomerInvoiceDto) => {
    setActionError(null);
    setDraftTarget(null);
    setPostTarget(null);
    setCreditTarget(inv);
    setCreditDate(todayJakartaDate());
    setCreditEvidenceRef("");
    setCreditNotes("");
    const init: Record<string, string> = {};
    for (const l of inv.lines) {
      const rem = Number(l.remainingCreditableBaseQuantity);
      if (rem > 0) {
        init[l.invoiceLineId] = String(rem);
      }
    }
    setCreditQtys(init);
  };

  const draftPreview = useMemo(() => {
    if (!draftTarget) return { subtotal: 0, tax: 0, grand: 0, rate: 0 };
    const qty = Number(draftQty);
    const price = Number(draftTarget.unitPricePerBase);
    if (!Number.isFinite(qty) || qty <= 0 || !Number.isFinite(price)) {
      return { subtotal: 0, tax: 0, grand: 0, rate: 0 };
    }
    const subtotal = Math.round(qty * price * 100) / 100;
    const tc = taxCodes.find((x) => x.taxCodeId === selectedTaxCodeId);
    const effRate = tc ? Number(tc.effectiveRatePercent) : 0;
    const tax = Math.round(subtotal * (effRate / 100) * 100) / 100;
    return { subtotal, tax, grand: Math.round((subtotal + tax) * 100) / 100, rate: effRate };
  }, [draftTarget, draftQty, selectedTaxCodeId, taxCodes]);

  const handleSubmitDraft = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!draftTarget || !selectedTaxCodeId) return;
    const qty = Number(draftQty);
    if (!Number.isFinite(qty) || qty <= 0) return;

    setCreatingDraft(true);
    setActionError(null);
    try {
      await createCustomerInvoiceDraft(tenantId, entityId, {
        salesOrderId: draftTarget.salesOrderId,
        taxCodeId: selectedTaxCodeId,
        invoiceDate,
        dueDate,
        taxEvidenceReference: draftEvidenceRef.trim() || null,
        idempotencyKey: `inv-${draftTarget.shipmentLineId.slice(0, 8)}-${Date.now()}`,
        notes: draftNotes.trim() || null,
        lines: [
          {
            shipmentLineId: draftTarget.shipmentLineId,
            billedBaseQuantity: qty,
          },
        ],
      });
      setDraftTarget(null);
      await loadSnapshot();
      setActiveTab("invoices");
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setCreatingDraft(false);
    }
  };

  const handleSubmitToFinance = async (inv: CustomerInvoiceDto) => {
    setBusyInvoiceId(inv.invoiceId);
    setActionError(null);
    try {
      await submitCustomerInvoice(tenantId, entityId, inv.invoiceId);
      await loadSnapshot();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusyInvoiceId(null);
    }
  };

  const handleCancelDraft = async (inv: CustomerInvoiceDto) => {
    setBusyInvoiceId(inv.invoiceId);
    setActionError(null);
    try {
      await cancelCustomerInvoice(tenantId, entityId, inv.invoiceId);
      await loadSnapshot();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusyInvoiceId(null);
    }
  };

  const handleConfirmPost = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!postTarget) return;
    setPostingInvoice(true);
    setActionError(null);
    try {
      await postCustomerInvoice(tenantId, entityId, postTarget.invoiceId, {
        taxEvidenceReference: postEvidenceRef.trim() || null,
      });
      setPostTarget(null);
      await loadSnapshot();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setPostingInvoice(false);
    }
  };

  const handleConfirmCredit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!creditTarget) return;
    const lines = creditTarget.lines
      .map((l) => ({
        originalInvoiceLineId: l.invoiceLineId,
        creditedBaseQuantity: Number(creditQtys[l.invoiceLineId] ?? 0),
      }))
      .filter((x) => Number.isFinite(x.creditedBaseQuantity) && x.creditedBaseQuantity > 0);
    if (lines.length === 0) return;

    setPostingCredit(true);
    setActionError(null);
    try {
      await createAndPostCreditNote(tenantId, entityId, {
        originalInvoiceId: creditTarget.invoiceId,
        creditDate,
        taxEvidenceReference: creditEvidenceRef.trim() || null,
        idempotencyKey: `cn-${creditTarget.invoiceId.slice(0, 8)}-${Date.now()}`,
        notes: creditNotes.trim() || null,
        lines,
      });
      setCreditTarget(null);
      await loadSnapshot();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setPostingCredit(false);
    }
  };

  if (loading) {
    return (
      <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
        <p className="text-sm text-[var(--text-secondary)]">{bt.loading}</p>
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
            <h1 className="text-base font-semibold">{bt.accessDeniedHeading}</h1>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">{bt.accessDeniedBody}</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-[var(--text-primary)]">{bt.heading}</h1>
          <p className="mt-1 max-w-3xl text-xs text-[var(--text-secondary)]">{bt.lead}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{bt.backToEntityLink}</span>
          </Link>
          <button
            type="button"
            onClick={() => void handleRefresh()}
            disabled={refreshing}
            className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`} strokeWidth={1.75} aria-hidden="true" />
            <span>{refreshing ? bt.refreshing : bt.refreshButton}</span>
          </button>
        </div>
      </div>

      {/* Read-only boundary notice */}
      {!hasAnyAction ? (
        <div
          id="bil-readonly-banner"
          className="flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-subtle)] px-4 py-3 text-xs text-[var(--text-secondary)]"
        >
          <Eye className="mt-0.5 h-4 w-4 shrink-0 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <span>{bt.readOnlyBanner}</span>
        </div>
      ) : null}

      {/* OD-07 / OD-09 Policy Banner */}
      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)]">
        <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs text-[var(--status-warn-text)]">
          <div className="flex items-start gap-2">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span className="font-medium">{bt.od07PolicyBanner}</span>
          </div>
          <button
            type="button"
            onClick={() => setShowPolicy((v) => !v)}
            className="inline-flex min-h-9 shrink-0 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <span>{showPolicy ? bt.policyToggleHide : bt.policyToggleShow}</span>
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
              <div className="text-xs font-semibold text-[var(--text-primary)]">{bt.policyCards.rule1Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{bt.policyCards.rule1Body}</p>
            </div>
            <div className="p-3.5">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{bt.policyCards.rule2Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{bt.policyCards.rule2Body}</p>
            </div>
            <div className="p-3.5">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{bt.policyCards.rule3Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{bt.policyCards.rule3Body}</p>
            </div>
            <div className="p-3.5">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{bt.policyCards.rule4Title}</div>
              <p className="mt-1 text-[11px] leading-relaxed text-[var(--text-secondary)]">{bt.policyCards.rule4Body}</p>
            </div>
          </div>
        ) : null}
      </section>

      {/* Errors */}
      {loadError ? (
        <div role="alert" className="flex items-start gap-2 rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-xs text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{loadError}</span>
        </div>
      ) : null}
      {actionError ? (
        <div id="bil-action-error" role="alert" className="flex items-start gap-2 rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-xs text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{actionError}</span>
        </div>
      ) : null}

      {/* 4-Metric Hairline KPI Strip */}
      <section
        aria-label={bt.statsAriaLabel}
        className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] sm:grid-cols-2 md:grid-cols-4 md:divide-x md:divide-y-0"
      >
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">{bt.stats.unbilledDispatchedQty}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {data?.stats.unbilledDispatchedBaseQty ?? 0}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">{bt.stats.openDraftInvoicesCount}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {data?.stats.openDraftInvoicesCount ?? 0}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">{bt.stats.postedRevenueNetTotal}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(data?.stats.postedRevenueNetTotal ?? 0)}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-[11px] font-medium text-[var(--text-secondary)]">{bt.stats.openReceivablesTotal}</div>
          <div className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(data?.stats.openReceivablesTotal ?? 0)}
          </div>
        </div>
      </section>

      {/* Inline Draft Invoice Composer Drawer */}
      {draftTarget && canDraft ? (
        <section
          id="bil-draft-drawer"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {bt.billablePanel.drawerTitlePrefix}{" "}
              <span className="font-code whitespace-nowrap">{draftTarget.shipmentNumber}</span> ·{" "}
              <span className="font-code whitespace-nowrap">{draftTarget.itemCode}</span>
            </span>
            <button
              type="button"
              id="bil-draft-cancel-btn"
              onClick={() => setDraftTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {bt.billablePanel.cancelBtn}
            </button>
          </div>
          <form onSubmit={handleSubmitDraft} className="space-y-3 p-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-12">
              <label className="block space-y-1 md:col-span-3">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.billablePanel.invoiceDateLabel}
                </span>
                <input
                  id="bil-draft-date"
                  type="date"
                  required
                  value={invoiceDate}
                  onChange={(e) => setInvoiceDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-3">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.billablePanel.dueDateLabel}
                </span>
                <input
                  id="bil-draft-due"
                  type="date"
                  required
                  value={dueDate}
                  onChange={(e) => setDueDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-3">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.billablePanel.taxCodeLabel}
                </span>
                <select
                  id="bil-draft-tax"
                  value={selectedTaxCodeId}
                  onChange={(e) => setSelectedTaxCodeId(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  {taxCodes.map((tc) => (
                    <option key={tc.taxCodeId} value={tc.taxCodeId}>
                      {tc.code} ({tc.effectiveRatePercent}%)
                    </option>
                  ))}
                </select>
              </label>
              <label className="block space-y-1 md:col-span-3">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.billablePanel.billQtyLabel} ({bt.billablePanel.maxPrefix}{" "}
                  {draftTarget.remainingBillableBaseQuantity} {draftTarget.baseUomCode})
                </span>
                <input
                  id="bil-draft-qty"
                  type="number"
                  step="0.001"
                  min="0.001"
                  max={Number(draftTarget.remainingBillableBaseQuantity)}
                  required
                  value={draftQty}
                  onChange={(e) => setDraftQty(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-6">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.billablePanel.evidenceRefLabel}
                </span>
                <input
                  id="bil-draft-evidence"
                  type="text"
                  value={draftEvidenceRef}
                  placeholder={bt.billablePanel.evidenceRefPlaceholder}
                  onChange={(e) => setDraftEvidenceRef(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-6">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.billablePanel.notesLabel}
                </span>
                <input
                  id="bil-draft-notes"
                  type="text"
                  value={draftNotes}
                  placeholder={bt.billablePanel.notesPlaceholder}
                  onChange={(e) => setDraftNotes(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-hairline)] pt-3">
              <div className="flex flex-wrap items-center gap-4 text-xs">
                <span>
                  <span className="text-[var(--text-secondary)]">{bt.billablePanel.previewSubtotal}: </span>
                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatIdr(draftPreview.subtotal)}
                  </span>
                </span>
                <span>
                  <span className="text-[var(--text-secondary)]">
                    {bt.billablePanel.previewTax} ({draftPreview.rate}%):{" "}
                  </span>
                  <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                    {formatIdr(draftPreview.tax)}
                  </span>
                </span>
                <span>
                  <span className="text-[var(--text-secondary)]">{bt.billablePanel.previewGrandTotal}: </span>
                  <span className="font-code font-bold tabular-nums text-[var(--accent-primary)]">
                    {formatIdr(draftPreview.grand)}
                  </span>
                </span>
              </div>
              <button
                type="submit"
                id="bil-draft-submit-btn"
                disabled={creatingDraft || !selectedTaxCodeId}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <FileSpreadsheet className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{creatingDraft ? bt.billablePanel.creatingDraft : bt.billablePanel.submitDraftCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Inline Irreversible Posting Confirmation Drawer */}
      {postTarget && canPost ? (
        <section
          id="bil-post-drawer"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {bt.invoicesPanel.postDrawerTitlePrefix}{" "}
              <span className="font-code whitespace-nowrap">{postTarget.invoiceNumber}</span> (
              <span className="font-code whitespace-nowrap">{postTarget.customerCode}</span>)
            </span>
            <button
              type="button"
              id="bil-post-cancel-btn"
              onClick={() => setPostTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {bt.billablePanel.cancelBtn}
            </button>
          </div>
          <form onSubmit={handleConfirmPost} className="space-y-3 p-4">
            <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3 py-2 text-xs text-[var(--status-warn-text)]">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{bt.invoicesPanel.postWarningBanner}</span>
            </div>

            <div className="divide-y divide-[var(--border-hairline)] border-y border-[var(--border-hairline)] text-xs">
              <div className="py-1.5 text-[11px] font-semibold text-[var(--text-secondary)]">
                {bt.invoicesPanel.postPreviewHeader} ({postTarget.taxCodeSnapshot})
              </div>
              <div className="flex items-center justify-between py-1.5 font-code tabular-nums">
                <span>{bt.invoicesPanel.glPreviewDrAr}</span>
                <span className="font-bold text-[var(--text-primary)]">{formatIdr(postTarget.grandTotalAmount)}</span>
              </div>
              <div className="flex items-center justify-between py-1.5 font-code tabular-nums">
                <span>{bt.invoicesPanel.glPreviewCrRevenue}</span>
                <span className="text-[var(--text-primary)]">{formatIdr(postTarget.subtotalAmount)}</span>
              </div>
              {Number(postTarget.taxAmount) > 0 ? (
                <div className="flex items-center justify-between py-1.5 font-code tabular-nums">
                  <span>{bt.invoicesPanel.glPreviewCrOutputVat} ({postTarget.taxCodeSnapshot})</span>
                  <span className="text-[var(--text-primary)]">{formatIdr(postTarget.taxAmount)}</span>
                </div>
              ) : null}
            </div>

            <div className="grid grid-cols-1 gap-3 md:grid-cols-12">
              <label className="block space-y-1 md:col-span-8">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.invoicesPanel.postEvidenceLabel}
                </span>
                <input
                  id="bil-post-evidence"
                  type="text"
                  required={postTarget.requiresTaxEvidenceSnapshot}
                  value={postEvidenceRef}
                  placeholder={bt.invoicesPanel.postEvidencePlaceholder}
                  onChange={(e) => setPostEvidenceRef(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <div className="flex items-end justify-end md:col-span-4">
                <button
                  type="submit"
                  id="bil-post-confirm-btn"
                  disabled={postingInvoice || (postTarget.requiresTaxEvidenceSnapshot && !postEvidenceRef.trim())}
                  className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                >
                  <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                  <span>{postingInvoice ? bt.invoicesPanel.posting : bt.invoicesPanel.confirmPostCta}</span>
                </button>
              </div>
            </div>
          </form>
        </section>
      ) : null}

      {/* Inline Credit Note Drawer */}
      {creditTarget && canPost ? (
        <section
          id="bil-credit-drawer"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {bt.invoicesPanel.creditDrawerTitlePrefix}{" "}
              <span className="font-code whitespace-nowrap">{creditTarget.invoiceNumber}</span>
            </span>
            <button
              type="button"
              id="bil-credit-cancel-btn"
              onClick={() => setCreditTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {bt.billablePanel.cancelBtn}
            </button>
          </div>
          <form onSubmit={handleConfirmCredit} className="space-y-3 p-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-12">
              <label className="block space-y-1 md:col-span-3">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.invoicesPanel.creditDateLabel}
                </span>
                <input
                  id="bil-credit-date"
                  type="date"
                  required
                  value={creditDate}
                  onChange={(e) => setCreditDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-4">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.invoicesPanel.creditEvidenceLabel}
                </span>
                <input
                  id="bil-credit-evidence"
                  type="text"
                  required={Number(creditTarget.taxAmount) > 0 || creditTarget.requiresTaxEvidenceSnapshot}
                  value={creditEvidenceRef}
                  placeholder={bt.invoicesPanel.creditEvidencePlaceholder}
                  onChange={(e) => setCreditEvidenceRef(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1 md:col-span-5">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {bt.invoicesPanel.creditNotesLabel}
                </span>
                <input
                  id="bil-credit-notes"
                  type="text"
                  value={creditNotes}
                  placeholder={bt.invoicesPanel.creditNotesPlaceholder}
                  onChange={(e) => setCreditNotes(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
            </div>

            <div className="divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)]">
              {creditTarget.lines
                .filter((l) => Number(l.remainingCreditableBaseQuantity) > 0)
                .map((l, idx) => (
                  <div key={l.invoiceLineId} className="flex flex-wrap items-center justify-between gap-2 py-2.5 text-xs">
                    <div>
                      <span className="font-code font-bold text-[var(--text-primary)]">{l.itemCode}</span> ·{" "}
                      <span>{l.itemName}</span>{" "}
                      <span className="font-code text-[var(--text-secondary)]">
                        ({bt.invoicesPanel.creditMaxPrefix} {l.remainingCreditableBaseQuantity} {l.baseUomCode})
                      </span>
                    </div>
                    <label className="inline-flex items-center gap-2">
                      <span className="text-[11px] font-medium text-[var(--text-secondary)]">
                        {bt.invoicesPanel.creditQtyLabel}
                      </span>
                      <input
                        id={`bil-credit-qty-${idx}`}
                        type="number"
                        step="0.001"
                        min="0.001"
                        max={Number(l.remainingCreditableBaseQuantity)}
                        required
                        value={creditQtys[l.invoiceLineId] ?? ""}
                        onChange={(e) =>
                          setCreditQtys((prev) => ({ ...prev, [l.invoiceLineId]: e.target.value }))
                        }
                        className="font-code min-h-9 w-28 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                      />
                    </label>
                  </div>
                ))}
            </div>

            <div className="flex justify-end border-t border-[var(--border-hairline)] pt-3">
              <button
                type="submit"
                id="bil-credit-submit-btn"
                disabled={postingCredit}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <RotateCcw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{postingCredit ? bt.invoicesPanel.postingCredit : bt.invoicesPanel.confirmCreditCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Horizontally Scrollable Tablist */}
      <div
        role="tablist"
        aria-label={bt.tabListLabel}
        className="flex flex-nowrap items-center gap-1 overflow-x-auto border-b border-[var(--border-hairline)]"
      >
        <button
          type="button"
          role="tab"
          id="bil-tab-billable"
          aria-selected={activeTab === "billable"}
          aria-controls="bil-panel-billable"
          onClick={() => setActiveTab("billable")}
          className={`min-h-10 shrink-0 whitespace-nowrap rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
            activeTab === "billable"
              ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
              : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          {bt.tabs.billable} ({billableLines.length})
        </button>
        <button
          type="button"
          role="tab"
          id="bil-tab-invoices"
          aria-selected={activeTab === "invoices"}
          aria-controls="bil-panel-invoices"
          onClick={() => setActiveTab("invoices")}
          className={`min-h-10 shrink-0 whitespace-nowrap rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
            activeTab === "invoices"
              ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
              : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          {bt.tabs.invoices} ({invoices.length})
        </button>
        <button
          type="button"
          role="tab"
          id="bil-tab-receivables"
          aria-selected={activeTab === "receivables"}
          aria-controls="bil-panel-receivables"
          onClick={() => setActiveTab("receivables")}
          className={`min-h-10 shrink-0 whitespace-nowrap rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
            activeTab === "receivables"
              ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
              : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          {bt.tabs.receivables} ({receivables.length})
        </button>
      </div>

      {/* TAB 1: BILLABLE SHIPMENTS */}
      {activeTab === "billable" ? (
        <div id="bil-panel-billable" role="tabpanel" aria-labelledby="bil-tab-billable">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {bt.billablePanel.title}
            </div>
            {billableLines.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{bt.billablePanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="bil-shipments-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{bt.billablePanel.colShipmentSo}</th>
                      <th scope="col" className="py-2 md:px-2">{bt.billablePanel.colCustomerItem}</th>
                      <th scope="col" className="py-2 md:px-2">{bt.billablePanel.colCapacity}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{bt.billablePanel.colUnitPrice}</th>
                      {canDraft ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{bt.billablePanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {billableLines.map((line) => (
                      <tr key={line.shipmentLineId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.billablePanel.colShipmentSo}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                            {line.shipmentNumber}
                          </div>
                          <div className="font-code flex flex-wrap gap-x-1.5 text-[11px] tabular-nums text-[var(--text-secondary)]">
                            <span className="whitespace-nowrap">{line.dispatchDate}</span>
                            <span className="whitespace-nowrap">{line.salesOrderNumber}</span>
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.billablePanel.colCustomerItem}
                          </span>
                          <div className="font-medium text-[var(--text-primary)]">
                            <span className="font-code">{line.customerCode}</span> · {line.customerName}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span className="font-code font-semibold">{line.itemCode}</span> · {line.itemName}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.billablePanel.colCapacity}
                          </span>
                          <div className="font-code tabular-nums text-[var(--text-primary)]">
                            {bt.billablePanel.shippedPrefix} {line.dispatchedBaseQuantity} {line.baseUomCode} ·{" "}
                            {bt.billablePanel.billedPrefix} {line.alreadyBilledBaseQuantity}
                          </div>
                          <div className="font-code text-[11px] font-bold tabular-nums text-[var(--status-ok-text)]">
                            {bt.billablePanel.eligiblePrefix} {line.remainingBillableBaseQuantity} {line.baseUomCode}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.billablePanel.colUnitPrice}
                          </span>
                          <div className="font-code whitespace-nowrap font-semibold tabular-nums text-[var(--text-primary)]">
                            {formatUnitCostIdr(line.unitPricePerBase)}
                          </div>
                        </td>
                        {canDraft ? (
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {bt.billablePanel.colAction}
                            </span>
                            <button
                              type="button"
                              data-testid={`draft-inv-${line.shipmentNumber}-${line.lineageLineKey}`}
                              onClick={() => openDraftDrawer(line)}
                              className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                            >
                              <FileSpreadsheet className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                              <span>{bt.billablePanel.draftInvoiceBtn}</span>
                            </button>
                          </td>
                        ) : null}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}

      {/* TAB 2: INVOICES & CREDIT NOTES */}
      {activeTab === "invoices" ? (
        <div id="bil-panel-invoices" role="tabpanel" aria-labelledby="bil-tab-invoices">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {bt.invoicesPanel.title}
            </div>
            {invoices.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{bt.invoicesPanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="bil-invoices-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{bt.invoicesPanel.colDocType}</th>
                      <th scope="col" className="py-2 md:px-2">{bt.invoicesPanel.colCustomerSo}</th>
                      <th scope="col" className="py-2 md:px-2">{bt.invoicesPanel.colTaxEvidence}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{bt.invoicesPanel.colAmounts}</th>
                      <th scope="col" className="py-2 md:px-2">{bt.invoicesPanel.colStatusLedger}</th>
                      {hasAnyAction ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{bt.invoicesPanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {invoices.map((inv) => {
                      const canSubmitRow = canDraft && inv.status === "DRAFT" && inv.documentType === "INVOICE";
                      const canCancelRow = canDraft && (inv.status === "DRAFT" || inv.status === "SUBMITTED");
                      const canPostRow = canPost && (inv.status === "DRAFT" || inv.status === "SUBMITTED") && inv.documentType === "INVOICE";
                      const canCreditRow =
                        canPost &&
                        inv.status === "POSTED" &&
                        inv.documentType === "INVOICE" &&
                        inv.lines.some((l) => Number(l.remainingCreditableBaseQuantity) > 0);
                      const rowHasAction = canSubmitRow || canCancelRow || canPostRow || canCreditRow;

                      return (
                        <tr key={inv.invoiceId} className="block p-3 md:table-row md:p-0">
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {bt.invoicesPanel.colDocType}
                            </span>
                            <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                              {inv.invoiceNumber}
                            </div>
                            <div className="text-[11px] font-medium text-[var(--text-secondary)]">
                              {bt.docTypeLabels[inv.documentType] ?? inv.documentType}
                            </div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {inv.invoiceDate}
                            </div>
                            {inv.originalInvoiceNumber ? (
                              <div className="font-code whitespace-nowrap text-[11px] text-[var(--status-danger-text)]">
                                {bt.invoicesPanel.creditOfPrefix} {inv.originalInvoiceNumber}
                              </div>
                            ) : null}
                          </td>

                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {bt.invoicesPanel.colCustomerSo}
                            </span>
                            <div className="font-medium text-[var(--text-primary)]">
                              <span className="font-code">{inv.customerCode}</span> · {inv.customerName}
                            </div>
                            <div className="font-code whitespace-nowrap text-[11px] text-[var(--text-secondary)]">
                              {inv.salesOrderNumber}
                            </div>
                          </td>

                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {bt.invoicesPanel.colTaxEvidence}
                            </span>
                            <div className="font-code whitespace-nowrap font-semibold text-[var(--text-primary)]">
                              {inv.taxCodeSnapshot} ({inv.taxRatePercentSnapshot}%)
                            </div>
                            {inv.taxEvidenceReference ? (
                              <div className="font-code text-[11px] text-[var(--text-secondary)]">
                                {bt.invoicesPanel.evidencePrefix} {inv.taxEvidenceReference}
                              </div>
                            ) : null}
                          </td>

                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {bt.invoicesPanel.colAmounts}
                            </span>
                            <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                              {formatIdr(inv.grandTotalAmount)}
                            </div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {bt.invoicesPanel.subtotalPrefix} {formatIdr(inv.subtotalAmount)}
                            </div>
                            <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {bt.invoicesPanel.taxPrefix} {formatIdr(inv.taxAmount)}
                            </div>
                          </td>

                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {bt.invoicesPanel.colStatusLedger}
                            </span>
                            <div>
                              <BillingStatusBadge
                                status={inv.status}
                                label={bt.statusLabels[inv.status] ?? inv.status}
                              />
                            </div>
                            {inv.glJournalNumber ? (
                              <div className="font-code mt-0.5 whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                                {bt.invoicesPanel.journalPrefix} {inv.glJournalNumber}
                              </div>
                            ) : null}
                          </td>

                          {hasAnyAction ? (
                            <td className={`${rowHasAction ? "block py-1" : "hidden"} md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right`}>
                              {rowHasAction ? (
                                <>
                                  <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                                    {bt.invoicesPanel.colAction}
                                  </span>
                                  <div className="mt-1 flex flex-wrap items-center justify-start gap-1.5 md:mt-0 md:justify-end">
                                    {canSubmitRow && !canPost ? (
                                      <button
                                        type="button"
                                        data-testid={`submit-inv-${inv.invoiceNumber}`}
                                        disabled={busyInvoiceId === inv.invoiceId}
                                        onClick={() => void handleSubmitToFinance(inv)}
                                        className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-2.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                                      >
                                        <Send className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                        <span>{bt.invoicesPanel.submitBtn}</span>
                                      </button>
                                    ) : null}
                                    {canPostRow ? (
                                      <button
                                        type="button"
                                        data-testid={`open-post-${inv.invoiceNumber}`}
                                        onClick={() => openPostDrawer(inv)}
                                        className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-2.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                                      >
                                        <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                        <span>{bt.invoicesPanel.postBtn}</span>
                                      </button>
                                    ) : null}
                                    {canCancelRow ? (
                                      <button
                                        type="button"
                                        data-testid={`cancel-inv-${inv.invoiceNumber}`}
                                        disabled={busyInvoiceId === inv.invoiceId}
                                        onClick={() => void handleCancelDraft(inv)}
                                        className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                                      >
                                        <span>{bt.invoicesPanel.cancelBtn}</span>
                                      </button>
                                    ) : null}
                                    {canCreditRow ? (
                                      <button
                                        type="button"
                                        data-testid={`open-credit-${inv.invoiceNumber}`}
                                        onClick={() => openCreditDrawer(inv)}
                                        className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                                      >
                                        <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                        <span>{bt.invoicesPanel.creditBtn}</span>
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

      {/* TAB 3: AR SUBLEDGER */}
      {activeTab === "receivables" ? (
        <div id="bil-panel-receivables" role="tabpanel" aria-labelledby="bil-tab-receivables">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {bt.receivablesPanel.title}
            </div>
            {receivables.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{bt.receivablesPanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="bil-ar-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{bt.receivablesPanel.colArDoc}</th>
                      <th scope="col" className="py-2 md:px-2">{bt.receivablesPanel.colCustomer}</th>
                      <th scope="col" className="py-2 md:px-2">{bt.receivablesPanel.colDates}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{bt.receivablesPanel.colOriginalCredited}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{bt.receivablesPanel.colOpenBalance}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4">{bt.receivablesPanel.colStatusGl}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {receivables.map((ar) => (
                      <tr key={ar.receivableId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.receivablesPanel.colArDoc}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                            {ar.receivableNumber}
                          </div>
                          <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {ar.invoiceNumber}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.receivablesPanel.colCustomer}
                          </span>
                          <div className="font-medium text-[var(--text-primary)]">
                            <span className="font-code">{ar.customerCode}</span> · {ar.customerName}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.receivablesPanel.colDates}
                          </span>
                          <div className="font-code whitespace-nowrap tabular-nums text-[var(--text-primary)]">
                            {ar.invoiceDate}
                          </div>
                          <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {bt.receivablesPanel.duePrefix} {ar.dueDate}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.receivablesPanel.colOriginalCredited}
                          </span>
                          <div className="font-code whitespace-nowrap tabular-nums text-[var(--text-primary)]">
                            {bt.receivablesPanel.originalPrefix} {formatIdr(ar.originalAmount)}
                          </div>
                          <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {bt.receivablesPanel.creditedPrefix} {formatIdr(ar.creditedAmount)} ·{" "}
                            {bt.receivablesPanel.receivedPrefix} {formatIdr(ar.receivedAmount)}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.receivablesPanel.colOpenBalance}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--accent-primary)]">
                            {formatIdr(ar.openAmount)}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {bt.receivablesPanel.colStatusGl}
                          </span>
                          <div>
                            <BillingStatusBadge
                              status={ar.status}
                              label={bt.arStatusLabels[ar.status] ?? ar.status}
                            />
                          </div>
                          <div className="font-code mt-0.5 whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {ar.glJournalNumber}
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
    </div>
  );
}
