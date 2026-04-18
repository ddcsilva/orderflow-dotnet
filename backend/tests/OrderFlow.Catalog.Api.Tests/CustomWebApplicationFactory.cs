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
            // Remove todos os registros do DbContext e opções
            services.RemoveAll<CatalogDbContext>();
            services.RemoveAll<DbContextOptions<CatalogDbContext>>();

            // Remove health checks que dependem do SQL Server
            services.RemoveAll<Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck>();

            // Registra CatalogDbContext com InMemory usando seu próprio service provider
            // (evita conflito com serviços do provider SqlServer no contêiner DI)
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
