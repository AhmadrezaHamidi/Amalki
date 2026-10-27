using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Sms;

/// <summary>Resolves a campaign's recipients and sends it; <see cref="CampaignWorker"/> runs the scheduled ones.</summary>
public class BulkSmsService(AppDbContext db, SmsGateway sms, IConfiguration cfg, TimeProvider clock)
{
    private readonly string _baseUrl = (cfg["Site:BaseUrl"] ?? "http://localhost").TrimEnd('/');

    /// <summary>Distinct, valid, not-opted-out numbers, with the subscriber's name when known.</summary>
    public async Task<List<(string Phone, string? Name)>> RecipientsAsync(SmsCampaign c, CancellationToken ct = default)
    {
        List<(string Phone, string? Name)> list;
        if (c.Audience == CampaignAudience.Uploaded)
        {
            list = (c.UploadedPhones ?? "").Split(['\n', '\r', ',', ';', '،'], StringSplitOptions.RemoveEmptyEntries)
                .Select(Fa.Mobile).OfType<string>().Select(p => (p, (string?)null)).ToList();
        }
        else
        {
            var subs = await db.SavedSearches.AsNoTracking().Where(s => s.Active)
                .Where(s => c.Deal == null || s.Deal == c.Deal)
                .Where(s => c.City == null || s.City == c.City)
                .ToListAsync(ct);
            if (!string.IsNullOrWhiteSpace(c.District))
            {
                var district = PersianText.Normalize(c.District);
                subs = subs.Where(s => AlertMatcher.Districts(s.Districts) is var d && (d.Count == 0 || d.Contains(district))).ToList();
            }
            list = subs.Select(s => (s.Phone, s.Name)).ToList();
        }

        var optedOut = (await db.SmsOptOuts.Select(x => x.Phone).ToListAsync(ct)).ToHashSet();
        return list.Where(r => !optedOut.Contains(r.Phone)).DistinctBy(r => r.Phone).ToList();
    }

    public async Task<string> RenderAsync(SmsCampaign c, string? name, CancellationToken ct = default)
    {
        var p = c.PropertyId is { } id ? await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        return SmsText.WithOptOut(SmsText.Render(c.Template, new Dictionary<string, string?>
        {
            ["نام"] = string.IsNullOrWhiteSpace(name) ? "کاربر" : name,
            ["عنوان"] = p?.Title,
            ["محله"] = p?.District,
            ["قیمت"] = p is null ? null : PriceText.Label(p),
            ["لینک"] = p is null ? _baseUrl : AlertService.ShortLink(_baseUrl, p.Id),
        }));
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
        // Marked first, so a crash mid-send leaves it "Sending" instead of texting everyone again.
        c.Status = CampaignStatus.Sending;
        await db.SaveChangesAsync(ct);

        var recipients = await RecipientsAsync(c, ct);
        c.Recipients = recipients.Count;
        foreach (var (phone, name) in recipients)
        {
            ct.ThrowIfCancellationRequested();
            if (await sms.SendAsync(phone, await RenderAsync(c, name, ct), c.Id, ct)) c.Sent++; else c.Failed++;
        }
        c.Status = CampaignStatus.Done;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Checks once a minute for scheduled campaigns that are due.</summary>
public class CampaignWorker(IServiceScopeFactory scopes, ILogger<CampaignWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<BulkSmsService>().ProcessDueAsync(stop);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "SMS campaign run failed");
                }
            } while (await timer.WaitForNextTickAsync(stop));
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }
}
