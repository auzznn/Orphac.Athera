using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Api.Cli;

public static partial class DemoDataSeeder
{
    private static async Task EnsureS10ReturnsDemoDataAsync(
        AtheraDbContext db,
        Guid nusaTenantId,
        Guid nusaJktId,
        Guid nusaSupplierId,
        Guid pcsUomId,
        Guid requesterUserId,
        Guid approverUserId,
        Guid apOfficerUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var sealItem = await EnsureItemAsync(
            db,
            nusaTenantId,
            "ITM-S10-SEAL-01",
            "Seal Silikon Hidrolik Industrial",
            ItemKinds.StockedGood,
            pcsUomId,
            pcsUomId,
            pcsUomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct);

        var gasketItem = await EnsureItemAsync(
            db,
            nusaTenantId,
            "ITM-S10-GASKET-02",
            "Gasket PTFE Flange 4 Inch",
            ItemKinds.StockedGood,
            pcsUomId,
            pcsUomId,
            pcsUomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct);

        await db.SaveChangesAsync(ct);

        var alreadySeeded = await db.GoodsReceipts.AnyAsync(
            r => r.TenantId == nusaTenantId &&
                 r.LegalEntityId == nusaJktId &&
                 r.SupplierDeliveryNoteNumber == "SJ-JKT-DEMO-S10-01",
            ct);

        if (alreadySeeded)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Fixture 1: ITM-S10-SEAL-01 — 20 PCS ordered @ Rp 75.000; 6 dock-rejected (M1 queue) + 14 accepted & billed unpaid (M3 queue)
        var (poSealId, poSealLineId) = await CreateAndOptionallyApprovePoAsync(
            db,
            nusaTenantId,
            nusaJktId,
            nusaSupplierId,
            sealItem.ItemId,
            pcsUomId,
            20m,
            75_000m,
            "Dermaga S10 Return Hub Jakarta",
            new DateOnly(2026, 11, 10),
            "Komitmen pengadaan seal hidrolik (Demo S10)",
            "Seal Silikon Hidrolik Industrial — Batch S10",
            requesterUserId,
            approverUserId,
            approve: true,
            nowUtc.AddMinutes(10),
            ct);

        var grSealOutcome = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                nusaTenantId,
                nusaJktId,
                poSealId,
                "SJ-JKT-DEMO-S10-01",
                "seed:jkt-s10-gr1",
                new DateOnly(2026, 11, 10),
                "Dermaga Utama Jakarta",
                "B 9010 S10 · Segel R1",
                "DO-JKT-S10-01.pdf",
                "Tiba 20 PCS seal; 6 unit ditemukan cacat kemasan di dermaga",
                [new ArrivalLineInput(poSealLineId, pcsUomId, 20m, true, DiscrepancyReasonCodes.WetOrDefectiveGoods, "6 PCS seal retak saat cek visual awal", null)],
                requesterUserId,
                nowUtc.AddMinutes(11)),
            ct);

        if (!grSealOutcome.Succeeded || grSealOutcome.Receipt is null)
        {
            throw new InvalidOperationException($"Failed seeding S10 seal GR: {grSealOutcome.ErrorCode} {grSealOutcome.ErrorDetail}");
        }

        var grSealLineId = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == nusaTenantId && l.LegalEntityId == nusaJktId && l.GoodsReceiptId == grSealOutcome.Receipt.GoodsReceiptId)
            .Select(l => l.GoodsReceiptLineId)
            .SingleAsync(ct);

        var dcSealOutcome = await GoodsReceiptWorkflowService.RecordDockCheckAsync(
            db,
            new RecordDockCheckCommand(
                nusaTenantId,
                nusaJktId,
                grSealOutcome.Receipt.GoodsReceiptId,
                grSealLineId,
                "seed:jkt-s10-dc1",
                PendingCheckBaseQuantity: 0m,
                DockHeldBaseQuantity: 0m,
                DockReleasedBaseQuantity: 14m,
                DockRejectedBaseQuantity: 6m,
                DiscrepancyReasonCode: DiscrepancyReasonCodes.WetOrDefectiveGoods,
                InspectionNotes: "6 PCS ditolak di dermaga (M1 unvalued queue); 14 PCS lulus masuk acceptance",
                EvidenceReference: "QC-JKT-S10-01",
                ActorUserId: requesterUserId,
                NowUtc: nowUtc.AddMinutes(12)),
            ct);

        if (!dcSealOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S10 seal dock check: {dcSealOutcome.ErrorCode} {dcSealOutcome.ErrorDetail}");
        }

        var accSealOutcome = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
            db,
            new AcceptReceiptCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                grSealOutcome.Receipt.GoodsReceiptId,
                "seed:jkt-s10-acc1",
                new DateOnly(2026, 11, 10),
                StockStatuses.Unrestricted,
                "Penerimaan bernilai 14 PCS seal hidrolik",
                [new AcceptReceiptLineInput(grSealLineId, 14m)],
                approverUserId,
                nowUtc.AddMinutes(13)),
            ct);

        if (!accSealOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S10 seal acceptance: {accSealOutcome.ErrorCode} {accSealOutcome.ErrorDetail}");
        }

        var sealAccrualLineId = await db.ReceiptAccrualLines.AsNoTracking()
            .Where(al => al.TenantId == nusaTenantId && al.LegalEntityId == nusaJktId && al.GoodsReceiptLineId == grSealLineId)
            .Select(al => al.ReceiptAccrualLineId)
            .SingleAsync(ct);

        var ppnCode = await db.TaxCodes.AsNoTracking()
            .Where(t =>
                t.TenantId == nusaTenantId &&
                t.LegalEntityId == nusaJktId &&
                t.TaxFamily == TaxFamilies.Ppn &&
                t.Direction == TaxDirections.Input &&
                t.Status == MasterStatuses.Active)
            .OrderByDescending(t => t.NominalRatePercent == 11m)
            .ThenBy(t => t.EffectiveFrom)
            .FirstAsync(ct);

        var billDate = new DateOnly(2026, 11, 10);
        var subSeal = 14m * 75_000m;
        var ppnEval = TaxDeterminationEvaluator.Evaluate(ppnCode, subSeal, billDate, hasCounterpartyNpwp: true, evidenceProvided: true, currencyScale: 0);
        var grossSeal = decimal.Round(subSeal + ppnEval.TaxAmount, 4, MidpointRounding.AwayFromZero);

        var billDraft = await SupplierBillCoordinator.CreateDraftAsync(
            db,
            new CreateSupplierBillCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                SupplierBillTypes.PoThreeWayMatch,
                poSealId,
                "INV-JKT-S10-M3-01",
                "010.001-26.10000101",
                WithholdingSlipReference: null,
                billDate,
                DueDate: new DateOnly(2026, 12, 10),
                CurrencyCode: "IDR",
                DeclaredGrossAmount: grossSeal,
                Notes: "Faktur belum dibayar 14 PCS seal siap demonstrasi M3 Credit Note + Nota Retur",
                Lines: [new BillLineDraftInput(sealAccrualLineId, 14m, 75_000m, ppnCode.TaxCodeId, null, "Batch S10 M3")],
                ActorUserId: apOfficerUserId,
                NowUtc: nowUtc.AddMinutes(14)),
            ct);

        if (!billDraft.Succeeded || billDraft.Bill is null)
        {
            throw new InvalidOperationException($"Failed creating S10 M3 bill draft: {billDraft.ErrorCode} {billDraft.ErrorDetail}");
        }

        var billSubmit = await SupplierBillCoordinator.SubmitBillAsync(
            db,
            new SubmitSupplierBillCommand(nusaTenantId, nusaJktId, billDraft.Bill.SupplierBillId, "IDR", apOfficerUserId, nowUtc.AddMinutes(15)),
            ct);

        if (!billSubmit.Succeeded)
        {
            throw new InvalidOperationException($"Failed submitting S10 M3 bill: {billSubmit.ErrorCode} {billSubmit.ErrorDetail}");
        }

        var billDecide = await SupplierBillCoordinator.DecideBillAsync(
            db,
            new DecideSupplierBillCommand(
                nusaTenantId,
                nusaJktId,
                billDraft.Bill.SupplierBillId,
                ApprovalDecisions.Approved,
                "Disetujui Controller untuk demo faktur terbuka S10",
                ApprovePriceVariance: false,
                ApplyPaymentBlock: false,
                PaymentBlockReason: null,
                ActorUserId: approverUserId,
                NowUtc: nowUtc.AddMinutes(16)),
            ct);

        if (!billDecide.Succeeded)
        {
            throw new InvalidOperationException($"Failed deciding S10 M3 bill: {billDecide.ErrorCode} {billDecide.ErrorDetail}");
        }

        var billPost = await SupplierBillCoordinator.PostBillAsync(
            db,
            new PostSupplierBillCommand(
                nusaTenantId,
                nusaJktId,
                billDraft.Bill.SupplierBillId,
                "IDR",
                billDate,
                "seed:jkt-s10-bill1-post",
                apOfficerUserId,
                nowUtc.AddMinutes(17)),
            ct);

        if (!billPost.Succeeded)
        {
            throw new InvalidOperationException($"Failed posting S10 M3 bill: {billPost.ErrorCode} {billPost.ErrorDetail}");
        }

        // Fixture 2: ITM-S10-GASKET-02 — 30 PCS ordered & valued-accepted unbilled @ Rp 120.000 (M2 queue)
        var (poGasketId, poGasketLineId) = await CreateAndOptionallyApprovePoAsync(
            db,
            nusaTenantId,
            nusaJktId,
            nusaSupplierId,
            gasketItem.ItemId,
            pcsUomId,
            30m,
            120_000m,
            "Dermaga S10 Return Hub Jakarta",
            new DateOnly(2026, 11, 11),
            "Komitmen pengadaan gasket PTFE (Demo S10)",
            "Gasket PTFE Flange 4 Inch — Batch S10",
            requesterUserId,
            approverUserId,
            approve: true,
            nowUtc.AddMinutes(18),
            ct);

        var grGasketOutcome = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                nusaTenantId,
                nusaJktId,
                poGasketId,
                "SJ-JKT-DEMO-S10-02",
                "seed:jkt-s10-gr2",
                new DateOnly(2026, 11, 11),
                "Dermaga Utama Jakarta",
                "B 9022 S10 · Segel G2",
                "DO-JKT-S10-02.pdf",
                "Tiba 30 PCS gasket untuk stok terbuka belum ditagih (M2 return queue)",
                [new ArrivalLineInput(poGasketLineId, pcsUomId, 30m, false, DiscrepancyReasonCodes.None, null, null)],
                requesterUserId,
                nowUtc.AddMinutes(19)),
            ct);

        if (!grGasketOutcome.Succeeded || grGasketOutcome.Receipt is null)
        {
            throw new InvalidOperationException($"Failed seeding S10 gasket GR: {grGasketOutcome.ErrorCode} {grGasketOutcome.ErrorDetail}");
        }

        var grGasketLineId = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == nusaTenantId && l.LegalEntityId == nusaJktId && l.GoodsReceiptId == grGasketOutcome.Receipt.GoodsReceiptId)
            .Select(l => l.GoodsReceiptLineId)
            .SingleAsync(ct);

        var dcGasketOutcome = await GoodsReceiptWorkflowService.RecordDockCheckAsync(
            db,
            new RecordDockCheckCommand(
                nusaTenantId,
                nusaJktId,
                grGasketOutcome.Receipt.GoodsReceiptId,
                grGasketLineId,
                "seed:jkt-s10-dc2",
                PendingCheckBaseQuantity: 0m,
                DockHeldBaseQuantity: 0m,
                DockReleasedBaseQuantity: 30m,
                DockRejectedBaseQuantity: 0m,
                DiscrepancyReasonCode: DiscrepancyReasonCodes.None,
                InspectionNotes: "Lolos cek fisik 30 PCS gasket masuk ke Gate 2",
                EvidenceReference: "QC-JKT-S10-02",
                ActorUserId: requesterUserId,
                NowUtc: nowUtc.AddMinutes(20)),
            ct);

        if (!dcGasketOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S10 gasket dock check: {dcGasketOutcome.ErrorCode} {dcGasketOutcome.ErrorDetail}");
        }

        var accGasketOutcome = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
            db,
            new AcceptReceiptCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                grGasketOutcome.Receipt.GoodsReceiptId,
                "seed:jkt-s10-acc2",
                new DateOnly(2026, 11, 11),
                StockStatuses.Unrestricted,
                "Penerimaan bernilai 30 PCS gasket (unbilled GRNI siap demo M2)",
                [new AcceptReceiptLineInput(grGasketLineId, 30m)],
                approverUserId,
                nowUtc.AddMinutes(21)),
            ct);

        if (!accGasketOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S10 gasket acceptance: {accGasketOutcome.ErrorCode} {accGasketOutcome.ErrorDetail}");
        }

        await tx.CommitAsync(ct);
    }
}
