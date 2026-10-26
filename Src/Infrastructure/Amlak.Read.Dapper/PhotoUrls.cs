using Microsoft.Extensions.Configuration;

namespace Amlak.Read.Dapper;

/// <summary>
/// پیشوند آدرس عمومی عکس‌ها (<c>Storage:PhotoUrlPrefix</c>). در دیتابیس فقط نام فایل ذخیره می‌شود
/// تا جابه‌جایی انبار یا افزودن CDN نیاز به تغییر داده نداشته باشد.
/// </summary>
public sealed class PhotoUrls(IConfiguration configuration)
{
    public string Prefix { get; } = configuration["Storage:PhotoUrlPrefix"] ?? "/api/v1/Photos/";
}
