import React from "react";
import { render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import PartnersPage from "@/app/(shell)/w/[tenantId]/[entityId]/partners/page";
import CatalogPage from "@/app/(shell)/w/[tenantId]/[entityId]/catalog/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  PagedItemsResponse,
  PagedPartnersResponse,
  UomListResponse,
} from "@/lib/api/client";

const TENANT_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01";
const ENTITY_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101";

const mockUser: CurrentUserResponse = {
  userId: "11111111-1111-1111-1111-111111111101",
  email: "rina.utami@demo.athera.local",
  displayName: "Rina Utami",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-18T00:00:00Z",
  workspaceAccessStatus: "ACTIVE_WORKSPACES",
  authorizedWorkspaceCount: 1,
  mfaEnabled: false,
};

let currentPermissions = ["workspace.view", "partners.read", "partners.manage", "catalog.read", "catalog.manage"];

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };
const fetchDetailMock = vi.fn(async (): Promise<EntityWorkspaceDetailResponse> => ({
  tenant: { tenantId: TENANT_ID, code: "NUSA", name: "PT Nusantara Niaga Utama", status: "ACTIVE" },
  entity: {
    legalEntityId: ENTITY_ID,
    tenantId: TENANT_ID,
    code: "NUSA-JKT",
    name: "PT Nusantara Distribusi Jakarta",
    functionalCurrencyCode: "IDR",
    countryCode: "ID",
    timeZoneId: "Asia/Jakarta",
    taxRegistrationNpwp: "01.234.567.8-011.000",
    status: "ACTIVE",
    permissions: currentPermissions,
  },
  effectivePermissions: currentPermissions,
  navigationCapabilities: [],
  serverTimestampUtc: "2026-11-18T00:00:00Z",
}));

const fetchPartnersMock = vi.fn();
const fetchUomsMock = vi.fn();
const fetchItemsMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/components/AppShell", () => ({
  useSessionUser: () => mockUser,
  useRefreshShell: () => vi.fn().mockResolvedValue(undefined),
}));

vi.mock("@/lib/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/client")>("@/lib/api/client");
  return {
    ...actual,
    fetchEntityWorkspaceDetail: () => fetchDetailMock(),
    fetchPartners: () => fetchPartnersMock(),
    fetchUnitsOfMeasure: () => fetchUomsMock(),
    fetchCatalogItems: () => fetchItemsMock(),
  };
});

describe("S03 Partners, Catalogue and UoM UI", () => {
  it("renders dual-role partner ledger with OD-07 notice and enforces read-only view cleanly", async () => {
    currentPermissions = ["workspace.view", "partners.read"];
    const payload: PagedPartnersResponse = {
      items: [
        {
          partnerId: "p1",
          partnerCode: "PT-DUAL-001",
          legalName: "PT Mitra Niaga Nusantara",
          displayName: "Mitra Niaga Nusantara",
          isSupplier: true,
          isCustomer: true,
          taxRegistrationNpwp: "01.999.456.7-011.000",
          taxPkpStatus: "PKP",
          entityScopeMode: "RESTRICTED_ENTITIES",
          status: "ACTIVE",
          effectiveSupplierEligible: false,
          effectiveCustomerEligible: false,
          contactCount: 1,
          updatedAtUtc: "2026-11-18T00:00:00Z",
          version: 1,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 50,
    };
    fetchPartnersMock.mockResolvedValueOnce(payload);

    render(
      <LocaleProvider>
        <PartnersPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Mitra Bisnis (Pemasok & Pelanggan)");
    });
    expect(screen.getByText(/OD-07/i)).toBeInTheDocument();
    expect(screen.getByText("PT-DUAL-001")).toBeInTheDocument();
    expect(screen.getByText("Pemasok & Pelanggan")).toBeInTheDocument();
    expect(screen.getByText("Tidak Layak di Entitas Ini")).toBeInTheDocument();
    expect(screen.getByText(/Mode baca saja/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Simpan Mitra Baru/ })).not.toBeInTheDocument();
  });

  it("renders catalogue items, UoM dictionary, and manages button visibility by permission", async () => {
    currentPermissions = ["workspace.view", "catalog.read", "catalog.manage"];
    const uoms: UomListResponse = {
      items: [
        { uomId: "u-pcs", uomCode: "PCS", name: "Pieces", quantityPrecisionScale: 0, status: "ACTIVE", updatedAtUtc: "2026-11-18T00:00:00Z", version: 1 },
        { uomId: "u-box", uomCode: "BOX", name: "Box Karton", quantityPrecisionScale: 0, status: "ACTIVE", updatedAtUtc: "2026-11-18T00:00:00Z", version: 1 },
      ],
    };
    const items: PagedItemsResponse = {
      items: [
        {
          itemId: "i-1",
          itemCode: "ITM-PKG-001",
          name: "Karton Lipat Polos 40x30x25 cm",
          itemKind: "STOCKED_GOOD",
          baseUomId: "u-pcs",
          baseUomCode: "PCS",
          basePrecisionScale: 0,
          defaultPurchaseUomId: "u-box",
          defaultPurchaseUomCode: "BOX",
          defaultSalesUomId: "u-pcs",
          defaultSalesUomCode: "PCS",
          entityScopeMode: "ALL_ENTITIES",
          status: "ACTIVE",
          effectivePurchaseAllowed: true,
          effectiveSalesAllowed: true,
          conversionCount: 1,
          updatedAtUtc: "2026-11-18T00:00:00Z",
          version: 1,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 50,
    };
    fetchUomsMock.mockResolvedValueOnce(uoms);
    fetchItemsMock.mockResolvedValueOnce(items);

    render(
      <LocaleProvider>
        <CatalogPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Katalog Barang & Satuan UoM");
    });
    const itemRow = screen.getByRole("row", { name: /ITM-PKG-001/ });
    expect(within(itemRow).getByText("Barang Stok")).toBeInTheDocument();
    expect(within(itemRow).getByText("Beli Diizinkan")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Simpan Item Katalog" })).toBeInTheDocument();
  });
});
