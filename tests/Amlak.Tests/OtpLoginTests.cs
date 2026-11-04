using Amlak.Domain;
using Amlak.Application.Abstractions;
using Amlak.Application.Identity;

namespace Amlak.Tests;

/// <summary>
/// OTP login, test-first: the code's own rules live on the <see cref="PhoneCode"/> aggregate, the flow
/// (rate limit, sending, picking the latest code) in the <see cref="OtpLogin"/> use case. No database here:
/// the ports are faked, so these tests pin the behaviour, not EF.
/// </summary>
public class OtpLoginTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);
    private static readonly PhoneNumber Mobile = PhoneNumber.Parse("09121234567");

    // ---------- Domain: PhoneCode ----------

    [Fact]
    public void A_code_is_stored_only_as_a_hash()
    {
        var code = PhoneCode.Issue(Mobile, "123456", T0);
        Assert.Equal("09121234567", code.Phone);
        Assert.DoesNotContain("123456", code.CodeHash);
        Assert.Equal(64, code.CodeHash.Length);
        Assert.Equal(T0 + PhoneCode.Lifetime, code.ExpiresAtUtc);
    }

    [Fact]
    public void The_right_code_verifies_once()
    {
        var code = PhoneCode.Issue(Mobile, "123456", T0);
        Assert.Equal(OtpCheck.Verified, code.Verify("123456", T0.AddMinutes(1)));
        Assert.True(code.Used);
        Assert.Equal(OtpCheck.Used, code.Verify("123456", T0.AddMinutes(1)));
    }

    [Fact]
    public void Persian_digits_and_spaces_are_accepted()
    {
        var code = PhoneCode.Issue(Mobile, "123456", T0);
        Assert.Equal(OtpCheck.Verified, code.Verify(" ۱۲۳۴۵۶ ", T0));
    }

    [Fact]
    public void A_wrong_code_counts_an_attempt_and_locks_after_the_limit()
    {
        var code = PhoneCode.Issue(Mobile, "123456", T0);
        for (var i = 0; i < PhoneCode.MaxAttempts; i++) Assert.Equal(OtpCheck.Wrong, code.Verify("000000", T0));
        Assert.Equal(PhoneCode.MaxAttempts, code.Attempts);
        Assert.Equal(OtpCheck.Locked, code.Verify("123456", T0));
        Assert.False(code.Used);
    }

    [Fact]
    public void An_expired_code_is_refused_even_when_right()
    {
        var code = PhoneCode.Issue(Mobile, "123456", T0);
        Assert.Equal(OtpCheck.Expired, code.Verify("123456", T0 + PhoneCode.Lifetime));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void Only_six_digit_codes_can_be_issued(string bad) =>
        Assert.Throws<DomainException>(() => PhoneCode.Issue(Mobile, bad, T0));

    // ---------- Application: OtpLogin ----------

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(T0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Codes : IPhoneCodeRepository
    {
        public readonly List<PhoneCode> All = [];
        public Task<bool> IssuedSinceAsync(string phone, DateTime sinceUtc, CancellationToken ct = default) =>
            Task.FromResult(All.Any(c => c.Phone == phone && c.CreatedAtUtc > sinceUtc));
        public Task<PhoneCode?> LatestOpenAsync(string phone, DateTime nowUtc, CancellationToken ct = default) =>
            Task.FromResult(All.Where(c => c.Phone == phone && !c.Used && c.ExpiresAtUtc > nowUtc).MaxBy(c => c.CreatedAtUtc));
        public void Add(PhoneCode code) => All.Add(code);
    }

    private sealed class Sms : ISmsSender
    {
        public bool Works = true;
        public readonly List<(string Mobile, string Code)> Codes = [];
        public Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default)
        {
            Codes.Add((mobile, code));
            return Task.FromResult(Works);
        }
        public Task<bool> SendTextAsync(string mobile, string text, CancellationToken ct = default) => Task.FromResult(Works);
    }

    private sealed class Uow : IUnitOfWork
    {
        public int Saves;
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
    }

    private readonly Clock _clock = new();
    private readonly Codes _codes = new();
    private readonly Sms _sms = new();
    private readonly Uow _uow = new();
    private OtpLogin Login() => new(_codes, _sms, _uow, _clock);

    [Fact]
    public async Task Requesting_a_code_saves_it_and_texts_six_digits()
    {
        Assert.Equal(OtpRequest.Sent, await Login().RequestCodeAsync("۰۹۱۲ ۱۲۳ ۴۵۶۷"));
        var (to, code) = Assert.Single(_sms.Codes);
        Assert.Equal("09121234567", to);
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Single(_codes.All);
        Assert.Equal(1, _uow.Saves);
    }

    [Fact]
    public async Task An_invalid_number_gets_no_code()
    {
        Assert.Equal(OtpRequest.InvalidPhone, await Login().RequestCodeAsync("12345"));
        Assert.Empty(_sms.Codes);
    }

    [Fact]
    public async Task One_code_per_number_per_resend_window()
    {
        var login = Login();
        Assert.Equal(OtpRequest.Sent, await login.RequestCodeAsync("09121234567"));
        Assert.Equal(OtpRequest.TooSoon, await login.RequestCodeAsync("09121234567"));
        Assert.Equal(OtpRequest.Sent, await login.RequestCodeAsync("09350000000")); // other numbers are not held up
        _clock.Now += OtpLogin.ResendAfter + TimeSpan.FromSeconds(1);
        Assert.Equal(OtpRequest.Sent, await login.RequestCodeAsync("09121234567"));
        Assert.Equal(3, _sms.Codes.Count);
    }

    [Fact]
    public async Task A_failed_sms_is_reported()
    {
        _sms.Works = false;
        Assert.Equal(OtpRequest.Failed, await Login().RequestCodeAsync("09121234567"));
    }

    [Fact]
    public async Task The_texted_code_logs_in_and_returns_the_normalized_number()
    {
        var login = Login();
        await login.RequestCodeAsync("+989121234567");
        var result = await login.VerifyAsync("09121234567", _sms.Codes[0].Code);
        Assert.True(result.Succeeded);
        Assert.Equal("09121234567", result.Value);
        Assert.False((await login.VerifyAsync("09121234567", _sms.Codes[0].Code)).Succeeded); // single use
    }

    [Fact]
    public async Task Only_the_latest_code_counts()
    {
        var login = Login();
        await login.RequestCodeAsync("09121234567");
        _clock.Now += OtpLogin.ResendAfter + TimeSpan.FromSeconds(1);
        await login.RequestCodeAsync("09121234567");
        var (old, latest) = (_sms.Codes[0].Code, _sms.Codes[1].Code);
        if (old != latest) Assert.False((await login.VerifyAsync("09121234567", old)).Succeeded);
        Assert.True((await login.VerifyAsync("09121234567", latest)).Succeeded);
    }

    [Fact]
    public async Task Wrong_codes_are_saved_so_the_lock_survives_requests()
    {
        var login = Login();
        await login.RequestCodeAsync("09121234567");
        var saves = _uow.Saves;
        var wrong = _sms.Codes[0].Code == "000000" ? "111111" : "000000";
        var result = await login.VerifyAsync("09121234567", wrong);
        Assert.False(result.Succeeded);
        Assert.Equal("کد درست نیست.", result.Error);
        Assert.Equal(saves + 1, _uow.Saves);
        Assert.Equal(1, _codes.All[0].Attempts);
    }

    [Fact]
    public async Task No_code_and_expired_code_get_a_persian_error()
    {
        var login = Login();
        Assert.Equal("کدی برای این شماره فرستاده نشده یا منقضی شده است.", (await login.VerifyAsync("09121234567", "123456")).Error);
        await login.RequestCodeAsync("09121234567");
        _clock.Now += PhoneCode.Lifetime;
        Assert.False((await login.VerifyAsync("09121234567", _sms.Codes[0].Code)).Succeeded);
    }
}
