using Amlak.Application.Identity;
using Amlak.Domain.Identity.Enums;
using Amlak.Domain.Identity.Exceptions;
using Amlak.Persistence.EF.Repositories;
using Amlak.Persistence.EF.Services;

namespace Amlak.Application.Tests.Identity;

public sealed class IdentityHandlerTests : DbTestBase
{
    private readonly FakeSms _sms = new();

    private IdentityHandlers Handlers(AmlakDbContext db)
    {
        var cfg = Config();
        return new IdentityHandlers(new UserRepository(db, HiLo), new OtpRepository(db), new RefreshTokenRepository(db),
            _sms, new JwtService(cfg), cfg, db);
    }

    private static string CodeOf(RequestOtpResponse r) => r.Message.Split(':')[1];

    [Fact]
    public async Task Admin_role_comes_from_configuration_only()
    {
        using var db = NewDb();
        var h = Handlers(db);

        var admin = await h.Handle(new VerifyOtpCommand("09120000000", CodeOf(await h.Handle(new RequestOtpCommand("09120000000"), default))), default);
        var user  = await h.Handle(new VerifyOtpCommand("09121111111", CodeOf(await h.Handle(new RequestOtpCommand("09121111111"), default))), default);

        admin.Role.Should().Be(nameof(UserRole.Admin));
        user.Role.Should().Be(nameof(UserRole.Customer));
        admin.AccessToken.Should().NotBeNullOrEmpty();
        _sms.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task Second_code_within_a_minute_is_refused()
    {
        using var db = NewDb();
        var h = Handlers(db);
        await h.Handle(new RequestOtpCommand("09121111111"), default);

        await FluentActions.Awaiting(() => h.Handle(new RequestOtpCommand("09121111111"), default))
            .Should().ThrowAsync<OtpRequestTooSoonException>();
    }

    [Fact]
    public async Task Wrong_code_attempts_are_persisted()
    {
        using (var db = NewDb())
        {
            var h = Handlers(db);
            await h.Handle(new RequestOtpCommand("09121111111"), default);
            await FluentActions.Awaiting(() => h.Handle(new VerifyOtpCommand("09121111111", "000000"), default))
                .Should().ThrowAsync<OtpInvalidException>();
        }

        using var fresh = NewDb();
        (await fresh.OtpRequests.SingleAsync()).FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_logout_revokes_it()
    {
        using var db = NewDb();
        var h = Handlers(db);
        var login = await h.Handle(new VerifyOtpCommand("09121111111", CodeOf(await h.Handle(new RequestOtpCommand("09121111111"), default))), default);

        var refreshed = await h.Handle(new RefreshTokenCommand(login.RefreshToken), default);
        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);
        await FluentActions.Awaiting(() => h.Handle(new RefreshTokenCommand(login.RefreshToken), default))
            .Should().ThrowAsync<InvalidRefreshTokenException>("توکن قبلی باطل شده است");

        await h.Handle(new LogoutCommand(refreshed.RefreshToken), default);
        await FluentActions.Awaiting(() => h.Handle(new RefreshTokenCommand(refreshed.RefreshToken), default))
            .Should().ThrowAsync<InvalidRefreshTokenException>();
    }
}
