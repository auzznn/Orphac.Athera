using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.UnitTests;

public sealed class S06DockReceivingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 5, 0, 0, TimeSpan.Zero);

    private static (PurchaseOrder Po, PurchaseOrderRevision Rev, PurchaseOrderLine Line) BuildApprovedPoLine(decimal orderedBaseQty, int lineCountQty = 1)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var supId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var revId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var pcsId = Guid.NewGuid();

        var po = PurchaseOrder.CreateDraft(
            poId,
            tenantId,
            entityId,
            "DRF-TEST-001",
            supId,
            "IDR",
            "IDR",
            new DateOnly(2026, 11, 18),
            new DateOnly(2026, 11, 25),
            "Gudang Pusat Jakarta",
            30,
            creatorId,
            Now);

        var line = PurchaseOrderLine.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            poId,
            revId,
            1,
            "L001",
            itemId,
            "ITM-PKG-001",
            "Karton Lipat",
            "STOCKED_GOOD",
            pcsId,
            "PCS",
            pcsId,
            "PCS",
            1m,
            orderedBaseQty / lineCountQty,
            orderedBaseQty,
            10_000m,
            null,
            null,
            null,
            null,
            orderedBaseQty * 10_000m,
            0m,
            0m,
            0m,
            false,
            null);

        var rev = PurchaseOrderRevision.CreateDraft(
            revId,
            tenantId,
            entityId,
            poId,
            1,
            null,
            supId,
            "PT-SUP-001",
            "PT Sumber Makmur",
            "01.111.111.1-011.000",
            "PKP",
            "IDR",
            new DateOnly(2026, 11, 18),
            new DateOnly(2026, 11, 25),
            "Gudang Pusat Jakarta",
            30,
            Guid.NewGuid(),
            DeliveryToleranceModes.ExactApprovedQtyOnly,
            null,
            null,
            creatorId,
            Now);

        rev.ApplyTotalsAndHash(orderedBaseQty * 10_000m, 0m, 0m, orderedBaseQty * 10_000m, orderedBaseQty, [line], Now);
        rev.FreezeAndMarkSubmitted(creatorId, Guid.NewGuid(), "hash123", Now);
        po.MarkSubmitted("PO-2026-00001", Now);
        rev.ApplyDecision(approverId, true, "Approved", Now);
        po.ApplyRevisionDecision(revId, 1, true, supId, new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 25), "Gudang Pusat Jakarta", 30, Now);

        return (po, rev, line);
    }

    [Fact]
    public void GoodsReceiptLine_EnforcesConservationAndRejectsInvalidBucketsOrMissingReason()
    {
        var (po, rev, poLine) = BuildApprovedPoLine(100m);
        var grnId = Guid.NewGuid();
        var line = GoodsReceiptLine.CreateArrival(
            Guid.NewGuid(),
            po.TenantId,
            po.LegalEntityId,
            grnId,
            1,
            poLine,
            poLine.BaseUomId,
            "PCS",
            1m,
            60m,
            60m,
            remainingApprovedEligibleBaseQty: 100m,
            flagForInspection: false,
            discrepancyReasonCode: DiscrepancyReasonCodes.None,
            discrepancyNotes: null,
            conditionEvidenceRef: null,
            Now);

        Assert.Equal(DockCustodyStatuses.Arrived, line.CustodyStatus);
        Assert.Equal(60m, line.PendingCheckBaseQuantity);
        Assert.Equal(0m, line.DockHeldBaseQuantity);
        Assert.Equal(0m, line.ValuedAcceptedBaseQuantity);

        var receipt = GoodsReceipt.Create(
            grnId,
            po.TenantId,
            po.LegalEntityId,
            "GRN-2026-00001",
            po,
            rev,
            "SJ-001",
            "idem-1",
            "payload-1",
            new DateOnly(2026, 11, 18),
            "Dock A",
            null,
            null,
            null,
            [line],
            po.CreatedByUserId,
            Now);

        Assert.False(receipt.HasFinancialPosting);
        Assert.Equal(GoodsReceiptStatuses.Arrived, receipt.Status);

        // Unbalanced conservation rejected
        var mismatchEx = Assert.Throws<InvalidOperationException>(() =>
            line.ApplyDockDisposition(0m, 0m, 55m, 4m, 100m, 0, DiscrepancyReasonCodes.DamagedPackaging, "Bad sum", null, Now));
        Assert.Contains("dock_quantity_conservation_mismatch", mismatchEx.Message);

        // Fractional discrete split rejected
        var fracEx = Assert.Throws<InvalidOperationException>(() =>
            line.ApplyDockDisposition(0m, 0m, 55.5m, 4.5m, 100m, 0, DiscrepancyReasonCodes.DamagedPackaging, "Fractional", null, Now));
        Assert.Contains("fractional_discrete_base_quantity", fracEx.Message);

        // Rejecting or holding without discrepancy reason fails
        var missingReasonEx = Assert.Throws<InvalidOperationException>(() =>
            line.ApplyDockDisposition(0m, 0m, 55m, 5m, 100m, 0, DiscrepancyReasonCodes.None, "Missing code", null, Now));
        Assert.Contains("discrepancy_reason_required", missingReasonEx.Message);

        // Valid split: 55 released + 5 rejected damaged -> line conservation preserved & receipt transitions cleanly
        line.ApplyDockDisposition(0m, 0m, 55m, 5m, 100m, 0, DiscrepancyReasonCodes.DamagedPackaging, "5 PCS basah", "IMG-01.jpg", Now);
        receipt.RecalculateHeaderFromLines([line], Now);

        Assert.Equal(DockCustodyStatuses.DockReleased, line.CustodyStatus);
        Assert.Equal(55m, line.DockReleasedBaseQuantity);
        Assert.Equal(5m, line.DockRejectedBaseQuantity);
        Assert.Equal(0m, line.ValuedAcceptedBaseQuantity);
        Assert.Equal(55m, line.UnresolvedCustodyBaseQuantity);
        Assert.Equal(GoodsReceiptStatuses.ReleasedForAcceptance, receipt.Status);
        Assert.False(receipt.HasFinancialPosting);
    }

    [Fact]
    public void GoodsReceiptLine_SegregatesOd01OverdeliveryIntoDockHeldAndBlocksUnauthorizedRelease()
    {
        var (po, _, poLine) = BuildApprovedPoLine(48m);

        var line = GoodsReceiptLine.CreateArrival(
            Guid.NewGuid(),
            po.TenantId,
            po.LegalEntityId,
            Guid.NewGuid(),
            1,
            poLine,
            poLine.BaseUomId,
            "PCS",
            1m,
            60m,
            60m,
            remainingApprovedEligibleBaseQty: 48m,
            flagForInspection: false,
            discrepancyReasonCode: DiscrepancyReasonCodes.None,
            discrepancyNotes: null,
            conditionEvidenceRef: null,
            Now);

        Assert.Equal(DockCustodyStatuses.DockCheckPending, line.CustodyStatus);
        Assert.Equal(48m, line.PendingCheckBaseQuantity);
        Assert.Equal(12m, line.DockHeldBaseQuantity);
        Assert.Equal(12m, line.OverdeliveryExcessBaseQuantity);
        Assert.Equal(DiscrepancyReasonCodes.OverdeliveryOd01Hold, line.DiscrepancyReasonCode);

        // Attempting to release > approved 48 PCS must fail under OD-01
        var od01Ex = Assert.Throws<InvalidOperationException>(() =>
            line.ApplyDockDisposition(0m, 0m, 60m, 0m, 48m, 0, DiscrepancyReasonCodes.None, null, null, Now));
        Assert.Contains("od01_overdelivery_release_blocked", od01Ex.Message);

        // Releasing exact 48 PCS while keeping 12 PCS held succeeds
        line.ApplyDockDisposition(0m, 12m, 48m, 0m, 48m, 0, DiscrepancyReasonCodes.OverdeliveryOd01Hold, "Tunggu amandemen PO", null, Now);
        Assert.Equal(DockCustodyStatuses.DockHeld, line.CustodyStatus);
        Assert.Equal(12m, line.OverdeliveryExcessBaseQuantity);

        // Rejecting the 12 PCS excess unvalued clears excess hold cleanly
        line.ApplyDockDisposition(0m, 0m, 48m, 12m, 48m, 0, DiscrepancyReasonCodes.OverdeliveryOd01Hold, "Tolak kelebihan ke supplier", "SJ-RET-01", Now);
        Assert.Equal(DockCustodyStatuses.DockReleased, line.CustodyStatus);
        Assert.Equal(0m, line.OverdeliveryExcessBaseQuantity);
    }

    [Fact]
    public void ReceivingPermissions_AndDeterministicPayloadHashes_VerifyCleanly()
    {
        Assert.Contains(PermissionCodes.ReceivingRead, PermissionCodes.KnownAll);
        Assert.Contains(PermissionCodes.ReceivingManage, PermissionCodes.KnownAll);

        var clerk = RoleTemplates.GetPermissions(RoleTemplates.InventoryClerk);
        Assert.Contains(PermissionCodes.ReceivingRead, clerk);
        Assert.Contains(PermissionCodes.ReceivingManage, clerk);

        var finance = RoleTemplates.GetPermissions(RoleTemplates.FinanceOfficer);
        Assert.Contains(PermissionCodes.ReceivingRead, finance);
        Assert.DoesNotContain(PermissionCodes.ReceivingManage, finance);

        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var uomId = Guid.NewGuid();

        var h1 = ReceivingHash.ComputeArrivalPayloadHash(poId, " sj-01 ", new DateOnly(2026, 11, 18), "Dock 1", "B 1234", "DOC-1", [(lineId, uomId, 30m, true, "none")]);
        var h2 = ReceivingHash.ComputeArrivalPayloadHash(poId, "SJ-01", new DateOnly(2026, 11, 18), "Dock 1", "B 1234", "DOC-1", [(lineId, uomId, 30.000m, true, "NONE")]);
        var h3 = ReceivingHash.ComputeArrivalPayloadHash(poId, "SJ-01", new DateOnly(2026, 11, 18), "Dock 1", "B 1234", "DOC-1", [(lineId, uomId, 31m, true, "NONE")]);

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
    }
}
