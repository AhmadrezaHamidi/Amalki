using AhmadBase.Event.Handler;
using Amlak.Config;
using Amlak.Domain.Events.Listing;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Args;
using Amlak.Domain.Listing.Enums;
using Autofac;

namespace Amlak.Application.Tests.Config;

/// <summary>چسب اصلی بین Command و Event Handler — باید همهٔ handler ها را صدا بزند و خطایشان را ببلعد</summary>
public sealed class DomainEventDispatchBehaviorTests : DbTestBase
{
    private sealed class Throwing : IEventHandlerAsync<PropertyCreatedEvent>
    {
        public Task HandleAsync(PropertyCreatedEvent e, CancellationToken ct) => throw new InvalidOperationException("tg down");
    }

    private sealed class Recording : IEventHandlerAsync<PropertyCreatedEvent>
    {
        public List<long> Seen { get; } = [];
        public Task HandleAsync(PropertyCreatedEvent e, CancellationToken ct) { Seen.Add(e.PropertyId); return Task.CompletedTask; }
    }

    [Fact]
    public async Task All_handlers_run_after_the_command_and_a_failing_one_does_not_break_it()
    {
        using var db = NewDb();
        var recording = new Recording();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new Throwing()).As<IEventHandlerAsync<PropertyCreatedEvent>>();
        builder.RegisterInstance(recording).As<IEventHandlerAsync<PropertyCreatedEvent>>();
        using var scope = builder.Build().BeginLifetimeScope();

        var behavior = new DomainEventDispatchBehavior<object, string>(db, scope);
        var result = await behavior.Handle(new object(), () =>
        {
            db.Properties.Add(Property.Create(new CreatePropertyArg(7, "s", new PropertyDetailsArg(
                "t", "d", DealType.Sale, PropertyKind.Land, 1, null, null, 10, null, null, null, null, "c", "d", null, null, null))));
            return Task.FromResult("done");
        });

        result.Should().Be("done");
        recording.Seen.Should().Equal(7);
        db.DrainDomainEvents().Should().BeEmpty("رویدادها یک‌بار منتشر و پاک می‌شوند");
    }
}
