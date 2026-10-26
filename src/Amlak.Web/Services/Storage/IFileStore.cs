namespace Amlak.Web.Services.Storage;

/// <summary>Where uploaded files live. Files are addressed by the public URL the store returned for them.</summary>
public interface IFileStore
{
    /// <summary>True when <paramref name="url"/> points into this store.</summary>
    bool Owns(string url);

    /// <returns>Public URL of the stored file.</returns>
    Task<string> PutAsync(string name, Stream content, long length, string contentType, CancellationToken ct = default);

    /// <returns>The file's bytes, or null when it does not exist.</returns>
    Task<Stream?> OpenAsync(string url, CancellationToken ct = default);

    Task DeleteAsync(string url, CancellationToken ct = default);
}
