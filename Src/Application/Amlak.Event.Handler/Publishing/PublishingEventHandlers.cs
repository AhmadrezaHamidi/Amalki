using Amlak.Domain.Events.Publishing;

namespace Amlak.Event.Handler.Publishing;

/// <summary>شکست ارسال به کانال — فقط لاگ؛ مدیر خطا را کنار پست می‌بیند و دوباره همگام می‌کند</summary>
public sealed class ChannelSyncFailedEventHandler(ILogger<ChannelSyncFailedEventHandler> logger)
    : IEventHandlerAsync<ChannelSyncFailedEvent>
{
    public Task HandleAsync(ChannelSyncFailedEvent @event, CancellationToken ct)
    {
        logger.LogWarning("همگام‌سازی آگهی {PropertyId} با {Channel} ناموفق بود: {Error}",
            @event.PropertyId, @event.Channel, @event.Error);
        return Task.CompletedTask;
    }
}
