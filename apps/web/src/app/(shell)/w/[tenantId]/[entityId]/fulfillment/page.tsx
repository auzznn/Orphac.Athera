"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock,
  Lock,
  PackageCheck,
  RefreshCw,
  ShieldCheck,
  Truck,
  Undo2,
} from "lucide-react";
import {
  ApiProblemError,
  confirmPickOrder,
  confirmSalesShipmentDelivery,
  createPickOrder,
  dispatchSalesShipment,
  EntityWorkspaceDetailResponse,
  fetchEntityWorkspaceDetail,
  fetchFulfillmentOverview,
  FulfillableSalesOrderDto,
  FulfillableSalesOrderLineDto,
  FulfillmentOverviewResponse,
  PickOrderDto,
  releaseSalesOrderReservation,
  reserveSalesOrderLine,
  SalesReservationDto,
  SalesShipmentDto,
} from "@/lib/api/client";
import { SalesStatusBadge } from "@/components/sales/SalesOrderDetailSheet";
import { formatIdr, formatJakartaDateTime, formatUnitCostIdr, todayJakartaDate } from "@/lib/i18n/messages";
import { useLocale } from "@/lib/i18n/LocaleProvider";

type FulfillmentTab = "orders" | "picks" | "shipments";

function FulfillmentStatusBadge({ status, label }: { status: string; label: string }) {
  if (
    status === "DELIVERED" ||
    status === "FULFILLED" ||
    status === "FULLY_DISPATCHED" ||
    status === "FULLY_RESERVED"
  ) {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-ok-text)]">
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  if (status === "RELEASED" || status === "CANCELLED" || status === "SHORT_CLOSED") {
    return (
      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-2 py-0.5 text-xs font-medium text-[var(--text-secondary)]">
        <Lock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
        <span>{label}</span>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-xs font-medium text-[var(--status-warn-text)]">
      <Clock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}

export default function FulfillmentPage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <FulfillmentWorkbench key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}

function FulfillmentWorkbench({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const { locale, t } = useLocale();
  const ft = t.fulfillment;
  const st = t.salesOrders;

  const [workspace, setWorkspace] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [overview, setOverview] = useState<FulfillmentOverviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const [activeTab, setActiveTab] = useState<FulfillmentTab>("orders");
  const [policyOpen, setPolicyOpen] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  // Reserve drawer state
  const [reserveTarget, setReserveTarget] = useState<{
    order: FulfillableSalesOrderDto;
    line: FulfillableSalesOrderLineDto;
  } | null>(null);
  const [selectedBalanceId, setSelectedBalanceId] = useState("");
  const [reserveQty, setReserveQty] = useState("");
  const [reserveNotes, setReserveNotes] = useState("");
  const [reserving, setReserving] = useState(false);

  // Quick release / pick busy state
  const [busyRsvId, setBusyRsvId] = useState<string | null>(null);
  const [busyPickId, setBusyPickId] = useState<string | null>(null);

  // Dispatch drawer state
  const [dispatchTarget, setDispatchTarget] = useState<PickOrderDto | null>(null);
  const [dispatchDate, setDispatchDate] = useState<string>(todayJakartaDate);
  const [deliveryNoteRef, setDeliveryNoteRef] = useState("");
  const [carrierName, setCarrierName] = useState("");
  const [vehicleNumber, setVehicleNumber] = useState("");
  const [dispatchQtys, setDispatchQtys] = useState<Record<string, string>>({});
  const [dispatching, setDispatching] = useState(false);

  // Delivery confirmation drawer state
  const [deliverTarget, setDeliverTarget] = useState<SalesShipmentDto | null>(null);
  const [confirmedByName, setConfirmedByName] = useState("");
  const [evidenceReference, setEvidenceReference] = useState("");
  const [confirmingDelivery, setConfirmingDelivery] = useState(false);

  const mapError = useCallback(
    (err: unknown): string => {
      const problem = err as Partial<ApiProblemError>;
      if (problem.errorCode && ft.errors[problem.errorCode]) {
        return ft.errors[problem.errorCode];
      }
      return problem.detail ?? t.login.errors.unknown;
    },
    [ft.errors, t.login.errors.unknown],
  );

  const loadSnapshot = useCallback(async () => {
    const [ws, ov] = await Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchFulfillmentOverview(tenantId, entityId),
    ]);
    return { ws, ov };
  }, [tenantId, entityId]);

  const loadData = useCallback(
    async (isRefresh = false) => {
      if (isRefresh) setRefreshing(true);
      setActionError(null);
      try {
        const { ws, ov } = await loadSnapshot();
        setWorkspace(ws);
        setOverview(ov);
        setLoadError(null);
        setAccessDenied(false);
      } catch (err) {
        const problem = err as Partial<ApiProblemError>;
        if (problem.status === 401) {
          router.replace("/login");
          return;
        }
        if (problem.status === 403 || problem.status === 404) {
          setAccessDenied(true);
          return;
        }
        setLoadError(problem.detail ?? t.login.errors.upstream_unavailable);
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [loadSnapshot, router, t.login.errors.upstream_unavailable],
  );

  useEffect(() => {
    if (!tenantId || !entityId) return;
    let active = true;
    loadSnapshot()
      .then(({ ws, ov }) => {
        if (!active) return;
        setWorkspace(ws);
        setOverview(ov);
        setLoadError(null);
        setAccessDenied(false);
      })
      .catch((err) => {
        if (!active) return;
        const problem = err as Partial<ApiProblemError>;
        if (problem.status === 401) {
          router.replace("/login");
          return;
        }
        if (problem.status === 403 || problem.status === 404) {
          setAccessDenied(true);
          return;
        }
        setLoadError(problem.detail ?? t.login.errors.upstream_unavailable);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [tenantId, entityId, loadSnapshot, router, t.login.errors.upstream_unavailable]);

  const perms = useMemo(() => new Set(workspace?.effectivePermissions ?? []), [workspace]);
  const canManage = perms.has("inventory.manage");

  const orders = useMemo(() => overview?.fulfillableOrders ?? [], [overview]);
  const reservations = useMemo(() => overview?.reservations ?? [], [overview]);
  const pickOrders = useMemo(() => overview?.pickOrders ?? [], [overview]);
  const shipments = useMemo(() => overview?.shipments ?? [], [overview]);

  const kpis = useMemo(() => {
    const openRemainingQty = orders.reduce(
      (s, o) => s + Number(o.remainingUnfulfilledBaseQuantity || 0),
      0,
    );
    const activeReservedQty = reservations
      .filter((r) => r.status === "ACTIVE" || r.status === "PARTIALLY_CONSUMED")
      .reduce((s, r) => s + Number(r.remainingReservedBaseQuantity || 0), 0);
    const pickedReadyQty = pickOrders
      .filter((p) => p.status === "PICKED")
      .reduce((s, p) => s + Number(p.remainingToDispatchBaseQuantity || 0), 0);
    const totalDispatchedCogs = shipments.reduce(
      (s, shp) => s + Number(shp.totalCogsAmount || 0),
      0,
    );
    return {
      openRemainingQty,
      activeReservedQty,
      pickedReadyQty,
      totalDispatchedCogs,
    };
  }, [orders, reservations, pickOrders, shipments]);

  const unitCostByItemId = useMemo(() => {
    const m = new Map<string, number>();
    for (const o of orders) {
      for (const l of o.lines) {
        m.set(l.itemId, Number(l.movingAverageUnitCost || 0));
      }
    }
    return m;
  }, [orders]);

  const openReserveDrawer = (order: FulfillableSalesOrderDto, line: FulfillableSalesOrderLineDto) => {
    setActionError(null);
    setDispatchTarget(null);
    setDeliverTarget(null);
    setReserveTarget({ order, line });
    const firstBal = line.eligibleLocations.find((b) => Number(b.availableBaseQuantity) > 0) ?? line.eligibleLocations[0];
    setSelectedBalanceId(firstBal?.stockLocationBalanceId ?? "");
    const maxAvail = firstBal ? Number(firstBal.availableBaseQuantity) : 0;
    const remLine = Number(line.remainingToReserveBaseQuantity);
    const suggested = Math.min(remLine, maxAvail > 0 ? maxAvail : remLine);
    setReserveQty(suggested > 0 ? String(suggested) : "");
    setReserveNotes("");
  };

  const handleSubmitReserve = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reserveTarget) return;
    const bal = reserveTarget.line.eligibleLocations.find(
      (b) => b.stockLocationBalanceId === selectedBalanceId,
    );
    if (!bal) return;
    const qty = Number(reserveQty);
    if (!Number.isFinite(qty) || qty <= 0) return;

    setReserving(true);
    setActionError(null);
    try {
      await reserveSalesOrderLine(tenantId, entityId, {
        salesOrderId: reserveTarget.order.salesOrderId,
        soLineId: reserveTarget.line.soLineId,
        locationId: bal.locationId,
        stockLotId: bal.stockLotId ?? null,
        quantityBase: qty,
        idempotencyKey: `rsv-${reserveTarget.line.soLineId.slice(0, 8)}-${Date.now()}`,
        notes: reserveNotes.trim() || null,
      });
      setReserveTarget(null);
      await loadData(true);
      setActiveTab("picks");
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setReserving(false);
    }
  };

  const handleReleaseReservation = async (rsv: SalesReservationDto) => {
    setBusyRsvId(rsv.stockReservationId);
    setActionError(null);
    try {
      await releaseSalesOrderReservation(tenantId, entityId, rsv.stockReservationId, {});
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusyRsvId(null);
    }
  };

  const handleQuickCreateAndConfirmPick = async (rsv: SalesReservationDto) => {
    const qty = Number(rsv.unallocatedReservedBaseQuantity);
    if (qty <= 0) return;
    setBusyRsvId(rsv.stockReservationId);
    setActionError(null);
    try {
      await createPickOrder(tenantId, entityId, {
        salesOrderId: rsv.salesOrderId,
        confirmImmediately: true,
        idempotencyKey: `pck-${rsv.stockReservationId.slice(0, 8)}-${Date.now()}`,
        notes: `Quick pick from ${rsv.reservationNumber}`,
        lines: [
          {
            reservationId: rsv.stockReservationId,
            requestedPickBaseQuantity: qty,
            pickedBaseQuantity: qty,
          },
        ],
      });
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusyRsvId(null);
    }
  };

  const handleConfirmOpenPick = async (po: PickOrderDto) => {
    setBusyPickId(po.pickOrderId);
    setActionError(null);
    try {
      await confirmPickOrder(tenantId, entityId, po.pickOrderId, {
        lines: po.lines.map((l) => ({
          pickLineId: l.pickLineId,
          pickedBaseQuantity: Number(l.requestedPickBaseQuantity),
        })),
      });
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setBusyPickId(null);
    }
  };

  const openDispatchDrawer = (po: PickOrderDto) => {
    setActionError(null);
    setReserveTarget(null);
    setDeliverTarget(null);
    setDispatchTarget(po);
    setDispatchDate(todayJakartaDate());
    setDeliveryNoteRef(`SJ-${po.salesOrderNumber}-${po.pickNumber.slice(-5)}`);
    setCarrierName("");
    setVehicleNumber("");
    const initQtys: Record<string, string> = {};
    for (const l of po.lines) {
      const rem = Number(l.remainingToDispatchBaseQuantity);
      if (rem > 0) {
        initQtys[l.pickLineId] = String(rem);
      }
    }
    setDispatchQtys(initQtys);
  };

  const projectedDispatchCogs = useMemo(() => {
    if (!dispatchTarget) return 0;
    let total = 0;
    for (const l of dispatchTarget.lines) {
      const q = Number(dispatchQtys[l.pickLineId] ?? 0);
      const cost = unitCostByItemId.get(l.itemId) ?? 0;
      if (Number.isFinite(q) && q > 0) {
        total += Math.round(q * cost);
      }
    }
    return total;
  }, [dispatchTarget, dispatchQtys, unitCostByItemId]);

  const handleSubmitDispatch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!dispatchTarget || !deliveryNoteRef.trim()) return;

    const lines = dispatchTarget.lines
      .map((l) => ({
        pickLineId: l.pickLineId,
        dispatchedBaseQuantity: Number(dispatchQtys[l.pickLineId] ?? 0),
      }))
      .filter((x) => Number.isFinite(x.dispatchedBaseQuantity) && x.dispatchedBaseQuantity > 0);

    if (lines.length === 0) return;

    setDispatching(true);
    setActionError(null);
    try {
      await dispatchSalesShipment(tenantId, entityId, {
        pickOrderId: dispatchTarget.pickOrderId,
        dispatchDate,
        deliveryNoteReference: deliveryNoteRef.trim(),
        carrierName: carrierName.trim() || null,
        trackingOrVehicleNumber: vehicleNumber.trim() || null,
        idempotencyKey: `shp-${dispatchTarget.pickOrderId.slice(0, 8)}-${Date.now()}`,
        notes: null,
        lines,
      });
      setDispatchTarget(null);
      await loadData(true);
      setActiveTab("shipments");
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setDispatching(false);
    }
  };

  const openDeliverDrawer = (shp: SalesShipmentDto) => {
    setActionError(null);
    setReserveTarget(null);
    setDispatchTarget(null);
    setDeliverTarget(shp);
    setConfirmedByName("");
    setEvidenceReference(`POD-${shp.deliveryNoteReference}`);
  };

  const handleSubmitDeliveryConfirmation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!deliverTarget || !confirmedByName.trim() || !evidenceReference.trim()) return;

    setConfirmingDelivery(true);
    setActionError(null);
    try {
      await confirmSalesShipmentDelivery(tenantId, entityId, deliverTarget.shipmentId, {
        confirmedByName: confirmedByName.trim(),
        evidenceReference: evidenceReference.trim(),
      });
      setDeliverTarget(null);
      await loadData(true);
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setConfirmingDelivery(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4">
        <div className="rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-6">
          <p className="text-sm text-[var(--text-secondary)]">{ft.loading}</p>
        </div>
      </div>
    );
  }

  if (accessDenied) {
    return (
      <div
        role="alert"
        className="rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6"
      >
        <div className="flex items-start gap-3 text-[var(--status-danger-text)]">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div>
            <h1 className="text-base font-semibold text-[var(--text-primary)]">{ft.accessDeniedHeading}</h1>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">{ft.accessDeniedBody}</p>
          </div>
        </div>
      </div>
    );
  }

  if (loadError || !workspace || !overview) {
    return (
      <div
        role="alert"
        className="rounded-[var(--radius-md)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] p-6 text-sm text-[var(--status-danger-text)]"
      >
        {loadError ?? t.login.errors.upstream_unavailable}
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <header className="space-y-2">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-xl font-bold tracking-tight text-[var(--text-primary)]">{ft.heading}</h1>
            <p className="mt-1 max-w-3xl text-xs leading-relaxed text-[var(--text-secondary)]">{ft.lead}</p>
            <div className="mt-1.5 font-code text-[11px] tabular-nums text-[var(--text-muted)]">
              {workspace.tenant.code} · {workspace.entity.code} ({workspace.entity.functionalCurrencyCode}) ·{" "}
              {formatJakartaDateTime(workspace.serverTimestampUtc, locale)}
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Link
              href={`/w/${tenantId}/${entityId}`}
              className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>{ft.backToEntityLink}</span>
            </Link>
            <button
              type="button"
              id="ful-refresh-btn"
              disabled={refreshing}
              onClick={() => void loadData(true)}
              className="inline-flex min-h-9 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
            >
              <RefreshCw
                className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`}
                strokeWidth={1.75}
                aria-hidden="true"
              />
              <span>{refreshing ? ft.refreshing : ft.refreshButton}</span>
            </button>
          </div>
        </div>
      </header>

      {/* Read-only notice */}
      {!canManage ? (
        <div
          id="ful-readonly-banner"
          role="status"
          className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-3.5 py-2.5 text-xs text-[var(--status-warn-text)]"
        >
          <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{ft.readOnlyBanner}</span>
        </div>
      ) : null}

      {/* OD-10 & OD-09 Governance Strip */}
      <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)]">
        <div className="flex flex-wrap items-center justify-between gap-2 bg-[var(--status-warn-bg)] px-4 py-2.5 text-xs text-[var(--status-warn-text)] md:flex-nowrap">
          <div className="flex items-start gap-2">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
            <span>{ft.od10PolicyBanner}</span>
          </div>
          <button
            type="button"
            id="ful-policy-toggle"
            aria-expanded={policyOpen}
            onClick={() => setPolicyOpen((v) => !v)}
            className="inline-flex min-h-9 shrink-0 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          >
            <span>{policyOpen ? ft.policyToggleHide : ft.policyToggleShow}</span>
            {policyOpen ? (
              <ChevronUp className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            ) : (
              <ChevronDown className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
            )}
          </button>
        </div>

        {policyOpen ? (
          <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)] md:grid-cols-4 md:divide-x md:divide-y-0">
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{ft.policyCards.rule1Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{ft.policyCards.rule1Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{ft.policyCards.rule2Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{ft.policyCards.rule2Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{ft.policyCards.rule3Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{ft.policyCards.rule3Body}</p>
            </div>
            <div className="p-3">
              <div className="text-xs font-semibold text-[var(--text-primary)]">{ft.policyCards.rule4Title}</div>
              <p className="mt-1 text-xs leading-relaxed text-[var(--text-secondary)]">{ft.policyCards.rule4Body}</p>
            </div>
          </div>
        ) : null}
      </section>

      {/* 4-Metric Hairline KPI Strip */}
      <section
        aria-label={ft.statsAriaLabel}
        className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] md:grid-cols-4 md:divide-x md:divide-y-0"
      >
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{ft.stats.openOrderRemainingBaseQty}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {kpis.openRemainingQty}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{ft.stats.activeReservedBaseQty}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {kpis.activeReservedQty}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{ft.stats.pickedReadyToDispatchBaseQty}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {kpis.pickedReadyQty}
          </div>
        </div>
        <div className="p-3.5">
          <div className="text-xs font-medium text-[var(--text-secondary)]">{ft.stats.totalDispatchedCogsAmount}</div>
          <div className="mt-1 font-code text-base font-bold tabular-nums text-[var(--text-primary)]">
            {formatIdr(kpis.totalDispatchedCogs)}
          </div>
        </div>
      </section>

      {actionError ? (
        <div
          id="ful-action-error"
          role="alert"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-3.5 py-2.5 text-xs text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{actionError}</span>
        </div>
      ) : null}

      {/* Inline Reservation Drawer */}
      {reserveTarget && canManage ? (
        <section
          id="ful-reserve-drawer"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {ft.ordersPanel.drawerTitlePrefix}{" "}
              <span className="font-code">{reserveTarget.order.soNumber}</span> ·{" "}
              <span className="font-code">{reserveTarget.line.itemCode}</span>
            </span>
            <button
              type="button"
              id="ful-rsv-cancel-btn"
              onClick={() => setReserveTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {ft.ordersPanel.cancelDrawerBtn}
            </button>
          </div>
          <form onSubmit={handleSubmitReserve} className="grid grid-cols-1 gap-3 p-4 md:grid-cols-12">
            <label className="block space-y-1 md:col-span-5">
              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                {ft.ordersPanel.selectLocationLotLabel}
              </span>
              <select
                id="ful-rsv-balance-select"
                value={selectedBalanceId}
                onChange={(e) => setSelectedBalanceId(e.target.value)}
                className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
              >
                {reserveTarget.line.eligibleLocations.map((b) => (
                  <option key={b.stockLocationBalanceId} value={b.stockLocationBalanceId}>
                    {b.warehouseCode} / {b.locationCode}
                    {b.lotNumber ? ` · ${ft.ordersPanel.lotPrefix} ${b.lotNumber}` : ""} — {ft.ordersPanel.availPrefix}{" "}
                    {b.availableBaseQuantity} {reserveTarget.line.baseUomCode}
                  </option>
                ))}
              </select>
            </label>
            <label className="block space-y-1 md:col-span-3">
              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                {ft.ordersPanel.reserveQtyLabel} ({reserveTarget.line.baseUomCode})
              </span>
              <input
                id="ful-rsv-qty"
                type="number"
                step="0.001"
                min="0.001"
                required
                value={reserveQty}
                onChange={(e) => setReserveQty(e.target.value)}
                className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
              />
            </label>
            <label className="block space-y-1 md:col-span-4">
              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                {ft.ordersPanel.notesLabel}
              </span>
              <input
                id="ful-rsv-notes"
                type="text"
                value={reserveNotes}
                placeholder={ft.ordersPanel.notesPlaceholder}
                onChange={(e) => setReserveNotes(e.target.value)}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
              />
            </label>
            <div className="flex justify-end md:col-span-12">
              <button
                type="submit"
                id="ful-rsv-submit-btn"
                disabled={reserving || !selectedBalanceId}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{reserving ? ft.ordersPanel.reserving : ft.ordersPanel.submitReserveCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Inline Dispatch Drawer */}
      {dispatchTarget && canManage ? (
        <section
          id="ful-dispatch-drawer"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {ft.picksPanel.dispatchDrawerTitlePrefix}{" "}
              <span className="font-code">{dispatchTarget.pickNumber}</span> (
              <span className="font-code">{dispatchTarget.salesOrderNumber}</span>)
            </span>
            <button
              type="button"
              id="ful-disp-cancel-btn"
              onClick={() => setDispatchTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {ft.ordersPanel.cancelDrawerBtn}
            </button>
          </div>
          <form onSubmit={handleSubmitDispatch} className="space-y-3 p-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {ft.picksPanel.dispatchDateLabel}
                </span>
                <input
                  id="ful-disp-date"
                  type="date"
                  required
                  value={dispatchDate}
                  onChange={(e) => setDispatchDate(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {ft.picksPanel.deliveryNoteLabel}
                </span>
                <input
                  id="ful-disp-sj"
                  type="text"
                  required
                  value={deliveryNoteRef}
                  placeholder={ft.picksPanel.deliveryNotePlaceholder}
                  onChange={(e) => setDeliveryNoteRef(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {ft.picksPanel.carrierNameLabel}
                </span>
                <input
                  id="ful-disp-carrier"
                  type="text"
                  value={carrierName}
                  placeholder={ft.picksPanel.carrierNamePlaceholder}
                  onChange={(e) => setCarrierName(e.target.value)}
                  className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
              <label className="block space-y-1">
                <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                  {ft.picksPanel.vehicleLabel}
                </span>
                <input
                  id="ful-disp-vehicle"
                  type="text"
                  value={vehicleNumber}
                  placeholder={ft.picksPanel.vehiclePlaceholder}
                  onChange={(e) => setVehicleNumber(e.target.value)}
                  className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                />
              </label>
            </div>

            <div className="divide-y divide-[var(--border-hairline)] border-t border-[var(--border-hairline)]">
              {dispatchTarget.lines.map((l, idx) => (
                <div
                  key={l.pickLineId}
                  className="flex flex-wrap items-center justify-between gap-2 py-2.5 text-xs"
                >
                  <div>
                    <span className="font-code font-bold text-[var(--text-primary)]">{l.itemCode}</span> ·{" "}
                    <span>{l.itemName}</span>{" "}
                    <span className="font-code text-[var(--text-secondary)]">
                      ({l.locationCode}
                      {l.lotNumber ? ` · ${ft.ordersPanel.lotPrefix} ${l.lotNumber}` : ""} ·{" "}
                      {ft.picksPanel.maxPrefix} {l.remainingToDispatchBaseQuantity} {l.baseUomCode})
                    </span>
                  </div>
                  <label className="inline-flex items-center gap-2">
                    <span className="text-[11px] font-medium text-[var(--text-secondary)]">
                      {ft.picksPanel.dispatchQtyLabel}
                    </span>
                    <input
                      id={`ful-disp-qty-${idx}`}
                      type="number"
                      step="0.001"
                      min="0.001"
                      max={Number(l.remainingToDispatchBaseQuantity)}
                      required
                      value={dispatchQtys[l.pickLineId] ?? ""}
                      onChange={(e) =>
                        setDispatchQtys((prev) => ({
                          ...prev,
                          [l.pickLineId]: e.target.value,
                        }))
                      }
                      className="font-code min-h-9 w-28 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs tabular-nums text-[var(--text-primary)]"
                    />
                  </label>
                </div>
              ))}
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border-hairline)] pt-3">
              <div className="text-xs text-[var(--text-secondary)]">
                <span>{ft.picksPanel.projectedCogsPrefix} </span>
                <span className="font-code font-bold tabular-nums text-[var(--text-primary)]">
                  {formatIdr(projectedDispatchCogs)}
                </span>
              </div>
              <button
                type="submit"
                id="ful-disp-submit-btn"
                disabled={dispatching || !deliveryNoteRef.trim()}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <Truck className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>{dispatching ? ft.picksPanel.dispatching : ft.picksPanel.submitDispatchCta}</span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Inline Delivery Confirmation (POD) Drawer */}
      {deliverTarget && canManage ? (
        <section
          id="ful-deliver-drawer"
          className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]"
        >
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
            <span>
              {ft.shipmentsPanel.deliverDrawerTitlePrefix}{" "}
              <span className="font-code">{deliverTarget.shipmentNumber}</span> (
              <span className="font-code">{deliverTarget.salesOrderNumber}</span>)
            </span>
            <button
              type="button"
              id="ful-pod-cancel-btn"
              onClick={() => setDeliverTarget(null)}
              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
            >
              {ft.ordersPanel.cancelDrawerBtn}
            </button>
          </div>
          <form onSubmit={handleSubmitDeliveryConfirmation} className="grid grid-cols-1 gap-3 p-4 md:grid-cols-12">
            <label className="block space-y-1 md:col-span-5">
              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                {ft.shipmentsPanel.confirmedByLabel}
              </span>
              <input
                id="ful-pod-receiver"
                type="text"
                required
                value={confirmedByName}
                placeholder={ft.shipmentsPanel.confirmedByPlaceholder}
                onChange={(e) => setConfirmedByName(e.target.value)}
                className="min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
              />
            </label>
            <label className="block space-y-1 md:col-span-4">
              <span className="block text-[11px] font-medium text-[var(--text-secondary)]">
                {ft.shipmentsPanel.evidenceRefLabel}
              </span>
              <input
                id="ful-pod-ref"
                type="text"
                required
                value={evidenceReference}
                placeholder={ft.shipmentsPanel.evidenceRefPlaceholder}
                onChange={(e) => setEvidenceReference(e.target.value)}
                className="font-code min-h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
              />
            </label>
            <div className="flex items-end justify-end md:col-span-3">
              <button
                type="submit"
                id="ful-pod-submit-btn"
                disabled={confirmingDelivery || !confirmedByName.trim() || !evidenceReference.trim()}
                className="inline-flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
              >
                <CheckCircle2 className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>
                  {confirmingDelivery
                    ? ft.shipmentsPanel.confirmingDelivery
                    : ft.shipmentsPanel.submitDeliverCta}
                </span>
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {/* Tabs */}
      <div
        role="tablist"
        aria-label={ft.tabListLabel}
        className="flex flex-nowrap items-center gap-1 overflow-x-auto border-b border-[var(--border-hairline)]"
      >
        <button
          type="button"
          role="tab"
          id="ful-tab-orders"
          aria-selected={activeTab === "orders"}
          aria-controls="ful-panel-orders"
          onClick={() => setActiveTab("orders")}
          className={`min-h-10 shrink-0 whitespace-nowrap rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
            activeTab === "orders"
              ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
              : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          {ft.tabs.orders} ({orders.length})
        </button>
        <button
          type="button"
          role="tab"
          id="ful-tab-picks"
          aria-selected={activeTab === "picks"}
          aria-controls="ful-panel-picks"
          onClick={() => setActiveTab("picks")}
          className={`min-h-10 shrink-0 whitespace-nowrap rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
            activeTab === "picks"
              ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
              : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          {ft.tabs.reservationsAndPicks} ({reservations.length} / {pickOrders.length})
        </button>
        <button
          type="button"
          role="tab"
          id="ful-tab-shipments"
          aria-selected={activeTab === "shipments"}
          aria-controls="ful-panel-shipments"
          onClick={() => setActiveTab("shipments")}
          className={`min-h-10 shrink-0 whitespace-nowrap rounded-t-[var(--radius-sm)] px-3.5 text-xs font-semibold ${
            activeTab === "shipments"
              ? "border-b-2 border-[var(--accent-primary)] bg-[var(--bg-surface)] text-[var(--accent-primary)]"
              : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          {ft.tabs.shipments} ({shipments.length})
        </button>
      </div>

      {/* TAB 1: SO FULFILLMENT QUEUE */}
      {activeTab === "orders" ? (
        <div id="ful-panel-orders" role="tabpanel" aria-labelledby="ful-tab-orders">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {ft.ordersPanel.title}
            </div>
            {orders.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{ft.ordersPanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ful-orders-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{ft.ordersPanel.colSoCustomer}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.ordersPanel.colLineItem}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.ordersPanel.colProgressQty}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.ordersPanel.colAvailBins}</th>
                      {canManage ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{ft.ordersPanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {orders.flatMap((so) =>
                      so.lines.map((line) => {
                        const canReserveLine =
                          canManage &&
                          Number(line.remainingToReserveBaseQuantity) > 0 &&
                          line.eligibleLocations.some((b) => Number(b.availableBaseQuantity) > 0);
                        return (
                        <tr key={`${so.salesOrderId}:${line.soLineId}`} className="block p-3 md:table-row md:p-0">
                          <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {ft.ordersPanel.colSoCustomer}
                            </span>
                            <div className="flex flex-wrap items-center gap-1.5">
                              <span className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                                {so.soNumber}
                              </span>
                              <SalesStatusBadge
                                status={so.fulfillmentProgress}
                                label={st.fulfillmentLabels[so.fulfillmentProgress] ?? so.fulfillmentProgress}
                              />
                            </div>
                            <div className="mt-0.5 text-[11px] font-medium text-[var(--text-primary)]">
                              <span className="font-code">{so.customerCode}</span> · {so.customerName}
                            </div>
                            <div className="text-[11px] text-[var(--text-secondary)]">
                              {so.deliveryAddressSnapshot}
                            </div>
                          </td>

                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {ft.ordersPanel.colLineItem}
                            </span>
                            <div className="font-medium text-[var(--text-primary)]">
                              <span className="font-code font-bold">{line.itemCode}</span> · {line.itemName}
                            </div>
                            <div className="mt-0.5 text-[11px] text-[var(--text-secondary)]">
                              <span className="font-code">{line.lineageLineKey}</span> ·{" "}
                              {ft.ordersPanel.lotPolicyLabels[line.lotTrackingPolicy] ?? line.lotTrackingPolicy} ·{" "}
                              {ft.ordersPanel.avgCostPrefix}{" "}
                              <span className="font-code tabular-nums">
                                {formatUnitCostIdr(line.movingAverageUnitCost)}
                              </span>
                            </div>
                          </td>

                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {ft.ordersPanel.colProgressQty}
                            </span>
                            <div className="font-code tabular-nums text-[var(--text-primary)]">
                              {ft.ordersPanel.orderedPrefix}{" "}
                              <span className="font-bold">{line.orderedBaseQuantity}</span> {line.baseUomCode}
                            </div>
                            <div className="mt-0.5 font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                              {ft.ordersPanel.reservedPrefix} {line.reservedBaseQuantity} ·{" "}
                              {ft.ordersPanel.dispatchedPrefix} {line.dispatchedBaseQuantity} ·{" "}
                              {ft.ordersPanel.remainingReservePrefix}{" "}
                              <span className="font-bold text-[var(--text-primary)]">
                                {line.remainingToReserveBaseQuantity}
                              </span>
                            </div>
                          </td>

                          <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                            <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                              {ft.ordersPanel.colAvailBins}
                            </span>
                            {line.eligibleLocations.length === 0 ? (
                              <span className="text-[11px] text-[var(--text-muted)]">
                                {ft.ordersPanel.noEligibleBins}
                              </span>
                            ) : (
                              <div className="space-y-0.5">
                                {line.eligibleLocations.map((b) => (
                                  <div
                                    key={b.stockLocationBalanceId}
                                    className="font-code text-[11px] tabular-nums text-[var(--text-primary)]"
                                  >
                                    <span className="font-semibold">{b.locationCode}</span>
                                    {b.lotNumber ? ` (${b.lotNumber})` : ""}:{" "}
                                    <span className="font-bold text-[var(--status-ok-text)]">
                                      {b.availableBaseQuantity} {line.baseUomCode}
                                    </span>
                                  </div>
                                ))}
                              </div>
                            )}
                          </td>

                          {canManage ? (
                            <td className={`${canReserveLine ? "block py-1" : "hidden"} md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right`}>
                              {canReserveLine ? (
                                <>
                                  <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                                    {ft.ordersPanel.colAction}
                                  </span>
                                  <button
                                    type="button"
                                    data-testid={`open-reserve-${so.soNumber}-${line.lineageLineKey}`}
                                    onClick={() => openReserveDrawer(so, line)}
                                    className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                                  >
                                    <PackageCheck className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                    <span>{ft.ordersPanel.reserveBtn}</span>
                                  </button>
                                </>
                              ) : null}
                            </td>
                          ) : null}
                        </tr>
                      );
                      }),
                    )}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}

      {/* TAB 2: RESERVATIONS & WAREHOUSE PICKS */}
      {activeTab === "picks" ? (
        <div id="ful-panel-picks" role="tabpanel" aria-labelledby="ful-tab-picks" className="space-y-5">
          {/* Active Reservations */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {ft.picksPanel.reservationsTitle}
            </div>
            {reservations.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">
                {ft.picksPanel.emptyReservations}
              </p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ful-reservations-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{ft.picksPanel.colRsvDoc}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.picksPanel.colSoLineItem}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.picksPanel.colLocLot}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.picksPanel.colReservedBalances}</th>
                      {canManage ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{ft.picksPanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {reservations.map((rsv) => {
                      const canActOnRsv = canManage && Number(rsv.unallocatedReservedBaseQuantity) > 0;
                      return (
                      <tr key={rsv.stockReservationId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colRsvDoc}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                            {rsv.reservationNumber}
                          </div>
                          <div className="mt-0.5">
                            <FulfillmentStatusBadge
                              status={rsv.status}
                              label={ft.statusLabels[rsv.status] ?? rsv.status}
                            />
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colSoLineItem}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold text-[var(--text-primary)]">{rsv.salesOrderNumber}</div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            <span className="font-code">{rsv.itemCode}</span> · {rsv.itemName}
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colLocLot}
                          </span>
                          <div className="font-code text-[var(--text-primary)]">
                            {rsv.warehouseCode} / <span className="font-bold">{rsv.locationCode}</span>
                          </div>
                          {rsv.lotNumber ? (
                            <div className="font-code text-[11px] text-[var(--text-secondary)]">
                              {ft.ordersPanel.lotPrefix} {rsv.lotNumber}
                            </div>
                          ) : null}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colReservedBalances}
                          </span>
                          <div className="font-code tabular-nums text-[var(--text-primary)]">
                            RSV: <span className="font-bold">{rsv.reservedBaseQuantity}</span> {rsv.baseUomCode} ·{" "}
                            {ft.picksPanel.remainingPrefix} {rsv.remainingReservedBaseQuantity}
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {ft.picksPanel.inPickPrefix} {rsv.allocatedToOpenPickBaseQuantity} ·{" "}
                            {ft.picksPanel.unallocPrefix}{" "}
                            <span className="font-bold text-[var(--text-primary)]">
                              {rsv.unallocatedReservedBaseQuantity}
                            </span>
                          </div>
                        </td>
                        {canManage ? (
                          <td className={`${canActOnRsv ? "block py-1" : "hidden"} md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right`}>
                            {canActOnRsv ? (
                              <>
                                <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                                  {ft.picksPanel.colAction}
                                </span>
                                <div className="flex flex-wrap items-center justify-end gap-1.5">
                                  <button
                                    type="button"
                                    data-testid={`release-rsv-${rsv.reservationNumber}`}
                                    disabled={busyRsvId === rsv.stockReservationId}
                                    onClick={() => void handleReleaseReservation(rsv)}
                                    className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-medium text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                                  >
                                    <Undo2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                    <span>
                                      {busyRsvId === rsv.stockReservationId
                                        ? ft.picksPanel.releasing
                                        : ft.picksPanel.releaseBtn}
                                    </span>
                                  </button>
                                  <button
                                    type="button"
                                    data-testid={`pick-rsv-${rsv.reservationNumber}`}
                                    disabled={busyRsvId === rsv.stockReservationId}
                                    onClick={() => void handleQuickCreateAndConfirmPick(rsv)}
                                    className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                                  >
                                    <PackageCheck className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                    <span>
                                      {busyRsvId === rsv.stockReservationId
                                        ? ft.picksPanel.creatingPick
                                        : ft.picksPanel.createPickQuickBtn}
                                    </span>
                                  </button>
                                </div>
                              </>
                            ) : null}
                          </td>
                        ) : null}
                      </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* Warehouse Pick Orders */}
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {ft.picksPanel.picksTitle}
            </div>
            {pickOrders.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{ft.picksPanel.emptyPicks}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ful-picks-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{ft.picksPanel.colPickDoc}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.picksPanel.colSoWarehouse}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.picksPanel.colPickLines}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.picksPanel.colPickQty}</th>
                      {canManage ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{ft.picksPanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {pickOrders.map((po) => {
                      const canConfirmPick = canManage && po.status === "OPEN";
                      const canDispatchPick =
                        canManage && po.status === "PICKED" && Number(po.remainingToDispatchBaseQuantity) > 0;
                      const hasPickAction = canConfirmPick || canDispatchPick;
                      return (
                      <tr key={po.pickOrderId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colPickDoc}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                            {po.pickNumber}
                          </div>
                          <div className="mt-0.5">
                            <FulfillmentStatusBadge
                              status={po.status}
                              label={ft.statusLabels[po.status] ?? po.status}
                            />
                          </div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colSoWarehouse}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold text-[var(--text-primary)]">{po.salesOrderNumber}</div>
                          <div className="font-code text-[11px] text-[var(--text-secondary)]">{po.warehouseCode}</div>
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colPickLines}
                          </span>
                          {po.lines.map((l) => (
                            <div key={l.pickLineId} className="text-[11px] text-[var(--text-primary)]">
                              <span className="font-code font-semibold">{l.itemCode}</span> @{" "}
                              <span className="font-code">{l.locationCode}</span>
                              {l.lotNumber ? ` (${l.lotNumber})` : ""} —{" "}
                              <span className="font-code whitespace-nowrap">{l.reservationNumber}</span>
                            </div>
                          ))}
                        </td>
                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.picksPanel.colPickQty}
                          </span>
                          <div className="font-code tabular-nums text-[var(--text-primary)]">
                            {ft.picksPanel.reqPrefix} {po.totalRequestedBaseQuantity} · {ft.picksPanel.pickedPrefix}{" "}
                            <span className="font-bold">{po.totalPickedBaseQuantity}</span>
                          </div>
                          <div className="font-code text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {ft.picksPanel.dispatchedPrefix} {po.totalDispatchedBaseQuantity} · {ft.picksPanel.remPrefix}{" "}
                            <span className="font-bold text-[var(--text-primary)]">
                              {po.remainingToDispatchBaseQuantity}
                            </span>
                          </div>
                        </td>
                        {canManage ? (
                          <td className={`${hasPickAction ? "block py-1" : "hidden"} md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right`}>
                            {hasPickAction ? (
                              <>
                                <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                                  {ft.picksPanel.colAction}
                                </span>
                                {canConfirmPick ? (
                                  <button
                                    type="button"
                                    data-testid={`confirm-pick-${po.pickNumber}`}
                                    disabled={busyPickId === po.pickOrderId}
                                    onClick={() => void handleConfirmOpenPick(po)}
                                    className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                                  >
                                    <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                    <span>
                                      {busyPickId === po.pickOrderId
                                        ? ft.picksPanel.confirmingPick
                                        : ft.picksPanel.confirmPickBtn}
                                    </span>
                                  </button>
                                ) : (
                                  <button
                                    type="button"
                                    data-testid={`open-dispatch-${po.pickNumber}`}
                                    onClick={() => openDispatchDrawer(po)}
                                    className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                                  >
                                    <Truck className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                    <span>{ft.picksPanel.openDispatchDrawerBtn}</span>
                                  </button>
                                )}
                              </>
                            ) : null}
                          </td>
                        ) : null}
                      </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}

      {/* TAB 3: SHIPMENTS, COGS & POD */}
      {activeTab === "shipments" ? (
        <div id="ful-panel-shipments" role="tabpanel" aria-labelledby="ful-tab-shipments">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs font-semibold text-[var(--text-primary)]">
              {ft.shipmentsPanel.title}
            </div>
            {shipments.length === 0 ? (
              <p className="p-6 text-center text-xs text-[var(--text-secondary)]">{ft.shipmentsPanel.empty}</p>
            ) : (
              <div className="md:overflow-x-auto">
                <table id="ful-shipments-table" className="w-full border-collapse text-left text-xs">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="py-2 md:pl-4 md:pr-2">{ft.shipmentsPanel.colShipmentDoc}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.shipmentsPanel.colCustomerDelivery}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.shipmentsPanel.colItemsQty}</th>
                      <th scope="col" className="py-2 md:px-2 text-right">{ft.shipmentsPanel.colCogsLedger}</th>
                      <th scope="col" className="py-2 md:px-2">{ft.shipmentsPanel.colStatusEvidence}</th>
                      {canManage ? (
                        <th scope="col" className="py-2 md:pl-2 md:pr-4 text-right">{ft.shipmentsPanel.colAction}</th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-hairline)]">
                    {shipments.map((shp) => {
                      const canDeliver = canManage && shp.status === "DISPATCHED";
                      return (
                      <tr key={shp.shipmentId} className="block p-3 md:table-row md:p-0">
                        <td className="block py-1 md:table-cell md:py-2.5 md:pl-4 md:pr-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.shipmentsPanel.colShipmentDoc}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                            {shp.shipmentNumber}
                          </div>
                          <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {shp.dispatchDate}
                          </div>
                          <div className="font-code flex flex-wrap gap-x-1.5 text-[11px] tabular-nums text-[var(--text-secondary)]">
                            <span className="whitespace-nowrap">{shp.salesOrderNumber}</span>
                            <span className="whitespace-nowrap">{shp.pickNumber}</span>
                          </div>
                        </td>

                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.shipmentsPanel.colCustomerDelivery}
                          </span>
                          <div className="font-medium text-[var(--text-primary)]">
                            <span className="font-code">{shp.customerCode}</span> · {shp.customerName}
                          </div>
                          <div className="text-[11px] text-[var(--text-secondary)]">
                            {ft.shipmentsPanel.sjPrefix}{" "}
                            <span className="font-code font-semibold text-[var(--text-primary)]">
                              {shp.deliveryNoteReference}
                            </span>
                            {shp.carrierName ? ` · ${shp.carrierName}` : ""}
                            {shp.trackingOrVehicleNumber ? ` (${shp.trackingOrVehicleNumber})` : ""}
                          </div>
                        </td>

                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.shipmentsPanel.colItemsQty}
                          </span>
                          {shp.lines.map((l) => (
                            <div key={l.shipmentLineId} className="font-code text-[11px] tabular-nums text-[var(--text-primary)]">
                              <div className="whitespace-nowrap font-bold">{l.itemCode}</div>
                              <div className="whitespace-nowrap text-[var(--text-secondary)]">
                                {l.dispatchedBaseQuantity} {l.baseUomCode} @ {l.locationCode}
                                {l.lotNumber ? ` (${l.lotNumber})` : ""}
                              </div>
                            </div>
                          ))}
                        </td>

                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2 md:text-right">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.shipmentsPanel.colCogsLedger}
                          </span>
                          <div className="font-code whitespace-nowrap font-bold tabular-nums text-[var(--text-primary)]">
                            {formatIdr(shp.totalCogsAmount)}
                          </div>
                          <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {ft.shipmentsPanel.movementPrefix} {shp.stockMovementNumber}
                          </div>
                          <div className="font-code whitespace-nowrap text-[11px] tabular-nums text-[var(--text-secondary)]">
                            {ft.shipmentsPanel.journalPrefix} {shp.glJournalNumber}
                          </div>
                        </td>

                        <td className="block py-1 md:table-cell md:py-2.5 md:px-2">
                          <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                            {ft.shipmentsPanel.colStatusEvidence}
                          </span>
                          <div>
                            <FulfillmentStatusBadge
                              status={shp.status}
                              label={ft.statusLabels[shp.status] ?? shp.status}
                            />
                          </div>
                          {shp.deliveryEvidenceReference ? (
                            <div className="mt-0.5 text-[11px] text-[var(--text-secondary)]">
                              {ft.shipmentsPanel.podPrefix}{" "}
                              <span className="font-code font-semibold text-[var(--text-primary)]">
                                {shp.deliveryEvidenceReference}
                              </span>{" "}
                              ({shp.deliveryConfirmedByName})
                            </div>
                          ) : null}
                        </td>

                        {canManage ? (
                          <td className={`${canDeliver ? "block py-1" : "hidden"} md:table-cell md:py-2.5 md:pl-2 md:pr-4 md:text-right`}>
                            {canDeliver ? (
                              <>
                                <span className="block font-medium text-[var(--text-secondary)] md:hidden">
                                  {ft.shipmentsPanel.colAction}
                                </span>
                                <button
                                  type="button"
                                  data-testid={`open-deliver-${shp.shipmentNumber}`}
                                  onClick={() => openDeliverDrawer(shp)}
                                  className="inline-flex min-h-9 items-center gap-1 whitespace-nowrap rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                                >
                                  <CheckCircle2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                                  <span>{ft.shipmentsPanel.openDeliverDrawerBtn}</span>
                                </button>
                              </>
                            ) : null}
                          </td>
                        ) : null}
                      </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}
    </div>
  );
}
