using Amlak.Web.Domain;

namespace Amlak.Tests;

/// <summary>The business rules of the aggregates, without a database.</summary>
public class DomainTests
{
    private static readonly DateTime T0 = TestData.Now, T1 = TestData.Now.AddDays(3);

    private static ListingDetails Details(string city = "تهران", int area = 90) =>
        ListingDetails.Of("آپارتمان", "d", PropertyKind.Apartment, area, city, "ونک");

    [Fact]
    public void A_sale_draft_without_price_is_saved_but_cannot_go_live()
    {
        var p = Property.Create("a", Details(), Pricing.ForSale(null), T0);

        var ex = Assert.Throws<DomainException>(() => p.Publish(T0));
        Assert.Contains("قیمت", ex.Message);
        Assert.Equal(ListingStatus.Draft, p.Status);
        Assert.DoesNotContain(p.DomainEvents, e => e is ListingPublished);
    }

    [Fact]
    public void A_rent_needs_a_deposit_or_a_monthly_rent_to_go_live()
    {
        Assert.Throws<DomainException>(() => Property.Create("a", Details(), Pricing.ForRent(null, null), T0).Publish(T0));
        var p = Property.Create("b", Details(), Pricing.ForRent(null, 8_000_000), T0);
        p.Publish(T0);
        Assert.Equal(ListingStatus.Published, p.Status);
    }

    [Fact]
    public void Negative_amounts_and_bad_details_are_rejected()
    {
        Assert.Throws<DomainException>(() => Pricing.ForSale(-1));
        Assert.Throws<DomainException>(() => Property.Create("a", Details(area: 0), Pricing.ForSale(1), T0));
        Assert.Throws<DomainException>(() => Property.Create("a", Details(city: " "), Pricing.ForSale(1), T0));
        Assert.Throws<DomainException>(() => Property.Create("a", Details() with { Kind = (PropertyKind)42 }, Pricing.ForSale(1), T0));
    }

    [Fact]
    public void Publishing_keeps_the_first_date_and_raises_events_once_per_change()
    {
        var p = Property.Create("a", Details(), Pricing.ForSale(5), T0);
        p.ClearDomainEvents();

        p.Publish(T0);
        p.Publish(T0); // already live: nothing happens
        p.Unpublish(T1);
        p.Publish(T1);

        Assert.Equal(T0, p.PublishedAtUtc);
        Assert.Equal([typeof(ListingPublished), typeof(ListingUnpublished), typeof(ListingPublished)], p.DomainEvents.Select(e => e.GetType()));
    }

    [Fact]
    public void Closing_a_draft_raises_no_unpublish_event()
    {
        var p = Property.Create("a", Details(), Pricing.ForSale(5), T0);
        p.ClearDomainEvents();
        p.Close(T0);
        Assert.Equal(ListingStatus.Closed, p.Status);
        Assert.Empty(p.DomainEvents);
    }

    [Fact]
    public void A_live_listing_cannot_lose_its_price()
    {
        var p = TestData.Listing();
        Assert.Throws<DomainException>(() => p.Update(Details(), Pricing.ForSale(null), T1));
    }

    [Fact]
    public void Arabic_letters_in_place_names_are_normalized()
    {
        var p = Property.Create("a", ListingDetails.Of(" كرج ", "d", PropertyKind.Villa, 100, " كرج ", "مهرشهر"), Pricing.ForSale(1), T0);
        Assert.Equal("کرج", p.City);
        Assert.Equal("کرج", p.Title);
    }

    [Fact]
    public void Photos_get_the_next_sort_order_and_their_alt_text()
    {
        var p = TestData.Listing();
        var a = p.AddPhoto("/uploads/a.png", T0);
        var b = p.AddPhoto("/uploads/b.png", T0);
        Assert.Equal([0, 1], new[] { a.SortOrder, b.SortOrder });
        Assert.Equal("آپارتمان تست - ونک تهران", a.Alt);
        Assert.All(p.DomainEvents, e => Assert.IsType<ListingPhotosChanged>(e));
    }

    [Theory]
    [InlineData("۰۹۱۲ ۱۲۳ ۴۵۶۷", "09121234567")]
    [InlineData("+98 912 123 4567", "09121234567")]
    [InlineData("9121234567", "09121234567")]
    public void Phone_numbers_are_normalized(string input, string expected) =>
        Assert.Equal(expected, PhoneNumber.Parse(input).Value);

    [Theory]
    [InlineData("021-1234567")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_phone_numbers_cannot_be_built(string? input)
    {
        Assert.False(PhoneNumber.TryParse(input, out _));
        Assert.Throws<DomainException>(() => PhoneNumber.Parse(input));
    }

    private static Ticket NewTicket() =>
        Ticket.Open("K7Q2M9XA", "سارا", PhoneNumber.Parse("09121112233"), "سؤال", TicketCategory.General, "سلام", T0);

    [Fact]
    public void A_new_ticket_holds_the_first_message_and_raises_one_event()
    {
        var t = NewTicket();
        Assert.Equal("سلام", Assert.Single(t.Messages).Body);
        Assert.IsType<TicketOpened>(Assert.Single(t.DomainEvents));
        Assert.Equal(TicketStatus.Open, t.Status);
    }

    [Fact]
    public void A_closed_ticket_takes_no_visitor_message_but_a_staff_answer_reopens_it()
    {
        var t = NewTicket();
        t.ChangeStatus(TicketStatus.Closed, T1);

        Assert.Throws<DomainException>(() => t.ReplyFromVisitor("هنوز هست؟", T1));
        t.ReplyFromStaff("بله", T1);

        Assert.Equal(TicketStatus.Answered, t.Status);
        Assert.Equal(T1, t.UpdatedAtUtc);
        Assert.Equal(2, t.Messages.Count);
    }

    [Fact]
    public void Undefined_ticket_status_and_empty_messages_are_rejected()
    {
        var t = NewTicket();
        Assert.Throws<DomainException>(() => t.ChangeStatus((TicketStatus)99, T1));
        Assert.Throws<DomainException>(() => t.ReplyFromVisitor("  ", T1));
        Assert.Throws<DomainException>(() => Ticket.Open("C", "سارا", PhoneNumber.Parse("09121112233"), "s", (TicketCategory)9, "m", T0));
    }

    [Fact]
    public void A_lead_trims_its_input_and_raises_an_event()
    {
        var lead = Lead.Submit("  سارا ", PhoneNumber.Parse("09121112233"), "   ", 5, T0);
        Assert.Equal("سارا", lead.Name);
        Assert.Null(lead.Message);
        Assert.IsType<LeadSubmitted>(Assert.Single(lead.DomainEvents));
        lead.ToggleHandled();
        Assert.True(lead.Handled);
    }
}
