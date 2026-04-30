using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderFlow.Identity.Api.Data;

namespace OrderFlow.Identity.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "IdentityTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:IdentityDb", "InMemory");
        builder.UseSetting("JwtSettings:Secret", "TEST-SECRET-KEY-FOR-INTEGRATION-TESTS-MIN-32-CHARS-LONG");
        builder.UseSetting("JwtSettings:Issuer", "OrderFlow.Identity.Tests");
        builder.UseSetting("JwtSettings:Audience", "OrderFlow.Tests");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppIdentityDbContext>();
            services.RemoveAll<DbContextOptions<AppIdentityDbContext>>();
            services.RemoveAll<DbContextOptions>();

            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });

        builder.UseEnvironment("Testing");
    }
}
