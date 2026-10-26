using Amlak.Domain.Events.Listing;
using Publishing.Application.Commands;

namespace Amlak.Event.Handler.Listing;

/// <summary>انتشار آگهی — ارسال در کانال‌ها</summary>
public sealed class PropertyPublishedEventHandler(ICommandBus bus) : IEventHandlerAsync<PropertyPublishedEvent>
{
    public Task HandleAsync(PropertyPublishedEvent @event, CancellationToken ct)
        => bus.Dispatch<bool>(new SyncPropertyChannelsCommand(@event.PropertyId), ct);
}

/// <summary>تغییر آگهی منتشرشده — ویرایش پست کانال‌ها</summary>
public sealed class PropertyUpdatedEventHandler(ICommandBus bus) : IEventHandlerAsync<PropertyUpdatedEvent>
{
    public Task HandleAsync(PropertyUpdatedEvent @event, CancellationToken ct)
        => bus.Dispatch<bool>(new SyncPropertyChannelsCommand(@event.PropertyId), ct);
}

/// <summary>لغو انتشار — حذف پست کانال‌ها</summary>
public sealed class PropertyUnpublishedEventHandler(ICommandBus bus) : IEventHandlerAsync<PropertyUnpublishedEvent>
{
    public Task HandleAsync(PropertyUnpublishedEvent @event, CancellationToken ct)
        => bus.Dispatch<bool>(new SyncPropertyChannelsCommand(@event.PropertyId), ct);
}

/// <summary>بستن آگهی — حذف پست کانال‌ها</summary>
public sealed class PropertyClosedEventHandler(ICommandBus bus) : IEventHandlerAsync<PropertyClosedEvent>
{
    public Task HandleAsync(PropertyClosedEvent @event, CancellationToken ct)
        => bus.Dispatch<bool>(new SyncPropertyChannelsCommand(@event.PropertyId), ct);
}

/// <summary>حذف آگهی — حذف پست کانال‌ها و فایل عکس‌ها</summary>
public sealed class PropertyDeletedEventHandler(
    ICommandBus                          bus,
    IPhotoStorage                        photoStorage,
    ILogger<PropertyDeletedEventHandler> logger) : IEventHandlerAsync<PropertyDeletedEvent>
{
    public async Task HandleAsync(PropertyDeletedEvent @event, CancellationToken ct)
    {
        await bus.Dispatch<bool>(new RemovePropertyChannelsCommand(@event.PropertyId), ct);

        foreach (var fileName in @event.PhotoFileNames)
            photoStorage.Delete(fileName);

        logger.LogInformation("آگهی {PropertyId} حذف شد و {Count} عکس آن پاک شد", @event.PropertyId, @event.PhotoFileNames.Count);
    }
}
