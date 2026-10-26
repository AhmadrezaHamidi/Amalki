using AhmadBase.Doamin;
using AhmadBase.Event.Handler;
using AhmadBase.IOC;
using Autofac;
using Amlak.Persistence.EF;
using Microsoft.Extensions.Logging;

namespace Amlak.Config;

/// <summary>
/// انتشار رویدادهای دامین پس از اتمام موفق هر Command.
///
/// چرا دست‌ساز است: مسیر پیش‌فرض فریم‌ورک فقط داخل <c>BaseDbContext.CommitAsync</c>
/// رویداد منتشر می‌کند (که هیچ‌جا صدا زده نمی‌شود) و برای هر رویداد دقیقاً یک handler
/// resolve می‌کند — رویداد بدون handler استثنا می‌دهد.
///
/// این پیاده‌سازی همه‌ی handler ها را صدا می‌زند، رویداد بدون handler را بی‌صدا رد می‌کند،
/// و خطای یک handler هرگز Command را نمی‌شکند؛ شکست ارسال به تلگرام نباید انتشار آگهی را برگرداند.
/// </summary>
public sealed class DomainEventDispatchBehavior<TRequest, TResponse>(
    AmlakDbContext  db,
    ILifetimeScope scope,
    ILogger<DomainEventDispatchBehavior<TRequest, TResponse>>? logger = null)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next)
    {
        var response = await next();

        foreach (var domainEvent in db.DrainDomainEvents())
            await PublishAsync(domainEvent);

        return response;
    }

    private async Task PublishAsync(IEvent domainEvent)
    {
        var eventType      = domainEvent.GetType();
        var handlerType    = typeof(IEventHandlerAsync<>).MakeGenericType(eventType);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);

        // رویداد بدون handler بی‌صدا رد می‌شود — Autofac برای IEnumerable لیست خالی برمی‌گرداند
        if (scope.ResolveOptional(enumerableType) is not System.Collections.IEnumerable handlers)
            return;

        var handleMethod = handlerType.GetMethod(nameof(IEventHandlerAsync<IEvent>.HandleAsync))!;

        foreach (var handler in handlers)
        {
            try
            {
                await (Task)handleMethod.Invoke(handler, [domainEvent, CancellationToken.None])!;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "پردازش رویداد {EventType} در {Handler} ناموفق بود",
                    eventType.Name, handler?.GetType().Name);
            }
        }
    }
}
