"use client";

import Link from "next/link";
import { ArrowRight, Lock, ShieldCheck } from "lucide-react";
import { useSessionUser } from "@/components/AppShell";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

export default function SessionHomePage() {
  const user = useSessionUser();
  const { locale, t } = useLocale();

  const authorizedCount = Number(user.authorizedWorkspaceCount ?? 0);
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
    {
      label: t.shell.fieldAuthorizedCount,
      value: String(authorizedCount),
      mono: true,
    },
    { label: t.shell.fieldDbVerification, value: t.shell.dbVerifiedValue, mono: false },
  ];

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
          <div className="mt-4">
            <Link
              href="/workspaces"
              className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <span>{t.shell.openDirectoryCta}</span>
              <ArrowRight className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
