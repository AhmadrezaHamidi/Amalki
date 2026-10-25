using Microsoft.EntityFrameworkCore.Design;

namespace Amlak.Persistence.EF;

/// <summary>Design-time factory برای EF Core migrations</summary>
public sealed class AmlakDbContextFactory : IDesignTimeDbContextFactory<AmlakDbContext>
{
    public AmlakDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AmlakDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;Database=AmlakApi;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsAssembly(typeof(AmlakDbContext).Assembly.FullName));

        return new AmlakDbContext(optionsBuilder.Options);
    }
}
