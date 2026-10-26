namespace Amlak.Web.Services.Storage;

/// <summary>Files under wwwroot/uploads, served by the app itself at /uploads/...</summary>
public class LocalFileStore(IWebHostEnvironment env) : IFileStore
{
    private const string Folder = "uploads";

    public bool Owns(string url) => url.StartsWith($"/{Folder}/", StringComparison.Ordinal);

    public async Task<string> PutAsync(string name, Stream content, long length, string contentType, CancellationToken ct = default)
    {
        var dir = Path.Combine(env.WebRootPath, Folder);
        Directory.CreateDirectory(dir);
        await using (var output = File.Create(Path.Combine(dir, name)))
            await content.CopyToAsync(output, ct);
        return $"/{Folder}/{name}";
    }

    public Task<Stream?> OpenAsync(string url, CancellationToken ct = default)
    {
        var path = PathOf(url);
        return Task.FromResult<Stream?>(path is not null && File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (PathOf(url) is { } path && File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    // GetFileName drops any directory part, so a crafted URL cannot reach outside the uploads folder.
    private string? PathOf(string url) => Owns(url) ? Path.Combine(env.WebRootPath, Folder, Path.GetFileName(url)) : null;
}
