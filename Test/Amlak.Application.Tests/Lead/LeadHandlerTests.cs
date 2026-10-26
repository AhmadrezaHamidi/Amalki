using Amlak.Application.Lead;
using Amlak.Application.Listing;
using Amlak.Domain.Lead.Enums;
using Amlak.Domain.Lead.Exceptions;
using Amlak.Domain.Listing.Enums;
using Amlak.Domain.Listing.Exceptions;
using Amlak.Persistence.EF.Repositories;
using Amlak.Persistence.EF.Services;

namespace Amlak.Application.Tests.Lead;

public sealed class LeadHandlerTests : DbTestBase
{
    private (LeadHandlers Leads, ListingHandlers Listing, AmlakDbContext Db) Handlers()
    {
        var db    = NewDb();
        var props = new PropertyRepository(db, HiLo);
        return (new LeadHandlers(new LeadRepository(db, HiLo), props, db),
                new ListingHandlers(props, new LocalPhotoStorage(Config()), db), db);
    }

    private static CreatePropertyCommand Create() => new(
        "ویلا", "d", DealType.Rent, PropertyKind.Villa, null, 500_000_000, 20_000_000,
        200, 3, null, null, null, "رامسر", "ساحلی", null, null, null);

    [Fact]
    public async Task Lead_for_a_draft_listing_is_rejected_as_not_found()
    {
        var (leads, listing, db) = Handlers();
        var id = (await listing.Handle(Create(), default)).Id;

        await FluentActions.Awaiting(() => leads.Handle(new SubmitLeadCommand(id, "علی", "09121234567", null), default))
            .Should().ThrowAsync<PropertyNotFoundException>();

        await listing.Handle(new PublishPropertyCommand(id), default);
        var leadId = await leads.Handle(new SubmitLeadCommand(id, "علی", "09121234567", "بازدید جمعه"), default);
        (await db.Leads.SingleAsync(l => l.Id == leadId)).PropertyId.Should().Be(id);
    }

    [Fact]
    public async Task Same_phone_is_rate_limited()
    {
        var (leads, _, _) = Handlers();
        for (var i = 0; i < Domain.Lead.Aggregates.ContactLead.MaxPerPhoneInWindow; i++)
            await leads.Handle(new SubmitLeadCommand(null, "علی", "09121234567", null), default);

        await FluentActions.Awaiting(() => leads.Handle(new SubmitLeadCommand(null, "علی", "09121234567", null), default))
            .Should().ThrowAsync<TooManyLeadsException>();
        await leads.Handle(new SubmitLeadCommand(null, "رضا", "09351234567", null), default);
    }

    [Fact]
    public async Task Status_change_is_persisted()
    {
        var (leads, _, db) = Handlers();
        var id = await leads.Handle(new SubmitLeadCommand(null, "علی", "09121234567", null), default);
        await leads.Handle(new ChangeLeadStatusCommand(id, LeadStatus.Contacted, "تماس گرفته شد"), default);

        using var fresh = NewDb();
        var lead = await fresh.Leads.SingleAsync(l => l.Id == id);
        lead.Status.Should().Be(LeadStatus.Contacted);
        lead.AdminNote.Should().Be("تماس گرفته شد");
    }
}
