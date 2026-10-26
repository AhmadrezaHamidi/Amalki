using Amlak.Domain.Listing.Exceptions;

namespace Amlak.Domain.Listing;

/// <summary>
/// قاعدهٔ فایل عکس آگهی: فقط JPG/PNG/WebP، حداکثر ۵ مگابایت، و محتوای فایل باید با پسوند بخواند
/// (فایل تغییرنام‌یافته رد می‌شود).
/// </summary>
public static class PhotoFile
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, Func<ReadOnlyMemory<byte>, bool>> Signatures = new()
    {
        [".jpg"]  = IsJpeg,
        [".jpeg"] = IsJpeg,
        [".png"]  = b => b.Length > 8 && b.Span[0] == 0x89 && b.Span[1] == 0x50 && b.Span[2] == 0x4E && b.Span[3] == 0x47,
        [".webp"] = b => b.Length > 12 && b.Span[0] == 'R' && b.Span[1] == 'I' && b.Span[2] == 'F' && b.Span[3] == 'F'
                                       && b.Span[8] == 'W' && b.Span[9] == 'E' && b.Span[10] == 'B' && b.Span[11] == 'P',
    };

    /// <returns>پسوند نرمال‌شده (مثل <c>.jpg</c>)</returns>
    public static string Validate(string fileName, ReadOnlyMemory<byte> content)
    {
        if (content.Length == 0)       throw new InvalidPhotoFileException();
        if (content.Length > MaxBytes) throw new PhotoTooLargeException();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!Signatures.TryGetValue(ext, out var matches) || !matches(content))
            throw new InvalidPhotoFileException();

        return ext == ".jpeg" ? ".jpg" : ext;
    }

    public static string ContentTypeOf(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png"  => "image/png",
        ".webp" => "image/webp",
        _       => "image/jpeg",
    };

    private static bool IsJpeg(ReadOnlyMemory<byte> b) => b.Length > 3 && b.Span[0] == 0xFF && b.Span[1] == 0xD8 && b.Span[2] == 0xFF;
}
