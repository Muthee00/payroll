using Microsoft.Extensions.Options;
using PayMaster.Application.Common.Interfaces;

namespace PayMaster.Infrastructure.Services;

public sealed class FileStorageOptions
{
    public const string Section = "FileStorage";

    /// <summary>Root folder for private files. Must not be inside wwwroot.</summary>
    public string RootPath { get; set; } = "storage";
}

/// <summary>Stores files on local disk under random names; callers keep the original name in the database.</summary>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(string area, Stream content, string extension, CancellationToken ct = default)
    {
        var safeArea = new string(area.Where(char.IsLetterOrDigit).ToArray());
        var safeExtension = new string(extension.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray()).ToLowerInvariant();
        var relative = Path.Combine(safeArea, DateTime.UtcNow.ToString("yyyy-MM"), $"{Guid.NewGuid():N}{safeExtension}");
        var fullPath = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
        return relative.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Resolve(storagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Stored file not found.", storagePath);
        return Task.FromResult<Stream>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Resolve(storagePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    /// <summary>Resolves a storage key and rejects anything escaping the root (path traversal).</summary>
    private string Resolve(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storagePath));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid storage path.");
        return fullPath;
    }
}
