using Amlak.Application.Abstractions;
using Amlak.Infrastructure;
using Amlak.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Amlak.Tests;

/// <summary>Test mode: on only with the flag AND mock SMS; then every code sent is kept for the page to show.</summary>
public class DemoModeTests
{
    private static IConfiguration Cfg(string? demo, string? sms) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Enabled"] = demo, ["Sms:Provider"] = sms, ["Sms:ApiKey"] = "k", ["Sms:Template"] = "t" })
        .Build();

    [Theory]
    [InlineData("true", null, true)]
    [InlineData("true", "Log", true)]
    [InlineData("true", "Kavenegar", false)] // real SMS: never show codes or demo logins
    [InlineData("false", null, false)]
    [InlineData(null, null, false)]
    public void Demo_mode_needs_the_flag_and_mock_sms(string? demo, string? sms, bool on) =>
        Assert.Equal(on, DemoMode.IsOn(Cfg(demo, sms)));

    [Fact]
    public async Task In_demo_mode_the_sent_code_is_kept_for_the_page()
    {
        var cfg = Cfg("true", null);
        using var sp = new ServiceCollection().AddLogging().AddInfrastructure(cfg, Path.GetTempPath()).AddDemoMode(cfg).BuildServiceProvider();
        using var scope = sp.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISmsSender>().SendCodeAsync("09121234567", "123456"));
        Assert.Equal("123456", sp.GetRequiredService<DemoCodes>().For("09121234567"));
        Assert.Null(sp.GetRequiredService<DemoCodes>().For("09350000000"));
    }

    [Fact]
    public void Outside_demo_mode_nothing_is_recorded()
    {
        var cfg = Cfg("false", null);
        using var sp = new ServiceCollection().AddLogging().AddInfrastructure(cfg, Path.GetTempPath()).AddDemoMode(cfg).BuildServiceProvider();
        Assert.Null(sp.GetService<DemoCodes>());
    }
}
