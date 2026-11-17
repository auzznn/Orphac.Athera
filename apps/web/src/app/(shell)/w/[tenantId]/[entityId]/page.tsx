"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  Building2,
  CheckCircle2,
  Clock,
  Lock,
  RefreshCw,
  ShieldCheck,
} from "lucide-react";
import {
  ApiProblemError,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

function EntityWorkspaceSheet({
  tenantId,
  entityId,
}: {
  tenantId: string;
  entityId: string;
}) {
  const router = useRouter();
  const routerRef = useRef(router);
  const { locale, t } = useLocale();

  const [detail, setDetail] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loading, setLoading] = useState(true);
  const [retryNonce, setRetryNonce] = useState(0);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  useEffect(() => {
    let active = true;

    fetchEntityWorkspaceDetail(tenantId, entityId)
      .then((data) => {
        if (!active) return;
        setDetail(data);
        setProblem(null);
      })
      .catch((err) => {
        if (!active) return;
        const apiProblem = err as ApiProblemError;
        if (apiProblem.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
        setProblem(apiProblem);
        setDetail(null);
      })
      .finally(() => {
        if (active) {
          setLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, retryNonce]);

  function handleRetry() {
    setLoading(true);
    setProblem(null);
    setRetryNonce((prev) => prev + 1);
  }

  if (loading) {
    return (
      <div className="mx-auto max-w-5xl rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
        <div className="h-5 w-52 animate-pulse rounded bg-[var(--bg-chrome)]" />
        <p className="mt-3 text-sm text-[var(--text-secondary)]">{t.entitySheet.loadingEntity}</p>
      </div>
    );
  }

  if (problem || !detail) {
    const status = problem?.status ?? 0;
    const isForbidden = status === 403;
    const isNotFound = status === 404;

    return (
      <div className="mx-auto max-w-2xl space-y-4">
        <section
          role="alert"
          className={`rounded-[var(--radius-md)] border bg-[var(--bg-surface)] p-6 ${
            isForbidden
              ? "border-[var(--status-warn-border)]"
              : "border-[var(--status-danger-border)]"
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
              <p className="text-sm leading-relaxed text-[var(--text-secondary)]">
                {isForbidden
                  ? t.entitySheet.forbiddenBody
                  : isNotFound
                    ? t.entitySheet.unavailableBody
                    : problem?.detail ?? t.login.errors.upstream_unavailable}
              </p>
              {problem?.errorCode ? (
                <p className="font-code text-xs text-[var(--text-muted)]">{problem.errorCode}</p>
              ) : null}
            </div>
          </div>

          <div className="mt-5 flex flex-wrap items-center gap-3 border-t border-[var(--border-hairline)] pt-4">
            {!isForbidden && !isNotFound ? (
              <button
                type="button"
                onClick={handleRetry}
                className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
              >
                <RefreshCw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{t.shell.errorRetry}</span>
              </button>
            ) : null}
            <Link
              href="/workspaces"
              className={`inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] px-3.5 text-sm font-semibold ${
                !isForbidden && !isNotFound
                  ? "border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                  : "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
              }`}
            >
              <ArrowLeft className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
              <span>{t.entitySheet.returnWorkspaces}</span>
            </Link>
          </div>
        </section>
      </div>
    );
  }

  const contextRows = [
    {
      label: t.entitySheet.fieldTenant,
      value: `${detail.tenant.code} — ${detail.tenant.name}`,
      mono: false,
    },
    { label: t.entitySheet.fieldEntityCode, value: detail.entity.code, mono: true },
    { label: t.entitySheet.fieldEntityName, value: detail.entity.name, mono: false },
    {
      label: t.entitySheet.fieldNpwp,
      value: detail.entity.taxRegistrationNpwp ?? t.workspaces.noNpwp,
      mono: true,
    },
    {
      label: t.entitySheet.fieldCurrency,
      value: detail.entity.functionalCurrencyCode,
      mono: true,
    },
    {
      label: t.entitySheet.fieldCountryTz,
      value: `${detail.entity.countryCode} · ${detail.entity.timeZoneId}`,
      mono: true,
    },
    {
      label: t.entitySheet.fieldEvaluatedAt,
      value: formatJakartaDateTime(detail.serverTimestampUtc, locale),
      mono: true,
    },
  ];

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">
            {detail.entity.name}
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
              {detail.tenant.code} · {detail.entity.code}
            </span>
            {" · "}
            <span className="font-code tabular-nums">{detail.entity.functionalCurrencyCode}</span>
          </p>
        </div>

        <Link
          href="/workspaces"
          className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.entitySheet.backToDirectory}</span>
        </Link>
      </div>

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <Building2 className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">
            {t.entitySheet.contextCardTitle}
          </h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left text-sm">
            <tbody>
              {contextRows.map((row) => (
                <tr
                  key={row.label}
                  className="block border-b border-[var(--border-hairline)] py-2 last:border-b-0 sm:table-row sm:py-0"
                >
                  <th
                    scope="row"
                    className="block px-4 pb-0.5 pt-1 text-xs font-medium text-[var(--text-secondary)] sm:table-cell sm:w-60 sm:py-3 sm:text-sm"
                  >
                    {row.label}
                  </th>
                  <td
                    className={`block px-4 pb-1 pt-0.5 text-[var(--text-primary)] sm:table-cell sm:py-3 ${
                      row.mono ? "font-code tabular-nums text-[13px]" : "font-medium"
                    }`}
                  >
                    {row.value}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <ShieldCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">
            {t.entitySheet.permissionsCardTitle}
          </h2>
        </div>

        <div className="flex flex-wrap gap-1.5 border-b border-[var(--border-hairline)] px-4 py-3">
          {detail.effectivePermissions.map((perm) => (
            <span
              key={perm}
              className="font-code rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs text-[var(--text-secondary)]"
            >
              {perm}
            </span>
          ))}
        </div>

        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left text-sm">
            <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] sm:table-header-group">
              <tr>
                <th scope="col" className="px-4 py-2.5">{t.entitySheet.colModule}</th>
                <th scope="col" className="px-4 py-2.5">{t.entitySheet.colPermission}</th>
                <th scope="col" className="px-4 py-2.5">{t.entitySheet.colReadiness}</th>
              </tr>
            </thead>
            <tbody>
              {detail.navigationCapabilities.map((item) => {
                const isAvailable = item.status === "AVAILABLE";
                const sliceStage = item.status
                  .replace(/^PLANNED_/, "")
                  .replace(/_/g, "–");
                const statusText = isAvailable
                  ? t.entitySheet.availableState
                  : item.permitted
                    ? `${t.entitySheet.permittedPlannedPrefix} ${sliceStage}`
                    : t.entitySheet.notGrantedState;

                const badgeClasses = isAvailable
                  ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                  : item.permitted
                    ? "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                    : "border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-muted)]";

                return (
                  <tr
                    key={item.key}
                    className="block border-b border-[var(--border-hairline)] py-2.5 last:border-b-0 sm:table-row sm:py-0"
                  >
                    <td className="block px-4 py-0.5 font-medium text-[var(--text-primary)] sm:table-cell sm:py-2.5">
                      {t.entitySheet.moduleNames[item.key] ?? item.key}
                    </td>
                    <td className="font-code block px-4 py-0.5 text-xs text-[var(--text-secondary)] sm:table-cell sm:py-2.5">
                      {item.requiredPermission ?? "—"}
                    </td>
                    <td className="block px-4 pb-1 pt-1.5 sm:table-cell sm:py-2.5">
                      <span
                        className={`inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-semibold ${badgeClasses}`}
                      >
                        {isAvailable ? (
                          <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        ) : item.permitted ? (
                          <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        ) : (
                          <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        )}
                        <span>{statusText}</span>
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

export default function EntityWorkspacePage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <EntityWorkspaceSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
