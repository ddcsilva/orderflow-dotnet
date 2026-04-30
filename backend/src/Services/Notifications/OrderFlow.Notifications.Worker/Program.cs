using System.Globalization;
using MassTransit;
using OrderFlow.Notifications.Worker.Idempotency;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

builder.Services.AddMassTransit(cfg =>
{
    cfg.SetKebabCaseEndpointNameFormatter();
    cfg.AddConsumers(typeof(Program).Assembly);

    cfg.UsingRabbitMq((context, rabbitCfg) =>
    {
        var host = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var username = builder.Configuration["RabbitMQ:Username"] ?? "orderflow";
        var password = builder.Configuration["RabbitMQ:Password"] ?? "orderflow123";
        var virtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

        rabbitCfg.Host(host, virtualHost, h =>
        {
            h.Username(username);
            h.Password(password);
        });

        rabbitCfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);

public partial class Program;
