using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderFlow.Catalog.Infrastructure.Data;

namespace OrderFlow.Catalog.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "CatalogTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext and options registrations
            services.RemoveAll<CatalogDbContext>();
            services.RemoveAll<DbContextOptions<CatalogDbContext>>();

            // Remove health checks that depend on SQL Server
            services.RemoveAll<Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck>();

            // Register CatalogDbContext with InMemory provider using its own service provider
            // (avoids conflict with SqlServer provider services in the DI container)
            services.AddScoped(_ =>
            {
                var options = new DbContextOptionsBuilder<CatalogDbContext>()
                    .UseInMemoryDatabase(_dbName)
                    .Options;
                return new CatalogDbContext(options);
            });
        });

        builder.UseEnvironment("Testing");
    }
}
