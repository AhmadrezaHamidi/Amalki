using System.Text.Json;
using Amlak.Web.Services.Publishing;

namespace Amlak.Web.Data;

/// <summary>
/// A domain event saved in the same transaction as the change that raised it, then handled in the
/// background (<see cref="Amlak.Web.Services.Events.OutboxDispatcher"/>). Nothing is lost when the
/// app stops between the save and the Telegram call, and a failed call is retried with backoff.
/// </summary>
public class OutboxMessage
{
    public long Id { get; set; }
    /// <summary>The event's type name, e.g. ListingPublished.</summary>
    public string Type { get; set; } = "";
    public int AggregateId { get; set; }
    /// <summary>A second id the handler needs, e.g. the ticket message.</summary>
    public int? RefId { get; set; }
    /// <summary>JSON for events whose aggregate is gone after the save (ListingDeleted).</summary>
    public string? Payload { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }

    public sealed record DeletedListing(List<DeletedListing.File> Photos, List<DeletedListing.Post> Posts)
    {
        public sealed record File(string Url, string? Variants);
        public sealed record Post(Channel Channel, List<string> MessageIds);
    }

    /// <summary>Called after the aggregate is saved, so database ids are known.</summary>
    public static OutboxMessage From(IDomainEvent e)
    {
        var m = new OutboxMessage { Type = e.GetType().Name, OccurredAtUtc = e.OccurredAtUtc, NextAttemptAtUtc = e.OccurredAtUtc };
        switch (e)
        {
            case ListingDeleted d:
                m.AggregateId = d.Listing.Id;
                m.Payload = JsonSerializer.Serialize(new DeletedListing(
                    d.Photos.Select(x => new DeletedListing.File(x.Url, x.Variants)).ToList(),
                    d.Posts.Select(x => new DeletedListing.Post(x.Channel, ChannelPublisher.MessageIdsOf(x).ToList())).ToList()));
                break;
            case IListingEvent l: m.AggregateId = l.Listing.Id; break;
            case LeadSubmitted l: m.AggregateId = l.Lead.Id; break;
            case TicketOpened t: m.AggregateId = t.Ticket.Id; m.RefId = t.Message.Id; break;
            case TicketMessageReceived t: m.AggregateId = t.Ticket.Id; m.RefId = t.Message.Id; break;
            case TicketAnswered t: m.AggregateId = t.Ticket.Id; m.RefId = t.Message.Id; break;
            default: throw new InvalidOperationException($"No outbox mapping for {e.GetType().Name}");
        }
        return m;
    }
}
