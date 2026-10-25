using Amlak.Domain.Identity.Entities;
using Amlak.Domain.Identity.Exceptions;

namespace Amlak.Domain.Tests.Identity;

public class OtpRequestTests
{
    [Fact]
    public void Correct_code_is_consumed_once()
    {
        var otp = new OtpRequest(1, "09121234567", "123456");
        otp.Verify("123456");

        otp.IsUsed.Should().BeTrue();
        FluentActions.Invoking(() => otp.Verify("123456")).Should().Throw<OtpAlreadyUsedException>();
    }

    [Fact]
    public void Code_burns_after_max_wrong_attempts_so_it_cannot_be_brute_forced()
    {
        var otp = new OtpRequest(1, "09121234567", "123456");
        for (var i = 0; i < OtpRequest.MaxAttempts; i++)
            FluentActions.Invoking(() => otp.Verify("000000")).Should().Throw<OtpInvalidException>();

        FluentActions.Invoking(() => otp.Verify("123456")).Should().Throw<OtpAlreadyUsedException>();
    }

    [Fact]
    public void Expired_code_is_rejected()
    {
        var otp = new OtpRequest(1, "09121234567", "123456", expiryMinutes: -1);
        FluentActions.Invoking(() => otp.Verify("123456")).Should().Throw<OtpExpiredException>();
    }
}
