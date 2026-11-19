using System.Text;
using Orphac.Athera.Modules.Documents;
using Xunit;

namespace Orphac.Athera.UnitTests;

public sealed class S17DocumentsUnitTests
{
    [Fact]
    public void ValidateFilePayload_BlocksUnsafeBinariesAndOversizedPayloads()
    {
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF");
        var (okPdf, _, _) = DocumentSecurity.ValidateFilePayload("faktur-001.pdf", "application/pdf", pdfBytes);
        Assert.True(okPdf);

        // Windows PE/MZ executable disguised as PDF
        var mzBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };
        var (okMz, mzCode, _) = DocumentSecurity.ValidateFilePayload("fake.pdf", "application/pdf", mzBytes);
        Assert.False(okMz);
        Assert.Equal("unsafe_file_content", mzCode);

        // Script disguised as plain text
        var htmlScript = Encoding.UTF8.GetBytes("<script>alert(1)</script>");
        var (okScript, scriptCode, _) = DocumentSecurity.ValidateFilePayload("note.txt", "text/plain", htmlScript);
        Assert.False(okScript);
        Assert.Equal("unsafe_file_content", scriptCode);

        // Disallowed MIME type
        var (okExe, exeCode, _) = DocumentSecurity.ValidateFilePayload("run.exe", "application/x-msdownload", [1, 2, 3]);
        Assert.False(okExe);
        Assert.Equal("unsupported_media_type", exeCode);

        // Oversized (> 5 MB)
        var huge = new byte[DocumentSecurity.MaxFileSizeBytes + 1];
        var (okHuge, hugeCode, _) = DocumentSecurity.ValidateFilePayload("huge.pdf", "application/pdf", huge);
        Assert.False(okHuge);
        Assert.Equal("file_too_large", hugeCode);
    }

    [Fact]
    public void SignedDownloadToken_EnforcesTenantEntityDocumentActorAndExpiry()
    {
        var key = Encoding.UTF8.GetBytes("test-signing-key-32-bytes-minimum-2026");
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

        var token = DocumentSecurity.CreateDownloadToken(key, tenantId, entityId, docId, userId, now.AddMinutes(5));

        Assert.True(DocumentSecurity.VerifyDownloadToken(key, token, tenantId, entityId, docId, userId, now.AddMinutes(1)));
        Assert.False(DocumentSecurity.VerifyDownloadToken(key, token, tenantId, entityId, docId, userId, now.AddMinutes(6))); // expired
        Assert.False(DocumentSecurity.VerifyDownloadToken(key, token, Guid.NewGuid(), entityId, docId, userId, now)); // wrong tenant
        Assert.False(DocumentSecurity.VerifyDownloadToken(key, token, tenantId, Guid.NewGuid(), docId, userId, now)); // wrong entity
        Assert.False(DocumentSecurity.VerifyDownloadToken(key, token, tenantId, entityId, Guid.NewGuid(), userId, now)); // wrong doc
        Assert.False(DocumentSecurity.VerifyDownloadToken(key, token, tenantId, entityId, docId, Guid.NewGuid(), now)); // wrong actor
        Assert.False(DocumentSecurity.VerifyDownloadToken(key, token + "a", tenantId, entityId, docId, userId, now)); // tampered
    }

    [Fact]
    public void BuildSimplePdf_ProducesValidPdf14StructureWithContent()
    {
        var pdf = DocumentSecurity.BuildSimplePdf("Orphac Athera Evidence Pack", ["DOC-2026-00001 | faktur.pdf", "SHA-256: abcdef123456"]);
        var text = Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("Orphac Athera Evidence Pack", text, StringComparison.Ordinal);
        Assert.Contains("DOC-2026-00001 | faktur.pdf", text, StringComparison.Ordinal);
        Assert.Contains("%%EOF", text, StringComparison.Ordinal);
    }
}
