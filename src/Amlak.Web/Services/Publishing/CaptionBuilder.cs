namespace Amlak.Web.Services.Publishing;

public static class CaptionBuilder
{
    public static string Build(Property p, string baseUrl)
    {
        var deal = p.Deal == DealType.ShortTerm ? "اجاره_روزانه" : Tag(Labels.Deal(p.Deal));
        var lines = new List<string>
        {
            $"🏠 {p.Title}",
            $"📍 {p.City}، {p.District}",
            "📐 " + Fa.Digits(p.AreaM2.ToString()) + " متر" + (p.Bedrooms is { } b ? $" | {Fa.Digits(b.ToString())} خواب" : ""),
        };

        if (!p.ShowsPrice)
            lines.Add("💰 قیمت: توافقی");
        else if (p.Deal == DealType.Sale)
            lines.Add($"💰 قیمت: {PriceText.Label(p)}");
        else if (p.Deal == DealType.ShortTerm)
            lines.Add($"💰 هر شب: {Fa.Money(p.Price)} تومان");
        else
        {
            lines.Add($"💰 رهن: {Fa.Money(p.Deposit)} تومان");
            lines.Add($"🗓 اجارهٔ ماهانه: {Fa.Money(p.MonthlyRent)} تومان");
        }

        lines.Add($"🔗 {Url(p, baseUrl)}");
        lines.Add($"#{deal} #{Tag(p.City)} #{Tag(p.District)}");
        return string.Join("\n", lines);
    }

    public static string Url(Property p, string baseUrl) => $"{baseUrl.TrimEnd('/')}/ملک/{Uri.EscapeDataString(p.Slug)}";

    /// <summary>Private message to the admin chat about a new contact request.</summary>
    public static string Lead(Lead lead, Property? p, string baseUrl)
    {
        var lines = new List<string> { "📩 درخواست تماس جدید", $"👤 {lead.Name}", $"📞 {lead.Phone}" };
        if (p is not null) lines.Add($"🏠 {p.Title} — {Url(p, baseUrl)}");
        if (!string.IsNullOrWhiteSpace(lead.Message)) lines.Add($"💬 {lead.Message}");
        return string.Join("\n", lines);
    }

    public static string Submitted(Property p, string baseUrl) => string.Join("\n",
        "📝 آگهی جدید در انتظار بررسی",
        $"🏠 {p.Title} ({Labels.Deal(p.Deal)} {Labels.Kind(p.Kind)}، {p.District} {p.City})",
        $"👤 {p.OwnerName} — 📞 {p.OwnerPhone}",
        $"🔗 {baseUrl.TrimEnd('/')}/Admin/Edit/{p.Id}");

    public static string Ticket(Ticket t, string message, bool isNew, string baseUrl)
    {
        var lines = new List<string>
        {
            isNew ? "🎫 تیکت پشتیبانی جدید" : "💬 پیام تازه در تیکت",
            $"📌 {t.Subject} ({Labels.Category(t.Category)})",
            $"👤 {t.Name} — 📞 {t.Phone}",
            $"💬 {(message.Length > 500 ? message[..499] + "…" : message)}",
            $"🔗 {baseUrl.TrimEnd('/')}/Admin/Tickets?id={t.Id}",
        };
        return string.Join("\n", lines);
    }

    // Hashtags cannot contain spaces or punctuation.
    private static string Tag(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
}
