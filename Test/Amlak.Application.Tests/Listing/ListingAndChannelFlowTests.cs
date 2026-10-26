using AhmadBase.Application;
using Amlak.Application.Listing;
using Amlak.Application.Publishing;
using Amlak.Domain.Events.Listing;
using Amlak.Domain.Listing.Enums;
using Amlak.Domain.Listing.Exceptions;
using Amlak.Domain.Publishing.Enums;
using Amlak.Event.Handler.Listing;
using Amlak.Persistence.EF.Repositories;
using Amlak.Persistence.EF.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amlak.Application.Tests.Listing;

/// <summary>
/// جریان کامل آگهی روی SQLite: ساخت، عکس، انتشار → رویداد → پست کانال، ویرایش، لغو، حذف.
/// رویدادها همان‌طور که DomainEventDispatchBehavior می‌کند، پس از هر Command به Event Handler های واقعی می‌روند.
/// </summary>
public sealed class ListingAndChannelFlowTests : DbTestBase
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];
    private static readonly byte[] Png  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0];

    private readonly FakeGateway _gateway = new(Channel.Telegram, Channel.Bale);

    private static CreatePropertyCommand Create(string title = "آپارتمان ۹۰ متری ونک") => new(
        title, "نورگیر", DealType.Sale, PropertyKind.Apartment, 5_000_000_000, null, null,
        90, 2, 3, 1, 1398, "تهران", "ونک", null, null, null);

    private sealed class Ctx : IDisposable
    {
        public required AmlakDbContext     Db         { get; init; }
        public required ListingHandlers    Listing    { get; init; }
        public required PublishingHandlers Publishing { get; init; }
        public required LocalPhotoStorage  Storage    { get; init; }
        public void Dispose() => Db.Dispose();
    }

    private Ctx NewCtx()
    {
        var db      = NewDb();
        var cfg     = Config();
        var storage = new LocalPhotoStorage(cfg);
        var props   = new PropertyRepository(db, HiLo);
        return new Ctx
        {
            Db         = db,
            Storage    = storage,
            Listing    = new ListingHandlers(props, storage, db),
            Publishing = new PublishingHandlers(props, new ChannelPostRepository(db, HiLo), _gateway, storage, cfg, db),
        };
    }

    /// <summary>کار DomainEventDispatchBehavior: رویدادهای ذخیره‌شده به handler های واقعی</summary>
    private static async Task DispatchEventsAsync(Ctx c)
    {
        var bus = new RoutingBus(c.Publishing);
        foreach (var e in c.Db.DrainDomainEvents())
        {
            var task = e switch
            {
                PropertyPublishedEvent x   => new PropertyPublishedEventHandler(bus).HandleAsync(x, default),
                PropertyUpdatedEvent x     => new PropertyUpdatedEventHandler(bus).HandleAsync(x, default),
                PropertyUnpublishedEvent x => new PropertyUnpublishedEventHandler(bus).HandleAsync(x, default),
                PropertyClosedEvent x      => new PropertyClosedEventHandler(bus).HandleAsync(x, default),
                PropertyDeletedEvent x     => new PropertyDeletedEventHandler(bus, c.Storage, NullLogger<PropertyDeletedEventHandler>.Instance).HandleAsync(x, default),
                _                          => Task.CompletedTask,
            };
            await task;
        }
    }

    private sealed class RoutingBus(PublishingHandlers publishing) : ICommandBus
    {
        public Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> request, CancellationToken token) => request switch
        {
            SyncPropertyChannelsCommand c   => (Task<TResponse>)(object)publishing.Handle(c, token),
            RemovePropertyChannelsCommand c => (Task<TResponse>)(object)publishing.Handle(c, token),
            _ => throw new NotSupportedException(request.GetType().Name),
        };

        public Task<long> Dispatch<T>(T command, CancellationToken token) => throw new NotSupportedException();
        public Task DispatchAsync<T>(T command, CancellationToken token) => throw new NotSupportedException();
        public void DispatchSync<T>(T command, CancellationToken token) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Same_title_gets_a_unique_slug()
    {
        using var c = NewCtx();
        var first  = await c.Listing.Handle(Create(), default);
        var second = await c.Listing.Handle(Create(), default);

        first.Slug.Should().Be("آپارتمان-۹۰-متری-ونک");
        second.Slug.Should().Be("آپارتمان-۹۰-متری-ونک-2");
    }

    [Fact]
    public async Task Photos_are_saved_with_the_tracked_aggregate_and_survive_a_reload()
    {
        long id;
        using (var c = NewCtx())
        {
            id = (await c.Listing.Handle(Create(), default)).Id;
            await c.Listing.Handle(new AddPropertyPhotoCommand(id, "a.jpg", Jpeg, null), default);
            var second = await c.Listing.Handle(new AddPropertyPhotoCommand(id, "b.png", Png, "نما"), default);
            await c.Listing.Handle(new SetCoverPhotoCommand(id, second), default);
        }

        using var db = NewDb();
        var photos = await db.PropertyPhotos.Where(p => p.PropertyId == id).OrderBy(p => p.SortOrder).ToListAsync();
        photos.Select(p => p.Alt).Should().Equal("نما", "آپارتمان ۹۰ متری ونک");
        Directory.GetFiles(PhotoRoot).Should().HaveCount(2);
    }

    [Fact]
    public async Task Rejected_photo_leaves_no_file_behind()
    {
        using var c = NewCtx();
        var id = (await c.Listing.Handle(Create(), default)).Id;

        await FluentActions.Awaiting(() => c.Listing.Handle(new AddPropertyPhotoCommand(id, "evil.jpg", Png, null), default))
            .Should().ThrowAsync<InvalidPhotoFileException>();

        (Directory.Exists(PhotoRoot) ? Directory.GetFiles(PhotoRoot) : []).Should().BeEmpty();
    }

    [Fact]
    public async Task Draft_is_never_sent_to_channels()
    {
        using var c = NewCtx();
        var id = (await c.Listing.Handle(Create(), default)).Id;
        await c.Listing.Handle(new UpdatePropertyCommand(id, "عنوان تازه", "d", DealType.Sale, PropertyKind.Villa,
            1, null, null, 100, null, null, null, null, "تهران", "ونک", null, null, null), default);
        await DispatchEventsAsync(c);

        _gateway.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_sends_once_per_channel_then_edits_instead_of_reposting()
    {
        using var c = NewCtx();
        var id = (await c.Listing.Handle(Create(), default)).Id;
        await c.Listing.Handle(new AddPropertyPhotoCommand(id, "a.jpg", Jpeg, null), default);
        await DispatchEventsAsync(c);

        await c.Listing.Handle(new PublishPropertyCommand(id), default);
        await DispatchEventsAsync(c);

        _gateway.Calls.Where(x => x.Method == "sendPhoto").Select(x => x.Channel).Should().BeEquivalentTo([Channel.Telegram, Channel.Bale]);
        var posts = await c.Db.ChannelPosts.Where(p => p.PropertyId == id).ToListAsync();
        posts.Should().HaveCount(2).And.OnlyContain(p => p.IsSent && p.HasPhoto && p.Error == null);

        _gateway.Calls.Clear();
        await c.Listing.Handle(new UpdatePropertyCommand(id, "قیمت جدید", "d", DealType.Sale, PropertyKind.Apartment,
            6_000_000_000, null, null, 90, 2, 3, 1, 1398, "تهران", "ونک", null, null, null), default);
        await DispatchEventsAsync(c);

        _gateway.Calls.Select(x => x.Method).Should().Equal("editMessageCaption", "editMessageCaption");
    }

    [Fact]
    public async Task Unpublish_and_close_delete_the_channel_message()
    {
        using var c = NewCtx();
        var id = (await c.Listing.Handle(Create(), default)).Id;
        await c.Listing.Handle(new PublishPropertyCommand(id), default);
        await DispatchEventsAsync(c);

        await c.Listing.Handle(new UnpublishPropertyCommand(id), default);
        await DispatchEventsAsync(c);
        _gateway.Calls.Count(x => x.Method == "deleteMessage").Should().Be(2);
        (await c.Db.ChannelPosts.CountAsync()).Should().Be(0);

        await c.Listing.Handle(new PublishPropertyCommand(id), default);
        await c.Listing.Handle(new ClosePropertyCommand(id), default);
        await DispatchEventsAsync(c);
        (await c.Db.ChannelPosts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Channel_failure_is_recorded_and_retried_on_next_sync()
    {
        using var c = NewCtx();
        var id = (await c.Listing.Handle(Create(), default)).Id;

        _gateway.Override = m => m == "sendMessage" ? ChannelResult.Fail("Bad Request: chat not found") : null;
        await c.Listing.Handle(new PublishPropertyCommand(id), default);
        await DispatchEventsAsync(c);

        var failed = await c.Db.ChannelPosts.ToListAsync();
        failed.Should().HaveCount(2).And.OnlyContain(p => !p.IsSent && p.Error == "Bad Request: chat not found");
        (await c.Db.Properties.SingleAsync()).Status.Should().Be(ListingStatus.Published, "شکست کانال انتشار را برنمی‌گرداند");

        _gateway.Override = null;
        await c.Publishing.Handle(new SyncPropertyChannelsCommand(id), default);
        (await c.Db.ChannelPosts.ToListAsync()).Should().OnlyContain(p => p.IsSent && p.Error == null);
    }

    [Fact]
    public async Task Already_deleted_message_counts_as_removed()
    {
        using var c = NewCtx();
        var id = (await c.Listing.Handle(Create(), default)).Id;
        await c.Listing.Handle(new PublishPropertyCommand(id), default);
        await DispatchEventsAsync(c);

        _gateway.Override = m => m == "deleteMessage" ? ChannelResult.Fail("Bad Request: message to delete not found") : null;
        await c.Listing.Handle(new UnpublishPropertyCommand(id), default);
        await DispatchEventsAsync(c);

        (await c.Db.ChannelPosts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_removes_posts_and_photo_files()
    {
        using var c = NewCtx();
        var id = (await c.Listing.Handle(Create(), default)).Id;
        await c.Listing.Handle(new AddPropertyPhotoCommand(id, "a.jpg", Jpeg, null), default);
        await c.Listing.Handle(new PublishPropertyCommand(id), default);
        await DispatchEventsAsync(c);

        await c.Listing.Handle(new DeletePropertyCommand(id), default);
        await DispatchEventsAsync(c);

        (await c.Db.ChannelPosts.CountAsync()).Should().Be(0);
        Directory.GetFiles(PhotoRoot).Should().BeEmpty();
        (await c.Db.Properties.CountAsync()).Should().Be(0, "حذف نرم با query filter پنهان می‌شود");
        await FluentActions.Awaiting(() => c.Listing.Handle(new PublishPropertyCommand(id), default))
            .Should().ThrowAsync<PropertyNotFoundException>();
    }

    [Fact]
    public async Task No_configured_channel_means_no_calls()
    {
        using var db = NewDb();
        var cfg   = Config();
        var store = new LocalPhotoStorage(cfg);
        var props = new PropertyRepository(db, HiLo);
        var none  = new FakeGateway();
        var pub   = new PublishingHandlers(props, new ChannelPostRepository(db, HiLo), none, store, cfg, db);
        var id    = (await new ListingHandlers(props, store, db).Handle(Create(), default)).Id;

        await pub.Handle(new SyncPropertyChannelsCommand(id), default);
        none.Calls.Should().BeEmpty();
    }
}
