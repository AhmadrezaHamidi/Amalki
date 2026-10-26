namespace Amlak.Web.Domain;

// Events carry the aggregate itself: ids are assigned by the database on save, and handlers run after it.

/// <summary>An event about one listing.</summary>
public interface IListingEvent : IDomainEvent
{
    Property Listing { get; }
}

public sealed record ListingCreated(Property Listing, DateTime OccurredAtUtc) : IListingEvent;
public sealed record ListingChanged(Property Listing, DateTime OccurredAtUtc) : IListingEvent;
public sealed record ListingPublished(Property Listing, DateTime OccurredAtUtc) : IListingEvent;
public sealed record ListingUnpublished(Property Listing, DateTime OccurredAtUtc) : IListingEvent;
public sealed record ListingPhotosChanged(Property Listing, DateTime OccurredAtUtc) : IListingEvent;
/// <summary>The listing row is gone after the save, so the event keeps what is needed to clean up after it.</summary>
public sealed record ListingDeleted(Property Listing, IReadOnlyList<PropertyPhoto> Photos, IReadOnlyList<ChannelPost> Posts, DateTime OccurredAtUtc) : IListingEvent;

public sealed record LeadSubmitted(Lead Lead, DateTime OccurredAtUtc) : IDomainEvent;

public sealed record TicketOpened(Ticket Ticket, TicketMessage Message, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record TicketMessageReceived(Ticket Ticket, TicketMessage Message, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record TicketAnswered(Ticket Ticket, TicketMessage Message, DateTime OccurredAtUtc) : IDomainEvent;
