using Amlak.Application.Contract.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ISmsService = AhmadBase.External.Service.ISmsService;

namespace Amlak.Persistence.EF.Services;

/// <summary>پیامک به شماره‌های <c>Platform:AdminPhones</c></summary>
public sealed class SmsAdminNotifier(
    ISmsService               sms,
    IConfiguration            configuration,
    ILogger<SmsAdminNotifier> logger) : IAdminNotifier
{
    public async Task NotifyAsync(string message, CancellationToken ct = default)
    {
        var phones = configuration.GetSection("Platform:AdminPhones").Get<string[]>() ?? [];
        if (phones.Length == 0)
        {
            logger.LogWarning("Platform:AdminPhones خالی است؛ اعلان مدیر ارسال نشد");
            return;
        }

        await sms.SendBulkAsync(phones, message, ct);
    }
}
