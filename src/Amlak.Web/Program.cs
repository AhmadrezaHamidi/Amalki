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
builder.Services.AddDbContext<AppDbContext>((sp, o) => o
    .UseSqlServer(builder.Configuration.GetConnectionString("Default"))
    .AddInterceptors(sp.GetRequiredService<ListingCacheInterceptor>()));
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

// File storage: "Local" (wwwroot/uploads, default) or "Minio" (Storage:Minio:* settings).
builder.Services.AddSingleton<Amlak.Web.Services.Storage.LocalFileStore>();
if (string.Equals(builder.Configuration["Storage:Provider"], "Minio", StringComparison.OrdinalIgnoreCase))
{
    var minio = builder.Configuration.GetSection("Storage:Minio").Get<Amlak.Web.Services.Storage.MinioOptions>() ?? new();
    if (string.IsNullOrWhiteSpace(minio.Endpoint) || string.IsNullOrWhiteSpace(minio.AccessKey) || string.IsNullOrWhiteSpace(minio.SecretKey))
        throw new InvalidOperationException("Storage:Provider is Minio but Storage:Minio:Endpoint/AccessKey/SecretKey are not set.");
    builder.Services.AddSingleton<Amlak.Web.Services.Storage.IFileStore>(new Amlak.Web.Services.Storage.MinioFileStore(minio));
}
else
{
    builder.Services.AddSingleton<Amlak.Web.Services.Storage.IFileStore>(sp => sp.GetRequiredService<Amlak.Web.Services.Storage.LocalFileStore>());
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TicketAccess>();
// /healthz for the load balancer or uptime monitor: 200 when the app can reach SQL Server, 503 otherwise.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddProxySupport(builder.Configuration);

// Emit Persian text as-is instead of &#x...; entities: smaller HTML that crawlers and AI engines read directly.
// HTML-sensitive characters (<, >, &, quotes) are still encoded.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

builder.Services.AddScoped<PhotoStorage>();
builder.Services.AddHttpClient("bots", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<ChannelPublisher>();
builder.Services.AddScoped<Amlak.Web.Read.ListingQueries>();
builder.Services.AddScoped<Amlak.Web.Read.AdminQueries>();
builder.Services.AddScoped<AdminNotifier>();
// Domain events saved with each change are handled here, outside the request (channel posts, admin alerts, file cleanup).
builder.Services.AddScoped<Amlak.Web.Services.Events.OutboxDispatcher>();
builder.Services.AddHostedService<Amlak.Web.Services.Events.OutboxProcessor>();

// SMS one-time codes for visitor listings: "Kavenegar" (Sms:ApiKey, Sms:Template) or "Log" (development).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Amlak.Web.Services.Sms.PhoneVerification>();
// Alerts and bulk SMS ("Sms:Sender" is the Kavenegar line for them; codes use the template).
builder.Services.AddScoped<Amlak.Web.Services.Sms.SmsGateway>();
builder.Services.AddScoped<Amlak.Web.Services.Sms.AlertService>();
builder.Services.AddScoped<Amlak.Web.Services.Sms.BulkSmsService>();
builder.Services.AddHostedService<Amlak.Web.Services.Sms.CampaignWorker>();
if (string.Equals(builder.Configuration["Sms:Provider"], "Kavenegar", StringComparison.OrdinalIgnoreCase))
{
    var key = builder.Configuration["Sms:ApiKey"];
    var template = builder.Configuration["Sms:Template"];
    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(template))
        throw new InvalidOperationException("Sms:Provider is Kavenegar but Sms:ApiKey/Sms:Template are not set.");
    builder.Services.AddScoped<Amlak.Web.Services.Sms.ISmsSender>(sp => new Amlak.Web.Services.Sms.KavenegarSmsSender(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("bots"), key, template, builder.Configuration["Sms:Sender"],
        sp.GetRequiredService<ILogger<Amlak.Web.Services.Sms.KavenegarSmsSender>>()));
}
else
{
    builder.Services.AddScoped<Amlak.Web.Services.Sms.ISmsSender, Amlak.Web.Services.Sms.LogSmsSender>();
}

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
builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/Admin", "Admin"));

var app = builder.Build();

// First, so the real client IP and scheme reach HTTPS redirection, the rate limiter and the logs.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await DevSeed.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Environment.WebRootPath);
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Admin account comes from configuration (user-secrets / env vars), never from source.
using (var scope = app.Services.CreateScope())
{
    await AdminSeed.RunAsync(scope.ServiceProvider, builder.Configuration, app.Logger);
}

app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment()) app.UseCanonicalHost(builder.Configuration);
app.UseSecurityHeaders();
// Serves runtime uploads in wwwroot/uploads. Names are random GUIDs and never reused, so browsers may keep them for a year.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    },
});
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseUiVariants(); // before the output cache, which keys entries on the variant
app.UseOutputCache(); // after auth, so the default policy can see signed-in admins and skip them

app.MapHealthChecks("/healthz").DisableHttpMetrics();
app.MapSeo();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
