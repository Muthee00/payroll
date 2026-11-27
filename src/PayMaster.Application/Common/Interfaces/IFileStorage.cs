namespace PayMaster.Application.Common.Interfaces;

/// <summary>Private file store (outside the web root). Paths returned are opaque storage keys.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(string area, Stream content, string extension, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
