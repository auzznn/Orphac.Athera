namespace Orphac.Athera.Modules.Identity;

/// <summary>
/// Pre-approved entity-scoped permission bundles used during user invitations.
/// </summary>
public static class RoleTemplates
{
    public const string ProcurementOfficer = "PROCUREMENT_OFFICER";
    public const string SalesOfficer = "SALES_OFFICER";
    public const string InventoryClerk = "INVENTORY_CLERK";
    public const string FinanceOfficer = "FINANCE_OFFICER";
    public const string EntityApprover = "ENTITY_APPROVER";
    public const string EntityAdmin = "ENTITY_ADMIN";

    private static readonly Dictionary<string, IReadOnlyList<string>> Bundles = new(StringComparer.Ordinal)
    {
        [ProcurementOfficer] = [PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.PartnersRead, PermissionCodes.PartnersManage, PermissionCodes.CatalogRead, PermissionCodes.CatalogManage, PermissionCodes.ApprovalRequest],
        [SalesOfficer] = [PermissionCodes.WorkspaceView, PermissionCodes.SalesRead, PermissionCodes.PartnersRead, PermissionCodes.PartnersManage, PermissionCodes.CatalogRead, PermissionCodes.ApprovalRequest],
        [InventoryClerk] = [PermissionCodes.WorkspaceView, PermissionCodes.InventoryRead, PermissionCodes.CatalogRead, PermissionCodes.ApprovalRequest],
        [FinanceOfficer] = [PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost, PermissionCodes.PartnersRead, PermissionCodes.CatalogRead, PermissionCodes.ApprovalRequest],
        [EntityApprover] = [PermissionCodes.WorkspaceView, PermissionCodes.ApprovalRequest, PermissionCodes.ApprovalDecide],
        [EntityAdmin] = [PermissionCodes.WorkspaceView, PermissionCodes.AdminRead, PermissionCodes.AdminManage, PermissionCodes.ApprovalRequest, PermissionCodes.ApprovalDecide]
    };

    public static readonly IReadOnlyList<string> AllCodes = Bundles.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Bundles.ContainsKey(code.Trim().ToUpperInvariant());

    public static IReadOnlyList<string> GetPermissions(string? roleTemplateCode)
    {
        var normalized = roleTemplateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Bundles.TryGetValue(normalized, out var permissions))
        {
            throw new ArgumentException($"Unknown role template '{roleTemplateCode}'.", nameof(roleTemplateCode));
        }

        return permissions;
    }
}
