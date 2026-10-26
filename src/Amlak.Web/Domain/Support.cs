namespace Amlak.Web.Domain;

/// <summary>A visitor's contact/visit request, optionally tied to a listing.</summary>
public class Lead : AggregateRoot
{
    public const int MaxName = 80, MaxMessage = 1000;

    private Lead() { } // EF Core

    public int Id { get; private set; }
    public int? PropertyId { get; private set; }
    public Property? Property { get; private set; }
    public string Name { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public string? Message { get; private set; }
    public bool Handled { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Lead Submit(string name, PhoneNumber phone, string? message, int? propertyId, DateTime nowUtc)
    {
        name = (name ?? "").Trim();
        message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (name.Length is 0 or > MaxName) throw new DomainException("نام را وارد کنید (حداکثر ۸۰ نویسه)");
        if (message?.Length > MaxMessage) throw new DomainException("پیام حداکثر ۱۰۰۰ نویسه است");
        var lead = new Lead { Name = name, Phone = phone.Value, Message = message, PropertyId = propertyId, CreatedAtUtc = nowUtc };
        lead.Raise(new LeadSubmitted(lead, nowUtc));
        return lead;
    }

    public void ToggleHandled() => Handled = !Handled;
}

public enum TicketCategory { General = 1, Listing = 2, Technical = 3, Complaint = 4 }
public enum TicketStatus { Open = 0, Answered = 1, Closed = 2 }

/// <summary>A support conversation. Visitors reach it by its tracking code; staff reply from the panel.</summary>
public class Ticket : AggregateRoot
{
    public const int MaxName = 80, MaxSubject = 150;

    private readonly List<TicketMessage> _messages = [];

    private Ticket() { } // EF Core

    public int Id { get; private set; }
    /// <summary>Unguessable tracking code shown to the visitor (e.g. K7Q2M9XA).</summary>
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public string Subject { get; private set; } = "";
    public TicketCategory Category { get; private set; } = TicketCategory.General;
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyList<TicketMessage> Messages => _messages;

    public static Ticket Open(string code, string name, PhoneNumber phone, string subject, TicketCategory category, string message, DateTime nowUtc)
    {
        name = (name ?? "").Trim();
        subject = (subject ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("کد پیگیری خالی است");
        if (name.Length is 0 or > MaxName) throw new DomainException("نام را وارد کنید (حداکثر ۸۰ نویسه)");
        if (subject.Length is 0 or > MaxSubject) throw new DomainException("موضوع را بنویسید (حداکثر ۱۵۰ نویسه)");
        var t = new Ticket
        {
            Code = code, Name = name, Phone = phone.Value, Subject = subject,
            Category = Defined(category, "دستهٔ تیکت نامعتبر است"), CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc,
        };
        var first = t.Add(message, fromStaff: false, nowUtc);
        t.Raise(new TicketOpened(t, first, nowUtc));
        return t;
    }

    /// <summary>A visitor's follow-up. A closed ticket takes no more messages; the visitor opens a new one.</summary>
    public TicketMessage ReplyFromVisitor(string body, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed) throw new DomainException("این تیکت بسته شده است؛ برای موضوع تازه یک تیکت جدید ثبت کنید.");
        var m = Add(body, fromStaff: false, nowUtc);
        Status = TicketStatus.Open;
        Raise(new TicketMessageReceived(this, m, nowUtc));
        return m;
    }

    /// <summary>A staff answer. Answering a closed ticket reopens it as answered.</summary>
    public TicketMessage ReplyFromStaff(string body, DateTime nowUtc)
    {
        var m = Add(body, fromStaff: true, nowUtc);
        Status = TicketStatus.Answered;
        Raise(new TicketAnswered(this, m, nowUtc));
        return m;
    }

    public void ChangeStatus(TicketStatus to, DateTime nowUtc)
    {
        Status = Defined(to, "وضعیت تیکت نامعتبر است");
        UpdatedAtUtc = nowUtc;
    }

    private TicketMessage Add(string body, bool fromStaff, DateTime nowUtc)
    {
        body = (body ?? "").Trim();
        if (body.Length is 0 or > TicketMessage.MaxBody) throw new DomainException("پیام را بنویسید (حداکثر ۴۰۰۰ نویسه)");
        var m = new TicketMessage(fromStaff, body, nowUtc);
        _messages.Add(m);
        UpdatedAtUtc = nowUtc;
        return m;
    }
}

public class TicketMessage
{
    public const int MaxBody = 4000;

    private TicketMessage() { } // EF Core

    internal TicketMessage(bool fromStaff, string body, DateTime nowUtc)
    {
        FromStaff = fromStaff; Body = body; CreatedAtUtc = nowUtc;
    }

    public int Id { get; private set; }
    public int TicketId { get; private set; }
    public bool FromStaff { get; private set; }
    public string Body { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
}
