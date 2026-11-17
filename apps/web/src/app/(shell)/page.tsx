"use client";

import { useState } from "react";
import Link from "next/link";
import { ArrowRight, CheckCircle2, KeyRound, Lock, ShieldCheck } from "lucide-react";
import { useRefreshShell, useSessionUser } from "@/components/AppShell";
import { ApiProblemError, MfaSetupResponse, setupMfa, verifyMfa } from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

export default function SessionHomePage() {
  const user = useSessionUser();
  const refreshShell = useRefreshShell();
  const { locale, t } = useLocale();

  const [mfaSetup, setMfaSetup] = useState<MfaSetupResponse | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [mfaBusy, setMfaBusy] = useState(false);
  const [mfaError, setMfaError] = useState<string | null>(null);
  const [mfaCodeError, setMfaCodeError] = useState<string | null>(null);
  const [mfaDone, setMfaDone] = useState(false);

  const authorizedCount = Number(user.authorizedWorkspaceCount ?? 0);
  const mfaEnabled = Boolean(user.mfaEnabled || mfaDone);

  const rows = [
    { label: t.shell.fieldUser, value: user.displayName, mono: false },
    { label: t.shell.fieldEmail, value: user.email, mono: true },
    { label: t.shell.fieldLocale, value: user.preferredLocale, mono: true },
    {
      label: t.shell.fieldLastLogin,
      value: formatJakartaDateTime(user.lastSignedInAtUtc, locale),
      mono: true,
    },
    { label: t.shell.fieldWorkspaceStatus, value: user.workspaceAccessStatus, mono: true },
    { label: t.shell.fieldAuthorizedCount, value: String(authorizedCount), mono: true },
    {
      label: t.shell.fieldMfaStatus,
      value: mfaEnabled ? t.mfa.enabledBadge : t.mfa.disabledBadge,
      mono: false,
    },
    { label: t.shell.fieldDbVerification, value: t.shell.dbVerifiedValue, mono: false },
  ];

  async function handleStartMfaSetup() {
    setMfaBusy(true);
    setMfaError(null);
    try {
      const res = await setupMfa();
      setMfaSetup(res);
    } catch (err) {
      setMfaError((err as ApiProblemError).detail ?? t.login.errors.unknown);
    } finally {
      setMfaBusy(false);
    }
  }

  async function handleVerifyMfa(e: React.FormEvent) {
    e.preventDefault();
    setMfaError(null);
    setMfaCodeError(null);
    if (!mfaCode.trim()) {
      setMfaCodeError(t.mfa.codeRequiredError);
      return;
    }

    setMfaBusy(true);
    try {
      await verifyMfa(mfaCode.trim());
      await refreshShell();
      setMfaDone(true);
      setMfaSetup(null);
      setMfaCode("");
    } catch (err) {
      const msg = (err as ApiProblemError).detail ?? t.login.errors.unknown;
      setMfaCodeError(msg);
      setMfaError(msg);
    } finally {
      setMfaBusy(false);
    }
  }

  const hasWorkspaces = authorizedCount > 0;

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="border-b border-[var(--border-hairline)] pb-4">
        <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">
          {t.shell.sessionHeading}
        </h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          {t.shell.sessionLead}
        </p>
      </div>

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <ShieldCheck className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <span className="text-xs font-semibold text-[var(--text-primary)]">
            {t.shell.recordCardTitle}
          </span>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left text-sm">
            <tbody>
              {rows.map((row) => (
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

      <section className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2.5">
            <KeyRound className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <div>
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">{t.mfa.cardTitle}</h2>
              <p className="text-xs text-[var(--text-secondary)]">{t.mfa.description}</p>
            </div>
          </div>
          {mfaEnabled ? (
            <span className="inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2.5 py-1 text-xs font-semibold text-[var(--status-ok-text)]">
              <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <span>{t.mfa.enabledBadge}</span>
            </span>
          ) : (
            <button
              type="button"
              onClick={handleStartMfaSetup}
              disabled={mfaBusy}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
            >
              <span>{mfaBusy ? t.mfa.setupBusy : t.mfa.setupButton}</span>
            </button>
          )}
        </div>

        {mfaError ? (
          <p role="alert" className="mt-3 text-xs font-medium text-[var(--status-danger-text)]">
            {mfaError}
          </p>
        ) : null}

        {mfaSetup && !mfaEnabled ? (
          <form onSubmit={handleVerifyMfa} className="mt-4 space-y-3 border-t border-[var(--border-hairline)] pt-4">
            <div>
              <p className="text-xs font-medium text-[var(--text-secondary)]">{t.mfa.sharedKeyLabel}</p>
              <p className="font-code mt-1 break-all rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3 py-2 text-xs font-semibold text-[var(--text-primary)]">
                {mfaSetup.sharedKey}
              </p>
            </div>
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <label htmlFor="mfa-code" className="block text-xs font-medium text-[var(--text-secondary)]">
                  {t.mfa.codeLabel}
                </label>
                <input
                  id="mfa-code"
                  type="text"
                  inputMode="numeric"
                  aria-invalid={Boolean(mfaCodeError)}
                  aria-describedby={mfaCodeError ? "mfa-code-error" : undefined}
                  value={mfaCode}
                  onChange={(e) => {
                    setMfaCode(e.target.value);
                    setMfaCodeError(null);
                  }}
                  placeholder={t.mfa.codePlaceholder}
                  className={`font-code mt-1 h-10 w-44 rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                    mfaCodeError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                  }`}
                />
                {mfaCodeError ? (
                  <p id="mfa-code-error" role="alert" className="mt-1 text-xs text-[var(--status-danger-text)]">
                    {mfaCodeError}
                  </p>
                ) : null}
              </div>
              <button
                type="submit"
                disabled={mfaBusy}
                className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                {mfaBusy ? t.mfa.verifyBusy : t.mfa.verifyButton}
              </button>
            </div>
          </form>
        ) : null}
      </section>

      <section className="grid grid-cols-1 gap-4 border-t border-[var(--border-hairline)] pt-5 md:grid-cols-2">
        <div className="rounded-[var(--radius-md)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] p-4">
          <div className="flex items-start gap-2.5">
            <Lock className="mt-0.5 h-4 w-4 shrink-0 text-[var(--status-warn-text)]" strokeWidth={1.75} aria-hidden="true" />
            <div>
              <h2 className="text-sm font-semibold text-[var(--status-warn-text)]">
                {t.shell.boundaryTitle}
              </h2>
              <p className="mt-1 text-sm leading-relaxed text-[var(--status-warn-text)]">
                {t.shell.boundaryBody}
              </p>
            </div>
          </div>
        </div>

        <div className="flex flex-col justify-between rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-4">
          <div className="flex items-start gap-2.5">
            <ArrowRight className="mt-0.5 h-4 w-4 shrink-0 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <div>
              <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                {t.shell.nextStepHeading}
              </h2>
              <p className="mt-1 text-sm leading-relaxed text-[var(--text-secondary)]">
                {hasWorkspaces ? t.shell.nextStepBodyActive : t.shell.nextStepBodyEmpty}
              </p>
            </div>
          </div>
          <div className="mt-4 flex flex-wrap gap-2.5">
            <Link
              href="/workspaces"
              className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <span>{t.shell.openDirectoryCta}</span>
              <ArrowRight className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </Link>
            {!hasWorkspaces ? (
              <Link
                href="/invitations"
                className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-sm font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
              >
                {t.workspaces.openInvitationCta}
              </Link>
            ) : null}
          </div>
        </div>
      </section>
    </div>
  );
}
