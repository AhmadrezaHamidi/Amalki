using Amlak.Application.Abstractions;

namespace Amlak.Application.Consultants;

/// <summary>Staff add and edit the consultants visitors can call; a hidden consultant keeps their leads.</summary>
public sealed class ConsultantCommands(IConsultantRepository consultants, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<Result<int>> CreateAsync(ConsultantProfile profile, CancellationToken ct = default)
    {
        Consultant c;
        try
        {
            c = Consultant.Create(profile, clock.GetUtcNow().UtcDateTime);
        }
        catch (DomainException ex)
        {
            return Result<int>.Fail(ex.Message);
        }
        consultants.Add(c);
        await uow.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task<Result> UpdateAsync(int id, ConsultantProfile profile, CancellationToken ct = default)
    {
        var c = await consultants.GetAsync(id, ct);
        if (c is null) return Result.NotFound();
        try
        {
            c.Update(profile);
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.Message);
        }
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ToggleActiveAsync(int id, CancellationToken ct = default)
    {
        var c = await consultants.GetAsync(id, ct);
        if (c is null) return Result.NotFound();
        if (c.Active) c.Deactivate(); else c.Activate();
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
