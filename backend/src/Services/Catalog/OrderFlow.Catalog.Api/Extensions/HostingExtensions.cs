using System.Diagnostics.CodeAnalysis;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Catalog.Api.Middleware;
using OrderFlow.Catalog.Infrastructure;
using OrderFlow.Catalog.Infrastructure.Data;
using OrderFlow.SharedKernel.Observability;
using Serilog;

namespace OrderFlow.Catalog.Api.Extensions;

internal static class HostingExtensions
{
    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider",
        Justification = "Serilog gerencia formatação de strings internamente — falso positivo")]
    internal static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
    {
        builder.AddOrderFlowSerilog();
        return builder;
    }

    internal static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "OrderFlow Catalog API",
                Version = "v1",
                Description = "API para gerenciamento do catálogo de produtos e categorias."
            });
        });

        builder.Services.AddCatalogInfrastructure(builder.Configuration);
        builder.Services.AddTransient<GlobalExceptionHandler>();

        // Redis Distributed Cache
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
            ?? builder.Configuration["Redis:ConnectionString"]
            ?? "localhost:6379";

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "OrderFlow.Catalog:";
        });

        // OpenTelemetry
        builder.Services.AddOrderFlowOpenTelemetry(builder.Configuration, "OrderFlow.Catalog.Api");

        // Output Caching
        builder.Services.AddOutputCache(options =>
        {
            options.AddPolicy("Products", policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .Tag("products"));
        });

        builder.Services.AddHealthChecks()
            .AddSqlServer(
                builder.Configuration.GetConnectionString("CatalogDb")!,
                name: "sqlserver",
                tags: ["db", "ready", "startup"])
            .AddRedis(
                redisConnectionString,
                name: "redis",
                tags: ["cache", "ready"]);

        return builder;
    }

    internal static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<GlobalExceptionHandler>();
        app.UseCorrelationId();
        app.UseSerilogRequestLogging();
        app.UseOutputCache();

        app.MapControllers();
        app.MapPrometheusScrapingEndpoint();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return app;
    }

    internal static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
