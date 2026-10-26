using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Seo;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
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
builder.Services.AddProxySupport(builder.Configuration);
builder.Services.AddScoped<PhotoStorage>();
builder.Services.AddHttpClient("bots", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<ChannelPublisher>();
builder.Services.AddScoped<AdminNotifier>();

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
    await DevSeed.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
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
app.UseStaticFiles(); // serves runtime uploads in wwwroot/uploads
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapSeo();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
