using Amlak.Domain.Lead.Enums;

namespace Lead.Application.Queries;

public record GetLeadsQuery(
    int         Page       = 1,
    int         PageSize   = 20,
    LeadStatus? Status     = null,
    long?       PropertyId = null) : IQuery<PagedResult<LeadItem>>;

public record GetLeadQuery(long Id) : IQuery<LeadItem?>;

/// <summary>تعداد درخواست‌ها به تفکیک وضعیت — نشان روی منوی پنل</summary>
public record GetLeadStatsQuery : IQuery<IReadOnlyList<LeadStatItem>>;

public sealed record LeadItem(
    long      Id,
    long?     PropertyId,
    string?   PropertyTitle,
    string?   PropertySlug,
    string    FullName,
    string    PhoneNumber,
    string?   Message,
    string    Status,
    string?   AdminNote,
    DateTime  CreatedAt,
    DateTime? HandledAt);

public sealed record LeadStatItem(string Status, int Count);
