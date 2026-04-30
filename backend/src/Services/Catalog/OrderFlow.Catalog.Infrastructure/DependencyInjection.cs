using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Application.Services;
using OrderFlow.Catalog.Domain.Interfaces;
using OrderFlow.Catalog.Infrastructure.Caching;
using OrderFlow.Catalog.Infrastructure.Data;
using OrderFlow.Catalog.Infrastructure.Data.Repositories;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Banco de Dados (EF Core)
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("CatalogDb"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                }));

        // Unidade de Trabalho
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CatalogDbContext>());

        // Repositórios
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // Serviços (com Decorator de Cache para IProductService)
        services.AddScoped<ProductService>();
        services.AddScoped<IProductService>(sp => new CachedProductService(
            sp.GetRequiredService<ProductService>(),
            sp.GetRequiredService<IDistributedCache>(),
            sp.GetRequiredService<ILogger<CachedProductService>>()));
        services.AddScoped<ICategoryService, CategoryService>();

        // Validadores
        services.AddValidatorsFromAssemblyContaining<Application.Validators.CreateProductValidator>();

        return services;
    }
}
