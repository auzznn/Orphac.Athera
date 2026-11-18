"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  CreditCard,
  Eye,
  Plus,
  RefreshCw,
  Scale,
  ShieldCheck,
} from "lucide-react";
import {
  ApiProblemError,
  createSupplierBill,
  EligibleAccrualLineDto,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchPayablesOverview,
  fetchSupplierBillDetail,
  PayablesOverviewResponse,
  previewSupplierBill,
  SupplierBillDetailResponse,
  SupplierBillPreviewResponse,
  updateSupplierBill,
} from "@/lib/api/client";
import { useSessionUser } from "@/components/AppShell";
import {
  BillStatusBadge,
  MatchStatusBadge,
  PaymentBlockBadge,
  SupplierBillDetailSheet,
} from "@/components/payables/SupplierBillDetailSheet";
import { formatIdr, formatUnitCostIdr } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatQuantityValue } from "@/lib/receivingMath";

type PayablesTab = "bills" | "grniQueue" | "subledger" | "policyTax";

function addDaysIso(baseIso: string, days: number): string {
  const parsed = new Date(`${baseIso}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime())) return "2026-12-10";
  parsed.setUTCDate(parsed.getUTCDate() + days);
  return parsed.toISOString().slice(0, 10);
}

export default function PayablesPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = params?.tenantId ?? "";
  const entityId = params?.entityId ?? "";
  const router = useRouter();
  const user = useSessionUser();
  const { locale, t } = useLocale();
  const pt = t.payables;

  const [workspace, setWorkspace] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [overview, setOverview] = useState<PayablesOverviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const [activeTab, setActiveTab] = useState<PayablesTab>("bills");
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedBillDetail, setSelectedBillDetail] = useState<SupplierBillDetailResponse | null>(null);

  // Composer state
  const [composerOpen, setComposerOpen] = useState(false);
  const [editingBillState, setEditingBillState] = useState<{ billId: string; draftLabel: string } | null>(null);
  const [selectedPoId, setSelectedPoId] = useState("");
  const [selectedAccrualLineId, setSelectedAccrualLineId] = useState("");
  const [vendorInvoiceNo, setVendorInvoiceNo] = useState("");
  const [taxInvoiceNo, setTaxInvoiceNo] = useState("");
  const [withholdingSlipRef, setWithholdingSlipRef] = useState("");
  const [invoiceDate, setInvoiceDate] = useState("2026-11-10");
  const [dueDate, setDueDate] = useState("2026-12-10");
  const [previewPostingDate, setPreviewPostingDate] = useState("2026-11-10");
  const [billedQty, setBilledQty] = useState("");
  const [invoiceUnitPrice, setInvoiceUnitPrice] = useState("");
  const [ppnTaxCodeId, setPpnTaxCodeId] = useState("");
  const [withholdingTaxCodeId, setWithholdingTaxCodeId] = useState("");
  const [declaredGross, setDeclaredGross] = useState("");
  const [billNotes, setBillNotes] = useState("");

  const [previewBusy, setPreviewBusy] = useState(false);
  const [saveBusy, setSaveBusy] = useState(false);
  const [composerError, setComposerError] = useState<string | null>(null);
  const [previewResult, setPreviewResult] = useState<SupplierBillPreviewResponse | null>(null);
  const [previewSig, setPreviewSig] = useState<string>("");

  const fetchSnapshot = useCallback(async () => {
    const [ws, ov] = await Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchPayablesOverview(tenantId, entityId),
    ]);
    return { ws, ov };
  }, [tenantId, entityId]);

  const applyLoadError = useCallback(
    (err: unknown) => {
      const apiErr = err as ApiProblemError;
      if (apiErr?.status === 401) {
        router.replace("/login");
        return;
      }
      if (apiErr?.status === 403) {
        setAccessDenied(true);
        return;
      }
      setLoadError(
        (apiErr?.errorCode && pt.errors[apiErr.errorCode]) ||
          apiErr?.detail ||
          pt.loadErrorTitle,
      );
    },
    [router, pt.errors, pt.loadErrorTitle],
  );

  const loadData = useCallback(
    async (isRefresh = false) => {
      if (!tenantId || !entityId) return;
      if (isRefresh) {
        setRefreshing(true);
      } else {
        setLoading(true);
      }
      setLoadError(null);
      setAccessDenied(false);
      try {
        const { ws, ov } = await fetchSnapshot();
        setWorkspace(ws);
        setOverview(ov);
      } catch (err) {
        applyLoadError(err);
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [tenantId, entityId, fetchSnapshot, applyLoadError],
  );

  useEffect(() => {
    if (!tenantId || !entityId) return;
    let active = true;
    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchPayablesOverview(tenantId, entityId),
    ])
      .then(([ws, ov]) => {
        if (!active) return;
        setWorkspace(ws);
        setOverview(ov);
        setLoadError(null);
        setAccessDenied(false);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (!active) return;
        applyLoadError(err);
        setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [tenantId, entityId, applyLoadError]);

  const permissionSet = useMemo(
    () => new Set(workspace?.effectivePermissions ?? []),
    [workspace],
  );
  const canManage = permissionSet.has("ap.manage");
  const canApprove = permissionSet.has("ap.approve") && permissionSet.has("approval.decide");
  const canPost = permissionSet.has("ap.post");
  const isReadOnly = !canManage && !canApprove && !canPost;

  const ppnTaxOptions = useMemo(
    () => (overview?.taxCodes ?? []).filter((tCode) => tCode.taxFamily === "PPN" && tCode.direction === "INPUT"),
    [overview],
  );
  const pphTaxOptions = useMemo(
    () => (overview?.taxCodes ?? []).filter((tCode) => tCode.taxFamily === "PPH" && tCode.direction === "WITHHOLDING"),
    [overview],
  );

  const poGroups = useMemo(() => {
    const map = new Map<string, { purchaseOrderId: string; poNumber: string; supplierName: string; lines: EligibleAccrualLineDto[] }>();
    for (const line of overview?.eligibleAccrualLines ?? []) {
      const existing = map.get(line.purchaseOrderId);
      if (existing) {
        existing.lines.push(line);
      } else {
        map.set(line.purchaseOrderId, {
          purchaseOrderId: line.purchaseOrderId,
          poNumber: line.poNumber,
          supplierName: line.supplierName,
          lines: [line],
        });
      }
    }
    return Array.from(map.values());
  }, [overview]);

  const accrualLinesForPo = useMemo(
    () => poGroups.find((g) => g.purchaseOrderId === selectedPoId)?.lines ?? [],
    [poGroups, selectedPoId],
  );

  const chosenAccrualLine = useMemo(
    () => accrualLinesForPo.find((l) => l.receiptAccrualLineId === selectedAccrualLineId) ?? null,
    [accrualLinesForPo, selectedAccrualLineId],
  );

  const currentSig = useMemo(
    () =>
      JSON.stringify({
        selectedPoId,
        selectedAccrualLineId,
        invoiceDate,
        previewPostingDate,
        billedQty,
        invoiceUnitPrice,
        ppnTaxCodeId,
        withholdingTaxCodeId,
        declaredGross,
      }),
    [
      selectedPoId,
      selectedAccrualLineId,
      invoiceDate,
      previewPostingDate,
      billedQty,
      invoiceUnitPrice,
      ppnTaxCodeId,
      withholdingTaxCodeId,
      declaredGross,
    ],
  );
  const previewIsStale = Boolean(previewResult && previewSig !== currentSig);

  function seedComposerFromLine(line: EligibleAccrualLineDto) {
    const defaultPpn =
      line.linePpnTaxCodeId ??
      ppnTaxOptions.find((x) => x.code === "PPN-IN-11")?.taxCodeId ??
      ppnTaxOptions[0]?.taxCodeId ??
      "";
    const qtyNum = Number(line.remainingUnbilledBaseQty || 0);
    const unitNum = Number(line.unitCostBase || 0);
    const ppnObj = ppnTaxOptions.find((x) => x.taxCodeId === defaultPpn);
    const ratio = ppnObj
      ? Number(ppnObj.taxableBaseRatioNumerator) / Number(ppnObj.taxableBaseRatioDenominator || 1)
      : 1;
    const rate = ppnObj ? Number(ppnObj.nominalRatePercent) / 100 : 0;
    const net = Math.round(qtyNum * unitNum);
    const grossEst = net + Math.round(net * ratio * rate);

    setSelectedPoId(line.purchaseOrderId);
    setSelectedAccrualLineId(line.receiptAccrualLineId);
    setBilledQty(String(qtyNum));
    setInvoiceUnitPrice(String(unitNum));
    setPpnTaxCodeId(defaultPpn);
    setWithholdingTaxCodeId("");
    setDueDate(addDaysIso(invoiceDate, Number(line.defaultPaymentTermsDays || 30)));
    setDeclaredGross(String(grossEst));
    setComposerError(null);
    setPreviewResult(null);
    setPreviewSig("");
    setComposerOpen(true);
    setActiveTab("bills");
  }

  function handleOpenNewComposer() {
    setEditingBillState(null);
    if (overview && overview.eligibleAccrualLines.length > 0) {
      seedComposerFromLine(overview.eligibleAccrualLines[0]!);
    } else {
      setComposerError(null);
      setPreviewResult(null);
      setComposerOpen(true);
    }
  }

  function handleEditDraft(detail: SupplierBillDetailResponse) {
    const b = detail.bill;
    const firstLine = detail.lines[0];
    setEditingBillState({ billId: b.supplierBillId, draftLabel: b.billNumber ?? b.draftCode });
    setSelectedPoId(b.purchaseOrderId);
    setSelectedAccrualLineId(firstLine?.receiptAccrualLineId ?? "");
    setVendorInvoiceNo(b.supplierInvoiceNumber);
    setTaxInvoiceNo(b.taxInvoiceNumber ?? "");
    setWithholdingSlipRef("");
    setInvoiceDate(b.invoiceDate);
    setDueDate(b.dueDate);
    setDeclaredGross(String(b.declaredGrossAmount));
    setBilledQty(firstLine ? String(firstLine.billedBaseQuantity) : "");
    setInvoiceUnitPrice(firstLine ? String(firstLine.invoiceUnitPriceBase) : "");
    setPpnTaxCodeId(firstLine?.ppnTaxCodeId ?? "");
    setWithholdingTaxCodeId(firstLine?.withholdingTaxCodeId ?? "");
    setBillNotes(firstLine?.lineNotes ?? "");
    setComposerError(null);
    setPreviewResult(null);
    setPreviewSig("");
    setComposerOpen(true);
    setActiveTab("bills");
  }

  async function handleRunPreview() {
    if (!selectedPoId || !selectedAccrualLineId || !ppnTaxCodeId) return;
    setPreviewBusy(true);
    setComposerError(null);
    try {
      const qtyNum = Number(billedQty || "0");
      const priceNum = Number(invoiceUnitPrice || "0");
      const grossNum = Number(declaredGross || "0");
      const outcome = await previewSupplierBill(tenantId, entityId, {
        billType: "PO_THREE_WAY_MATCH",
        purchaseOrderId: selectedPoId,
        currencyCode: "IDR",
        invoiceDate,
        postingDate: previewPostingDate || null,
        declaredGrossAmount: grossNum,
        lines: [
          {
            receiptAccrualLineId: selectedAccrualLineId,
            billedBaseQuantity: qtyNum,
            invoiceUnitPriceBase: priceNum,
            ppnTaxCodeId,
            withholdingTaxCodeId: withholdingTaxCodeId || null,
            lineNotes: null,
          },
        ],
      });
      setPreviewResult(outcome);
      setPreviewSig(currentSig);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr?.status === 401) {
        router.replace("/login");
        return;
      }
      setComposerError(
        (apiErr?.errorCode && pt.errors[apiErr.errorCode]) ||
          apiErr?.detail ||
          pt.loadErrorTitle,
      );
    } finally {
      setPreviewBusy(false);
    }
  }

  async function handleSaveDraft(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedPoId || !selectedAccrualLineId || !ppnTaxCodeId || !vendorInvoiceNo.trim()) return;
    setSaveBusy(true);
    setComposerError(null);
    const linesPayload = [
      {
        receiptAccrualLineId: selectedAccrualLineId,
        billedBaseQuantity: Number(billedQty || "0"),
        invoiceUnitPriceBase: Number(invoiceUnitPrice || "0"),
        ppnTaxCodeId,
        withholdingTaxCodeId: withholdingTaxCodeId || null,
        lineNotes: billNotes.trim() || null,
      },
    ];
    try {
      const saved = editingBillState
        ? await updateSupplierBill(tenantId, entityId, editingBillState.billId, {
            supplierInvoiceNumber: vendorInvoiceNo.trim(),
            taxInvoiceNumber: taxInvoiceNo.trim() || null,
            withholdingSlipReference: withholdingSlipRef.trim() || null,
            invoiceDate,
            dueDate,
            declaredGrossAmount: Number(declaredGross || "0"),
            notes: billNotes.trim() || null,
            lines: linesPayload,
          })
        : await createSupplierBill(tenantId, entityId, {
            billType: "PO_THREE_WAY_MATCH",
            purchaseOrderId: selectedPoId,
            supplierInvoiceNumber: vendorInvoiceNo.trim(),
            taxInvoiceNumber: taxInvoiceNo.trim() || null,
            withholdingSlipReference: withholdingSlipRef.trim() || null,
            invoiceDate,
            dueDate,
            currencyCode: "IDR",
            declaredGrossAmount: Number(declaredGross || "0"),
            notes: billNotes.trim() || null,
            lines: linesPayload,
          });
      setComposerOpen(false);
      setEditingBillState(null);
      setPreviewResult(null);
      setVendorInvoiceNo("");
      setTaxInvoiceNo("");
      setWithholdingSlipRef("");
      setBillNotes("");
      setSelectedBillDetail(saved);
      await loadData(true);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr?.status === 401) {
        router.replace("/login");
        return;
      }
      setComposerError(
        (apiErr?.errorCode && pt.errors[apiErr.errorCode]) ||
          apiErr?.detail ||
          pt.loadErrorTitle,
      );
    } finally {
      setSaveBusy(false);
    }
  }

  async function handleOpenBillDetail(billId: string) {
    try {
      const detail = await fetchSupplierBillDetail(tenantId, entityId, billId);
      setSelectedBillDetail(detail);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      if (apiErr?.status === 401) {
        router.replace("/login");
        return;
      }
      setLoadError(
        (apiErr?.errorCode && pt.errors[apiErr.errorCode]) ||
          apiErr?.detail ||
          pt.loadErrorTitle,
      );
    }
  }

  const filteredBills = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return (overview?.bills ?? []).filter((b) => {
      if (!q) return true;
      return (
        (b.billNumber ?? b.draftCode).toLowerCase().includes(q) ||
        b.supplierInvoiceNumber.toLowerCase().includes(q) ||
        b.poNumber.toLowerCase().includes(q) ||
        b.supplierLegalName.toLowerCase().includes(q) ||
        b.supplierCode.toLowerCase().includes(q)
      );
    });
  }, [overview, searchQuery]);

  if (loading) {
    return (
      <div className="mx-auto max-w-5xl rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
        <div className="h-4 w-56 animate-pulse rounded bg-[var(--bg-chrome)]" />
        <p className="mt-3 text-sm text-[var(--text-secondary)]">{t.shell.loadingSession}</p>
      </div>
    );
  }

  if (accessDenied) {
    return (
      <section
        data-testid="ap-access-denied"
        className="mx-auto max-w-3xl rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
      >
        <div className="flex items-start gap-3 text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div>
            <h1 className="text-base font-semibold text-[var(--text-primary)]">{pt.accessDeniedTitle}</h1>
            <p className="mt-1.5 text-sm leading-relaxed text-[var(--text-secondary)]">{pt.accessDeniedBody}</p>
          </div>
        </div>
        <div className="mt-5">
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.backToOverview}</span>
          </Link>
        </div>
      </section>
    );
  }

  if (loadError) {
    return (
      <section className="mx-auto max-w-3xl rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6">
        <div className="flex items-start gap-3 text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div>
            <h1 className="text-base font-semibold text-[var(--text-primary)]">{pt.loadErrorTitle}</h1>
            <p className="mt-1.5 text-sm text-[var(--text-secondary)]">{loadError}</p>
          </div>
        </div>
        <div className="mt-4">
          <button
            type="button"
            onClick={() => void loadData(false)}
            className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)]"
          >
            <RefreshCw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.retryButton}</span>
          </button>
        </div>
      </section>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      {/* Page Title & Action Strip */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Link
            href={`/w/${tenantId}/${entityId}`}
            className="inline-flex min-h-9 items-center gap-1.5 text-xs font-semibold text-[var(--accent-primary)] hover:underline"
          >
            <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.backToOverview}</span>
          </Link>
          <h1 className="mt-1 text-xl font-bold tracking-tight text-[var(--text-primary)]">
            {pt.pageTitle}
          </h1>
          {workspace ? (
            <p className="font-code mt-0.5 text-xs text-[var(--text-secondary)]">
              {workspace.tenant.code} · {workspace.entity.code} ({workspace.entity.name}) ·{" "}
              {workspace.entity.functionalCurrencyCode}
            </p>
          ) : null}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => void loadData(true)}
            disabled={refreshing}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`} strokeWidth={1.75} aria-hidden="true" />
            <span>{pt.refreshButton}</span>
          </button>

          {canManage ? (
            <button
              type="button"
              id="ap-new-bill-btn"
              onClick={handleOpenNewComposer}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <Plus className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
              <span>{pt.newBillButton}</span>
            </button>
          ) : null}
        </div>
      </div>

      {/* Role / OD-03 / OD-08 Governance Banners */}
      {isReadOnly ? (
        <div
          data-testid="ap-readonly-banner"
          role="status"
          className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
        >
          <Eye className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{pt.readOnlyBanner}</span>
        </div>
      ) : null}

      <div
        data-testid="ap-od03-governance-banner"
        className="rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
      >
        <div className="flex items-start gap-2">
          <Scale className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div className="space-y-1">
            <p className="font-semibold">{pt.od03GovernanceTitle}</p>
            <p>{pt.od03GovernanceBody}</p>
            <p>{pt.od08GovernanceBody}</p>
          </div>
        </div>
      </div>

      {/* 4-Metric Hairline Strip */}
      {overview ? (
        <div className="grid grid-cols-1 gap-px overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--border-hairline)] sm:grid-cols-2 lg:grid-cols-4">
          <div className="bg-[var(--bg-surface)] px-4 py-3.5">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{pt.stats.openPayableLiability}</p>
            <p className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
              {formatIdr(overview.openPayableTotalIdr)}
            </p>
          </div>
          <div className="bg-[var(--bg-surface)] px-4 py-3.5">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{pt.stats.openGrniPool}</p>
            <p className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
              {formatIdr(overview.openGrniBalanceIdr)}
            </p>
          </div>
          <div className="bg-[var(--bg-surface)] px-4 py-3.5">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{pt.stats.pendingApprovalBills}</p>
            <p className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
              {String(overview.pendingApprovalBillCount)}
            </p>
          </div>
          <div className="bg-[var(--bg-surface)] px-4 py-3.5">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{pt.stats.blockedPayables}</p>
            <p className="font-code mt-1 text-lg font-bold tabular-nums text-[var(--text-primary)]">
              {String(overview.blockedPayableCount)}
            </p>
          </div>
        </div>
      ) : null}

      {/* Workbench Tabs */}
      <div
        role="tablist"
        aria-label={pt.tabListLabel}
        className="flex flex-wrap gap-1.5 border-b border-[var(--border-hairline)] pb-2.5"
      >
        {(
          [
            ["bills", pt.tabs.bills],
            ["grniQueue", pt.tabs.grniQueue],
            ["subledger", pt.tabs.subledger],
            ["policyTax", pt.tabs.policyTax],
          ] as const
        ).map(([key, label]) => (
          <button
            key={key}
            id={`ap-tab-${key}`}
            type="button"
            role="tab"
            aria-selected={activeTab === key}
            onClick={() => setActiveTab(key)}
            className={`min-h-9 rounded-[var(--radius-sm)] px-3 text-xs font-semibold ${
              activeTab === key
                ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "border border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* Inline Three-Way Match Composer + Live Preview (Flat divide-y Sheet) */}
      {composerOpen && canManage ? (
        <section
          id="ap-bill-composer"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <div className="flex items-center gap-2">
              <CreditCard className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                {editingBillState ? `${pt.composer.titleEditPrefix}: ${editingBillState.draftLabel}` : pt.composer.titleNew}
              </h2>
            </div>
            <button
              type="button"
              onClick={() => {
                setComposerOpen(false);
                setEditingBillState(null);
              }}
              className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {pt.composer.cancelButton}
            </button>
          </div>

          <form onSubmit={handleSaveDraft} className="divide-y divide-[var(--border-hairline)]">
            <div className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 lg:grid-cols-4">
              <label className="block space-y-1 sm:col-span-2">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.poSelectLabel}
                </span>
                <select
                  id="ap-composer-po"
                  disabled={editingBillState !== null}
                  value={selectedPoId}
                  onChange={(e) => {
                    const poId = e.target.value;
                    const first = poGroups.find((g) => g.purchaseOrderId === poId)?.lines[0];
                    if (first) seedComposerFromLine(first);
                  }}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="" disabled>
                    {pt.composer.emptyPoOption}
                  </option>
                  {poGroups.map((g) => (
                    <option key={g.purchaseOrderId} value={g.purchaseOrderId}>
                      {g.poNumber} — {g.supplierName}
                    </option>
                  ))}
                </select>
              </label>

              <label className="block space-y-1 sm:col-span-2">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.accrualLineSelectLabel}
                </span>
                <select
                  id="ap-composer-accrual"
                  value={selectedAccrualLineId}
                  onChange={(e) => {
                    const chosen = accrualLinesForPo.find((l) => l.receiptAccrualLineId === e.target.value);
                    if (chosen) seedComposerFromLine(chosen);
                  }}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="" disabled>
                    {pt.composer.emptyAccrualOption}
                  </option>
                  {accrualLinesForPo.map((l) => (
                    <option key={l.receiptAccrualLineId} value={l.receiptAccrualLineId}>
                      {l.accrualNumber} · {l.itemCode} — {pt.micro.unbilledGrniShort}{" "}
                      {formatQuantityValue(l.remainingUnbilledBaseQty, locale)} {l.baseUomCode} @{" "}
                      {formatUnitCostIdr(l.unitCostBase)} ({pt.micro.poolOnHandShort}:{" "}
                      {formatQuantityValue(l.currentPoolOnHandQty, locale)})
                    </option>
                  ))}
                </select>
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.vendorInvoiceLabel}
                </span>
                <input
                  id="ap-composer-vendor-inv"
                  type="text"
                  required
                  placeholder="INV-VND-2026-001"
                  value={vendorInvoiceNo}
                  onChange={(e) => setVendorInvoiceNo(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.taxInvoiceLabel}
                </span>
                <input
                  id="ap-composer-tax-inv"
                  type="text"
                  placeholder="010.001-26.00000001"
                  value={taxInvoiceNo}
                  onChange={(e) => setTaxInvoiceNo(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.invoiceDateLabel}
                </span>
                <input
                  id="ap-composer-inv-date"
                  type="date"
                  value={invoiceDate}
                  onChange={(e) => setInvoiceDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.dueDateLabel}
                </span>
                <input
                  id="ap-composer-due-date"
                  type="date"
                  value={dueDate}
                  onChange={(e) => setDueDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.billedQtyLabel}
                </span>
                <input
                  id="ap-composer-qty"
                  type="text"
                  inputMode="decimal"
                  required
                  value={billedQty}
                  onChange={(e) => setBilledQty(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.invoiceUnitPriceLabel}
                </span>
                <input
                  id="ap-composer-unit-price"
                  type="text"
                  inputMode="decimal"
                  required
                  value={invoiceUnitPrice}
                  onChange={(e) => setInvoiceUnitPrice(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.ppnCodeLabel}
                </span>
                <select
                  id="ap-composer-ppn"
                  value={ppnTaxCodeId}
                  onChange={(e) => setPpnTaxCodeId(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="" disabled>
                    {pt.composer.selectOptionPlaceholder}
                  </option>
                  {ppnTaxOptions.map((tx) => (
                    <option key={tx.taxCodeId} value={tx.taxCodeId}>
                      {tx.code} — {tx.name}
                    </option>
                  ))}
                </select>
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.pphCodeLabel}
                </span>
                <select
                  id="ap-composer-pph"
                  value={withholdingTaxCodeId}
                  onChange={(e) => setWithholdingTaxCodeId(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="">{pt.composer.noneOption}</option>
                  {pphTaxOptions.map((tx) => (
                    <option key={tx.taxCodeId} value={tx.taxCodeId}>
                      {tx.code} — {tx.name}
                    </option>
                  ))}
                </select>
              </label>

              <div className="space-y-1 sm:col-span-2">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.declaredGrossLabel}
                </span>
                <div className="flex flex-wrap gap-2">
                  <input
                    id="ap-composer-declared-gross"
                    type="text"
                    inputMode="decimal"
                    required
                    value={declaredGross}
                    onChange={(e) => setDeclaredGross(e.target.value)}
                    className="font-code min-h-9 flex-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                  />
                  {previewResult ? (
                    <button
                      type="button"
                      id="ap-sync-gross-btn"
                      onClick={() => setDeclaredGross(String(previewResult.computedGrossTotal))}
                      className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                    >
                      {pt.composer.syncComputedGrossButton} ({formatIdr(previewResult.computedGrossTotal)})
                    </button>
                  ) : null}
                </div>
              </div>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.withholdingSlipLabel}
                </span>
                <input
                  id="ap-composer-withholding-slip"
                  type="text"
                  value={withholdingSlipRef}
                  onChange={(e) => setWithholdingSlipRef(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>

              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {pt.composer.postingDateLabel}
                </span>
                <input
                  id="ap-composer-post-date"
                  type="date"
                  value={previewPostingDate}
                  onChange={(e) => setPreviewPostingDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
            </div>

            {chosenAccrualLine ? (
              <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs text-[var(--text-secondary)]">
                <span className="font-code font-semibold text-[var(--text-primary)]">
                  {chosenAccrualLine.itemCode}
                </span>
                {" · "}
                {pt.micro.poolOnHandShort}:{" "}
                <span className="font-code tabular-nums text-[var(--text-primary)]">
                  {formatQuantityValue(chosenAccrualLine.currentPoolOnHandQty, locale)} {chosenAccrualLine.baseUomCode}
                </span>{" "}
                ({formatIdr(chosenAccrualLine.currentPoolCarryingValue)}) · {pt.micro.unbilledGrniShort}:{" "}
                <span className="font-code tabular-nums text-[var(--text-primary)]">
                  {formatQuantityValue(chosenAccrualLine.remainingUnbilledBaseQty, locale)}{" "}
                  {chosenAccrualLine.baseUomCode}
                </span>
              </div>
            ) : null}

            {composerError ? (
              <div className="px-4 py-3">
                <div
                  role="alert"
                  data-testid="ap-composer-error"
                  className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3 py-2 text-xs text-[var(--status-danger-text)]"
                >
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{composerError}</span>
                </div>
              </div>
            ) : null}

            <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
              <div className="flex flex-wrap items-center gap-2.5">
                <button
                  type="button"
                  id="ap-preview-match-btn"
                  disabled={previewBusy || !selectedAccrualLineId || !ppnTaxCodeId}
                  onClick={() => void handleRunPreview()}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                >
                  <Scale className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                  <span>{previewBusy ? pt.composer.previewBusy : pt.composer.previewButton}</span>
                </button>

                <button
                  type="submit"
                  id="ap-save-draft-btn"
                  disabled={saveBusy || !selectedAccrualLineId || !ppnTaxCodeId || !vendorInvoiceNo.trim()}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                >
                  <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                  <span>
                    {saveBusy
                      ? pt.composer.saveBusy
                      : editingBillState
                        ? pt.composer.updateDraftButton
                        : pt.composer.saveDraftButton}
                  </span>
                </button>
              </div>
              {previewIsStale ? (
                <p className="text-xs font-medium text-[var(--status-warn-text)]">{pt.composer.stalePreviewNotice}</p>
              ) : null}
            </div>

            {/* Live Three-Way Match Comparison + GL Preview */}
            {previewResult ? (
              <div data-testid="ap-live-match-preview" className="divide-y divide-[var(--border-hairline)]">
                <div className="flex flex-wrap items-center justify-between gap-2 bg-[var(--bg-subtle)] px-4 py-2.5">
                  <span className="text-xs font-semibold text-[var(--text-primary)]">{pt.composer.previewTitle}</span>
                  <div className="flex flex-wrap items-center gap-1.5">
                    <MatchStatusBadge
                      status={previewResult.overallMatchStatus}
                      label={pt.matchStatusLabels[previewResult.overallMatchStatus] ?? previewResult.overallMatchStatus}
                    />
                    <MatchStatusBadge
                      status={previewResult.dqOutcome}
                      label={`DQ: ${pt.dimensionOutcomeLabels[previewResult.dqOutcome] ?? previewResult.dqOutcome}`}
                    />
                    <MatchStatusBadge
                      status={previewResult.ppOutcome}
                      label={`PP: ${pt.dimensionOutcomeLabels[previewResult.ppOutcome] ?? previewResult.ppOutcome}`}
                    />
                    <MatchStatusBadge
                      status={previewResult.bdOutcome}
                      label={`BD: ${pt.dimensionOutcomeLabels[previewResult.bdOutcome] ?? previewResult.bdOutcome}`}
                    />
                  </div>
                </div>

                {previewResult.blockingReasons.length > 0 ? (
                  <div className="px-4 py-2.5 text-xs text-[var(--status-danger-text)]">
                    {previewResult.blockingReasons.join(" | ")}
                  </div>
                ) : null}

                {/* Stock-Coverage Split Metric Band */}
                <div className="grid grid-cols-1 gap-px bg-[var(--border-hairline)] sm:grid-cols-2 lg:grid-cols-4">
                  <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                    <p className="text-[11px] font-medium text-[var(--text-secondary)]">{pt.composer.coveredStockLabel}</p>
                    <p className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--text-primary)]">
                      {formatIdr(previewResult.totalInventoryAdjAmount)}
                    </p>
                  </div>
                  <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                    <p className="text-[11px] font-medium text-[var(--text-secondary)]">{pt.composer.uncoveredPpvLabel}</p>
                    <p className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--text-primary)]">
                      {formatIdr(previewResult.totalPnlVarianceAmount)}
                    </p>
                  </div>
                  <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                    <p className="text-[11px] font-medium text-[var(--text-secondary)]">{pt.composer.bdDiffLabel}</p>
                    <p className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--text-primary)]">
                      {formatIdr(previewResult.bdWriteOffAmount)}
                    </p>
                  </div>
                  <div className="bg-[var(--bg-surface)] px-4 py-2.5">
                    <p className="text-[11px] font-medium text-[var(--text-secondary)]">{pt.composer.netApLabel}</p>
                    <p className="font-code mt-0.5 text-sm font-bold tabular-nums text-[var(--accent-primary)]">
                      {formatIdr(previewResult.netPayableAmount)}
                    </p>
                  </div>
                </div>

                {/* Comparison Grid */}
                <div className="md:overflow-x-auto">
                  <table className="w-full border-collapse text-left text-xs">
                    <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                      <tr>
                        <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.composer.colOrderedAcceptedOpen}</th>
                        <th scope="col" className="py-2 md:px-2 text-right">{pt.composer.colBilledPrice}</th>
                        <th scope="col" className="py-2 md:px-2 text-right">{pt.composer.colVarianceSplit}</th>
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.composer.colTaxes}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--border-hairline)]">
                      {previewResult.lines.map((line) => (
                        <tr key={line.receiptAccrualLineId} className="block p-3 md:table-row md:p-0">
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.composer.colOrderedAcceptedOpen}
                            </span>
                            <div className="font-semibold text-[var(--text-primary)]">
                              <span className="font-code">{line.itemCode}</span> — {line.itemName}
                            </div>
                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              PO {formatQuantityValue(line.orderedBaseQty, locale)} @ {formatUnitCostIdr(line.poUnitPriceFunctional)} · {pt.micro.openShort}:{" "}
                              {formatQuantityValue(line.remainingUnbilledBaseQty, locale)} · {pt.micro.poolOnHandShort}:{" "}
                              {formatQuantityValue(line.currentPoolOnHandQty, locale)}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.composer.colBilledPrice}
                            </span>
                            <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                              {formatQuantityValue(line.billedBaseQuantity, locale)} {line.baseUomCode} @{" "}
                              {formatUnitCostIdr(line.invoiceUnitPriceBase)}
                            </div>
                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {pt.micro.netShort} {formatIdr(line.lineNetAmount)} (GRNI {formatIdr(line.grniClearAmount)})
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.composer.colVarianceSplit}
                            </span>
                            <div className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                              {pt.micro.inv1300Short}: {formatIdr(line.inventoryAdjustmentAmount)} · {pt.micro.ppv5200Short}: {formatIdr(line.pnlVarianceAmount)}
                            </div>
                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {pt.composer.projectedCostLabel}: {formatUnitCostIdr(line.projectedMovingAvgCost)}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.composer.colTaxes}
                            </span>
                            <div className="font-code tabular-nums text-[var(--text-primary)]">
                              {line.ppnTaxCodeSnapshot}: {formatIdr(line.ppnTaxAmount)}
                            </div>
                            {line.withholdingTaxCodeSnapshot ? (
                              <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                                {line.withholdingTaxCodeSnapshot}:{" "}
                                <span className="whitespace-nowrap">-{formatIdr(line.withholdingTaxAmount)}</span>
                              </div>
                            ) : null}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Journal Preview */}
                {previewResult.journalPreview.length > 0 ? (
                  <div>
                    <div className="bg-[var(--bg-subtle)] px-4 py-2 text-xs font-semibold text-[var(--text-primary)]">
                      {pt.composer.journalPreviewTitle}
                    </div>
                    <table className="w-full border-collapse text-left text-xs">
                      <thead className="hidden border-y border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                        <tr>
                          <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.composer.colJournalSide}</th>
                          <th scope="col" className="py-2 md:px-2">{pt.composer.colJournalAccount}</th>
                          <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.composer.colJournalAmount}</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[var(--border-hairline)]">
                        {previewResult.journalPreview.map((jLine, idx) => (
                          <tr key={`${jLine.accountCode}-${idx}`} className="block p-3 md:table-row md:p-0">
                            <td className="block py-1 font-semibold text-[var(--text-primary)] md:table-cell md:w-24 md:py-2 md:pl-4 md:pr-2">
                              <span className="mr-1.5 font-medium text-[var(--text-secondary)] md:hidden">
                                {pt.composer.colJournalSide}:
                              </span>
                              {jLine.side === "DEBIT" ? pt.composer.sideDebit : pt.composer.sideCredit}
                            </td>
                            <td className="block py-1 md:table-cell md:py-2 md:px-2">
                              <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                                {pt.composer.colJournalAccount}
                              </span>
                              <span className="font-code font-semibold text-[var(--text-primary)]">{jLine.accountCode}</span>{" "}
                              <span className="text-[var(--text-secondary)]">— {jLine.accountName}</span>
                              <div className="text-[11px] text-[var(--text-secondary)]">{jLine.description}</div>
                            </td>
                            <td className="block py-1 font-code font-bold tabular-nums text-[var(--text-primary)] md:table-cell md:py-2 md:pl-2 md:pr-4 md:text-right">
                              <span className="mr-1.5 font-sans font-medium text-[var(--text-secondary)] md:hidden">
                                {pt.composer.colJournalAmount}:
                              </span>
                              {formatIdr(jLine.amount)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : null}
              </div>
            ) : null}
          </form>
        </section>
      ) : null}

      {/* Tab 1: Supplier Bills + Detail Sheet */}
      {activeTab === "bills" && overview ? (
        <div className="space-y-4">
          <div className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <div className="flex items-center gap-2">
                <ShieldCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                <h2 className="text-xs font-semibold text-[var(--text-primary)]">{pt.billsCardTitle}</h2>
              </div>
              <input
                id="ap-search-input"
                type="search"
                aria-label={pt.filterSearchLabel}
                placeholder={pt.filterSearchPlaceholder}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="min-h-9 w-full max-w-xs rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
              />
            </div>

            {filteredBills.length === 0 ? (
              <div className="p-6 text-center">
                <p className="text-sm font-semibold text-[var(--text-primary)]">{pt.emptyBillsTitle}</p>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">{pt.emptyBillsBody}</p>
              </div>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ap-bills-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.detail.headerRef}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.detail.headerVendor}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.detail.headerMatchStatus}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{pt.detail.headerTotals}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{pt.detail.headerPaymentBlock}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.detail.colAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {filteredBills.map((b) => {
                      const isSelected = selectedBillDetail?.bill.supplierBillId === b.supplierBillId;
                      return (
                        <tr
                          key={b.supplierBillId}
                          className={`block p-3 md:table-row md:p-0 ${
                            isSelected ? "bg-[var(--accent-tint)]" : "hover:bg-[var(--bg-subtle)]"
                          }`}
                        >
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.detail.headerRef}
                            </span>
                            <div className="font-code font-bold text-[var(--text-primary)]">
                              {b.billNumber ?? b.draftCode}
                            </div>
                            <div className="font-code text-[11px] text-[var(--text-secondary)]">
                              {b.poNumber} · {b.supplierInvoiceNumber}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.detail.headerVendor}
                            </span>
                            <div className="font-medium text-[var(--text-primary)]">{b.supplierLegalName}</div>
                            <div className="font-code text-[11px] text-[var(--text-secondary)]">
                              {b.invoiceDate} → {b.dueDate}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="mb-1 block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.detail.headerMatchStatus}
                            </span>
                            <div className="flex flex-wrap gap-1.5">
                              <BillStatusBadge
                                status={b.status}
                                label={pt.billStatusLabels[b.status] ?? b.status}
                              />
                              <MatchStatusBadge
                                status={b.matchStatus}
                                label={pt.matchStatusLabels[b.matchStatus] ?? b.matchStatus}
                              />
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.detail.headerTotals}
                            </span>
                            <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                              {formatIdr(b.netPayableAmount)}
                            </div>
                            <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {pt.micro.varShort}: {formatIdr(b.totalPriceVarianceAmount)}
                            </div>
                          </td>
                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                            <span className="mb-1 block font-medium text-[var(--text-secondary)] md:hidden">
                              {pt.detail.headerPaymentBlock}
                            </span>
                            <PaymentBlockBadge
                              status={b.paymentBlockStatus}
                              label={pt.paymentBlockLabels[b.paymentBlockStatus] ?? b.paymentBlockStatus}
                            />
                          </td>
                          <td className="block pt-2 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                            <button
                              type="button"
                              onClick={() => void handleOpenBillDetail(b.supplierBillId)}
                              className={`inline-flex min-h-9 items-center whitespace-nowrap rounded-[var(--radius-sm)] px-3 text-xs font-semibold ${
                                isSelected
                                  ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                                  : "border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                              }`}
                            >
                              {isSelected ? pt.detail.activeDetailButton : pt.detail.openDetailButton}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {selectedBillDetail ? (
            <SupplierBillDetailSheet
              key={`${selectedBillDetail.bill.supplierBillId}:${selectedBillDetail.bill.status}`}
              tenantId={tenantId}
              entityId={entityId}
              detail={selectedBillDetail}
              currentUserEmail={user.email}
              canManage={canManage}
              canApprove={canApprove}
              canPost={canPost}
              onEditDraft={
                selectedBillDetail.bill.status === "DRAFT" && canManage
                  ? () => handleEditDraft(selectedBillDetail)
                  : undefined
              }
              onClose={() => setSelectedBillDetail(null)}
              onAuthExpired={() => router.replace("/login")}
              onActionCompleted={async (updated) => {
                setSelectedBillDetail(updated);
                await loadData(true);
              }}
            />
          ) : null}
        </div>
      ) : null}

      {/* Tab 2: Open GRNI Queue */}
      {activeTab === "grniQueue" && overview ? (
        <div className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            {pt.grniQueueCardTitle}
          </div>
          {overview.eligibleAccrualLines.length === 0 ? (
            <div className="p-6 text-center">
              <p className="text-sm font-semibold text-[var(--text-primary)]">{pt.emptyGrniTitle}</p>
              <p className="mt-1 text-xs text-[var(--text-secondary)]">{pt.emptyGrniBody}</p>
            </div>
          ) : (
            <div className="md:overflow-x-auto">
              <table id="ap-grni-table" className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.composer.accrualLineSelectLabel}</th>
                    <th scope="col" className="py-2 md:px-2">{pt.detail.headerVendor}</th>
                    <th scope="col" className="py-2 md:px-2 text-right">{pt.composer.colOrderedAcceptedOpen}</th>
                    <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.createBillAction}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.eligibleAccrualLines.map((line) => (
                    <tr key={line.receiptAccrualLineId} className="block p-3 md:table-row md:p-0">
                      <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                          {pt.composer.accrualLineSelectLabel}
                        </span>
                        <div className="font-code font-bold text-[var(--text-primary)]">
                          {line.accrualNumber} · {line.itemCode}
                        </div>
                        <div className="text-[11px] text-[var(--text-secondary)]">
                          {line.itemName} · PO <span className="font-code">{line.poNumber}</span> · GRN{" "}
                          <span className="font-code">{line.receiptNumber}</span>
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                          {pt.detail.headerVendor}
                        </span>
                        <div className="font-medium text-[var(--text-primary)]">{line.supplierName}</div>
                        <div className="font-code text-[11px] text-[var(--text-secondary)]">
                          {line.supplierCode} · {line.receiptPostingDate}
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                        <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                          {pt.composer.colOrderedAcceptedOpen}
                        </span>
                        <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                          {formatQuantityValue(line.remainingUnbilledBaseQty, locale)} /{" "}
                          {formatQuantityValue(line.acceptedBaseQty, locale)} {line.baseUomCode} @{" "}
                          {formatUnitCostIdr(line.unitCostBase)}
                        </div>
                        <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                          {formatIdr(line.remainingOpenLineAmount)} ({pt.micro.poolOnHandShort}:{" "}
                          {formatQuantityValue(line.currentPoolOnHandQty, locale)} {line.baseUomCode})
                        </div>
                      </td>
                      <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                        {canManage ? (
                          <button
                            type="button"
                            onClick={() => seedComposerFromLine(line)}
                            className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                          >
                            <Plus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            <span>{pt.createBillAction}</span>
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ) : null}

      {/* Tab 3: AP Subledger & Price Adjustments */}
      {activeTab === "subledger" && overview ? (
        <div className="space-y-4">
          <div className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.subledgerCardTitle}
            </div>
            {overview.payables.length === 0 ? (
              <div className="p-6 text-center">
                <p className="text-sm font-semibold text-[var(--text-primary)]">{pt.emptyPayablesTitle}</p>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">{pt.emptyPayablesBody}</p>
              </div>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ap-subledger-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{pt.detail.headerRef}</th>
                      <th scope="col" className="py-2 md:px-2">{pt.detail.headerVendor}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{pt.detail.headerTotals}</th>
                      <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{pt.detail.headerPaymentBlock}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.payables.map((p) => (
                      <tr key={p.payableItemId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.detail.headerRef}
                          </span>
                          <div className="font-code font-bold text-[var(--text-primary)]">{p.payableNumber}</div>
                          <div className="font-code text-[11px] text-[var(--text-secondary)]">
                            Bill {p.billNumber} · GL {p.glJournalNumber}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.detail.headerVendor}
                          </span>
                          <div className="font-medium text-[var(--text-primary)]">{p.supplierLegalName}</div>
                          <div className="font-code text-[11px] text-[var(--text-secondary)]">
                            {pt.micro.invShort}: {p.supplierInvoiceNumber} · {pt.micro.dueShort}: {p.dueDate}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.detail.headerTotals}
                          </span>
                          <div className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                            {pt.micro.openShort}: {formatIdr(p.remainingOpenAmount)}
                          </div>
                          {Number(p.paidAmount ?? 0) > 0 ? (
                            <div className="font-code text-[11px] font-medium tabular-nums text-[var(--status-ok-text)]">
                              {pt.payableStatusLabels.PAID ?? "Paid"}: -{formatIdr(p.paidAmount ?? 0)}
                            </div>
                          ) : null}
                          {Number(p.creditedAmount ?? 0) > 0 ? (
                            <div className="font-code text-[11px] font-medium tabular-nums text-[var(--status-ok-text)]">
                              {t.returns.creditedShort}: -{formatIdr(p.creditedAmount ?? 0)}
                            </div>
                          ) : null}
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {pt.micro.netShort} {formatIdr(p.netPayableAmount)} ({pt.micro.whtShort}{" "}
                            <span className="whitespace-nowrap">-{formatIdr(p.withholdingTaxAmount)}</span>)
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right">
                          <span className="mb-1 block font-medium text-[var(--text-secondary)] md:hidden">
                            {pt.detail.headerPaymentBlock}
                          </span>
                          <div className="flex flex-wrap gap-1.5 md:justify-end">
                            <BillStatusBadge
                              status={p.status === "OPEN" ? "APPROVED" : p.status}
                              label={pt.payableStatusLabels[p.status] ?? p.status}
                            />
                            <PaymentBlockBadge
                              status={p.paymentBlockStatus}
                              label={pt.paymentBlockLabels[p.paymentBlockStatus] ?? p.paymentBlockStatus}
                            />
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.priceAdjustmentsTitle}
            </div>
            {overview.recentPriceAdjustments.length === 0 ? (
              <div className="p-6 text-center text-xs text-[var(--text-secondary)]">{pt.emptyPriceAdjustments}</div>
            ) : (
              <div className="divide-y divide-[var(--border-hairline)]">
                {overview.recentPriceAdjustments.map((adj) => (
                  <div key={adj.adjustmentId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs">
                    <div>
                      <span className="font-code font-bold text-[var(--text-primary)]">
                        {adj.billNumber} · {adj.itemCode}
                      </span>
                      <span className="ml-2 text-[var(--text-secondary)]">
                        {adj.postingDate} · GL {adj.glJournalNumber} · {pt.micro.coveredShort}{" "}
                        {formatQuantityValue(adj.coveredBaseQuantity, locale)} /{" "}
                        {formatQuantityValue(adj.billedBaseQuantity, locale)}
                      </span>
                    </div>
                    <div className="font-code tabular-nums text-[var(--text-primary)]">
                      {pt.micro.inv1300Short}: {formatIdr(adj.inventoryAdjustmentAmount)} · {pt.micro.ppv5200Short}: {formatIdr(adj.pnlVarianceAmount)} · {pt.micro.macShort}{" "}
                      {formatUnitCostIdr(adj.previousMovingAverageCost)} → {formatUnitCostIdr(adj.newMovingAverageCost)}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      ) : null}

      {/* Tab 4: Match & Tax Policy */}
      {activeTab === "policyTax" && overview ? (
        <div className="space-y-4">
          <div className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.policySectionTitle}
            </div>
            <div className="divide-y divide-[var(--border-hairline)] px-4 text-xs">
              <div className="flex flex-wrap justify-between gap-2 py-2.5">
                <span className="text-[var(--text-secondary)]">{overview.activeMatchPolicy.code}</span>
                <span className="font-semibold text-[var(--text-primary)]">{overview.activeMatchPolicy.name}</span>
              </div>
              <div className="grid grid-cols-1 gap-2 py-2.5 sm:grid-cols-3">
                <div className="font-code tabular-nums text-[var(--text-primary)]">
                  {pt.micro.ppToleranceShort}: {String(overview.activeMatchPolicy.ppTolerancePercent)}% /{" "}
                  {formatIdr(overview.activeMatchPolicy.ppToleranceAmount)}
                </div>
                <div className="font-code tabular-nums text-[var(--text-primary)]">
                  {pt.micro.dqToleranceShort}: {String(overview.activeMatchPolicy.dqTolerancePercent)}% /{" "}
                  {String(overview.activeMatchPolicy.dqToleranceBaseQty)}
                </div>
                <div className="font-code tabular-nums text-[var(--text-primary)]">
                  {pt.micro.bdLimitShort}: {formatIdr(overview.activeMatchPolicy.bdWriteOffLimitAmount)}
                </div>
              </div>
            </div>
          </div>

          <div className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {pt.taxTableTitle}
            </div>
            <div className="divide-y divide-[var(--border-hairline)]">
              <div className="bg-[var(--status-warn-bg)] px-4 py-2 text-xs text-[var(--status-warn-text)]">
                {pt.od07TaxDisclaimer}
              </div>
              {overview.taxCodes.map((tx) => (
                <div key={tx.taxCodeId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-xs">
                  <div>
                    <span className="font-code font-bold text-[var(--text-primary)]">{tx.code}</span>{" "}
                    <span className="text-[var(--text-secondary)]">— {tx.name}</span>
                  </div>
                  <div className="font-code tabular-nums text-[var(--text-primary)]">
                    {pt.taxFamilyLabels[tx.taxFamily] ?? tx.taxFamily} · {String(tx.nominalRatePercent)}% ({tx.taxableBaseRatioNumerator}/{tx.taxableBaseRatioDenominator}) · {pt.micro.effectiveShort} {tx.effectiveFrom}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
