namespace Amlak.Application;

/// <summary>The use cases, by type, for the composition root to register (the project has no DI package on purpose).</summary>
public static class UseCases
{
    public static readonly Type[] All =
    [
        typeof(Listings.ListingCommands),
        typeof(Leads.LeadCommands),
        typeof(Support.TicketCommands),
    ];
}
