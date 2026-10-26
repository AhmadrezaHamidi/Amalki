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

    /// <returns>The stored photo (URL, size and WebP copies), or null when the file is not an accepted image.</returns>
    public async Task<PropertyPhoto?> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length is 0 or > MaxBytes) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Signatures.TryGetValue(ext, out var isValid)) return null;

        await using var input = file.OpenReadStream();
        var head = new byte[16];
        var read = await input.ReadAsync(head, ct);
        if (!isValid(head.AsSpan(0, read).ToArray())) return null;
        input.Position = 0;

        var url = await primary.PutAsync($"{Guid.NewGuid():N}{ext}", input, file.Length, MimeFor(ext), ct);
        var photo = new PropertyPhoto { Url = url };

        input.Position = 0;
        await AddVariantsAsync(photo, input, primary, ct);
        return photo;
    }

    /// <summary>
    /// Makes the WebP copies of a photo uploaded before copies existed (Variants is null) and records them.
    /// Afterwards Variants is never null, so a photo the decoder cannot read is not retried.
    /// </summary>
    /// <returns>False when the original is gone from its store.</returns>
    public async Task<bool> BackfillAsync(PropertyPhoto photo, CancellationToken ct = default)
    {
        if (StoreOf(photo.Url) is not { } store) return false;
        await using var original = await store.OpenAsync(photo.Url, ct);
        if (original is null) return false;
        await AddVariantsAsync(photo, original, store, ct);
        return true;
    }

    // WebP copies for srcset, stored next to the original. A photo the decoder cannot read is kept without copies.
    private static async Task AddVariantsAsync(PropertyPhoto photo, Stream original, IFileStore store, CancellationToken ct)
    {
        var made = PhotoVariants.Make(original);
        photo.Variants = "";
        if (made is null) return;
        photo.Width = made.Width;
        photo.Height = made.Height;
        var stem = Path.GetFileNameWithoutExtension(photo.Url);
        foreach (var v in made.Variants)
        {
            using var webp = new MemoryStream(v.Webp);
            await store.PutAsync($"{stem}-{v.Width}.webp", webp, v.Webp.Length, "image/webp", ct);
        }
        photo.Variants = string.Join(",", made.Variants.Select(v => v.Width));
    }

    /// <returns>The photo's bytes, or null when it is gone from its store.</returns>
    public Task<Stream?> OpenAsync(string url, CancellationToken ct = default) =>
        StoreOf(url)?.OpenAsync(url, ct) ?? Task.FromResult<Stream?>(null);

    /// <summary>Deletes the photo and its WebP copies.</summary>
    public async Task DeleteAsync(PropertyPhoto photo, CancellationToken ct = default)
    {
        if (StoreOf(photo.Url) is not { } store) return;
        await store.DeleteAsync(photo.Url, ct);
        foreach (var w in PhotoVariants.ParseWidths(photo.Variants))
            await store.DeleteAsync(PhotoVariants.UrlFor(photo.Url, w), ct);
    }

    private IFileStore? StoreOf(string url) => primary.Owns(url) ? primary : local.Owns(url) ? local : null;

    private static string MimeFor(string ext) => ext switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
