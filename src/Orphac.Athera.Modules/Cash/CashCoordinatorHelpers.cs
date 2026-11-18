using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Modules.Cash;

internal static class CashCoordinatorHelpers
{
    public static async Task<ApprovalPolicy> EnsurePaymentReleasePolicyAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.ApprovalPolicies
            .SingleOrDefaultAsync(
                p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PolicyCode == CashPolicyDefaults.SupplierPaymentReleasePolicyCode,
                ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = ApprovalPolicy.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            CashPolicyDefaults.SupplierPaymentReleasePolicyCode,
            "Supplier Payment Four-Eyes Release Authorization",
            requiresFourEyes: true,
            requiresApproverMfa: false,
            nowUtc);
        db.ApprovalPolicies.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    public static async Task<SupplierPayment?> LockPaymentForUpdateAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid paymentId,
        CancellationToken ct)
    {
        return (await db.SupplierPayments
            .FromSqlInterpolated(
                $"""
                SELECT * FROM ap.supplier_payments
                WHERE tenant_id = {tenantId}
                  AND legal_entity_id = {entityId}
                  AND supplier_payment_id = {paymentId}
                FOR UPDATE
                """)
            .ToListAsync(ct))
            .SingleOrDefault();
    }

    public static async Task<List<PayableItem>> LockPayablesOrderedAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        IReadOnlyList<Guid> payableIds,
        CancellationToken ct)
    {
        var ordered = payableIds.Distinct().OrderBy(x => x).ToList();
        var locked = new List<PayableItem>(ordered.Count);
        foreach (var pid in ordered)
        {
            var row = (await db.PayableItems
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM ap.payable_items
                    WHERE tenant_id = {tenantId}
                      AND legal_entity_id = {entityId}
                      AND payable_item_id = {pid}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .SingleOrDefault();
            if (row is not null)
            {
                locked.Add(row);
            }
        }

        return locked;
    }

    public static (int Status, string Code, string Detail) ClassifyDomainError(InvalidOperationException ex)
    {
        var msg = ex.Message ?? "payment_domain_error";
        var colon = msg.IndexOf(':');
        if (colon > 0 && colon < 64)
        {
            var code = msg[..colon].Trim();
            var detail = msg[(colon + 1)..].Trim();
            var status = code switch
            {
                "four_eyes_violation" or "four_eyes_self_approval_forbidden" => 403,
                "foreign_or_missing_account" => 404,
                _ => 409
            };
            return (status, code, string.IsNullOrEmpty(detail) ? msg : detail);
        }

        return (409, "payment_domain_conflict", msg);
    }
}
