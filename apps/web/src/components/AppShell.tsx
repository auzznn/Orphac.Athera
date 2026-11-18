"use client";

import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, usePathname, useRouter } from "next/navigation";
import {
  AlertCircle,
  Building2,
  Check,
  CheckCircle2,
  ChevronDown,
  FolderKanban,
  Handshake,
  KeyRound,
  Landmark,
  LayoutGrid,
  Lock,
  LogOut,
  Menu,
  Package,
  PackageCheck,
  Receipt,
  RefreshCw,
  ShieldCheck,
  ShoppingCart,
  Tags,
  Users,
  X,
} from "lucide-react";
import {
  ApiProblemError,
  AuthorizedTenantDto,
  CurrentUserResponse,
  fetchCurrentUser,
  fetchWorkspaces,
  LegalEntityDto,
  logoutSession,
  WorkspacesResponse,
} from "@/lib/api/client";
import { hasSavedLocalePreference, useLocale } from "@/lib/i18n/LocaleProvider";
import { LocaleToggle } from "@/components/LocaleToggle";

const SessionContext = createContext<CurrentUserResponse | null>(null);
const WorkspacesContext = createContext<WorkspacesResponse | null>(null);
const RefreshShellContext = createContext<() => Promise<void>>(async () => {});

export function useSessionUser(): CurrentUserResponse {
  const user = useContext(SessionContext);
  if (!user) {
    throw new Error("useSessionUser must be used inside authenticated AppShell");
  }
  return user;
}

export function useWorkspaces(): WorkspacesResponse | null {
  return useContext(WorkspacesContext);
}

export function useRefreshShell(): () => Promise<void> {
  return useContext(RefreshShellContext);
}

interface ActiveScopeMatch {
  tenant: AuthorizedTenantDto;
  entity: LegalEntityDto;
}

function resolveActiveScope(
  workspaces: WorkspacesResponse | null,
  tenantIdParam?: string,
  entityIdParam?: string,
): ActiveScopeMatch | null {
  if (!workspaces || !tenantIdParam || !entityIdParam) {
    return null;
  }

  for (const tenant of workspaces.tenants) {
    if (tenant.tenantId.toLowerCase() !== tenantIdParam.toLowerCase()) {
      continue;
    }
    const entity = tenant.entities.find(
      (e) => e.legalEntityId.toLowerCase() === entityIdParam.toLowerCase(),
    );
    if (entity) {
      return { tenant, entity };
    }
  }

  return null;
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const pathname = usePathname();
  const params = useParams<{ tenantId?: string; entityId?: string }>();
  const { t, setLocale } = useLocale();

  const [user, setUser] = useState<CurrentUserResponse | null>(null);
  const [workspaces, setWorkspaces] = useState<WorkspacesResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [retryNonce, setRetryNonce] = useState(0);
  const [signingOut, setSigningOut] = useState(false);
  const [signOutError, setSignOutError] = useState<string | null>(null);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [scopeMenuOpen, setScopeMenuOpen] = useState(false);
  const scopeContainerRef = useRef<HTMLDivElement | null>(null);
  const desktopTriggerRef = useRef<HTMLButtonElement | null>(null);
  const mobileTriggerRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  const refreshShell = useCallback(async () => {
    const current = await fetchCurrentUser();
    if (!current) {
      routerRef.current.replace("/login");
      return;
    }

    const ws = await fetchWorkspaces();
    setUser(current);
    setWorkspaces(ws);
    setLoadError(null);
  }, []);

  useEffect(() => {
    let active = true;

    fetchCurrentUser()
      .then(async (current) => {
        if (!active) return;
        if (!current) {
          routerRef.current.replace("/login");
          return;
        }

        const ws = await fetchWorkspaces();
        if (!active) return;

        setUser(current);
        setWorkspaces(ws);
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
        if (problem.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
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
  }, [setLocale, retryNonce, t.login.errors.upstream_unavailable]);

  useEffect(() => {
    if (!mobileNavOpen && !scopeMenuOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        const wasScopeOpen = scopeMenuOpen;
        setMobileNavOpen(false);
        setScopeMenuOpen(false);
        if (wasScopeOpen) {
          const desktopBtn = desktopTriggerRef.current;
          const mobileBtn = mobileTriggerRef.current;
          if (desktopBtn && desktopBtn.offsetParent !== null) {
            desktopBtn.focus();
          } else if (mobileBtn && mobileBtn.offsetParent !== null) {
            mobileBtn.focus();
          }
        }
      }
    };
    const onPointerDown = (event: MouseEvent) => {
      if (
        scopeMenuOpen &&
        scopeContainerRef.current &&
        !scopeContainerRef.current.contains(event.target as Node)
      ) {
        setScopeMenuOpen(false);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("mousedown", onPointerDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("mousedown", onPointerDown);
    };
  }, [mobileNavOpen, scopeMenuOpen]);

  async function handleSignOut() {
    setSigningOut(true);
    setSignOutError(null);
    try {
      await logoutSession();
      setUser(null);
      setWorkspaces(null);
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

  if (loading && !user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[var(--bg-canvas)] px-4">
        <div className="w-full max-w-md rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
          <div className="h-4 w-40 animate-pulse rounded bg-[var(--bg-chrome)]" />
          <p className="mt-3 text-sm text-[var(--text-secondary)]">{t.shell.loadingSession}</p>
        </div>
      </div>
    );
  }

  if (!user) {
    return null;
  }

  const activeScope = resolveActiveScope(
    workspaces,
    typeof params?.tenantId === "string" ? params.tenantId : undefined,
    typeof params?.entityId === "string" ? params.entityId : undefined,
  );
  const authorizedCount = Number(workspaces?.totalAuthorizedEntities ?? 0);
  const permissionSet = new Set(activeScope?.entity.permissions ?? []);

  const baseEntityPath = activeScope
    ? `/w/${activeScope.tenant.tenantId}/${activeScope.entity.legalEntityId}`
    : null;

  const topNavItems = [
    {
      key: "overview",
      href: "/",
      label: t.shell.navItems.overview,
      icon: LayoutGrid,
      active: pathname === "/",
    },
    {
      key: "workspaces",
      href: "/workspaces",
      label: t.shell.navItems.workspaces,
      icon: FolderKanban,
      active: pathname === "/workspaces" || (baseEntityPath !== null && pathname === baseEntityPath),
    },
    {
      key: "invitations",
      href: "/invitations",
      label: t.shell.navItems.invitations,
      icon: KeyRound,
      active: pathname.startsWith("/invitations"),
    },
  ];

  const liveEntityModules = [
    {
      key: "partners",
      label: t.shell.navItems.partners,
      icon: Handshake,
      href: baseEntityPath && (permissionSet.has("partners.read") || permissionSet.has("partners.manage")) ? `${baseEntityPath}/partners` : null,
      permitted: permissionSet.has("partners.read") || permissionSet.has("partners.manage"),
      active: pathname.endsWith("/partners"),
    },
    {
      key: "catalog",
      label: t.shell.navItems.catalog,
      icon: Tags,
      href: baseEntityPath && (permissionSet.has("catalog.read") || permissionSet.has("catalog.manage")) ? `${baseEntityPath}/catalog` : null,
      permitted: permissionSet.has("catalog.read") || permissionSet.has("catalog.manage"),
      active: pathname.endsWith("/catalog"),
    },
    {
      key: "approvals",
      label: t.shell.navItems.approvals,
      icon: CheckCircle2,
      href: baseEntityPath ? `${baseEntityPath}/approvals` : null,
      permitted: Boolean(activeScope),
      active: pathname.endsWith("/approvals"),
    },
    {
      key: "procurement",
      label: t.shell.navItems.procurement,
      icon: ShoppingCart,
      href:
        baseEntityPath &&
        (permissionSet.has("procurement.read") ||
          permissionSet.has("procurement.manage") ||
          permissionSet.has("procurement.approve"))
          ? `${baseEntityPath}/procurement`
          : null,
      permitted:
        permissionSet.has("procurement.read") ||
        permissionSet.has("procurement.manage") ||
        permissionSet.has("procurement.approve"),
      active: pathname.endsWith("/procurement"),
    },
    {
      key: "receiving",
      label: t.shell.navItems.receiving,
      icon: PackageCheck,
      href:
        baseEntityPath &&
        (permissionSet.has("receiving.read") ||
          permissionSet.has("receiving.manage") ||
          permissionSet.has("receiving.accept"))
          ? `${baseEntityPath}/receiving`
          : null,
      permitted:
        permissionSet.has("receiving.read") ||
        permissionSet.has("receiving.manage") ||
        permissionSet.has("receiving.accept"),
      active: pathname.endsWith("/receiving"),
    },
    {
      key: "inventory",
      label: t.shell.navItems.inventory,
      icon: Package,
      href: baseEntityPath && permissionSet.has("inventory.read") ? `${baseEntityPath}/inventory` : null,
      permitted: permissionSet.has("inventory.read"),
      active: pathname.endsWith("/inventory") || pathname.includes("/inventory/"),
    },
    {
      key: "finance",
      label: t.shell.navItems.finance,
      icon: Landmark,
      href:
        baseEntityPath &&
        (permissionSet.has("finance.read") ||
          permissionSet.has("finance.config.manage") ||
          permissionSet.has("finance.journal.post"))
          ? `${baseEntityPath}/finance`
          : null,
      permitted:
        permissionSet.has("finance.read") ||
        permissionSet.has("finance.config.manage") ||
        permissionSet.has("finance.journal.post"),
      active: pathname.endsWith("/finance"),
    },
    {
      key: "admin",
      label: t.shell.navItems.admin,
      icon: Users,
      href: baseEntityPath && (permissionSet.has("admin.read") || permissionSet.has("admin.manage")) ? `${baseEntityPath}/admin` : null,
      permitted: permissionSet.has("admin.read") || permissionSet.has("admin.manage"),
      active: pathname.endsWith("/admin"),
    },
  ];

  const plannedModuleItems = [
    {
      key: "sales",
      label: t.shell.navItems.sales,
      icon: Receipt,
      permitted: permissionSet.has("sales.read"),
    },
  ];

  const switcherSummary = activeScope
    ? `${activeScope.tenant.code} · ${activeScope.entity.code} · ${activeScope.entity.functionalCurrencyCode}`
    : `${t.shell.selectScope} (${authorizedCount})`;

  function renderScopePanel(panelId: string, positionClassName: string) {
    if (!scopeMenuOpen || !workspaces || authorizedCount === 0) {
      return null;
    }

    return (
      <div
        id={panelId}
        data-scope-panel="true"
        role="region"
        aria-label={t.shell.scopeMenuHeading}
        className={`absolute z-40 max-h-[75vh] overflow-y-auto rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)] p-2 ${positionClassName}`}
      >
        <div className="border-b border-[var(--border-hairline)] px-2.5 pb-2 pt-1 text-xs font-semibold text-[var(--text-secondary)]">
          {t.shell.scopeMenuHeading}
        </div>
        <div className="mt-1 space-y-2">
          {workspaces.tenants.map((tenant) => (
            <div key={tenant.tenantId} className="space-y-1">
              <div className="px-2.5 pt-1 text-xs font-semibold text-[var(--text-secondary)]">
                <span className="font-code text-[var(--text-primary)]">{tenant.code}</span>
                {" · "}
                <span>{tenant.name}</span>
              </div>
              {tenant.entities.map((entity) => {
                const isSelected = activeScope?.entity.legalEntityId === entity.legalEntityId;
                return (
                  <Link
                    key={entity.legalEntityId}
                    href={`/w/${tenant.tenantId}/${entity.legalEntityId}`}
                    onClick={() => setScopeMenuOpen(false)}
                    className={`flex min-h-10 items-center justify-between gap-2 rounded-[var(--radius-sm)] px-2.5 py-2 text-xs transition-colors ${
                      isSelected
                        ? "bg-[var(--accent-tint)] font-semibold text-[var(--accent-primary)]"
                        : "text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                    }`}
                  >
                    <span className="min-w-0">
                      <span className="font-code block font-semibold">
                        {entity.code} · {entity.functionalCurrencyCode}
                      </span>
                      <span className="block truncate text-[11px] text-[var(--text-secondary)]">
                        {entity.name}
                      </span>
                    </span>
                    {isSelected ? (
                      <Check
                        className="h-4 w-4 shrink-0 text-[var(--accent-primary)]"
                        strokeWidth={1.75}
                        aria-hidden="true"
                      />
                    ) : null}
                  </Link>
                );
              })}
            </div>
          ))}
        </div>
        <div className="mt-2 border-t border-[var(--border-hairline)] pt-2">
          <Link
            href="/workspaces"
            onClick={() => setScopeMenuOpen(false)}
            className="flex min-h-9 items-center justify-between rounded-[var(--radius-sm)] px-2.5 text-xs font-semibold text-[var(--accent-primary)] hover:bg-[var(--accent-tint)]"
          >
            <span>{t.shell.openDirectoryLink}</span>
          </Link>
        </div>
      </div>
    );
  }

  return (
    <RefreshShellContext.Provider value={refreshShell}>
      <SessionContext.Provider value={user}>
        <WorkspacesContext.Provider value={workspaces}>
          <div className="flex min-h-screen flex-col bg-[var(--bg-canvas)] text-[var(--text-primary)]">
            <a
              href="#main-content"
              className="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-50 focus:rounded-[var(--radius-sm)] focus:bg-[var(--accent-primary)] focus:px-3 focus:py-2 focus:text-sm focus:font-semibold focus:text-white"
            >
              {t.shell.skipToMain}
            </a>

            <header
              ref={scopeContainerRef}
              className="sticky top-0 z-20 border-b border-[var(--border-hairline)] bg-[var(--bg-surface)]"
            >
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

                  <Link href="/" className="flex shrink-0 items-center gap-2">
                    <span className="inline-flex h-7 w-7 items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]">
                      <ShieldCheck className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                    </span>
                    <span className="text-sm font-semibold tracking-tight sm:text-base">{t.appName}</span>
                  </Link>

                  {authorizedCount > 0 ? (
                    <div className="relative hidden md:block">
                      <button
                        ref={desktopTriggerRef}
                        type="button"
                        onClick={() => setScopeMenuOpen((prev) => !prev)}
                        aria-expanded={scopeMenuOpen}
                        aria-controls="scope-switcher-panel-desktop"
                        aria-label={t.shell.switchScopeLabel}
                        className="inline-flex min-h-9 max-w-[360px] items-center gap-2 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                      >
                        <Building2
                          className="h-3.5 w-3.5 shrink-0 text-[var(--accent-primary)]"
                          strokeWidth={1.75}
                          aria-hidden="true"
                        />
                        <span className="font-code truncate tabular-nums">{switcherSummary}</span>
                        <ChevronDown
                          className="h-3.5 w-3.5 shrink-0 text-[var(--text-secondary)]"
                          strokeWidth={1.75}
                          aria-hidden="true"
                        />
                      </button>
                      {renderScopePanel("scope-switcher-panel-desktop", "left-0 top-full mt-1.5 w-[380px]")}
                    </div>
                  ) : (
                    <div className="hidden items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2.5 py-1 text-xs font-medium text-[var(--status-warn-text)] md:inline-flex">
                      <Building2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                      <span className="truncate">{t.shell.scopeUnboundBadge}</span>
                    </div>
                  )}
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
                    className="inline-flex min-h-9 shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                  >
                    <LogOut className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    <span>{signingOut ? t.shell.signingOut : t.shell.signOut}</span>
                  </button>
                </div>
              </div>

              {authorizedCount > 0 ? (
                <div className="relative border-t border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-1.5 md:hidden">
                  <button
                    ref={mobileTriggerRef}
                    type="button"
                    onClick={() => setScopeMenuOpen((prev) => !prev)}
                    aria-expanded={scopeMenuOpen}
                    aria-controls="scope-switcher-panel-mobile"
                    aria-label={t.shell.switchScopeLabel}
                    className="flex min-h-9 w-full items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)]"
                  >
                    <span className="flex min-w-0 items-center gap-1.5">
                      <Building2
                        className="h-3.5 w-3.5 shrink-0 text-[var(--accent-primary)]"
                        strokeWidth={1.75}
                        aria-hidden="true"
                      />
                      <span className="font-code truncate tabular-nums">{switcherSummary}</span>
                    </span>
                    <ChevronDown
                      className="h-3.5 w-3.5 shrink-0 text-[var(--text-secondary)]"
                      strokeWidth={1.75}
                      aria-hidden="true"
                    />
                  </button>
                  {renderScopePanel("scope-switcher-panel-mobile", "left-4 right-4 top-full mt-1")}
                </div>
              ) : (
                <div className="flex items-center gap-2 border-t border-[var(--border-hairline)] bg-[var(--status-warn-bg)] px-4 py-1.5 text-xs font-medium text-[var(--status-warn-text)] md:hidden">
                  <Building2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span className="truncate">{t.shell.scopeCompactBadge}</span>
                </div>
              )}
            </header>

            <div className="relative flex flex-1">
              {mobileNavOpen ? (
                <div
                  aria-hidden="true"
                  onClick={() => setMobileNavOpen(false)}
                  className="fixed inset-0 z-30 bg-black/35 lg:hidden"
                />
              ) : null}

              <aside
                aria-label={t.shell.navTitle}
                className={`${
                  mobileNavOpen ? "fixed inset-y-0 left-0 z-40 block overflow-y-auto shadow-lg" : "hidden"
                } w-[232px] shrink-0 border-r border-[var(--border-hairline)] bg-[var(--bg-chrome)] p-3.5 lg:static lg:z-auto lg:block lg:shadow-none`}
              >
                <div className="mb-3 flex items-center justify-between border-b border-[var(--border-hairline)] pb-2.5 lg:hidden">
                  <span className="text-sm font-semibold text-[var(--text-primary)]">{t.appName}</span>
                  <button
                    type="button"
                    onClick={() => setMobileNavOpen(false)}
                    aria-label={t.shell.closeMenu}
                    className="inline-flex h-9 w-9 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] text-[var(--text-primary)]"
                  >
                    <X className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                  </button>
                </div>
                <nav className="space-y-1">
                  {topNavItems.map((item) => {
                    const Icon = item.icon;
                    return (
                      <Link
                        key={item.key}
                        href={item.href}
                        aria-current={item.active ? "page" : undefined}
                        onClick={() => setMobileNavOpen(false)}
                        className={`flex min-h-10 items-center justify-between rounded-[var(--radius-sm)] px-2.5 py-2 text-sm font-semibold ${
                          item.active
                            ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                            : "text-[var(--text-primary)] hover:bg-[var(--bg-surface)]"
                        }`}
                      >
                        <span className="flex items-center gap-2.5">
                          <Icon className="h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>{item.label}</span>
                        </span>
                      </Link>
                    );
                  })}
                </nav>

                <p className="mt-5 border-t border-[var(--border-hairline)] px-2 pt-3 text-xs font-semibold text-[var(--text-secondary)]">
                  {t.shell.navTitle}
                </p>
                <nav className="mt-2 space-y-1">
                  {liveEntityModules.map((item) => {
                    const Icon = item.icon;
                    if (item.href && item.permitted) {
                      return (
                        <Link
                          key={item.key}
                          href={item.href}
                          aria-current={item.active ? "page" : undefined}
                          onClick={() => setMobileNavOpen(false)}
                          className={`flex min-h-10 items-center justify-between rounded-[var(--radius-sm)] px-2.5 py-2 text-sm font-semibold ${
                            item.active
                              ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                              : "text-[var(--text-primary)] hover:bg-[var(--bg-surface)]"
                          }`}
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

                  {plannedModuleItems.map((item) => {
                    const Icon = item.icon;
                    const showPermitted = Boolean(activeScope && item.permitted);
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
                        {showPermitted ? (
                          <span className="rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--text-secondary)]">
                            {t.shell.modulePlannedHint}
                          </span>
                        ) : (
                          <Lock className="h-3.5 w-3.5 shrink-0 opacity-75" strokeWidth={1.75} aria-hidden="true" />
                        )}
                        <span className="sr-only">
                          {" "}
                          — {showPermitted ? t.shell.permittedSuffix : t.shell.lockedSuffix}
                        </span>
                      </button>
                    );
                  })}
                </nav>

                <p className="mt-6 border-t border-[var(--border-hairline)] px-2 pt-3 text-xs leading-relaxed text-[var(--text-secondary)]">
                  {authorizedCount > 0 ? t.shell.scopeBoundHint : t.shell.scopeUnboundHint}
                </p>
              </aside>

              <main id="main-content" className="min-w-0 flex-1 p-4 sm:p-6 lg:p-8">
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
        </WorkspacesContext.Provider>
      </SessionContext.Provider>
    </RefreshShellContext.Provider>
  );
}
