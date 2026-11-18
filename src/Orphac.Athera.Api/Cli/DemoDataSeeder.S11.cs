using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Api.Cli;

public static partial class DemoDataSeeder
{
    private static async Task EnsureS11PaymentsDemoDataAsync(
        AtheraDbContext db,
        Guid nusaTenantId,
        Guid nusaJktId,
        Guid nusaSbyId,
        Guid smdrTenantId,
        Guid smdrBdgId,
        Guid nusaSupplierId,
        Guid pcsUomId,
        Guid requesterUserId,
        Guid financeControllerUserId,
        Guid apClerkUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        foreach (var pair in new[] { (nusaTenantId, nusaJktId), (nusaTenantId, nusaSbyId), (smdrTenantId, smdrBdgId) })
        {
            await EnsureApprovalPolicyAsync(
                db,
                pair.Item1,
                pair.Item2,
                CashPolicyDefaults.SupplierPaymentReleasePolicyCode,
                "Supplier Payment Four-Eyes Release Authorization",
                requiresFourEyes: true,
                requiresApproverMfa: false,
                nowUtc,
                ct);
        }

        var jktBank = await EnsureEntityBankAccountAsync(
            db,
            nusaTenantId,
            nusaJktId,
            "BCA-IDR-001",
            "PT Bank Central Asia Tbk",
            "KCU Sudirman Jakarta",
            "035-9988-771",
            "PT Nusantara Distribusi Jakarta",
            financeControllerUserId,
            nowUtc,
            ct);

        await EnsureEntityBankAccountAsync(
            db,
            nusaTenantId,
            nusaSbyId,
            "BCA-SBY-001",
            "PT Bank Central Asia Tbk",
            "KCU Pemuda Surabaya",
            "088-4411-220",
            "PT Nusantara Niaga Surabaya",
            financeControllerUserId,
            nowUtc,
            ct);

        var filterItem = await EnsureItemAsync(
            db,
            nusaTenantId,
            "ITM-S11-FILTER-01",
            "Filter Oli Hidrolik Industrial",
            ItemKinds.StockedGood,
            pcsUomId,
            pcsUomId,
            pcsUomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct);

        await db.SaveChangesAsync(ct);

        var alreadySeeded = await db.GoodsReceipts.AsNoTracking().AnyAsync(
            r => r.TenantId == nusaTenantId &&
                 r.LegalEntityId == nusaJktId &&
                 r.SupplierDeliveryNoteNumber == "SJ-JKT-DEMO-S11-01",
            ct);

        if (alreadySeeded)
        {
            return;
        }

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

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var p1 = await SeedBilledOpenPayableForS11Async(
            db, nusaTenantId, nusaJktId, nusaSupplierId, filterItem.ItemId, pcsUomId, ppnCode,
            10m, 100_000m, "SJ-JKT-DEMO-S11-01", "INV-JKT-S11-01", "010.001-26.20000101",
            new DateOnly(2026, 11, 12), requesterUserId, financeControllerUserId, apClerkUserId,
            nowUtc.AddMinutes(30), "seed:jkt-s11-p1", ct);

        var p2 = await SeedBilledOpenPayableForS11Async(
            db, nusaTenantId, nusaJktId, nusaSupplierId, filterItem.ItemId, pcsUomId, ppnCode,
            20m, 100_000m, "SJ-JKT-DEMO-S11-02", "INV-JKT-S11-02", "010.001-26.20000102",
            new DateOnly(2026, 11, 12), requesterUserId, financeControllerUserId, apClerkUserId,
            nowUtc.AddMinutes(40), "seed:jkt-s11-p2", ct);

        await SeedBilledOpenPayableForS11Async(
            db, nusaTenantId, nusaJktId, nusaSupplierId, filterItem.ItemId, pcsUomId, ppnCode,
            15m, 100_000m, "SJ-JKT-DEMO-S11-03", "INV-JKT-S11-03", "010.001-26.20000103",
            new DateOnly(2026, 11, 13), requesterUserId, financeControllerUserId, apClerkUserId,
            nowUtc.AddMinutes(50), "seed:jkt-s11-p3", ct);

        // Payment A (Prepared by Siti, simulated file PIN, submitted -> ready for four-eyes release by Budi)
        var propA = await SupplierSettlementCoordinator.CreatePaymentProposalAsync(
            db,
            new CreatePaymentProposalCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                jktBank.BankAccountId,
                nusaSupplierId,
                "PT Bank Mandiri (Persero) Tbk",
                "122-000-4455-889",
                "PT Sumber Makmur Kemasan",
                PaymentMethods.BankTransferIdr,
                new DateOnly(2026, 11, 14),
                "Usulan bayar INV-JKT-S11-01 siap simulasi file & rilis 4-mata",
                [new PaymentAllocationInput(p1.PayableItemId, p1.RemainingOpenAmount, "Pelunasan penuh")],
                apClerkUserId,
                nowUtc.AddMinutes(60)),
            ct);
        if (!propA.Succeeded || propA.Payment is null)
        {
            throw new InvalidOperationException($"Failed seeding S11 payment A: {propA.ErrorCode}");
        }

        await SupplierSettlementCoordinator.SimulatePaymentInstructionFileAsync(
            db,
            new SimulatePaymentFileCommand(nusaTenantId, nusaJktId, propA.Payment.SupplierPaymentId, PaymentInstructionFormats.SimulatedCsvV1, apClerkUserId, nowUtc.AddMinutes(61)),
            ct);
        await SupplierSettlementCoordinator.SubmitPaymentForReleaseAsync(
            db,
            new SubmitPaymentCommand(nusaTenantId, nusaJktId, propA.Payment.SupplierPaymentId, apClerkUserId, nowUtc.AddMinutes(62)),
            ct);

        // Payment B (Prepared by Siti, released by Budi -> sitting in Outbound Clearing 1120; ready for Bank Statement match)
        var propB = await SupplierSettlementCoordinator.CreatePaymentProposalAsync(
            db,
            new CreatePaymentProposalCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                jktBank.BankAccountId,
                nusaSupplierId,
                "PT Bank Mandiri (Persero) Tbk",
                "122-000-4455-889",
                "PT Sumber Makmur Kemasan",
                PaymentMethods.BankTransferIdr,
                new DateOnly(2026, 11, 14),
                "Pembayaran dirilis ke kliring keluar INV-JKT-S11-02 siap rekonsiliasi mutasi",
                [new PaymentAllocationInput(p2.PayableItemId, p2.RemainingOpenAmount, "Pelunasan kliring S11")],
                apClerkUserId,
                nowUtc.AddMinutes(63)),
            ct);
        if (!propB.Succeeded || propB.Payment is null)
        {
            throw new InvalidOperationException($"Failed seeding S11 payment B: {propB.ErrorCode}");
        }

        await SupplierSettlementCoordinator.SimulatePaymentInstructionFileAsync(
            db,
            new SimulatePaymentFileCommand(nusaTenantId, nusaJktId, propB.Payment.SupplierPaymentId, PaymentInstructionFormats.SimulatedCsvV1, apClerkUserId, nowUtc.AddMinutes(64)),
            ct);
        await SupplierSettlementCoordinator.SubmitPaymentForReleaseAsync(
            db,
            new SubmitPaymentCommand(nusaTenantId, nusaJktId, propB.Payment.SupplierPaymentId, apClerkUserId, nowUtc.AddMinutes(65)),
            ct);
        var relB = await SupplierSettlementCoordinator.ReleasePaymentAsync(
            db,
            new ReleasePaymentCommand(
                nusaTenantId,
                nusaJktId,
                propB.Payment.SupplierPaymentId,
                "IDR",
                new DateOnly(2026, 11, 14),
                "BCA-BATCH-20261114-01",
                "seed:jkt-s11-release-b",
                financeControllerUserId,
                nowUtc.AddMinutes(66)),
            ct);
        if (!relB.Succeeded)
        {
            throw new InvalidOperationException($"Failed releasing S11 payment B: {relB.ErrorCode} {relB.ErrorDetail}");
        }

        // Import statement BST with unmatched DEBIT matching Payment B exact IDR amount
        var stmtImport = await BankReconciliationCoordinator.ImportBankStatementAsync(
            db,
            new ImportBankStatementCommand(
                nusaTenantId,
                nusaJktId,
                jktBank.BankAccountId,
                "BCA-STMT-2026-11-15",
                new DateOnly(2026, 11, 15),
                OpeningBalance: 500_000_000m,
                ClosingBalance: 497_780_000m,
                CsvContent: null,
                Lines:
                [
                    new BankStatementLineInput(
                        new DateOnly(2026, 11, 15),
                        BankStatementLineDirections.Debit,
                        p2.NetPayableAmount,
                        "TRF-BCA-20261115-01",
                        "PT SUMBER MAKMUR KEMASAN",
                        "TRANSFER KELUAR INV-JKT-S11-02 / BCA-BATCH-20261114-01")
                ],
                financeControllerUserId,
                nowUtc.AddMinutes(67)),
            ct);
        if (!stmtImport.Succeeded)
        {
            throw new InvalidOperationException($"Failed importing S11 bank statement: {stmtImport.ErrorCode} {stmtImport.ErrorDetail}");
        }

        await tx.CommitAsync(ct);
    }

    private static async Task<BankAccount> EnsureEntityBankAccountAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string accountCode,
        string bankName,
        string branchName,
        string accountNumber,
        string holderName,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.BankAccounts.SingleOrDefaultAsync(
            b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.AccountCode == accountCode,
            ct);
        if (existing is not null)
        {
            return existing;
        }

        var bankMap = await db.AccountMappings.AsNoTracking().SingleAsync(
            m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.PurposeCode == GlMappingPurposes.BankMainOperating,
            ct);
        var clearingMap = await db.AccountMappings.AsNoTracking().SingleAsync(
            m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.PurposeCode == GlMappingPurposes.BankOutboundClearing,
            ct);

        var outcome = await SupplierSettlementCoordinator.UpsertBankAccountAsync(
            db,
            new UpsertBankAccountCommand(
                tenantId,
                entityId,
                "IDR",
                accountCode,
                bankName,
                branchName,
                accountNumber,
                holderName,
                bankMap.AccountId,
                clearingMap.AccountId,
                MasterStatuses.Active,
                actorUserId,
                nowUtc),
            ct);

        if (!outcome.Succeeded || outcome.BankAccount is null)
        {
            throw new InvalidOperationException($"Failed seeding bank account {accountCode}: {outcome.ErrorCode} {outcome.ErrorDetail}");
        }

        return outcome.BankAccount;
    }

    private static async Task<PayableItem> SeedBilledOpenPayableForS11Async(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid supplierId,
        Guid itemId,
        Guid pcsUomId,
        TaxCode ppnCode,
        decimal qty,
        decimal unitPrice,
        string sjNumber,
        string invNumber,
        string taxInvNumber,
        DateOnly docDate,
        Guid requesterUserId,
        Guid approverUserId,
        Guid apClerkUserId,
        DateTimeOffset ts,
        string keyPrefix,
        CancellationToken ct)
    {
        var (poId, poLineId) = await CreateAndOptionallyApprovePoAsync(
            db,
            tenantId,
            entityId,
            supplierId,
            itemId,
            pcsUomId,
            qty,
            unitPrice,
            "Gudang Utama Jakarta",
            docDate,
            $"Komitmen pengadaan filter {sjNumber} (Demo S11)",
            $"Filter Oli Hidrolik {sjNumber}",
            requesterUserId,
            approverUserId,
            approve: true,
            ts,
            ct);

        var gr = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                tenantId,
                entityId,
                poId,
                sjNumber,
                $"{keyPrefix}-gr",
                docDate,
                "Dermaga Utama Jakarta",
                $"B 9110 S11 · {sjNumber}",
                $"{sjNumber}.pdf",
                $"Tiba {qty:N0} PCS filter S11",
                [new ArrivalLineInput(poLineId, pcsUomId, qty, false, DiscrepancyReasonCodes.None, null, null)],
                requesterUserId,
                ts.AddMinutes(1)),
            ct);

        if (!gr.Succeeded || gr.Receipt is null)
        {
            throw new InvalidOperationException($"S11 GR seed failed: {gr.ErrorCode} {gr.ErrorDetail}");
        }

        var grLineId = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.GoodsReceiptId == gr.Receipt.GoodsReceiptId)
            .Select(l => l.GoodsReceiptLineId)
            .SingleAsync(ct);

        var dc = await GoodsReceiptWorkflowService.RecordDockCheckAsync(
            db,
            new RecordDockCheckCommand(
                tenantId,
                entityId,
                gr.Receipt.GoodsReceiptId,
                grLineId,
                $"{keyPrefix}-dc",
                0m, 0m, qty, 0m,
                DiscrepancyReasonCodes.None,
                "Lolos cek fisik S11",
                $"QC-{sjNumber}",
                requesterUserId,
                ts.AddMinutes(2)),
            ct);
        if (!dc.Succeeded)
        {
            throw new InvalidOperationException($"S11 dock check failed: {dc.ErrorCode}");
        }

        var acc = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
            db,
            new AcceptReceiptCommand(
                tenantId,
                entityId,
                "IDR",
                gr.Receipt.GoodsReceiptId,
                $"{keyPrefix}-acc",
                docDate,
                StockStatuses.Unrestricted,
                $"Terima bernilai S11 {sjNumber}",
                [new AcceptReceiptLineInput(grLineId, qty)],
                approverUserId,
                ts.AddMinutes(3)),
            ct);
        if (!acc.Succeeded)
        {
            throw new InvalidOperationException($"S11 accept failed: {acc.ErrorCode}");
        }

        var accrualLineId = await db.ReceiptAccrualLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.GoodsReceiptLineId == grLineId)
            .Select(l => l.ReceiptAccrualLineId)
            .SingleAsync(ct);

        var sub = decimal.Round(qty * unitPrice, 4, MidpointRounding.AwayFromZero);
        var ppnEval = TaxDeterminationEvaluator.Evaluate(ppnCode, sub, docDate, true, true, 0);
        var gross = decimal.Round(sub + ppnEval.TaxAmount, 4, MidpointRounding.AwayFromZero);

        var billDraft = await SupplierBillCoordinator.CreateDraftAsync(
            db,
            new CreateSupplierBillCommand(
                tenantId,
                entityId,
                "IDR",
                SupplierBillTypes.PoThreeWayMatch,
                poId,
                invNumber,
                taxInvNumber,
                null,
                docDate,
                docDate.AddDays(30),
                "IDR",
                gross,
                $"Faktur S11 siap settlement ({invNumber})",
                [new BillLineDraftInput(accrualLineId, qty, unitPrice, ppnCode.TaxCodeId, null, $"S11 {invNumber}")],
                apClerkUserId,
                ts.AddMinutes(4)),
            ct);
        if (!billDraft.Succeeded || billDraft.Bill is null)
        {
            throw new InvalidOperationException($"S11 bill draft failed: {billDraft.ErrorCode}");
        }

        await SupplierBillCoordinator.SubmitBillAsync(
            db,
            new SubmitSupplierBillCommand(tenantId, entityId, billDraft.Bill.SupplierBillId, "IDR", apClerkUserId, ts.AddMinutes(5)),
            ct);
        await SupplierBillCoordinator.DecideBillAsync(
            db,
            new DecideSupplierBillCommand(tenantId, entityId, billDraft.Bill.SupplierBillId, ApprovalDecisions.Approved, "Disetujui untuk settlement S11", false, false, null, approverUserId, ts.AddMinutes(6)),
            ct);
        var post = await SupplierBillCoordinator.PostBillAsync(
            db,
            new PostSupplierBillCommand(tenantId, entityId, billDraft.Bill.SupplierBillId, "IDR", docDate, $"{keyPrefix}-bill-post", apClerkUserId, ts.AddMinutes(7)),
            ct);
        if (!post.Succeeded || post.PayableItem is null)
        {
            throw new InvalidOperationException($"S11 bill post failed: {post.ErrorCode} {post.ErrorDetail}");
        }

        return post.PayableItem;
    }
}
