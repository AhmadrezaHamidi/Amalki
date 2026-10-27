using System.Text.Json;
using Amlak.Web.Data;
using Amlak.Web.Services.Publishing;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Events;

/// <summary>
/// Handles saved domain events: keeps the Telegram/Bale posts in sync, alerts the admin about leads and
/// tickets and visitor listings, and deletes the files of removed listings. A failure is retried with growing delays.
/// Handlers may run twice (a crash after the call, before the row is marked), so each one is safe to repeat.
/// Assumes one app instance; several would need a row lock (UPDLOCK, READPAST) on the batch query.
/// </summary>
public sealed class OutboxDispatcher(
    AppDbContext db, ChannelPublisher publisher, AdminNotifier notifier, PhotoStorage photos,
    TimeProvider clock, ILogger<OutboxDispatcher> log)
{
    public const int MaxAttempts = 10;

    /// <returns>How many rows were taken (handled or failed).</returns>
    public async Task<int> ProcessBatchAsync(int max = 20, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.Attempts < MaxAttempts && m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.Id).Take(max).ToListAsync(ct);

        var synced = new HashSet<int>(); // several events about one listing in a batch need one sync
        foreach (var m in batch)
        {
            try
            {
                await HandleAsync(m, synced, ct);
                m.ProcessedAtUtc = clock.GetUtcNow().UtcDateTime;
                m.Error = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                m.Attempts++;
                m.Error = ex.Message.Length <= 500 ? ex.Message : ex.Message[..500];
                m.NextAttemptAtUtc = clock.GetUtcNow().UtcDateTime + Backoff(m.Attempts);
                log.LogWarning("Outbox {Id} ({Type}) failed, attempt {Attempts}: {Error}", m.Id, m.Type, m.Attempts, m.Error);
            }
            // One row at a time, so a crash mid-batch does not repeat the rows already done.
            await db.SaveChangesAsync(ct);
        }
        return batch.Count;
    }

    /// <summary>30 s, 1 min, 2 min … capped at 1 hour.</summary>
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Min(3600, 15 * Math.Pow(2, attempts)));

    private async Task HandleAsync(OutboxMessage m, HashSet<int> synced, CancellationToken ct)
    {
        switch (m.Type)
        {
            case nameof(ListingCreated) or nameof(ListingChanged) or nameof(ListingPublished)
                or nameof(ListingUnpublished) or nameof(ListingPhotosChanged):
                if (!synced.Add(m.AggregateId)) return;
                try
                {
                    await publisher.SyncAsync(m.AggregateId, ct);
                    // The publisher records a failed call on the post instead of throwing; turn it into a retry.
                    var errors = await db.ChannelPosts.AsNoTracking()
                        .Where(c => c.PropertyId == m.AggregateId && c.Error != null).Select(c => c.Error).ToListAsync(ct);
                    if (errors.Count > 0) throw new InvalidOperationException("ارسال به کانال ناموفق: " + string.Join(" | ", errors));
                }
                catch
                {
                    synced.Remove(m.AggregateId);
                    throw;
                }
                return;

            case nameof(ListingDeleted):
                var gone = JsonSerializer.Deserialize<OutboxMessage.DeletedListing>(m.Payload ?? "{}");
                foreach (var post in gone?.Posts ?? []) await publisher.DeleteMessagesAsync(post.Channel, post.MessageIds, ct);
                foreach (var file in gone?.Photos ?? [])
                {
                    var photo = PropertyPhoto.Stored(file.Url);
                    photo.Variants = file.Variants;
                    await photos.DeleteAsync(photo, ct);
                }
                return;

            case nameof(ListingSubmitted):
                var submitted = await db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == m.AggregateId, ct);
                if (submitted is not null && !await notifier.NotifyListingAsync(submitted, ct))
                    throw new InvalidOperationException("اعلان آگهی تازه به هیچ پیام‌رسانی نرسید");
                return;

            case nameof(LeadSubmitted):
                var lead = await db.Leads.AsNoTracking().Include(l => l.Property).FirstOrDefaultAsync(l => l.Id == m.AggregateId, ct);
                if (lead is not null && !await notifier.NotifyAsync(lead, lead.Property, ct))
                    throw new InvalidOperationException("اعلان درخواست تماس به هیچ پیام‌رسانی نرسید");
                return;

            case nameof(TicketOpened) or nameof(TicketMessageReceived):
                var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == m.AggregateId, ct);
                var message = await db.TicketMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.RefId, ct);
                if (ticket is not null && message is not null
                    && !await notifier.NotifyTicketAsync(ticket, message.Body, isNew: m.Type == nameof(TicketOpened), ct))
                    throw new InvalidOperationException("اعلان تیکت به هیچ پیام‌رسانی نرسید");
                return;

            default:
                return; // no handler (e.g. TicketAnswered): kept as a record of what happened
        }
    }
}

/// <summary>Runs the dispatcher every couple of seconds, and straight away while there is a backlog.</summary>
public sealed class OutboxProcessor(IServiceScopeFactory scopes, ILogger<OutboxProcessor> log) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var taken = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                taken = await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().ProcessBatchAsync(BatchSize, stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // e.g. the database is down: try again on the next tick.
                log.LogError(ex, "Outbox batch failed");
            }
            if (taken < BatchSize)
                try { await Task.Delay(Idle, stop); } catch (OperationCanceledException) { return; }
        }
    }
}
