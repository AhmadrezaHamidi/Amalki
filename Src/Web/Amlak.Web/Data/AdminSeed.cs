using Microsoft.AspNetCore.Identity;

namespace Amlak.Web.Data;

public static class AdminSeed
{
    public const string Role = "Admin";

    /// <summary>Creates the Admin role and the first admin from Admin:Email / Admin:Password if both are configured.</summary>
    public static async Task RunAsync(IServiceProvider sp, IConfiguration cfg, ILogger log)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roles.RoleExistsAsync(Role))
            await roles.CreateAsync(new IdentityRole(Role));

        var email = cfg["Admin:Email"];
        var password = cfg["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            log.LogWarning("Admin:Email / Admin:Password not configured; no admin user was created.");
            return;
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                log.LogError("Admin creation failed: {Errors}", string.Join("; ", created.Errors.Select(e => e.Code)));
                return;
            }
        }
        if (!await users.IsInRoleAsync(user, Role))
            await users.AddToRoleAsync(user, Role);
    }
}
