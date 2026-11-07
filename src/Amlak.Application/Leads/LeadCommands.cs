using Amlak.Application.Abstractions;

namespace Amlak.Application.Leads;

/// <summary>Contact / visit requests: a visitor sends one, staff mark it handled or delete it.</summary>
public sealed class LeadCommands(ILeadRepository leads, IUnitOfWork uow, TimeProvider clock)
{
    /// <param name="propertyId">The published listing the request is about, if any.</param>
    /// <param name="consultantId">The consultant the visitor picked, if any.</param>
    public async Task<Result<int>> SubmitAsync(string name, PhoneNumber phone, string? message, int? propertyId, int? consultantId = null, CancellationToken ct = default)
    {
        Lead lead;
        try
        {
            lead = Lead.Submit(name, phone, message, propertyId, clock.GetUtcNow().UtcDateTime, consultantId);
        }
        catch (DomainException ex)
        {
            return Result<int>.Fail(ex.Message);
        }
        leads.Add(lead);
        await uow.SaveChangesAsync(ct);
        return lead.Id;
    }

    public async Task<Result> ToggleHandledAsync(int id, CancellationToken ct = default)
    {
        var lead = await leads.GetAsync(id, ct);
        if (lead is null) return Result.NotFound();
        lead.ToggleHandled();
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var lead = await leads.GetAsync(id, ct);
        if (lead is null) return Result.NotFound();
        leads.Remove(lead);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
