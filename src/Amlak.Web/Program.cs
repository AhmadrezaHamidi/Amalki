using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Seo;
using Amlak.Web.Services.Sms;
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
builder.Services.AddSingleton(TimeProvider.System);
// Until an SMS provider (Kavenegar, SMS.ir, ...) is wired up, messages are written to the log.
builder.Services.AddScoped<ISmsSender, LogSmsSender>();
builder.Services.AddScoped<SmsGateway>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<BulkSmsService>();
builder.Services.AddHostedService<CampaignWorker>();
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
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapSeo();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
