using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Employees;

public record DocumentUpload(
    EmployeeDocumentType DocumentType,
    string Title,
    DateOnly? ExpiryDate,
    string FileName,
    string ContentType,
    long Length,
    Stream Content);

public record DocumentDownload(Stream Content, string ContentType, string FileName);

public sealed class EmployeeDocumentService(
    IApplicationDbContext db,
    IFileStorage storage,
    EmployeeAccessScope scope,
    ICurrentUser currentUser,
    IAuditService audit)
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [[0x25, 0x50, 0x44, 0x46]]),
        [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47]]),
        [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [[0x50, 0x4B, 0x03, 0x04]]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [[0x50, 0x4B, 0x03, 0x04]])
    };

    public static string AcceptAttribute => string.Join(",", Allowed.Keys);

    public async Task<int> UploadAsync(int employeeId, DocumentUpload upload, CancellationToken ct = default)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
            throw new NotFoundException("Employee", employeeId);
        if (upload.Length <= 0)
            throw new DomainException("The file is empty.");
        if (upload.Length > MaxFileBytes)
            throw new DomainException("Files must be 10 MB or smaller.");

        var extension = Path.GetExtension(upload.FileName);
        if (!Allowed.TryGetValue(extension, out var rule))
            throw new DomainException($"File type {extension} is not allowed. Upload PDF, image, Word or Excel files.");

        // Verify the content matches the claimed type (rejects renamed executables etc.).
        await using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(8, buffer.Length)).ToArray();
        if (!rule.Signatures.Any(sig => header.Length >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig)))
            throw new DomainException("The file content does not match its extension.");

        buffer.Position = 0;
        var path = await storage.SaveAsync("employee-documents", buffer, extension, ct);

        var document = new EmployeeDocument
        {
            EmployeeId = employeeId,
            DocumentType = upload.DocumentType,
            Title = string.IsNullOrWhiteSpace(upload.Title) ? Path.GetFileNameWithoutExtension(upload.FileName) : upload.Title.Trim(),
            OriginalFileName = Path.GetFileName(upload.FileName),
            StoragePath = path,
            ContentType = rule.ContentType,
            SizeBytes = buffer.Length,
            ExpiryDate = upload.ExpiryDate
        };
        db.EmployeeDocuments.Add(document);
        audit.Record("Employee.DocumentUploaded", nameof(Employee), employeeId, $"{document.DocumentType}: {document.OriginalFileName}");
        await db.SaveChangesAsync(ct);
        return document.Id;
    }

    public async Task<DocumentDownload> DownloadAsync(int employeeId, int documentId, CancellationToken ct = default)
    {
        await EnsureCanAccessAsync(employeeId, ct);
        var document = await db.EmployeeDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == employeeId, ct)
            ?? throw new NotFoundException("Document", documentId);

        var stream = await storage.OpenReadAsync(document.StoragePath, ct);
        return new DocumentDownload(stream, document.ContentType, document.OriginalFileName);
    }

    public async Task DeleteAsync(int employeeId, int documentId, CancellationToken ct = default)
    {
        var document = await db.EmployeeDocuments.SingleOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == employeeId, ct)
                       ?? throw new NotFoundException("Document", documentId);
        db.EmployeeDocuments.Remove(document); // soft delete; the file is retained for audit
        audit.Record("Employee.DocumentDeleted", nameof(Employee), employeeId, $"{document.DocumentType}: {document.OriginalFileName}");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>HR (employees.manage) or the employee themselves.</summary>
    private async Task EnsureCanAccessAsync(int employeeId, CancellationToken ct)
    {
        if (currentUser.EmployeeId == employeeId || currentUser.HasPermission(Permissions.Employees.Manage))
            return;
        await scope.EnsureCanViewAsync(employeeId, ct);
        throw new ForbiddenAccessException("Only HR or the employee can open employee documents.");
    }
}
