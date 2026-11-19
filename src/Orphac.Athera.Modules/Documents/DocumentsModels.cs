using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Documents;

public static class DocumentCategories
{
    public const string TaxEvidence = "TAX_EVIDENCE";
    public const string DeliveryProof = "DELIVERY_PROOF";
    public const string SupplierInvoice = "SUPPLIER_INVOICE";
    public const string BankAdvice = "BANK_ADVICE";
    public const string Contract = "CONTRACT";
    public const string General = "GENERAL";

    public static readonly IReadOnlyList<string> All =
        [TaxEvidence, DeliveryProof, SupplierInvoice, BankAdvice, Contract, General];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? General : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported document category '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class DocumentSecurity
{
    public const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "text/csv",
        "text/plain"
    };

    public static (bool IsValid, string? ErrorCode, string? Message) ValidateFilePayload(
        string fileName,
        string contentType,
        byte[] contentBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Trim().Length > 180)
        {
            return (false, "invalid_file_name", "File name is required and must be at most 180 characters.");
        }

        if (contentBytes is null || contentBytes.Length == 0)
        {
            return (false, "empty_file_content", "Uploaded file cannot be empty.");
        }

        if (contentBytes.Length > MaxFileSizeBytes)
        {
            return (false, "file_too_large", $"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var normalizedMime = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedContentTypes.Contains(normalizedMime))
        {
            return (false, "unsupported_media_type", $"Content type '{contentType}' is not allowed. Allowed: PDF, PNG, JPEG, CSV, TXT.");
        }

        // Magic-byte & unsafe payload inspection (reject Windows PE/MZ, Linux ELF, Mach-O, shell scripts, HTML/SVG scripts)
        if (contentBytes.Length >= 2 && contentBytes[0] == 0x4D && contentBytes[1] == 0x5A)
        {
            return (false, "unsafe_file_content", "Executable binary payload (MZ) is blocked.");
        }

        if (contentBytes.Length >= 4 && contentBytes[0] == 0x7F && contentBytes[1] == 0x45 && contentBytes[2] == 0x4C && contentBytes[3] == 0x46)
        {
            return (false, "unsafe_file_content", "Executable binary payload (ELF) is blocked.");
        }

        if (contentBytes.Length >= 2 && contentBytes[0] == 0x23 && contentBytes[1] == 0x21)
        {
            return (false, "unsafe_file_content", "Executable script payload (#!) is blocked.");
        }

        if (normalizedMime is "text/plain" or "text/csv")
        {
            var head = Encoding.UTF8.GetString(contentBytes, 0, Math.Min(contentBytes.Length, 512));
            if (head.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
                head.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "unsafe_file_content", "Active HTML/script content is blocked.");
            }
        }

        return (true, null, null);
    }

    public static string ComputeSha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string CreateDownloadToken(
        byte[] signingKey,
        Guid tenantId,
        Guid legalEntityId,
        Guid documentId,
        Guid actorUserId,
        DateTimeOffset expiresAtUtc)
    {
        var expUnix = expiresAtUtc.ToUnixTimeSeconds();
        var payload = $"{tenantId:N}:{legalEntityId:N}:{documentId:N}:{actorUserId:N}:{expUnix}";
        var sig = ComputeHmacHex(signingKey, payload);
        return $"{expUnix}.{sig}";
    }

    public static bool VerifyDownloadToken(
        byte[] signingKey,
        string? token,
        Guid tenantId,
        Guid legalEntityId,
        Guid documentId,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.', 2);
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expUnix))
        {
            return false;
        }

        if (expUnix < nowUtc.ToUnixTimeSeconds())
        {
            return false;
        }

        var payload = $"{tenantId:N}:{legalEntityId:N}:{documentId:N}:{actorUserId:N}:{expUnix}";
        var expected = ComputeHmacHex(signingKey, payload);
        var actualBytes = Encoding.ASCII.GetBytes(parts[1]);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static string ComputeHmacHex(byte[] key, string payload)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a deterministic, valid single-page PDF 1.4 document with zero external binary dependencies.
    /// </summary>
    public static byte[] BuildSimplePdf(string title, IReadOnlyList<string> lines)
    {
        static string EscapePdfText(string input)
        {
            var sb = new StringBuilder(input.Length + 8);
            foreach (var ch in input)
            {
                if (ch is '\\' or '(' or ')')
                {
                    sb.Append('\\').Append(ch);
                }
                else if (ch >= 32 && ch <= 126)
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }

        var contentSb = new StringBuilder();
        contentSb.AppendLine("BT");
        contentSb.AppendLine("/F1 13 Tf");
        contentSb.AppendLine("48 760 Td");
        contentSb.AppendLine($"({EscapePdfText(title)}) Tj");
        contentSb.AppendLine("/F1 9 Tf");
        contentSb.AppendLine("0 -20 Td");
        foreach (var line in lines.Take(48))
        {
            var clipped = line.Length > 105 ? line[..102] + "..." : line;
            contentSb.AppendLine($"({EscapePdfText(clipped)}) Tj");
            contentSb.AppendLine("0 -14 Td");
        }
        contentSb.AppendLine("ET");

        var streamBytes = Encoding.ASCII.GetBytes(contentSb.ToString());
        using var ms = new MemoryStream();
        var offsets = new List<long>();

        void WriteAscii(string text)
        {
            var b = Encoding.ASCII.GetBytes(text);
            ms.Write(b, 0, b.Length);
        }

        WriteAscii("%PDF-1.4\n");

        // 1: Catalog
        offsets.Add(ms.Position);
        WriteAscii("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 2: Pages
        offsets.Add(ms.Position);
        WriteAscii("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // 3: Page
        offsets.Add(ms.Position);
        WriteAscii("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        // 4: Font
        offsets.Add(ms.Position);
        WriteAscii("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        // 5: Content stream
        offsets.Add(ms.Position);
        WriteAscii($"5 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        ms.Write(streamBytes, 0, streamBytes.Length);
        WriteAscii("\nendstream\nendobj\n");

        var xrefPos = ms.Position;
        WriteAscii($"xref\n0 {offsets.Count + 1}\n");
        WriteAscii("0000000000 65535 f \n");
        foreach (var off in offsets)
        {
            WriteAscii(string.Create(CultureInfo.InvariantCulture, $"{off:D10} 00000 n \n"));
        }

        WriteAscii($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
        return ms.ToArray();
    }
}

public sealed class DocumentSequence
{
    public Guid SequenceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public int LastNumber { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private DocumentSequence() { }

    public static DocumentSequence Create(Guid tenantId, Guid legalEntityId, int fiscalYear, DateTimeOffset nowUtc) =>
        new()
        {
            SequenceId = Guid.NewGuid(),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            FiscalYear = fiscalYear,
            LastNumber = 0,
            UpdatedAtUtc = nowUtc
        };

    public string NextNumber(DateTimeOffset nowUtc)
    {
        LastNumber++;
        UpdatedAtUtc = nowUtc;
        return string.Create(CultureInfo.InvariantCulture, $"DOC-{FiscalYear}-{LastNumber:D5}");
    }
}

public sealed class BusinessDocument
{
    public Guid DocumentId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ByteSize { get; private set; }
    public string Sha256Hash { get; private set; } = string.Empty;
    public string Category { get; private set; } = DocumentCategories.General;
    public string ScanStatus { get; private set; } = "CLEAN";
    public byte[] ContentBytes { get; private set; } = [];
    public string? Notes { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private BusinessDocument() { }

    public static BusinessDocument Create(
        Guid documentId,
        Guid tenantId,
        Guid legalEntityId,
        string documentNumber,
        string fileName,
        string contentType,
        string category,
        byte[] contentBytes,
        string? notes,
        Guid uploadedByUserId,
        DateTimeOffset nowUtc)
    {
        var (ok, _, msg) = DocumentSecurity.ValidateFilePayload(fileName, contentType, contentBytes);
        if (!ok)
        {
            throw new InvalidOperationException(msg ?? "Invalid document payload.");
        }

        return new BusinessDocument
        {
            DocumentId = GuardValidation.RequireNonEmptyGuid(documentId, nameof(documentId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            DocumentNumber = GuardValidation.RequireCode(documentNumber, nameof(documentNumber), 40),
            FileName = Path.GetFileName(fileName.Trim()),
            ContentType = contentType.Trim().ToLowerInvariant(),
            ByteSize = contentBytes.LongLength,
            Sha256Hash = DocumentSecurity.ComputeSha256Hex(contentBytes),
            Category = DocumentCategories.Normalize(category),
            ScanStatus = "CLEAN",
            ContentBytes = contentBytes,
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            UploadedByUserId = GuardValidation.RequireNonEmptyGuid(uploadedByUserId, nameof(uploadedByUserId)),
            CreatedAtUtc = nowUtc
        };
    }
}

public sealed class DocumentLink
{
    public Guid LinkId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string SourceModule { get; private set; } = string.Empty;
    public string SourceDocumentType { get; private set; } = string.Empty;
    public string SourceDocumentId { get; private set; } = string.Empty;
    public string SourceDocumentNumber { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private DocumentLink() { }

    public static DocumentLink Create(
        Guid linkId,
        Guid tenantId,
        Guid legalEntityId,
        Guid documentId,
        string sourceModule,
        string sourceDocumentType,
        string sourceDocumentId,
        string sourceDocumentNumber,
        Guid createdByUserId,
        DateTimeOffset nowUtc) =>
        new()
        {
            LinkId = GuardValidation.RequireNonEmptyGuid(linkId, nameof(linkId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            DocumentId = GuardValidation.RequireNonEmptyGuid(documentId, nameof(documentId)),
            SourceModule = GuardValidation.RequireCode(sourceModule.Trim().ToUpperInvariant(), nameof(sourceModule), 40),
            SourceDocumentType = GuardValidation.RequireCode(sourceDocumentType.Trim().ToUpperInvariant(), nameof(sourceDocumentType), 60),
            SourceDocumentId = GuardValidation.RequireName(sourceDocumentId, nameof(sourceDocumentId), 80),
            SourceDocumentNumber = GuardValidation.RequireName(sourceDocumentNumber, nameof(sourceDocumentNumber), 80),
            CreatedByUserId = GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = nowUtc
        };
}
