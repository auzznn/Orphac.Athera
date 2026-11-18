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

export type GlAccountDto = components["schemas"]["GlAccountDto"];
export type GlAccountListResponse = components["schemas"]["GlAccountListResponse"];
export type CreateGlAccountRequest = components["schemas"]["CreateGlAccountRequest"];
export type UpdateGlAccountRequest = components["schemas"]["UpdateGlAccountRequest"];
export type FinancialPeriodDto = components["schemas"]["FinancialPeriodDto"];
export type FinancialPeriodListResponse = components["schemas"]["FinancialPeriodListResponse"];
export type CreateFinancialPeriodRequest = components["schemas"]["CreateFinancialPeriodRequest"];
export type CloseFinancialPeriodRequest = components["schemas"]["CloseFinancialPeriodRequest"];
export type AccountMappingDto = components["schemas"]["AccountMappingDto"];
export type AccountMappingListResponse = components["schemas"]["AccountMappingListResponse"];
export type UpsertAccountMappingRequest = components["schemas"]["UpsertAccountMappingRequest"];
export type TaxCodeDto = components["schemas"]["TaxCodeDto"];
export type TaxCodeListResponse = components["schemas"]["TaxCodeListResponse"];
export type CreateTaxCodeRequest = components["schemas"]["CreateTaxCodeRequest"];
export type EvaluateTaxPreviewRequest = components["schemas"]["EvaluateTaxPreviewRequest"];
export type EvaluateTaxPreviewResponse = components["schemas"]["EvaluateTaxPreviewResponse"];
export type JournalLineInputDto = components["schemas"]["JournalLineInputDto"];
export type PostJournalRequest = components["schemas"]["PostJournalRequest"];
export type ReverseJournalRequest = components["schemas"]["ReverseJournalRequest"];
export type JournalListItemDto = components["schemas"]["JournalListItemDto"];
export type PagedJournalsResponse = components["schemas"]["PagedJournalsResponse"];
export type JournalLineDto = components["schemas"]["JournalLineDto"];
export type JournalDetailDto = components["schemas"]["JournalDetailDto"];

export type ProcurementReferenceDataResponse = components["schemas"]["ProcurementReferenceDataResponse"];
export type ProcurementSupplierOptionDto = components["schemas"]["ProcurementSupplierOptionDto"];
export type ProcurementItemOptionDto = components["schemas"]["ProcurementItemOptionDto"];
export type ProcurementItemUomOptionDto = components["schemas"]["ProcurementItemUomOptionDto"];
export type ProcurementTaxOptionDto = components["schemas"]["ProcurementTaxOptionDto"];
export type DeliveryTolerancePolicyDto = components["schemas"]["DeliveryTolerancePolicyDto"];
export type PurchaseOrderListResponse = components["schemas"]["PurchaseOrderListResponse"];
export type PurchaseOrderSummaryDto = components["schemas"]["PurchaseOrderSummaryDto"];
export type PurchaseOrderDetailDto = components["schemas"]["PurchaseOrderDetailDto"];
export type PurchaseOrderRevisionDto = components["schemas"]["PurchaseOrderRevisionDto"];
export type PurchaseOrderLineDto = components["schemas"]["PurchaseOrderLineDto"];
export type DocumentApprovalDto = components["schemas"]["DocumentApprovalDto"];
export type PoRevisionDiffReportDto = components["schemas"]["PoRevisionDiffReportDto"];
export type PoHeaderFieldDiffDto = components["schemas"]["PoHeaderFieldDiffDto"];
export type PoLineDiffItemDto = components["schemas"]["PoLineDiffItemDto"];
export type PoLineInputDto = components["schemas"]["PoLineInputDto"];
export type CreatePurchaseOrderRequest = components["schemas"]["CreatePurchaseOrderRequest"];
export type UpdatePurchaseOrderRevisionRequest = components["schemas"]["UpdatePurchaseOrderRevisionRequest"];
export type CreatePoAmendmentRequest = components["schemas"]["CreatePoAmendmentRequest"];
export type SubmitPoRevisionRequest = components["schemas"]["SubmitPoRevisionRequest"];
export type DecidePoRevisionRequest = components["schemas"]["DecidePoRevisionRequest"];
export type CloseOrCancelPoRequest = components["schemas"]["CloseOrCancelPoRequest"];
export type EligiblePoReceivingUomDto = components["schemas"]["EligiblePoReceivingUomDto"];
export type EligiblePoReceivingLineDto = components["schemas"]["EligiblePoReceivingLineDto"];
export type EligiblePurchaseOrderForReceivingDto =
  components["schemas"]["EligiblePurchaseOrderForReceivingDto"];
export type EligibleReceivingPosResponse = components["schemas"]["EligibleReceivingPosResponse"];
export type GoodsReceiptSummaryDto = components["schemas"]["GoodsReceiptSummaryDto"];
export type GoodsReceiptListResponse = components["schemas"]["GoodsReceiptListResponse"];
export type GoodsReceiptLineDto = components["schemas"]["GoodsReceiptLineDto"];
export type DockCheckHistoryDto = components["schemas"]["DockCheckHistoryDto"];
export type GoodsReceiptDetailDto = components["schemas"]["GoodsReceiptDetailDto"];
export type ArrivalLineRequest = components["schemas"]["ArrivalLineRequest"];
export type RecordGoodsReceiptRequest = components["schemas"]["RecordGoodsReceiptRequest"];
export type RecordDockCheckRequest = components["schemas"]["RecordDockCheckRequest"];

export type GoodsReceiptLinkedAcceptanceDto =
  components["schemas"]["GoodsReceiptLinkedAcceptanceDto"];
export type AcceptReceiptLineRequest = components["schemas"]["AcceptReceiptLineRequest"];
export type PreviewReceiptAcceptanceRequest =
  components["schemas"]["PreviewReceiptAcceptanceRequest"];
export type AcceptReceiptRequest = components["schemas"]["AcceptReceiptRequest"];
export type AcceptancePreviewLineDto = components["schemas"]["AcceptancePreviewLineDto"];
export type PreviewJournalSuggestionDto = components["schemas"]["PreviewJournalSuggestionDto"];
export type AcceptancePreviewDto = components["schemas"]["AcceptancePreviewDto"];
export type ValuedAcceptanceLineDto = components["schemas"]["ValuedAcceptanceLineDto"];
export type ValuedAcceptanceSummaryDto = components["schemas"]["ValuedAcceptanceSummaryDto"];
export type ValuedAcceptanceListResponse = components["schemas"]["ValuedAcceptanceListResponse"];
export type ValuedAcceptanceDetailDto = components["schemas"]["ValuedAcceptanceDetailDto"];
export type StockPositionOverviewDto = components["schemas"]["StockPositionOverviewDto"];
export type StockMovementSummaryDto = components["schemas"]["StockMovementSummaryDto"];
export type ReceiptAccrualOverviewDto = components["schemas"]["ReceiptAccrualOverviewDto"];
export type InventoryOverviewResponse = components["schemas"]["InventoryOverviewResponse"];
export type ValuationEntryLedgerDto = components["schemas"]["ValuationEntryLedgerDto"];
export type ItemValuationLedgerResponse = components["schemas"]["ItemValuationLedgerResponse"];
export type WarehouseDto = components["schemas"]["WarehouseDto"];
export type WarehouseLocationDto = components["schemas"]["WarehouseLocationDto"];
export type WarehouseListResponse = components["schemas"]["WarehouseListResponse"];
export type CreateWarehouseRequest = components["schemas"]["CreateWarehouseRequest"];
export type CreateWarehouseLocationRequest = components["schemas"]["CreateWarehouseLocationRequest"];
export type StockLotSummaryDto = components["schemas"]["StockLotSummaryDto"];
export type StockLocationBalanceDto = components["schemas"]["StockLocationBalanceDto"];
export type StockReservationDto = components["schemas"]["StockReservationDto"];
export type InspectionDispositionDto = components["schemas"]["InspectionDispositionDto"];
export type ReceiptInspectionSummaryDto = components["schemas"]["ReceiptInspectionSummaryDto"];
export type ItemReconciliationDto = components["schemas"]["ItemReconciliationDto"];
export type ItemMovementTraceDto = components["schemas"]["ItemMovementTraceDto"];
export type ItemStockDetailResponse = components["schemas"]["ItemStockDetailResponse"];
export type TransferStockLocationRequest = components["schemas"]["TransferStockLocationRequest"];
export type StockStatusTransitionRequest = components["schemas"]["StockStatusTransitionRequest"];
export type RecordInspectionDispositionRequest =
  components["schemas"]["RecordInspectionDispositionRequest"];
export type InspectionDispositionOutcomeDto = components["schemas"]["InspectionDispositionOutcomeDto"];
export type CreateStockReservationRequest = components["schemas"]["CreateStockReservationRequest"];
export type ReleaseStockReservationRequest = components["schemas"]["ReleaseStockReservationRequest"];
export type StockReservationOutcomeDto = components["schemas"]["StockReservationOutcomeDto"];
export type InventoryMovementResultDto = components["schemas"]["InventoryMovementResultDto"];
export type CreateOperationalIssueRequest = components["schemas"]["CreateOperationalIssueRequest"];
export type OperationalIssueOutcomeDto = components["schemas"]["OperationalIssueOutcomeDto"];

export type BillLineInputRequest = components["schemas"]["BillLineInputRequest"];
export type PreviewSupplierBillRequest = components["schemas"]["PreviewSupplierBillRequest"];
export type CreateSupplierBillRequest = components["schemas"]["CreateSupplierBillRequest"];
export type UpdateSupplierBillRequest = components["schemas"]["UpdateSupplierBillRequest"];
export type DecideSupplierBillRequest = components["schemas"]["DecideSupplierBillRequest"];
export type PostSupplierBillRequest = components["schemas"]["PostSupplierBillRequest"];
export type UpdateBillPaymentBlockRequest = components["schemas"]["UpdateBillPaymentBlockRequest"];
export type UpdateInvoiceMatchPolicyRequest = components["schemas"]["UpdateInvoiceMatchPolicyRequest"];
export type InvoiceMatchPolicyDto = components["schemas"]["InvoiceMatchPolicyDto"];
export type PayableTaxCodeDto = components["schemas"]["PayableTaxCodeDto"];
export type EligibleAccrualLineDto = components["schemas"]["EligibleAccrualLineDto"];
export type MatchedLineComparisonDto = components["schemas"]["MatchedLineComparisonDto"];
export type BillJournalSuggestionDto = components["schemas"]["BillJournalSuggestionDto"];
export type SupplierBillPreviewResponse = components["schemas"]["SupplierBillPreviewResponse"];
export type SupplierBillSummaryDto = components["schemas"]["SupplierBillSummaryDto"];
export type SupplierBillLineDto = components["schemas"]["SupplierBillLineDto"];
export type InvoiceMatchEvaluationDto = components["schemas"]["InvoiceMatchEvaluationDto"];
export type SupplierBillReceiptMatchDto = components["schemas"]["SupplierBillReceiptMatchDto"];
export type InvoicePriceAdjustmentDto = components["schemas"]["InvoicePriceAdjustmentDto"];
export type TaxDeterminationDto = components["schemas"]["TaxDeterminationDto"];
export type PayableItemDto = components["schemas"]["PayableItemDto"];
export type SupplierBillDetailResponse = components["schemas"]["SupplierBillDetailResponse"];
export type PayablesOverviewResponse = components["schemas"]["PayablesOverviewResponse"];
export type ReturnMatrixRuleDto = components["schemas"]["ReturnMatrixRuleDto"];
export type ReturnStockBalanceOptionDto = components["schemas"]["ReturnStockBalanceOptionDto"];
export type QueuedUnvaluedRejectionDto = components["schemas"]["QueuedUnvaluedRejectionDto"];
export type QueuedValuedUnbilledLineDto = components["schemas"]["QueuedValuedUnbilledLineDto"];
export type QueuedValuedBilledLineDto = components["schemas"]["QueuedValuedBilledLineDto"];
export type SupplierReturnSummaryDto = components["schemas"]["SupplierReturnSummaryDto"];
export type SupplierReturnLineDto = components["schemas"]["SupplierReturnLineDto"];
export type SupplierReturnEvaluatedLineDto = components["schemas"]["SupplierReturnEvaluatedLineDto"];
export type SupplierReturnJournalSuggestionDto = components["schemas"]["SupplierReturnJournalSuggestionDto"];
export type SupplierReturnPreviewResponse = components["schemas"]["SupplierReturnPreviewResponse"];
export type ReplacementAuthorizationDto = components["schemas"]["ReplacementAuthorizationDto"];
export type SupplierCreditDto = components["schemas"]["SupplierCreditDto"];
export type SupplierReturnDetailResponse = components["schemas"]["SupplierReturnDetailResponse"];
export type ReturnsOverviewResponse = components["schemas"]["ReturnsOverviewResponse"];
export type ReturnLineInputRequest = components["schemas"]["ReturnLineInputRequest"];
export type PreviewSupplierReturnRequest = components["schemas"]["PreviewSupplierReturnRequest"];
export type CreateSupplierReturnRequest = components["schemas"]["CreateSupplierReturnRequest"];
export type UpdateSupplierReturnRequest = components["schemas"]["UpdateSupplierReturnRequest"];
export type DecideSupplierReturnRequest = components["schemas"]["DecideSupplierReturnRequest"];
export type ExecuteSupplierReturnRequest = components["schemas"]["ExecuteSupplierReturnRequest"];
export type ReceiveReplacementRequest = components["schemas"]["ReceiveReplacementRequest"];

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

export async function fetchGlAccounts(
  tenantId: string,
  entityId: string,
): Promise<GlAccountListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/accounts",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createGlAccount(
  tenantId: string,
  entityId: string,
  payload: CreateGlAccountRequest,
): Promise<GlAccountDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/accounts",
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

export async function updateGlAccount(
  tenantId: string,
  entityId: string,
  accountId: string,
  payload: UpdateGlAccountRequest,
): Promise<GlAccountDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/accounts/{accountId}",
    {
      params: { path: { tenantId, entityId, accountId } },
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

export async function fetchFinancialPeriods(
  tenantId: string,
  entityId: string,
): Promise<FinancialPeriodListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/periods",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createFinancialPeriod(
  tenantId: string,
  entityId: string,
  payload: CreateFinancialPeriodRequest,
): Promise<FinancialPeriodDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/periods",
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

export async function closeFinancialPeriod(
  tenantId: string,
  entityId: string,
  periodId: string,
  payload: CloseFinancialPeriodRequest,
): Promise<FinancialPeriodDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/periods/{periodId}/close",
    {
      params: { path: { tenantId, entityId, periodId } },
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

export async function fetchAccountMappings(
  tenantId: string,
  entityId: string,
): Promise<AccountMappingListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/account-mappings",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function upsertAccountMapping(
  tenantId: string,
  entityId: string,
  payload: UpsertAccountMappingRequest,
): Promise<AccountMappingDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/account-mappings",
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

export async function fetchTaxCodes(
  tenantId: string,
  entityId: string,
): Promise<TaxCodeListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/tax-codes",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createTaxCode(
  tenantId: string,
  entityId: string,
  payload: CreateTaxCodeRequest,
): Promise<TaxCodeDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/tax-codes",
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

export async function evaluateTaxPreview(
  tenantId: string,
  entityId: string,
  payload: EvaluateTaxPreviewRequest,
): Promise<EvaluateTaxPreviewResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/tax-codes/evaluate-preview",
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

export async function fetchJournals(
  tenantId: string,
  entityId: string,
  filters?: {
    periodId?: string;
    entryKind?: string;
    q?: string;
    page?: number;
    pageSize?: number;
  },
): Promise<PagedJournalsResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/journals",
    {
      params: {
        path: { tenantId, entityId },
        query: {
          periodId: filters?.periodId || undefined,
          entryKind: filters?.entryKind || undefined,
          q: filters?.q || undefined,
          page: filters?.page ?? 1,
          pageSize: filters?.pageSize ?? 25,
        },
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchJournalDetail(
  tenantId: string,
  entityId: string,
  journalId: string,
): Promise<JournalDetailDto> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/journals/{journalId}",
    {
      params: { path: { tenantId, entityId, journalId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function postJournal(
  tenantId: string,
  entityId: string,
  payload: PostJournalRequest,
): Promise<JournalDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/journals",
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

export async function reverseJournal(
  tenantId: string,
  entityId: string,
  journalId: string,
  payload: ReverseJournalRequest,
): Promise<JournalDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/finance/journals/{journalId}/reverse",
    {
      params: { path: { tenantId, entityId, journalId } },
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

export async function fetchProcurementReferenceData(
  tenantId: string,
  entityId: string,
): Promise<ProcurementReferenceDataResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/reference-data",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchPurchaseOrders(
  tenantId: string,
  entityId: string,
  filters?: { status?: string; search?: string },
): Promise<PurchaseOrderListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders",
    {
      params: {
        path: { tenantId, entityId },
        query: {
          status: filters?.status || undefined,
          search: filters?.search || undefined,
        },
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchPurchaseOrderDetail(
  tenantId: string,
  entityId: string,
  poId: string,
): Promise<PurchaseOrderDetailDto> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{poId}",
    {
      params: { path: { tenantId, entityId, poId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createPurchaseOrder(
  tenantId: string,
  entityId: string,
  payload: CreatePurchaseOrderRequest,
): Promise<PurchaseOrderDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders",
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

export async function updatePurchaseOrderDraftRevision(
  tenantId: string,
  entityId: string,
  poId: string,
  revisionId: string,
  payload: UpdatePurchaseOrderRevisionRequest,
): Promise<PurchaseOrderDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{poId}/revisions/{revisionId}",
    {
      params: { path: { tenantId, entityId, poId, revisionId } },
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

export async function createPurchaseOrderAmendment(
  tenantId: string,
  entityId: string,
  poId: string,
  payload: CreatePoAmendmentRequest,
): Promise<PurchaseOrderDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{poId}/revisions",
    {
      params: { path: { tenantId, entityId, poId } },
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

export async function submitPurchaseOrderRevision(
  tenantId: string,
  entityId: string,
  poId: string,
  revisionId: string,
  payload?: SubmitPoRevisionRequest,
): Promise<PurchaseOrderDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{poId}/revisions/{revisionId}/submit",
    {
      params: { path: { tenantId, entityId, poId, revisionId } },
      headers: { "X-CSRF-Token": csrf },
      body: payload ?? { poNumberOverride: null },
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

export async function decidePurchaseOrderRevision(
  tenantId: string,
  entityId: string,
  poId: string,
  revisionId: string,
  payload: DecidePoRevisionRequest,
): Promise<PurchaseOrderDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{poId}/revisions/{revisionId}/decide",
    {
      params: { path: { tenantId, entityId, poId, revisionId } },
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

export async function cancelPurchaseOrder(
  tenantId: string,
  entityId: string,
  poId: string,
  payload: CloseOrCancelPoRequest,
): Promise<PurchaseOrderDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{poId}/cancel",
    {
      params: { path: { tenantId, entityId, poId } },
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

export async function shortClosePurchaseOrder(
  tenantId: string,
  entityId: string,
  poId: string,
  payload: CloseOrCancelPoRequest,
): Promise<PurchaseOrderDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{poId}/short-close",
    {
      params: { path: { tenantId, entityId, poId } },
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

export async function fetchEligibleReceivingPos(
  tenantId: string,
  entityId: string,
): Promise<EligibleReceivingPosResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/eligible-pos",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchGoodsReceipts(
  tenantId: string,
  entityId: string,
  filters?: { status?: string; purchaseOrderId?: string; search?: string },
): Promise<GoodsReceiptListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts",
    {
      params: {
        path: { tenantId, entityId },
        query: {
          status: filters?.status || undefined,
          purchaseOrderId: filters?.purchaseOrderId || undefined,
          search: filters?.search || undefined,
        },
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchGoodsReceiptDetail(
  tenantId: string,
  entityId: string,
  receiptId: string,
): Promise<GoodsReceiptDetailDto> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{receiptId}",
    {
      params: { path: { tenantId, entityId, receiptId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function recordGoodsReceiptArrival(
  tenantId: string,
  entityId: string,
  payload: RecordGoodsReceiptRequest,
): Promise<GoodsReceiptDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts",
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

export async function recordGoodsReceiptDockCheck(
  tenantId: string,
  entityId: string,
  receiptId: string,
  lineId: string,
  payload: RecordDockCheckRequest,
): Promise<GoodsReceiptDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{receiptId}/lines/{lineId}/dock-checks",
    {
      params: { path: { tenantId, entityId, receiptId, lineId } },
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

export async function previewReceiptAcceptance(
  tenantId: string,
  entityId: string,
  receiptId: string,
  payload: PreviewReceiptAcceptanceRequest,
): Promise<AcceptancePreviewDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{receiptId}/accept-preview",
    {
      params: { path: { tenantId, entityId, receiptId } },
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

export async function acceptGoodsReceiptValued(
  tenantId: string,
  entityId: string,
  receiptId: string,
  payload: AcceptReceiptRequest,
): Promise<ValuedAcceptanceDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{receiptId}/accept",
    {
      params: { path: { tenantId, entityId, receiptId } },
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

export async function fetchValuedAcceptances(
  tenantId: string,
  entityId: string,
  receiptId?: string,
): Promise<ValuedAcceptanceListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/receiving/acceptances",
    {
      params: {
        path: { tenantId, entityId },
        query: { receiptId: receiptId || undefined },
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchInventoryOverview(
  tenantId: string,
  entityId: string,
  filters?: { search?: string; stockStatus?: string },
): Promise<InventoryOverviewResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/overview",
    {
      params: {
        path: { tenantId, entityId },
        query: {
          search: filters?.search || undefined,
          stockStatus: filters?.stockStatus || undefined,
        },
      },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchItemValuationLedger(
  tenantId: string,
  entityId: string,
  itemId: string,
): Promise<ItemValuationLedgerResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/items/{itemId}/ledger",
    {
      params: { path: { tenantId, entityId, itemId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function listInventoryWarehouses(
  tenantId: string,
  entityId: string,
): Promise<WarehouseListResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/warehouses",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function createWarehouse(
  tenantId: string,
  entityId: string,
  payload: CreateWarehouseRequest,
): Promise<WarehouseDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/warehouses",
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

export async function createWarehouseLocation(
  tenantId: string,
  entityId: string,
  warehouseId: string,
  payload: CreateWarehouseLocationRequest,
): Promise<WarehouseLocationDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/warehouses/{warehouseId}/locations",
    {
      params: { path: { tenantId, entityId, warehouseId } },
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

export async function getItemStockDetail(
  tenantId: string,
  entityId: string,
  itemId: string,
): Promise<ItemStockDetailResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/items/{itemId}/detail",
    {
      params: { path: { tenantId, entityId, itemId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function transferInventoryLocation(
  tenantId: string,
  entityId: string,
  payload: TransferStockLocationRequest,
): Promise<InventoryMovementResultDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/transfers",
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

export async function recordInspectionDisposition(
  tenantId: string,
  entityId: string,
  inspectionId: string,
  payload: RecordInspectionDispositionRequest,
): Promise<InspectionDispositionOutcomeDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/inspections/{inspectionId}/dispositions",
    {
      params: { path: { tenantId, entityId, inspectionId } },
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

export async function transitionInventoryStockStatus(
  tenantId: string,
  entityId: string,
  payload: StockStatusTransitionRequest,
): Promise<InventoryMovementResultDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/status-transitions",
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

export async function createStockReservation(
  tenantId: string,
  entityId: string,
  payload: CreateStockReservationRequest,
): Promise<StockReservationOutcomeDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/reservations",
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

export async function releaseStockReservation(
  tenantId: string,
  entityId: string,
  reservationId: string,
  payload: ReleaseStockReservationRequest,
): Promise<StockReservationOutcomeDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/reservations/{reservationId}/release",
    {
      params: { path: { tenantId, entityId, reservationId } },
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

export async function postInventoryIssue(
  tenantId: string,
  entityId: string,
  payload: CreateOperationalIssueRequest,
): Promise<OperationalIssueOutcomeDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/inventory/issues",
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

export async function fetchPayablesOverview(
  tenantId: string,
  entityId: string,
): Promise<PayablesOverviewResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/overview",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    throw extractProblemError(error, status);
  }
  return data;
}

export async function fetchSupplierBillDetail(
  tenantId: string,
  entityId: string,
  billId: string,
): Promise<SupplierBillDetailResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{billId}",
    {
      params: { path: { tenantId, entityId, billId } },
      cache: "no-store",
    },
  );
  if (error || !data) {
    const status = (response as Response).status;
    throw extractProblemError(error, status);
  }
  return data;
}

export async function previewSupplierBill(
  tenantId: string,
  entityId: string,
  payload: PreviewSupplierBillRequest,
): Promise<SupplierBillPreviewResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/preview",
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

export async function createSupplierBill(
  tenantId: string,
  entityId: string,
  payload: CreateSupplierBillRequest,
): Promise<SupplierBillDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills",
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

export async function updateSupplierBill(
  tenantId: string,
  entityId: string,
  billId: string,
  payload: UpdateSupplierBillRequest,
): Promise<SupplierBillDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{billId}",
    {
      params: { path: { tenantId, entityId, billId } },
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

export async function submitSupplierBill(
  tenantId: string,
  entityId: string,
  billId: string,
): Promise<SupplierBillDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{billId}/submit",
    {
      params: { path: { tenantId, entityId, billId } },
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

export async function decideSupplierBill(
  tenantId: string,
  entityId: string,
  billId: string,
  payload: DecideSupplierBillRequest,
): Promise<SupplierBillDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{billId}/decision",
    {
      params: { path: { tenantId, entityId, billId } },
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

export async function postSupplierBill(
  tenantId: string,
  entityId: string,
  billId: string,
  payload: PostSupplierBillRequest,
): Promise<SupplierBillDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{billId}/post",
    {
      params: { path: { tenantId, entityId, billId } },
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

export async function updateSupplierBillPaymentBlock(
  tenantId: string,
  entityId: string,
  billId: string,
  payload: UpdateBillPaymentBlockRequest,
): Promise<SupplierBillDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{billId}/payment-block",
    {
      params: { path: { tenantId, entityId, billId } },
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

export async function updateInvoiceMatchPolicy(
  tenantId: string,
  entityId: string,
  payload: UpdateInvoiceMatchPolicyRequest,
): Promise<InvoiceMatchPolicyDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/payables/match-policy",
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

export async function fetchReturnsOverview(
  tenantId: string,
  entityId: string,
): Promise<ReturnsOverviewResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/overview",
    {
      params: { path: { tenantId, entityId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function fetchSupplierReturnDetail(
  tenantId: string,
  entityId: string,
  returnId: string,
): Promise<SupplierReturnDetailResponse> {
  const { data, error, response } = await rawClient.GET(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/{returnId}",
    {
      params: { path: { tenantId, entityId, returnId } },
      cache: "no-store",
    },
  );
  if (error || !data) throw extractProblemError(error, (response as Response).status);
  return data;
}

export async function previewSupplierReturn(
  tenantId: string,
  entityId: string,
  payload: PreviewSupplierReturnRequest,
): Promise<SupplierReturnPreviewResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/preview",
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

export async function createSupplierReturnDraft(
  tenantId: string,
  entityId: string,
  payload: CreateSupplierReturnRequest,
): Promise<SupplierReturnDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns",
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

export async function updateSupplierReturnDraft(
  tenantId: string,
  entityId: string,
  returnId: string,
  payload: UpdateSupplierReturnRequest,
): Promise<SupplierReturnDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.PUT(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/{returnId}",
    {
      params: { path: { tenantId, entityId, returnId } },
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

export async function submitSupplierReturn(
  tenantId: string,
  entityId: string,
  returnId: string,
): Promise<SupplierReturnDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/{returnId}/submit",
    {
      params: { path: { tenantId, entityId, returnId } },
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

export async function decideSupplierReturn(
  tenantId: string,
  entityId: string,
  returnId: string,
  payload: DecideSupplierReturnRequest,
): Promise<SupplierReturnDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/{returnId}/decision",
    {
      params: { path: { tenantId, entityId, returnId } },
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

export async function executeSupplierReturn(
  tenantId: string,
  entityId: string,
  returnId: string,
  payload: ExecuteSupplierReturnRequest,
): Promise<SupplierReturnDetailResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/{returnId}/execute",
    {
      params: { path: { tenantId, entityId, returnId } },
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

export async function receiveLinkedReplacement(
  tenantId: string,
  entityId: string,
  authorizationId: string,
  payload: ReceiveReplacementRequest,
): Promise<GoodsReceiptDetailDto> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST(
    "/api/workspaces/{tenantId}/entities/{entityId}/returns/replacements/{authorizationId}/receive",
    {
      params: { path: { tenantId, entityId, authorizationId } },
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
