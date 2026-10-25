using Msg = Amlak.Application.Validators.Resources.Message;

namespace Amlak.Application.Validators.Identity;

public sealed class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(Msg.PhoneNumberRequired)
            .Matches(Patterns.Mobile).WithMessage(Msg.PhoneNumberInvalid);
    }
}

public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(Msg.PhoneNumberRequired)
            .Matches(Patterns.Mobile).WithMessage(Msg.PhoneNumberInvalid);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(Msg.OtpCodeRequired)
            .Matches(@"^\d{4,6}$").WithMessage(Msg.OtpCodeInvalid);
    }
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
        => RuleFor(x => x.RefreshToken).NotEmpty().WithMessage(Msg.RefreshTokenRequired);
}

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(50).WithMessage(Msg.NameTooLong);
        RuleFor(x => x.LastName).MaximumLength(50).WithMessage(Msg.NameTooLong);
    }
}
