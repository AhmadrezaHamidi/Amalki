using Amlak.Web.Services.Storage;

namespace Amlak.Web.Services;

/// <summary>
/// Validates uploaded listing photos and keeps them in the configured store (local disk or MinIO).
/// Photos uploaded before a switch to MinIO stay readable from local disk.
/// </summary>
public class PhotoStorage(IFileStore primary, LocalFileStore local)
{
    public const long MaxBytes = 5 * 1024 * 1024;

    // Extension -> magic-number check, so a renamed file is rejected.
    private static readonly Dictionary<string, Func<byte[], bool>> Signatures = new()
    {
        [".jpg"] = b => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        [".jpeg"] = b => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        [".png"] = b => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        [".webp"] = b => b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P',
    };

    /// <returns>Public URL of the stored file, or null when the file is not an accepted image.</returns>
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

        return await primary.PutAsync($"{Guid.NewGuid():N}{ext}", input, file.Length, MimeFor(ext), ct);
    }

    /// <returns>The photo's bytes, or null when it is gone from its store.</returns>
    public Task<Stream?> OpenAsync(string url, CancellationToken ct = default) =>
        StoreOf(url)?.OpenAsync(url, ct) ?? Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string url, CancellationToken ct = default) =>
        StoreOf(url)?.DeleteAsync(url, ct) ?? Task.CompletedTask;

    private IFileStore? StoreOf(string url) => primary.Owns(url) ? primary : local.Owns(url) ? local : null;

    private static string MimeFor(string ext) => ext switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
