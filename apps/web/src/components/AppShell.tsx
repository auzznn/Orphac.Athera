"use client";

import React, { createContext, useContext, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  AlertCircle,
  Building2,
  Landmark,
  LayoutGrid,
  Lock,
  LogOut,
  Menu,
  Package,
  Receipt,
  RefreshCw,
  ShieldCheck,
  ShoppingCart,
  Users,
  X,
} from "lucide-react";
import { ApiProblemError, CurrentUserResponse, fetchCurrentUser, logoutSession } from "@/lib/api/client";
import { hasSavedLocalePreference, useLocale } from "@/lib/i18n/LocaleProvider";
import { LocaleToggle } from "@/components/LocaleToggle";

const SessionContext = createContext<CurrentUserResponse | null>(null);

export function useSessionUser(): CurrentUserResponse {
  const user = useContext(SessionContext);
  if (!user) {
    throw new Error("useSessionUser must be used inside authenticated AppShell");
  }
  return user;
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { t, setLocale } = useLocale();

  const [user, setUser] = useState<CurrentUserResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [retryNonce, setRetryNonce] = useState(0);
  const [signingOut, setSigningOut] = useState(false);
  const [signOutError, setSignOutError] = useState<string | null>(null);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  useEffect(() => {
    let active = true;

    fetchCurrentUser()
      .then((current) => {
        if (!active) return;
        if (!current) {
          router.replace("/login");
          return;
        }
        setUser(current);
        setLoadError(null);
        if (
          !hasSavedLocalePreference() &&
          (current.preferredLocale === "id-ID" || current.preferredLocale === "en-US")
        ) {
          setLocale(current.preferredLocale);
        }
      })
      .catch((err) => {
        if (!active) return;
        const problem = err as Partial<ApiProblemError>;
        setLoadError(problem.detail ?? t.login.errors.upstream_unavailable);
      })
      .finally(() => {
        if (active) {
          setLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [router, setLocale, retryNonce, t.login.errors.upstream_unavailable]);

  useEffect(() => {
    if (!mobileNavOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMobileNavOpen(false);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [mobileNavOpen]);

  async function handleSignOut() {
    setSigningOut(true);
    setSignOutError(null);
    try {
      await logoutSession();
      setUser(null);
      router.replace("/login");
      router.refresh();
    } catch {
      setSignOutError(t.shell.logoutError);
      setSigningOut(false);
    }
  }

  function handleRetry() {
    setLoading(true);
    setLoadError(null);
    setRetryNonce((prev) => prev + 1);
  }

  if (loadError) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[var(--bg-canvas)] px-4">
        <div
          role="alert"
          className="w-full max-w-md rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
        >
          <div className="flex items-start gap-3 text-[var(--status-danger-text)]">
            <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <div>
              <h1 className="text-base font-semibold text-[var(--text-primary)]">{t.shell.errorHeading}</h1>
              <p className="mt-1.5 text-sm leading-relaxed text-[var(--text-secondary)]">{loadError}</p>
            </div>
          </div>
          <div className="mt-5 flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={handleRetry}
              className="inline-flex min-h-10 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
            >
              <RefreshCw className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
              <span>{t.shell.errorRetry}</span>
            </button>
            <Link
              href="/login"
              className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-sm font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {t.shell.errorBackToLogin}
            </Link>
          </div>
        </div>
      </div>
    );
  }

  if (loading || !user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[var(--bg-canvas)] px-4">
        <div className="w-full max-w-md rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
          <div className="h-4 w-40 animate-pulse rounded bg-[var(--bg-chrome)]" />
          <p className="mt-3 text-sm text-[var(--text-secondary)]">{t.shell.loadingSession}</p>
        </div>
      </div>
    );
  }

  const navItems = [
    { key: "overview", label: t.shell.navItems.overview, icon: LayoutGrid, active: true, locked: false },
    { key: "procurement", label: t.shell.navItems.procurement, icon: ShoppingCart, active: false, locked: true },
    { key: "inventory", label: t.shell.navItems.inventory, icon: Package, active: false, locked: true },
    { key: "sales", label: t.shell.navItems.sales, icon: Receipt, active: false, locked: true },
    { key: "finance", label: t.shell.navItems.finance, icon: Landmark, active: false, locked: true },
    { key: "admin", label: t.shell.navItems.admin, icon: Users, active: false, locked: true },
  ];

  return (
    <SessionContext.Provider value={user}>
      <div className="flex min-h-screen flex-col bg-[var(--bg-canvas)] text-[var(--text-primary)]">
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-50 focus:rounded-[var(--radius-sm)] focus:bg-[var(--accent-primary)] focus:px-3 focus:py-2 focus:text-sm focus:font-semibold focus:text-white"
        >
          {t.shell.skipToMain}
        </a>

        <header className="sticky top-0 z-30 border-b border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="flex h-12 items-center justify-between gap-3 px-4 sm:px-6">
            <div className="flex min-w-0 items-center gap-2.5">
              <button
                type="button"
                onClick={() => setMobileNavOpen((prev) => !prev)}
                aria-label={mobileNavOpen ? t.shell.closeMenu : t.shell.openMenu}
                aria-expanded={mobileNavOpen}
                className="inline-flex h-9 w-9 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-hairline)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] lg:hidden"
              >
                {mobileNavOpen ? (
                  <X className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                ) : (
                  <Menu className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                )}
              </button>

              <div className="flex shrink-0 items-center gap-2">
                <span className="inline-flex h-7 w-7 items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]">
                  <ShieldCheck className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                </span>
                <span className="text-sm font-semibold tracking-tight sm:text-base">{t.appName}</span>
              </div>

              <div className="hidden items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2.5 py-0.5 text-xs font-medium text-[var(--status-warn-text)] md:inline-flex">
                <Building2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span className="truncate">{t.shell.scopeUnboundBadge}</span>
              </div>
            </div>

            <div className="flex items-center gap-2 sm:gap-3">
              <LocaleToggle />
              <div className="hidden text-right sm:block">
                <p className="text-xs font-semibold leading-tight text-[var(--text-primary)]">
                  {user.displayName}
                </p>
                <p className="font-code text-[11px] leading-tight text-[var(--text-secondary)]">
                  {user.email}
                </p>
              </div>
              <button
                type="button"
                onClick={handleSignOut}
                disabled={signingOut}
                className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
              >
                <LogOut className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{signingOut ? t.shell.signingOut : t.shell.signOut}</span>
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2 border-t border-[var(--border-hairline)] bg-[var(--status-warn-bg)] px-4 py-1 text-xs font-medium text-[var(--status-warn-text)] md:hidden">
            <Building2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span className="truncate">{t.shell.scopeCompactBadge}</span>
          </div>
        </header>

        <div className="relative flex flex-1">
          {mobileNavOpen ? (
            <div
              aria-hidden="true"
              onClick={() => setMobileNavOpen(false)}
              className="fixed inset-0 top-12 z-20 bg-black/35 lg:hidden"
            />
          ) : null}

          <aside
            aria-label={t.shell.navTitle}
            className={`${
              mobileNavOpen ? "fixed inset-y-12 left-0 z-30 block shadow-lg" : "hidden"
            } w-[232px] shrink-0 border-r border-[var(--border-hairline)] bg-[var(--bg-chrome)] p-3.5 lg:static lg:z-auto lg:block lg:shadow-none`}
          >
            <p className="px-2 text-xs font-semibold text-[var(--text-secondary)]">
              {t.shell.navTitle}
            </p>
            <nav className="mt-2 space-y-1">
              {navItems.map((item) => {
                const Icon = item.icon;
                if (item.active) {
                  return (
                    <Link
                      key={item.key}
                      href="/"
                      aria-current="page"
                      onClick={() => setMobileNavOpen(false)}
                      className="flex min-h-10 items-center justify-between rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-2.5 py-2 text-sm font-semibold text-[var(--accent-primary-ink)]"
                    >
                      <span className="flex items-center gap-2.5">
                        <Icon className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{item.label}</span>
                      </span>
                    </Link>
                  );
                }

                return (
                  <button
                    key={item.key}
                    type="button"
                    disabled
                    className="flex min-h-10 w-full cursor-not-allowed items-center justify-between rounded-[var(--radius-sm)] px-2.5 py-2 text-left text-sm text-[var(--text-muted)]"
                  >
                    <span className="flex items-center gap-2.5">
                      <Icon className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                      <span>{item.label}</span>
                    </span>
                    <Lock className="h-3.5 w-3.5 shrink-0 opacity-75" strokeWidth={1.75} aria-hidden="true" />
                    <span className="sr-only"> — {t.shell.lockedSuffix}</span>
                  </button>
                );
              })}
            </nav>

            <p className="mt-6 border-t border-[var(--border-hairline)] px-2 pt-3 text-xs leading-relaxed text-[var(--text-secondary)]">
              {t.shell.scopeUnboundHint}
            </p>
          </aside>

          <main id="main-content" className="flex-1 p-4 sm:p-6 lg:p-8">
            {signOutError ? (
              <div
                role="alert"
                className="mx-auto mb-4 flex max-w-4xl items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-2.5 text-sm text-[var(--status-danger-text)]"
              >
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                <span>{signOutError}</span>
              </div>
            ) : null}
            {children}
          </main>
        </div>
      </div>
    </SessionContext.Provider>
  );
}
