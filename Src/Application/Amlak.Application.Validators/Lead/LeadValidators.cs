using Amlak.Domain.Lead.Enums;
using Msg = Amlak.Application.Validators.Resources.Message;

namespace Amlak.Application.Validators.Lead;

public sealed class SubmitLeadCommandValidator : AbstractValidator<SubmitLeadCommand>
{
    public SubmitLeadCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage(Msg.FullNameRequired).MaximumLength(100).WithMessage(Msg.FullNameTooLong);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(Msg.PhoneNumberRequired)
            .Matches(Patterns.Mobile).WithMessage(Msg.PhoneNumberInvalid);

        RuleFor(x => x.Message).MaximumLength(1000).WithMessage(Msg.MessageTooLong);
        RuleFor(x => x.PropertyId).GreaterThan(0).WithMessage(Msg.IdInvalid);
    }
}

public sealed class ChangeLeadStatusCommandValidator : AbstractValidator<ChangeLeadStatusCommand>
{
    public ChangeLeadStatusCommandValidator()
    {
        RuleFor(x => x.Status).IsInEnum().NotEqual(LeadStatus.New).WithMessage(Msg.LeadStatusInvalid);
        RuleFor(x => x.Note).MaximumLength(1000).WithMessage(Msg.NoteTooLong);
    }
}
