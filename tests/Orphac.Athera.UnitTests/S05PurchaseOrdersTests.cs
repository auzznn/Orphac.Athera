using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.UnitTests;

public sealed class S05PurchaseOrdersTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S05PermissionCodesAndRoleTemplates_SeparateProcurementManageFromApprove()
    {
        Assert.Contains(PermissionCodes.ProcurementRead, PermissionCodes.KnownAll);
        Assert.Contains(PermissionCodes.ProcurementManage, PermissionCodes.KnownAll);
        Assert.Contains(PermissionCodes.ProcurementApprove, PermissionCodes.KnownAll);

        var officer = RoleTemplates.GetPermissions(RoleTemplates.ProcurementOfficer);
        Assert.Contains(PermissionCodes.ProcurementRead, officer);
        Assert.Contains(PermissionCodes.ProcurementManage, officer);
        Assert.DoesNotContain(PermissionCodes.ProcurementApprove, officer);
        Assert.DoesNotContain(PermissionCodes.ApprovalDecide, officer);

        var approver = RoleTemplates.GetPermissions(RoleTemplates.EntityApprover);
        Assert.Contains(PermissionCodes.ProcurementRead, approver);
        Assert.Contains(PermissionCodes.ProcurementApprove, approver);
        Assert.DoesNotContain(PermissionCodes.ProcurementManage, approver);
    }

    [Fact]
    public void DeliveryTolerancePolicy_EnforcesOd01SafeDefaultZeroTolerance()
    {
        var exPct = Assert.Throws<InvalidOperationException>(() =>
            DeliveryTolerancePolicy.CreateSafeDefault(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "TOL-5PCT",
                "Five percent",
                overdeliveryTolerancePercent: 5m,
                overdeliveryToleranceBaseQty: 0m,
                allowUnlimitedOverdelivery: false,
                isDefault: false,
                Now));
        Assert.StartsWith("od01_overdelivery_tolerance_blocked:", exPct.Message);

        var exUnlim = Assert.Throws<InvalidOperationException>(() =>
            DeliveryTolerancePolicy.EnsureSafeToleranceValues(0m, 0m, allowUnlimitedOverdelivery: true));
        Assert.StartsWith("od01_overdelivery_tolerance_blocked:", exUnlim.Message);

        var safe = DeliveryTolerancePolicy.CreateSafeDefault(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "EXACT_SAFE",
            "Exact Qty Safe",
            0m,
            0m,
            false,
            true,
            Now);
        Assert.Equal(DeliveryToleranceModes.ExactApprovedQtyOnly, safe.Mode);
        Assert.Equal(0m, safe.OverdeliveryTolerancePercent);
        Assert.False(safe.AllowUnlimitedOverdelivery);
    }

    [Fact]
    public void DocumentApproval_EnforcesFourEyesSelfApprovalAndSingleDecision()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var approval = DocumentApproval.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            Guid.NewGuid(),
            "PROCUREMENT",
            "PURCHASE_ORDER_REVISION",
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "PO-2026-00001 rev 1",
            requesterId,
            Now);

        var selfEx = Assert.Throws<InvalidOperationException>(() =>
            approval.RecordDecision(requesterId, ApprovalDecisions.Approved, "Self attempt", false, Now.AddMinutes(1)));
        Assert.StartsWith("four_eyes_self_approval_forbidden:", selfEx.Message);

        approval.RecordDecision(approverId, ApprovalDecisions.Approved, "Commercial terms verified", false, Now.AddMinutes(2));
        Assert.Equal(DocumentApprovalStatuses.Approved, approval.Status);

        var dupEx = Assert.Throws<InvalidOperationException>(() =>
            approval.RecordDecision(Guid.NewGuid(), ApprovalDecisions.Rejected, "Too late", false, Now.AddMinutes(3)));
        Assert.StartsWith("approval_already_decided:", dupEx.Message);
    }

    [Fact]
    public void PurchaseOrderAndRevision_EnforceFrozenImmutabilityDownstreamCancelGuardAndShortClose()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var rev1Id = Guid.NewGuid();
        var tolId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var pcsId = Guid.NewGuid();
        var boxId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var po = PurchaseOrder.CreateDraft(
            poId, tenantId, entityId, "DRF-001", supplierId, "IDR", "IDR",
            new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 25), "Gudang Jakarta", 30, requesterId, Now);

        var rev1 = PurchaseOrderRevision.CreateDraft(
            rev1Id, tenantId, entityId, poId, 1, null, supplierId, "PT-SUP-001", "PT Sumber Makmur", null, "PKP",
            "IDR", new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 25), "Gudang Jakarta", 30, tolId,
            DeliveryToleranceModes.ExactApprovedQtyOnly, null, null, requesterId, Now);

        var line1 = PurchaseOrderLine.Create(
            Guid.NewGuid(), tenantId, entityId, poId, rev1Id, 1, "L001",
            itemId, "ITM-001", "Karton", "STOCKED_GOOD",
            boxId, "BOX", pcsId, "PCS", 24m,
            2m, 48m, 100_000m, null, null, null, null, 200_000m, 0m,
            0m, 0m, false, null);

        rev1.ApplyTotalsAndHash(200_000m, 0m, 0m, 200_000m, 48m, [line1], Now);
        po.MarkSubmitted("PO-2026-00001", Now.AddMinutes(1));
        rev1.FreezeAndMarkSubmitted(requesterId, Guid.NewGuid(), rev1.SnapshotHash, Now.AddMinutes(1));

        // Mutating a frozen revision throws
        var frozenEx = Assert.Throws<InvalidOperationException>(() =>
            rev1.UpdateDraftDetailsAndTotals(
                supplierId, "PT-SUP-001", "PT Sumber Makmur", null, "PKP",
                new DateOnly(2026, 11, 18), null, "Changed", 30, tolId,
                DeliveryToleranceModes.ExactApprovedQtyOnly, null, null,
                1m, 0m, 0m, 1m, 24m, [line1], Now.AddMinutes(2)));
        Assert.StartsWith("po_revision_frozen_immutable:", frozenEx.Message);

        // Self decision throws
        var selfEx = Assert.Throws<InvalidOperationException>(() =>
            rev1.ApplyDecision(requesterId, true, "Self approve", Now.AddMinutes(2)));
        Assert.StartsWith("four_eyes_self_approval_forbidden:", selfEx.Message);

        // Independent approval succeeds
        rev1.ApplyDecision(approverId, true, "OK", Now.AddMinutes(3));
        po.ApplyRevisionDecision(rev1.RevisionId, 1, true, supplierId, rev1.OrderDate, rev1.ExpectedDeliveryDate, rev1.DeliveryDestination, 30, Now.AddMinutes(3));
        Assert.Equal(PurchaseOrderStatuses.Approved, po.Status);
        Assert.Equal(ReceivingProgressStatuses.NotReceived, ReceivingProgressStatuses.Derive(po.Status, true, 48m, 0m));

        // Downstream receiving reference blocks cancel; short-close still permitted cleanly
        po.RegisterDownstreamReference(24m, Now.AddMinutes(5));
        Assert.Equal(ReceivingProgressStatuses.PartiallyReceived, ReceivingProgressStatuses.Derive(po.Status, true, 48m, 24m));

        var cancelEx = Assert.Throws<InvalidOperationException>(() =>
            po.Cancel(requesterId, "Try cancel after receipt", Now.AddMinutes(6)));
        Assert.StartsWith("po_has_downstream_references:", cancelEx.Message);

        po.ShortClose(requesterId, "Remaining 24 PCS discontinued by supplier", hasOpenRevision: false, Now.AddMinutes(7));
        Assert.Equal(PurchaseOrderStatuses.Closed, po.Status);
        Assert.Equal(ReceivingProgressStatuses.ShortClosed, ReceivingProgressStatuses.Derive(po.Status, true, 48m, 24m));
    }

    [Fact]
    public void PurchaseOrderDiffCalculator_DetectsHeaderAddedModifiedRemovedLinesAndTotals()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var rev1Id = Guid.NewGuid();
        var rev2Id = Guid.NewGuid();
        var tolId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var pcsId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var rev1 = PurchaseOrderRevision.CreateDraft(
            rev1Id, tenantId, entityId, poId, 1, null, supplierId, "PT-SUP-001", "PT Sumber Makmur", null, "PKP",
            "IDR", new DateOnly(2026, 11, 18), null, "Gudang Lama", 30, tolId, DeliveryToleranceModes.ExactApprovedQtyOnly,
            null, null, userId, Now);
        var r1l1 = PurchaseOrderLine.Create(Guid.NewGuid(), tenantId, entityId, poId, rev1Id, 1, "L001", Guid.NewGuid(), "ITM-A", "Item A", "STOCKED_GOOD", pcsId, "PCS", pcsId, "PCS", 1m, 10m, 10m, 1000m, null, null, null, null, 10_000m, 0m, 0m, 0m, false, null);
        var r1l2 = PurchaseOrderLine.Create(Guid.NewGuid(), tenantId, entityId, poId, rev1Id, 2, "L002", Guid.NewGuid(), "ITM-B", "Item B", "STOCKED_GOOD", pcsId, "PCS", pcsId, "PCS", 1m, 5m, 5m, 2000m, null, null, null, null, 10_000m, 0m, 0m, 0m, false, null);
        rev1.ApplyTotalsAndHash(20_000m, 0m, 0m, 20_000m, 15m, [r1l1, r1l2], Now);

        var rev2 = PurchaseOrderRevision.CreateDraft(
            rev2Id, tenantId, entityId, poId, 2, rev1Id, supplierId, "PT-SUP-001", "PT Sumber Makmur", null, "PKP",
            "IDR", new DateOnly(2026, 11, 18), null, "Gudang Baru", 45, tolId, DeliveryToleranceModes.ExactApprovedQtyOnly,
            "Ubah gudang & qty", null, userId, Now.AddMinutes(5));
        var r2l1 = PurchaseOrderLine.Create(Guid.NewGuid(), tenantId, entityId, poId, rev2Id, 1, "L001", r1l1.ItemId, "ITM-A", "Item A", "STOCKED_GOOD", pcsId, "PCS", pcsId, "PCS", 1m, 12m, 12m, 1000m, null, null, null, null, 12_000m, 0m, 0m, 0m, false, null);
        var r2l3 = PurchaseOrderLine.Create(Guid.NewGuid(), tenantId, entityId, poId, rev2Id, 2, "L003", Guid.NewGuid(), "ITM-C", "Item C", "NON_STOCK_SERVICE", pcsId, "PCS", pcsId, "PCS", 1m, 1m, 1m, 5000m, null, null, null, null, 5_000m, 0m, 0m, 0m, false, null);
        rev2.ApplyTotalsAndHash(17_000m, 0m, 0m, 17_000m, 13m, [r2l1, r2l3], Now.AddMinutes(5));

        var diff = PurchaseOrderDiffCalculator.Compute(rev2, [r2l1, r2l3], rev1, [r1l1, r1l2]);
        Assert.Equal(2, diff.HeaderChanges.Count);
        Assert.Contains(diff.HeaderChanges, h => h.FieldKey == "deliveryDestination" && h.PreviousValue == "Gudang Lama" && h.CurrentValue == "Gudang Baru");
        Assert.Contains(diff.HeaderChanges, h => h.FieldKey == "paymentTermsDays" && h.PreviousValue == "30" && h.CurrentValue == "45");

        Assert.Collection(
            diff.LineChanges,
            c => { Assert.Equal("L001", c.LineageLineKey); Assert.Equal(PoDiffChangeKinds.Modified, c.ChangeKind); },
            c => { Assert.Equal("L003", c.LineageLineKey); Assert.Equal(PoDiffChangeKinds.Added, c.ChangeKind); },
            c => { Assert.Equal("L002", c.LineageLineKey); Assert.Equal(PoDiffChangeKinds.Removed, c.ChangeKind); });

        Assert.Equal(-3_000m, diff.GrandTotalDelta);
        Assert.Equal(-2m, diff.OrderedBaseQtyDelta);
    }
}
