using System.Collections.Concurrent;
using Amlak.Web.Data;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.Identity;

namespace Amlak.Web.Services;

/// <summary>
/// Test mode for trying the site without real SMS: the one-time code is shown on the page, and a bar at the top logs
/// in as a demo admin or a demo visitor with one click. On only when <c>Demo:Enabled</c> is true AND SMS is the mock
/// (<c>Sms:Provider</c> is not Kavenegar); with real SMS it stays off whatever the flag says, so it cannot leak into a
/// live site that sends codes.
/// </summary>
public static class DemoMode
{
    public const string UserPhone = "09120000000";
    public const string AdminEmail = "demo-admin@royalfinehomes.local";

    public static bool IsOn(IConfiguration cfg) =>
        cfg.GetValue<bool>("Demo:Enabled")
        && !string.Equals(cfg["Sms:Provider"], "Kavenegar", StringComparison.OrdinalIgnoreCase);

    public static IServiceCollection AddDemoMode(this IServiceCollection services, IConfiguration cfg)
    {
        if (!IsOn(cfg)) return services;
        services.AddSingleton<DemoCodes>();
        // Keep the real (log) sender, and remember each code so the page can show it.
        var inner = services.Last(d => d.ServiceType == typeof(ISmsSender));
        services.Remove(inner);
        services.AddScoped<ISmsSender>(sp => new RecordingSender(
            (ISmsSender)ActivatorUtilities.CreateInstance(sp, inner.ImplementationType!), sp.GetRequiredService<DemoCodes>()));
        return services;
    }

    public static void MapDemoMode(this WebApplication app)
    {
        if (!IsOn(app.Configuration)) return;
        app.Logger.LogWarning("Demo mode is ON: OTP codes are shown on screen and /demo/login signs anyone in. Never enable it on a live site.");

        // POST so a link or an image cannot log someone in; the layout is output-cached, so the forms carry no token.
        app.MapPost("/demo/login/user", (HttpContext ctx, PhoneVerification verification) =>
        {
            verification.SignIn(ctx.Response, UserPhone);
            return Results.Redirect(Encoded("/ورود"));
        }).DisableAntiforgery();

        app.MapPost("/demo/login/admin", async (UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) =>
        {
            var user = await users.FindByEmailAsync(AdminEmail);
            if (user is null)
            {
                user = new IdentityUser { UserName = AdminEmail, Email = AdminEmail, EmailConfirmed = true };
                await users.CreateAsync(user); // no password: this account can only come in through the demo button
            }
            if (!await users.IsInRoleAsync(user, AdminSeed.Role)) await users.AddToRoleAsync(user, AdminSeed.Role);
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.Redirect("/Admin");
        }).DisableAntiforgery();
    }

    private static string Encoded(string path) => Amlak.Web.Pages.Account.OtpModel.Encoded(path);

    private sealed class RecordingSender(ISmsSender inner, DemoCodes codes) : ISmsSender
    {
        public async Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default)
        {
            codes.Remember(mobile, code);
            return await inner.SendCodeAsync(mobile, code, ct);
        }

        public Task<bool> SendTextAsync(string mobile, string text, CancellationToken ct = default) => inner.SendTextAsync(mobile, text, ct);
    }
}

/// <summary>The last code sent to each number, for showing on the page in demo mode.</summary>
public sealed class DemoCodes
{
    private readonly ConcurrentDictionary<string, string> _last = new();
    public void Remember(string mobile, string code) => _last[mobile] = code;
    public string? For(string? mobile) => mobile is not null && _last.TryGetValue(mobile, out var c) ? c : null;
}
