namespace Amlak.Application.Listings;

/// <summary>What an admin or a visitor filled in for a listing, before the domain checks it.</summary>
public sealed record ListingForm
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required DealType Deal { get; init; }
    public required PropertyKind Kind { get; init; }
    public required int AreaM2 { get; init; }
    public required string City { get; init; }
    public required string District { get; init; }
    public long? Price { get; init; }
    public long? Deposit { get; init; }
    public long? MonthlyRent { get; init; }
    public bool Convertible { get; init; }
    public int? Bedrooms { get; init; }
    public int? Floor { get; init; }
    public int? Parking { get; init; }
    public int? YearBuilt { get; init; }
    public string? Address { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public bool? HasElevator { get; init; }
    public bool? HasStorage { get; init; }
    public bool? HasBalcony { get; init; }
    public DeedType? Deed { get; init; }

    /// <exception cref="DomainException">A value breaks a listing rule.</exception>
    internal ListingDetails Details() =>
        ListingDetails.Of(Title, Description, Kind, AreaM2, City, District, Bedrooms, Floor, Parking, YearBuilt, Address, Latitude, Longitude)
            with { HasElevator = HasElevator, HasStorage = HasStorage, HasBalcony = HasBalcony, Deed = Deed };

    /// <exception cref="DomainException">An amount is negative or the deal type is unknown.</exception>
    internal Pricing Pricing() => Domain.Pricing.For(Deal, Price, Deposit, MonthlyRent, Convertible);
}
