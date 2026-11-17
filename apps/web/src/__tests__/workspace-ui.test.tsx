import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import WorkspacesDirectoryPage from "@/app/(shell)/workspaces/page";
import EntityWorkspacePage from "@/app/(shell)/w/[tenantId]/[entityId]/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  ApiProblemError,
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
  WorkspacesResponse,
} from "@/lib/api/client";

const mockUser: CurrentUserResponse = {
  userId: "11111111-1111-1111-1111-111111111101",
  email: "rina.utami@demo.athera.local",
  displayName: "Rina Utami (Demo Procurement)",
  preferredLocale: "id-ID",
  lastSignedInAtUtc: "2026-11-18T00:00:00Z",
  workspaceAccessStatus: "ACTIVE_WORKSPACES",
  authorizedWorkspaceCount: 1,
};

let mockWorkspaces: WorkspacesResponse = {
  tenants: [
    {
      tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01",
      code: "NUSA",
      name: "PT Nusantara Niaga Utama",
      status: "ACTIVE",
      entities: [
        {
          legalEntityId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101",
          tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01",
          code: "NUSA-JKT",
          name: "PT Nusantara Distribusi Jakarta",
          functionalCurrencyCode: "IDR",
          countryCode: "ID",
          timeZoneId: "Asia/Jakarta",
          taxRegistrationNpwp: "01.234.567.8-011.000",
          status: "ACTIVE",
          permissions: ["procurement.read", "workspace.view"],
        },
      ],
    },
  ],
  totalAuthorizedEntities: 1,
  evaluatedAtUtc: "2026-11-18T00:00:00Z",
};

const fetchDetailMock = vi.fn();

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = {
  tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01",
  entityId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101",
};

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/components/AppShell", () => ({
  useSessionUser: () => mockUser,
  useWorkspaces: () => mockWorkspaces,
}));

vi.mock("@/lib/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/client")>("@/lib/api/client");
  return {
    ...actual,
    fetchEntityWorkspaceDetail: (...args: unknown[]) => fetchDetailMock(...args),
  };
});

describe("S01 Workspaces and Entity Sheet UI", () => {
  it("renders authorized tenants and entities in Indonesian ledger table", () => {
    render(
      <LocaleProvider>
        <WorkspacesDirectoryPage />
      </LocaleProvider>,
    );

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Direktori Ruang Kerja");
    expect(screen.getByText("NUSA-JKT")).toBeInTheDocument();
    expect(screen.getByText("PT Nusantara Distribusi Jakarta")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Masuk Ruang Kerja/i })).toHaveAttribute(
      "href",
      "/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101",
    );
  });

  it("renders no-access empty state when user has zero workspaces", () => {
    mockWorkspaces = {
      tenants: [],
      totalAuthorizedEntities: 0,
      evaluatedAtUtc: "2026-11-18T00:00:00Z",
    };

    render(
      <LocaleProvider>
        <WorkspacesDirectoryPage />
      </LocaleProvider>,
    );

    expect(screen.getByText("Belum Ada Ruang Kerja yang Diotorisasi")).toBeInTheDocument();
    expect(screen.getByText("rina.utami@demo.athera.local")).toBeInTheDocument();
  });

  it("renders entity profile ledger on 200 and non-leaking boundary on 404", async () => {
    const detailPayload: EntityWorkspaceDetailResponse = {
      tenant: {
        tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01",
        code: "NUSA",
        name: "PT Nusantara Niaga Utama",
        status: "ACTIVE",
      },
      entity: {
        legalEntityId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101",
        tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01",
        code: "NUSA-JKT",
        name: "PT Nusantara Distribusi Jakarta",
        functionalCurrencyCode: "IDR",
        countryCode: "ID",
        timeZoneId: "Asia/Jakarta",
        taxRegistrationNpwp: "01.234.567.8-011.000",
        status: "ACTIVE",
        permissions: ["procurement.read", "workspace.view"],
      },
      effectivePermissions: ["procurement.read", "workspace.view"],
      navigationCapabilities: [
        { key: "overview", status: "AVAILABLE", permitted: true, requiredPermission: "workspace.view" },
        { key: "procurement", status: "PLANNED_S03_S08", permitted: true, requiredPermission: "procurement.read" },
        { key: "finance", status: "PLANNED_S10_S13", permitted: false, requiredPermission: "finance.read" },
      ],
      serverTimestampUtc: "2026-11-18T00:00:00Z",
    };

    fetchDetailMock.mockResolvedValueOnce(detailPayload);

    const { rerender } = render(
      <LocaleProvider>
        <EntityWorkspacePage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("PT Nusantara Distribusi Jakarta");
    });
    expect(screen.getByText("01.234.567.8-011.000")).toBeInTheDocument();

    const err404: ApiProblemError = {
      status: 404,
      errorCode: "entity_not_found",
      title: "Entity not found",
      detail: "Outside your active scope.",
    };
    fetchDetailMock.mockRejectedValueOnce(err404);

    // Trigger clean unmount/remount to test unavailable boundary
    rerender(<div />);
    rerender(
      <LocaleProvider>
        <EntityWorkspacePage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("Ruang Kerja Tidak Tersedia");
    });
  });
});
