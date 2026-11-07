namespace Amlak.Domain;

/// <summary>
/// A person who talks to the site's bot on Telegram or Bale. There is no separate bot database: this row
/// sits in the site's own database and ties the messenger chat to the site's identity, the mobile number
/// (the same one used for OTP login, tickets and leads). Until the number is shared it is anonymous.
/// </summary>
public class MessengerAccount
{
    private MessengerAccount() { } // EF Core

    public int Id { get; private set; }
    public Messenger Messenger { get; private set; }
    /// <summary>The private chat id (equals the user id on Telegram).</summary>
    public string ChatId { get; private set; } = "";
    public string Name { get; private set; } = "";
    /// <summary>Normalized 09… number once the person shared their own contact; null before.</summary>
    public string? Phone { get; private set; }
    /// <summary>Ticket code the next text message answers, set by the "reply" button.</summary>
    public string? ReplyingToTicket { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LinkedAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }

    public bool Linked => Phone is not null;

    public static MessengerAccount Start(Messenger messenger, string chatId, string? name, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(chatId)) throw new DomainException("شناسهٔ چت خالی است");
        return new MessengerAccount
        {
            Messenger = messenger, ChatId = chatId.Trim(), Name = Clip(name), CreatedAtUtc = nowUtc, LastSeenAtUtc = nowUtc,
        };
    }

    public void Seen(string? name, DateTime nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = Clip(name);
        LastSeenAtUtc = nowUtc;
    }

    /// <summary>
    /// Links the chat to a mobile number. <paramref name="ownContact"/> must be true: the messenger reported that
    /// the shared contact belongs to the sender, so nobody can claim someone else's tickets by forwarding a contact.
    /// </summary>
    public void Link(PhoneNumber phone, bool ownContact, DateTime nowUtc)
    {
        if (!ownContact) throw new DomainException("لطفاً شمارهٔ خودتان را با دکمهٔ «اشتراک شماره» بفرستید.");
        Phone = phone.Value;
        LinkedAtUtc = nowUtc;
        ReplyingToTicket = null;
    }

    public void Unlink()
    {
        Phone = null;
        LinkedAtUtc = null;
        ReplyingToTicket = null;
    }

    /// <summary>The next text answers this ticket. Only the ticket's own phone may answer it.</summary>
    public void BeginReply(Ticket ticket)
    {
        if (!Linked || ticket.Phone != Phone) throw new DomainException("این تیکت به شمارهٔ شما تعلق ندارد.");
        if (ticket.Status == TicketStatus.Closed) throw new DomainException("این تیکت بسته شده است.");
        ReplyingToTicket = ticket.Code;
    }

    public void EndReply() => ReplyingToTicket = null;

    private static string Clip(string? s)
    {
        s = (s ?? "").Trim();
        return s.Length <= 120 ? s : s[..120];
    }
}

/// <summary>
/// Every update id the bot has handled. Telegram and Bale redeliver an update when the answer was slow or
/// failed, so the webhook records the id first and skips one it has already seen.
/// </summary>
public class MessengerInbox
{
    public Messenger Messenger { get; set; }
    public long UpdateId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
