using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Bot;

public static class BotEndpoints
{
    /// <summary>
    /// <c>POST /bot/telegram/webhook</c> and <c>/bot/bale/webhook</c>. Answers 404 unless a webhook secret is configured,
    /// and 401 unless the request carries it, so only the messenger can feed updates in. Always answers 200 after
    /// that: an error is logged, never retried by the messenger in a loop.
    /// </summary>
    public static void MapBotWebhooks(this IEndpointRouteBuilder app)
    {
        foreach (var messenger in Enum.GetValues<Messenger>())
        {
            app.MapPost(BotClients.WebhookPath(messenger), async (HttpContext http, BotClients clients, BotUpdateHandler handler,
                ILogger<BotUpdateHandler> log, CancellationToken ct) =>
            {
                if (clients.WebhookSecret(messenger) is not { } secret || !clients.Configured(messenger)) return Results.NotFound();
                var given = http.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(secret)))
                    return Results.Unauthorized();
                try
                {
                    using var doc = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: ct);
                    await handler.HandleAsync(messenger, doc.RootElement, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Bot update from {Messenger} failed", messenger);
                }
                return Results.Ok();
            }).DisableAntiforgery().ExcludeFromDescription();
        }
    }
}

/// <summary>
/// Pulls updates with <c>getUpdates</c> for messengers set to <c>{Section}:Mode = Polling</c>: a test bot works on a
/// laptop with no public address. Removes any webhook first (Telegram refuses getUpdates while one is set).
/// </summary>
public sealed class BotPollingService(IServiceScopeFactory scopes, BotClients clients, ILogger<BotPollingService> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stop) =>
        Task.WhenAll(clients.All.Where(clients.Polling).Select(m => PollAsync(m, stop)));

    private async Task PollAsync(Messenger messenger, CancellationToken stop)
    {
        var bot = clients.For(messenger)!;
        await bot.CallAsync("deleteWebhook", stop);
        log.LogInformation("Bot polling started for {Messenger}", messenger);
        long offset = 0;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var r = await bot.CallAsync("getUpdates", stop, ("offset", offset.ToString()), ("timeout", "20"));
                if (!r.Ok || r.Result is not { ValueKind: JsonValueKind.Array } updates)
                {
                    log.LogWarning("getUpdates on {Messenger} failed: {Error}", messenger, r.Error);
                    await Task.Delay(TimeSpan.FromSeconds(5), stop);
                    continue;
                }
                foreach (var u in updates.EnumerateArray())
                {
                    if (u.TryGetProperty("update_id", out var id) && id.TryGetInt64(out var n)) offset = Math.Max(offset, n + 1);
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<BotUpdateHandler>().HandleAsync(messenger, u, stop);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        log.LogError(ex, "Bot update from {Messenger} failed", messenger);
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Bot polling on {Messenger} failed", messenger);
                await Task.Delay(TimeSpan.FromSeconds(5), stop);
            }
        }
    }
}

/// <summary>Sends a staff answer to every bot chat linked to the ticket's phone (outbox handler for TicketAnswered).</summary>
public sealed class BotTicketNotifier(AppDbContext db, BotClients clients, ILogger<BotTicketNotifier> log)
{
    /// <returns>False when linked chats exist but none received the message (the outbox retries).</returns>
    public async Task<bool> TicketAnsweredAsync(int ticketId, int? messageId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        var message = await db.TicketMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (ticket is null || message is null) return true;
        var chats = await db.MessengerAccounts.AsNoTracking().Where(a => a.Phone == ticket.Phone)
            .Select(a => new { a.Messenger, a.ChatId }).ToListAsync(ct);
        chats = chats.Where(c => clients.Configured(c.Messenger)).ToList();
        if (chats.Count == 0) return true;

        var text = $"📩 پاسخ پشتیبانی به تیکت {TicketCodes.Pretty(ticket.Code)}\n«{ticket.Subject}»\n\n{message.Body}";
        var markup = JsonSerializer.Serialize(new { inline_keyboard = new[] { new[] { new { text = "✍️ پاسخ", callback_data = "r:" + ticket.Code } } } });
        var delivered = false;
        foreach (var c in chats)
        {
            var r = await clients.For(c.Messenger, c.ChatId)!.SendMessageAsync(text, markup, ct);
            if (r.Ok) delivered = true;
            else log.LogWarning("Ticket answer to {Messenger} chat failed: {Error}", c.Messenger, r.Error);
        }
        return delivered;
    }
}
