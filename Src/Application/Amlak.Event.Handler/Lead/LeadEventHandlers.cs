using Amlak.Domain.Events.Lead;

namespace Amlak.Event.Handler.Lead;

/// <summary>درخواست تماس جدید — پیامک به مدیران</summary>
public sealed class LeadSubmittedEventHandler(
    IAdminNotifier                     notifier,
    ILogger<LeadSubmittedEventHandler> logger) : IEventHandlerAsync<LeadSubmittedEvent>
{
    public async Task HandleAsync(LeadSubmittedEvent @event, CancellationToken ct)
    {
        logger.LogInformation("درخواست تماس {LeadId} برای آگهی {PropertyId} ثبت شد", @event.LeadId, @event.PropertyId);
        await notifier.NotifyAsync(NotificationTemplates.NewLead(@event.FullName, @event.PhoneNumber, @event.PropertyId), ct);
    }
}
