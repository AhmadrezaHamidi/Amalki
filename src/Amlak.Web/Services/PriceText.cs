namespace Amlak.Web.Services;

/// <summary>Price math for alerts and the one place that turns a listing's price into text, honouring <see cref="Property.PriceDisplay"/>.</summary>
public static class PriceText
{
    /// <summary>Common market conversion: 1 toman of monthly rent is worth about 33 toman of deposit (3% a month).</summary>
    public const decimal RentRate = 0.03m;

    /// <summary>Sale only, and only when the listing shows it (<see cref="PriceDisplay.Full"/>).</summary>
    public static long? PerSquareMeter(Property p) =>
        p.Deal == DealType.Sale && p.PriceDisplay == PriceDisplay.Full && p.Price is > 0 && p.AreaM2 > 0 ? p.Price / p.AreaM2 : null;

    /// <summary>Deposit that would replace the monthly rent entirely ("رهن کامل").</summary>
    public static long FullDeposit(long? deposit, long? monthlyRent) =>
        (deposit ?? 0) + (long)Math.Round((monthlyRent ?? 0) / RentRate);

    /// <summary>One amount to compare budgets and spot price drops: the price, or the full-deposit equivalent of a rent.</summary>
    public static long? Comparable(Property p) => p.Deal == DealType.Rent
        ? p.Deposit is null && p.MonthlyRent is null ? null : FullDeposit(p.Deposit, p.MonthlyRent)
        : p.Price is > 0 ? p.Price : null;

    /// <summary>One line, e.g. "۱۲٫۵ میلیارد تومان · متری ۱۰۴٫۱۷ میلیون", "رهن ۵۰۰ میلیون · اجاره ۱۰ میلیون", "توافقی".</summary>
    public static string Label(Property p)
    {
        if (!p.ShowsPrice) return "توافقی";
        if (p.Deal == DealType.Rent) return $"رهن {Fa.Money(p.Deposit)} · اجاره {Fa.Money(p.MonthlyRent)}";
        if (p.Deal == DealType.ShortTerm) return $"{Fa.Money(p.Price)} تومان / شب";
        var text = $"{Fa.Money(p.Price)} تومان";
        return PerSquareMeter(p) is { } perM ? text + $" · متری {Fa.Money(perM)}" : text;
    }
}
