using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Sms;

/// <summary>Resolves a campaign's recipients and sends it; <see cref="CampaignWorker"/> runs scheduled ones.</summary>
public class BulkSmsService(AppDbContext db, SmsGateway sms, IConfiguration cfg, TimeProvider clock)
{
    private readonly string _baseUrl = (cfg["Site:BaseUrl"] ?? "http://localhost").TrimEnd('/');

    /// <summary>Distinct, valid, not-opted-out numbers, with the subscriber's name when known.</summary>
    public async Task<List<(string Phone, string? Name)>> RecipientsAsync(SmsCampaign c, CancellationToken ct = default)
    {
        List<(string Phone, string? Name)> list;
        if (c.Audience == CampaignAudience.Uploaded)
        {
            list = (c.UploadedPhones ?? "").Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(PhoneNumber.Normalize).OfType<string>().Select(p => (p, (string?)null)).ToList();
        }
        else
        {
            var subs = await db.SavedSearches.AsNoTracking().Where(s => s.Active)
                .Where(s => c.Deal == null || s.Deal == c.Deal)
                .Where(s => c.City == null || s.City == c.City)
                .ToListAsync(ct);
            if (!string.IsNullOrWhiteSpace(c.District))
                subs = subs.Where(s => AlertMatcher.SplitDistricts(s.Districts) is var d && (d.Count == 0 || d.Contains(c.District.Trim()))).ToList();
            list = subs.Select(s => (s.Phone, s.Name)).ToList();
        }

        var optedOut = (await db.SmsOptOuts.Select(x => x.Phone).ToListAsync(ct)).ToHashSet();
        return list.Where(r => !optedOut.Contains(r.Phone)).DistinctBy(r => r.Phone).ToList();
    }

    public async Task<string> RenderAsync(SmsCampaign c, string? name, CancellationToken ct = default)
    {
        var p = c.PropertyId is { } id ? await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        var text = SmsText.Render(c.Template, new Dictionary<string, string?>
        {
            ["نام"] = string.IsNullOrWhiteSpace(name) ? "کاربر" : name,
            ["عنوان"] = p?.Title,
            ["محله"] = p?.District,
            ["قیمت"] = p is null ? null : Pricing.Label(p),
            ["لینک"] = p is null ? _baseUrl : $"{_baseUrl}/ملک/{Uri.EscapeDataString(p.Slug)}",
        });
        return SmsText.WithOptOut(text);
    }

    /// <summary>Sends every scheduled campaign that is due. Safe to call repeatedly.</summary>
    public async Task<int> ProcessDueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await db.SmsCampaigns
            .Where(c => c.Status == CampaignStatus.Scheduled && c.ScheduledAtUtc <= now)
            .OrderBy(c => c.ScheduledAtUtc).ToListAsync(ct);
        foreach (var c in due) await SendAsync(c, ct);
        return due.Count;
    }

    private async Task SendAsync(SmsCampaign c, CancellationToken ct)
    {
        c.Status = CampaignStatus.Sending;
        await db.SaveChangesAsync(ct);

        var recipients = await RecipientsAsync(c, ct);
        c.Recipients = recipients.Count;
        foreach (var (phone, name) in recipients)
        {
            ct.ThrowIfCancellationRequested();
            var result = await sms.SendAsync(phone, await RenderAsync(c, name, ct), promotional: true, campaignId: c.Id, ct: ct);
            if (result.Ok) c.Sent++; else c.Failed++;
        }
        c.Status = CampaignStatus.Done;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Checks once a minute for scheduled campaigns that are due.</summary>
public class CampaignWorker(IServiceScopeFactory scopes, ILogger<CampaignWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BulkSmsService>().ProcessDueAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "SMS campaign run failed");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
