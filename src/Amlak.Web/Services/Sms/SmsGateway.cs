using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Sms;

public record SmsResult(bool Ok, string? MessageId = null, string? Error = null);

/// <summary>Provider adapter (Kavenegar, SMS.ir, …). Only this interface talks to the provider.</summary>
public interface ISmsSender
{
    Task<SmsResult> SendAsync(string phone, string text, CancellationToken ct = default);
}

/// <summary>Development sender: writes the message to the log instead of sending it.</summary>
public class LogSmsSender(ILogger<LogSmsSender> log) : ISmsSender
{
    public Task<SmsResult> SendAsync(string phone, string text, CancellationToken ct = default)
    {
        log.LogInformation("SMS to {Phone}: {Text}", phone, text);
        return Task.FromResult(new SmsResult(true, "log-" + Guid.NewGuid().ToString("N")[..8]));
    }
}

/// <summary>
/// Every SMS goes through here: numbers are normalized, opted-out numbers are skipped for
/// promotional messages, and each attempt is written to <see cref="SmsLog"/>.
/// </summary>
public class SmsGateway(AppDbContext db, ISmsSender sender, ILogger<SmsGateway> log)
{
    public async Task<SmsResult> SendAsync(string phone, string text, bool promotional, int? campaignId = null,
        CancellationToken ct = default)
    {
        var normalized = PhoneNumber.Normalize(phone);
        if (normalized is null) return new SmsResult(false, Error: "شمارهٔ موبایل نامعتبر است");
        if (promotional && await IsOptedOutAsync(normalized, ct)) return new SmsResult(false, Error: "opted-out");

        if (promotional) text = SmsText.WithOptOut(text);
        SmsResult result;
        try
        {
            result = await sender.SendAsync(normalized, text, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("SMS to {Phone} failed: {Error}", normalized, ex.Message);
            result = new SmsResult(false, Error: "ارتباط با سرویس پیامک برقرار نشد");
        }

        db.SmsLogs.Add(new SmsLog
        {
            Phone = normalized, Text = text, CampaignId = campaignId, Ok = result.Ok,
            Error = result.Error is { Length: > 500 } e ? e[..500] : result.Error, ProviderMessageId = result.MessageId,
        });
        await db.SaveChangesAsync(ct);
        return result;
    }

    public Task<bool> IsOptedOutAsync(string normalizedPhone, CancellationToken ct = default) =>
        db.SmsOptOuts.AnyAsync(x => x.Phone == normalizedPhone, ct);

    /// <summary>Stops all promotional SMS and SMS alerts to a number.</summary>
    public async Task OptOutAsync(string phone, CancellationToken ct = default)
    {
        if (PhoneNumber.Normalize(phone) is not { } normalized) return;
        if (!await IsOptedOutAsync(normalized, ct)) db.SmsOptOuts.Add(new SmsOptOut { Phone = normalized });
        await db.SavedSearches.Where(x => x.Phone == normalized && x.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false), ct);
        await db.SaveChangesAsync(ct);
    }
}
