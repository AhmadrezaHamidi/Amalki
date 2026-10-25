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

builder.Services.AddScoped<PhotoStorage>();
builder.Services.AddHttpClient("bots", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<ChannelPublisher>();
builder.Services.AddScoped<LeadNotifier>();

// Contact form: at most 5 submissions per IP per 10 minutes (NFR-09). Every other request is unlimited.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.Equals(Amlak.Web.Pages.ContactModel.Path)
            ? RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) })
            : RateLimitPartition.GetNoLimiter(""));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync("درخواست‌های زیادی فرستاده‌اید. لطفاً چند دقیقهٔ دیگر دوباره تلاش کنید.", ct);
    };
});
builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/Admin", "Admin"));

var app = builder.Build();

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
