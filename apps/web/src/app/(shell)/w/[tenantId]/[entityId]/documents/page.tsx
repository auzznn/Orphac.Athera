"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  Clock,
  Download,
  FileText,
  RefreshCw,
  RotateCcw,
  Search,
  ShieldAlert,
  Upload,
  X,
} from "lucide-react";
import {
  ApiProblemError,
  DocumentsOverviewResponse,
  fetchDocumentsOverview,
  getEntityAuditCsvUrl,
  getEvidencePackPdfUrl,
  issueDocumentDownloadToken,
  retryOutboxMessage,
  uploadBusinessDocument,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

type ActiveTab = "documents" | "exceptions" | "audit";

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

function humanizeCode(code: string): string {
  const spaced = code.replace(/([a-z0-9])([A-Z])/g, "$1_$2");
  return spaced
    .toLowerCase()
    .split("_")
    .filter(Boolean)
    .map((p) => p.charAt(0).toUpperCase() + p.slice(1))
    .join(" ");
}

function toUpperSnake(code: string): string {
  return code
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .replace(/[^A-Za-z0-9]+/g, "_")
    .toUpperCase();
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result ?? "");
      const idx = result.indexOf(",");
      resolve(idx >= 0 ? result.slice(idx + 1) : result);
    };
    reader.onerror = () => reject(new Error("Failed to read file."));
    reader.readAsDataURL(file);
  });
}

export default function DocumentsAndEvidencePage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = params?.tenantId ?? "";
  const entityId = params?.entityId ?? "";
  const { locale, t } = useLocale();
  const td = t.documents;

  const [overview, setOverview] = useState<DocumentsOverviewResponse | null>(null);
  const [activeTab, setActiveTab] = useState<ActiveTab>("documents");
  const [searchText, setSearchText] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Upload drawer state
  const [uploadOpen, setUploadOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [category, setCategory] = useState("TAX_EVIDENCE");
  const [sourceModule, setSourceModule] = useState("BILLING");
  const [sourceType, setSourceType] = useState("CUSTOMER_INVOICE");
  const [sourceNumber, setSourceNumber] = useState("");
  const [sourceId, setSourceId] = useState("");
  const [notes, setNotes] = useState("");
  const [uploading, setUploading] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const resolveError = useCallback(
    (err: unknown): string => {
      const apiErr = err as Partial<ApiProblemError> | null;
      if (apiErr?.errorCode && td.errors[apiErr.errorCode]) {
        return td.errors[apiErr.errorCode];
      }
      if (apiErr?.detail) return apiErr.detail;
      if (err instanceof Error) return err.message;
      return t.login.errors.unknown;
    },
    [t.login.errors.unknown, td.errors],
  );

  const reload = useCallback(
    async (q?: string, cat?: string) => {
      if (!tenantId || !entityId) return;
      setLoading(true);
      setErrorMessage(null);
      try {
        const data = await fetchDocumentsOverview(tenantId, entityId, {
          q: q?.trim() || undefined,
          category: cat || undefined,
        });
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
    fetchDocumentsOverview(tenantId, entityId, {
      q: searchText.trim() || undefined,
      category: categoryFilter || undefined,
    })
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
  }, [categoryFilter, entityId, resolveError, searchText, tenantId]);

  const handleUploadSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) return;
    setUploading(true);
    setErrorMessage(null);
    setSuccessMessage(null);
    try {
      const base64 = await fileToBase64(selectedFile);
      const effectiveSourceId = sourceId.trim() || sourceNumber.trim();
      await uploadBusinessDocument(tenantId, entityId, {
        fileName: selectedFile.name,
        contentType: selectedFile.type || "application/pdf",
        contentBase64: base64,
        category,
        sourceModule,
        sourceDocumentType: sourceType,
        sourceDocumentId: effectiveSourceId,
        sourceDocumentNumber: sourceNumber.trim(),
        notes: notes.trim() || null,
      });
      setSuccessMessage(td.successUploaded);
      setUploadOpen(false);
      setSelectedFile(null);
      setSourceNumber("");
      setSourceId("");
      setNotes("");
      await reload(searchText, categoryFilter);
    } catch (err) {
      setErrorMessage(resolveError(err));
    } finally {
      setUploading(false);
    }
  };

  const handleSecureDownload = async (documentId: string) => {
    setErrorMessage(null);
    try {
      const tokenRes = await issueDocumentDownloadToken(tenantId, entityId, documentId);
      window.open(tokenRes.downloadUrl, "_blank", "noopener,noreferrer");
    } catch (err) {
      setErrorMessage(resolveError(err));
    }
  };

  const handleRetryOutbox = async (messageId: string) => {
    setRetryingId(messageId);
    setErrorMessage(null);
    setSuccessMessage(null);
    try {
      await retryOutboxMessage(tenantId, entityId, messageId);
      setSuccessMessage(td.successRetried);
      await reload(searchText, categoryFilter);
    } catch (err) {
      setErrorMessage(resolveError(err));
    } finally {
      setRetryingId(null);
    }
  };

  const auditCsvHref = getEntityAuditCsvUrl(tenantId, entityId);
  const canMutate = Boolean(overview?.canUpload || overview?.canRetryOutbox);

  const renderOutboxBadge = (status: string) => {
    const label = td.outboxStatusLabels[status] ?? humanizeCode(status);
    if (status === "PROCESSED") {
      return (
        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)] whitespace-nowrap">
          <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          {label}
        </span>
      );
    }
    if (status === "FAILED") {
      return (
        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-danger-text)] whitespace-nowrap">
          <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          {label}
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-warn-text)] whitespace-nowrap">
        <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        {label}
      </span>
    );
  };

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
            {td.backToEntity}
          </Link>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)] sm:text-2xl">
            {td.heading}
          </h1>
          <p className="text-sm text-[var(--text-secondary)]">{td.lead}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 lg:shrink-0">
          {overview?.canUpload && (
            <button
              type="button"
              onClick={() => {
                setUploadOpen((v) => !v);
                setErrorMessage(null);
                setSuccessMessage(null);
              }}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 py-1.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <Upload className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              {td.uploadOpenButton}
            </button>
          )}

          <a
            href={auditCsvHref}
            download
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-1.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <Download className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            {td.exportAuditCsvButton}
          </a>

          <button
            type="button"
            onClick={() => void reload(searchText, categoryFilter)}
            disabled={loading}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-1.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-50"
          >
            <RefreshCw
              className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`}
              strokeWidth={1.75}
              aria-hidden="true"
            />
            {td.refreshButton}
          </button>
        </div>
      </div>

      {/* Read-only notice */}
      {overview && !canMutate && (
        <div
          role="status"
          className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
        >
          <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <p>{td.readOnlyNotice}</p>
        </div>
      )}

      {/* Collapsible OD-12 governance notice */}
      <details className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3.5 py-2.5 text-xs">
        <summary className="flex cursor-pointer list-none items-center justify-between gap-2 font-medium text-[var(--text-primary)]">
          <span>{td.governanceTitle}</span>
          <ChevronDown
            className="h-4 w-4 shrink-0 text-[var(--text-secondary)]"
            strokeWidth={1.75}
            aria-hidden="true"
          />
        </summary>
        <p className="mt-2 leading-relaxed text-[var(--text-secondary)]">{td.governanceBody}</p>
      </details>

      {/* Alerts */}
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

      {/* 4 Baseline-Aligned KPI Strip (Joined Single-Sheet Hairline) */}
      {overview && (
        <section
          aria-label={td.kpiAriaLabel}
          className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] sm:grid-cols-2 lg:grid-cols-4 lg:divide-x lg:divide-y-0"
        >
          <div className="flex flex-col justify-between p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{td.kpiTotalDocs}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)]">
              {overview.totalDocuments}
            </p>
            <p className="mt-2 text-xs text-[var(--text-secondary)]">SHA-256 · Append-only</p>
          </div>

          <div className="flex flex-col justify-between p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{td.kpiExceptions}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)]">
              {overview.totalExceptions}
            </p>
            <div className="mt-2 flex items-center gap-1.5 text-xs">
              {overview.totalExceptions === 0 ? (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)]">
                  <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {td.kpiNoExceptions}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-warn-text)]">
                  <AlertCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {td.kpiExceptionsActive}
                </span>
              )}
            </div>
          </div>

          <div className="flex flex-col justify-between p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{td.kpiAuditEvents}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)]">
              {overview.auditEvents.length}
            </p>
            <p className="mt-2 text-xs text-[var(--text-secondary)]">WIB (Asia/Jakarta)</p>
          </div>

          <div className="flex flex-col justify-between p-4">
            <p className="text-xs font-medium text-[var(--text-secondary)]">{td.kpiOutboxStatus}</p>
            <p className="mt-2 font-code text-lg font-semibold tabular-nums text-[var(--text-primary)]">
              {overview.failedOutboxCount} / {overview.pendingOutboxCount}
            </p>
            <div className="mt-2 flex items-center gap-1.5 text-xs">
              {overview.failedOutboxCount === 0 ? (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-ok-text)]">
                  <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {td.kpiOutboxHealthy}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--status-danger-text)]">
                  <AlertCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  {overview.failedOutboxCount} {td.kpiOutboxFailedSuffix}
                </span>
              )}
            </div>
          </div>
        </section>
      )}

      {/* Upload & Link Evidence Inline Drawer */}
      {uploadOpen && overview?.canUpload && (
        <form
          onSubmit={handleUploadSubmit}
          className="space-y-3 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4"
        >
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">{td.drawerTitle}</h2>
            <button
              type="button"
              onClick={() => setUploadOpen(false)}
              aria-label={td.drawerCancel}
              className="inline-flex h-9 w-9 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            >
              <X className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </button>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <div className="space-y-1">
              <label htmlFor="doc-file-input" className="block text-xs font-medium text-[var(--text-primary)]">
                {td.drawerFileLabel}
              </label>
              <input
                id="doc-file-input"
                type="file"
                required
                accept=".pdf,.png,.jpg,.jpeg,.csv,.txt"
                onChange={(e) => setSelectedFile(e.target.files?.[0] ?? null)}
                className="block min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"
              />
            </div>

            <div className="space-y-1">
              <label htmlFor="doc-category-select" className="block text-xs font-medium text-[var(--text-primary)]">
                {td.drawerCategoryLabel}
              </label>
              <select
                id="doc-category-select"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"
              >
                {Object.entries(td.categoryLabels).map(([k, label]) => (
                  <option key={k} value={k}>
                    {label}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-1">
              <label htmlFor="doc-source-module" className="block text-xs font-medium text-[var(--text-primary)]">
                {td.drawerSourceModuleLabel}
              </label>
              <select
                id="doc-source-module"
                value={sourceModule}
                onChange={(e) => setSourceModule(e.target.value)}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"
              >
                {(["PROCUREMENT", "AP", "CASH", "BILLING", "FULFILLMENT", "MANUAL_GL"] as const).map(
                  (code) => (
                    <option key={code} value={code}>
                      {t.reports.sourceModuleLabels[code] ?? humanizeCode(code)}
                    </option>
                  ),
                )}
              </select>
            </div>

            <div className="space-y-1">
              <label htmlFor="doc-source-type" className="block text-xs font-medium text-[var(--text-primary)]">
                {td.drawerSourceTypeLabel}
              </label>
              <select
                id="doc-source-type"
                value={sourceType}
                onChange={(e) => setSourceType(e.target.value)}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"
              >
                {(
                  [
                    "CUSTOMER_INVOICE",
                    "PURCHASE_ORDER",
                    "SUPPLIER_INVOICE",
                    "SUPPLIER_PAYMENT",
                    "SALES_SHIPMENT",
                    "MANUAL_JOURNAL",
                  ] as const
                ).map((code) => (
                  <option key={code} value={code}>
                    {t.reports.sourceDocumentTypeLabels[code] ?? humanizeCode(code)}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-1">
              <label htmlFor="doc-source-number" className="block text-xs font-medium text-[var(--text-primary)]">
                {td.drawerSourceNumberLabel}
              </label>
              <input
                id="doc-source-number"
                type="text"
                required
                value={sourceNumber}
                onChange={(e) => setSourceNumber(e.target.value)}
                placeholder={td.drawerSourceNumberPlaceholder}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"
              />
            </div>

            <div className="space-y-1">
              <label htmlFor="doc-source-id" className="block text-xs font-medium text-[var(--text-primary)]">
                {td.drawerSourceIdLabel}
              </label>
              <input
                id="doc-source-id"
                type="text"
                value={sourceId}
                onChange={(e) => setSourceId(e.target.value)}
                placeholder={td.drawerSourceIdPlaceholder}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"
              />
            </div>
          </div>

          <div className="space-y-1">
            <label htmlFor="doc-notes" className="block text-xs font-medium text-[var(--text-primary)]">
              {td.drawerNotesLabel}
            </label>
            <input
              id="doc-notes"
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder={td.drawerNotesPlaceholder}
              className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="submit"
              disabled={uploading || !selectedFile}
              className="min-h-9 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 py-1.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-50"
            >
              {uploading ? td.drawerBusy : td.drawerSubmit}
            </button>
            <button
              type="button"
              onClick={() => setUploadOpen(false)}
              className="min-h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-1.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {td.drawerCancel}
            </button>
          </div>
        </form>
      )}

      {/* Search & Filter Bar + Tabs */}
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div
          role="tablist"
          aria-label={td.heading}
          className="grid grid-cols-1 gap-1.5 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-1 sm:flex sm:flex-wrap"
        >
          {(
            [
              ["documents", td.tabDocuments],
              ["exceptions", td.tabExceptionsOutbox],
              ["audit", td.tabAudit],
            ] as [ActiveTab, string][]
          ).map(([key, label]) => {
            const active = activeTab === key;
            return (
              <button
                key={key}
                id={`docs-tab-${key}`}
                type="button"
                role="tab"
                aria-selected={active}
                aria-controls={`docs-panel-${key}`}
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

        <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <div className="relative w-full sm:w-64">
            <Search
              className="pointer-events-none absolute top-1/2 left-2.5 h-3.5 w-3.5 -translate-y-1/2 text-[var(--text-secondary)]"
              strokeWidth={1.75}
              aria-hidden="true"
            />
            <input
              type="search"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={td.searchPlaceholder}
              aria-label={td.searchPlaceholder}
              className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] py-1.5 pr-3 pl-8 text-xs text-[var(--text-primary)]"
            />
          </div>

          {activeTab === "documents" && (
            <select
              aria-label={td.drawerCategoryLabel}
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value)}
              className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-xs text-[var(--text-primary)] sm:w-auto"
            >
              <option value="">{td.filterCategoryAll}</option>
              {Object.entries(td.categoryLabels).map(([k, label]) => (
                <option key={k} value={k}>
                  {label}
                </option>
              ))}
            </select>
          )}
        </div>
      </div>

      {/* TAB 1: Documents & Linked Evidence */}
      {overview && activeTab === "documents" && (
        <section
          id="docs-panel-documents"
          role="tabpanel"
          aria-labelledby="docs-tab-documents"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">{td.docsTitle}</h2>
          </div>
          {overview.documents.length === 0 ? (
            <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">{td.docsEmpty}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th className="px-4 py-2.5 font-medium whitespace-nowrap">{td.colDocNumber}</th>
                    <th className="px-3 py-2.5 font-medium">{td.colFile}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{td.colCategory}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{td.colSourceRef}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{td.colIntegrity}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{td.colUploadedAt}</th>
                    <th className="px-4 py-2.5 text-right font-medium whitespace-nowrap">{td.colActions}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.documents.map((doc) => (
                    <tr
                      key={doc.documentId}
                      className="block p-3.5 hover:bg-[var(--bg-subtle)]/50 md:table-row md:p-0"
                    >
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colDocNumber}</span>
                        <span className="font-code font-medium tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                          {doc.documentNumber}
                        </span>
                      </td>
                      <td className="flex items-center justify-between gap-2 py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colFile}</span>
                        <div className="text-right md:text-left">
                          <div className="font-medium text-[var(--text-primary)]">{doc.fileName}</div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {formatBytes(Number(doc.byteSize))} · {doc.contentType}
                          </div>
                        </div>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colCategory}</span>
                        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-[11px] font-medium text-[var(--text-primary)] whitespace-nowrap">
                          <FileText className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          {td.categoryLabels[doc.category] ?? humanizeCode(doc.category)}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colSourceRef}</span>
                        <div className="text-right md:text-left">
                          <div className="font-code font-medium tabular-nums text-[var(--text-primary)] whitespace-nowrap">
                            {doc.sourceDocumentNumber}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            {t.reports.sourceDocumentTypeLabels[doc.sourceDocumentType] ??
                              humanizeCode(doc.sourceDocumentType)}
                          </div>
                        </div>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colIntegrity}</span>
                        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 font-code text-[11px] tabular-nums text-[var(--status-ok-text)] whitespace-nowrap">
                          <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          {doc.sha256Hash.slice(0, 12)}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colUploadedAt}</span>
                        <span className="font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                          {formatJakartaDateTime(doc.createdAtUtc, locale)}
                        </span>
                      </td>
                      <td className="block pt-2 md:table-cell md:px-4 md:py-2.5">
                        <div className="mt-1 flex flex-wrap items-center justify-start gap-1.5 md:mt-0 md:justify-end">
                          <button
                            type="button"
                            onClick={() => void handleSecureDownload(doc.documentId)}
                            className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1 text-xs font-medium text-[var(--accent-primary)] hover:bg-[var(--bg-chrome)] whitespace-nowrap"
                          >
                            <Download className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            {td.downloadButton}
                          </button>
                          <a
                            href={getEvidencePackPdfUrl(
                              tenantId,
                              entityId,
                              doc.sourceDocumentType,
                              doc.sourceDocumentId,
                            )}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] whitespace-nowrap"
                          >
                            <FileText className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                            {td.pdfSummaryButton}
                          </a>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* TAB 2: Exceptions Queue & Outbox Diagnostics */}
      {overview && activeTab === "exceptions" && (
        <div
          id="docs-panel-exceptions"
          role="tabpanel"
          aria-labelledby="docs-tab-exceptions"
          className="space-y-5"
        >
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">{td.exceptionsTitle}</h2>
            </div>
            {overview.exceptions.length === 0 ? (
              <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">{td.exceptionsEmpty}</p>
            ) : (
              <ul className="divide-y divide-[var(--border-hairline)]">
                {overview.exceptions.map((ex) => (
                  <li key={ex.exceptionId} className="flex flex-col gap-1.5 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="space-y-0.5">
                      <div className="flex flex-wrap items-center gap-2">
                        <span
                          className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-medium whitespace-nowrap ${
                            ex.severity === "HIGH"
                              ? "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
                              : "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                          }`}
                        >
                          <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          {td.severityLabels[ex.severity] ?? ex.severity}
                        </span>
                        <span className="text-xs font-semibold text-[var(--text-primary)]">
                          {ex.title}
                        </span>
                      </div>
                      <p className="text-xs text-[var(--text-secondary)]">{ex.detail}</p>
                    </div>
                    <span className="font-code text-xs tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                      {formatJakartaDateTime(ex.occurredAtUtc, locale)}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">{td.outboxTitle}</h2>
            </div>
            {overview.outboxMessages.length === 0 ? (
              <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">{td.outboxEmpty}</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th className="px-4 py-2.5 font-medium">{td.colTopic}</th>
                      <th className="px-3 py-2.5 font-medium whitespace-nowrap">{td.colStatus}</th>
                      <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{td.colAttempts}</th>
                      <th className="px-3 py-2.5 font-medium">{td.colLastError}</th>
                      <th className="px-3 py-2.5 text-right font-medium whitespace-nowrap">{td.colCreatedAt}</th>
                      {overview.canRetryOutbox && (
                        <th className="px-4 py-2.5 text-right font-medium whitespace-nowrap">{td.colActions}</th>
                      )}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {overview.outboxMessages.map((msg) => (
                      <tr key={msg.messageId} className="block p-3.5 md:table-row md:p-0">
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-4 md:py-2.5">
                          <span className="text-[var(--text-secondary)] md:hidden">{td.colTopic}</span>
                          <span className="font-code font-medium text-[var(--text-primary)]">{msg.topic}</span>
                        </td>
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                          <span className="text-[var(--text-secondary)] md:hidden">{td.colStatus}</span>
                          {renderOutboxBadge(msg.status)}
                        </td>
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                          <span className="text-[var(--text-secondary)] md:hidden">{td.colAttempts}</span>
                          <span className="font-code tabular-nums text-[var(--text-secondary)]">{msg.attemptCount}</span>
                        </td>
                        <td className="flex items-center justify-between gap-2 py-1 md:table-cell md:px-3 md:py-2.5">
                          <span className="text-[var(--text-secondary)] md:hidden">{td.colLastError}</span>
                          <span className="text-right text-[var(--text-secondary)] md:text-left">
                            {msg.lastError || "—"}
                          </span>
                        </td>
                        <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5 md:text-right">
                          <span className="text-[var(--text-secondary)] md:hidden">{td.colCreatedAt}</span>
                          <span className="font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                            {formatJakartaDateTime(msg.createdAtUtc, locale)}
                          </span>
                        </td>
                        {overview.canRetryOutbox && (
                          <td className="block pt-2 md:table-cell md:px-4 md:py-2.5 md:text-right">
                            {msg.status === "FAILED" ? (
                              <button
                                type="button"
                                disabled={retryingId === msg.messageId}
                                onClick={() => void handleRetryOutbox(msg.messageId)}
                                className="inline-flex min-h-9 items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 py-1 text-xs font-medium text-[var(--accent-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-50"
                              >
                                <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                {td.retryButton}
                              </button>
                            ) : (
                              <span className="text-[var(--text-secondary)]">—</span>
                            )}
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}

      {/* TAB 3: Immutable Entity Audit Trail */}
      {overview && activeTab === "audit" && (
        <section
          id="docs-panel-audit"
          role="tabpanel"
          aria-labelledby="docs-tab-audit"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
        >
          <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">{td.auditTitle}</h2>
          </div>
          {overview.auditEvents.length === 0 ? (
            <p className="px-4 py-5 text-xs text-[var(--text-secondary)]">{td.auditEmpty}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th className="px-4 py-2.5 font-medium whitespace-nowrap">{td.colTime}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{td.colEventType}</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">{td.colSubject}</th>
                    <th className="px-4 py-2.5 font-medium">{td.colSummary}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-hairline)]">
                  {overview.auditEvents.map((ev) => (
                    <tr key={ev.eventId} className="block p-3.5 md:table-row md:p-0">
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colTime}</span>
                        <span className="font-code tabular-nums text-[var(--text-secondary)] whitespace-nowrap">
                          {formatJakartaDateTime(ev.occurredAtUtc, locale)}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colEventType}</span>
                        <span className="font-medium text-[var(--text-primary)] whitespace-nowrap">
                          {humanizeCode(ev.eventType)}
                        </span>
                      </td>
                      <td className="flex items-center justify-between py-1 md:table-cell md:px-3 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colSubject}</span>
                        <span className="text-[var(--text-secondary)] whitespace-nowrap">
                          {t.reports.sourceDocumentTypeLabels[toUpperSnake(ev.subjectType)] ??
                            humanizeCode(ev.subjectType)}
                        </span>
                      </td>
                      <td className="flex items-center justify-between gap-2 py-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="text-[var(--text-secondary)] md:hidden">{td.colSummary}</span>
                        <span className="text-right text-[var(--text-primary)] md:text-left">
                          {ev.summary}
                        </span>
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
