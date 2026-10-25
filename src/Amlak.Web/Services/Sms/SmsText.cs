namespace Amlak.Web.Services.Sms;

public static class SmsText
{
    /// <summary>Footer required on promotional SMS in Iran; operators unsubscribe numbers that reply with it.</summary>
    public const string OptOutFooter = "لغو۱۱";

    /// <summary>Fills {placeholders}; unknown placeholders are left as they are so mistakes stay visible in the preview.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        var text = template;
        foreach (var (key, value) in values)
            text = text.Replace("{" + key + "}", value ?? "");
        return text.Trim();
    }

    public static string WithOptOut(string text) =>
        text.Contains(OptOutFooter) ? text : text.TrimEnd() + "\n" + OptOutFooter;

    /// <summary>
    /// Number of billable parts. Text with any non-GSM character (all Persian text) is sent as UCS-2:
    /// 70 characters in one part, 67 per part when concatenated. GSM-7: 160 / 153.
    /// </summary>
    public static int Segments(string text)
    {
        if (text.Length == 0) return 0;
        var unicode = text.Any(c => c > 127);
        var (single, multi) = unicode ? (70, 67) : (160, 153);
        return text.Length <= single ? 1 : (int)Math.Ceiling(text.Length / (double)multi);
    }
}
