"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { AlertCircle, ArrowRight, CheckCircle2, KeyRound } from "lucide-react";
import { useRefreshShell } from "@/components/AppShell";
import {
  acceptInvitation,
  AcceptInvitationResponse,
  ApiProblemError,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";

function InvitationAcceptanceForm() {
  const searchParams = useSearchParams();
  const refreshShell = useRefreshShell();
  const { t } = useLocale();

  const [token, setToken] = useState(() => searchParams.get("token") ?? "");
  const [tokenError, setTokenError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [accepted, setAccepted] = useState<AcceptInvitationResponse | null>(null);

  async function handleAccept(event: React.FormEvent) {
    event.preventDefault();
    setErrorMsg(null);
    setTokenError(null);

    if (!token.trim()) {
      setTokenError(t.invitations.tokenRequiredError);
      return;
    }

    setSubmitting(true);
    try {
      const result = await acceptInvitation(token.trim());
      await refreshShell();
      setAccepted(result);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      const mapped = t.invitations.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown;
      setErrorMsg(mapped);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="border-b border-[var(--border-hairline)] pb-4">
        <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">
          {t.invitations.heading}
        </h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          {t.invitations.lead}
        </p>
      </div>

      {accepted ? (
        <section className="rounded-[var(--radius-md)] border border-[var(--status-ok-border)] bg-[var(--bg-surface)] p-6">
          <div className="flex items-start gap-3">
            <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]">
              <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </span>
            <div className="space-y-2">
              <h2 className="text-base font-semibold text-[var(--text-primary)]">
                {t.invitations.successTitle} —{" "}
                <span className="font-code">{accepted.tenantCode} · {accepted.entityCode}</span>
              </h2>
              <p className="text-sm leading-relaxed text-[var(--text-secondary)]">
                {t.invitations.successBody}
              </p>
              <div className="flex flex-wrap gap-1.5 pt-1">
                {accepted.grantedPermissions.map((perm) => (
                  <span
                    key={perm}
                    className="font-code rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs text-[var(--text-secondary)]"
                  >
                    {perm}
                  </span>
                ))}
              </div>
            </div>
          </div>

          <div className="mt-5 border-t border-[var(--border-hairline)] pt-4">
            <Link
              href={`/w/${accepted.tenantId}/${accepted.legalEntityId}`}
              className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <span>{t.invitations.enterGrantedEntity}</span>
              <ArrowRight className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </Link>
          </div>
        </section>
      ) : (
        <section className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <KeyRound className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">
              {t.invitations.cardTitle}
            </h2>
          </div>

          <form onSubmit={handleAccept} noValidate className="space-y-4 p-4 sm:p-6">
            {errorMsg ? (
              <div
                role="alert"
                className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-2.5 text-sm text-[var(--status-danger-text)]"
              >
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{errorMsg}</span>
              </div>
            ) : null}

            <div>
              <label htmlFor="invitation-token" className="block text-xs font-semibold text-[var(--text-primary)]">
                {t.invitations.tokenLabel}
              </label>
              <input
                id="invitation-token"
                type="text"
                aria-invalid={Boolean(tokenError)}
                aria-describedby={tokenError ? "invitation-token-error" : undefined}
                value={token}
                onChange={(e) => {
                  setToken(e.target.value);
                  setTokenError(null);
                }}
                placeholder={t.invitations.tokenPlaceholder}
                className={`font-code mt-1.5 h-11 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                  tokenError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                }`}
              />
              {tokenError ? (
                <p id="invitation-token-error" role="alert" className="mt-1.5 text-xs text-[var(--status-danger-text)]">
                  {tokenError}
                </p>
              ) : null}
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="inline-flex min-h-10 items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
            >
              {submitting ? t.invitations.submitBusy : t.invitations.submitButton}
            </button>
          </form>
        </section>
      )}
    </div>
  );
}

export default function InvitationsPage() {
  return (
    <Suspense>
      <InvitationAcceptanceForm />
    </Suspense>
  );
}
