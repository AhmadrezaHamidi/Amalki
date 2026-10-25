namespace Amlak.Web.Services;

/// <summary>Price math and the one place that decides how a listing's price is shown.</summary>
public static class Pricing
{
    /// <summary>Common market conversion: every 1 toman of deposit replaces 0.03 toman of monthly rent.</summary>
    public const decimal DefaultRentRate = 0.03m;

    public static long? PerSquareMeter(Property p) =>
        p.Deal == DealType.Sale && p.Price is > 0 && p.AreaM2 > 0 ? p.Price / p.AreaM2 : null;

    /// <summary>Deposit that would replace the monthly rent entirely ("رهن کامل").</summary>
    public static long FullDeposit(long? deposit, long? monthlyRent, decimal rate = DefaultRentRate) =>
        (deposit ?? 0) + (long)Math.Round((monthlyRent ?? 0) / rate);

    /// <summary>Single comparable amount: sale price, or full-deposit equivalent for rentals.</summary>
    public static long? Effective(Property p) => p.Deal == DealType.Sale
        ? p.Price is > 0 ? p.Price : null
        : p.Deposit is null && p.MonthlyRent is null ? null : FullDeposit(p.Deposit, p.MonthlyRent);

    /// <summary>Price text honouring <see cref="Property.PriceDisplay"/>, e.g. "۱۲٫۵ میلیارد تومان · متری ۱۰۴٫۱۷ میلیون".</summary>
    public static string Label(Property p)
    {
        if (p.PriceDisplay == PriceDisplay.Negotiable) return "توافقی";
        if (p.Deal == DealType.Rent)
            return $"رهن {Fa.Money(p.Deposit)} · اجاره {Fa.Money(p.MonthlyRent)}";

        var text = $"{Fa.Money(p.Price)} تومان";
        if (p.PriceDisplay == PriceDisplay.Full && PerSquareMeter(p) is { } perM)
            text += $" · متری {Fa.Money(perM)}";
        return text;
    }
}
