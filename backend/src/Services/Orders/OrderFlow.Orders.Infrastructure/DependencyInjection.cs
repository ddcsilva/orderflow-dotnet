using System.Data;
using MassTransit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Orders.Application.Common.Interfaces;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Infrastructure.Http;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Persistence.Repositories;
using OrderFlow.Resilience;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException("Connection string 'OrdersDb' not found.");

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(OrdersDbContext).Assembly.FullName);
                // NOTE: EnableRetryOnFailure desabilitado porque é incompatível
                // com transações iniciadas pelo TransactionBehavior + Outbox do MassTransit.
                // O Outbox já garante entrega confiável; resiliência de SQL transitório virá na Fase 9 (Polly).
            }));

        services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

        // MassTransit + RabbitMQ + EF Core Transactional Outbox
        services.AddMassTransit(cfg =>
        {
            cfg.AddEntityFrameworkOutbox<OrdersDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            cfg.AddConfigureEndpointsCallback((context, _, endpointCfg) =>
            {
                endpointCfg.UseEntityFrameworkOutbox<OrdersDbContext>(context);
            });

            cfg.UsingRabbitMq((context, rabbitCfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "localhost";
                var username = configuration["RabbitMQ:Username"] ?? "orderflow";
                var password = configuration["RabbitMQ:Password"] ?? "orderflow123";
                var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";

                rabbitCfg.Host(host, virtualHost, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                rabbitCfg.ConfigureEndpoints(context);
            });
        });

        // Catalog HTTP client com pipeline Polly v8 (retry + CB + timeout + bulkhead)
        services.AddOrderFlowHttpPipeline(
            ResiliencePipelineKeys.CatalogClient,
            "Resilience:CatalogClient");

        services.AddHttpClient<ICatalogClient, CatalogHttpClient>(client =>
        {
            var baseUrl = configuration["Services:Catalog:BaseUrl"] ?? "http://localhost:5001";
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
