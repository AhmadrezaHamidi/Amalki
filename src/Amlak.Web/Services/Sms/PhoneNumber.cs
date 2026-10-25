namespace Amlak.Web.Services.Sms;

public static class PhoneNumber
{
    /// <summary>
    /// Normalizes an Iranian mobile number to 09xxxxxxxxx. Accepts Persian/Arabic digits, spaces, dashes
    /// and the +98 / 0098 / 98 prefixes. Returns null when it is not a mobile number.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Select(c => c switch
        {
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            _ => c,
        }).Where(char.IsAsciiDigit).ToArray());

        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits[2..];
        else if (digits.StartsWith('9') && digits.Length == 10) digits = "0" + digits;

        return digits.Length == 11 && digits.StartsWith("09") ? digits : null;
    }
}
