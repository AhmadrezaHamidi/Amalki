using Amlak.Domain.Listing.Enums;

namespace Amlak.Domain.Listing.Args;

/// <summary>
/// مشخصات قابل ویرایش آگهی — هم برای ساخت و هم برای ویرایش.
/// مبالغ به تومان. فروش: <see cref="Price"/>؛ اجاره: <see cref="Deposit"/> و/یا <see cref="MonthlyRent"/>.
/// </summary>
public sealed record PropertyDetailsArg(
    string       Title,
    string       Description,
    DealType     Deal,
    PropertyKind Kind,
    long?        Price,
    long?        Deposit,
    long?        MonthlyRent,
    int          AreaM2,
    int?         Bedrooms,
    int?         Floor,
    int?         Parking,
    int?         YearBuilt,
    string       City,
    string       District,
    string?      Address,
    double?      Latitude,
    double?      Longitude);

public sealed record CreatePropertyArg(long Id, string Slug, PropertyDetailsArg Details);
