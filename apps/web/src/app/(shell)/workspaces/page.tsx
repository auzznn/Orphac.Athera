"use client";

import Link from "next/link";
import { ArrowRight, Building2, CheckCircle2, Lock } from "lucide-react";
import { useSessionUser, useWorkspaces } from "@/components/AppShell";
import { useLocale } from "@/lib/i18n/LocaleProvider";

export default function WorkspacesDirectoryPage() {
  const user = useSessionUser();
  const workspaces = useWorkspaces();
  const { t } = useLocale();

  const tenants = workspaces?.tenants ?? [];
  const totalEntities = Number(workspaces?.totalAuthorizedEntities ?? 0);

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">
            {t.workspaces.heading}
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            {t.workspaces.lead}
          </p>
        </div>

        <div className="flex items-center gap-4 text-xs text-[var(--text-secondary)]">
          <span>
            {t.workspaces.tenantCountLabel}:{" "}
            <strong className="font-code tabular-nums text-[var(--text-primary)]">
              {tenants.length}
            </strong>
          </span>
          <span>
            {t.workspaces.entityCountLabel}:{" "}
            <strong className="font-code tabular-nums text-[var(--text-primary)]">
              {totalEntities}
            </strong>
          </span>
        </div>
      </div>

      {tenants.length === 0 ? (
        <section
          aria-labelledby="no-access-heading"
          className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6"
        >
          <div className="flex items-start gap-3">
            <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]">
              <Lock className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </span>
            <div className="max-w-2xl space-y-2">
              <h2 id="no-access-heading" className="text-base font-semibold text-[var(--text-primary)]">
                {t.workspaces.emptyTitle}
              </h2>
              <p className="text-sm leading-relaxed text-[var(--text-secondary)]">
                {t.workspaces.emptyBody}
              </p>
            </div>
          </div>

          <div className="mt-5 border-t border-[var(--border-hairline)] pt-4">
            <h3 className="text-xs font-semibold text-[var(--text-primary)]">
              {t.workspaces.emptyGuidanceTitle}
            </h3>
            <ul className="mt-2 list-disc space-y-1.5 pl-5 text-sm text-[var(--text-secondary)]">
              <li>{t.workspaces.emptyGuidanceStep1}</li>
              <li>{t.workspaces.emptyGuidanceStep2}</li>
            </ul>
            <p className="mt-4 text-xs text-[var(--text-secondary)]">
              {t.workspaces.emptyIdentityRef}:{" "}
              <span className="font-code text-[var(--text-primary)]">{user.email}</span>
            </p>
            <div className="mt-4">
              <Link
                href="/invitations"
                className="inline-flex min-h-10 items-center gap-2 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
              >
                <span>{t.workspaces.openInvitationCta}</span>
                <ArrowRight className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
              </Link>
            </div>
          </div>
        </section>
      ) : (
        <div className="space-y-5">
          {tenants.map((tenant) => (
            <section
              key={tenant.tenantId}
              className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]"
            >
              <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-3">
                <div className="flex items-center gap-2.5">
                  <Building2 className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                  <h2 className="text-sm font-semibold text-[var(--text-primary)]">
                    <span className="font-code">{tenant.code}</span>
                    {" — "}
                    <span>{tenant.name}</span>
                  </h2>
                </div>
                <span className="inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                  <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{t.workspaces.activeStatus}</span>
                </span>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-sm">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="px-4 py-2.5">{t.workspaces.colEntity}</th>
                      <th scope="col" className="px-4 py-2.5">{t.workspaces.colFiscalProfile}</th>
                      <th scope="col" className="px-4 py-2.5">{t.workspaces.colGrants}</th>
                      <th scope="col" className="px-4 py-2.5 text-right">{t.workspaces.colAction}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {tenant.entities.map((entity) => (
                      <tr
                        key={entity.legalEntityId}
                        className="block border-b border-[var(--border-hairline)] p-4 last:border-b-0 hover:bg-[var(--bg-subtle)]/60 md:table-row md:p-0"
                      >
                        <td className="block pb-1.5 md:table-cell md:px-4 md:py-3">
                          <div className="font-code text-xs font-semibold text-[var(--accent-primary)]">
                            {entity.code}
                          </div>
                          <div className="mt-0.5 font-medium text-[var(--text-primary)]">
                            {entity.name}
                          </div>
                        </td>
                        <td className="block pb-2 md:table-cell md:whitespace-nowrap md:px-4 md:py-3">
                          <div className="font-code text-xs tabular-nums text-[var(--text-primary)] md:text-[13px]">
                            <span className="mr-1.5 font-sans font-medium text-[var(--text-secondary)] md:hidden">
                              {t.workspaces.colNpwp}:
                            </span>
                            <span>{entity.taxRegistrationNpwp ?? t.workspaces.noNpwp}</span>
                          </div>
                          <div className="font-code mt-0.5 text-xs tabular-nums text-[var(--text-secondary)]">
                            <span className="mr-1.5 font-sans font-medium text-[var(--text-secondary)] md:hidden">
                              {t.workspaces.colFiscal}:
                            </span>
                            <span>{entity.functionalCurrencyCode} · {entity.timeZoneId}</span>
                          </div>
                        </td>
                        <td className="block pb-3 md:table-cell md:px-4 md:py-3">
                          <span className="mb-1 block text-xs font-medium text-[var(--text-secondary)] md:hidden">
                            {t.workspaces.colGrants}
                          </span>
                          <div className="flex flex-wrap gap-1.5">
                            {entity.permissions.map((perm) => (
                              <span
                                key={perm}
                                className="font-code whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs text-[var(--text-secondary)]"
                              >
                                {perm}
                              </span>
                            ))}
                          </div>
                        </td>
                        <td className="block md:table-cell md:whitespace-nowrap md:px-4 md:py-3 md:text-right">
                          <Link
                            href={`/w/${tenant.tenantId}/${entity.legalEntityId}`}
                            className="inline-flex min-h-10 w-full items-center justify-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] md:min-h-9 md:w-auto"
                          >
                            <span>{t.workspaces.enterWorkspace}</span>
                            <ArrowRight className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}
