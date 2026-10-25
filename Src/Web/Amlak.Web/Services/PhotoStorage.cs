namespace Amlak.Web.Services;

/// <summary>Stores uploaded listing photos under wwwroot/uploads after validating type and size.</summary>
public class PhotoStorage(IWebHostEnvironment env)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private const string Folder = "uploads";

    // Extension -> magic-number check, so a renamed file is rejected.
    private static readonly Dictionary<string, Func<byte[], bool>> Signatures = new()
    {
        [".jpg"] = b => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        [".jpeg"] = b => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        [".png"] = b => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        [".webp"] = b => b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P',
    };

    /// <returns>Public URL of the stored file, or null with <paramref name="error"/> set.</returns>
    public async Task<string?> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length is 0 or > MaxBytes) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Signatures.TryGetValue(ext, out var isValid)) return null;

        await using var input = file.OpenReadStream();
        var head = new byte[16];
        var read = await input.ReadAsync(head, ct);
        if (!isValid(head.AsSpan(0, read).ToArray())) return null;
        input.Position = 0;

        var dir = Path.Combine(env.WebRootPath, Folder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var output = File.Create(Path.Combine(dir, name)))
            await input.CopyToAsync(output, ct);
        return $"/{Folder}/{name}";
    }

    public void Delete(string url)
    {
        if (!url.StartsWith($"/{Folder}/", StringComparison.Ordinal)) return;
        var path = Path.Combine(env.WebRootPath, Folder, Path.GetFileName(url));
        if (File.Exists(path)) File.Delete(path);
    }
}
