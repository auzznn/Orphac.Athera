using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.UnitTests;

public sealed class S11SupplierSettlementAndBankReconciliationUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static (BankAccount Bank, Partner Supplier, PayableItem Payable) BuildFixture(
        decimal netPayable = 1_110_000m,
        bool paymentBlocked = false)
    {
        var tId = Guid.NewGuid();
        var eId = Guid.NewGuid();
        var bankGlId = Guid.NewGuid();
        var clearGlId = Guid.NewGuid();
        var submitter = Guid.NewGuid();
        var approver = Guid.NewGuid();

        var bank = BankAccount.Create(
            Guid.NewGuid(), tId, eId, "BCA-IDR-001", "PT Bank Central Asia Tbk",
            "Sudirman", "035-9988-771", "PT Nusantara Distribusi", "IDR", bankGlId, clearGlId, submitter, Now);

        var supplier = Partner.Create(
            Guid.NewGuid(), tId, "SUP-001", "PT Sumber Makmur", "Sumber Makmur",
            isSupplier: true, isCustomer: false, "01.111.222.3-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, Now);

        var bill = SupplierBill.CreateDraft(
            Guid.NewGuid(), tId, eId, "BILL-DRF-001", SupplierBillTypes.PoThreeWayMatch,
            Guid.NewGuid(), Guid.NewGuid(), "PO-2026-00001", supplier.PartnerId, supplier.PartnerCode,
            supplier.LegalName, supplier.TaxRegistrationNpwp, "INV-S11-TEST", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", netPayable, null, submitter, Now);

        var dummyComp = new InvoiceMatchComputation(
            MatchDimensionOutcomes.Matched,
            MatchDimensionOutcomes.Matched,
            MatchDimensionOutcomes.Matched,
            InvoiceMatchStatuses.Matched,
            IsPostingBlocked: false,
            RequiresApproval: false,
            LinesNetSubtotal: 1_000_000m,
            TotalGrniClearAmount: 1_000_000m,
            TotalPriceVarianceAmount: 0m,
            TotalInventoryAdjAmount: 0m,
            TotalPnlVarianceAmount: 0m,
            DeclaredGrossAmount: netPayable,
            ComputedGrossTotal: netPayable,
            BdWriteOffAmount: 0m,
            PpnTaxTotal: netPayable - 1_000_000m,
            WithholdingTaxTotal: 0m,
            NetPayableAmount: netPayable,
            Lines: Array.Empty<MatchedLineEvaluation>(),
            BlockingReasons: Array.Empty<string>());

        bill.ApplyMatchComputation(dummyComp, Now);
        bill.MarkSubmitted("BILL-2026-00001", submitter, Now);
        bill.RecordApprovalDecision(approver, ApprovalDecisions.Approved, "OK", false, paymentBlocked, paymentBlocked ? "OD-03 Hold" : null, Now);
        bill.MarkPosted(Guid.NewGuid(), "post-1", submitter, Now);

        var payable = PayableItem.Create(Guid.NewGuid(), tId, eId, "PAYB-2026-00001", bill, Guid.NewGuid(), new DateOnly(2026, 11, 10), submitter, Now);
        return (bank, supplier, payable);
    }

    [Fact]
    public void PayableItem_ApplyPaymentSettlement_ConservesBalancesAndBlocksPaymentBlocked()
    {
        var (_, _, blockedPayable) = BuildFixture(1_110_000m, paymentBlocked: true);
        var blockEx = Assert.Throws<InvalidOperationException>(() => blockedPayable.ApplyPaymentSettlement(500_000m, Now));
        Assert.Contains("payable_payment_blocked", blockEx.Message);

        var (_, _, openPayable) = BuildFixture(1_110_000m, paymentBlocked: false);
        openPayable.ApplyPaymentSettlement(400_000m, Now);
        Assert.Equal(400_000m, openPayable.PaidAmount);
        Assert.Equal(710_000m, openPayable.RemainingOpenAmount);
        Assert.Equal(PayableStatuses.PartiallyPaid, openPayable.Status);

        Assert.Throws<InvalidOperationException>(() => openPayable.ApplyPaymentSettlement(710_000.0001m, Now));

        openPayable.ApplyPaymentSettlement(710_000m, Now);
        Assert.Equal(1_110_000m, openPayable.PaidAmount);
        Assert.Equal(0m, openPayable.RemainingOpenAmount);
        Assert.Equal(PayableStatuses.Paid, openPayable.Status);

        openPayable.ReversePaymentSettlement(710_000m, Now);
        Assert.Equal(400_000m, openPayable.PaidAmount);
        Assert.Equal(710_000m, openPayable.RemainingOpenAmount);
        Assert.Equal(PayableStatuses.PartiallyPaid, openPayable.Status);

        openPayable.ReversePaymentSettlement(400_000m, Now);
        Assert.Equal(0m, openPayable.PaidAmount);
        Assert.Equal(1_110_000m, openPayable.RemainingOpenAmount);
        Assert.Equal(PayableStatuses.Open, openPayable.Status);
    }

    [Fact]
    public void SupplierPayment_FourEyesSoD_FileSimulationNonPosting_AndClearingConservation()
    {
        var (bank, supplier, payable) = BuildFixture(1_110_000m);
        var preparerId = Guid.NewGuid();
        var releaserId = Guid.NewGuid();

        var payment = SupplierPayment.CreateDraft(
            Guid.NewGuid(),
            bank.TenantId,
            bank.LegalEntityId,
            "PAY-DRF-2026-00001",
            bank,
            supplier,
            "PT Bank Mandiri",
            "122-000-4455",
            supplier.LegalName,
            PaymentMethods.BankTransferIdr,
            new DateOnly(2026, 11, 15),
            1_110_000m,
            "Test payment",
            preparerId,
            Now);

        // Stage 1: Simulated file generation leaves RemainingClearingAmount == 0, ReleaseJournalId == null
        var inst = PaymentInstruction.RecordSimulation(
            Guid.NewGuid(),
            bank.TenantId,
            bank.LegalEntityId,
            "PIN-2026-00001",
            payment.SupplierPaymentId,
            bank.BankAccountId,
            PaymentInstructionFormats.SimulatedCsvV1,
            payment.TotalAllocatedAmount,
            "IDR",
            "csv-payload",
            preparerId,
            Now);
        Assert.True(inst.SimulationOnly);
        payment.RecordInstructionSimulation(inst.PaymentInstructionId, inst.InstructionNumber, Now);
        Assert.Equal(1, payment.SimulatedInstructionCount);
        Assert.Equal(0m, payment.RemainingClearingAmount);
        Assert.Equal(SupplierPaymentStatuses.Draft, payment.Status);

        // Cannot release while DRAFT
        Assert.Throws<InvalidOperationException>(() =>
            payment.ReleaseToClearing("PAY-2026-00001", "REF-1", "k1", Guid.NewGuid(), releaserId, Now));

        payment.SubmitForRelease(preparerId, Now);
        Assert.Equal(SupplierPaymentStatuses.PendingRelease, payment.Status);

        // OD-02 Four-eyes: preparer cannot self-release
        var sodEx = Assert.Throws<InvalidOperationException>(() =>
            payment.ReleaseToClearing("PAY-2026-00001", "REF-1", "k1", Guid.NewGuid(), preparerId, Now));
        Assert.Contains("four_eyes_violation", sodEx.Message);

        // Independent release opens Outbound Clearing
        payment.ReleaseToClearing("PAY-2026-00001", "BCA-REF-01", "k1", Guid.NewGuid(), releaserId, Now);
        Assert.Equal(SupplierPaymentStatuses.Released, payment.Status);
        Assert.Equal(1_110_000m, payment.RemainingClearingAmount);
        Assert.Equal(0m, payment.ReconciledAmount);

        // Partial then full bank reconciliation conserves TotalAllocatedAmount == Reconciled + RemainingClearing
        payment.ApplyBankReconciliation(500_000m, Now);
        Assert.Equal(SupplierPaymentStatuses.PartiallyReconciled, payment.Status);
        Assert.Equal(610_000m, payment.RemainingClearingAmount);

        // Bank rejection must fail when partial match already reconciled
        Assert.Throws<InvalidOperationException>(() =>
            payment.MarkBankRejected(Guid.NewGuid(), "Rejected", releaserId, Now));

        payment.ApplyBankReconciliation(610_000m, Now);
        Assert.Equal(SupplierPaymentStatuses.Reconciled, payment.Status);
        Assert.Equal(0m, payment.RemainingClearingAmount);
        Assert.Equal(1_110_000m, payment.ReconciledAmount);

        // Full reversal returns to RELEASED so bank rejection can proceed cleanly
        payment.ReverseBankReconciliation(610_000m, Now);
        payment.ReverseBankReconciliation(500_000m, Now);
        Assert.Equal(SupplierPaymentStatuses.Released, payment.Status);
        Assert.Equal(1_110_000m, payment.RemainingClearingAmount);

        payment.MarkBankRejected(Guid.NewGuid(), "Invalid beneficiary account", releaserId, Now);
        Assert.Equal(SupplierPaymentStatuses.BankRejected, payment.Status);
        Assert.Equal(0m, payment.RemainingClearingAmount);
        Assert.NotNull(payable);
    }

    [Fact]
    public void BankStatementLine_CsvParserFingerprintAndMatchConservation()
    {
        const string csv = """
            ValueDate,Direction,Amount,BankReference,Counterparty,Description
            2026-11-15,DEBIT,2220000,TRF-BCA-001,PT Sumber Makmur,Pembayaran supplier
            2026-11-15,CREDIT,5000000,CR-BCA-002,PT Pelanggan Jaya,Pelunasan piutang
            """;

        var parsed = BankReconciliationCoordinator.ParseCsvLines(csv);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(2_220_000m, parsed[0].Amount);
        Assert.Equal("DEBIT", parsed[0].Direction);

        var bankId = Guid.NewGuid();
        var debitLine = BankStatementLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), bankId, 1,
            parsed[0].ValueDate, parsed[0].Direction, parsed[0].Amount, parsed[0].BankReference, parsed[0].CounterpartyName, parsed[0].Description, Now);
        var creditLine = BankStatementLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), bankId, 2,
            parsed[1].ValueDate, parsed[1].Direction, parsed[1].Amount, parsed[1].BankReference, parsed[1].CounterpartyName, parsed[1].Description, Now);

        // Fingerprints remain deterministic for duplicate detection across imports
        Assert.Equal(
            debitLine.LineFingerprintSha256,
            BankStatementLine.ComputeFingerprintSha256(bankId, new DateOnly(2026, 11, 15), "debit", 2_220_000m, " trf-bca-001 "));

        // CREDIT line cannot match outbound supplier payment
        Assert.Throws<InvalidOperationException>(() => creditLine.ApplyMatch(100_000m, Now));

        debitLine.ApplyMatch(1_000_000m, Now);
        Assert.Equal(BankStatementLineStatuses.PartiallyMatched, debitLine.Status);
        Assert.Equal(1_220_000m, debitLine.UnmatchedAmount);

        debitLine.ApplyMatch(1_220_000m, Now);
        Assert.Equal(BankStatementLineStatuses.Matched, debitLine.Status);
        Assert.Equal(0m, debitLine.UnmatchedAmount);

        debitLine.ReverseMatch(2_220_000m, Now);
        Assert.Equal(BankStatementLineStatuses.Unmatched, debitLine.Status);
        Assert.Equal(2_220_000m, debitLine.UnmatchedAmount);
    }
}
