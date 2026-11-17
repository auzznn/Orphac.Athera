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
  Handshake,
  Info,
  Lock,
  Plus,
  Search,
  UserPlus,
} from "lucide-react";
import { ScopedBoundaryError, ScopedLoadingSheet } from "@/components/ScopedBoundaryState";
import {
  ApiProblemError,
  createPartner,
  createPartnerContact,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchPartnerDetail,
  fetchPartners,
  PagedPartnersResponse,
  PartnerDetailDto,
  updatePartner,
  upsertPartnerEntityProfile,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

function PartnersSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const { locale, t } = useLocale();

  const [detail, setDetail] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [partnersData, setPartnersData] = useState<PagedPartnersResponse | null>(null);
  const [selectedPartner, setSelectedPartner] = useState<PartnerDetailDto | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loading, setLoading] = useState(true);
  const [reloadNonce, setReloadNonce] = useState(0);
  const [actionError, setActionError] = useState<string | null>(null);

  const [searchText, setSearchText] = useState("");
  const [roleFilter, setRoleFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [eligibleOnly, setEligibleOnly] = useState(false);

  const [partnerCode, setPartnerCode] = useState("");
  const [legalName, setLegalName] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [isSupplier, setIsSupplier] = useState(true);
  const [isCustomer, setIsCustomer] = useState(false);
  const [npwp, setNpwp] = useState("");
  const [pkpStatus, setPkpStatus] = useState("UNKNOWN");
  const [scopeMode, setScopeMode] = useState("ALL_ENTITIES");
  const [createError, setCreateError] = useState<{
    field: "code" | "legalName" | "roles" | "form";
    message: string;
  } | null>(null);
  const [createBusy, setCreateBusy] = useState(false);

  const [contactName, setContactName] = useState("");
  const [contactRole, setContactRole] = useState("COMMERCIAL");
  const [contactEmail, setContactEmail] = useState("");
  const [contactPhone, setContactPhone] = useState("");
  const [contactPrimary, setContactPrimary] = useState(true);
  const [contactError, setContactError] = useState<string | null>(null);
  const [contactBusy, setContactBusy] = useState(false);

  const [profSupplier, setProfSupplier] = useState(false);
  const [profCustomer, setProfCustomer] = useState(false);
  const [profTerms, setProfTerms] = useState(30);
  const [profNotes, setProfNotes] = useState("");
  const [profBusy, setProfBusy] = useState(false);
  const [profileError, setProfileError] = useState<{
    field: "terms" | "form";
    message: string;
  } | null>(null);
  const [updateScopeValue, setUpdateScopeValue] = useState("ALL_ENTITIES");
  const [statusBusy, setStatusBusy] = useState(false);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchPartners(tenantId, entityId, {
        q: searchText.trim() || undefined,
        role: roleFilter || undefined,
        status: statusFilter || undefined,
        eligibleOnly: eligibleOnly || undefined,
        page: 1,
        pageSize: 50,
      }),
    ])
      .then(([entityDetail, paged]) => {
        if (!active) return;
        setDetail(entityDetail);
        setPartnersData(paged);
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
  }, [tenantId, entityId, searchText, roleFilter, statusFilter, eligibleOnly, reloadNonce]);

  useEffect(() => {
    if (!selectedId) {
      return;
    }

    let active = true;
    fetchPartnerDetail(tenantId, entityId, selectedId)
      .then((d) => {
        if (!active) return;
        setSelectedPartner(d);
        setUpdateScopeValue(d.entityScopeMode);
        setProfSupplier(d.currentEntityProfile?.supplierEligible ?? d.effectiveSupplierEligible);
        setProfCustomer(d.currentEntityProfile?.customerEligible ?? d.effectiveCustomerEligible);
        setProfTerms(Number(d.currentEntityProfile?.defaultPaymentTermsDays ?? 30));
        setProfNotes(d.currentEntityProfile?.notes ?? "");
        setProfileError(null);
        setContactError(null);
      })
      .catch((err) => {
        if (!active) return;
        setActionError((err as ApiProblemError).detail ?? t.login.errors.unknown);
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, selectedId, t.login.errors.unknown]);

  async function handleCreatePartner(e: React.FormEvent) {
    e.preventDefault();
    if (partnerCode.trim().length < 2) {
      setCreateError({ field: "code", message: t.partners.validation.codeRequired });
      return;
    }
    if (!legalName.trim()) {
      setCreateError({ field: "legalName", message: t.partners.validation.legalNameRequired });
      return;
    }
    if (!isSupplier && !isCustomer) {
      setCreateError({ field: "roles", message: t.partners.validation.roleRequired });
      return;
    }

    setCreateBusy(true);
    setCreateError(null);
    setActionError(null);
    try {
      const created = await createPartner(tenantId, entityId, {
        partnerCode: partnerCode.trim(),
        legalName: legalName.trim(),
        displayName: displayName.trim() || legalName.trim(),
        isSupplier,
        isCustomer,
        taxRegistrationNpwp: npwp.trim() || null,
        taxPkpStatus: pkpStatus,
        entityScopeMode: scopeMode,
      });
      setPartnerCode("");
      setLegalName("");
      setDisplayName("");
      setNpwp("");
      setSelectedId(created.partnerId);
      setSelectedPartner(created);
      setUpdateScopeValue(created.entityScopeMode);
      setProfSupplier(created.currentEntityProfile?.supplierEligible ?? created.effectiveSupplierEligible);
      setProfCustomer(created.currentEntityProfile?.customerEligible ?? created.effectiveCustomerEligible);
      setProfTerms(Number(created.currentEntityProfile?.defaultPaymentTermsDays ?? 30));
      setProfNotes(created.currentEntityProfile?.notes ?? "");
      setProfileError(null);
      setContactError(null);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setCreateError({
        field: "form",
        message: t.partners.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown,
      });
    } finally {
      setCreateBusy(false);
    }
  }

  async function handleAddContact(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedPartner) return;
    if (!contactName.trim()) {
      setContactError(t.partners.validation.contactNameRequired);
      return;
    }

    setContactBusy(true);
    setContactError(null);
    setActionError(null);
    try {
      const updated = await createPartnerContact(tenantId, entityId, selectedPartner.partnerId, {
        contactName: contactName.trim(),
        contactRole,
        email: contactEmail.trim() || null,
        phone: contactPhone.trim() || null,
        isPrimary: contactPrimary,
      });
      setSelectedPartner(updated);
      setContactName("");
      setContactEmail("");
      setContactPhone("");
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setContactError(t.partners.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setContactBusy(false);
    }
  }

  async function handleSaveProfile(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedPartner) return;
    if (!Number.isInteger(profTerms) || profTerms < 0 || profTerms > 365) {
      setProfileError({ field: "terms", message: t.partners.validation.profileTermsInvalid });
      return;
    }

    setProfBusy(true);
    setProfileError(null);
    setActionError(null);
    try {
      const updated = await upsertPartnerEntityProfile(tenantId, entityId, selectedPartner.partnerId, {
        supplierEligible: profSupplier,
        customerEligible: profCustomer,
        defaultPaymentTermsDays: profTerms,
        notes: profNotes.trim() || null,
      });
      setSelectedPartner(updated);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setProfileError({
        field: "form",
        message: t.partners.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown,
      });
    } finally {
      setProfBusy(false);
    }
  }

  async function handleUpdatePartnerMeta(nextStatus?: string, nextScopeMode?: string) {
    if (!selectedPartner) return;
    setStatusBusy(true);
    setProfileError(null);
    setActionError(null);
    try {
      const updated = await updatePartner(tenantId, entityId, selectedPartner.partnerId, {
        legalName: selectedPartner.legalName,
        displayName: selectedPartner.displayName,
        isSupplier: selectedPartner.isSupplier,
        isCustomer: selectedPartner.isCustomer,
        taxRegistrationNpwp: selectedPartner.taxRegistrationNpwp,
        taxPkpStatus: selectedPartner.taxPkpStatus,
        entityScopeMode: nextScopeMode ?? selectedPartner.entityScopeMode,
        status: nextStatus ?? selectedPartner.status,
        expectedVersion: selectedPartner.version,
      });
      setSelectedPartner(updated);
      setUpdateScopeValue(updated.entityScopeMode);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setProfileError({
        field: "form",
        message: t.partners.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown,
      });
    } finally {
      setStatusBusy(false);
    }
  }

  if (loading && !partnersData) {
    return <ScopedLoadingSheet />;
  }

  if (problem || !detail || !partnersData) {
    return (
      <ScopedBoundaryError
        problem={problem}
        onRetry={() => {
          setLoading(true);
          setReloadNonce((n) => n + 1);
        }}
      />
    );
  }

  const canManage = detail.effectivePermissions.includes("partners.manage");

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">{t.partners.heading}</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
              {detail.tenant.code} · {detail.entity.code}
            </span>
            {" — "}
            <span>{t.partners.lead}</span>
          </p>
        </div>

        <Link
          href={`/w/${tenantId}/${entityId}`}
          className="inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.partners.backToEntity} · <span className="font-code">{detail.entity.code}</span></span>
        </Link>
      </div>

      {!canManage ? (
        <div className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-4 py-3 text-xs text-[var(--status-warn-text)]">
          <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.partners.readOnlyNotice}</span>
        </div>
      ) : null}

      <div className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-4 py-2.5 text-xs text-[var(--text-secondary)]">
        <Info className="mt-0.5 h-4 w-4 shrink-0 text-[var(--text-secondary)]" strokeWidth={1.75} aria-hidden="true" />
        <span>{t.partners.od07MetadataNotice}</span>
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

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <Search className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.partners.filterCardTitle}</h2>
        </div>

        <div className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-12">
          <div className="sm:col-span-5">
            <label htmlFor="partner-search" className="block text-xs font-semibold text-[var(--text-secondary)]">
              {t.partners.searchLabel}
            </label>
            <input
              id="partner-search"
              type="text"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={t.partners.searchPlaceholder}
              className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
            />
          </div>

          <div className="sm:col-span-3">
            <label htmlFor="partner-role-filter" className="block text-xs font-semibold text-[var(--text-secondary)]">
              {t.partners.roleFilterLabel}
            </label>
            <select
              id="partner-role-filter"
              value={roleFilter}
              onChange={(e) => setRoleFilter(e.target.value)}
              className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
            >
              <option value="">{t.partners.roleFilterAll}</option>
              <option value="SUPPLIER">{t.partners.roleFilterSupplier}</option>
              <option value="CUSTOMER">{t.partners.roleFilterCustomer}</option>
            </select>
          </div>

          <div className="sm:col-span-2">
            <label htmlFor="partner-status-filter" className="block text-xs font-semibold text-[var(--text-secondary)]">
              {t.partners.statusFilterLabel}
            </label>
            <select
              id="partner-status-filter"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
            >
              <option value="">{t.partners.statusFilterAll}</option>
              <option value="ACTIVE">{t.partners.statusLabels.ACTIVE}</option>
              <option value="INACTIVE">{t.partners.statusLabels.INACTIVE}</option>
            </select>
          </div>

          <div className="flex items-end pb-2 sm:col-span-2">
            <label className="inline-flex min-h-9 cursor-pointer items-center gap-2 text-xs font-medium text-[var(--text-primary)]">
              <input
                type="checkbox"
                checked={eligibleOnly}
                onChange={(e) => setEligibleOnly(e.target.checked)}
                className="h-4 w-4 rounded accent-[var(--accent-primary)]"
              />
              <span>{t.partners.eligibleOnlyLabel}</span>
            </label>
          </div>
        </div>
      </section>

      {canManage ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <Plus className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.partners.createCardTitle}</h2>
          </div>

          <form onSubmit={handleCreatePartner} noValidate className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-12">
            <div className="sm:col-span-3">
              <label htmlFor="new-partner-code" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.partners.codeLabel}
              </label>
              <input
                id="new-partner-code"
                type="text"
                value={partnerCode}
                onChange={(e) => {
                  setPartnerCode(e.target.value);
                  if (createError) setCreateError(null);
                }}
                placeholder={t.partners.codePlaceholder}
                aria-invalid={createError?.field === "code"}
                aria-describedby={createError?.field === "code" ? "partner-create-error" : undefined}
                className={`font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm uppercase text-[var(--text-primary)] ${
                  createError?.field === "code" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                }`}
              />
            </div>

            <div className="sm:col-span-5">
              <label htmlFor="new-partner-legal-name" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.partners.legalNameLabel}
              </label>
              <input
                id="new-partner-legal-name"
                type="text"
                value={legalName}
                onChange={(e) => {
                  setLegalName(e.target.value);
                  if (createError) setCreateError(null);
                }}
                placeholder={t.partners.legalNamePlaceholder}
                aria-invalid={createError?.field === "legalName"}
                aria-describedby={createError?.field === "legalName" ? "partner-create-error" : undefined}
                className={`mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                  createError?.field === "legalName" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                }`}
              />
            </div>

            <div className="sm:col-span-4">
              <label htmlFor="new-partner-display" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.partners.displayNameLabel}
              </label>
              <input
                id="new-partner-display"
                type="text"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                placeholder={t.partners.displayNamePlaceholder}
                className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
              />
            </div>

            <fieldset
              aria-describedby={createError?.field === "roles" ? "partner-create-error" : undefined}
              className={`sm:col-span-12 rounded-[var(--radius-sm)] ${
                createError?.field === "roles" ? "border border-[var(--status-danger-text)] p-2.5" : ""
              }`}
            >
              <legend className="text-xs font-semibold text-[var(--text-secondary)]">{t.partners.rolesLabel}</legend>
              <div className="mt-1.5 flex flex-wrap gap-6">
                <label className="inline-flex min-h-9 cursor-pointer items-center gap-2 text-sm text-[var(--text-primary)]">
                  <input
                    type="checkbox"
                    checked={isSupplier}
                    onChange={(e) => {
                      setIsSupplier(e.target.checked);
                      if (createError) setCreateError(null);
                    }}
                    className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                  />
                  <span>{t.partners.roleSupplierCheck}</span>
                </label>
                <label className="inline-flex min-h-9 cursor-pointer items-center gap-2 text-sm text-[var(--text-primary)]">
                  <input
                    type="checkbox"
                    checked={isCustomer}
                    onChange={(e) => {
                      setIsCustomer(e.target.checked);
                      if (createError) setCreateError(null);
                    }}
                    className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                  />
                  <span>{t.partners.roleCustomerCheck}</span>
                </label>
              </div>
            </fieldset>

            <div className="sm:col-span-4">
              <label htmlFor="new-partner-npwp" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.partners.npwpLabel}
              </label>
              <input
                id="new-partner-npwp"
                type="text"
                value={npwp}
                onChange={(e) => setNpwp(e.target.value)}
                placeholder={t.partners.npwpPlaceholder}
                className="font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)]"
              />
            </div>

            <div className="sm:col-span-4">
              <label htmlFor="new-partner-pkp" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.partners.pkpLabel}
              </label>
              <select
                id="new-partner-pkp"
                value={pkpStatus}
                onChange={(e) => setPkpStatus(e.target.value)}
                className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                <option value="UNKNOWN">{t.partners.pkpLabels.UNKNOWN}</option>
                <option value="PKP">{t.partners.pkpLabels.PKP}</option>
                <option value="NON_PKP">{t.partners.pkpLabels.NON_PKP}</option>
              </select>
            </div>

            <div className="sm:col-span-4">
              <label htmlFor="new-partner-scope" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.partners.scopeModeLabel}
              </label>
              <select
                id="new-partner-scope"
                value={scopeMode}
                onChange={(e) => setScopeMode(e.target.value)}
                className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                <option value="ALL_ENTITIES">{t.partners.scopeAllOption}</option>
                <option value="RESTRICTED_ENTITIES">{t.partners.scopeRestrictedOption}</option>
              </select>
            </div>

            {createError ? (
              <p id="partner-create-error" role="alert" className="text-xs text-[var(--status-danger-text)] sm:col-span-12">
                {createError.message}
              </p>
            ) : null}

            <div className="sm:col-span-12">
              <button
                type="submit"
                disabled={createBusy}
                className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                {createBusy ? t.partners.createBusy : t.partners.createButton}
              </button>
            </div>
          </form>
        </section>
      ) : null}

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <div className="flex items-center gap-2">
            <Handshake className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.partners.listCardTitle}</h2>
          </div>
          <span className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
            {Number(partnersData.totalCount)}
          </span>
        </div>

        {partnersData.items.length === 0 ? (
          <p className="p-6 text-sm text-[var(--text-secondary)]">{t.partners.emptyList}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="px-4 py-2.5">{t.partners.colCodeName}</th>
                  <th scope="col" className="px-4 py-2.5">{t.partners.colRoles}</th>
                  <th scope="col" className="px-4 py-2.5">{t.partners.colScopeTax}</th>
                  <th scope="col" className="px-4 py-2.5">{t.partners.colEligibility}</th>
                  <th scope="col" className="px-4 py-2.5 text-right">{t.partners.colStatusAction}</th>
                </tr>
              </thead>
              <tbody>
                {partnersData.items.map((p) => {
                  const isActive = p.status === "ACTIVE";
                  const isSelected = selectedId === p.partnerId;
                  const roleLabel =
                    p.isSupplier && p.isCustomer
                      ? t.partners.roleBothBadge
                      : p.isSupplier
                        ? t.partners.roleSupplierBadge
                        : t.partners.roleCustomerBadge;
                  const scopeBadge =
                    p.entityScopeMode === "ALL_ENTITIES"
                      ? "border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)]"
                      : "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]";

                  return (
                    <tr
                      key={p.partnerId}
                      className={`block border-b border-[var(--border-hairline)] p-4 last:border-b-0 md:table-row md:p-0 ${
                        isSelected ? "bg-[var(--bg-subtle)]" : ""
                      }`}
                    >
                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <div className="font-code text-xs font-semibold tabular-nums text-[var(--text-primary)]">
                          {p.partnerCode}
                        </div>
                        <div className="mt-0.5 font-medium text-[var(--text-primary)]">{p.legalName}</div>
                        <div className="text-xs text-[var(--text-secondary)]">{p.displayName}</div>
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.partners.colRoles}:
                        </span>
                        <span className="inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-semibold text-[var(--text-primary)]">
                          {roleLabel}
                        </span>
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.partners.colScopeTax}:
                        </span>
                        <span className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-medium ${scopeBadge}`}>
                          {p.entityScopeMode === "RESTRICTED_ENTITIES" ? (
                            <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          ) : (
                            <Building2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          )}
                          <span>{t.partners.scopeLabels[p.entityScopeMode] ?? p.entityScopeMode}</span>
                        </span>
                        <div className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-0.5 text-xs text-[var(--text-secondary)]">
                          <span>{t.partners.pkpLabels[p.taxPkpStatus] ?? p.taxPkpStatus}</span>
                          {p.taxRegistrationNpwp ? (
                            <span className="font-code whitespace-nowrap tabular-nums text-[var(--text-primary)]">
                              {p.taxRegistrationNpwp}
                            </span>
                          ) : null}
                        </div>
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.partners.colEligibility}:
                        </span>
                        <div className="flex flex-wrap gap-1.5">
                          {p.effectiveSupplierEligible ? (
                            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                              <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              <span>{t.partners.supplierEligibleTag}</span>
                            </span>
                          ) : null}
                          {p.effectiveCustomerEligible ? (
                            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                              <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              <span>{t.partners.customerEligibleTag}</span>
                            </span>
                          ) : null}
                          {!p.effectiveSupplierEligible && !p.effectiveCustomerEligible ? (
                            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-medium text-[var(--text-muted)]">
                              <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              <span>{t.partners.ineligibleTag}</span>
                            </span>
                          ) : null}
                        </div>
                      </td>

                      <td className="block md:table-cell md:px-4 md:py-3 md:text-right md:align-top">
                        <div className="flex flex-wrap items-center gap-2 md:justify-end">
                          <span
                            className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-semibold ${
                              isActive
                                ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                                : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
                            }`}
                          >
                            {isActive ? (
                              <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            ) : (
                              <AlertCircle className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            )}
                            <span>{t.partners.statusLabels[p.status] ?? p.status}</span>
                          </span>
                          <button
                            type="button"
                            aria-label={`${canManage ? t.partners.inspectButton : t.partners.viewDetailsButton} (${p.partnerCode} — ${p.legalName})`}
                            onClick={() => setSelectedId(p.partnerId)}
                            className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                          >
                            {canManage ? t.partners.inspectButton : t.partners.viewDetailsButton}
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {selectedPartner ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <div>
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">
                {t.partners.detailCardTitle} —{" "}
                <span className="font-code">{selectedPartner.partnerCode}</span> ({selectedPartner.legalName})
              </h2>
              <p className="font-code mt-0.5 text-[11px] tabular-nums text-[var(--text-secondary)]">
                {formatJakartaDateTime(selectedPartner.updatedAtUtc, locale)}
              </p>
            </div>
            {canManage ? (
              <div className="flex flex-wrap items-center gap-2">
                <select
                  aria-label={t.partners.updateScopeLabel}
                  value={updateScopeValue}
                  disabled={statusBusy}
                  onChange={(e) => setUpdateScopeValue(e.target.value)}
                  className="h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="ALL_ENTITIES">{t.partners.scopeAllOption}</option>
                  <option value="RESTRICTED_ENTITIES">{t.partners.scopeRestrictedOption}</option>
                </select>
                <button
                  type="button"
                  disabled={statusBusy || updateScopeValue === selectedPartner.entityScopeMode}
                  onClick={() => handleUpdatePartnerMeta(undefined, updateScopeValue)}
                  className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-50"
                >
                  {t.partners.updateScopeButton}
                </button>
                <button
                  type="button"
                  disabled={statusBusy}
                  onClick={() =>
                    handleUpdatePartnerMeta(selectedPartner.status === "ACTIVE" ? "INACTIVE" : "ACTIVE")
                  }
                  className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                >
                  {selectedPartner.status === "ACTIVE"
                    ? t.partners.deactivateButton
                    : t.partners.activateButton}
                </button>
              </div>
            ) : null}
          </div>

          <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] lg:grid-cols-2 lg:divide-x lg:divide-y-0">
            <div className="space-y-4 p-4">
              <h3 className="text-xs font-semibold text-[var(--text-primary)]">{t.partners.contactsTitle}</h3>
              {selectedPartner.contacts.length === 0 ? (
                <p className="text-xs text-[var(--text-secondary)]">{t.partners.emptyContacts}</p>
              ) : (
                <ul className="space-y-2">
                  {selectedPartner.contacts.map((c) => (
                    <li
                      key={c.contactId}
                      className="rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3 py-2 text-xs"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="font-semibold text-[var(--text-primary)]">{c.contactName}</span>
                        <span className="rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--text-secondary)]">
                          {t.partners.contactRoleLabels[c.contactRole] ?? c.contactRole}
                        </span>
                      </div>
                      {c.email || c.phone ? (
                        <div className="font-code mt-1 text-[11px] text-[var(--text-secondary)]">
                          {[c.email, c.phone].filter(Boolean).join(" · ")}
                        </div>
                      ) : null}
                    </li>
                  ))}
                </ul>
              )}

              {canManage ? (
                <form onSubmit={handleAddContact} noValidate className="space-y-3 border-t border-[var(--border-hairline)] pt-3">
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <label htmlFor="contact-name" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.partners.contactNameLabel}
                      </label>
                      <input
                        id="contact-name"
                        type="text"
                        value={contactName}
                        onChange={(e) => {
                          setContactName(e.target.value);
                          if (contactError) setContactError(null);
                        }}
                        placeholder={t.partners.contactNamePlaceholder}
                        aria-invalid={Boolean(contactError)}
                        aria-describedby={contactError ? "contact-form-error" : undefined}
                        className={`mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)] ${
                          contactError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div>
                      <label htmlFor="contact-role" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.partners.contactRoleLabel}
                      </label>
                      <select
                        id="contact-role"
                        value={contactRole}
                        onChange={(e) => setContactRole(e.target.value)}
                        className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-sm text-[var(--text-primary)]"
                      >
                        <option value="COMMERCIAL">{t.partners.contactRoleLabels.COMMERCIAL}</option>
                        <option value="BILLING">{t.partners.contactRoleLabels.BILLING}</option>
                        <option value="RECEIVING_DISPATCH">{t.partners.contactRoleLabels.RECEIVING_DISPATCH}</option>
                      </select>
                    </div>
                    <div>
                      <label htmlFor="contact-email" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.partners.contactEmailLabel}
                      </label>
                      <input
                        id="contact-email"
                        type="email"
                        value={contactEmail}
                        onChange={(e) => setContactEmail(e.target.value)}
                        placeholder={t.partners.contactEmailPlaceholder}
                        className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                      />
                    </div>
                    <div>
                      <label htmlFor="contact-phone" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.partners.contactPhoneLabel}
                      </label>
                      <input
                        id="contact-phone"
                        type="text"
                        value={contactPhone}
                        onChange={(e) => setContactPhone(e.target.value)}
                        placeholder={t.partners.contactPhonePlaceholder}
                        className="font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                      />
                    </div>
                  </div>
                  <label className="inline-flex min-h-9 cursor-pointer items-center gap-2 text-xs text-[var(--text-primary)]">
                    <input
                      type="checkbox"
                      checked={contactPrimary}
                      onChange={(e) => setContactPrimary(e.target.checked)}
                      className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                    />
                    <span>{t.partners.contactPrimaryLabel}</span>
                  </label>
                  {contactError ? (
                    <p id="contact-form-error" role="alert" className="text-xs text-[var(--status-danger-text)]">
                      {contactError}
                    </p>
                  ) : null}
                  <div>
                    <button
                      type="submit"
                      disabled={contactBusy}
                      className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                    >
                      <UserPlus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                      <span>{contactBusy ? t.partners.addContactBusy : t.partners.addContactButton}</span>
                    </button>
                  </div>
                </form>
              ) : null}
            </div>

            <div className="space-y-4 p-4">
              <h3 className="text-xs font-semibold text-[var(--text-primary)]">
                {t.partners.profileTitle} (<span className="font-code">{detail.entity.code}</span>)
              </h3>
              {canManage ? (
                <form onSubmit={handleSaveProfile} noValidate className="space-y-3">
                  {selectedPartner.isSupplier ? (
                    <label className="flex min-h-9 cursor-pointer items-center gap-2 text-sm text-[var(--text-primary)]">
                      <input
                        type="checkbox"
                        checked={profSupplier}
                        onChange={(e) => setProfSupplier(e.target.checked)}
                        className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                      />
                      <span>{t.partners.profileSupplierCheck}</span>
                    </label>
                  ) : null}
                  {selectedPartner.isCustomer ? (
                    <label className="flex min-h-9 cursor-pointer items-center gap-2 text-sm text-[var(--text-primary)]">
                      <input
                        type="checkbox"
                        checked={profCustomer}
                        onChange={(e) => setProfCustomer(e.target.checked)}
                        className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                      />
                      <span>{t.partners.profileCustomerCheck}</span>
                    </label>
                  ) : null}

                  <div className="space-y-3">
                    <div>
                      <label htmlFor="profile-terms" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.partners.profileTermsLabel}
                      </label>
                      <input
                        id="profile-terms"
                        type="number"
                        min={0}
                        max={365}
                        step={1}
                        value={Number.isNaN(profTerms) ? "" : profTerms}
                        onChange={(e) => {
                          setProfTerms(e.target.value === "" ? Number.NaN : Number(e.target.value));
                          if (profileError) setProfileError(null);
                        }}
                        aria-invalid={profileError?.field === "terms"}
                        aria-describedby={profileError ? "profile-form-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm tabular-nums text-[var(--text-primary)] sm:max-w-[200px] ${
                          profileError?.field === "terms" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div>
                      <label htmlFor="profile-notes" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.partners.profileNotesLabel}
                      </label>
                      <input
                        id="profile-notes"
                        type="text"
                        value={profNotes}
                        onChange={(e) => setProfNotes(e.target.value)}
                        placeholder={t.partners.profileNotesPlaceholder}
                        className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                      />
                    </div>
                  </div>

                  {profileError ? (
                    <p id="profile-form-error" role="alert" className="text-xs text-[var(--status-danger-text)]">
                      {profileError.message}
                    </p>
                  ) : null}

                  <button
                    type="submit"
                    disabled={profBusy}
                    className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    {profBusy ? t.partners.saveProfileBusy : t.partners.saveProfileButton}
                  </button>
                </form>
              ) : (
                <div className="space-y-3 text-xs text-[var(--text-secondary)]">
                  <div>
                    {t.partners.scopeModeLabel}:{" "}
                    <span className="font-semibold text-[var(--text-primary)]">
                      {t.partners.scopeLabels[selectedPartner.entityScopeMode] ?? selectedPartner.entityScopeMode}
                    </span>
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {selectedPartner.effectiveSupplierEligible ? (
                      <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                        <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.partners.supplierEligibleTag}</span>
                      </span>
                    ) : null}
                    {selectedPartner.effectiveCustomerEligible ? (
                      <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                        <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.partners.customerEligibleTag}</span>
                      </span>
                    ) : null}
                    {!selectedPartner.effectiveSupplierEligible && !selectedPartner.effectiveCustomerEligible ? (
                      <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-medium text-[var(--text-muted)]">
                        <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.partners.ineligibleTag}</span>
                      </span>
                    ) : null}
                  </div>
                  {selectedPartner.currentEntityProfile ? (
                    <div>
                      {t.partners.profileTermsLabel}:{" "}
                      <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                        {Number(selectedPartner.currentEntityProfile.defaultPaymentTermsDays)}
                      </span>
                    </div>
                  ) : null}
                  {selectedPartner.currentEntityProfile?.notes ? (
                    <p>{selectedPartner.currentEntityProfile.notes}</p>
                  ) : null}
                </div>
              )}
            </div>
          </div>
        </section>
      ) : null}
    </div>
  );
}

export default function PartnersPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <PartnersSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
