namespace Amlak.Web.Domain;

/// <summary>An Iranian mobile number, always stored as 09xxxxxxxxx. An invalid number cannot be built.</summary>
public readonly record struct PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    /// <summary>Accepts what people type: Persian or Arabic digits, spaces, dashes, +98 / 0098 / 98 prefixes.</summary>
    public static bool TryParse(string? input, out PhoneNumber phone)
    {
        phone = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var d = new string(PersianText.LatinDigits(input).Where(char.IsAsciiDigit).ToArray());
        if (d.StartsWith("0098")) d = "0" + d[4..];
        else if (d.StartsWith("98")) d = "0" + d[2..];
        else if (d.StartsWith('9')) d = "0" + d;
        if (d.Length != 11 || !d.StartsWith("09")) return false;
        phone = new PhoneNumber(d);
        return true;
    }

    public static PhoneNumber Parse(string? input) =>
        TryParse(input, out var p) ? p : throw new DomainException("شمارهٔ موبایل معتبر نیست (مثل ۰۹۱۲۱۲۳۴۵۶۷)");
}

/// <summary>Text rules shared by everything people type in Persian.</summary>
public static class PersianText
{
    /// <summary>Trims and replaces Arabic yeh/kaf (ي ك ى) with the Persian letters, so searches and city names match.</summary>
    public static string Normalize(string? s) =>
        (s ?? "").Trim().Replace('ي', 'ی').Replace('ى', 'ی').Replace('ك', 'ک');

    /// <summary>Persian (۰-۹) and Arabic-Indic (٠-٩) digits to ASCII, so typed numbers parse.</summary>
    public static string LatinDigits(string s) => string.Create(s.Length, s, (span, src) =>
    {
        for (var i = 0; i < src.Length; i++)
            span[i] = src[i] switch
            {
                >= '۰' and <= '۹' => (char)('0' + (src[i] - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (src[i] - '٠')),
                _ => src[i],
            };
    });
}
