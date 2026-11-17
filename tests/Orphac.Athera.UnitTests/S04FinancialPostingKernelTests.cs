using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.UnitTests;

public sealed class S04FinancialPostingKernelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S04PermissionCodesAndFinanceOfficerTemplate_ExposeLeastPrivilegeFinanceScope()
    {
        Assert.Contains(PermissionCodes.FinanceConfigManage, PermissionCodes.KnownAll);
        Assert.Contains(PermissionCodes.FinanceRead, PermissionCodes.KnownAll);
        Assert.Contains(PermissionCodes.FinanceJournalPost, PermissionCodes.KnownAll);

        var perms = RoleTemplates.GetPermissions(RoleTemplates.FinanceOfficer);
        Assert.Contains(PermissionCodes.WorkspaceView, perms);
        Assert.Contains(PermissionCodes.FinanceRead, perms);
        Assert.Contains(PermissionCodes.FinanceConfigManage, perms);
        Assert.Contains(PermissionCodes.FinanceJournalPost, perms);
        Assert.Contains(PermissionCodes.PartnersRead, perms);
        Assert.Contains(PermissionCodes.CatalogRead, perms);
        Assert.DoesNotContain(PermissionCodes.AdminManage, perms);
        Assert.DoesNotContain(PermissionCodes.ApprovalDecide, perms);
    }

    [Fact]
    public void GlAccountAndAccountMappingValidator_EnforceHeaderPostingTypeAndEntityBoundaries()
    {
        var tenantId = Guid.NewGuid();
        var entityA = Guid.NewGuid();
        var entityB = Guid.NewGuid();

        var header = GlAccount.Create(
            Guid.NewGuid(),
            tenantId,
            entityA,
            "1000",
            "Assets",
            GlAccountTypes.Asset,
            GlAccountCategories.HeaderGroup,
            parentAccountId: null,
            allowPosting: true, // Header forces false
            isControlAccount: false,
            nowUtc: Now);

        Assert.Equal("1000", header.AccountCode);
        Assert.Equal(GlNormalBalances.Debit, header.NormalBalance);
        Assert.False(header.AllowPosting);

        var grni = GlAccount.Create(
            Guid.NewGuid(),
            tenantId,
            entityA,
            "2110",
            "GRNI Clearing",
            GlAccountTypes.Liability,
            GlAccountCategories.GrniAccrual,
            parentAccountId: header.AccountId,
            allowPosting: true,
            isControlAccount: true,
            nowUtc: Now);

        Assert.Equal(GlNormalBalances.Credit, grni.NormalBalance);
        Assert.True(grni.AllowPosting);
        Assert.True(grni.IsControlAccount);

        // Header cannot be mapped
        Assert.Throws<InvalidOperationException>(() =>
            AccountMappingValidator.ValidateAccountForPurpose(GlMappingPurposes.InventoryAsset, header, entityA));

        // Mismatched type (Liability cannot map to INVENTORY_ASSET which requires ASSET)
        Assert.Throws<InvalidOperationException>(() =>
            AccountMappingValidator.ValidateAccountForPurpose(GlMappingPurposes.InventoryAsset, grni, entityA));

        // Foreign entity cannot be mapped
        Assert.Throws<InvalidOperationException>(() =>
            AccountMappingValidator.ValidateAccountForPurpose(GlMappingPurposes.GrniClearing, grni, entityB));

        // Matching LIABILITY + active + postable + entityA passes
        AccountMappingValidator.ValidateAccountForPurpose(GlMappingPurposes.GrniClearing, grni, entityA);

        grni.Deactivate(Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() =>
            AccountMappingValidator.ValidateAccountForPurpose(GlMappingPurposes.GrniClearing, grni, entityA));
    }

    [Fact]
    public void JournalEntry_EnforcesDecimalBalanceSingleSidedLinesAndDeterministicPayloadHash()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var capitalId = Guid.NewGuid();
        var postingDate = new DateOnly(2026, 11, 18);

        var lineInputs = new List<GlJournalLineInput>
        {
            new(cashId, "Setoran modal kas", 2_500_000.00m, 0m),
            new(capitalId, "Modal disetor", 0m, 2_500_000.00m),
        };

        var hash1 = GlPostingHash.ComputePayloadHash(
            postingDate,
            "IDR",
            JournalEntryKinds.Standard,
            reversalOfJournalId: null,
            lineInputs);

        var hash2 = GlPostingHash.ComputePayloadHash(
            postingDate,
            "IDR",
            JournalEntryKinds.Standard,
            reversalOfJournalId: null,
            lineInputs);

        var hashChangedAmount = GlPostingHash.ComputePayloadHash(
            postingDate,
            "IDR",
            JournalEntryKinds.Standard,
            reversalOfJournalId: null,
            [
                new(cashId, "Setoran modal kas", 2_500_000.50m, 0m),
                new(capitalId, "Modal disetor", 0m, 2_500_000.50m),
            ]);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hashChangedAmount);

        var (entry, lines) = JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            entityId,
            periodId,
            "JRN-2026-11-00001",
            JournalEntryKinds.Standard,
            postingDate,
            "IDR",
            "Initial capital posting",
            "MANUAL_GL",
            "MANUAL_JOURNAL",
            "seed-01",
            "manual:seed-01",
            reversalOfJournalId: null,
            lineInputs,
            userId,
            Now);

        Assert.Equal(2_500_000.00m, entry.TotalDebit);
        Assert.Equal(2_500_000.00m, entry.TotalCredit);
        Assert.Equal(2, entry.LineCount);
        Assert.Equal(2, lines.Count);
        Assert.Equal(hash1, entry.PayloadHash);

        // Unbalanced rejects
        Assert.Throws<InvalidOperationException>(() => JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            entityId,
            periodId,
            "JRN-2026-11-00002",
            JournalEntryKinds.Standard,
            postingDate,
            "IDR",
            "Unbalanced",
            "MANUAL_GL",
            "MANUAL_JOURNAL",
            "bad-01",
            "manual:bad-01",
            null,
            [
                new(cashId, null, 1000m, 0m),
                new(capitalId, null, 0m, 999m),
            ],
            userId,
            Now));

        // Double-sided single line rejects
        Assert.Throws<ArgumentException>(() => JournalEntry.CreatePosted(
            Guid.NewGuid(),
            tenantId,
            entityId,
            periodId,
            "JRN-2026-11-00003",
            JournalEntryKinds.Standard,
            postingDate,
            "IDR",
            "Both debit and credit",
            "MANUAL_GL",
            "MANUAL_JOURNAL",
            "bad-02",
            "manual:bad-02",
            null,
            [
                new(cashId, null, 1000m, 1000m),
                new(capitalId, null, 1000m, 1000m),
            ],
            userId,
            Now));
    }

    [Fact]
    public void FinancialPeriodAndTaxCode_EnforceClosedBlocksExplicitTaxSelectionAndDpp1112Math()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var period = FinancialPeriod.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            "2026-11",
            2026,
            11,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            FinancialPeriodStatuses.Open,
            Now);

        Assert.True(period.ContainsDate(new DateOnly(2026, 11, 18)));
        Assert.True(period.IsOpenForPosting(new DateOnly(2026, 11, 18)));

        period.Close(userId, "Month-end close", Now.AddHours(2));
        Assert.Equal(FinancialPeriodStatuses.Closed, period.Status);
        Assert.False(period.IsOpenForPosting(new DateOnly(2026, 11, 18)));

        period.Lock(userId, "Hard statutory lock", Now.AddHours(3));
        Assert.Equal(FinancialPeriodStatuses.Locked, period.Status);
        Assert.Throws<InvalidOperationException>(() => period.Close(userId, "Re-close", Now.AddHours(4)));

        // Tax requires explicit selection
        Assert.Throws<InvalidOperationException>(() =>
            TaxDeterminationEvaluator.Evaluate(null, 1_000_000m, new DateOnly(2026, 11, 18), true, false, 0));

        // 12% nominal over 11/12 DPP => DPP 916,667, PPN 110,000, effective 11%
        var ppnDpp1112 = TaxCode.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            "PPN-12-DPP1112",
            "PPN Masukan/Keluaran 12% (DPP 11/12)",
            TaxFamilies.Ppn,
            TaxDirections.Input,
            TaxTreatmentClasses.SpecialDpp1112,
            nominalRatePercent: 12m,
            taxableBaseRatioNumerator: 11,
            taxableBaseRatioDenominator: 12,
            requiresDocumentaryEvidence: true,
            requiresCounterpartyNpwp: false,
            taxAccountId: null,
            effectiveFrom: new DateOnly(2025, 1, 1),
            effectiveTo: null,
            reviewStatus: TaxReviewStatuses.UnreviewedConfigurable,
            statutoryReferenceNote: "PMK 131/2024 configurable template pending OD-07 sign-off",
            nowUtc: Now);

        var env = TaxDeterminationEvaluator.Evaluate(
            ppnDpp1112,
            grossAmount: 1_000_000m,
            transactionDate: new DateOnly(2026, 11, 18),
            hasCounterpartyNpwp: true,
            evidenceProvided: true,
            currencyScale: 0);

        Assert.False(env.IsBlocked);
        Assert.Equal(916_667m, env.TaxableBaseAmount);
        Assert.Equal(110_000m, env.TaxAmount);
        Assert.Equal(11.00m, env.EffectiveRatePercent);
        Assert.Equal(TaxReviewStatuses.UnreviewedConfigurable, env.ReviewStatus);

        // PPh 23 requiring counterparty NPWP flags block when NPWP absent
        var pph23 = TaxCode.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            "PPH23-SVC-2",
            "PPh 23 Jasa 2%",
            TaxFamilies.Pph,
            TaxDirections.Withholding,
            TaxTreatmentClasses.WithholdingDeduction,
            nominalRatePercent: 2m,
            taxableBaseRatioNumerator: 1,
            taxableBaseRatioDenominator: 1,
            requiresDocumentaryEvidence: true,
            requiresCounterpartyNpwp: true,
            taxAccountId: null,
            effectiveFrom: new DateOnly(2025, 1, 1),
            effectiveTo: null,
            reviewStatus: TaxReviewStatuses.UnreviewedConfigurable,
            statutoryReferenceNote: "Configurable withholding rate pending OD-07 review",
            nowUtc: Now);

        var blockedNoNpwp = TaxDeterminationEvaluator.Evaluate(
            pph23,
            grossAmount: 5_000_000m,
            transactionDate: new DateOnly(2026, 11, 18),
            hasCounterpartyNpwp: false,
            evidenceProvided: false,
            currencyScale: 0);

        Assert.True(blockedNoNpwp.IsBlocked);
        Assert.Equal("counterparty_npwp_required_for_rate", blockedNoNpwp.BlockedReason);
    }
}
