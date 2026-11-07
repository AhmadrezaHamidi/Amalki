using Amlak.Web.Services.Bot;
using Amlak.Infrastructure;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Seo;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSiteCache();
// Compression, request time limits, Redis for several servers, OpenTelemetry (docs/PERFORMANCE.md).
builder.AddPerformance();
builder.Services.AddWebVitals();
// Pooled contexts: a request reuses a ready DbContext instead of building one (SQL connections are pooled by
// SqlClient underneath; size it with "Max Pool Size" in the connection string). The factory also lets a page
// run independent queries at the same time, each on its own context (home page).
builder.Services.AddPooledDbContextFactory<AppDbContext>((sp, o) => o
    .UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.CommandTimeout(builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 15)))
    .AddInterceptors(sp.GetRequiredService<ListingCacheInterceptor>()));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
builder.Services.AddDefaultIdentity<IdentityUser>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.Cookie.HttpOnly = true;
    o.SlidingExpiration = true;
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("Admin"));

// Adapters (Amlak.Infrastructure): file storage on local disk or MinIO, SMS through the log or Kavenegar.
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.WebRootPath);
// Test mode (Demo:Enabled + mock SMS): codes shown on screen, one-click demo admin / visitor login.
builder.Services.AddDemoMode(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TicketAccess>();
// /healthz for the load balancer or uptime monitor: 200 when the app can reach SQL Server, 503 otherwise.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddProxySupport(builder.Configuration);

// Emit Persian text as-is instead of &#x...; entities: smaller HTML that crawlers and AI engines read directly.
// HTML-sensitive characters (<, >, &, quotes) are still encoded.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

// Use cases (Amlak.Application) and the EF Core repositories behind their ports.
builder.Services.AddApplication();
builder.Services.AddScoped<PhotoStorage>();
builder.Services.AddHttpClient("bots", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<ChannelPublisher>();
builder.Services.AddScoped<Amlak.Web.Read.ListingQueries>();
builder.Services.AddScoped<Amlak.Web.Read.ConsultantQueries>();
builder.Services.AddScoped<Amlak.Web.Read.AdminQueries>();
builder.Services.AddScoped<AdminNotifier>();
builder.Services.AddScoped<BotDiagnostics>();
builder.Services.AddSingleton<Amlak.Web.Services.Bot.BotClients>();
builder.Services.AddScoped<Amlak.Web.Services.Bot.BotUpdateHandler>();
builder.Services.AddScoped<Amlak.Web.Services.Bot.BotTicketNotifier>();
builder.Services.AddHostedService<Amlak.Web.Services.Bot.BotPollingService>();
// Domain events saved with each change are handled here, outside the request (channel posts, admin alerts, file cleanup).
builder.Services.AddScoped<Amlak.Web.Services.Events.OutboxDispatcher>();
builder.Services.AddHostedService<Amlak.Web.Services.Events.OutboxProcessor>();

// SMS one-time codes for visitor listings: "Kavenegar" (Sms:ApiKey, Sms:Template) or "Log" (development).
builder.Services.AddScoped<Amlak.Web.Services.Sms.PhoneVerification>();
// Alerts and bulk SMS ("Sms:Sender" is the Kavenegar line for them; codes use the template).
builder.Services.AddScoped<Amlak.Web.Services.Sms.SmsGateway>();
builder.Services.AddScoped<Amlak.Web.Services.Sms.AlertService>();
builder.Services.AddScoped<Amlak.Web.Services.Sms.BulkSmsService>();
builder.Services.AddHostedService<Amlak.Web.Services.Sms.CampaignWorker>();
// Public forms, per IP per 10 minutes (NFR-09): contact form 5 posts, support tickets 10 (new ticket,
// tracking lookup and replies share one allowance). Every other request is unlimited.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (!HttpMethods.IsPost(ctx.Request.Method)) return RateLimitPartition.GetNoLimiter("");
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
        var path = ctx.Request.Path;
        var (bucket, limit) =
            path.Equals(Amlak.Web.Pages.ContactModel.Path) ? ("contact", 5)
            : path.StartsWithSegments(Amlak.Web.Pages.Support.IndexModel.Path) ? ("support", 10)
            // Code requests, code checks and submissions share one allowance; codes are also limited per phone.
            : path.Equals(Amlak.Web.Pages.SubmitModel.Path) ? ("submit", 10)
            : path.Equals(Amlak.Web.Pages.TrackModel.Path) ? ("track", 10)
            : path.Equals(Amlak.Web.Pages.Account.OtpModel.Path) ? ("login", 10)
            : ("", 0);
        return limit == 0
            ? RateLimitPartition.GetNoLimiter("")
            : RateLimitPartition.GetFixedWindowLimiter($"{bucket}:{ip}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(10) });
    });
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync("درخواست‌های زیادی فرستاده‌اید. لطفاً چند دقیقهٔ دیگر دوباره تلاش کنید.", ct);
    };
});
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", "Admin");
    // Photo uploads from a phone on a slow network can take longer than the page time limit.
    var noTimeout = new Microsoft.AspNetCore.Http.Timeouts.DisableRequestTimeoutAttribute();
    o.Conventions.AddFolderApplicationModelConvention("/Admin", m => m.EndpointMetadata.Add(noTimeout));
    o.Conventions.AddPageApplicationModelConvention("/Submit", m => m.EndpointMetadata.Add(noTimeout));
});

var app = builder.Build();

// First, so the real client IP and scheme reach HTTPS redirection, the rate limiter and the logs.
app.UseForwardedHeaders();

// Database: Development always migrates and seeds sample listings. Elsewhere Database:Migrate applies the
// migrations at startup (docker-compose turns it on) and Database:SeedDemo adds the sample listings too.
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Database:SeedDemo"))
{
    using var scope = app.Services.CreateScope();
    await DevSeed.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Environment.WebRootPath);
}
else if (builder.Configuration.GetValue<bool>("Database:Migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Admin account comes from configuration (user-secrets / env vars), never from source.
using (var scope = app.Services.CreateScope())
{
    await AdminSeed.RunAsync(scope.ServiceProvider, builder.Configuration, app.Logger);
}

app.UseResponseCompression();
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment()) app.UseCanonicalHost(builder.Configuration);
app.UseSecurityHeaders();
// Runtime uploads, fonts, photos and icons, with browser-cache headers (Performance.StaticFiles).
app.UseStaticFiles(Performance.StaticFiles());
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseRouting();
app.UseRateLimiter();
app.UseRequestTimeouts();
app.UseAuthentication();
app.UseAuthorization();
app.UseUiVariants(); // before the output cache, which keys entries on the variant
app.UseOutputCache(); // after auth, so the default policy can see signed-in admins and skip them

app.MapHealthChecks("/healthz").DisableHttpMetrics();
app.MapSeo();
app.MapDemoMode();
app.MapSuggest();
app.MapWebVitals();
app.MapBotWebhooks();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
