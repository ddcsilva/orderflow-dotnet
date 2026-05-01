using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OrderFlow.SharedKernel.Observability;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddOrderFlowOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];

        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: "1.0.0")
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", environment)
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                                              && !ctx.Request.Path.StartsWithSegments("/metrics");
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddSource(DiagnosticsConfig.ServiceName)
                    .AddSource("MassTransit")
                    .AddSource("Polly");

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(DiagnosticsConfig.ServiceName)
                    .AddMeter("Polly")
                    .AddPrometheusExporter();
            });

        return services;
    }
}
