"use client";

import Link from "next/link";
import { AlertCircle, ArrowLeft, Lock, RefreshCw } from "lucide-react";
import { ApiProblemError } from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";

export function ScopedLoadingSheet() {
  const { t } = useLocale();
  return (
    <div className="mx-auto max-w-5xl rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
      <div className="h-5 w-56 animate-pulse rounded bg-[var(--bg-chrome)]" />
      <p className="mt-3 text-sm text-[var(--text-secondary)]">{t.entitySheet.loadingEntity}</p>
    </div>
  );
}

export function ScopedBoundaryError({
  problem,
  onRetry,
}: {
  problem: ApiProblemError | null;
  onRetry: () => void;
}) {
  const { t } = useLocale();
  const status = problem?.status ?? 0;
  const isForbidden = status === 403;
  const isNotFound = status === 404;

  return (
    <div className="mx-auto max-w-2xl">
      <section
        role="alert"
        className={`rounded-[var(--radius-md)] border bg-[var(--bg-surface)] p-6 ${
          isForbidden ? "border-[var(--status-warn-border)]" : "border-[var(--status-danger-border)]"
        }`}
      >
        <div className="flex items-start gap-3">
          <span
            className={`inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[var(--radius-sm)] border ${
              isForbidden
                ? "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
            }`}
          >
            {isForbidden ? (
              <Lock className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            ) : (
              <AlertCircle className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            )}
          </span>
          <div className="space-y-2">
            <h1 className="text-lg font-semibold text-[var(--text-primary)]">
              {isForbidden
                ? t.entitySheet.forbiddenTitle
                : isNotFound
                  ? t.entitySheet.unavailableTitle
                  : t.shell.errorHeading}
            </h1>
            <p className="text-sm text-[var(--text-secondary)]">
              {isForbidden
                ? t.entitySheet.forbiddenBody
                : isNotFound
                  ? t.entitySheet.unavailableBody
                  : problem?.detail ?? t.login.errors.upstream_unavailable}
            </p>
          </div>
        </div>
        <div className="mt-5 flex flex-wrap gap-3 border-t border-[var(--border-hairline)] pt-4">
          {!isForbidden && !isNotFound ? (
            <button
              type="button"
              onClick={onRetry}
              className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <RefreshCw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
              <span>{t.shell.errorRetry}</span>
            </button>
          ) : null}
          <Link
            href="/workspaces"
            className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-sm font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <ArrowLeft className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            <span>{t.entitySheet.returnWorkspaces}</span>
          </Link>
        </div>
      </section>
    </div>
  );
}
