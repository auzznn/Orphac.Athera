import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type CurrentUserResponse = components["schemas"]["CurrentUserResponse"];
export type AuthSessionResponse = components["schemas"]["AuthSessionResponse"];
export type HealthResponse = components["schemas"]["HealthResponse"];
export type WorkspacesResponse = components["schemas"]["WorkspacesResponse"];
export type AuthorizedTenantDto = components["schemas"]["AuthorizedTenantDto"];
export type LegalEntityDto = components["schemas"]["LegalEntityDto"];
export type TenantEntitiesResponse = components["schemas"]["TenantEntitiesResponse"];
export type EntityWorkspaceDetailResponse = components["schemas"]["EntityWorkspaceDetailResponse"];
export type ModuleNavigationItemDto = components["schemas"]["ModuleNavigationItemDto"];

export type MfaSetupResponse = components["schemas"]["MfaSetupResponse"];
export type MfaVerifyResponse = components["schemas"]["MfaVerifyResponse"];
export type AdminOverviewResponse = components["schemas"]["AdminOverviewResponse"];
export type EntityMemberDto = components["schemas"]["EntityMemberDto"];
export type InvitationDto = components["schemas"]["InvitationDto"];
export type ApprovalPolicyDto = components["schemas"]["ApprovalPolicyDto"];
export type AuditEventDto = components["schemas"]["AuditEventDto"];
export type OutboxSummaryDto = components["schemas"]["OutboxSummaryDto"];
export type CreateInvitationRequest = components["schemas"]["CreateInvitationRequest"];
export type CreateInvitationResponse = components["schemas"]["CreateInvitationResponse"];
export type AcceptInvitationResponse = components["schemas"]["AcceptInvitationResponse"];
export type ApprovalCaseDto = components["schemas"]["ApprovalCaseDto"];
export type ApprovalCasesResponse = components["schemas"]["ApprovalCasesResponse"];
export type CreateApprovalCaseRequest = components["schemas"]["CreateApprovalCaseRequest"];
export type DecideApprovalCaseRequest = components["schemas"]["DecideApprovalCaseRequest"];
export type OutboxDispatchResponse = components["schemas"]["OutboxDispatchResponse"];

export type PartnerContactDto = components["schemas"]["PartnerContactDto"];
export type PartnerEntityProfileDto = components["schemas"]["PartnerEntityProfileDto"];
export type PartnerListItemDto = components["schemas"]["PartnerListItemDto"];
export type PartnerDetailDto = components["schemas"]["PartnerDetailDto"];
export type PagedPartnersResponse = components["schemas"]["PagedPartnersResponse"];
export type CreatePartnerRequest = components["schemas"]["CreatePartnerRequest"];
export type UpdatePartnerRequest = components["schemas"]["UpdatePartnerRequest"];
export type CreatePartnerContactRequest = components["schemas"]["CreatePartnerContactRequest"];
export type UpsertPartnerEntityProfileRequest = components["schemas"]["UpsertPartnerEntityProfileRequest"];

export type UnitOfMeasureDto = components["schemas"]["UnitOfMeasureDto"];
export type UomListResponse = components["schemas"]["UomListResponse"];
export type CreateUomRequest = components["schemas"]["CreateUomRequest"];
export type ItemUomDto = components["schemas"]["ItemUomDto"];
export type ItemEntityPolicyDto = components["schemas"]["ItemEntityPolicyDto"];
export type ItemListItemDto = components["schemas"]["ItemListItemDto"];
export type ItemDetailDto = components["schemas"]["ItemDetailDto"];
export type PagedItemsResponse = components["schemas"]["PagedItemsResponse"];
export type CreateItemRequest = components["schemas"]["CreateItemRequest"];
export type UpdateItemRequest = components["schemas"]["UpdateItemRequest"];
export type UpsertItemUomRequest = components["schemas"]["UpsertItemUomRequest"];
export type UpsertItemEntityPolicyRequest = components["schemas"]["UpsertItemEntityPolicyRequest"];
export type ConvertQuantityPreviewRequest = components["schemas"]["ConvertQuantityPreviewRequest"];
export type ConvertQuantityPreviewResponse = components["schemas"]["ConvertQuantityPreviewResponse"];

export interface ApiProblemError {
  status: number;
  errorCode: string;
  title: string;
  detail: string;
  traceId?: string;
}

const rawClient = createClient<paths>({
  baseUrl: "",
  credentials: "include",
});

let cachedCsrfToken: string | null = null;

export function setCsrfToken(token: string | null): void {
  cachedCsrfToken = token;
}

export function extractProblemError(error: unknown, fallbackStatus: number): ApiProblemError {
  if (error && typeof error === "object") {
    const rec = error as Record<string, unknown>;
    return {
      status: typeof rec.status === "number" ? rec.status : fallbackStatus,
      errorCode: typeof rec.errorCode === "string" ? rec.errorCode : "unknown",
      title: typeof rec.title === "string" ? rec.title : "Request failed",
      detail: typeof rec.detail === "string" ? rec.detail : "Unexpected API response.",
      traceId: typeof rec.traceId === "string" ? rec.traceId : undefined,
    };
  }

  return {
    status: fallbackStatus,
    errorCode: "unknown",
    title: "Request failed",
    detail: "Unexpected API response.",
  };
}

export async function ensureCsrfToken(): Promise<string> {
  if (cachedCsrfToken) {
    return cachedCsrfToken;
  }

  const { data, error, response } = await rawClient.GET("/api/auth/csrf", {
    cache: "no-store",
  });

  if (error || !data) {
    throw extractProblemError(error, (response as Response).status);
  }

  cachedCsrfToken = data.requestToken;
  return data.requestToken;
}

function handleCsrfFailure(status: number): void {
  if (status === 403) {
    cachedCsrfToken = null;
  }
}

export async function fetchHealth(): Promise<HealthResponse> {
  const { data, error, response } = await rawClient.GET("/api/health", { cache: "no-store" });
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchCurrentUser(): Promise<CurrentUserResponse | null> {
  const { data, error, response } = await rawClient.GET("/api/auth/me", { cache: "no-store" });
  if (response.status === 401) return null;
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchWorkspaces(): Promise<WorkspacesResponse> {
  const { data, error, response } = await rawClient.GET("/api/workspaces", { cache: "no-store" });
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchTenantEntities(tenantId: string): Promise<TenantEntitiesResponse> {
  const { data, error, response } = await rawClient.GET("/api/workspaces/{tenantId}/entities", {
    params: { path: { tenantId } },
    cache: "no-store",
  });
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchEntityWorkspaceDetail(
  tenantId: string,
  entityId: string,
  requirePermission?: string,
): Promise<EntityWorkspaceDetailResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}",
    {
      params: {
        path: { tenantId, entityId },
        query: requirePermission ? { requirePermission } : undefined,
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function loginWithPassword(
  email: string,
  password: string,
  rememberMe: boolean,
): Promise<AuthSessionResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST("/api/auth/login", {
    headers: { "X-CSRF-Token": csrf },
    body: { email, password, rememberMe },
    cache: "no-store",
  });

  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }

  cachedCsrfToken = data.csrfToken;
  return data;
}

export async function logoutSession(): Promise<void> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST("/api/auth/logout", {
    headers: { "X-CSRF-Token": csrf },
    cache: "no-store",
  });

  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }

  cachedCsrfToken = data.csrfToken;
}

export async function setupMfa(): Promise<MfaSetupResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST("/api/auth/mfa/setup", {
    headers: { "X-CSRF-Token": csrf },
    cache: "no-store",
  });
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function verifyMfa(code: string): Promise<MfaVerifyResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST("/api/auth/mfa/verify", {
    headers: { "X-CSRF-Token": csrf },
    body: { code },
    cache: "no-store",
  });
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function fetchEntityAdminOverview(
  tenantId: string,
  entityId: string,
): Promise<AdminOverviewResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/admin/overview",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createEntityInvitation(
  tenantId: string,
  entityId: string,
  payload: CreateInvitationRequest,
): Promise<CreateInvitationResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/admin/invitations",
    {
      params: { path: { tenantId, entityId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function revokeEntityInvitation(
  tenantId: string,
  entityId: string,
  invitationId: string,
): Promise<void> {
  const csrf = await ensureCsrfToken();
  const { error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/admin/invitations/{invitationId}/revoke",
    {
      params: { path: { tenantId, entityId, invitationId } },
      headers: { "X-CSRF-Token": csrf },
      cache: "no-store",
    },
  );
  if (error) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
}

export async function acceptInvitation(token: string): Promise<AcceptInvitationResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST("/api/invitations/accept", {
    headers: { "X-CSRF-Token": csrf },
    body: { token },
    cache: "no-store",
  });
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function grantEntityPermission(
  tenantId: string,
  entityId: string,
  targetUserId: string,
  permissionCode: string,
): Promise<void> {
  const csrf = await ensureCsrfToken();
  const { error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/admin/grants",
    {
      params: { path: { tenantId, entityId } },
      headers: { "X-CSRF-Token": csrf },
      body: { targetUserId, permissionCode },
      cache: "no-store",
    },
  );
  if (error) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
}

export async function updateMemberStatus(
  tenantId: string,
  entityId: string,
  targetUserId: string,
  action: "suspend" | "revoke",
): Promise<void> {
  const csrf = await ensureCsrfToken();
  const path =
    action === "revoke"
      ? "/api/workspaces/{tenantId}/entities/{entityId}/admin/members/{targetUserId}/revoke"
      : "/api/workspaces/{tenantId}/entities/{entityId}/admin/members/{targetUserId}/suspend";
  const { error, response } = await rawClient.POST(path, {
    params: { path: { tenantId, entityId, targetUserId } },
    headers: { "X-CSRF-Token": csrf },
    cache: "no-store",
  });
  if (error) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
}

export async function fetchApprovalCases(
  tenantId: string,
  entityId: string,
): Promise<ApprovalCasesResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/approvals/cases",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createApprovalCase(
  tenantId: string,
  entityId: string,
  payload: CreateApprovalCaseRequest,
  idempotencyKey: string,
): Promise<ApprovalCaseDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/approvals/cases",
    {
      params: { path: { tenantId, entityId } },
      headers: {
        "X-CSRF-Token": csrf,
        "Idempotency-Key": idempotencyKey,
      },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function decideApprovalCase(
  tenantId: string,
  entityId: string,
  caseId: string,
  payload: DecideApprovalCaseRequest,
): Promise<ApprovalCaseDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/approvals/cases/{caseId}/decide",
    {
      params: { path: { tenantId, entityId, caseId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function dispatchEntityOutbox(
  tenantId: string,
  entityId: string,
): Promise<OutboxDispatchResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/admin/outbox/dispatch",
    {
      params: { path: { tenantId, entityId } },
      headers: { "X-CSRF-Token": csrf },
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function fetchPartners(
  tenantId: string,
  entityId: string,
  filters?: {
    q?: string;
    role?: string;
    status?: string;
    eligibleOnly?: boolean;
    page?: number;
    pageSize?: number;
  },
): Promise<PagedPartnersResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/partners",
    {
      params: {
        path: { tenantId, entityId },
        query: {
          q: filters?.q || undefined,
          role: filters?.role || undefined,
          status: filters?.status || undefined,
          eligibleOnly: filters?.eligibleOnly || undefined,
          page: filters?.page ?? 1,
          pageSize: filters?.pageSize ?? 50,
        },
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchPartnerDetail(
  tenantId: string,
  entityId: string,
  partnerId: string,
): Promise<PartnerDetailDto> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/partners/{partnerId}",
    {
      params: { path: { tenantId, entityId, partnerId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createPartner(
  tenantId: string,
  entityId: string,
  payload: CreatePartnerRequest,
): Promise<PartnerDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/partners",
    {
      params: { path: { tenantId, entityId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function updatePartner(
  tenantId: string,
  entityId: string,
  partnerId: string,
  payload: UpdatePartnerRequest,
): Promise<PartnerDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/partners/{partnerId}",
    {
      params: { path: { tenantId, entityId, partnerId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function createPartnerContact(
  tenantId: string,
  entityId: string,
  partnerId: string,
  payload: CreatePartnerContactRequest,
): Promise<PartnerDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/partners/{partnerId}/contacts",
    {
      params: { path: { tenantId, entityId, partnerId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function upsertPartnerEntityProfile(
  tenantId: string,
  entityId: string,
  partnerId: string,
  payload: UpsertPartnerEntityProfileRequest,
): Promise<PartnerDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/partners/{partnerId}/entity-profile",
    {
      params: { path: { tenantId, entityId, partnerId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function fetchUnitsOfMeasure(
  tenantId: string,
  entityId: string,
): Promise<UomListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/uoms",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createUnitOfMeasure(
  tenantId: string,
  entityId: string,
  payload: CreateUomRequest,
): Promise<UnitOfMeasureDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/uoms",
    {
      params: { path: { tenantId, entityId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function fetchCatalogItems(
  tenantId: string,
  entityId: string,
  filters?: {
    q?: string;
    itemKind?: string;
    status?: string;
    eligibleOnly?: boolean;
    page?: number;
    pageSize?: number;
  },
): Promise<PagedItemsResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/items",
    {
      params: {
        path: { tenantId, entityId },
        query: {
          q: filters?.q || undefined,
          itemKind: filters?.itemKind || undefined,
          status: filters?.status || undefined,
          eligibleOnly: filters?.eligibleOnly || undefined,
          page: filters?.page ?? 1,
          pageSize: filters?.pageSize ?? 50,
        },
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchCatalogItemDetail(
  tenantId: string,
  entityId: string,
  itemId: string,
): Promise<ItemDetailDto> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/items/{itemId}",
    {
      params: { path: { tenantId, entityId, itemId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createCatalogItem(
  tenantId: string,
  entityId: string,
  payload: CreateItemRequest,
): Promise<ItemDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/items",
    {
      params: { path: { tenantId, entityId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function updateCatalogItem(
  tenantId: string,
  entityId: string,
  itemId: string,
  payload: UpdateItemRequest,
): Promise<ItemDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/items/{itemId}",
    {
      params: { path: { tenantId, entityId, itemId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function upsertItemUomConversion(
  tenantId: string,
  entityId: string,
  itemId: string,
  payload: UpsertItemUomRequest,
): Promise<ItemDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/items/{itemId}/conversions",
    {
      params: { path: { tenantId, entityId, itemId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function upsertItemEntityPolicy(
  tenantId: string,
  entityId: string,
  itemId: string,
  payload: UpsertItemEntityPolicyRequest,
): Promise<ItemDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/items/{itemId}/entity-policy",
    {
      params: { path: { tenantId, entityId, itemId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}

export async function previewItemUomConversion(
  tenantId: string,
  entityId: string,
  itemId: string,
  payload: ConvertQuantityPreviewRequest,
): Promise<ConvertQuantityPreviewResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/catalog/items/{itemId}/convert-preview",
    {
      params: { path: { tenantId, entityId, itemId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload,
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    handleCsrfFailure(status);
    throw extractProblemError(error, status);
  }
  return data;
}
