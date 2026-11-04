using Amlak.Infrastructure.Sms;
using Amlak.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amlak.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// File storage — <c>Storage:Provider</c> "Local" (default, {webRoot}/uploads) or "Minio" (<c>Storage:Minio:*</c>) —
    /// and SMS — <c>Sms:Provider</c> "Log" (default) or "Kavenegar" (<c>Sms:ApiKey</c>, <c>Sms:Template</c>, <c>Sms:Sender</c>).
    /// A provider that is chosen but not configured stops the app at startup instead of failing on the first upload or code.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg, string webRoot)
    {
        // Local disk stays registered after a switch to MinIO: older photos are still read from there.
        services.AddSingleton(new LocalFileStore(webRoot));
        if (string.Equals(cfg["Storage:Provider"], "Minio", StringComparison.OrdinalIgnoreCase))
        {
            var minio = cfg.GetSection("Storage:Minio").Get<MinioOptions>() ?? new();
            if (string.IsNullOrWhiteSpace(minio.Endpoint) || string.IsNullOrWhiteSpace(minio.AccessKey) || string.IsNullOrWhiteSpace(minio.SecretKey))
                throw new InvalidOperationException("Storage:Provider is Minio but Storage:Minio:Endpoint/AccessKey/SecretKey are not set.");
            services.AddSingleton<IFileStore>(new MinioFileStore(minio));
        }
        else
        {
            services.AddSingleton<IFileStore>(sp => sp.GetRequiredService<LocalFileStore>());
        }

        services.AddHttpClient("sms", c => c.Timeout = TimeSpan.FromSeconds(30));
        if (string.Equals(cfg["Sms:Provider"], "Kavenegar", StringComparison.OrdinalIgnoreCase))
        {
            var key = cfg["Sms:ApiKey"];
            var template = cfg["Sms:Template"];
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(template))
                throw new InvalidOperationException("Sms:Provider is Kavenegar but Sms:ApiKey/Sms:Template are not set.");
            services.AddScoped<ISmsSender>(sp => new KavenegarSmsSender(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("sms"), key, template, cfg["Sms:Sender"],
                sp.GetRequiredService<ILogger<KavenegarSmsSender>>()));
        }
        else
        {
            services.AddScoped<ISmsSender, LogSmsSender>();
        }
        return services;
    }
}
