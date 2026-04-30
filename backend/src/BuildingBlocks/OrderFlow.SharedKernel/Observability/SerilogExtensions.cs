using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Enrichers.Span;

namespace OrderFlow.SharedKernel.Observability;

public static class SerilogExtensions
{
    public static WebApplicationBuilder AddOrderFlowSerilog(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSerilog((services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .Enrich.With<ActivityEnricher>()
                .WriteTo.Console(
                    formatProvider: CultureInfo.InvariantCulture,
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.Seq(
                    serverUrl: builder.Configuration["Seq:Url"] ?? "http://localhost:5341",
                    formatProvider: CultureInfo.InvariantCulture);
        });

        return builder;
    }
}
