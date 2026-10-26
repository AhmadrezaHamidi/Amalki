using Amlak.Application.Contract.Services;
using Microsoft.Extensions.Configuration;

namespace Amlak.Persistence.EF.Services;

/// <summary>
/// نگه‌داری عکس‌ها روی دیسک سرور (<c>Storage:PhotoPath</c>، پیش‌فرض <c>storage/photos</c>).
/// نام فایل همیشه GUID تصادفی است؛ نام ورودی کاربر هرگز در مسیر استفاده نمی‌شود.
/// </summary>
public sealed class LocalPhotoStorage(IConfiguration configuration) : IPhotoStorage
{
    private readonly string _root = Path.GetFullPath(configuration["Storage:PhotoPath"] ?? Path.Combine("storage", "photos"));

    public async Task<string> SaveAsync(string extension, ReadOnlyMemory<byte> content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);
        var name = $"{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(_root, name), content.ToArray(), ct);
        return name;
    }

    public void Delete(string fileName)
    {
        if (PathOf(fileName) is { } path) File.Delete(path);
    }

    public string? PathOf(string fileName)
    {
        // فقط نام ساده؛ «../» یا مسیر مطلق پذیرفته نمی‌شود
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)) return null;
        var path = Path.Combine(_root, fileName);
        return File.Exists(path) ? path : null;
    }
}
