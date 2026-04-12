using backend.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Infrastructure.Data;

/// <summary>
/// Design-time factory used exclusively by EF Core tooling (migrations, scaffolding).
/// Reads the connection string directly so the Web startup project does not need to
/// be built or running when executing `dotnet ef` commands.
///
/// Usage:
///   dotnet ef migrations add &lt;Name&gt; --project src/Infrastructure
///   dotnet ef database update       --project src/Infrastructure
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__backendDb")
            ?? "Server=127.0.0.1;Port=5432;Database=backendDb;Username=admin;Password=password;";

        var services = new ServiceCollection();

        // Minimal stub so ApplicationDbContext constructor is satisfied at design time.
        services.AddSingleton<ICurrentUserService, DesignTimeCurrentUserService>();

        var sp = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options, sp.GetRequiredService<ICurrentUserService>());
    }

    /// <summary>No-op stub — global query filters that reference ICurrentUserService
    /// are not evaluated at migration time.</summary>
    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public string? UserId       => null;
        public int?    TenantId     => null;
        public int?    DomainUserId => null;
    }
}
