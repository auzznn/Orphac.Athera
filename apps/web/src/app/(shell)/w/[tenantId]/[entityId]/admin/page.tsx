"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  ArrowUpRight,
  CheckCircle2,
  Clock,
  Copy,
  KeyRound,
  Lock,
  RefreshCw,
  ShieldCheck,
  Users,
} from "lucide-react";
import { useRefreshShell, useSessionUser } from "@/components/AppShell";
import {
  AdminOverviewResponse,
  ApiProblemError,
  createEntityInvitation,
  CreateInvitationResponse,
  dispatchEntityOutbox,
  EntityWorkspaceDetailResponse,
  fetchEntityAdminOverview,
  fetchEntityWorkspaceDetail,
  grantEntityPermission,
  revokeEntityInvitation,
  updateMemberStatus,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

function EntityAdminSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const currentUser = useSessionUser();
  const refreshShell = useRefreshShell();
  const { locale, t } = useLocale();

  const [detail, setDetail] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [overview, setOverview] = useState<AdminOverviewResponse | null>(null);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loading, setLoading] = useState(true);
  const [reloadNonce, setReloadNonce] = useState(0);

  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteEmailError, setInviteEmailError] = useState<string | null>(null);
  const [inviteRole, setInviteRole] = useState("PROCUREMENT_OFFICER");
  const [inviteExpiryHours, setInviteExpiryHours] = useState(48);
  const [inviteBusy, setInviteBusy] = useState(false);
  const [issuedInvite, setIssuedInvite] = useState<CreateInvitationResponse | null>(null);
  const [copiedToken, setCopiedToken] = useState(false);

  const [selectedGrantByUser, setSelectedGrantByUser] = useState<Record<string, string>>({});
  const [busyGrantUserId, setBusyGrantUserId] = useState<string | null>(null);
  const [busyMemberId, setBusyMemberId] = useState<string | null>(null);
  const [busyInviteId, setBusyInviteId] = useState<string | null>(null);
  const [dispatchBusy, setDispatchBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchEntityAdminOverview(tenantId, entityId),
    ])
      .then(([entityDetail, adminOverview]) => {
        if (!active) return;
        setDetail(entityDetail);
        setOverview(adminOverview);
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

  async function handleCreateInvite(e: React.FormEvent) {
    e.preventDefault();
    const trimmedEmail = inviteEmail.trim();
    if (!trimmedEmail || !trimmedEmail.includes("@")) {
      setInviteEmailError(t.admin.inviteEmailRequiredError);
      return;
    }

    setInviteBusy(true);
    setInviteEmailError(null);
    setActionError(null);
    try {
      const created = await createEntityInvitation(tenantId, entityId, {
        email: trimmedEmail,
        roleTemplateCode: inviteRole,
        expiresInHours: inviteExpiryHours,
      });
      setIssuedInvite(created);
      setCopiedToken(false);
      setInviteEmail("");
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setActionError(t.admin.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setInviteBusy(false);
    }
  }

  async function handleCopyInviteToken() {
    if (!issuedInvite || typeof navigator === "undefined" || !navigator.clipboard) return;
    await navigator.clipboard.writeText(issuedInvite.inviteToken);
    setCopiedToken(true);
  }

  async function handleRevokeInvite(invitationId: string) {
    setBusyInviteId(invitationId);
    setActionError(null);
    try {
      await revokeEntityInvitation(tenantId, entityId, invitationId);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      setActionError((err as ApiProblemError).detail ?? t.login.errors.unknown);
    } finally {
      setBusyInviteId(null);
    }
  }

  async function handleGrantPermission(targetUserId: string, permissionCode: string) {
    if (!permissionCode) return;
    setBusyGrantUserId(targetUserId);
    setActionError(null);
    try {
      await grantEntityPermission(tenantId, entityId, targetUserId, permissionCode);
      await refreshShell();
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setActionError(t.admin.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setBusyGrantUserId(null);
    }
  }

  async function handleMemberAction(targetUserId: string, action: "suspend" | "revoke") {
    setBusyMemberId(targetUserId);
    setActionError(null);
    try {
      await updateMemberStatus(tenantId, entityId, targetUserId, action);
      await refreshShell();
      setReloadNonce((n) => n + 1);
    } catch (err) {
      setActionError((err as ApiProblemError).detail ?? t.login.errors.unknown);
    } finally {
      setBusyMemberId(null);
    }
  }

  async function handleDispatchOutbox() {
    setDispatchBusy(true);
    setActionError(null);
    try {
      await dispatchEntityOutbox(tenantId, entityId);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      setActionError((err as ApiProblemError).detail ?? t.login.errors.unknown);
    } finally {
      setDispatchBusy(false);
    }
  }

  if (loading && !overview) {
    return (
      <div className="mx-auto max-w-5xl rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
        <div className="h-5 w-56 animate-pulse rounded bg-[var(--bg-chrome)]" />
        <p className="mt-3 text-sm text-[var(--text-secondary)]">{t.entitySheet.loadingEntity}</p>
      </div>
    );
  }

  if (problem || !detail || !overview) {
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

  const canManage = detail.effectivePermissions.includes("admin.manage");

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">
            {t.admin.heading}
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
              {detail.tenant.code} · {detail.entity.code}
            </span>
            {" — "}
            <span>{t.admin.lead}</span>
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

      <section className="flex flex-wrap items-center justify-between gap-4 rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-4 py-3">
        <div className="flex flex-wrap items-center gap-4 text-xs">
          <span className="font-semibold text-[var(--text-primary)]">{t.admin.outboxCardTitle}:</span>
          <span className="inline-flex items-center gap-1.5 text-[var(--status-warn-text)]">
            <Clock className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span className="font-medium">{t.admin.outboxPending}:</span>
            <span className="font-code font-semibold tabular-nums">{overview.outboxSummary.pendingCount}</span>
          </span>
          <span className="inline-flex items-center gap-1.5 text-[var(--status-ok-text)]">
            <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span className="font-medium">{t.admin.outboxProcessed}:</span>
            <span className="font-code font-semibold tabular-nums">{overview.outboxSummary.processedCount}</span>
          </span>
          <span className="inline-flex items-center gap-1.5 text-[var(--status-danger-text)]">
            <AlertCircle className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span className="font-medium">{t.admin.outboxFailed}:</span>
            <span className="font-code font-semibold tabular-nums">{overview.outboxSummary.failedCount}</span>
          </span>
        </div>
        {canManage ? (
          <button
            type="button"
            disabled={dispatchBusy}
            onClick={handleDispatchOutbox}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
          >
            <RefreshCw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            <span>{dispatchBusy ? t.admin.dispatchBusy : t.admin.dispatchOutboxButton}</span>
          </button>
        ) : null}
      </section>

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <Users className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.admin.membersCardTitle}</h2>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left text-sm">
            <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
              <tr>
                <th scope="col" className="px-4 py-2.5">{t.admin.colMember}</th>
                <th scope="col" className="px-4 py-2.5">{t.admin.colMembershipState}</th>
                <th scope="col" className="px-4 py-2.5">{t.admin.colGrants}</th>
                <th scope="col" className="px-4 py-2.5">{t.admin.colMemberActions}</th>
              </tr>
            </thead>
            <tbody>
              {overview.members.map((m) => {
                const isActive = m.membershipStatus === "ACTIVE";
                const isSelf = m.userId.toLowerCase() === currentUser.userId.toLowerCase();
                const remainingPerms = overview.availablePermissions.filter((p) => !m.permissions.includes(p));
                const selectedCode = remainingPerms.includes(selectedGrantByUser[m.userId] ?? "")
                  ? selectedGrantByUser[m.userId]!
                  : (remainingPerms[0] ?? "");
                const isMemberBusy = busyMemberId === m.userId;
                const isGrantBusy = busyGrantUserId === m.userId;
                const statusText = t.admin.statusLabels[m.membershipStatus] ?? m.membershipStatus;
                const statusBadge = isActive
                  ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                  : m.membershipStatus === "SUSPENDED"
                    ? "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                    : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]";

                return (
                  <tr
                    key={m.userId}
                    className="block border-b border-[var(--border-hairline)] p-4 last:border-b-0 md:table-row md:p-0"
                  >
                    <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                      <div className="font-medium text-[var(--text-primary)]">{m.displayName}</div>
                      <div className="font-code text-xs text-[var(--text-secondary)]">{m.email}</div>
                      <span className="mt-1 inline-block rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-1.5 py-0.5 text-[11px] text-[var(--text-secondary)]">
                        {m.mfaEnabled ? t.admin.mfaActiveTag : t.admin.mfaOffTag}
                      </span>
                    </td>

                    <td className="block pb-2 md:table-cell md:whitespace-nowrap md:px-4 md:py-3 md:align-top">
                      <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                        {t.admin.colMembershipState}:
                      </span>
                      <span className={`inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-semibold ${statusBadge}`}>
                        {isActive ? (
                          <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        ) : (
                          <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        )}
                        <span>{statusText}</span>
                      </span>
                    </td>

                    <td className="block pb-2.5 md:table-cell md:px-4 md:py-3 md:align-top">
                      <div className="mb-1 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                        {t.admin.colGrants}
                      </div>
                      <div className="flex flex-wrap gap-1.5">
                        {m.permissions.length === 0 ? (
                          <span className="text-xs text-[var(--text-muted)]">—</span>
                        ) : (
                          m.permissions.map((perm) => (
                            <span
                              key={perm}
                              className="font-code rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs text-[var(--text-secondary)]"
                            >
                              {perm}
                            </span>
                          ))
                        )}
                      </div>
                      {canManage && isActive && remainingPerms.length > 0 ? (
                        <div className="mt-2 flex flex-wrap items-center gap-1.5">
                          <select
                            aria-label={`${t.admin.addPermissionLabel} ${m.email}`}
                            value={selectedCode}
                            disabled={isGrantBusy}
                            onChange={(e) =>
                              setSelectedGrantByUser((prev) => ({ ...prev, [m.userId]: e.target.value }))
                            }
                            className="font-code h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)] disabled:opacity-60"
                          >
                            {remainingPerms.map((p) => (
                              <option key={p} value={p}>
                                {p}
                              </option>
                            ))}
                          </select>
                          <button
                            type="button"
                            disabled={isGrantBusy}
                            aria-label={`${t.admin.grantButton} ${selectedCode} (${m.email})`}
                            onClick={() => handleGrantPermission(m.userId, selectedCode)}
                            className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                          >
                            {t.admin.grantButton}
                          </button>
                        </div>
                      ) : null}
                    </td>

                    <td className="block md:table-cell md:whitespace-nowrap md:px-4 md:py-3 md:align-top">
                      {isSelf ? (
                        <span className="text-xs text-[var(--text-muted)]">{t.admin.selfLockedHint}</span>
                      ) : canManage && m.membershipStatus !== "REVOKED" ? (
                        <div className="flex flex-wrap gap-2">
                          {isActive ? (
                            <button
                              type="button"
                              disabled={isMemberBusy}
                              aria-label={`${t.admin.suspendButton} (${m.email})`}
                              onClick={() => handleMemberAction(m.userId, "suspend")}
                              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--status-warn-text)] hover:bg-[var(--status-warn-bg)] disabled:opacity-60"
                            >
                              {t.admin.suspendButton}
                            </button>
                          ) : null}
                          <button
                            type="button"
                            disabled={isMemberBusy}
                            aria-label={`${t.admin.revokeButton} (${m.email})`}
                            onClick={() => handleMemberAction(m.userId, "revoke")}
                            className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                          >
                            {t.admin.revokeButton}
                          </button>
                        </div>
                      ) : (
                        <span className="hidden text-xs text-[var(--text-muted)] md:inline">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <KeyRound className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.admin.inviteCardTitle}</h2>
        </div>

        {canManage ? (
          <form onSubmit={handleCreateInvite} noValidate className="grid grid-cols-1 gap-4 border-b border-[var(--border-hairline)] p-4 sm:grid-cols-12">
            <div className="sm:col-span-5">
              <label htmlFor="invite-email" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.admin.inviteEmailLabel}
              </label>
              <input
                id="invite-email"
                type="email"
                value={inviteEmail}
                onChange={(e) => {
                  setInviteEmail(e.target.value);
                  if (inviteEmailError) setInviteEmailError(null);
                }}
                placeholder={t.admin.inviteEmailPlaceholder}
                aria-invalid={Boolean(inviteEmailError)}
                aria-describedby={inviteEmailError ? "invite-email-error" : undefined}
                className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
              />
              {inviteEmailError ? (
                <p id="invite-email-error" role="alert" className="mt-1.5 text-xs text-[var(--status-danger-text)]">
                  {inviteEmailError}
                </p>
              ) : null}
            </div>

            <div className="sm:col-span-4">
              <label htmlFor="invite-role" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.admin.inviteRoleLabel}
              </label>
              <select
                id="invite-role"
                value={inviteRole}
                onChange={(e) => setInviteRole(e.target.value)}
                className="font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                {overview.availableRoleTemplates.map((rt) => (
                  <option key={rt} value={rt}>
                    {rt}
                  </option>
                ))}
              </select>
            </div>

            <div className="sm:col-span-3">
              <label htmlFor="invite-expiry" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.admin.inviteExpiryLabel}
              </label>
              <select
                id="invite-expiry"
                value={inviteExpiryHours}
                onChange={(e) => setInviteExpiryHours(Number(e.target.value))}
                className="font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                <option value={24}>24</option>
                <option value={48}>48</option>
                <option value={72}>72</option>
              </select>
            </div>

            <div className="sm:col-span-12">
              <button
                type="submit"
                disabled={inviteBusy}
                className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                {inviteBusy ? t.admin.createInviteBusy : t.admin.createInviteButton}
              </button>
            </div>
          </form>
        ) : null}

        {issuedInvite ? (
          <div className="mx-4 my-3 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] p-3.5 text-xs text-[var(--status-ok-text)]">
            <div className="flex items-start gap-2">
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <div className="min-w-0 flex-1">
                <p className="font-semibold">
                  {t.admin.tokenCreatedTitle} ({issuedInvite.invitedEmail})
                </p>
                <p className="mt-1">{t.admin.tokenCreatedNote}</p>
                <p
                  data-testid="issued-invite-token"
                  className="font-code mt-2 select-all break-all rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--bg-surface)] px-2.5 py-1.5 text-[var(--text-primary)]"
                >
                  {issuedInvite.inviteToken}
                </p>
                <div className="mt-2.5 flex flex-wrap items-center gap-2">
                  <button
                    type="button"
                    onClick={handleCopyInviteToken}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--status-ok-text)] hover:bg-[var(--bg-chrome)]"
                  >
                    <Copy className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                    <span>{copiedToken ? t.admin.copiedTokenLabel : t.admin.copyTokenButton}</span>
                  </button>
                  <Link
                    href={`/invitations?token=${encodeURIComponent(issuedInvite.inviteToken)}`}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                  >
                    <span>{t.admin.openRedeemLink}</span>
                    <ArrowUpRight className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                  </Link>
                </div>
              </div>
            </div>
          </div>
        ) : null}

        {overview.invitations.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="px-4 py-2.5">{t.admin.colInviteEmail}</th>
                  <th scope="col" className="px-4 py-2.5">{t.admin.colInviteStatus}</th>
                  <th scope="col" className="px-4 py-2.5">{t.admin.colInviteExpiry}</th>
                  <th scope="col" className="px-4 py-2.5 text-right">{t.admin.colInviteAction}</th>
                </tr>
              </thead>
              <tbody>
                {overview.invitations.map((inv) => {
                  const isInvAccepted = inv.status === "ACCEPTED";
                  const isInvPending = inv.status === "PENDING";
                  const isInvBusy = busyInviteId === inv.invitationId;
                  const invStatusText = t.admin.statusLabels[inv.status] ?? inv.status;
                  const invBadge = isInvAccepted
                    ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                    : isInvPending
                      ? "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                      : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]";
                  return (
                    <tr
                      key={inv.invitationId}
                      className="block border-b border-[var(--border-hairline)] p-4 last:border-b-0 md:table-row md:p-0"
                    >
                      <td className="block pb-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.admin.colInviteEmail}:
                        </span>
                        <div className="font-code text-xs font-semibold text-[var(--text-primary)]">{inv.invitedEmail}</div>
                        <div className="font-code text-xs text-[var(--text-secondary)]">{inv.roleTemplateCode}</div>
                      </td>
                      <td className="block pb-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.admin.colInviteStatus}:
                        </span>
                        <span className={`inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-semibold ${invBadge}`}>
                          {isInvAccepted ? (
                            <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          ) : isInvPending ? (
                            <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          ) : (
                            <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          )}
                          <span>{invStatusText}</span>
                        </span>
                      </td>
                      <td className="font-code block pb-2 text-xs tabular-nums text-[var(--text-secondary)] md:table-cell md:px-4 md:py-2.5">
                        <span className="mr-1.5 font-sans font-medium text-[var(--text-secondary)] md:hidden">
                          {t.admin.colInviteExpiry}:
                        </span>
                        <span>{formatJakartaDateTime(inv.expiresAtUtc, locale)}</span>
                      </td>
                      <td className="block md:table-cell md:px-4 md:py-2.5 md:text-right">
                        {canManage && isInvPending ? (
                          <button
                            type="button"
                            disabled={isInvBusy}
                            aria-label={`${t.admin.revokeInviteButton} (${inv.invitedEmail})`}
                            onClick={() => handleRevokeInvite(inv.invitationId)}
                            className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                          >
                            {t.admin.revokeInviteButton}
                          </button>
                        ) : (
                          <span className="hidden text-xs text-[var(--text-muted)] md:inline">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <ShieldCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.admin.auditCardTitle}</h2>
        </div>

        {overview.auditEvents.length === 0 ? (
          <p className="p-6 text-sm text-[var(--text-secondary)]">{t.admin.emptyAudit}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="px-4 py-2.5">{t.admin.colAuditTime}</th>
                  <th scope="col" className="px-4 py-2.5">{t.admin.colAuditEvent}</th>
                  <th scope="col" className="px-4 py-2.5">{t.admin.colAuditSummary}</th>
                </tr>
              </thead>
              <tbody>
                {overview.auditEvents.map((evt) => (
                  <tr
                    key={evt.eventId}
                    className="block border-b border-[var(--border-hairline)] p-4 last:border-b-0 md:table-row md:p-0"
                  >
                    <td className="font-code block pb-0.5 text-xs tabular-nums text-[var(--text-secondary)] md:table-cell md:whitespace-nowrap md:px-4 md:py-2.5">
                      <span className="mr-1.5 font-sans font-medium text-[var(--text-secondary)] md:hidden">
                        {t.admin.colAuditTime}:
                      </span>
                      <span>{formatJakartaDateTime(evt.occurredAtUtc, locale)}</span>
                    </td>
                    <td className="block pb-0.5 md:table-cell md:px-4 md:py-2.5">
                      <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                        {t.admin.colAuditEvent}:
                      </span>
                      <div className="font-code text-xs font-semibold text-[var(--text-primary)]">{evt.eventType}</div>
                      <div className="font-code text-[11px] text-[var(--text-secondary)]">{evt.actorEmail}</div>
                    </td>
                    <td className="block text-sm text-[var(--text-primary)] md:table-cell md:px-4 md:py-2.5">
                      <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                        {t.admin.colAuditSummary}:
                      </span>
                      <span>{evt.summary}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

export default function EntityAdminPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <EntityAdminSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
