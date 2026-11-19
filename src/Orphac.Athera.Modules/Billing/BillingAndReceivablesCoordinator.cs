using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Fulfillment;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Modules.Billing;

public sealed record BillingLineInput(
    Guid ShipmentLineId,
    decimal BilledBaseQuantity);

public sealed record CreateCustomerInvoiceCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    Guid TaxCodeId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? TaxEvidenceReference,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<BillingLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record PostCustomerInvoiceCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid InvoiceId,
    string? TaxEvidenceReference,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record CreditNoteLineInput(
    Guid OriginalInvoiceLineId,
    decimal CreditedBaseQuantity);

public sealed record CreateAndPostCreditNoteCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid OriginalInvoiceId,
    DateOnly CreditDate,
    string? TaxEvidenceReference,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<CreditNoteLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record BillingOutcome<T>(
    bool Succeeded,
    int StatusCode,
    string? ErrorCode,
    string? Title,
    string? Detail,
    T? Value)
{
    public static BillingOutcome<T> Ok(T value) => new(true, 200, null, null, null, value);
    public static BillingOutcome<T> Created(T value) => new(true, 201, null, null, null, value);
    public static BillingOutcome<T> Fail(int statusCode, string errorCode, string title, string detail) =>
        new(false, statusCode, errorCode, title, detail, default);
}

public sealed record InvoicePostingResult(
    CustomerInvoice Invoice,
    IReadOnlyList<CustomerInvoiceLine> Lines,
    CustomerReceivable Receivable);

public static class BillingTaxCalculator
{
    public static (decimal TaxableBase, decimal TaxAmount, decimal EffectiveRatePercent) ComputeOutputTax(
        decimal subtotalAmount,
        TaxCode taxCode)
    {
        var sub = decimal.Round(subtotalAmount, 2, MidpointRounding.AwayFromZero);
        if (sub <= 0m || taxCode.NominalRatePercent <= 0m)
        {
            return (sub, 0m, 0m);
        }

        var dppRatio = (decimal)taxCode.TaxableBaseRatioNumerator / taxCode.TaxableBaseRatioDenominator;
        var taxableBase = decimal.Round(sub * dppRatio, 2, MidpointRounding.AwayFromZero);
        var taxAmount = decimal.Round(taxableBase * (taxCode.NominalRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        var effectiveRate = decimal.Round(taxCode.NominalRatePercent * dppRatio, 4, MidpointRounding.AwayFromZero);
        return (taxableBase, taxAmount, effectiveRate);
    }

    public static bool RequiresEvidence(TaxCode taxCode) =>
        taxCode.RequiresDocumentaryEvidence
        || taxCode.TreatmentClass is TaxTreatmentClasses.Exempt
            or TaxTreatmentClasses.ZeroRated
            or TaxTreatmentClasses.NotCollectedFacility;
}

public static class BillingAndReceivablesCoordinator
{
    public static async Task<BillingOutcome<(CustomerInvoice Invoice, IReadOnlyList<CustomerInvoiceLine> Lines)>> CreateDraftInvoiceAsync(
        AtheraDbContext db,
        CreateCustomerInvoiceCommand cmd,
        CancellationToken ct)
    {
        if (cmd.Lines is null || cmd.Lines.Count == 0)
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                400, "billing_lines_required", "Invoice lines required", "At least one shipment line must be selected to bill.");
        }

        string idempKey;
        try
        {
            idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);
        }
        catch (ArgumentException ex)
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                400, "billing_idempotency_key_invalid", "Invalid idempotency key", ex.Message);
        }

        var existing = await db.CustomerInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == cmd.TenantId && x.LegalEntityId == cmd.LegalEntityId && x.IdempotencyKey == idempKey, ct);
        if (existing is not null)
        {
            var existingLines = await db.CustomerInvoiceLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.InvoiceId == existing.InvoiceId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Ok((existing, existingLines));
        }

        var so = await db.SalesOrders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == cmd.TenantId && x.LegalEntityId == cmd.LegalEntityId && x.SalesOrderId == cmd.SalesOrderId, ct);
        if (so is null || so.Status != SalesOrderStatuses.Approved || string.IsNullOrWhiteSpace(so.SoNumber))
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                409, "billing_so_not_approved", "Sales order not eligible", "Customer invoices can only be created against approved sales orders.");
        }

        var customer = await db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.PartnerId == so.CustomerPartnerId, ct);
        if (customer is null)
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                404, "billing_customer_not_found", "Customer not found", "The sales order customer partner could not be found.");
        }

        var taxCode = await db.TaxCodes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == cmd.TenantId && t.LegalEntityId == cmd.LegalEntityId && t.TaxCodeId == cmd.TaxCodeId, ct);
        var taxCheck = ValidateOutputTaxCode(taxCode, cmd.InvoiceDate);
        if (taxCheck is not null)
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                taxCheck.Value.Status, taxCheck.Value.Code, taxCheck.Value.Title, taxCheck.Value.Detail);
        }

        var dueDate = cmd.DueDate ?? cmd.InvoiceDate.AddDays(Math.Max(0, so.PaymentTermsDays));
        if (dueDate < cmd.InvoiceDate)
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                400, "billing_due_date_before_invoice_date", "Invalid due date", "Due date cannot precede invoice date.");
        }

        var requestedByShipmentLine = cmd.Lines
            .GroupBy(l => l.ShipmentLineId)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.BilledBaseQuantity), 6, MidpointRounding.AwayFromZero));

        if (requestedByShipmentLine.Values.Any(q => q <= 0m))
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                400, "billing_qty_must_be_positive", "Invalid billing quantity", "Every invoice line quantity must be strictly positive.");
        }

        var orderedIds = requestedByShipmentLine.Keys.OrderBy(id => id).ToList();
        var shipmentLines = await LockShipmentLinesAsync(db, cmd.TenantId, cmd.LegalEntityId, orderedIds, ct);
        if (shipmentLines.Count != orderedIds.Count)
        {
            return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                404, "billing_shipment_line_not_found", "Shipment line not found", "One or more shipment lines were not found in this legal entity.");
        }

        var shipmentIds = shipmentLines.Select(l => l.ShipmentId).Distinct().ToList();
        var shipments = await db.SalesShipments.AsNoTracking()
            .Where(s => s.TenantId == cmd.TenantId && s.LegalEntityId == cmd.LegalEntityId && shipmentIds.Contains(s.ShipmentId))
            .ToDictionaryAsync(s => s.ShipmentId, ct);

        foreach (var sl in shipmentLines)
        {
            if (!shipments.TryGetValue(sl.ShipmentId, out var shp)
                || shp.SalesOrderId != so.SalesOrderId
                || shp.Status is not (ShipmentStatuses.Dispatched or ShipmentStatuses.Delivered))
            {
                return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                    409, "billing_shipment_not_eligible", "Shipment not eligible for billing",
                    "All billed shipment lines must belong to the target sales order and have status DISPATCHED or DELIVERED.");
            }
        }

        var alreadyBilledByShipmentLine = await GetActiveBilledQuantitiesByShipmentLineAsync(db, cmd.TenantId, cmd.LegalEntityId, orderedIds, excludeInvoiceId: null, ct);
        var soLineIds = shipmentLines.Select(l => l.SoLineId).Distinct().ToList();
        var soLines = await db.SalesOrderLines.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && soLineIds.Contains(l.SoLineId))
            .ToDictionaryAsync(l => l.SoLineId, ct);

        var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
            db, "billing.invoice_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.InvoiceDate.Year, ct);
        var invoiceNumber = $"INV-{cmd.InvoiceDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";
        var invoiceId = Guid.NewGuid();

        var createdLines = new List<CustomerInvoiceLine>(orderedIds.Count);
        decimal totalSubtotal = 0m;
        decimal totalTax = 0m;
        var lineNum = 1;

        foreach (var sl in shipmentLines)
        {
            var reqQty = requestedByShipmentLine[sl.ShipmentLineId];
            var billedSoFar = alreadyBilledByShipmentLine.GetValueOrDefault(sl.ShipmentLineId, 0m);
            var remainingCap = decimal.Round(sl.DispatchedBaseQuantity - billedSoFar, 6, MidpointRounding.AwayFromZero);
            if (reqQty > remainingCap)
            {
                return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                    409, "billing_exceeds_shipped_capacity", "Exceeds billable shipment capacity",
                    $"Line {sl.ItemCodeSnapshot} on {shipments[sl.ShipmentId].ShipmentNumber} has only {remainingCap:0.######} {sl.BaseUomCodeSnapshot} unbilled (requested {reqQty:0.######}).");
            }

            if (!soLines.TryGetValue(sl.SoLineId, out var soLine))
            {
                return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                    404, "billing_so_line_not_found", "Sales order line missing", "Referenced sales order line could not be resolved.");
            }

            var unitPricePerBase = soLine.UomFactorToBaseSnapshot > 0m
                ? decimal.Round(soLine.UnitPrice / soLine.UomFactorToBaseSnapshot, 6, MidpointRounding.AwayFromZero)
                : soLine.UnitPrice;
            var lineSubtotal = decimal.Round(reqQty * unitPricePerBase, 2, MidpointRounding.AwayFromZero);
            var (_, lineTax, effectiveRate) = BillingTaxCalculator.ComputeOutputTax(lineSubtotal, taxCode!);

            var line = CustomerInvoiceLine.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                invoiceId,
                lineNum++,
                sl.ShipmentId,
                shipments[sl.ShipmentId].ShipmentNumber,
                sl.ShipmentLineId,
                sl.SoLineId,
                sl.LineageLineKey,
                sl.ItemId,
                sl.ItemCodeSnapshot,
                sl.ItemNameSnapshot,
                sl.BaseUomId,
                sl.BaseUomCodeSnapshot,
                reqQty,
                unitPricePerBase,
                taxCode!.TaxCodeId,
                taxCode.Code,
                effectiveRate,
                lineSubtotal,
                lineTax);

            createdLines.Add(line);
            totalSubtotal = decimal.Round(totalSubtotal + lineSubtotal, 2, MidpointRounding.AwayFromZero);
            totalTax = decimal.Round(totalTax + lineTax, 2, MidpointRounding.AwayFromZero);
        }

        var (_, _, effRateHdr) = BillingTaxCalculator.ComputeOutputTax(totalSubtotal, taxCode!);
        var requiresEvidence = BillingTaxCalculator.RequiresEvidence(taxCode!);
        var summary = $"{taxCode!.Code} ({taxCode.TreatmentClass}, {effRateHdr:0.##}%) · {(requiresEvidence ? "Bukti Faktur/Fasilitas Wajib (OD-07)" : "Tanpa Bukti Tambahan")}";

        var invoice = CustomerInvoice.CreateDraft(
            invoiceId,
            cmd.TenantId,
            cmd.LegalEntityId,
            invoiceNumber,
            InvoiceDocumentTypes.Invoice,
            null,
            null,
            so.SalesOrderId,
            so.SoNumber!,
            customer.PartnerId,
            customer.PartnerCode,
            customer.LegalName,
            customer.TaxRegistrationNpwp,
            cmd.InvoiceDate,
            dueDate,
            so.CurrencyCode,
            totalSubtotal,
            totalTax,
            taxCode.TaxCodeId,
            taxCode.Code,
            taxCode.TreatmentClass,
            effRateHdr,
            requiresEvidence,
            cmd.TaxEvidenceReference,
            summary,
            idempKey,
            cmd.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        db.CustomerInvoices.Add(invoice);
        db.CustomerInvoiceLines.AddRange(createdLines);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "BILLING_INVOICE_DRAFT_CREATED",
            "CustomerInvoice",
            invoice.InvoiceId.ToString("D"),
            $"Draft customer invoice {invoice.InvoiceNumber} created",
            JsonSerializer.Serialize(new { invoice.InvoiceNumber, invoice.SalesOrderNumberSnapshot, invoice.GrandTotalAmount, invoice.TaxCodeSnapshot }),
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Created((invoice, createdLines));
    }

    public static async Task<BillingOutcome<CustomerInvoice>> SubmitInvoiceAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid invoiceId,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var invoice = await db.CustomerInvoices
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.InvoiceId == invoiceId, ct);
        if (invoice is null)
        {
            return BillingOutcome<CustomerInvoice>.Fail(404, "billing_invoice_not_found", "Invoice not found", "The customer invoice does not exist in this entity.");
        }

        try
        {
            invoice.Submit(actorUserId, nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            return BillingOutcome<CustomerInvoice>.Fail(409, "billing_invalid_status_transition", "Cannot submit invoice", ex.Message);
        }

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            tenantId,
            legalEntityId,
            actorUserId,
            "BILLING_INVOICE_SUBMITTED",
            "CustomerInvoice",
            invoice.InvoiceId.ToString("D"),
            $"Customer invoice {invoice.InvoiceNumber} submitted for finance posting",
            JsonSerializer.Serialize(new { invoice.InvoiceNumber, invoice.Status }),
            nowUtc));

        await db.SaveChangesAsync(ct);
        return BillingOutcome<CustomerInvoice>.Ok(invoice);
    }

    public static async Task<BillingOutcome<CustomerInvoice>> CancelInvoiceAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid invoiceId,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var invoice = await db.CustomerInvoices
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.InvoiceId == invoiceId, ct);
        if (invoice is null)
        {
            return BillingOutcome<CustomerInvoice>.Fail(404, "billing_invoice_not_found", "Invoice not found", "The customer invoice does not exist in this entity.");
        }

        try
        {
            invoice.Cancel(nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            return BillingOutcome<CustomerInvoice>.Fail(409, "billing_cannot_cancel_posted", "Cannot cancel invoice", ex.Message);
        }

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            tenantId,
            legalEntityId,
            actorUserId,
            "BILLING_INVOICE_CANCELLED",
            "CustomerInvoice",
            invoice.InvoiceId.ToString("D"),
            $"Customer invoice {invoice.InvoiceNumber} cancelled",
            JsonSerializer.Serialize(new { invoice.InvoiceNumber, invoice.Status }),
            nowUtc));

        await db.SaveChangesAsync(ct);
        return BillingOutcome<CustomerInvoice>.Ok(invoice);
    }

    public static async Task<BillingOutcome<InvoicePostingResult>> PostInvoiceAsync(
        AtheraDbContext db,
        PostCustomerInvoiceCommand cmd,
        CancellationToken ct)
    {
        var invoice = await db.CustomerInvoices
            .FromSqlInterpolated($"""
                SELECT * FROM billing.customer_invoices
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND invoice_id = {cmd.InvoiceId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (invoice is null)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(404, "billing_invoice_not_found", "Invoice not found", "Customer invoice not found in this legal entity.");
        }

        var lines = await db.CustomerInvoiceLines.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.InvoiceId == invoice.InvoiceId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        if (invoice.Status == CustomerInvoiceStatuses.Posted)
        {
            var existingAr = await db.CustomerReceivables.AsNoTracking()
                .SingleAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.InvoiceId == invoice.InvoiceId, ct);
            return BillingOutcome<InvoicePostingResult>.Ok(new InvoicePostingResult(invoice, lines, existingAr));
        }

        if (invoice.Status is not (CustomerInvoiceStatuses.Draft or CustomerInvoiceStatuses.Submitted) || invoice.DocumentType != InvoiceDocumentTypes.Invoice)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                409, "billing_invalid_status_transition", "Invoice cannot be posted",
                $"Only DRAFT or SUBMITTED customer invoices can be posted (current status: '{invoice.Status}').");
        }

        var taxCode = await db.TaxCodes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == cmd.TenantId && t.LegalEntityId == cmd.LegalEntityId && t.TaxCodeId == invoice.TaxCodeId, ct);
        var taxCheck = ValidateOutputTaxCode(taxCode, invoice.InvoiceDate);
        if (taxCheck is not null)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(taxCheck.Value.Status, taxCheck.Value.Code, taxCheck.Value.Title, taxCheck.Value.Detail);
        }

        var effectiveEvidence = !string.IsNullOrWhiteSpace(cmd.TaxEvidenceReference)
            ? cmd.TaxEvidenceReference.Trim()
            : invoice.TaxEvidenceReference;

        if (BillingTaxCalculator.RequiresEvidence(taxCode!) && string.IsNullOrWhiteSpace(effectiveEvidence))
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                409, "billing_tax_evidence_required", "Tax evidence reference required (OD-07)",
                $"Tax code '{taxCode!.Code}' ({taxCode.TreatmentClass}) requires a Faktur Pajak or tax facility evidence reference before posting.");
        }

        // Re-verify shipment capacity under FOR UPDATE lock
        var shipmentLineIds = lines.Select(l => l.ShipmentLineId).Distinct().OrderBy(id => id).ToList();
        var lockedShipmentLines = await LockShipmentLinesAsync(db, cmd.TenantId, cmd.LegalEntityId, shipmentLineIds, ct);
        var shippedMap = lockedShipmentLines.ToDictionary(l => l.ShipmentLineId);
        var otherBilledMap = await GetActiveBilledQuantitiesByShipmentLineAsync(db, cmd.TenantId, cmd.LegalEntityId, shipmentLineIds, excludeInvoiceId: invoice.InvoiceId, ct);

        foreach (var line in lines)
        {
            var maxCap = shippedMap[line.ShipmentLineId].DispatchedBaseQuantity - otherBilledMap.GetValueOrDefault(line.ShipmentLineId, 0m);
            if (line.BilledBaseQuantity > maxCap)
            {
                return BillingOutcome<InvoicePostingResult>.Fail(
                    409, "billing_exceeds_shipped_capacity", "Shipment capacity exceeded",
                    $"Line {line.ItemCodeSnapshot} exceeds remaining unbilled shipment quantity ({maxCap:0.######}).");
            }
        }

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId)
            .ToDictionaryAsync(m => m.PurposeCode, m => m.AccountId, StringComparer.Ordinal, ct);

        if (!mappings.TryGetValue(GlMappingPurposes.ArTradeControl, out var arAccountId)
            || !mappings.TryGetValue(GlMappingPurposes.SalesRevenueDefault, out var revenueAccountId))
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                409, "billing_gl_mapping_missing", "Missing AR or Revenue GL mapping",
                "Entity must configure AR_TRADE_CONTROL (1200) and SALES_REVENUE_DEFAULT (4100) GL mappings before posting.");
        }

        Guid? taxOutputAccountId = taxCode!.TaxAccountId;
        if (invoice.TaxAmount > 0m && (!taxOutputAccountId.HasValue || taxOutputAccountId.Value == Guid.Empty))
        {
            if (mappings.TryGetValue(GlMappingPurposes.PpnOutputTax, out var fallbackPpnOut))
            {
                taxOutputAccountId = fallbackPpnOut;
            }
            else
            {
                return BillingOutcome<InvoicePostingResult>.Fail(
                    409, "billing_tax_gl_account_missing", "Missing Output VAT GL account",
                    $"Tax code '{taxCode.Code}' computes tax ({invoice.TaxAmount:0.00}) but has no Output VAT GL account mapped.");
            }
        }

        var journalLines = new List<GlJournalLineInput>
        {
            new(arAccountId, $"Piutang {invoice.InvoiceNumber} · {invoice.CustomerCodeSnapshot}", invoice.GrandTotalAmount, 0m, invoice.CustomerPartnerId, null, null),
            new(revenueAccountId, $"Pendapatan {invoice.InvoiceNumber} · {invoice.SalesOrderNumberSnapshot}", 0m, invoice.SubtotalAmount, invoice.CustomerPartnerId, null, null)
        };
        if (invoice.TaxAmount > 0m)
        {
            journalLines.Add(new GlJournalLineInput(
                taxOutputAccountId!.Value,
                $"PPN Keluaran {invoice.InvoiceNumber} ({invoice.TaxCodeSnapshot}) · Ref {effectiveEvidence}",
                0m,
                invoice.TaxAmount,
                invoice.CustomerPartnerId,
                null,
                taxCode.TaxCodeId));
        }

        var journalOutcome = await GlPostingKernel.PostJournalAsync(
            db,
            new GlPostJournalCommand(
                cmd.TenantId,
                cmd.LegalEntityId,
                invoice.CurrencyCode,
                invoice.InvoiceDate,
                invoice.CurrencyCode,
                $"Faktur Penjualan {invoice.InvoiceNumber} · {invoice.CustomerCodeSnapshot} ({invoice.SalesOrderNumberSnapshot})",
                "BILLING",
                "CUSTOMER_INVOICE",
                invoice.InvoiceNumber,
                $"billing-inv-post:{invoice.InvoiceId:N}",
                journalLines,
                cmd.ActorUserId,
                cmd.NowUtc),
            ct);

        if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                journalOutcome.FailureHttpStatus,
                journalOutcome.ErrorCode ?? "gl_posting_failed",
                "GL posting rejected",
                journalOutcome.ErrorDetail ?? "Failed to post customer invoice journal entry.");
        }

        var arSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
            db, "ar.receivable_sequences", cmd.TenantId, cmd.LegalEntityId, invoice.InvoiceDate.Year, ct);
        var receivableNumber = $"AR-{invoice.InvoiceDate.Year}-{arSeq.ToString("D5", CultureInfo.InvariantCulture)}";
        var receivable = CustomerReceivable.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            receivableNumber,
            invoice.InvoiceId,
            invoice.InvoiceNumber,
            invoice.CustomerPartnerId,
            invoice.CustomerCodeSnapshot,
            invoice.CustomerNameSnapshot,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.CurrencyCode,
            invoice.GrandTotalAmount,
            journalOutcome.Journal.JournalId,
            journalOutcome.Journal.JournalNumber,
            cmd.NowUtc);

        db.CustomerReceivables.Add(receivable);
        invoice.MarkPosted(
            journalOutcome.Journal.JournalId,
            journalOutcome.Journal.JournalNumber,
            receivable.ReceivableId,
            effectiveEvidence,
            cmd.ActorUserId,
            cmd.NowUtc);

        // Update SO & SO line billed counters
        var so = await db.SalesOrders
            .FirstAsync(x => x.TenantId == cmd.TenantId && x.LegalEntityId == cmd.LegalEntityId && x.SalesOrderId == invoice.SalesOrderId, ct);
        var soLineIds = lines.Select(l => l.SoLineId).Distinct().ToList();
        var soLines = await db.SalesOrderLines
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && soLineIds.Contains(l.SoLineId))
            .ToDictionaryAsync(l => l.SoLineId, ct);

        decimal totalBilledDelta = 0m;
        foreach (var line in lines)
        {
            soLines[line.SoLineId].ApplyFulfillmentDelta(0m, 0m, line.BilledBaseQuantity);
            totalBilledDelta = decimal.Round(totalBilledDelta + line.BilledBaseQuantity, 6, MidpointRounding.AwayFromZero);
        }

        so.ApplyFulfillmentCounters(
            so.TotalReservedBaseQuantity,
            so.TotalDispatchedBaseQuantity,
            decimal.Round(so.TotalBilledBaseQuantity + totalBilledDelta, 6, MidpointRounding.AwayFromZero),
            cmd.NowUtc);

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "billing.customer_invoice.posted",
            invoice.InvoiceId.ToString("D"),
            JsonSerializer.Serialize(new
            {
                invoice.InvoiceId,
                invoice.InvoiceNumber,
                receivable.ReceivableId,
                receivable.ReceivableNumber,
                invoice.GrandTotalAmount,
                invoice.TaxCodeSnapshot,
                invoice.TaxEvidenceReference,
                journalOutcome.Journal.JournalNumber
            }),
            cmd.NowUtc));

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "BILLING_INVOICE_POSTED",
            "CustomerInvoice",
            invoice.InvoiceId.ToString("D"),
            $"Posted customer invoice {invoice.InvoiceNumber} -> {receivable.ReceivableNumber} / {journalOutcome.Journal.JournalNumber}",
            JsonSerializer.Serialize(new
            {
                invoice.InvoiceNumber,
                receivable.ReceivableNumber,
                invoice.SubtotalAmount,
                invoice.TaxAmount,
                invoice.GrandTotalAmount,
                journalOutcome.Journal.JournalNumber
            }),
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return BillingOutcome<InvoicePostingResult>.Ok(new InvoicePostingResult(invoice, lines, receivable));
    }

    public static async Task<BillingOutcome<InvoicePostingResult>> CreateAndPostCreditNoteAsync(
        AtheraDbContext db,
        CreateAndPostCreditNoteCommand cmd,
        CancellationToken ct)
    {
        if (cmd.Lines is null || cmd.Lines.Count == 0)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                400, "billing_credit_lines_required", "Credit note lines required", "At least one original invoice line must be selected to credit.");
        }

        string idempKey;
        try
        {
            idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);
        }
        catch (ArgumentException ex)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(400, "billing_idempotency_key_invalid", "Invalid idempotency key", ex.Message);
        }

        var existingCn = await db.CustomerInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == cmd.TenantId && x.LegalEntityId == cmd.LegalEntityId && x.IdempotencyKey == idempKey, ct);
        if (existingCn is not null)
        {
            var existingLines = await db.CustomerInvoiceLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.InvoiceId == existingCn.InvoiceId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);
            var existingAr = await db.CustomerReceivables.AsNoTracking()
                .SingleAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.ReceivableId == existingCn.ArReceivableId, ct);
            return BillingOutcome<InvoicePostingResult>.Ok(new InvoicePostingResult(existingCn, existingLines, existingAr));
        }

        // Lock original invoice + AR row
        var originalInvoice = await db.CustomerInvoices
            .FromSqlInterpolated($"""
                SELECT * FROM billing.customer_invoices
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND invoice_id = {cmd.OriginalInvoiceId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (originalInvoice is null
            || originalInvoice.DocumentType != InvoiceDocumentTypes.Invoice
            || originalInvoice.Status != CustomerInvoiceStatuses.Posted)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                409, "billing_credit_requires_posted_invoice", "Original invoice not posted",
                "Credit notes can only be issued against a POSTED customer invoice.");
        }

        var receivable = await db.CustomerReceivables
            .FromSqlInterpolated($"""
                SELECT * FROM ar.customer_receivables
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND invoice_id = {originalInvoice.InvoiceId}
                FOR UPDATE
                """)
            .SingleAsync(ct);

        var taxCode = await db.TaxCodes.AsNoTracking()
            .SingleAsync(t => t.TenantId == cmd.TenantId && t.LegalEntityId == cmd.LegalEntityId && t.TaxCodeId == originalInvoice.TaxCodeId, ct);

        var evidenceRef = GuardValidation.NormalizeOptional(cmd.TaxEvidenceReference, 120, nameof(cmd.TaxEvidenceReference));
        if ((originalInvoice.TaxAmount > 0m || BillingTaxCalculator.RequiresEvidence(taxCode)) && string.IsNullOrWhiteSpace(evidenceRef))
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                409, "billing_credit_tax_evidence_required", "Nota Retur / tax evidence required (OD-07)",
                "Issuing a credit note against a taxable or facility invoice requires a Nota Retur / tax evidence reference.");
        }

        var origLines = await db.CustomerInvoiceLines.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.InvoiceId == originalInvoice.InvoiceId)
            .ToDictionaryAsync(l => l.InvoiceLineId, ct);

        var origLineIds = origLines.Keys.ToList();
        var priorCreditedRows = await (
            from l in db.CustomerInvoiceLines.AsNoTracking()
            join hdr in db.CustomerInvoices.AsNoTracking()
                on new { l.TenantId, l.LegalEntityId, l.InvoiceId } equals new { hdr.TenantId, hdr.LegalEntityId, hdr.InvoiceId }
            where l.TenantId == cmd.TenantId
                && l.LegalEntityId == cmd.LegalEntityId
                && hdr.DocumentType == InvoiceDocumentTypes.CreditNote
                && hdr.Status == CustomerInvoiceStatuses.Posted
                && l.OriginalInvoiceLineId.HasValue
                && origLineIds.Contains(l.OriginalInvoiceLineId.Value)
            select new { LineId = l.OriginalInvoiceLineId!.Value, l.BilledBaseQuantity })
            .ToListAsync(ct);

        var creditedByOrigLine = priorCreditedRows
            .GroupBy(x => x.LineId)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.BilledBaseQuantity), 6, MidpointRounding.AwayFromZero));

        var reqByOrigLine = cmd.Lines
            .GroupBy(x => x.OriginalInvoiceLineId)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.CreditedBaseQuantity), 6, MidpointRounding.AwayFromZero));

        if (reqByOrigLine.Values.Any(q => q <= 0m))
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                400, "billing_qty_must_be_positive", "Invalid credit quantity", "Every credit note line quantity must be strictly positive.");
        }

        var cnSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
            db, "billing.credit_note_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.CreditDate.Year, ct);
        var cnNumber = $"CN-{cmd.CreditDate.Year}-{cnSeq.ToString("D5", CultureInfo.InvariantCulture)}";
        var cnId = Guid.NewGuid();

        var cnLines = new List<CustomerInvoiceLine>(reqByOrigLine.Count);
        decimal totalSub = 0m;
        decimal totalTax = 0m;
        var lineNum = 1;

        foreach (var (origLineId, creditQty) in reqByOrigLine)
        {
            if (!origLines.TryGetValue(origLineId, out var origLine))
            {
                return BillingOutcome<InvoicePostingResult>.Fail(
                    404, "billing_original_line_not_found", "Original invoice line not found", "Referenced invoice line does not belong to the original invoice.");
            }

            var remainingCreditable = decimal.Round(origLine.BilledBaseQuantity - creditedByOrigLine.GetValueOrDefault(origLineId, 0m), 6, MidpointRounding.AwayFromZero);
            if (creditQty > remainingCreditable)
            {
                return BillingOutcome<InvoicePostingResult>.Fail(
                    409, "billing_credit_exceeds_invoiced_qty", "Credit exceeds remaining invoiced quantity",
                    $"Line {origLine.ItemCodeSnapshot} only has {remainingCreditable:0.######} {origLine.BaseUomCodeSnapshot} remaining creditable (requested {creditQty:0.######}).");
            }

            var lineSub = decimal.Round(creditQty * origLine.UnitPrice, 2, MidpointRounding.AwayFromZero);
            var (_, lineTax, effRate) = BillingTaxCalculator.ComputeOutputTax(lineSub, taxCode);

            cnLines.Add(CustomerInvoiceLine.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cnId,
                lineNum++,
                origLine.ShipmentId,
                origLine.ShipmentNumberSnapshot,
                origLine.ShipmentLineId,
                origLine.SoLineId,
                origLine.LineageLineKey,
                origLine.ItemId,
                origLine.ItemCodeSnapshot,
                origLine.ItemNameSnapshot,
                origLine.BaseUomId,
                origLine.BaseUomCodeSnapshot,
                creditQty,
                origLine.UnitPrice,
                taxCode.TaxCodeId,
                taxCode.Code,
                effRate,
                lineSub,
                lineTax,
                origLine.InvoiceLineId));

            totalSub = decimal.Round(totalSub + lineSub, 2, MidpointRounding.AwayFromZero);
            totalTax = decimal.Round(totalTax + lineTax, 2, MidpointRounding.AwayFromZero);
        }

        var grandCredit = decimal.Round(totalSub + totalTax, 2, MidpointRounding.AwayFromZero);
        if (grandCredit > receivable.OpenAmount)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                409, "ar_credit_exceeds_open_amount", "Credit exceeds open receivable",
                $"Credit total ({grandCredit:0.00}) exceeds remaining open AR balance ({receivable.OpenAmount:0.00}).");
        }

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId)
            .ToDictionaryAsync(m => m.PurposeCode, m => m.AccountId, StringComparer.Ordinal, ct);

        var arAccountId = mappings[GlMappingPurposes.ArTradeControl];
        var revAccountId = mappings[GlMappingPurposes.SalesRevenueDefault];
        var taxAccountId = taxCode.TaxAccountId ?? (mappings.TryGetValue(GlMappingPurposes.PpnOutputTax, out var fallbackTax) ? fallbackTax : (Guid?)null);

        var journalLines = new List<GlJournalLineInput>
        {
            new(revAccountId, $"Nota Kredit {cnNumber} atas {originalInvoice.InvoiceNumber}", totalSub, 0m, originalInvoice.CustomerPartnerId, null, null),
            new(arAccountId, $"Pengurangan Piutang {cnNumber} ({receivable.ReceivableNumber})", 0m, grandCredit, originalInvoice.CustomerPartnerId, null, null)
        };
        if (totalTax > 0m)
        {
            journalLines.Add(new GlJournalLineInput(
                taxAccountId!.Value,
                $"Koreksi PPN Keluaran {cnNumber} · Ref {evidenceRef}",
                totalTax,
                0m,
                originalInvoice.CustomerPartnerId,
                null,
                taxCode.TaxCodeId));
        }

        var journalOutcome = await GlPostingKernel.PostJournalAsync(
            db,
            new GlPostJournalCommand(
                cmd.TenantId,
                cmd.LegalEntityId,
                originalInvoice.CurrencyCode,
                cmd.CreditDate,
                originalInvoice.CurrencyCode,
                $"Nota Kredit Pelanggan {cnNumber} atas {originalInvoice.InvoiceNumber} ({originalInvoice.CustomerCodeSnapshot})",
                "BILLING",
                "CUSTOMER_CREDIT_NOTE",
                cnNumber,
                $"billing-cn-post:{cnId:N}",
                journalLines,
                cmd.ActorUserId,
                cmd.NowUtc),
            ct);

        if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
        {
            return BillingOutcome<InvoicePostingResult>.Fail(
                journalOutcome.FailureHttpStatus,
                journalOutcome.ErrorCode ?? "gl_posting_failed",
                "GL credit posting rejected",
                journalOutcome.ErrorDetail ?? "Failed to post compensating credit note journal.");
        }

        var creditNote = CustomerInvoice.CreateDraft(
            cnId,
            cmd.TenantId,
            cmd.LegalEntityId,
            cnNumber,
            InvoiceDocumentTypes.CreditNote,
            originalInvoice.InvoiceId,
            originalInvoice.InvoiceNumber,
            originalInvoice.SalesOrderId,
            originalInvoice.SalesOrderNumberSnapshot,
            originalInvoice.CustomerPartnerId,
            originalInvoice.CustomerCodeSnapshot,
            originalInvoice.CustomerNameSnapshot,
            originalInvoice.CustomerNpwpSnapshot,
            cmd.CreditDate,
            cmd.CreditDate,
            originalInvoice.CurrencyCode,
            totalSub,
            totalTax,
            taxCode.TaxCodeId,
            taxCode.Code,
            taxCode.TreatmentClass,
            originalInvoice.TaxRatePercentSnapshot,
            BillingTaxCalculator.RequiresEvidence(taxCode),
            evidenceRef,
            $"Nota Kredit atas {originalInvoice.InvoiceNumber} · Ref {evidenceRef ?? "-"}",
            idempKey,
            cmd.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        creditNote.MarkPosted(
            journalOutcome.Journal.JournalId,
            journalOutcome.Journal.JournalNumber,
            receivable.ReceivableId,
            evidenceRef,
            cmd.ActorUserId,
            cmd.NowUtc);

        receivable.ApplyCredit(grandCredit, cmd.NowUtc);

        // Update SO & SO line billed counters downward cleanly
        var so = await db.SalesOrders
            .FirstAsync(x => x.TenantId == cmd.TenantId && x.LegalEntityId == cmd.LegalEntityId && x.SalesOrderId == originalInvoice.SalesOrderId, ct);
        var soLineIds = cnLines.Select(l => l.SoLineId).Distinct().ToList();
        var soLines = await db.SalesOrderLines
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && soLineIds.Contains(l.SoLineId))
            .ToDictionaryAsync(l => l.SoLineId, ct);

        decimal totalCreditedQty = 0m;
        foreach (var cnLine in cnLines)
        {
            soLines[cnLine.SoLineId].ApplyFulfillmentDelta(0m, 0m, -cnLine.BilledBaseQuantity);
            totalCreditedQty = decimal.Round(totalCreditedQty + cnLine.BilledBaseQuantity, 6, MidpointRounding.AwayFromZero);
        }

        so.ApplyFulfillmentCounters(
            so.TotalReservedBaseQuantity,
            so.TotalDispatchedBaseQuantity,
            Math.Max(0m, decimal.Round(so.TotalBilledBaseQuantity - totalCreditedQty, 6, MidpointRounding.AwayFromZero)),
            cmd.NowUtc);

        db.CustomerInvoices.Add(creditNote);
        db.CustomerInvoiceLines.AddRange(cnLines);
        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "billing.credit_note.posted",
            creditNote.InvoiceId.ToString("D"),
            JsonSerializer.Serialize(new
            {
                creditNote.InvoiceId,
                creditNote.InvoiceNumber,
                creditNote.OriginalInvoiceNumberSnapshot,
                receivable.ReceivableNumber,
                creditNote.GrandTotalAmount,
                receivable.OpenAmount,
                journalOutcome.Journal.JournalNumber
            }),
            cmd.NowUtc));

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "BILLING_CREDIT_NOTE_POSTED",
            "CustomerInvoice",
            creditNote.InvoiceId.ToString("D"),
            $"Posted credit note {creditNote.InvoiceNumber} against {creditNote.OriginalInvoiceNumberSnapshot}",
            JsonSerializer.Serialize(new
            {
                creditNote.InvoiceNumber,
                creditNote.OriginalInvoiceNumberSnapshot,
                creditNote.GrandTotalAmount,
                receivable.OpenAmount,
                journalOutcome.Journal.JournalNumber
            }),
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return BillingOutcome<InvoicePostingResult>.Created(new InvoicePostingResult(creditNote, cnLines, receivable));
    }

    private static (int Status, string Code, string Title, string Detail)? ValidateOutputTaxCode(TaxCode? taxCode, DateOnly docDate)
    {
        if (taxCode is null || taxCode.Status != MasterStatuses.Active)
        {
            return (404, "billing_tax_code_not_found", "Output tax code not found", "Selected tax code does not exist or is inactive on this legal entity.");
        }

        if (taxCode.Direction != TaxDirections.Output)
        {
            return (409, "billing_tax_direction_invalid", "Invalid tax direction", $"Tax code '{taxCode.Code}' is '{taxCode.Direction}'; customer invoices require an OUTPUT tax code.");
        }

        if (docDate < taxCode.EffectiveFrom || (taxCode.EffectiveTo.HasValue && docDate > taxCode.EffectiveTo.Value))
        {
            return (409, "billing_tax_code_not_effective", "Tax code not effective on invoice date",
                $"Tax code '{taxCode.Code}' is effective from {taxCode.EffectiveFrom:yyyy-MM-dd} to {(taxCode.EffectiveTo.HasValue ? taxCode.EffectiveTo.Value.ToString("yyyy-MM-dd") : "open")}.");
        }

        return null;
    }

    private static async Task<List<SalesShipmentLine>> LockShipmentLinesAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        IReadOnlyList<Guid> orderedShipmentLineIds,
        CancellationToken ct)
    {
        foreach (var id in orderedShipmentLineIds)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext('billing-shpline'), hashtext({id.ToString("D")}))",
                ct);
        }

        return await db.SalesShipmentLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && l.LegalEntityId == legalEntityId
                && orderedShipmentLineIds.Contains(l.ShipmentLineId))
            .OrderBy(l => l.ShipmentLineId)
            .ToListAsync(ct);
    }

    public static async Task<Dictionary<Guid, decimal>> GetActiveBilledQuantitiesByShipmentLineAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        IReadOnlyList<Guid> shipmentLineIds,
        Guid? excludeInvoiceId,
        CancellationToken ct)
    {
        if (shipmentLineIds.Count == 0)
        {
            return [];
        }

        var rows = await (
            from l in db.CustomerInvoiceLines.AsNoTracking()
            join hdr in db.CustomerInvoices.AsNoTracking()
                on new { l.TenantId, l.LegalEntityId, l.InvoiceId } equals new { hdr.TenantId, hdr.LegalEntityId, hdr.InvoiceId }
            where l.TenantId == tenantId
                && l.LegalEntityId == legalEntityId
                && shipmentLineIds.Contains(l.ShipmentLineId)
                && hdr.Status != CustomerInvoiceStatuses.Cancelled
                && (!excludeInvoiceId.HasValue || hdr.InvoiceId != excludeInvoiceId.Value)
            select new { l.ShipmentLineId, hdr.DocumentType, hdr.Status, l.BilledBaseQuantity })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, decimal>();
        foreach (var r in rows)
        {
            var signedQty = r.DocumentType == InvoiceDocumentTypes.CreditNote
                ? (r.Status == CustomerInvoiceStatuses.Posted ? -r.BilledBaseQuantity : 0m)
                : r.BilledBaseQuantity;

            result[r.ShipmentLineId] = decimal.Round(
                result.GetValueOrDefault(r.ShipmentLineId, 0m) + signedQty,
                6,
                MidpointRounding.AwayFromZero);
        }

        return result;
    }
}
