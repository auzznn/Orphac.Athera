using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Modules.AccountsPayable;

public sealed record BillLineDraftInput(
    Guid ReceiptAccrualLineId,
    decimal BilledBaseQuantity,
    decimal InvoiceUnitPriceBase,
    Guid PpnTaxCodeId,
    Guid? WithholdingTaxCodeId = null,
    string? LineNotes = null);

public sealed record PreviewSupplierBillCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    string BillType,
    Guid PurchaseOrderId,
    string? CurrencyCode,
    DateOnly InvoiceDate,
    decimal DeclaredGrossAmount,
    IReadOnlyList<BillLineDraftInput> Lines,
    DateOnly? PostingDate = null);

public sealed record CreateSupplierBillCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    string BillType,
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    string? TaxInvoiceNumber,
    string? WithholdingSlipReference,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? CurrencyCode,
    decimal DeclaredGrossAmount,
    string? Notes,
    IReadOnlyList<BillLineDraftInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record UpdateSupplierBillCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierBillId,
    string ExpectedFunctionalCurrencyCode,
    string SupplierInvoiceNumber,
    string? TaxInvoiceNumber,
    string? WithholdingSlipReference,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    decimal DeclaredGrossAmount,
    string? Notes,
    IReadOnlyList<BillLineDraftInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SubmitSupplierBillCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierBillId,
    string ExpectedFunctionalCurrencyCode,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record DecideSupplierBillCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierBillId,
    string Decision,
    string Reason,
    bool ApprovePriceVariance,
    bool ApplyPaymentBlock,
    string? PaymentBlockReason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record PostSupplierBillCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierBillId,
    string ExpectedFunctionalCurrencyCode,
    DateOnly PostingDate,
    string IdempotencyKey,
    Guid ActorUserId,
    DateTimeOffset NowUtc,
    Action? FaultAfterSubledgerBeforeCommit = null);

public sealed record UpdateBillPaymentBlockCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierBillId,
    string TargetBlockStatus,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record BillJournalSuggestion(
    string Side,
    string AccountCode,
    string AccountName,
    decimal Amount,
    string Description);

public sealed record BillPreviewOutcome(
    bool Succeeded,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    Guid PurchaseOrderId,
    string PoNumber,
    Guid SupplierPartnerId,
    string SupplierCode,
    string SupplierLegalName,
    string CurrencyCode,
    DateOnly InvoiceDate,
    InvoiceMatchPolicy? Policy,
    InvoiceMatchComputation? Computation,
    IReadOnlyList<BillJournalSuggestion> JournalPreview,
    IReadOnlyList<string> BlockingReasons)
{
    public static BillPreviewOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, statusCode, errorCode, detail, Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, "IDR", default, null, null, Array.Empty<BillJournalSuggestion>(), new[] { detail });
}

public sealed record BillCommandOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    SupplierBill? Bill,
    IReadOnlyList<SupplierBillLine> Lines,
    InvoiceMatchEvaluation? Evaluation,
    PayableItem? PayableItem,
    JournalEntry? GlJournal,
    IReadOnlyList<SupplierBillReceiptMatch> ReceiptMatches,
    IReadOnlyList<InvoicePriceAdjustment> PriceAdjustments,
    IReadOnlyList<TaxDetermination> TaxDeterminations)
{
    public static BillCommandOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, false, statusCode, errorCode, detail, null, Array.Empty<SupplierBillLine>(), null, null, null, Array.Empty<SupplierBillReceiptMatch>(), Array.Empty<InvoicePriceAdjustment>(), Array.Empty<TaxDetermination>());
}

/// <summary>
/// Cross-module Accounts Payable coordinator owning three-way matching (PP/DQ/BD), four-eyes approval (`OD-02`),
/// atomic `TX-BILL` posting (GRNI clearing, `DEC-P2P-013` stock-coverage apportionment & PPV, `OD-07` tax evidence, AP, GL, outbox),
/// and audited payment block/release state (`OD-03`, `OD-08`).
/// </summary>
public static class SupplierBillCoordinator
{
    public static async Task<InvoiceMatchPolicy> EnsureDefaultMatchPolicyAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.InvoiceMatchPolicies
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.Status == MasterStatuses.Active && p.IsDefault)
            .OrderBy(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        var policyId = Guid.NewGuid();
        var defaultCode = ApPolicyDefaults.DefaultMatchPolicyCode;
        var defaultName = "Exact Three-Way Match Only (OD-03 Safe Default)";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO ap.invoice_match_policies (
                policy_id, tenant_id, legal_entity_id, code, name, review_status,
                pp_tolerance_percent, pp_tolerance_amount, dq_tolerance_percent, dq_tolerance_base_qty,
                bd_write_off_limit_amount, bd_write_off_account_id, is_default, status,
                created_at_utc, updated_at_utc, version
            )
            VALUES (
                {policyId}, {tenantId}, {entityId}, {defaultCode}, {defaultName}, {InvoiceMatchPolicyReviewStatuses.UnreviewedSafeDefault},
                0, 0, 0, 0, 0, NULL, TRUE, {MasterStatuses.Active},
                {nowUtc}, {nowUtc}, 1
            )
            ON CONFLICT (tenant_id, legal_entity_id, code) DO NOTHING
            """,
            ct);

        return await db.InvoiceMatchPolicies
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.Code == defaultCode)
            .SingleAsync(ct);
    }

    public static async Task<BillPreviewOutcome> PreviewBillAsync(
        AtheraDbContext db,
        PreviewSupplierBillCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var expectedCurrency = GuardValidation.RequireExactIsoCode(cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));
            var requestedCurrency = string.IsNullOrWhiteSpace(cmd.CurrencyCode)
                ? expectedCurrency
                : GuardValidation.RequireExactIsoCode(cmd.CurrencyCode, expectedCurrency, 3, nameof(cmd.CurrencyCode));

            SupplierBillTypes.EnsureSupported(cmd.BillType);
            if (!string.Equals(requestedCurrency, expectedCurrency, StringComparison.Ordinal))
            {
                return BillPreviewOutcome.Fail(400, "unsupported_currency_mvp", $"AP bill currency must match legal entity functional currency '{expectedCurrency}'.");
            }

            if (cmd.Lines is null || cmd.Lines.Count == 0)
            {
                return BillPreviewOutcome.Fail(400, "bill_lines_required", "At least one matched receipt accrual line is required.");
            }

            var po = await db.PurchaseOrders.AsNoTracking()
                .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PurchaseOrderId == cmd.PurchaseOrderId, ct);
            if (po is null || po.CurrentApprovedRevisionId is null || string.IsNullOrWhiteSpace(po.PoNumber))
            {
                return BillPreviewOutcome.Fail(404, "po_not_found_or_not_approved", "Approved purchase order was not found in this legal entity.");
            }

            var partner = await db.Partners.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.PartnerId == po.SupplierPartnerId, ct);

            var policy = await db.InvoiceMatchPolicies.AsNoTracking()
                .Where(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.Status == MasterStatuses.Active)
                .OrderByDescending(p => p.IsDefault)
                .FirstOrDefaultAsync(ct)
                ?? InvoiceMatchPolicy.CreateSafeDefault(Guid.NewGuid(), cmd.TenantId, cmd.LegalEntityId, ApPolicyDefaults.DefaultMatchPolicyCode, "Exact Three-Way Match Only (OD-03 Safe Default)", true, DateTimeOffset.UtcNow);

            var eval = await BuildComputationAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                expectedCurrency,
                po,
                partner,
                policy,
                cmd.InvoiceDate,
                cmd.DeclaredGrossAmount,
                cmd.Lines,
                ct);

            var journalRes = await BuildJournalPlanAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                policy,
                eval.Computation,
                eval.PpnAccountMap,
                eval.WhAccountMap,
                cmd.PostingDate ?? cmd.InvoiceDate,
                checkPeriodAndAccounts: cmd.PostingDate.HasValue,
                po.PoNumber,
                ct);

            var combinedBlocks = eval.Computation.BlockingReasons.Concat(journalRes.BlockingReasons).Distinct(StringComparer.Ordinal).ToList();
            var finalComputation = journalRes.BlockingReasons.Count == 0
                ? eval.Computation
                : eval.Computation with
                {
                    OverallMatchStatus = InvoiceMatchStatuses.PostingBlocked,
                    IsPostingBlocked = true,
                    BlockingReasons = combinedBlocks
                };

            return new BillPreviewOutcome(
                true,
                200,
                null,
                null,
                po.PurchaseOrderId,
                po.PoNumber,
                partner.PartnerId,
                partner.PartnerCode,
                partner.LegalName,
                expectedCurrency,
                cmd.InvoiceDate,
                policy,
                finalComputation,
                journalRes.Suggestions,
                combinedBlocks);
        }
        catch (ArgumentException ex)
        {
            return BillPreviewOutcome.Fail(400, "bill_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            var code = idx > 0 ? ex.Message[..idx].Trim() : "bill_policy_blocked";
            var detail = idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message;
            return BillPreviewOutcome.Fail(409, code, detail);
        }
    }

    public static async Task<BillCommandOutcome> CreateDraftAsync(
        AtheraDbContext db,
        CreateSupplierBillCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var expectedCurrency = GuardValidation.RequireExactIsoCode(cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));
            var requestedCurrency = string.IsNullOrWhiteSpace(cmd.CurrencyCode)
                ? expectedCurrency
                : GuardValidation.RequireExactIsoCode(cmd.CurrencyCode, expectedCurrency, 3, nameof(cmd.CurrencyCode));

            var billType = SupplierBillTypes.EnsureSupported(cmd.BillType);
            if (!string.Equals(requestedCurrency, expectedCurrency, StringComparison.Ordinal))
            {
                return BillCommandOutcome.Fail(400, "unsupported_currency_mvp", $"AP bill currency must match legal entity functional currency '{expectedCurrency}'.");
            }

            if (cmd.Lines is null || cmd.Lines.Count == 0)
            {
                return BillCommandOutcome.Fail(400, "bill_lines_required", "At least one matched receipt accrual line is required.");
            }

            var po = await db.PurchaseOrders.AsNoTracking()
                .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PurchaseOrderId == cmd.PurchaseOrderId, ct);
            if (po is null || po.CurrentApprovedRevisionId is null || string.IsNullOrWhiteSpace(po.PoNumber))
            {
                return BillCommandOutcome.Fail(404, "po_not_found_or_not_approved", "Approved purchase order was not found in this legal entity.");
            }

            var partner = await db.Partners.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.PartnerId == po.SupplierPartnerId, ct);

            var cleanVendorInv = GuardValidation.RequireName(cmd.SupplierInvoiceNumber, nameof(cmd.SupplierInvoiceNumber), 64);
            var duplicateVendorInv = await db.SupplierBills.AsNoTracking().AnyAsync(b =>
                b.TenantId == cmd.TenantId &&
                b.LegalEntityId == cmd.LegalEntityId &&
                b.SupplierPartnerId == partner.PartnerId &&
                b.SupplierInvoiceNumber == cleanVendorInv &&
                b.Status != SupplierBillStatuses.Cancelled,
                ct);
            if (duplicateVendorInv)
            {
                return BillCommandOutcome.Fail(
                    409,
                    "duplicate_supplier_invoice_number",
                    $"Supplier invoice number '{cleanVendorInv}' already exists for supplier '{partner.PartnerCode}' in this legal entity.");
            }

            var policy = await EnsureDefaultMatchPolicyAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);
            var eval = await BuildComputationAsync(
                db, cmd.TenantId, cmd.LegalEntityId, expectedCurrency, po, partner, policy, cmd.InvoiceDate, cmd.DeclaredGrossAmount, cmd.Lines, ct);

            var billId = Guid.NewGuid();
            var draftCode = $"DRF-BL-{cmd.NowUtc:yyyyMMdd}-{billId.ToString("N")[..6].ToUpperInvariant()}";
            var dueDate = cmd.DueDate ?? cmd.InvoiceDate.AddDays(po.PaymentTermsDays);

            var bill = SupplierBill.CreateDraft(
                billId,
                cmd.TenantId,
                cmd.LegalEntityId,
                draftCode,
                billType,
                po.PurchaseOrderId,
                po.CurrentApprovedRevisionId.Value,
                po.PoNumber,
                partner.PartnerId,
                partner.PartnerCode,
                partner.LegalName,
                partner.TaxRegistrationNpwp,
                cleanVendorInv,
                cmd.TaxInvoiceNumber,
                cmd.WithholdingSlipReference,
                cmd.InvoiceDate,
                dueDate,
                expectedCurrency,
                cmd.DeclaredGrossAmount,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);
            bill.ApplyMatchComputation(eval.Computation, cmd.NowUtc);

            var billLines = CreateBillLines(cmd.TenantId, cmd.LegalEntityId, billId, cmd.Lines, eval.SourceInputs, eval.Computation.Lines);
            var evalRecord = InvoiceMatchEvaluation.Record(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                billId,
                policy.PolicyId,
                MatchEvaluationStages.DraftPreview,
                eval.Computation,
                requiresPaymentBlock: false,
                cmd.ActorUserId,
                cmd.NowUtc);

            db.SupplierBills.Add(bill);
            db.SupplierBillLines.AddRange(billLines);
            db.InvoiceMatchEvaluations.Add(evalRecord);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "AP_BILL_DRAFTED",
                "SUPPLIER_BILL",
                billId.ToString("D"),
                $"Drafted supplier bill {draftCode} (Vendor Inv {cleanVendorInv}) against PO {po.PoNumber} ({eval.Computation.OverallMatchStatus})",
                JsonSerializer.Serialize(new { billId, draftCode, cleanVendorInv, po.PoNumber, eval.Computation.OverallMatchStatus }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new BillCommandOutcome(true, false, 201, null, null, bill, billLines, evalRecord, null, null, Array.Empty<SupplierBillReceiptMatch>(), Array.Empty<InvoicePriceAdjustment>(), Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return BillCommandOutcome.Fail(400, "bill_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<BillCommandOutcome> UpdateDraftAsync(
        AtheraDbContext db,
        UpdateSupplierBillCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var expectedCurrency = GuardValidation.RequireExactIsoCode(cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));
            if (cmd.Lines is null || cmd.Lines.Count == 0)
            {
                return BillCommandOutcome.Fail(400, "bill_lines_required", "At least one matched receipt accrual line is required.");
            }

            var bill = await LockBillForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierBillId, ct);
            if (bill is null)
            {
                return BillCommandOutcome.Fail(404, "bill_not_found", "Supplier bill was not found in this legal entity.");
            }

            if (bill.Status != SupplierBillStatuses.Draft)
            {
                return BillCommandOutcome.Fail(409, "bill_not_editable", $"Supplier bill in status '{bill.Status}' cannot be edited.");
            }

            var cleanVendorInv = GuardValidation.RequireName(cmd.SupplierInvoiceNumber, nameof(cmd.SupplierInvoiceNumber), 64);
            var duplicateVendorInv = await db.SupplierBills.AsNoTracking().AnyAsync(b =>
                b.TenantId == cmd.TenantId &&
                b.LegalEntityId == cmd.LegalEntityId &&
                b.SupplierPartnerId == bill.SupplierPartnerId &&
                b.SupplierInvoiceNumber == cleanVendorInv &&
                b.SupplierBillId != bill.SupplierBillId &&
                b.Status != SupplierBillStatuses.Cancelled,
                ct);
            if (duplicateVendorInv)
            {
                return BillCommandOutcome.Fail(
                    409,
                    "duplicate_supplier_invoice_number",
                    $"Supplier invoice number '{cleanVendorInv}' already exists for this supplier.");
            }

            var po = await db.PurchaseOrders.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PurchaseOrderId == bill.PurchaseOrderId, ct);
            var partner = await db.Partners.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.PartnerId == bill.SupplierPartnerId, ct);

            var policy = await EnsureDefaultMatchPolicyAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);
            var eval = await BuildComputationAsync(
                db, cmd.TenantId, cmd.LegalEntityId, expectedCurrency, po, partner, policy, cmd.InvoiceDate, cmd.DeclaredGrossAmount, cmd.Lines, ct);

            var existingLines = await db.SupplierBillLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierBillId == bill.SupplierBillId)
                .ToListAsync(ct);
            db.SupplierBillLines.RemoveRange(existingLines);

            var dueDate = cmd.DueDate ?? cmd.InvoiceDate.AddDays(po.PaymentTermsDays);
            bill.UpdateDraftHeader(
                cleanVendorInv,
                cmd.TaxInvoiceNumber,
                cmd.WithholdingSlipReference,
                cmd.InvoiceDate,
                dueDate,
                cmd.DeclaredGrossAmount,
                cmd.Notes,
                cmd.NowUtc);
            bill.ApplyMatchComputation(eval.Computation, cmd.NowUtc);

            var newLines = CreateBillLines(cmd.TenantId, cmd.LegalEntityId, bill.SupplierBillId, cmd.Lines, eval.SourceInputs, eval.Computation.Lines);
            var evalRecord = InvoiceMatchEvaluation.Record(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                bill.SupplierBillId,
                policy.PolicyId,
                MatchEvaluationStages.DraftPreview,
                eval.Computation,
                requiresPaymentBlock: false,
                cmd.ActorUserId,
                cmd.NowUtc);

            db.SupplierBillLines.AddRange(newLines);
            db.InvoiceMatchEvaluations.Add(evalRecord);
            await db.SaveChangesAsync(ct);

            return new BillCommandOutcome(true, false, 200, null, null, bill, newLines, evalRecord, null, null, Array.Empty<SupplierBillReceiptMatch>(), Array.Empty<InvoicePriceAdjustment>(), Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return BillCommandOutcome.Fail(400, "bill_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<BillCommandOutcome> SubmitBillAsync(
        AtheraDbContext db,
        SubmitSupplierBillCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var bill = await LockBillForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierBillId, ct);
            if (bill is null)
            {
                return BillCommandOutcome.Fail(404, "bill_not_found", "Supplier bill was not found in this legal entity.");
            }

            if (bill.Status != SupplierBillStatuses.Draft)
            {
                return BillCommandOutcome.Fail(409, "bill_invalid_transition", $"Supplier bill in status '{bill.Status}' cannot be submitted.");
            }

            var billLines = await db.SupplierBillLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierBillId == bill.SupplierBillId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);
            if (billLines.Count == 0)
            {
                return BillCommandOutcome.Fail(400, "bill_lines_required", "Supplier bill has zero lines.");
            }

            var po = await db.PurchaseOrders.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PurchaseOrderId == bill.PurchaseOrderId, ct);
            var partner = await db.Partners.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.PartnerId == bill.SupplierPartnerId, ct);
            var policy = await EnsureDefaultMatchPolicyAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);

            var draftInputs = billLines.Select(l => new BillLineDraftInput(
                l.ReceiptAccrualLineId, l.BilledBaseQuantity, l.InvoiceUnitPriceBase, l.PpnTaxCodeId, l.WithholdingTaxCodeId, l.LineNotes)).ToList();

            var eval = await BuildComputationAsync(
                db, cmd.TenantId, cmd.LegalEntityId, cmd.ExpectedFunctionalCurrencyCode, po, partner, policy, bill.InvoiceDate, bill.DeclaredGrossAmount, draftInputs, ct);

            bill.ApplyMatchComputation(eval.Computation, cmd.NowUtc);

            var evalRecord = InvoiceMatchEvaluation.Record(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                bill.SupplierBillId,
                policy.PolicyId,
                MatchEvaluationStages.Submit,
                eval.Computation,
                requiresPaymentBlock: false,
                cmd.ActorUserId,
                cmd.NowUtc);
            db.InvoiceMatchEvaluations.Add(evalRecord);

            if (eval.Computation.IsPostingBlocked)
            {
                await db.SaveChangesAsync(ct);
                return BillCommandOutcome.Fail(
                    409,
                    "three_way_match_blocked",
                    string.Join(" | ", eval.Computation.BlockingReasons));
            }

            var seq = await IncrementApYearSequenceAsync(
                db, "ap.bill_sequences", cmd.TenantId, cmd.LegalEntityId, bill.InvoiceDate.Year, ct);
            var assignedBillNumber = bill.BillNumber ?? $"BILL-{bill.InvoiceDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";

            bill.MarkSubmitted(assignedBillNumber, cmd.ActorUserId, cmd.NowUtc);

            var approvalPolicy = await db.ApprovalPolicies.AsNoTracking()
                .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PolicyCode == ApPolicyDefaults.SupplierBillApprovalPolicyCode && p.IsActive, ct);
            if (approvalPolicy is null)
            {
                approvalPolicy = ApprovalPolicy.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    ApPolicyDefaults.SupplierBillApprovalPolicyCode,
                    "Supplier Bill Four-Eyes & Price Variance Approval",
                    requiresFourEyes: true,
                    requiresApproverMfa: false,
                    cmd.NowUtc);
                db.ApprovalPolicies.Add(approvalPolicy);
            }

            var existingPendingApprovals = await db.DocumentApprovals
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == bill.SupplierBillId && a.Status == DocumentApprovalStatuses.Pending)
                .ToListAsync(ct);
            foreach (var oldApproval in existingPendingApprovals)
            {
                oldApproval.Cancel(cmd.NowUtc);
            }

            var nextRevNo = existingPendingApprovals.Count + 1;
            var totalPrior = await db.DocumentApprovals.CountAsync(
                a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == bill.SupplierBillId,
                ct);
            nextRevNo = Math.Max(1, totalPrior + 1);

            db.DocumentApprovals.Add(DocumentApproval.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                approvalPolicy.PolicyId,
                "ACCOUNTS_PAYABLE",
                "SUPPLIER_BILL",
                bill.SupplierBillId,
                bill.SupplierBillId,
                nextRevNo,
                $"Approve supplier bill {assignedBillNumber} ({bill.SupplierInvoiceNumber}) [{eval.Computation.OverallMatchStatus}]",
                cmd.ActorUserId,
                cmd.NowUtc));

            var payloadJson = JsonSerializer.Serialize(new
            {
                bill.SupplierBillId,
                BillNumber = assignedBillNumber,
                bill.SupplierInvoiceNumber,
                eval.Computation.OverallMatchStatus,
                eval.Computation.TotalPriceVarianceAmount
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "AP_BILL_SUBMITTED",
                "SUPPLIER_BILL",
                bill.SupplierBillId.ToString("D"),
                $"Submitted supplier bill {assignedBillNumber} ({eval.Computation.OverallMatchStatus}) for four-eyes approval",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "finance.supplierbill.submitted.v1",
                $"ap:bill-submit:{bill.SupplierBillId:D}:{nextRevNo}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new BillCommandOutcome(true, false, 200, null, null, bill, billLines, evalRecord, null, null, Array.Empty<SupplierBillReceiptMatch>(), Array.Empty<InvoicePriceAdjustment>(), Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return BillCommandOutcome.Fail(400, "bill_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<BillCommandOutcome> DecideBillAsync(
        AtheraDbContext db,
        DecideSupplierBillCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var bill = await LockBillForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierBillId, ct);
            if (bill is null)
            {
                return BillCommandOutcome.Fail(404, "bill_not_found", "Supplier bill was not found in this legal entity.");
            }

            if (bill.Status != SupplierBillStatuses.PendingApproval)
            {
                return BillCommandOutcome.Fail(409, "bill_not_pending_approval", $"Supplier bill in status '{bill.Status}' is not awaiting an approval decision.");
            }

            bill.RecordApprovalDecision(
                cmd.ActorUserId,
                cmd.Decision,
                cmd.Reason,
                cmd.ApprovePriceVariance,
                cmd.ApplyPaymentBlock,
                cmd.PaymentBlockReason,
                cmd.NowUtc);

            var pendingApproval = await db.DocumentApprovals
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == bill.SupplierBillId && a.Status == DocumentApprovalStatuses.Pending)
                .OrderByDescending(a => a.RevisionNumber)
                .FirstOrDefaultAsync(ct);
            pendingApproval?.RecordDecision(cmd.ActorUserId, cmd.Decision, cmd.Reason, mfaVerified: false, cmd.NowUtc);

            var lines = await db.SupplierBillLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierBillId == bill.SupplierBillId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                bill.Status == SupplierBillStatuses.Approved ? "AP_BILL_APPROVED" : "AP_BILL_REJECTED",
                "SUPPLIER_BILL",
                bill.SupplierBillId.ToString("D"),
                $"Decided supplier bill {bill.BillNumber ?? bill.DraftCode}: {cmd.Decision} ({cmd.Reason})",
                JsonSerializer.Serialize(new
                {
                    bill.SupplierBillId,
                    bill.BillNumber,
                    cmd.Decision,
                    cmd.ApprovePriceVariance,
                    cmd.ApplyPaymentBlock
                }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new BillCommandOutcome(true, false, 200, null, null, bill, lines, null, null, null, Array.Empty<SupplierBillReceiptMatch>(), Array.Empty<InvoicePriceAdjustment>(), Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return BillCommandOutcome.Fail(400, "bill_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<BillCommandOutcome> PostBillAsync(
        AtheraDbContext db,
        PostSupplierBillCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction;
        if (tx is null)
        {
            throw new InvalidOperationException("PostBillAsync requires an active entity PostgreSQL transaction.");
        }

        const string savepointName = "sp_post_supplier_bill";
        await tx.CreateSavepointAsync(savepointName, ct);

        try
        {
            var idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);

            // Lock SupplierBill first for deterministic concurrency across retries/simultaneous posts.
            var bill = await LockBillForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierBillId, ct);
            if (bill is null)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return BillCommandOutcome.Fail(404, "bill_not_found", "Supplier bill was not found in this legal entity.");
            }

            if (bill.Status == SupplierBillStatuses.Posted)
            {
                if (!string.Equals(bill.PostIdempotencyKey, idempKey, StringComparison.Ordinal))
                {
                    await tx.RollbackToSavepointAsync(savepointName, ct);
                    return BillCommandOutcome.Fail(
                        409,
                        "bill_already_posted",
                        $"Supplier bill {bill.BillNumber ?? bill.DraftCode} is already posted with a different idempotency key.");
                }

                await tx.ReleaseSavepointAsync(savepointName, ct);
                return await LoadPostedOutcomeAsync(db, bill, ct);
            }

            if (bill.Status != SupplierBillStatuses.Approved)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return BillCommandOutcome.Fail(
                    409,
                    "bill_must_be_approved_before_post",
                    $"Supplier bill '{bill.BillNumber ?? bill.DraftCode}' is in status '{bill.Status}' and must be independently approved before posting.");
            }

            var billLines = await db.SupplierBillLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierBillId == bill.SupplierBillId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            var po = await db.PurchaseOrders.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PurchaseOrderId == bill.PurchaseOrderId, ct);
            var partner = await db.Partners.AsNoTracking()
                .SingleAsync(p => p.TenantId == cmd.TenantId && p.PartnerId == bill.SupplierPartnerId, ct);
            var policy = await EnsureDefaultMatchPolicyAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);

            // Determine touched ReceiptAccruals cleanly, then lock Accruals and their AccrualLines once in deterministic order.
            var requestedLineIds = billLines.Select(l => l.ReceiptAccrualLineId).Distinct().ToList();
            var accrualIds = await db.ReceiptAccrualLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && requestedLineIds.Contains(l.ReceiptAccrualLineId))
                .Select(l => l.ReceiptAccrualId)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);

            var lockedAccruals = await LockReceiptAccrualsAsync(db, cmd.TenantId, cmd.LegalEntityId, accrualIds, ct);
            var lockedAccrualLines = new Dictionary<Guid, ReceiptAccrualLine>();
            var allLinesByAccrual = new Dictionary<Guid, List<ReceiptAccrualLine>>();
            foreach (var accrualId in accrualIds)
            {
                var allForHeader = await db.ReceiptAccrualLines
                    .FromSqlInterpolated(
                        $"""
                        SELECT * FROM gl.receipt_accrual_lines
                        WHERE tenant_id = {cmd.TenantId}
                          AND legal_entity_id = {cmd.LegalEntityId}
                          AND receipt_accrual_id = {accrualId}
                        ORDER BY line_number ASC
                        FOR UPDATE
                        """)
                    .ToListAsync(ct);
                allLinesByAccrual[accrualId] = allForHeader;
                foreach (var al in allForHeader)
                {
                    if (requestedLineIds.Contains(al.ReceiptAccrualLineId))
                    {
                        lockedAccrualLines[al.ReceiptAccrualLineId] = al;
                    }
                }
            }

            // Lock ordered ValuationPools per item under the live transaction.
            var expectedCurrency = GuardValidation.RequireExactIsoCode(cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));
            var itemUomPairs = billLines.Select(l => (l.ItemId, l.BaseUomId)).Distinct().ToList();
            var lockedInv = await InventoryValuationService.EnsureAndLockItemTargetsAsync(
                db, cmd.TenantId, cmd.LegalEntityId, expectedCurrency, itemUomPairs, cmd.NowUtc, ct);

            // Re-evaluate live 3-way match, DQ capacity, tax dates & DEC-P2P-013 stock coverage against locked rows.
            var draftInputs = billLines.Select(l => new BillLineDraftInput(
                l.ReceiptAccrualLineId, l.BilledBaseQuantity, l.InvoiceUnitPriceBase, l.PpnTaxCodeId, l.WithholdingTaxCodeId, l.LineNotes)).ToList();
            var eval = await BuildComputationAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                expectedCurrency,
                po,
                partner,
                policy,
                bill.InvoiceDate,
                bill.DeclaredGrossAmount,
                draftInputs,
                ct,
                lockedAccrualLines,
                lockedInv.PoolsByItem);

            var journalRes = await BuildJournalPlanAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                policy,
                eval.Computation,
                eval.PpnAccountMap,
                eval.WhAccountMap,
                cmd.PostingDate,
                checkPeriodAndAccounts: true,
                bill.BillNumber ?? bill.DraftCode,
                ct);

            var combinedBlocks = eval.Computation.BlockingReasons.Concat(journalRes.BlockingReasons).Distinct(StringComparer.Ordinal).ToList();
            var liveComp = journalRes.BlockingReasons.Count == 0
                ? eval.Computation
                : eval.Computation with
                {
                    OverallMatchStatus = InvoiceMatchStatuses.PostingBlocked,
                    IsPostingBlocked = true,
                    BlockingReasons = combinedBlocks
                };

            var postEval = InvoiceMatchEvaluation.Record(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                bill.SupplierBillId,
                policy.PolicyId,
                MatchEvaluationStages.PostCommit,
                liveComp,
                bill.PaymentBlockStatus == PaymentBlockStatuses.PaymentBlocked,
                cmd.ActorUserId,
                cmd.NowUtc);

            if (liveComp.IsPostingBlocked)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return BillCommandOutcome.Fail(409, "three_way_match_blocked", string.Join(" | ", combinedBlocks));
            }

            if (liveComp.RequiresApproval && !bill.VarianceApproved)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return BillCommandOutcome.Fail(
                    409,
                    "variance_approval_acknowledgement_required",
                    "Live PP unit-price variance requires explicit independent approver acknowledgement prior to posting.");
            }

            // Check duplicate supplier invoice number across concurrent postings.
            var duplicatePosted = await db.SupplierBills.AsNoTracking().AnyAsync(b =>
                b.TenantId == cmd.TenantId &&
                b.LegalEntityId == cmd.LegalEntityId &&
                b.SupplierPartnerId == bill.SupplierPartnerId &&
                b.SupplierInvoiceNumber == bill.SupplierInvoiceNumber &&
                b.SupplierBillId != bill.SupplierBillId &&
                b.Status == SupplierBillStatuses.Posted,
                ct);
            if (duplicatePosted)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return BillCommandOutcome.Fail(
                    409,
                    "duplicate_supplier_invoice_number",
                    $"Another posted supplier bill already uses supplier invoice number '{bill.SupplierInvoiceNumber}'.");
            }

            var billNumber = bill.BillNumber ?? bill.DraftCode;
            var journalKey = $"apbill:{bill.SupplierBillId:N}:{idempKey}";
            if (journalKey.Length > 80)
            {
                journalKey = $"apbill:{bill.SupplierBillId:N}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempKey)))[..32].ToLowerInvariant()}";
            }

            // 1. Post balanced atomic GL Journal via GlPostingKernel.
            var journalOutcome = await GlPostingKernel.PostJournalAsync(
                db,
                new GlPostJournalCommand(
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    expectedCurrency,
                    cmd.PostingDate,
                    expectedCurrency,
                    $"Supplier Bill {billNumber} ({bill.SupplierInvoiceNumber}) · {bill.SupplierCodeSnapshot}",
                    "ACCOUNTS_PAYABLE",
                    "SUPPLIER_BILL",
                    billNumber,
                    journalKey,
                    journalRes.GlLines,
                    cmd.ActorUserId,
                    cmd.NowUtc),
                ct);

            if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return BillCommandOutcome.Fail(
                    journalOutcome.FailureHttpStatus,
                    journalOutcome.ErrorCode ?? "gl_posting_failed",
                    journalOutcome.ErrorDetail ?? "Failed posting AP / GRNI / Inventory / Tax GL journal.");
            }

            var journalId = journalOutcome.Journal.JournalId;
            bill.ApplyMatchComputation(liveComp, cmd.NowUtc);

            // 2. Apply GRNI Clearing, DEC-P2P-013 Stock Coverage Adjustment, Match Links & Tax Determinations per line.
            var clearings = new List<ReceiptAccrualClearing>(billLines.Count);
            var matches = new List<SupplierBillReceiptMatch>(billLines.Count);
            var valEntries = new List<ValuationEntry>();
            var priceAdjustments = new List<InvoicePriceAdjustment>();
            var taxDeterminations = new List<TaxDetermination>();

            for (var i = 0; i < billLines.Count; i++)
            {
                var billLine = billLines[i];
                var matchLine = liveComp.Lines[i];
                var accLine = lockedAccrualLines[billLine.ReceiptAccrualLineId];

                var clearedAmt = accLine.ApplyClearing(billLine.BilledBaseQuantity, matchLine.GrniClearAmount);
                clearings.Add(ReceiptAccrualClearing.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    accLine.ReceiptAccrualId,
                    accLine.ReceiptAccrualLineId,
                    bill.SupplierBillId,
                    billLine.SupplierBillLineId,
                    journalId,
                    billLine.BilledBaseQuantity,
                    clearedAmt,
                    cmd.PostingDate,
                    cmd.ActorUserId,
                    cmd.NowUtc));

                var coveredQty = 0m;
                var invAdjAmt = 0m;
                var pnlVarAmt = 0m;

                if (matchLine.PriceVarianceAmount != 0m)
                {
                    var pool = lockedInv.PoolsByItem[billLine.ItemId];
                    var adjStep = pool.ApplyInvoicePriceAdjustment(
                        billLine.BilledBaseQuantity,
                        matchLine.PriceVarianceAmount,
                        cmd.NowUtc);

                    coveredQty = adjStep.CoveredBaseQuantity;
                    invAdjAmt = adjStep.InventoryAdjustmentAmount;
                    pnlVarAmt = adjStep.PnlVarianceAmount;

                    Guid? createdValEntryId = null;
                    if (adjStep.InventoryAdjustmentAmount != 0m)
                    {
                        var valEntry = ValuationEntry.RecordPriceAdjustment(
                            Guid.NewGuid(),
                            cmd.TenantId,
                            cmd.LegalEntityId,
                            pool.ValuationPoolId,
                            billLine.ItemId,
                            cmd.PostingDate,
                            expectedCurrency,
                            bill.SupplierBillId,
                            billNumber,
                            adjStep.InventoryAdjustmentAmount,
                            adjStep.ValuationStep,
                            journalId,
                            cmd.ActorUserId,
                            cmd.NowUtc);
                        createdValEntryId = valEntry.ValuationEntryId;
                        valEntries.Add(valEntry);
                    }

                    priceAdjustments.Add(InvoicePriceAdjustment.Record(
                        Guid.NewGuid(),
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        bill.SupplierBillId,
                        billLine.SupplierBillLineId,
                        billNumber,
                        billLine.ItemId,
                        pool.ValuationPoolId,
                        journalId,
                        createdValEntryId,
                        cmd.PostingDate,
                        expectedCurrency,
                        adjStep,
                        cmd.ActorUserId,
                        cmd.NowUtc));
                }

                matches.Add(SupplierBillReceiptMatch.Record(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    bill.SupplierBillId,
                    billLine.SupplierBillLineId,
                    accLine.ReceiptAccrualId,
                    accLine.ReceiptAccrualLineId,
                    accLine.ValuedAcceptanceLineId,
                    po.PurchaseOrderId,
                    accLine.PoLineId,
                    billLine.ItemId,
                    billLine.BilledBaseQuantity,
                    accLine.UnitCostBase,
                    billLine.InvoiceUnitPriceBase,
                    clearedAmt,
                    matchLine.PriceVarianceAmount,
                    coveredQty,
                    invAdjAmt,
                    pnlVarAmt,
                    cmd.ActorUserId,
                    cmd.NowUtc));

                var ppnEnv = eval.PpnEnvelopes[i];
                taxDeterminations.Add(TaxDetermination.FromEnvelope(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    "ACCOUNTS_PAYABLE",
                    "SUPPLIER_BILL",
                    bill.SupplierBillId,
                    billLine.SupplierBillLineId,
                    TaxDeterminationRoles.PpnInput,
                    ppnEnv,
                    bill.TaxInvoiceNumber,
                    bill.SupplierNpwpSnapshot,
                    bill.InvoiceDate,
                    cmd.ActorUserId,
                    cmd.NowUtc));

                var whEnv = eval.WhEnvelopes[i];
                if (whEnv is not null)
                {
                    taxDeterminations.Add(TaxDetermination.FromEnvelope(
                        Guid.NewGuid(),
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        "ACCOUNTS_PAYABLE",
                        "SUPPLIER_BILL",
                        bill.SupplierBillId,
                        billLine.SupplierBillLineId,
                        TaxDeterminationRoles.PphWithholding,
                        whEnv,
                        bill.WithholdingSlipReference,
                        bill.SupplierNpwpSnapshot,
                        bill.InvoiceDate,
                        cmd.ActorUserId,
                        cmd.NowUtc));
                }
            }

            foreach (var accrualId in accrualIds)
            {
                lockedAccruals[accrualId].ApplyLineClearings(allLinesByAccrual[accrualId], cmd.NowUtc);
            }

            var paySeq = await IncrementApYearSequenceAsync(
                db, "ap.payable_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
            var payableNumber = $"AP-{cmd.PostingDate.Year}-{paySeq.ToString("D5", CultureInfo.InvariantCulture)}";

            bill.MarkPosted(journalId, idempKey, cmd.ActorUserId, cmd.NowUtc);
            var payable = PayableItem.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                payableNumber,
                bill,
                journalId,
                cmd.PostingDate,
                cmd.ActorUserId,
                cmd.NowUtc);

            db.ReceiptAccrualClearings.AddRange(clearings);
            db.ValuationEntries.AddRange(valEntries);
            db.InvoicePriceAdjustments.AddRange(priceAdjustments);
            db.SupplierBillReceiptMatches.AddRange(matches);
            db.InvoiceMatchEvaluations.Add(postEval);
            db.TaxDeterminations.AddRange(taxDeterminations);
            db.PayableItems.Add(payable);

            var payloadJson = JsonSerializer.Serialize(new
            {
                bill.SupplierBillId,
                BillNumber = billNumber,
                payable.PayableItemId,
                payable.PayableNumber,
                bill.SupplierInvoiceNumber,
                journalOutcome.Journal.JournalId,
                journalOutcome.Journal.JournalNumber,
                liveComp.TotalGrniClearAmount,
                liveComp.TotalPriceVarianceAmount,
                liveComp.TotalInventoryAdjAmount,
                liveComp.TotalPnlVarianceAmount,
                liveComp.PpnTaxTotal,
                liveComp.WithholdingTaxTotal,
                liveComp.NetPayableAmount,
                bill.PaymentBlockStatus
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "AP_BILL_POSTED",
                "SUPPLIER_BILL",
                bill.SupplierBillId.ToString("D"),
                $"Posted supplier bill {billNumber} -> Payable {payableNumber}, GL {journalOutcome.Journal.JournalNumber} (GRNI cleared {liveComp.TotalGrniClearAmount:N0}, AP net {liveComp.NetPayableAmount:N0})",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "finance.supplierbill.posted.v1",
                $"finance:supplierbill:{bill.SupplierBillId:D}",
                payloadJson,
                cmd.NowUtc));

            if (priceAdjustments.Any(a => a.InventoryAdjustmentAmount != 0m))
            {
                db.OutboxMessages.Add(OutboxMessage.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    "inventory.valuation.adjusted.v1",
                    $"inventory:bill-adj:{bill.SupplierBillId:D}",
                    JsonSerializer.Serialize(new
                    {
                        bill.SupplierBillId,
                        BillNumber = billNumber,
                        journalOutcome.Journal.JournalId,
                        Adjustments = priceAdjustments.Select(a => new
                        {
                            a.AdjustmentId,
                            a.ItemId,
                            a.CoveredBaseQuantity,
                            a.UncoveredBaseQuantity,
                            a.InventoryAdjustmentAmount,
                            a.PnlVarianceAmount,
                            a.PreviousMovingAverageCost,
                            a.NewMovingAverageCost
                        })
                    }),
                    cmd.NowUtc));
            }

            await db.SaveChangesAsync(ct);

            // Optional fault-injection hook executed BEFORE releasing savepoint / commit to prove atomic rollback.
            cmd.FaultAfterSubledgerBeforeCommit?.Invoke();

            await tx.ReleaseSavepointAsync(savepointName, ct);

            return new BillCommandOutcome(
                true,
                false,
                201,
                null,
                null,
                bill,
                billLines,
                postEval,
                payable,
                journalOutcome.Journal,
                matches,
                priceAdjustments,
                taxDeterminations);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackToSavepointAsync(savepointName, ct);
            if (ex is ArgumentException argEx)
            {
                return BillCommandOutcome.Fail(400, "bill_validation_failed", argEx.Message);
            }

            if (ex is InvalidOperationException invEx)
            {
                return MapDomainFailure(invEx);
            }

            throw;
        }
    }

    public static async Task<BillCommandOutcome> UpdatePaymentBlockAsync(
        AtheraDbContext db,
        UpdateBillPaymentBlockCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var bill = await LockBillForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierBillId, ct);
            if (bill is null)
            {
                return BillCommandOutcome.Fail(404, "bill_not_found", "Supplier bill was not found in this legal entity.");
            }

            var payable = await db.PayableItems
                .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.SupplierBillId == bill.SupplierBillId, ct);
            if (payable is null)
            {
                return BillCommandOutcome.Fail(409, "bill_must_be_posted_for_payment_block", "Payable item exists only after the supplier bill has been posted.");
            }

            bill.UpdatePaymentBlock(cmd.TargetBlockStatus, cmd.Reason, cmd.NowUtc);
            payable.UpdatePaymentBlock(cmd.TargetBlockStatus, cmd.Reason, cmd.NowUtc);

            var billLines = await db.SupplierBillLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierBillId == bill.SupplierBillId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                bill.PaymentBlockStatus == PaymentBlockStatuses.PaymentBlocked ? "AP_PAYMENT_BLOCKED" : "AP_PAYMENT_RELEASED",
                "SUPPLIER_BILL",
                bill.SupplierBillId.ToString("D"),
                $"Updated AP payment block on {bill.BillNumber ?? bill.DraftCode} to {bill.PaymentBlockStatus}: {cmd.Reason}",
                JsonSerializer.Serialize(new { bill.SupplierBillId, payable.PayableItemId, bill.PaymentBlockStatus, cmd.Reason }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new BillCommandOutcome(true, false, 200, null, null, bill, billLines, null, payable, null, Array.Empty<SupplierBillReceiptMatch>(), Array.Empty<InvoicePriceAdjustment>(), Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return BillCommandOutcome.Fail(400, "bill_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    private sealed record ComputationContext(
        InvoiceMatchComputation Computation,
        List<MatchLineSourceInput> SourceInputs,
        List<TaxDeterminationEnvelope> PpnEnvelopes,
        List<TaxDeterminationEnvelope?> WhEnvelopes,
        Dictionary<Guid, Guid?> PpnAccountMap,
        Dictionary<Guid, Guid?> WhAccountMap);

    private static async Task<ComputationContext> BuildComputationAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string currencyCode,
        Procurement.PurchaseOrder po,
        Partners.Partner partner,
        InvoiceMatchPolicy policy,
        DateOnly invoiceDate,
        decimal declaredGrossAmount,
        IReadOnlyList<BillLineDraftInput> lines,
        CancellationToken ct,
        IReadOnlyDictionary<Guid, ReceiptAccrualLine>? preLockedAccrualLines = null,
        IReadOnlyDictionary<Guid, ValuationPool>? preLockedPools = null)
    {
        var accrualLineIds = lines.Select(l => l.ReceiptAccrualLineId).Distinct().ToList();
        var accrualLinesMap = preLockedAccrualLines is not null
            ? preLockedAccrualLines
            : await db.ReceiptAccrualLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && accrualLineIds.Contains(l.ReceiptAccrualLineId))
                .ToDictionaryAsync(l => l.ReceiptAccrualLineId, ct);

        if (accrualLinesMap.Count != accrualLineIds.Count)
        {
            throw new InvalidOperationException("accrual_line_not_found: One or more selected receipt accrual lines do not exist in this legal entity.");
        }

        var accrualIds = accrualLinesMap.Values.Select(l => l.ReceiptAccrualId).Distinct().ToList();
        var accrualsMap = await db.ReceiptAccruals.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accrualIds.Contains(a.ReceiptAccrualId))
            .ToDictionaryAsync(a => a.ReceiptAccrualId, ct);

        var poLineIds = accrualLinesMap.Values.Select(l => l.PoLineId).Distinct().ToList();
        var poLinesMap = await db.PurchaseOrderLines.AsNoTracking()
            .Where(pl => pl.TenantId == tenantId && pl.LegalEntityId == entityId && poLineIds.Contains(pl.PoLineId))
            .ToDictionaryAsync(pl => pl.PoLineId, ct);

        var itemIds = accrualLinesMap.Values.Select(l => l.ItemId).Distinct().ToList();
        var itemsMap = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);
        var uomIds = itemsMap.Values.Select(i => i.BaseUomId).Distinct().ToList();
        var uomsMap = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
            .ToDictionaryAsync(u => u.UomId, ct);

        IReadOnlyDictionary<Guid, ValuationPool> poolsMap = preLockedPools is not null
            ? preLockedPools
            : await db.ValuationPools.AsNoTracking()
                .Where(vp => vp.TenantId == tenantId && vp.LegalEntityId == entityId && vp.CurrencyCode == currencyCode && vp.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode && itemIds.Contains(vp.ItemId))
                .ToDictionaryAsync(vp => vp.ItemId, ct);

        var allTaxIds = lines.Select(l => l.PpnTaxCodeId)
            .Concat(lines.Where(l => l.WithholdingTaxCodeId.HasValue).Select(l => l.WithholdingTaxCodeId!.Value))
            .Distinct()
            .ToList();
        var taxCodesMap = await db.TaxCodes.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && allTaxIds.Contains(t.TaxCodeId))
            .ToDictionaryAsync(t => t.TaxCodeId, ct);

        var hasNpwp = !string.IsNullOrWhiteSpace(partner.TaxRegistrationNpwp);
        var sourceInputs = new List<MatchLineSourceInput>(lines.Count);
        var ppnEnvs = new List<TaxDeterminationEnvelope>(lines.Count);
        var whEnvs = new List<TaxDeterminationEnvelope?>(lines.Count);
        var ppnAccountMap = new Dictionary<Guid, Guid?>();
        var whAccountMap = new Dictionary<Guid, Guid?>();

        for (var idx = 0; idx < lines.Count; idx++)
        {
            var input = lines[idx];
            var accLine = accrualLinesMap[input.ReceiptAccrualLineId];
            var accHeader = accrualsMap[accLine.ReceiptAccrualId];
            if (accHeader.PurchaseOrderId != po.PurchaseOrderId)
            {
                throw new InvalidOperationException("accrual_po_mismatch: All matched receipt lines must belong to the selected purchase order.");
            }

            if (!poLinesMap.TryGetValue(accLine.PoLineId, out var poLine))
            {
                throw new InvalidOperationException("po_line_not_found: Purchase order line for accrual was not found.");
            }

            var item = itemsMap[accLine.ItemId];
            var baseUom = uomsMap[item.BaseUomId];
            var pool = poolsMap.GetValueOrDefault(item.ItemId);

            if (!taxCodesMap.TryGetValue(input.PpnTaxCodeId, out var ppnCode))
            {
                throw new InvalidOperationException("foreign_or_missing_tax_code: Selected PPN tax code was not found in this legal entity.");
            }

            if (ppnCode.TaxFamily != TaxFamilies.Ppn || ppnCode.Direction != TaxDirections.Input)
            {
                throw new InvalidOperationException($"invalid_ppn_tax_code: Tax code '{ppnCode.Code}' must belong to PPN / INPUT family.");
            }

            var lineNet = decimal.Round(decimal.Round(input.BilledBaseQuantity, 6, MidpointRounding.AwayFromZero) * decimal.Round(input.InvoiceUnitPriceBase, 6, MidpointRounding.AwayFromZero), 4, MidpointRounding.AwayFromZero);
            if (lineNet <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(lines), "Line net amount must be strictly positive.");
            }

            TaxDeterminationEnvelope ppnEnv;
            var ppnBlocked = false;
            string? ppnBlockReason = null;
            try
            {
                ppnEnv = TaxDeterminationEvaluator.Evaluate(ppnCode, lineNet, invoiceDate, hasNpwp, evidenceProvided: true, currencyScale: 0);
                ppnBlocked = ppnEnv.IsBlocked;
                ppnBlockReason = ppnEnv.BlockedReason;
            }
            catch (InvalidOperationException ex)
            {
                ppnBlocked = true;
                ppnBlockReason = ex.Message;
                ppnEnv = new TaxDeterminationEnvelope(ppnCode.TaxCodeId, ppnCode.Code, ppnCode.TaxFamily, ppnCode.Direction, ppnCode.TreatmentClass, lineNet, lineNet, ppnCode.NominalRatePercent, ppnCode.NominalRatePercent, 0m, ppnCode.ReviewStatus, TaxEvidenceStatuses.NotRequired, true, ex.Message, TaxDeterminationEvaluator.Od07Disclaimer);
            }

            ppnAccountMap[ppnCode.TaxCodeId] = ppnCode.TaxAccountId;
            ppnEnvs.Add(ppnEnv);

            TaxCode? whCode = null;
            TaxDeterminationEnvelope? whEnv = null;
            var whBlocked = false;
            string? whBlockReason = null;
            if (input.WithholdingTaxCodeId is { } whId && whId != Guid.Empty)
            {
                if (!taxCodesMap.TryGetValue(whId, out whCode))
                {
                    throw new InvalidOperationException("foreign_or_missing_tax_code: Selected PPh withholding tax code was not found in this legal entity.");
                }

                if (whCode.TaxFamily != TaxFamilies.Pph || whCode.Direction != TaxDirections.Withholding)
                {
                    throw new InvalidOperationException($"invalid_pph_tax_code: Tax code '{whCode.Code}' must belong to PPH / WITHHOLDING family.");
                }

                try
                {
                    whEnv = TaxDeterminationEvaluator.Evaluate(whCode, lineNet, invoiceDate, hasNpwp, evidenceProvided: true, currencyScale: 0);
                    whBlocked = whEnv.IsBlocked;
                    whBlockReason = whEnv.BlockedReason;
                }
                catch (InvalidOperationException ex)
                {
                    whBlocked = true;
                    whBlockReason = ex.Message;
                    whEnv = new TaxDeterminationEnvelope(whCode.TaxCodeId, whCode.Code, whCode.TaxFamily, whCode.Direction, whCode.TreatmentClass, lineNet, lineNet, whCode.NominalRatePercent, whCode.NominalRatePercent, 0m, whCode.ReviewStatus, TaxEvidenceStatuses.NotRequired, true, ex.Message, TaxDeterminationEvaluator.Od07Disclaimer);
                }

                whAccountMap[whCode.TaxCodeId] = whCode.TaxAccountId;
            }

            whEnvs.Add(whEnv);

            var poUnitBase = poLine.OrderedBaseQuantity > 0m
                ? decimal.Round(poLine.LineSubtotalAmount / poLine.OrderedBaseQuantity, 6, MidpointRounding.AwayFromZero)
                : poLine.UnitPrice;

            sourceInputs.Add(new MatchLineSourceInput(
                idx + 1,
                accLine.ReceiptAccrualLineId,
                accLine.ValuedAcceptanceLineId,
                accLine.GoodsReceiptLineId,
                accLine.PoLineId,
                accLine.ItemId,
                item.ItemCode,
                item.Name,
                item.BaseUomId,
                baseUom.UomCode,
                poLine.OrderedBaseQuantity,
                accLine.RemainingUnbilledBaseQuantity,
                accLine.RemainingOpenLineAmount,
                accLine.UnitCostBase,
                poUnitBase,
                input.BilledBaseQuantity,
                input.InvoiceUnitPriceBase,
                pool?.ValuedBaseQuantity ?? 0m,
                pool?.CarryingValueAmount ?? 0m,
                pool?.MovingAverageUnitCost ?? 0m,
                ppnCode.TaxCodeId,
                ppnCode.Code,
                ppnEnv.TaxAmount,
                ppnBlocked,
                ppnBlockReason,
                whCode?.TaxCodeId,
                whCode?.Code,
                whEnv?.TaxAmount ?? 0m,
                whBlocked,
                whBlockReason));
        }

        var computation = InvoiceThreeWayMatchCalculator.Compute(policy, declaredGrossAmount, sourceInputs);
        return new ComputationContext(computation, sourceInputs, ppnEnvs, whEnvs, ppnAccountMap, whAccountMap);
    }

    private sealed record JournalPlanResult(
        List<GlJournalLineInput> GlLines,
        List<BillJournalSuggestion> Suggestions,
        List<string> BlockingReasons);

    private static async Task<JournalPlanResult> BuildJournalPlanAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        InvoiceMatchPolicy policy,
        InvoiceMatchComputation comp,
        Dictionary<Guid, Guid?> ppnAccountMap,
        Dictionary<Guid, Guid?> whAccountMap,
        DateOnly postingDate,
        bool checkPeriodAndAccounts,
        string docRef,
        CancellationToken ct)
    {
        var blocking = new List<string>();

        if (checkPeriodAndAccounts)
        {
            var period = await db.FinancialPeriods.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.StartDate <= postingDate && p.EndDate >= postingDate)
                .OrderBy(p => p.PeriodNumber)
                .FirstOrDefaultAsync(ct);

            if (period is null)
            {
                blocking.Add($"period_not_found_for_date: No fiscal period configured for {postingDate:yyyy-MM-dd}.");
            }
            else if (!period.IsOpenForPosting(postingDate))
            {
                blocking.Add($"period_closed: Fiscal period '{period.PeriodCode}' is '{period.Status}' and refuses postings.");
            }
        }

        var purposes = new[]
        {
            GlMappingPurposes.GrniClearing,
            GlMappingPurposes.InventoryAsset,
            GlMappingPurposes.PurchasePriceVariance,
            GlMappingPurposes.ApTradeControl,
            GlMappingPurposes.PpnInputTax,
            GlMappingPurposes.PphWithholdingPayable
        };
        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && purposes.Contains(m.PurposeCode))
            .ToDictionaryAsync(m => m.PurposeCode, ct);

        var extraAccIds = new List<Guid>();
        if (policy.BdWriteOffAccountId.HasValue) extraAccIds.Add(policy.BdWriteOffAccountId.Value);
        extraAccIds.AddRange(ppnAccountMap.Values.Where(x => x.HasValue).Select(x => x!.Value));
        extraAccIds.AddRange(whAccountMap.Values.Where(x => x.HasValue).Select(x => x!.Value));
        var allAccIds = mappings.Values.Select(m => m.AccountId).Concat(extraAccIds).Distinct().ToList();

        var accounts = allAccIds.Count == 0
            ? new Dictionary<Guid, GlAccount>()
            : await db.GlAccounts.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && allAccIds.Contains(a.AccountId))
                .ToDictionaryAsync(a => a.AccountId, ct);

        GlAccount? ResolvePurpose(string purpose, string missingCode)
        {
            if (!mappings.TryGetValue(purpose, out var map) || !accounts.TryGetValue(map.AccountId, out var acc))
            {
                blocking.Add($"{missingCode}: Configure active GL account mapping for '{purpose}'.");
                return null;
            }

            if (acc.Status != MasterStatuses.Active || !acc.AllowPosting)
            {
                blocking.Add($"account_not_postable: Account '{acc.AccountCode}' ({purpose}) must be active and postable.");
            }

            return acc;
        }

        var grniAcc = ResolvePurpose(GlMappingPurposes.GrniClearing, "missing_grni_clearing_mapping");
        var invAcc = ResolvePurpose(GlMappingPurposes.InventoryAsset, "missing_inventory_asset_mapping");
        var ppvAcc = ResolvePurpose(GlMappingPurposes.PurchasePriceVariance, "missing_ppv_mapping");
        var apAcc = ResolvePurpose(GlMappingPurposes.ApTradeControl, "missing_ap_trade_control_mapping");
        var ppnDefaultAcc = comp.PpnTaxTotal > 0m ? ResolvePurpose(GlMappingPurposes.PpnInputTax, "missing_ppn_input_mapping") : null;
        var whDefaultAcc = comp.WithholdingTaxTotal > 0m ? ResolvePurpose(GlMappingPurposes.PphWithholdingPayable, "missing_pph_withholding_mapping") : null;

        GlAccount? bdAcc = null;
        if (comp.BdWriteOffAmount != 0m)
        {
            if (policy.BdWriteOffAccountId is not { } bdId || !accounts.TryGetValue(bdId, out bdAcc) || bdAcc.Status != MasterStatuses.Active || !bdAcc.AllowPosting)
            {
                blocking.Add("missing_bd_writeoff_account: BD small-difference account must be active and postable.");
            }
        }

        var glLines = new List<GlJournalLineInput>();
        var suggestions = new List<BillJournalSuggestion>();

        void AddDr(GlAccount? acc, decimal amount, string desc, Guid? itemId = null, Guid? taxCodeId = null)
        {
            var amt = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
            if (amt <= 0m || acc is null) return;
            glLines.Add(new GlJournalLineInput(acc.AccountId, desc, DebitAmount: amt, CreditAmount: 0m, ItemId: itemId, TaxCodeId: taxCodeId));
            suggestions.Add(new BillJournalSuggestion("DEBIT", acc.AccountCode, acc.Name, amt, desc));
        }

        void AddCr(GlAccount? acc, decimal amount, string desc, Guid? itemId = null, Guid? taxCodeId = null)
        {
            var amt = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
            if (amt <= 0m || acc is null) return;
            glLines.Add(new GlJournalLineInput(acc.AccountId, desc, DebitAmount: 0m, CreditAmount: amt, ItemId: itemId, TaxCodeId: taxCodeId));
            suggestions.Add(new BillJournalSuggestion("CREDIT", acc.AccountCode, acc.Name, amt, desc));
        }

        // Dr GRNI Clearing
        AddDr(grniAcc, comp.TotalGrniClearAmount, $"Dr GRNI Clearing · {docRef}");

        // DEC-P2P-013 Stock Coverage Inventory Adjustments & Residual P&L Variance per line
        foreach (var line in comp.Lines)
        {
            if (line.InventoryAdjustmentAmount > 0m)
            {
                AddDr(invAcc, line.InventoryAdjustmentAmount, $"Dr Inventory Price Adj {line.ItemCode} (Cov {line.CoveredBaseQuantity:0.######}) · {docRef}", itemId: line.ItemId);
            }
            else if (line.InventoryAdjustmentAmount < 0m)
            {
                AddCr(invAcc, Math.Abs(line.InventoryAdjustmentAmount), $"Cr Inventory Price Adj {line.ItemCode} (Cov {line.CoveredBaseQuantity:0.######}) · {docRef}", itemId: line.ItemId);
            }

            if (line.PnlVarianceAmount > 0m)
            {
                AddDr(ppvAcc, line.PnlVarianceAmount, $"Dr Price Variance Exp {line.ItemCode} (Uncov {line.UncoveredBaseQuantity:0.######}) · {docRef}", itemId: line.ItemId);
            }
            else if (line.PnlVarianceAmount < 0m)
            {
                AddCr(ppvAcc, Math.Abs(line.PnlVarianceAmount), $"Cr Price Variance Gain {line.ItemCode} (Uncov {line.UncoveredBaseQuantity:0.######}) · {docRef}", itemId: line.ItemId);
            }

            if (line.PpnTaxAmount > 0m)
            {
                var linePpnAcc = ppnAccountMap.TryGetValue(line.PpnTaxCodeId, out var accId) && accId.HasValue && accounts.TryGetValue(accId.Value, out var explicitPpnAcc)
                    ? explicitPpnAcc
                    : ppnDefaultAcc;
                AddDr(linePpnAcc, line.PpnTaxAmount, $"Dr PPN Input ({line.PpnTaxCodeSnapshot}) {line.ItemCode} · {docRef}", itemId: line.ItemId, taxCodeId: line.PpnTaxCodeId);
            }

            if (line.WithholdingTaxAmount > 0m && line.WithholdingTaxCodeId.HasValue)
            {
                var lineWhAcc = whAccountMap.TryGetValue(line.WithholdingTaxCodeId.Value, out var accId) && accId.HasValue && accounts.TryGetValue(accId.Value, out var explicitWhAcc)
                    ? explicitWhAcc
                    : whDefaultAcc;
                AddCr(lineWhAcc, line.WithholdingTaxAmount, $"Cr PPh Withholding ({line.WithholdingTaxCodeSnapshot}) {line.ItemCode} · {docRef}", itemId: line.ItemId, taxCodeId: line.WithholdingTaxCodeId);
            }
        }

        if (comp.BdWriteOffAmount > 0m)
        {
            AddDr(bdAcc, comp.BdWriteOffAmount, $"Dr BD Small Diff Write-Off · {docRef}");
        }
        else if (comp.BdWriteOffAmount < 0m)
        {
            AddCr(bdAcc, Math.Abs(comp.BdWriteOffAmount), $"Cr BD Small Diff Write-Off · {docRef}");
        }

        // Cr AP Trade Control for Net Payable Amount (= Gross - Withholding)
        AddCr(apAcc, comp.NetPayableAmount, $"Cr AP Trade Control · {docRef}");

        return new JournalPlanResult(glLines, suggestions, blocking);
    }

    private static List<SupplierBillLine> CreateBillLines(
        Guid tenantId,
        Guid entityId,
        Guid billId,
        IReadOnlyList<BillLineDraftInput> draftInputs,
        IReadOnlyList<MatchLineSourceInput> sources,
        IReadOnlyList<MatchedLineEvaluation> evalLines)
    {
        var result = new List<SupplierBillLine>(evalLines.Count);
        for (var i = 0; i < evalLines.Count; i++)
        {
            var d = draftInputs[i];
            var s = sources[i];
            var e = evalLines[i];
            result.Add(SupplierBillLine.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                billId,
                e.LineNumber,
                e.ReceiptAccrualLineId,
                e.ValuedAcceptanceLineId,
                e.GoodsReceiptLineId,
                e.PoLineId,
                e.ItemId,
                e.ItemCode,
                e.ItemName,
                e.BaseUomId,
                e.BaseUomCode,
                e.OrderedBaseQty,
                e.RemainingUnbilledBaseQty,
                e.BilledBaseQuantity,
                e.PoUnitPriceFunctional,
                e.InvoiceUnitPriceBase,
                e.LineNetAmount,
                e.GrniClearAmount,
                e.PriceVarianceAmount,
                s.PpnTaxCodeId,
                s.PpnTaxCodeSnapshot,
                e.PpnTaxAmount,
                s.WithholdingTaxCodeId,
                s.WithholdingTaxCodeSnapshot,
                e.WithholdingTaxAmount,
                e.DqOutcome,
                e.PpOutcome,
                d.LineNotes));
        }

        return result;
    }

    private static async Task<SupplierBill?> LockBillForUpdateAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid billId,
        CancellationToken ct)
    {
        return (await db.SupplierBills
            .FromSqlInterpolated(
                $"""
                SELECT * FROM ap.supplier_bills
                WHERE tenant_id = {tenantId}
                  AND legal_entity_id = {entityId}
                  AND supplier_bill_id = {billId}
                FOR UPDATE
                """)
            .ToListAsync(ct))
            .SingleOrDefault();
    }

    private static async Task<long> IncrementApYearSequenceAsync(
        AtheraDbContext db,
        string tableName,
        Guid tenantId,
        Guid entityId,
        int fiscalYear,
        CancellationToken ct)
    {
        var sql = tableName switch
        {
            "ap.bill_sequences" => """
                INSERT INTO ap.bill_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = ap.bill_sequences.last_number + 1
                RETURNING last_number;
                """,
            "ap.payable_sequences" => """
                INSERT INTO ap.payable_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = ap.payable_sequences.last_number + 1
                RETURNING last_number;
                """,
            _ => throw new ArgumentException($"Unsupported AP sequence table '{tableName}'.", nameof(tableName))
        };

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = sql;

        var pTid = cmd.CreateParameter();
        pTid.ParameterName = "tid";
        pTid.Value = tenantId;
        cmd.Parameters.Add(pTid);

        var pEid = cmd.CreateParameter();
        pEid.ParameterName = "eid";
        pEid.Value = entityId;
        cmd.Parameters.Add(pEid);

        var pFy = cmd.CreateParameter();
        pFy.ParameterName = "fy";
        pFy.Value = fiscalYear;
        cmd.Parameters.Add(pFy);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<Dictionary<Guid, ReceiptAccrual>> LockReceiptAccrualsAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        IReadOnlyList<Guid> sortedAccrualIds,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, ReceiptAccrual>(sortedAccrualIds.Count);
        foreach (var accrualId in sortedAccrualIds)
        {
            var locked = (await db.ReceiptAccruals
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM gl.receipt_accruals
                    WHERE tenant_id = {tenantId}
                      AND legal_entity_id = {entityId}
                      AND receipt_accrual_id = {accrualId}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .Single();
            result[accrualId] = locked;
        }

        return result;
    }

    private static async Task<BillCommandOutcome> LoadPostedOutcomeAsync(
        AtheraDbContext db,
        SupplierBill bill,
        CancellationToken ct)
    {
        var lines = await db.SupplierBillLines.AsNoTracking()
            .Where(l => l.TenantId == bill.TenantId && l.LegalEntityId == bill.LegalEntityId && l.SupplierBillId == bill.SupplierBillId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);
        var eval = await db.InvoiceMatchEvaluations.AsNoTracking()
            .Where(e => e.TenantId == bill.TenantId && e.LegalEntityId == bill.LegalEntityId && e.SupplierBillId == bill.SupplierBillId)
            .OrderByDescending(e => e.EvaluatedAtUtc)
            .FirstOrDefaultAsync(ct);
        var payable = await db.PayableItems.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == bill.TenantId && p.LegalEntityId == bill.LegalEntityId && p.SupplierBillId == bill.SupplierBillId, ct);
        var journal = bill.PostedJournalId.HasValue
            ? await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(j => j.TenantId == bill.TenantId && j.LegalEntityId == bill.LegalEntityId && j.JournalId == bill.PostedJournalId.Value, ct)
            : null;
        var matches = await db.SupplierBillReceiptMatches.AsNoTracking()
            .Where(m => m.TenantId == bill.TenantId && m.LegalEntityId == bill.LegalEntityId && m.SupplierBillId == bill.SupplierBillId)
            .ToListAsync(ct);
        var adjs = await db.InvoicePriceAdjustments.AsNoTracking()
            .Where(a => a.TenantId == bill.TenantId && a.LegalEntityId == bill.LegalEntityId && a.SupplierBillId == bill.SupplierBillId)
            .ToListAsync(ct);
        var taxes = await db.TaxDeterminations.AsNoTracking()
            .Where(t => t.TenantId == bill.TenantId && t.LegalEntityId == bill.LegalEntityId && t.SourceDocumentId == bill.SupplierBillId)
            .ToListAsync(ct);

        return new BillCommandOutcome(true, true, 200, null, null, bill, lines, eval, payable, journal, matches, adjs, taxes);
    }

    private static BillCommandOutcome MapDomainFailure(InvalidOperationException ex)
    {
        var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
        var code = idx > 0 ? ex.Message[..idx].Trim() : "ap_domain_error";
        var detail = idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message;
        return BillCommandOutcome.Fail(409, code, detail);
    }
}
