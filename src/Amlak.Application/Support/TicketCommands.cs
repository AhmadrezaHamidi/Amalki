using Amlak.Application.Abstractions;

namespace Amlak.Application.Support;

/// <summary>Support tickets: visitors open and follow up by tracking code, staff answer and change the status.</summary>
public sealed class TicketCommands(ITicketRepository tickets, IUnitOfWork uow, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <returns>The new ticket's tracking code.</returns>
    public async Task<Result<string>> OpenAsync(string name, PhoneNumber phone, string subject, TicketCategory category, string message,
        CancellationToken ct = default)
    {
        string code;
        do code = TicketCodes.New(); while (await tickets.CodeExistsAsync(code, ct));
        Ticket ticket;
        try
        {
            ticket = Ticket.Open(code, name, phone, subject, category, message, Now);
        }
        catch (DomainException ex)
        {
            return Result<string>.Fail(ex.Message);
        }
        tickets.Add(ticket);
        await uow.SaveChangesAsync(ct);
        return ticket.Code;
    }

    /// <param name="code">The tracking code as typed; it is normalized here.</param>
    public async Task<Result> ReplyFromVisitorAsync(string code, string body, CancellationToken ct = default)
    {
        var t = await tickets.GetByCodeAsync(TicketCodes.Normalize(code), ct);
        return t is null ? Result.NotFound() : await CommitAsync(() => t.ReplyFromVisitor(body, Now), ct);
    }

    public async Task<Result> ReplyFromStaffAsync(int id, string body, CancellationToken ct = default)
    {
        var t = await tickets.GetAsync(id, ct);
        return t is null ? Result.NotFound() : await CommitAsync(() => t.ReplyFromStaff(body, Now), ct);
    }

    public async Task<Result> ChangeStatusAsync(int id, TicketStatus to, CancellationToken ct = default)
    {
        var t = await tickets.GetAsync(id, ct);
        return t is null ? Result.NotFound() : await CommitAsync(() => t.ChangeStatus(to, Now), ct);
    }

    private async Task<Result> CommitAsync(Action change, CancellationToken ct)
    {
        try
        {
            change();
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.Message);
        }
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
