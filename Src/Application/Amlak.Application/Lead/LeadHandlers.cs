using Amlak.Domain.Lead.Aggregates;
using Amlak.Domain.Lead.Args;
using Amlak.Domain.Lead.Exceptions;
using Amlak.Domain.Listing.Exceptions;

namespace Amlak.Application.Lead;

/// <summary>Handler های درخواست تماس (سرنخ)</summary>
public sealed class LeadHandlers(
    ILeadRepository     leadRepo,
    IPropertyRepository propertyRepo,
    AmlakDbContext      db) :
    ICommandHandler<SubmitLeadCommand, long>,
    ICommandHandler<ChangeLeadStatusCommand, bool>
{
    public async Task<long> Handle(SubmitLeadCommand command, CancellationToken token)
    {
        // درخواست فقط برای آگهی قابل‌مشاهده؛ وجود پیش‌نویس‌ها لو نمی‌رود
        if (command.PropertyId is { } propertyId && !await propertyRepo.IsPublishedAsync(propertyId, token))
            throw new PropertyNotFoundException();

        var recent = await leadRepo.CountRecentByPhoneAsync(command.PhoneNumber, ContactLead.RateWindow, token);
        if (recent >= ContactLead.MaxPerPhoneInWindow)
            throw new TooManyLeadsException();

        var id   = await leadRepo.GetNextIdAsync();
        var lead = ContactLead.Submit(new SubmitLeadArg(id, command.PropertyId, command.FullName, command.PhoneNumber, command.Message));

        await leadRepo.AddAsync(lead, token);
        await db.SaveChangesAsync(token);
        return id;
    }

    public async Task<bool> Handle(ChangeLeadStatusCommand command, CancellationToken token)
    {
        var lead = await leadRepo.GetByIdAsync(command.Id, token) ?? throw new LeadNotFoundException();
        lead.ChangeStatus(command.Status, command.Note);

        await leadRepo.UpdateAsync(lead, token);
        await db.SaveChangesAsync(token);
        return true;
    }
}
