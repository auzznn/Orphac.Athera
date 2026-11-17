"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  ArrowRightLeft,
  Building2,
  CheckCircle2,
  Clock,
  Lock,
  Plus,
  Scale,
  Search,
  Tags,
} from "lucide-react";
import { ScopedBoundaryError, ScopedLoadingSheet } from "@/components/ScopedBoundaryState";
import {
  ApiProblemError,
  ConvertQuantityPreviewResponse,
  createCatalogItem,
  createUnitOfMeasure,
  EntityWorkspaceDetailResponse,
  fetchCatalogItemDetail,
  fetchCatalogItems,
  fetchEntityWorkspaceDetail,
  fetchUnitsOfMeasure,
  ItemDetailDto,
  PagedItemsResponse,
  previewItemUomConversion,
  UomListResponse,
  updateCatalogItem,
  upsertItemEntityPolicy,
  upsertItemUomConversion,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatJakartaDateTime } from "@/lib/i18n/messages";

function CatalogSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const { locale, t } = useLocale();

  const [detail, setDetail] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [uomsData, setUomsData] = useState<UomListResponse | null>(null);
  const [itemsData, setItemsData] = useState<PagedItemsResponse | null>(null);
  const [selectedItem, setSelectedItem] = useState<ItemDetailDto | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loading, setLoading] = useState(true);
  const [reloadNonce, setReloadNonce] = useState(0);
  const [actionError, setActionError] = useState<string | null>(null);

  const [searchText, setSearchText] = useState("");
  const [kindFilter, setKindFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [eligibleOnly, setEligibleOnly] = useState(false);

  const [uomCode, setUomCode] = useState("");
  const [uomName, setUomName] = useState("");
  const [uomScale, setUomScale] = useState(0);
  const [uomError, setUomError] = useState<{
    field: "code" | "name" | "form";
    message: string;
  } | null>(null);
  const [uomBusy, setUomBusy] = useState(false);

  const [itemCode, setItemCode] = useState("");
  const [itemName, setItemName] = useState("");
  const [itemKind, setItemKind] = useState("STOCKED_GOOD");
  const [baseUomId, setBaseUomId] = useState("");
  const [purchaseUomId, setPurchaseUomId] = useState("");
  const [salesUomId, setSalesUomId] = useState("");
  const [itemScopeMode, setItemScopeMode] = useState("ALL_ENTITIES");
  const [itemCreateError, setItemCreateError] = useState<{
    field: "code" | "name" | "form";
    message: string;
  } | null>(null);
  const [itemCreateBusy, setItemCreateBusy] = useState(false);

  const [convUomId, setConvUomId] = useState("");
  const [convFactor, setConvFactor] = useState("24");
  const [convError, setConvError] = useState<string | null>(null);
  const [convBusy, setConvBusy] = useState(false);

  const [previewUomId, setPreviewUomId] = useState("");
  const [previewQty, setPreviewQty] = useState("3");
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewBusy, setPreviewBusy] = useState(false);
  const [previewResult, setPreviewResult] = useState<ConvertQuantityPreviewResponse | null>(null);

  const [policyPurchase, setPolicyPurchase] = useState(true);
  const [policySales, setPolicySales] = useState(true);
  const [policyNotes, setPolicyNotes] = useState("");
  const [policyBusy, setPolicyBusy] = useState(false);
  const [policyError, setPolicyError] = useState<string | null>(null);
  const [editScopeValue, setEditScopeValue] = useState("ALL_ENTITIES");
  const [statusBusy, setStatusBusy] = useState(false);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchUnitsOfMeasure(tenantId, entityId),
      fetchCatalogItems(tenantId, entityId, {
        q: searchText.trim() || undefined,
        itemKind: kindFilter || undefined,
        status: statusFilter || undefined,
        eligibleOnly: eligibleOnly || undefined,
        page: 1,
        pageSize: 50,
      }),
    ])
      .then(([entityDetail, uoms, pagedItems]) => {
        if (!active) return;
        setDetail(entityDetail);
        setUomsData(uoms);
        setItemsData(pagedItems);
        setProblem(null);

        const activeUoms = uoms.items;
        if (activeUoms.length > 0) {
          const pcs = activeUoms.find((u) => u.uomCode === "PCS") ?? activeUoms[0]!;
          const box = activeUoms.find((u) => u.uomCode === "BOX") ?? pcs;
          setBaseUomId((prev) => prev || pcs.uomId);
          setPurchaseUomId((prev) => prev || box.uomId);
          setSalesUomId((prev) => prev || pcs.uomId);
        }
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
  }, [tenantId, entityId, searchText, kindFilter, statusFilter, eligibleOnly, reloadNonce]);

  useEffect(() => {
    if (!selectedId) {
      return;
    }

    let active = true;
    fetchCatalogItemDetail(tenantId, entityId, selectedId)
      .then((itemDetail) => {
        if (!active) return;
        setSelectedItem(itemDetail);
        setEditScopeValue(itemDetail.entityScopeMode);
        setPolicyPurchase(itemDetail.currentEntityPolicy?.purchaseAllowed ?? itemDetail.effectivePurchaseAllowed);
        setPolicySales(itemDetail.currentEntityPolicy?.salesAllowed ?? itemDetail.effectiveSalesAllowed);
        setPolicyNotes(itemDetail.currentEntityPolicy?.notes ?? "");
        const preferred = itemDetail.conversions[0]?.uomId ?? itemDetail.defaultPurchaseUomId;
        setPreviewUomId(preferred);
        setPreviewResult(null);
        setPreviewError(null);
        setConvError(null);
        setPolicyError(null);
      })
      .catch((err) => {
        if (!active) return;
        setActionError((err as ApiProblemError).detail ?? t.login.errors.unknown);
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, selectedId, t.login.errors.unknown]);

  async function handleCreateUom(e: React.FormEvent) {
    e.preventDefault();
    if (!uomCode.trim()) {
      setUomError({ field: "code", message: t.catalog.validation.uomCodeRequired });
      return;
    }
    if (!uomName.trim()) {
      setUomError({ field: "name", message: t.catalog.validation.uomNameRequired });
      return;
    }

    setUomBusy(true);
    setUomError(null);
    setActionError(null);
    try {
      await createUnitOfMeasure(tenantId, entityId, {
        uomCode: uomCode.trim(),
        name: uomName.trim(),
        quantityPrecisionScale: uomScale,
      });
      setUomCode("");
      setUomName("");
      setUomScale(0);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setUomError({
        field: apiErr.errorCode === "UOM_CODE_CONFLICT" ? "code" : "form",
        message: t.catalog.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown,
      });
    } finally {
      setUomBusy(false);
    }
  }

  async function handleCreateItem(e: React.FormEvent) {
    e.preventDefault();
    if (itemCode.trim().length < 2) {
      setItemCreateError({ field: "code", message: t.catalog.validation.itemCodeRequired });
      return;
    }
    if (!itemName.trim()) {
      setItemCreateError({ field: "name", message: t.catalog.validation.itemNameRequired });
      return;
    }

    setItemCreateBusy(true);
    setItemCreateError(null);
    setActionError(null);
    try {
      const created = await createCatalogItem(tenantId, entityId, {
        itemCode: itemCode.trim(),
        name: itemName.trim(),
        itemKind,
        baseUomId,
        defaultPurchaseUomId: purchaseUomId || baseUomId,
        defaultSalesUomId: salesUomId || baseUomId,
        entityScopeMode: itemScopeMode,
      });
      setItemCode("");
      setItemName("");
      setSelectedId(created.itemId);
      setSelectedItem(created);
      setPolicyPurchase(created.currentEntityPolicy?.purchaseAllowed ?? created.effectivePurchaseAllowed);
      setPolicySales(created.currentEntityPolicy?.salesAllowed ?? created.effectiveSalesAllowed);
      setPolicyNotes(created.currentEntityPolicy?.notes ?? "");
      setConvError(null);
      setPolicyError(null);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setItemCreateError({
        field: apiErr.errorCode === "ITEM_CODE_CONFLICT" ? "code" : "form",
        message: t.catalog.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown,
      });
    } finally {
      setItemCreateBusy(false);
    }
  }

  async function handleAddConversion(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedItem || !uomsData) return;
    const altUoms = uomsData.items.filter((u) => u.uomId !== selectedItem.baseUomId);
    const chosenUomId = altUoms.some((u) => u.uomId === convUomId) ? convUomId : (altUoms[0]?.uomId ?? "");
    const parsedFactor = Number(convFactor);
    if (!chosenUomId || Number.isNaN(parsedFactor) || parsedFactor <= 0) {
      setConvError(t.catalog.validation.factorInvalid);
      return;
    }

    setConvBusy(true);
    setConvError(null);
    setActionError(null);
    try {
      const updated = await upsertItemUomConversion(tenantId, entityId, selectedItem.itemId, {
        uomId: chosenUomId,
        factorToBase: parsedFactor,
        status: "ACTIVE",
      });
      setSelectedItem(updated);
      setPreviewUomId(chosenUomId);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setConvError(t.catalog.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setConvBusy(false);
    }
  }

  async function handlePreviewConversion(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedItem) return;
    const chosenUomId = previewUomId || selectedItem.defaultPurchaseUomId;
    const parsedQty = Number(previewQty);
    if (Number.isNaN(parsedQty) || parsedQty <= 0) {
      setPreviewError(t.catalog.validation.quantityInvalid);
      return;
    }

    setPreviewBusy(true);
    setPreviewError(null);
    setActionError(null);
    try {
      const res = await previewItemUomConversion(tenantId, entityId, selectedItem.itemId, {
        sourceUomId: chosenUomId,
        quantity: parsedQty,
      });
      setPreviewResult(res);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setPreviewResult(null);
      setPreviewError(t.catalog.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setPreviewBusy(false);
    }
  }

  async function handleSavePolicy(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedItem) return;
    setPolicyBusy(true);
    setPolicyError(null);
    setActionError(null);
    try {
      const updated = await upsertItemEntityPolicy(tenantId, entityId, selectedItem.itemId, {
        purchaseAllowed: policyPurchase,
        salesAllowed: policySales,
        notes: policyNotes.trim() || null,
      });
      setSelectedItem(updated);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setPolicyError(t.catalog.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setPolicyBusy(false);
    }
  }

  async function handleUpdateItemMeta(nextStatus?: string, nextScope?: string) {
    if (!selectedItem) return;
    setStatusBusy(true);
    setPolicyError(null);
    setActionError(null);
    try {
      const updated = await updateCatalogItem(tenantId, entityId, selectedItem.itemId, {
        name: selectedItem.name,
        itemKind: selectedItem.itemKind,
        defaultPurchaseUomId: selectedItem.defaultPurchaseUomId,
        defaultSalesUomId: selectedItem.defaultSalesUomId,
        entityScopeMode: nextScope ?? selectedItem.entityScopeMode,
        status: nextStatus ?? selectedItem.status,
        expectedVersion: selectedItem.version,
      });
      setSelectedItem(updated);
      setEditScopeValue(updated.entityScopeMode);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setPolicyError(t.catalog.errors[apiErr.errorCode] ?? apiErr.detail ?? t.login.errors.unknown);
    } finally {
      setStatusBusy(false);
    }
  }

  if (loading && !itemsData) {
    return <ScopedLoadingSheet />;
  }

  if (problem || !detail || !uomsData || !itemsData) {
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

  const canManage = detail.effectivePermissions.includes("catalog.manage");
  const nonBaseUoms = selectedItem
    ? uomsData.items.filter((u) => u.uomId !== selectedItem.baseUomId)
    : [];
  const selectedConvUom = nonBaseUoms.some((u) => u.uomId === convUomId)
    ? convUomId
    : (nonBaseUoms[0]?.uomId ?? "");

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">{t.catalog.heading}</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
              {detail.tenant.code} · {detail.entity.code}
            </span>
            {" — "}
            <span>{t.catalog.lead}</span>
          </p>
        </div>

        <Link
          href={`/w/${tenantId}/${entityId}`}
          className="inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
          <span>
            {t.catalog.backToEntity} · <span className="font-code">{detail.entity.code}</span>
          </span>
        </Link>
      </div>

      {!canManage ? (
        <div className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-4 py-3 text-xs text-[var(--status-warn-text)]">
          <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.catalog.readOnlyNotice}</span>
        </div>
      ) : null}

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
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <div className="flex items-center gap-2">
            <Scale className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.catalog.uomCardTitle}</h2>
          </div>
          <span className="text-xs text-[var(--text-secondary)]">{t.catalog.scaleDiscreteHint}</span>
        </div>

        <div className="flex flex-wrap gap-2 border-b border-[var(--border-hairline)] px-4 py-3">
          {uomsData.items.map((u) => (
            <span
              key={u.uomId}
              className="inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2.5 py-1 text-xs text-[var(--text-primary)]"
            >
              <span className="font-code font-semibold">{u.uomCode}</span>
              <span className="text-[var(--text-secondary)]">({u.name})</span>
              <span className="rounded bg-[var(--bg-surface)] px-1.5 py-0.5 text-[11px] text-[var(--text-secondary)]">
                {t.catalog.uomScaleLabel}:{" "}
                <span className="font-code font-semibold tabular-nums">{Number(u.quantityPrecisionScale)}</span>
              </span>
            </span>
          ))}
        </div>

        {canManage ? (
          <form onSubmit={handleCreateUom} noValidate className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-12">
            <div className="sm:col-span-3">
              <label htmlFor="new-uom-code" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.uomCodeLabel}
              </label>
              <input
                id="new-uom-code"
                type="text"
                value={uomCode}
                onChange={(e) => {
                  setUomCode(e.target.value);
                  if (uomError) setUomError(null);
                }}
                placeholder={t.catalog.uomCodePlaceholder}
                aria-invalid={uomError?.field === "code"}
                aria-describedby={uomError?.field === "code" ? "uom-create-error" : undefined}
                className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm uppercase text-[var(--text-primary)] ${
                  uomError?.field === "code" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                }`}
              />
            </div>
            <div className="sm:col-span-4">
              <label htmlFor="new-uom-name" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.uomNameLabel}
              </label>
              <input
                id="new-uom-name"
                type="text"
                value={uomName}
                onChange={(e) => {
                  setUomName(e.target.value);
                  if (uomError) setUomError(null);
                }}
                placeholder={t.catalog.uomNamePlaceholder}
                aria-invalid={uomError?.field === "name"}
                aria-describedby={uomError?.field === "name" ? "uom-create-error" : undefined}
                className={`mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)] ${
                  uomError?.field === "name" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                }`}
              />
            </div>
            <div className="sm:col-span-2">
              <label htmlFor="new-uom-scale" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.uomScaleLabel}
              </label>
              <select
                id="new-uom-scale"
                value={uomScale}
                onChange={(e) => setUomScale(Number(e.target.value))}
                className="font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-sm tabular-nums text-[var(--text-primary)]"
              >
                <option value={0}>0</option>
                <option value={2}>2</option>
                <option value={3}>3</option>
                <option value={6}>6</option>
              </select>
            </div>
            <div className="flex items-end sm:col-span-3">
              <button
                type="submit"
                disabled={uomBusy}
                className="inline-flex min-h-9 w-full items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                {uomBusy ? t.catalog.createUomBusy : t.catalog.createUomButton}
              </button>
            </div>
            {uomError ? (
              <p id="uom-create-error" role="alert" className="text-xs text-[var(--status-danger-text)] sm:col-span-12">
                {uomError.message}
              </p>
            ) : null}
          </form>
        ) : null}
      </section>

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <Search className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.catalog.filterCardTitle}</h2>
        </div>

        <div className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-12">
          <div className="sm:col-span-5">
            <label htmlFor="item-search" className="block text-xs font-semibold text-[var(--text-secondary)]">
              {t.catalog.searchLabel}
            </label>
            <input
              id="item-search"
              type="text"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={t.catalog.searchPlaceholder}
              className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
            />
          </div>

          <div className="sm:col-span-3">
            <label htmlFor="item-kind-filter" className="block text-xs font-semibold text-[var(--text-secondary)]">
              {t.catalog.kindFilterLabel}
            </label>
            <select
              id="item-kind-filter"
              value={kindFilter}
              onChange={(e) => setKindFilter(e.target.value)}
              className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
            >
              <option value="">{t.catalog.kindFilterAll}</option>
              <option value="STOCKED_GOOD">{t.catalog.kindLabels.STOCKED_GOOD}</option>
              <option value="NON_STOCK_SERVICE">{t.catalog.kindLabels.NON_STOCK_SERVICE}</option>
            </select>
          </div>

          <div className="sm:col-span-2">
            <label htmlFor="item-status-filter" className="block text-xs font-semibold text-[var(--text-secondary)]">
              {t.catalog.statusFilterLabel}
            </label>
            <select
              id="item-status-filter"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
            >
              <option value="">{t.catalog.statusFilterAll}</option>
              <option value="ACTIVE">{t.catalog.statusLabels.ACTIVE}</option>
              <option value="INACTIVE">{t.catalog.statusLabels.INACTIVE}</option>
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
              <span>{t.catalog.eligibleOnlyLabel}</span>
            </label>
          </div>
        </div>
      </section>

      {canManage ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
          <div className="flex items-center gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <Plus className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.catalog.createItemCardTitle}</h2>
          </div>

          <form onSubmit={handleCreateItem} noValidate className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-12">
            <div className="sm:col-span-3">
              <label htmlFor="new-item-code" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.itemCodeLabel}
              </label>
              <input
                id="new-item-code"
                type="text"
                value={itemCode}
                onChange={(e) => {
                  setItemCode(e.target.value);
                  if (itemCreateError) setItemCreateError(null);
                }}
                placeholder={t.catalog.itemCodePlaceholder}
                aria-invalid={itemCreateError?.field === "code"}
                aria-describedby={itemCreateError?.field === "code" ? "item-create-error" : undefined}
                className={`font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm uppercase text-[var(--text-primary)] ${
                  itemCreateError?.field === "code"
                    ? "border-[var(--status-danger-text)]"
                    : "border-[var(--border-strong)]"
                }`}
              />
            </div>

            <div className="sm:col-span-6">
              <label htmlFor="new-item-name" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.itemNameLabel}
              </label>
              <input
                id="new-item-name"
                type="text"
                value={itemName}
                onChange={(e) => {
                  setItemName(e.target.value);
                  if (itemCreateError) setItemCreateError(null);
                }}
                placeholder={t.catalog.itemNamePlaceholder}
                aria-invalid={itemCreateError?.field === "name"}
                aria-describedby={itemCreateError?.field === "name" ? "item-create-error" : undefined}
                className={`mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                  itemCreateError?.field === "name"
                    ? "border-[var(--status-danger-text)]"
                    : "border-[var(--border-strong)]"
                }`}
              />
            </div>

            <div className="sm:col-span-3">
              <label htmlFor="new-item-kind" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.itemKindLabel}
              </label>
              <select
                id="new-item-kind"
                value={itemKind}
                onChange={(e) => setItemKind(e.target.value)}
                className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                <option value="STOCKED_GOOD">{t.catalog.kindLabels.STOCKED_GOOD}</option>
                <option value="NON_STOCK_SERVICE">{t.catalog.kindLabels.NON_STOCK_SERVICE}</option>
              </select>
            </div>

            <div className="sm:col-span-3">
              <label htmlFor="new-item-base-uom" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.baseUomLabel}
              </label>
              <select
                id="new-item-base-uom"
                value={baseUomId}
                onChange={(e) => setBaseUomId(e.target.value)}
                className="font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                {uomsData.items.map((u) => (
                  <option key={u.uomId} value={u.uomId}>
                    {u.uomCode}
                  </option>
                ))}
              </select>
            </div>

            <div className="sm:col-span-3">
              <label htmlFor="new-item-purchase-uom" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.purchaseUomLabel}
              </label>
              <select
                id="new-item-purchase-uom"
                value={purchaseUomId}
                onChange={(e) => setPurchaseUomId(e.target.value)}
                className="font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                {uomsData.items.map((u) => (
                  <option key={u.uomId} value={u.uomId}>
                    {u.uomCode}
                  </option>
                ))}
              </select>
            </div>

            <div className="sm:col-span-3">
              <label htmlFor="new-item-sales-uom" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.salesUomLabel}
              </label>
              <select
                id="new-item-sales-uom"
                value={salesUomId}
                onChange={(e) => setSalesUomId(e.target.value)}
                className="font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                {uomsData.items.map((u) => (
                  <option key={u.uomId} value={u.uomId}>
                    {u.uomCode}
                  </option>
                ))}
              </select>
            </div>

            <div className="sm:col-span-3">
              <label htmlFor="new-item-scope" className="block text-xs font-semibold text-[var(--text-secondary)]">
                {t.catalog.scopeModeLabel}
              </label>
              <select
                id="new-item-scope"
                value={itemScopeMode}
                onChange={(e) => setItemScopeMode(e.target.value)}
                className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
              >
                <option value="ALL_ENTITIES">{t.catalog.scopeLabels.ALL_ENTITIES}</option>
                <option value="RESTRICTED_ENTITIES">{t.catalog.scopeLabels.RESTRICTED_ENTITIES}</option>
              </select>
            </div>

            {itemCreateError ? (
              <p id="item-create-error" role="alert" className="text-xs text-[var(--status-danger-text)] sm:col-span-12">
                {itemCreateError.message}
              </p>
            ) : null}

            <div className="sm:col-span-12">
              <button
                type="submit"
                disabled={itemCreateBusy}
                className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                {itemCreateBusy ? t.catalog.createItemBusy : t.catalog.createItemButton}
              </button>
            </div>
          </form>
        </section>
      ) : null}

      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
        <div className="flex items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
          <div className="flex items-center gap-2">
            <Tags className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.catalog.listCardTitle}</h2>
          </div>
          <span className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
            {Number(itemsData.totalCount)}
          </span>
        </div>

        {itemsData.items.length === 0 ? (
          <p className="p-6 text-sm text-[var(--text-secondary)]">{t.catalog.emptyList}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                <tr>
                  <th scope="col" className="px-4 py-2.5">{t.catalog.colItem}</th>
                  <th scope="col" className="px-4 py-2.5">{t.catalog.colKind}</th>
                  <th scope="col" className="px-4 py-2.5">{t.catalog.colUoms}</th>
                  <th scope="col" className="px-4 py-2.5">{t.catalog.colEligibility}</th>
                  <th scope="col" className="px-4 py-2.5 text-right">{t.catalog.colAction}</th>
                </tr>
              </thead>
              <tbody>
                {itemsData.items.map((item) => {
                  const isActive = item.status === "ACTIVE";
                  const isSelected = selectedId === item.itemId;
                  return (
                    <tr
                      key={item.itemId}
                      className={`block border-b border-[var(--border-hairline)] p-4 last:border-b-0 md:table-row md:p-0 ${
                        isSelected ? "bg-[var(--bg-subtle)]" : ""
                      }`}
                    >
                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <div className="font-code text-xs font-semibold tabular-nums text-[var(--text-primary)]">
                          {item.itemCode}
                        </div>
                        <div className="mt-0.5 font-medium text-[var(--text-primary)]">{item.name}</div>
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.catalog.colKind}:
                        </span>
                        <span className="inline-flex items-center rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-medium text-[var(--text-primary)]">
                          {t.catalog.kindLabels[item.itemKind] ?? item.itemKind}
                        </span>
                        <div className="mt-1">
                          <span
                            className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-medium ${
                              item.entityScopeMode === "RESTRICTED_ENTITIES"
                                ? "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                                : "border-[var(--border-hairline)] bg-[var(--bg-subtle)] text-[var(--text-secondary)]"
                            }`}
                          >
                            {item.entityScopeMode === "RESTRICTED_ENTITIES" ? (
                              <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            ) : (
                              <Building2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                            )}
                            <span>{t.catalog.scopeLabels[item.entityScopeMode] ?? item.entityScopeMode}</span>
                          </span>
                        </div>
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.catalog.colUoms}:
                        </span>
                        <span className="font-code whitespace-nowrap text-xs font-semibold tabular-nums text-[var(--text-primary)]">
                          {item.baseUomCode} · {item.defaultPurchaseUomCode} · {item.defaultSalesUomCode}
                        </span>
                      </td>

                      <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                        <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                          {t.catalog.colEligibility}:
                        </span>
                        <div className="flex flex-wrap gap-1.5">
                          {item.effectivePurchaseAllowed ? (
                            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                              <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              <span>{t.catalog.purchaseEligibleTag}</span>
                            </span>
                          ) : null}
                          {item.effectiveSalesAllowed ? (
                            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                              <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              <span>{t.catalog.salesEligibleTag}</span>
                            </span>
                          ) : null}
                          {!item.effectivePurchaseAllowed && !item.effectiveSalesAllowed ? (
                            <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-medium text-[var(--text-muted)]">
                              <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              <span>{t.catalog.ineligibleTag}</span>
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
                            <span>{t.catalog.statusLabels[item.status] ?? item.status}</span>
                          </span>
                          <button
                            type="button"
                            aria-label={`${canManage ? t.catalog.inspectButton : t.catalog.viewItemButton} (${item.itemCode} — ${item.name})`}
                            onClick={() => setSelectedId(item.itemId)}
                            className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                          >
                            {canManage ? t.catalog.inspectButton : t.catalog.viewItemButton}
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

      {selectedItem ? (
        <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
            <div>
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">
                {t.catalog.detailCardTitle} —{" "}
                <span className="font-code">{selectedItem.itemCode}</span> ({selectedItem.name})
              </h2>
              <p className="font-code mt-0.5 text-[11px] tabular-nums text-[var(--text-secondary)]">
                {formatJakartaDateTime(selectedItem.updatedAtUtc, locale)}
              </p>
            </div>
            {canManage ? (
              <div className="flex flex-wrap items-center gap-2">
                <select
                  aria-label={t.catalog.scopeModeLabel}
                  value={editScopeValue}
                  disabled={statusBusy}
                  onChange={(e) => setEditScopeValue(e.target.value)}
                  className="h-9 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                >
                  <option value="ALL_ENTITIES">{t.catalog.scopeLabels.ALL_ENTITIES}</option>
                  <option value="RESTRICTED_ENTITIES">{t.catalog.scopeLabels.RESTRICTED_ENTITIES}</option>
                </select>
                <button
                  type="button"
                  disabled={statusBusy || editScopeValue === selectedItem.entityScopeMode}
                  onClick={() => handleUpdateItemMeta(undefined, editScopeValue)}
                  className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-50"
                >
                  {t.catalog.toggleScopeButton}
                </button>
                <button
                  type="button"
                  disabled={statusBusy}
                  onClick={() =>
                    handleUpdateItemMeta(selectedItem.status === "ACTIVE" ? "INACTIVE" : "ACTIVE")
                  }
                  className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                >
                  {selectedItem.status === "ACTIVE"
                    ? t.catalog.deactivateButton
                    : t.catalog.activateButton}
                </button>
              </div>
            ) : null}
          </div>

          <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] lg:grid-cols-2 lg:divide-x lg:divide-y-0">
            <div className="space-y-4 p-4">
              <div>
                <h3 className="text-xs font-semibold text-[var(--text-primary)]">{t.catalog.conversionsTitle}</h3>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">
                  {t.catalog.baseImplicitNote} (
                  <span className="font-code font-semibold">{selectedItem.baseUomCode}</span> = 1)
                </p>
              </div>

              {selectedItem.conversions.length === 0 ? (
                <p className="text-xs text-[var(--text-secondary)]">{t.catalog.emptyConversions}</p>
              ) : (
                <ul className="space-y-2">
                  {selectedItem.conversions.map((c) => (
                    <li
                      key={c.itemUomId}
                      className="flex items-center justify-between rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3 py-2 text-xs"
                    >
                      <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
                        1 {c.uomCode} = {Number(c.factorToBase)} {selectedItem.baseUomCode}
                      </span>
                      <span className="text-[var(--text-secondary)]">{c.uomName}</span>
                    </li>
                  ))}
                </ul>
              )}

              {canManage && nonBaseUoms.length > 0 ? (
                <form onSubmit={handleAddConversion} noValidate className="space-y-3 border-t border-[var(--border-hairline)] pt-3">
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <label htmlFor="conv-uom" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.catalog.convUomLabel}
                      </label>
                      <select
                        id="conv-uom"
                        value={selectedConvUom}
                        onChange={(e) => setConvUomId(e.target.value)}
                        className="font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-sm text-[var(--text-primary)]"
                      >
                        {nonBaseUoms.map((u) => (
                          <option key={u.uomId} value={u.uomId}>
                            {u.uomCode} ({u.name})
                          </option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label htmlFor="conv-factor" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.catalog.convFactorLabel}
                      </label>
                      <input
                        id="conv-factor"
                        type="number"
                        step="0.000001"
                        min="0.000001"
                        value={convFactor}
                        onChange={(e) => {
                          setConvFactor(e.target.value);
                          if (convError) setConvError(null);
                        }}
                        placeholder={t.catalog.convFactorPlaceholder}
                        aria-invalid={Boolean(convError)}
                        aria-describedby={convError ? "conv-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm tabular-nums text-[var(--text-primary)] ${
                          convError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                  </div>
                  {convError ? (
                    <p id="conv-error" role="alert" className="text-xs text-[var(--status-danger-text)]">
                      {convError}
                    </p>
                  ) : null}
                  <button
                    type="submit"
                    disabled={convBusy}
                    className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    {convBusy ? t.catalog.addConvBusy : t.catalog.addConvButton}
                  </button>
                </form>
              ) : null}

              <form onSubmit={handlePreviewConversion} noValidate className="space-y-3 border-t border-[var(--border-hairline)] pt-3">
                <h3 className="flex items-center gap-1.5 text-xs font-semibold text-[var(--text-primary)]">
                  <ArrowRightLeft className="h-3.5 w-3.5 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                  <span>{t.catalog.converterTitle}</span>
                </h3>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div>
                    <label htmlFor="preview-uom" className="block text-xs font-semibold text-[var(--text-secondary)]">
                      {t.catalog.converterUomLabel}
                    </label>
                    <select
                      id="preview-uom"
                      value={previewUomId}
                      onChange={(e) => setPreviewUomId(e.target.value)}
                      className="font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-sm text-[var(--text-primary)]"
                    >
                      {uomsData.items.map((u) => (
                        <option key={u.uomId} value={u.uomId}>
                          {u.uomCode}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="preview-qty" className="block text-xs font-semibold text-[var(--text-secondary)]">
                      {t.catalog.converterQtyLabel}
                    </label>
                    <input
                      id="preview-qty"
                      type="number"
                      step="any"
                      value={previewQty}
                      onChange={(e) => {
                        setPreviewQty(e.target.value);
                        if (previewError) setPreviewError(null);
                      }}
                      placeholder={t.catalog.converterQtyPlaceholder}
                      aria-invalid={Boolean(previewError)}
                      aria-describedby={previewError ? "preview-error" : undefined}
                      className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm tabular-nums text-[var(--text-primary)] ${
                        previewError ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                      }`}
                    />
                  </div>
                </div>
                {previewError ? (
                  <p id="preview-error" role="alert" className="text-xs text-[var(--status-danger-text)]">
                    {previewError}
                  </p>
                ) : null}
                <button
                  type="submit"
                  disabled={previewBusy}
                  className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                >
                  {previewBusy ? t.catalog.convertBusy : t.catalog.convertButton}
                </button>
                {previewResult ? (
                  <div
                    data-testid="uom-conversion-result"
                    className="rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-3 py-2 text-xs text-[var(--status-ok-text)]"
                  >
                    <div className="font-semibold">
                      {t.catalog.convertResultLabel}:{" "}
                      <span className="font-code tabular-nums">
                        {Number(previewResult.inputQuantity)} {previewResult.inputUomCode} ={" "}
                        {Number(previewResult.convertedBaseQuantity)} {previewResult.baseUomCode}
                      </span>
                    </div>
                    <div className="mt-0.5">
                      {t.catalog.convertRoundTripLabel}:{" "}
                      <span className="font-code tabular-nums">
                        {Number(previewResult.roundTripInputQuantity)} {previewResult.inputUomCode}
                      </span>
                    </div>
                  </div>
                ) : null}
              </form>
            </div>

            <div className="space-y-4 p-4">
              <h3 className="text-xs font-semibold text-[var(--text-primary)]">
                {t.catalog.policyTitle} (<span className="font-code">{detail.entity.code}</span>)
              </h3>
              {canManage ? (
                <form onSubmit={handleSavePolicy} noValidate className="space-y-3">
                  <label className="flex min-h-9 cursor-pointer items-center gap-2 text-sm text-[var(--text-primary)]">
                    <input
                      type="checkbox"
                      checked={policyPurchase}
                      onChange={(e) => setPolicyPurchase(e.target.checked)}
                      className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                    />
                    <span>{t.catalog.policyPurchaseCheck}</span>
                  </label>
                  <label className="flex min-h-9 cursor-pointer items-center gap-2 text-sm text-[var(--text-primary)]">
                    <input
                      type="checkbox"
                      checked={policySales}
                      onChange={(e) => setPolicySales(e.target.checked)}
                      className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                    />
                    <span>{t.catalog.policySalesCheck}</span>
                  </label>
                  <div>
                    <label htmlFor="policy-notes" className="block text-xs font-semibold text-[var(--text-secondary)]">
                      {t.catalog.policyNotesLabel}
                    </label>
                    <input
                      id="policy-notes"
                      type="text"
                      value={policyNotes}
                      onChange={(e) => setPolicyNotes(e.target.value)}
                      placeholder={t.catalog.policyNotesPlaceholder}
                      className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                    />
                  </div>
                  {policyError ? (
                    <p id="policy-form-error" role="alert" className="text-xs text-[var(--status-danger-text)]">
                      {policyError}
                    </p>
                  ) : null}
                  <button
                    type="submit"
                    disabled={policyBusy}
                    className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    {policyBusy ? t.catalog.savePolicyBusy : t.catalog.savePolicyButton}
                  </button>
                </form>
              ) : (
                <div className="space-y-3 text-xs text-[var(--text-secondary)]">
                  <div>
                    {t.catalog.scopeModeLabel}:{" "}
                    <span className="font-semibold text-[var(--text-primary)]">
                      {t.catalog.scopeLabels[selectedItem.entityScopeMode] ?? selectedItem.entityScopeMode}
                    </span>
                  </div>
                  <div className="space-y-1.5">
                    <div>{t.catalog.colEligibility}:</div>
                    <div className="flex flex-wrap gap-1.5">
                      {selectedItem.effectivePurchaseAllowed ? (
                        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                          <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>{t.catalog.purchaseEligibleTag}</span>
                        </span>
                      ) : null}
                      {selectedItem.effectiveSalesAllowed ? (
                        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-semibold text-[var(--status-ok-text)]">
                          <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>{t.catalog.salesEligibleTag}</span>
                        </span>
                      ) : null}
                      {!selectedItem.effectivePurchaseAllowed && !selectedItem.effectiveSalesAllowed ? (
                        <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-medium text-[var(--text-muted)]">
                          <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          <span>{t.catalog.ineligibleTag}</span>
                        </span>
                      ) : null}
                    </div>
                  </div>
                  {selectedItem.currentEntityPolicy?.notes ? (
                    <p>{selectedItem.currentEntityPolicy.notes}</p>
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

export default function CatalogPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <CatalogSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
