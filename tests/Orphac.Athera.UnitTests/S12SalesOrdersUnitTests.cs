using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Sales;

namespace Orphac.Athera.UnitTests;

public sealed class S12SalesOrdersUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S12PermissionCodesAndRoleTemplates_SeparateSalesManageFromApprove()
    {
        Assert.Contains(PermissionCodes.SalesRead, PermissionCodes.KnownAll);
        Assert.Contains(PermissionCodes.SalesManage, PermissionCodes.KnownAll);
        Assert.Contains(PermissionCodes.SalesApprove, PermissionCodes.KnownAll);

        var salesOfficer = RoleTemplates.GetPermissions(RoleTemplates.SalesOfficer);
        Assert.Contains(PermissionCodes.SalesRead, salesOfficer);
        Assert.Contains(PermissionCodes.SalesManage, salesOfficer);
        Assert.Contains(PermissionCodes.InventoryRead, salesOfficer);
        Assert.DoesNotContain(PermissionCodes.SalesApprove, salesOfficer);
        Assert.DoesNotContain(PermissionCodes.ApprovalDecide, salesOfficer);

        var approver = RoleTemplates.GetPermissions(RoleTemplates.EntityApprover);
        Assert.Contains(PermissionCodes.SalesRead, approver);
        Assert.Contains(PermissionCodes.SalesApprove, approver);
        Assert.DoesNotContain(PermissionCodes.SalesManage, approver);
    }

    [Fact]
    public void SalesOrderLine_EnforcesPositiveQtyPriceAndOd09ZeroDiscount()
    {
        var tid = Guid.NewGuid();
        var eid = Guid.NewGuid();
        var soId = Guid.NewGuid();
        var revId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var uomId = Guid.NewGuid();

        var exDiscount = Assert.Throws<InvalidOperationException>(() =>
            SalesOrderLine.Create(
                Guid.NewGuid(), tid, eid, soId, revId, 1, "SO-L001",
                itemId, "ITM-1", "Item Satu", "STOCKED_GOOD",
                uomId, "PCS", uomId, "PCS", 1m,
                5m, 5m, 100_000m, 10_000m,
                null, null, null, null, 500_000m, 0m, null, null));
        Assert.StartsWith("od09_sales_discount_policy_unresolved:", exDiscount.Message);

        var exZeroPrice = Assert.Throws<InvalidOperationException>(() =>
            SalesOrderLine.Create(
                Guid.NewGuid(), tid, eid, soId, revId, 1, "SO-L001",
                itemId, "ITM-1", "Item Satu", "STOCKED_GOOD",
                uomId, "PCS", uomId, "PCS", 1m,
                5m, 5m, 0m, 0m,
                null, null, null, null, 0m, 0m, null, null));
        Assert.StartsWith("invalid_quantity_or_price:", exZeroPrice.Message);

        var valid = SalesOrderLine.Create(
            Guid.NewGuid(), tid, eid, soId, revId, 1, "SO-L001",
            itemId, "ITM-1", "Item Satu", "STOCKED_GOOD",
            uomId, "PCS", uomId, "PCS", 1m,
            5m, 5m, 100_000m, 0m,
            null, "PPN-OUT-11", "STANDARD_TAXABLE", 11m, 500_000m, 55_000m, null, null);
        Assert.Equal(500_000m, valid.LineSubtotalAmount);
        Assert.Equal(55_000m, valid.TaxAmount);
        Assert.Equal(555_000m, valid.LineTotalAmount);
    }

    [Fact]
    public void SalesOrderRevision_HashDiffFourEyesAndDownstreamCancelGuards()
    {
        var tid = Guid.NewGuid();
        var eid = Guid.NewGuid();
        var custId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var pcsId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var so = SalesOrder.CreateDraft(
            Guid.NewGuid(), tid, eid, "SO-DRF-20261119-01", custId, "IDR", "IDR",
            new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 25), "Gudang Jakarta Timur", 14, "CUST-REF-1",
            creatorId, Now);

        var rev1 = SalesOrderRevision.CreateDraft(
            Guid.NewGuid(), tid, eid, so.SalesOrderId, 1, null,
            custId, "PT-CUST-001", "PT Graha Ritel Nusantara", null, "PKP",
            "IDR", new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 25), "Gudang Jakarta Timur", 14, "CUST-REF-1",
            null, null, creatorId, Now);

        var l1 = SalesOrderLine.Create(
            Guid.NewGuid(), tid, eid, so.SalesOrderId, rev1.RevisionId, 1, "SO-L001",
            itemId, "ITM-1", "Valve", "STOCKED_GOOD",
            pcsId, "PCS", pcsId, "PCS", 1m,
            10m, 10m, 50_000m, 0m, null, null, null, null, 500_000m, 0m, null, null);

        rev1.ApplyTotalsAndHash(500_000m, 0m, 500_000m, 10m, [l1], Now);
        var hash1 = SalesOrderHash.ComputeRevisionHash(rev1, [l1]);
        rev1.FreezeAndMarkSubmitted(creatorId, Guid.NewGuid(), hash1, Now.AddMinutes(1));
        so.MarkSubmitted(Now.AddMinutes(1));

        // Self-approval blocked (OD-02)
        var exSod = Assert.Throws<InvalidOperationException>(() =>
            rev1.ApplyDecision(creatorId, approved: true, "self try", Now.AddMinutes(2)));
        Assert.StartsWith("four_eyes_self_approval_forbidden:", exSod.Message);

        // Distinct approver approves
        rev1.ApplyDecision(approverId, approved: true, "approved", Now.AddMinutes(2));
        so.ApplyRevisionDecision(rev1.RevisionId, 1, true, "SO-2026-00001", custId, rev1.OrderDate, rev1.RequestedDeliveryDate, rev1.DeliveryAddressSnapshot, 14, "CUST-REF-1", Now.AddMinutes(2));
        Assert.Equal("SO-2026-00001", so.SoNumber);

        // Diff calculation against Rev 2 amendment
        var rev2 = SalesOrderRevision.CreateDraft(
            Guid.NewGuid(), tid, eid, so.SalesOrderId, 2, rev1.RevisionId,
            custId, "PT-CUST-001", "PT Graha Ritel Nusantara", null, "PKP",
            "IDR", new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 26), "Gudang Bekasi Barat", 30, "CUST-REF-1",
            "Revisi alamat & qty", null, creatorId, Now.AddMinutes(3));
        var l2 = SalesOrderLine.Create(
            Guid.NewGuid(), tid, eid, so.SalesOrderId, rev2.RevisionId, 1, "SO-L001",
            itemId, "ITM-1", "Valve", "STOCKED_GOOD",
            pcsId, "PCS", pcsId, "PCS", 1m,
            12m, 12m, 50_000m, 0m, null, null, null, null, 600_000m, 0m, null, null);
        rev2.ApplyTotalsAndHash(600_000m, 0m, 600_000m, 12m, [l2], Now.AddMinutes(3));

        var diff = SalesOrderDiffCalculator.Compute(rev2, [l2], rev1, [l1]);
        Assert.Equal(100_000m, diff.GrandTotalDelta);
        Assert.Equal(2m, diff.OrderedBaseQtyDelta);
        Assert.Contains(diff.HeaderChanges, h => h.Field == "deliveryAddress");
        Assert.Single(diff.LineChanges, c => c.ChangeKind == SoDiffChangeKinds.Modified);

        // Downstream fulfillment cancellation guard
        so.ApplyFulfillmentCounters(reservedBaseQty: 4m, dispatchedBaseQty: 0m, billedBaseQty: 0m, Now.AddMinutes(4));
        var exCancel = Assert.Throws<InvalidOperationException>(() => so.Cancel(creatorId, "try cancel", Now.AddMinutes(5)));
        Assert.StartsWith("so_has_downstream_fulfillment:", exCancel.Message);
    }
}
