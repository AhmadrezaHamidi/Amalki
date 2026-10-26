using System.Security.Cryptography;

namespace Amlak.Web.Services;

/// <summary>Tracking codes for support tickets: 8 characters, no look-alikes (0/O, 1/I/L), ~40 bits of randomness.</summary>
public static class TicketCodes
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static string New() => RandomNumberGenerator.GetString(Alphabet, 8);

    /// <summary>"K7Q2M9XA" → "K7Q2-M9XA" for display.</summary>
    public static string Pretty(string code) => code.Length == 8 ? $"{code[..4]}-{code[4..]}" : code;

    /// <summary>Accepts what people type: lower case, dashes, spaces, Persian digits.</summary>
    public static string Normalize(string? input) =>
        new(Fa.Latin(input ?? "").ToUpperInvariant().Where(c => Alphabet.Contains(c)).ToArray());
}
