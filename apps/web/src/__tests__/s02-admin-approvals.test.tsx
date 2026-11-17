import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ApprovalsPage from "@/app/(shell)/w/[tenantId]/[entityId]/approvals/page";
import EntityAdminPage from "@/app/(shell)/w/[tenantId]/[entityId]/admin/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import type {
  AdminOverviewResponse,
  ApprovalCasesResponse,
  CurrentUserResponse,
  EntityWorkspaceDetailResponse,
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

const mockDetail: EntityWorkspaceDetailResponse = {
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
    permissions: ["workspace.view", "approval.request", "approval.decide", "admin.read", "admin.manage"],
  },
  effectivePermissions: ["workspace.view", "approval.request", "approval.decide", "admin.read", "admin.manage"],
  navigationCapabilities: [],
  serverTimestampUtc: "2026-11-18T00:00:00Z",
};

const mockRouter = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
const mockParams = { tenantId: TENANT_ID, entityId: ENTITY_ID };
const fetchDetailMock = vi.fn().mockResolvedValue(mockDetail);
const fetchApprovalCasesMock = vi.fn();
const fetchAdminOverviewMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
  useParams: () => mockParams,
}));

vi.mock("@/components/AppShell", () => ({
  useSessionUser: () => mockUser,
  useRefreshShell: () => vi.fn(),
}));

vi.mock("@/lib/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/client")>("@/lib/api/client");
  return {
    ...actual,
    fetchEntityWorkspaceDetail: (...args: unknown[]) => fetchDetailMock(...args),
    fetchApprovalCases: (...args: unknown[]) => fetchApprovalCasesMock(...args),
    fetchEntityAdminOverview: (...args: unknown[]) => fetchAdminOverviewMock(...args),
  };
});

describe("S02 Approvals and Admin UI", () => {
  it("renders four-eyes block notice on requester own case and distinct Setujui/Tolak on peer case", async () => {
    const payload: ApprovalCasesResponse = {
      policies: [
        {
          policyId: "p1",
          policyCode: "SENSITIVE_ACCESS_CHANGE",
          name: "Sensitive Access Change",
          requiresFourEyes: true,
          requiresApproverMfa: false,
          isActive: true,
        },
      ],
      cases: [
        {
          caseId: "case-self",
          tenantId: TENANT_ID,
          legalEntityId: ENTITY_ID,
          policyId: "p1",
          policyCode: "SENSITIVE_ACCESS_CHANGE",
          requiresApproverMfa: false,
          actionType: "ENTITY_ACCESS_GRANT_REQUEST",
          targetUserId: mockUser.userId,
          targetPermissionCode: "inventory.read",
          requestReason: "Self-submitted item",
          status: "PENDING",
          requestedByUserId: mockUser.userId,
          requestedByName: "Rina Utami",
          requestedAtUtc: "2026-11-18T00:00:00Z",
          decidedByUserId: null,
          decidedByName: null,
          decidedAtUtc: null,
          decisionReason: null,
          approverMfaVerified: null,
        },
        {
          caseId: "case-peer",
          tenantId: TENANT_ID,
          legalEntityId: ENTITY_ID,
          policyId: "p1",
          policyCode: "HIGH_RISK_PRIVILEGE_ESCALATION",
          requiresApproverMfa: true,
          actionType: "ENTITY_ACCESS_GRANT_REQUEST",
          targetUserId: "22222222-2222-2222-2222-222222222202",
          targetPermissionCode: "finance.read",
          requestReason: "Peer request",
          status: "PENDING",
          requestedByUserId: "22222222-2222-2222-2222-222222222202",
          requestedByName: "Budi Santoso",
          requestedAtUtc: "2026-11-18T00:05:00Z",
          decidedByUserId: null,
          decidedByName: null,
          decidedAtUtc: null,
          decisionReason: null,
          approverMfaVerified: null,
        },
      ],
    };
    fetchApprovalCasesMock.mockResolvedValueOnce(payload);

    render(
      <LocaleProvider>
        <ApprovalsPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Aturan Empat Mata \(OD-02\)/)).toBeInTheDocument();
    });
    expect(screen.getByRole("button", { name: /Setujui/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Tolak/ })).toBeInTheDocument();
    expect(screen.getByText("Wajib MFA")).toBeInTheDocument();
  });

  it("renders entity admin outbox summary, members table, and append-only audit trail", async () => {
    const overviewPayload: AdminOverviewResponse = {
      members: [
        {
          userId: mockUser.userId,
          email: "rina.utami@demo.athera.local",
          displayName: "Rina Utami",
          membershipStatus: "ACTIVE",
          mfaEnabled: false,
          permissions: ["workspace.view", "admin.manage"],
        },
      ],
      invitations: [],
      policies: [],
      auditEvents: [
        {
          eventId: "e1",
          actorUserId: mockUser.userId,
          actorEmail: "rina.utami@demo.athera.local",
          eventType: "INVITATION_CREATED",
          subjectType: "UserInvitation",
          subjectId: "inv1",
          summary: "Invited staff@demo.athera.local",
          occurredAtUtc: "2026-11-18T00:00:00Z",
        },
      ],
      outboxSummary: { pendingCount: 0, processedCount: 3, failedCount: 0 },
      availablePermissions: ["workspace.view", "procurement.read"],
      availableRoleTemplates: ["PROCUREMENT_OFFICER"],
    };
    fetchAdminOverviewMock.mockResolvedValueOnce(overviewPayload);

    render(
      <LocaleProvider>
        <EntityAdminPage />
      </LocaleProvider>,
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Administrasi Entitas, Undangan & Audit");
    });
    expect(screen.getByText("Diproses:")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("INVITATION_CREATED")).toBeInTheDocument();
  });
});
