namespace Amlak.Web.Domain;

// Events carry the aggregate itself: ids are assigned by the database on save, and handlers run after it.

public sealed record ListingCreated(Property Listing, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record ListingChanged(Property Listing, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record ListingPublished(Property Listing, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record ListingUnpublished(Property Listing, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record ListingPhotosChanged(Property Listing, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record ListingDeleted(Property Listing, DateTime OccurredAtUtc) : IDomainEvent;

public sealed record LeadSubmitted(Lead Lead, DateTime OccurredAtUtc) : IDomainEvent;

public sealed record TicketOpened(Ticket Ticket, TicketMessage Message, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record TicketMessageReceived(Ticket Ticket, TicketMessage Message, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record TicketAnswered(Ticket Ticket, TicketMessage Message, DateTime OccurredAtUtc) : IDomainEvent;
