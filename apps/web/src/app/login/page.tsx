"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AlertCircle, Building2, Eye, EyeOff, KeyRound, ShieldCheck } from "lucide-react";
import { ApiProblemError, ensureCsrfToken, loginWithPassword } from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { LocaleToggle } from "@/components/LocaleToggle";

export default function LoginPage() {
  const router = useRouter();
  const { t } = useLocale();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [errorDetail, setErrorDetail] = useState<string | null>(null);
  const [emailInvalid, setEmailInvalid] = useState(false);
  const [passwordInvalid, setPasswordInvalid] = useState(false);

  useEffect(() => {
    void ensureCsrfToken().catch(() => {
      // Handled at submit time if upstream is unavailable
    });
  }, []);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setErrorCode(null);
    setErrorDetail(null);

    const missingEmail = !email.trim();
    const missingPassword = !password;
    if (missingEmail || missingPassword) {
      setEmailInvalid(missingEmail);
      setPasswordInvalid(missingPassword);
      setErrorCode("validation_error");
      return;
    }

    setEmailInvalid(false);
    setPasswordInvalid(false);
    setSubmitting(true);
    try {
      await loginWithPassword(email.trim(), password, rememberMe);
      router.replace("/");
      router.refresh();
    } catch (err) {
      const problem = err as ApiProblemError;
      const code = problem.errorCode ?? "unknown";
      setErrorCode(code);
      setErrorDetail(problem.detail ?? null);
      if (code === "invalid_credentials") {
        setEmailInvalid(true);
        setPasswordInvalid(true);
      }
    } finally {
      setSubmitting(false);
    }
  }

  const resolvedErrorMessage = errorCode
    ? (t.login.errors[errorCode] ?? errorDetail ?? t.login.errors.unknown)
    : null;

  const securityIcons = [KeyRound, ShieldCheck, Building2];

  return (
    <div className="flex min-h-screen flex-col bg-[var(--bg-canvas)] text-[var(--text-primary)]">
      <header className="border-b border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex h-12 items-center justify-between px-4 sm:px-6">
          <div className="flex items-center gap-2.5">
            <span className="inline-flex h-7 w-7 items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]">
              <ShieldCheck className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
            </span>
            <span className="text-sm font-semibold tracking-tight sm:text-base">{t.appName}</span>
            <span className="hidden rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-chrome)] px-2 py-0.5 text-xs font-medium text-[var(--text-secondary)] sm:inline-block">
              {t.envLabel}
            </span>
          </div>
          <LocaleToggle />
        </div>
      </header>

      <main className="grid flex-1 grid-cols-1 lg:grid-cols-12">
        <section className="order-1 flex flex-col justify-center bg-[var(--bg-surface)] px-6 py-8 sm:px-12 sm:py-12 lg:order-2 lg:col-span-7">
          <div className="mx-auto w-full max-w-md">
            <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)] sm:text-2xl">
              {t.login.heading}
            </h1>
            <p className="mt-1.5 text-sm text-[var(--text-secondary)]">
              {t.login.subtitle}
            </p>

            {resolvedErrorMessage ? (
              <div
                id="login-error-banner"
                role="alert"
                className="mt-5 flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-3 text-sm text-[var(--status-danger-text)]"
              >
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{resolvedErrorMessage}</span>
              </div>
            ) : null}

            <form onSubmit={handleSubmit} className="mt-6 space-y-4" noValidate>
              <div>
                <label htmlFor="email" className="block text-sm font-medium text-[var(--text-primary)]">
                  {t.login.emailLabel}
                </label>
                <input
                  id="email"
                  name="email"
                  type="email"
                  autoComplete="username"
                  required
                  value={email}
                  onChange={(e) => {
                    setEmail(e.target.value);
                    if (emailInvalid) setEmailInvalid(false);
                  }}
                  placeholder={t.login.emailPlaceholder}
                  aria-invalid={emailInvalid ? "true" : "false"}
                  aria-describedby={resolvedErrorMessage ? "login-error-banner" : undefined}
                  className="mt-1.5 block min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 py-2 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-muted)] aria-[invalid=true]:border-[var(--status-danger-text)]"
                />
              </div>

              <div>
                <label htmlFor="password" className="block text-sm font-medium text-[var(--text-primary)]">
                  {t.login.passwordLabel}
                </label>
                <div className="relative mt-1.5">
                  <input
                    id="password"
                    name="password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    required
                    value={password}
                    onChange={(e) => {
                      setPassword(e.target.value);
                      if (passwordInvalid) setPasswordInvalid(false);
                    }}
                    aria-invalid={passwordInvalid ? "true" : "false"}
                    aria-describedby={resolvedErrorMessage ? "login-error-banner" : undefined}
                    className="block min-h-11 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] py-2 pl-3 pr-12 text-sm text-[var(--text-primary)] aria-[invalid=true]:border-[var(--status-danger-text)]"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((prev) => !prev)}
                    aria-label={showPassword ? t.login.hidePassword : t.login.showPassword}
                    className="absolute right-1 top-1/2 flex h-9 w-9 -translate-y-1/2 items-center justify-center rounded-[var(--radius-sm)] text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
                  >
                    {showPassword ? (
                      <EyeOff className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                    ) : (
                      <Eye className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                    )}
                  </button>
                </div>
              </div>

              <div className="flex items-center gap-2.5 pt-1">
                <input
                  id="rememberMe"
                  name="rememberMe"
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  className="h-4 w-4 rounded border-[var(--border-strong)] accent-[var(--accent-primary)]"
                />
                <label htmlFor="rememberMe" className="text-sm text-[var(--text-secondary)]">
                  {t.login.rememberLabel}
                </label>
              </div>

              <div className="pt-2">
                <button
                  type="submit"
                  disabled={submitting}
                  className="inline-flex min-h-11 w-full items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 py-2.5 text-sm font-semibold text-[var(--accent-primary-ink)] transition-colors hover:bg-[var(--accent-primary-hover)] disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {submitting ? t.login.submitBusy : t.login.submitIdle}
                </button>
              </div>
            </form>
          </div>
        </section>

        <aside className="order-2 flex flex-col justify-between border-t border-[var(--border-hairline)] bg-[var(--bg-chrome)] px-6 py-8 sm:px-10 sm:py-12 lg:order-1 lg:col-span-5 lg:border-r lg:border-t-0">
          <div className="mx-auto w-full max-w-md lg:mx-0">
            <h2 className="text-base font-semibold text-[var(--text-primary)]">
              {t.login.securityNoteTitle}
            </h2>
            <ul className="mt-4 divide-y divide-[var(--border-hairline)] border-y border-[var(--border-hairline)] text-sm leading-relaxed text-[var(--text-secondary)]">
              {t.login.securityPoints.map((point, index) => {
                const Icon = securityIcons[index] ?? ShieldCheck;
                return (
                  <li key={point} className="flex items-start gap-3 py-3.5">
                    <Icon
                      className="mt-0.5 h-4 w-4 shrink-0 text-[var(--accent-primary)]"
                      strokeWidth={1.75}
                      aria-hidden="true"
                    />
                    <span>{point}</span>
                  </li>
                );
              })}
            </ul>
          </div>

          <div className="mx-auto mt-8 w-full max-w-md border-t border-[var(--border-hairline)] pt-4 lg:mx-0">
            <p className="text-xs font-semibold text-[var(--text-primary)]">
              {t.login.demoHintTitle}
            </p>
            <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">
              {t.login.demoHintBody}
            </p>
          </div>
        </aside>
      </main>
    </div>
  );
}
