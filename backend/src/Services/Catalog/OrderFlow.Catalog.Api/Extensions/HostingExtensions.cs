using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Catalog.Api.Middleware;
using OrderFlow.Catalog.Infrastructure;
using OrderFlow.Catalog.Infrastructure.Data;
using Serilog;

namespace OrderFlow.Catalog.Api.Extensions;

internal static class HostingExtensions
{
    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider",
        Justification = "Serilog gerencia formatação de strings internamente — falso positivo")]
    internal static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.Seq(builder.Configuration["Seq:Url"] ?? "http://localhost:5341");
        });

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

        builder.Services.AddHealthChecks()
            .AddSqlServer(
                builder.Configuration.GetConnectionString("CatalogDb")!,
                name: "sqlserver",
                tags: ["db", "ready"]);

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
        app.UseSerilogRequestLogging();

        app.MapControllers();

        app.MapHealthChecks("/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapHealthChecks("/health/live", new()
        {
            Predicate = _ => false
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
