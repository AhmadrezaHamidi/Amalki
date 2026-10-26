using Amlak.Domain.Events.Listing;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Args;
using Amlak.Domain.Listing.Enums;
using Amlak.Domain.Listing.Exceptions;

namespace Amlak.Domain.Tests.Listing;

public class PropertyAggregateTests
{
    internal static PropertyDetailsArg Sale(long? price = 5_000_000_000) => new(
        "آپارتمان ۹۰ متری ونک", "نورگیر و بازسازی‌شده", DealType.Sale, PropertyKind.Apartment,
        price, null, null, 90, 2, 3, 1, 1398, "تهران", "ونک", null, null, null);

    internal static PropertyDetailsArg Rent(long? deposit, long? rent) => Sale() with
    {
        Deal = DealType.Rent, Price = null, Deposit = deposit, MonthlyRent = rent
    };

    internal static Property NewProperty(PropertyDetailsArg? details = null)
        => Property.Create(new CreatePropertyArg(1, "آپارتمان-۹۰-متری-ونک", details ?? Sale()));

    [Fact]
    public void Create_starts_as_draft_and_raises_created()
    {
        var p = NewProperty();

        p.Status.Should().Be(ListingStatus.Draft);
        p.PublishedAt.Should().BeNull();
        p.DomainEvents.Should().ContainSingle(e => e is PropertyCreatedEvent);
    }

    [Fact]
    public void Sale_without_price_is_rejected()
        => FluentActions.Invoking(() => NewProperty(Sale(price: null))).Should().Throw<SalePriceRequiredException>();

    [Fact]
    public void Rent_needs_deposit_or_monthly_rent()
    {
        FluentActions.Invoking(() => NewProperty(Rent(null, 0))).Should().Throw<RentAmountRequiredException>();
        NewProperty(Rent(500_000_000, null)).Deposit.Should().Be(500_000_000);
        NewProperty(Rent(null, 20_000_000)).MonthlyRent.Should().Be(20_000_000);
    }

    [Fact]
    public void Switching_to_rent_drops_the_old_sale_price()
    {
        var p = NewProperty();
        p.Update(Rent(300_000_000, 15_000_000) with { Price = 9_999 });

        p.Price.Should().BeNull();
        p.Deposit.Should().Be(300_000_000);
    }

    [Fact]
    public void Negative_amount_area_and_half_location_are_rejected()
    {
        FluentActions.Invoking(() => NewProperty(Sale(-1))).Should().Throw<InvalidAmountException>();
        FluentActions.Invoking(() => NewProperty(Sale() with { AreaM2 = 0 })).Should().Throw<InvalidAreaException>();
        FluentActions.Invoking(() => NewProperty(Sale() with { Latitude = 35.7 })).Should().Throw<InvalidLocationException>();
        FluentActions.Invoking(() => NewProperty(Sale() with { Latitude = 95, Longitude = 51 })).Should().Throw<InvalidLocationException>();
    }

    [Fact]
    public void Publish_sets_first_publish_time_once_and_raises_event()
    {
        var p = NewProperty();
        p.Publish();
        var first = p.PublishedAt;
        p.Unpublish();
        p.Publish();

        p.PublishedAt.Should().Be(first);
        p.DomainEvents.OfType<PropertyPublishedEvent>().Should().HaveCount(2);
        FluentActions.Invoking(p.Publish).Should().Throw<PropertyAlreadyPublishedException>();
    }

    [Fact]
    public void Unpublish_requires_published()
        => FluentActions.Invoking(NewProperty().Unpublish).Should().Throw<PropertyNotPublishedException>();

    [Fact]
    public void Closing_a_published_listing_raises_closed_but_closing_a_draft_does_not()
    {
        var published = NewProperty();
        published.Publish();
        published.Close();
        published.DomainEvents.Should().Contain(e => e is PropertyClosedEvent);

        var draft = NewProperty();
        draft.Close();
        draft.DomainEvents.Should().NotContain(e => e is PropertyClosedEvent);
        FluentActions.Invoking(draft.Close).Should().Throw<PropertyAlreadyClosedException>();
    }

    [Fact]
    public void Editing_raises_updated_only_while_published()
    {
        var p = NewProperty();
        p.Update(Sale(6_000_000_000));
        p.DomainEvents.Should().NotContain(e => e is PropertyUpdatedEvent);

        p.Publish();
        p.Update(Sale(7_000_000_000));
        p.DomainEvents.Should().ContainSingle(e => e is PropertyUpdatedEvent);
    }

    [Fact]
    public void Photos_keep_a_dense_order_and_cover_moves_to_front()
    {
        var p = NewProperty();
        p.AddPhoto(10, "a.jpg", null);
        p.AddPhoto(11, "b.jpg", "نما");
        p.AddPhoto(12, "c.jpg", null);

        p.Photos.First().Alt.Should().Be(p.Title, "alt خالی عنوان آگهی می‌شود");

        p.SetCoverPhoto(12);
        p.Photos.Select(x => x.Id).Should().Equal(12, 10, 11);

        p.RemovePhoto(10).Should().Be("a.jpg");
        p.Photos.Select(x => (x.Id, x.SortOrder)).Should().Equal((12, 0), (11, 1));
    }

    [Fact]
    public void Reorder_must_be_an_exact_permutation()
    {
        var p = NewProperty();
        p.AddPhoto(1, "a.jpg", null);
        p.AddPhoto(2, "b.jpg", null);

        FluentActions.Invoking(() => p.ReorderPhotos([1])).Should().Throw<InvalidPhotoOrderException>();
        FluentActions.Invoking(() => p.ReorderPhotos([1, 1])).Should().Throw<InvalidPhotoOrderException>();
        FluentActions.Invoking(() => p.ReorderPhotos([1, 3])).Should().Throw<InvalidPhotoOrderException>();

        p.ReorderPhotos([2, 1]);
        p.Photos.Select(x => x.Id).Should().Equal(2, 1);
    }

    [Fact]
    public void Photo_limit_is_enforced()
    {
        var p = NewProperty();
        for (var i = 0; i < Property.MaxPhotos; i++) p.AddPhoto(i + 1, $"{i}.jpg", null);

        FluentActions.Invoking(() => p.AddPhoto(99, "x.jpg", null)).Should().Throw<PhotoLimitExceededException>();
    }

    [Fact]
    public void Unknown_photo_is_rejected()
        => FluentActions.Invoking(() => NewProperty().RemovePhoto(42)).Should().Throw<PhotoNotFoundException>();

    [Fact]
    public void Delete_marks_deleted_and_lists_photo_files_for_cleanup()
    {
        var p = NewProperty();
        p.AddPhoto(1, "a.jpg", null);
        p.Delete();

        p.IsDeleted.Should().BeTrue();
        p.DomainEvents.OfType<PropertyDeletedEvent>().Single().PhotoFileNames.Should().Equal("a.jpg");
    }
}
