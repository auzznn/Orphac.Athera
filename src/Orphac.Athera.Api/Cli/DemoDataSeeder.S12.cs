using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Api.Cli;

public static partial class DemoDataSeeder
{
    private static async Task EnsureS12SalesDemoDataAsync(
        AtheraDbContext db,
        Guid nusaTenantId,
        Guid nusaJktId,
        Guid nusaSbyId,
        Guid smdrTenantId,
        Guid smdrBdgId,
        Guid pcsUomId,
        Guid boxUomId,
        Guid salesOfficerUserId,
        Guid financeControllerUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        foreach (var pair in new[] { (nusaTenantId, nusaJktId), (nusaTenantId, nusaSbyId), (smdrTenantId, smdrBdgId) })
        {
            await EnsureApprovalPolicyAsync(
                db,
                pair.Item1,
                pair.Item2,
                SalesPolicyDefaults.SalesOrderApprovalPolicyCode,
                "Sales Order Commercial Commitment Four-Eyes Approval",
                requiresFourEyes: true,
                requiresApproverMfa: false,
                nowUtc,
                ct);
        }

        await EnsurePartnerAsync(
            db,
            nusaTenantId,
            "PT-CUST-001",
            "PT Graha Ritel Nusantara",
            "Graha Ritel Nusantara",
            isSupplier: false,
            isCustomer: true,
            npwp: "01.777.332.1-011.000",
            taxPkpStatus: TaxPkpStatuses.Pkp,
            entityScopeMode: EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            contactName: "Tari Wibowo",
            contactEmail: "purchasing@graharitel.demo.local",
            nowUtc,
            ct);

        await EnsurePartnerAsync(
            db,
            smdrTenantId,
            "SMDR-CUST-01",
            "PT Pelayaran Lintas Jawa",
            "Pelayaran Lintas Jawa",
            isSupplier: false,
            isCustomer: true,
            npwp: "02.555.888.1-421.000",
            taxPkpStatus: TaxPkpStatuses.Pkp,
            entityScopeMode: EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            contactName: "Andi Prasetyo",
            contactEmail: "orders@lintasjawa.demo.local",
            nowUtc,
            ct);

        await db.SaveChangesAsync(ct);

        var alreadySeededJkt = await db.SalesOrders.AsNoTracking()
            .AnyAsync(o => o.TenantId == nusaTenantId && o.LegalEntityId == nusaJktId, ct);

        if (!alreadySeededJkt)
        {
            var custRitel = await db.Partners.AsNoTracking().SingleAsync(p => p.TenantId == nusaTenantId && p.PartnerCode == "PT-CUST-001", ct);
            var custDual = await db.Partners.AsNoTracking().SingleAsync(p => p.TenantId == nusaTenantId && p.PartnerCode == "PT-DUAL-001", ct);
            var valveItem = await db.Items.AsNoTracking().SingleAsync(i => i.TenantId == nusaTenantId && i.ItemCode == "ITM-S09-VALVE-01", ct);
            var plateItem = await db.Items.AsNoTracking().SingleAsync(i => i.TenantId == nusaTenantId && i.ItemCode == "ITM-STL-PLATE-10", ct);
            var ppnOut = await db.TaxCodes.AsNoTracking()
                .SingleAsync(t => t.TenantId == nusaTenantId && t.LegalEntityId == nusaJktId && t.Code == "PPN-OUT-11", ct);

            // 1. Committed Approved SO-2026-00001 on NUSA-JKT (U5 prepared & submitted, U2 approved)
            await using (var tx1 = await db.Database.BeginTransactionAsync(ct))
            {
                var d1 = await SalesOrderCoordinator.CreateDraftAsync(
                    db,
                    new CreateSoDraftCommand(
                        nusaTenantId,
                        nusaJktId,
                        "IDR",
                        "IDR",
                        custRitel.PartnerId,
                        new DateOnly(2026, 11, 14),
                        new DateOnly(2026, 11, 22),
                        "Gudang Distribusi Bekasi Timur Blok C-12",
                        14,
                        "PO-CUST-GRN-2026-88",
                        CreditLimitOverrideRequested: false,
                        "Komitmen awal penjualan katup industri S12",
                        [new SoLineInput("SO-L001", valveItem.ItemId, pcsUomId, 10m, 95_000m, 0m, ppnOut.TaxCodeId, new DateOnly(2026, 11, 22), "Valve Kuningan 1 Inch")],
                        salesOfficerUserId,
                        nowUtc.AddMinutes(70)),
                    ct);
                if (!d1.Succeeded || d1.SalesOrder is null || d1.Revision is null)
                {
                    throw new InvalidOperationException($"Failed seeding S12 JKT SO1 draft: {d1.ErrorCode} {d1.ErrorDetail}");
                }

                var s1 = await SalesOrderCoordinator.SubmitRevisionAsync(
                    db,
                    new SubmitSoRevisionCommand(nusaTenantId, nusaJktId, d1.SalesOrder.SalesOrderId, d1.Revision.RevisionId, salesOfficerUserId, nowUtc.AddMinutes(71)),
                    ct);
                if (!s1.Succeeded)
                {
                    throw new InvalidOperationException($"Failed seeding S12 JKT SO1 submit: {s1.ErrorCode} {s1.ErrorDetail}");
                }

                var a1 = await SalesOrderCoordinator.DecideRevisionAsync(
                    db,
                    new DecideSoRevisionCommand(
                        nusaTenantId, nusaJktId, d1.SalesOrder.SalesOrderId, d1.Revision.RevisionId,
                        Modules.Approval.ApprovalDecisions.Approved, "Harga, stok, dan pajak keluaran PPN-OUT-11 terverifikasi.", null,
                        financeControllerUserId, nowUtc.AddMinutes(72)),
                    ct);
                if (!a1.Succeeded)
                {
                    throw new InvalidOperationException($"Failed seeding S12 JKT SO1 approve: {a1.ErrorCode} {a1.ErrorDetail}");
                }

                await tx1.CommitAsync(ct);
            }

            // 2. Pending Four-Eyes Approval Draft SO on NUSA-JKT prepared by U5 for U2 approval
            await using (var tx2 = await db.Database.BeginTransactionAsync(ct))
            {
                var d2 = await SalesOrderCoordinator.CreateDraftAsync(
                    db,
                    new CreateSoDraftCommand(
                        nusaTenantId,
                        nusaJktId,
                        "IDR",
                        "IDR",
                        custDual.PartnerId,
                        new DateOnly(2026, 11, 15),
                        new DateOnly(2026, 11, 25),
                        "Kawasan Industri Jababeka Tahap II Kav 8A",
                        30,
                        "REQ-MNN-2026-104",
                        CreditLimitOverrideRequested: false,
                        "Menunggu otorisasi empat mata oleh Controller",
                        [new SoLineInput("SO-L001", plateItem.ItemId, pcsUomId, 5m, 180_000m, 0m, ppnOut.TaxCodeId, new DateOnly(2026, 11, 25), "Pelat Baja 10mm Grade SS400")],
                        salesOfficerUserId,
                        nowUtc.AddMinutes(73)),
                    ct);
                if (!d2.Succeeded || d2.SalesOrder is null || d2.Revision is null)
                {
                    throw new InvalidOperationException($"Failed seeding S12 JKT SO2 draft: {d2.ErrorCode} {d2.ErrorDetail}");
                }

                var s2 = await SalesOrderCoordinator.SubmitRevisionAsync(
                    db,
                    new SubmitSoRevisionCommand(nusaTenantId, nusaJktId, d2.SalesOrder.SalesOrderId, d2.Revision.RevisionId, salesOfficerUserId, nowUtc.AddMinutes(74)),
                    ct);
                if (!s2.Succeeded)
                {
                    throw new InvalidOperationException($"Failed seeding S12 JKT SO2 submit: {s2.ErrorCode} {s2.ErrorDetail}");
                }

                await tx2.CommitAsync(ct);
            }
        }

        // 3. Read-only seed SO on NUSA-SBY for U2 multi-entity inspection
        var alreadySeededSby = await db.SalesOrders.AsNoTracking()
            .AnyAsync(o => o.TenantId == nusaTenantId && o.LegalEntityId == nusaSbyId, ct);

        if (!alreadySeededSby)
        {
            var custRitel = await db.Partners.AsNoTracking().SingleAsync(p => p.TenantId == nusaTenantId && p.PartnerCode == "PT-CUST-001", ct);
            var pkgItem = await db.Items.AsNoTracking().SingleAsync(i => i.TenantId == nusaTenantId && i.ItemCode == "ITM-PKG-001", ct);
            var ppnOutSby = await db.TaxCodes.AsNoTracking()
                .SingleAsync(t => t.TenantId == nusaTenantId && t.LegalEntityId == nusaSbyId && t.Code == "PPN-OUT-11", ct);

            await using var txSby = await db.Database.BeginTransactionAsync(ct);
            var dSby = await SalesOrderCoordinator.CreateDraftAsync(
                db,
                new CreateSoDraftCommand(
                    nusaTenantId,
                    nusaSbyId,
                    "IDR",
                    "IDR",
                    custRitel.PartnerId,
                    new DateOnly(2026, 11, 14),
                    new DateOnly(2026, 11, 24),
                    "Rungkut Industrial Estate Blok D-05 Surabaya",
                    14,
                    "PO-CUST-SBY-2026-09",
                    CreditLimitOverrideRequested: false,
                    "Baseline order Surabaya (hanya baca untuk Controller)",
                    [new SoLineInput("SO-L001", pkgItem.ItemId, boxUomId, 2m, 600_000m, 0m, ppnOutSby.TaxCodeId, new DateOnly(2026, 11, 24), "Karton Lipat Polos 40x30x25 cm (BOX)")],
                    salesOfficerUserId,
                    nowUtc.AddMinutes(75)),
                ct);
            if (!dSby.Succeeded || dSby.SalesOrder is null || dSby.Revision is null)
            {
                throw new InvalidOperationException($"Failed seeding S12 SBY SO: {dSby.ErrorCode} {dSby.ErrorDetail}");
            }

            var sSby = await SalesOrderCoordinator.SubmitRevisionAsync(
                db,
                new SubmitSoRevisionCommand(nusaTenantId, nusaSbyId, dSby.SalesOrder.SalesOrderId, dSby.Revision.RevisionId, salesOfficerUserId, nowUtc.AddMinutes(76)),
                ct);
            if (!sSby.Succeeded)
            {
                throw new InvalidOperationException($"Failed seeding S12 SBY submit: {sSby.ErrorCode} {sSby.ErrorDetail}");
            }

            var aSby = await SalesOrderCoordinator.DecideRevisionAsync(
                db,
                new DecideSoRevisionCommand(
                    nusaTenantId, nusaSbyId, dSby.SalesOrder.SalesOrderId, dSby.Revision.RevisionId,
                    Modules.Approval.ApprovalDecisions.Approved, "Disetujui untuk NUSA-SBY", null,
                    financeControllerUserId, nowUtc.AddMinutes(77)),
                ct);
            if (!aSby.Succeeded)
            {
                throw new InvalidOperationException($"Failed seeding S12 SBY approve: {aSby.ErrorCode} {aSby.ErrorDetail}");
            }

            await txSby.CommitAsync(ct);
        }
    }
}
