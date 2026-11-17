"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  Clock,
  Lock,
  RefreshCw,
  Send,
  ShieldCheck,
} from "lucide-react";
import { useRefreshShell, useSessionUser } from "@/components/AppShell";
import {
  ApiProblemError,
  ApprovalCasesResponse,
  createApprovalCase,
  decideApprovalCase,
  EntityWorkspaceDetailResponse,
  fetchApprovalCases,
  fetchEntityWorkspaceDetail,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

const REQUESTABLE_PERMISSIONS = [
  "procurement.read",
  "inventory.read",
  "sales.read",
  "finance.read",
  "admin.read",
];

function ApprovalsSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const currentUser = useSessionUser();
  const refreshShell = useRefreshShell();
  const { locale, t } = useLocale();

  const [detail, setDetail] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [data, setData] = useState<ApprovalCasesResponse | null>(null);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loading, setLoading] = useState(true);
  const [reloadNonce, setReloadNonce] = useState(0);

  const [policyCode, setPolicyCode] = useState("SENSITIVE_ACCESS_CHANGE");
  const [permissionCode, setPermissionCode] = useState("inventory.read");
  const [reason, setReason] = useState("");
  const [reasonFieldError, setReasonFieldError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const [decisionReasons, setDecisionReasons] = useState<Record<string, string>>({});
  const [decisionFieldErrors, setDecisionFieldErrors] = useState<Record<string, string>>({});
  const [decidingAction, setDecidingAction] = useState<{
    caseId: string;
    decision: "APPROVED" | "REJECTED";
  } | null>(null);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchApprovalCases(tenantId, entityId),
    ])
      .then(([entityDetail, casesPayload]) => {
        if (!active) return;
        setDetail(entityDetail);
        setData(casesPayload);
        setProblem(null);
      })
      .catch((err) => {
        if (!active) return;
        const apiErr = err as ApiProblemError;
        if (apiErr.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
        setProblem(apiErr);
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, reloadNonce]);

  async function handleSubmitRequest(event: React.FormEvent) {
    event.preventDefault();
    setActionError(null);
    setReasonFieldError(null);

    if (!reason.trim()) {
      setReasonFieldError(t.approvals.reasonRequiredError);
      return;
    }

    setSubmitting(true);
    try {
      await createApprovalCase(
        tenantId,
        entityId,
        {
          policyCode,
          targetPermissionCode: permissionCode,
          reason: reason.trim(),
        },
        crypto.randomUUID(),
      );
      setReason("");
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setActionError(t.approvals.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDecision(caseId: string, decision: "APPROVED" | "REJECTED") {
    setActionError(null);
    const note = (decisionReasons[caseId] ?? "").trim();
    if (!note) {
      setDecisionFieldErrors((prev) => ({ ...prev, [caseId]: t.approvals.decisionReasonRequired }));
      return;
    }

    setDecisionFieldErrors((prev) => {
      const next = { ...prev };
      delete next[caseId];
      return next;
    });
    setDecidingAction({ caseId, decision });
    try {
      await decideApprovalCase(tenantId, entityId, caseId, {
        decision,
        reason: note,
      });
      await refreshShell();
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setActionError(t.approvals.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setDecidingAction(null);
    }
  }

  if (loading && !detail) {
    return (
      <div className="mx-auto max-w-5xl rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
        <div className="h-5 w-56 animate-pulse rounded bg-[var(--bg-chrome)]" />
        <p className="mt-3 text-sm text-[var(--text-secondary)]">{t.entitySheet.loadingEntity}</p>
      </div>
    );
  }

  if (problem || !detail || !data) {
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
                onClick={() => {
                  setLoading(true);
                  setReloadNonce((n) => n + 1);
                }}
                className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-sm font-semibold text-[var(--accent-primary-ink)]"
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

  const canRequest = detail.effectivePermissions.includes("approval.request");
  const canDecide = detail.effectivePermissions.includes("approval.decide");

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">
            {t.approvals.heading}
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
              {detail.tenant.code} · {detail.entity.code}
            </span>
            {" — "}
            <span>{t.approvals.lead}</span>
          </p>
        </div>

        <Link
          href={`/w/${tenantId}/${entityId}`}
          className="inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
          <span>{detail.entity.code}</span>
        </Link>
      </div>

      {actionError ? (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-sm text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{actionError}</span>
        </div>
      ) : null}

      {canRequest ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <Send className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.approvals.requestCardTitle}</h2>
          </div>

          <form onSubmit={handleSubmitRequest} noValidate className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-12">
            <div className="sm:col-span-4">
              <label htmlFor="approval-policy" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.approvals.policyLabel}
              </label>
              <select
                id="approval-policy"
                value={policyCode}
                onChange={(e) => setPolicyCode(e.target.value)}
                className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                {data.policies.map((p) => (
                  <option key={p.policyId} value={p.policyCode}>
                    {p.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="sm:col-span-3">
              <label htmlFor="approval-permission" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.approvals.permissionLabel}
              </label>
              <select
                id="approval-permission"
                value={permissionCode}
                onChange={(e) => setPermissionCode(e.target.value)}
                className="font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                {REQUESTABLE_PERMISSIONS.map((perm) => (
                  <option key={perm} value={perm}>
                    {perm}
                  </option>
                ))}
              </select>
            </div>

            <div className="sm:col-span-5">
              <label htmlFor="approval-reason" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.approvals.reasonLabel}
              </label>
              <input
                id="approval-reason"
                type="text"
                aria-invalid={Boolean(reasonFieldError)}
                aria-describedby={reasonFieldError ? "approval-reason-error" : undefined}
                value={reason}
                onChange={(e) => {
                  setReason(e.target.value);
                  setReasonFieldError(null);
                }}
                placeholder={t.approvals.reasonPlaceholder}
                className={`mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                  reasonFieldError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                }`}
              />
              {reasonFieldError ? (
                <p id="approval-reason-error" role="alert" className="mt-1 text-xs text-[var(--status-danger-text)]">
                  {reasonFieldError}
                </p>
              ) : null}
            </div>

            <div className="sm:col-span-12">
              <button
                type="submit"
                disabled={submitting}
                className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <span>{submitting ? t.approvals.submitBusy : t.approvals.submitButton}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <ShieldCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.approvals.casesCardTitle}</h2>
        </div>

        {data.cases.length === 0 ? (
          <p className="p-6 text-sm text-[var(--text-secondary)]">{t.approvals.emptyCases}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="px-4 py-2.5">{t.approvals.colCase}</th>
                  <th scope="col" className="px-4 py-2.5">{t.approvals.colRequester}</th>
                  <th scope="col" className="px-4 py-2.5">{t.approvals.colStatus}</th>
                  <th scope="col" className="px-4 py-2.5">{t.approvals.colDecisionAction}</th>
                </tr>
              </thead>
              <tbody>
                {data.cases.map((item) => {
                  const isPending = item.status === "PENDING";
                  const isApproved = item.status === "APPROVED";
                  const isSelf = item.requestedByUserId.toLowerCase() === currentUser.userId.toLowerCase();
                  const rowFieldErr = decisionFieldErrors[item.caseId];
                  const isDecidingRow = decidingAction?.caseId === item.caseId;
                  const needsMfaSetup = isPending && canDecide && !isSelf && item.requiresApproverMfa && !currentUser.mfaEnabled;

                  const statusClass = isApproved
                    ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                    : isPending
                      ? "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                      : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]";

                  return (
                    <tr
                      key={item.caseId}
                      className="block border-b border-[var(--border-hairline)] p-4 last:border-b-0 md:table-row md:p-0"
                    >
                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-code text-xs font-semibold text-[var(--text-primary)]">
                            {item.targetPermissionCode}
                          </span>
                          {item.requiresApproverMfa ? (
                            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-1.5 py-0.5 text-[11px] font-semibold text-[var(--status-warn-text)]">
                              <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              <span>{t.approvals.mfaRequiredTag}</span>
                            </span>
                          ) : null}
                        </div>
                        <p className="mt-1 text-sm text-[var(--text-primary)]">{item.requestReason}</p>
                        <p className="font-code mt-0.5 text-xs text-[var(--text-muted)]">{item.policyCode}</p>
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mb-0.5 block text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.approvals.colRequester}
                        </span>
                        <p className="text-xs font-medium text-[var(--text-primary)]">{item.requestedByName}</p>
                        <p className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                          {formatJakartaDateTime(item.requestedAtUtc, locale)}
                        </p>
                        {item.decidedByName ? (
                          <div className="mt-1.5 border-t border-[var(--border-hairline)] pt-1.5 text-xs text-[var(--text-secondary)]">
                            <p>
                              {t.approvals.decidedPrefix}:{" "}
                              <span className="font-medium text-[var(--text-primary)]">{item.decidedByName}</span>
                            </p>
                            {item.decidedAtUtc ? (
                              <p className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                                {formatJakartaDateTime(item.decidedAtUtc, locale)}
                              </p>
                            ) : null}
                            <p className="text-[var(--text-primary)]">{item.decisionReason}</p>
                          </div>
                        ) : null}
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mb-1 block text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.approvals.colStatus}
                        </span>
                        <span className={`inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-semibold ${statusClass}`}>
                          {isApproved ? (
                            <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          ) : isPending ? (
                            <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          ) : (
                            <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          )}
                          <span>
                            {isApproved
                              ? t.approvals.statusApproved
                              : isPending
                                ? t.approvals.statusPending
                                : t.approvals.statusRejected}
                          </span>
                        </span>
                      </td>

                      <td
                        className={`${
                          isPending && (isSelf || canDecide) ? "block pt-1" : "hidden md:table-cell"
                        } md:px-4 md:py-3 md:align-top`}
                      >
                        {isPending && isSelf ? (
                          <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2.5 py-2 text-xs text-[var(--status-warn-text)]">
                            <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            <span>{t.approvals.selfApprovalNotice}</span>
                          </div>
                        ) : isPending && canDecide ? (
                          <div className="space-y-2">
                            {needsMfaSetup ? (
                              <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2.5 py-2 text-xs text-[var(--status-warn-text)]">
                                <Clock className="mt-0.5 h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                                <div>
                                  <p>{t.approvals.mfaEnrollPrompt}</p>
                                  <Link href="/" className="mt-1 inline-block font-semibold underline">
                                    {t.approvals.mfaEnrollLink}
                                  </Link>
                                </div>
                              </div>
                            ) : null}
                            <div>
                              <input
                                type="text"
                                aria-label={`${t.approvals.colDecisionAction} (${item.targetPermissionCode} — ${item.requestedByName})`}
                                aria-invalid={Boolean(rowFieldErr)}
                                aria-describedby={rowFieldErr ? `decision-err-${item.caseId}` : undefined}
                                value={decisionReasons[item.caseId] ?? ""}
                                onChange={(e) => {
                                  const val = e.target.value;
                                  setDecisionReasons((prev) => ({ ...prev, [item.caseId]: val }));
                                  setDecisionFieldErrors((prev) => {
                                    const next = { ...prev };
                                    delete next[item.caseId];
                                    return next;
                                  });
                                }}
                                placeholder={t.approvals.decisionReasonPlaceholder}
                                className={`h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] ${
                                  rowFieldErr ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                                }`}
                              />
                              {rowFieldErr ? (
                                <p
                                  id={`decision-err-${item.caseId}`}
                                  role="alert"
                                  className="mt-1 text-xs text-[var(--status-danger-text)]"
                                >
                                  {rowFieldErr}
                                </p>
                              ) : null}
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <button
                                type="button"
                                disabled={isDecidingRow || needsMfaSetup}
                                aria-label={`${t.approvals.approveButton} (${item.targetPermissionCode} — ${item.requestedByName})`}
                                onClick={() => handleDecision(item.caseId, "APPROVED")}
                                className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                              >
                                {isDecidingRow && decidingAction.decision === "APPROVED"
                                  ? t.approvals.decidingBusy
                                  : t.approvals.approveButton}
                              </button>
                              <button
                                type="button"
                                disabled={isDecidingRow || needsMfaSetup}
                                aria-label={`${t.approvals.rejectButton} (${item.targetPermissionCode} — ${item.requestedByName})`}
                                onClick={() => handleDecision(item.caseId, "REJECTED")}
                                className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                              >
                                {isDecidingRow && decidingAction.decision === "REJECTED"
                                  ? t.approvals.decidingBusy
                                  : t.approvals.rejectButton}
                              </button>
                            </div>
                          </div>
                        ) : (
                          <span className="text-xs text-[var(--text-muted)]">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

export default function ApprovalsPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <ApprovalsSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
