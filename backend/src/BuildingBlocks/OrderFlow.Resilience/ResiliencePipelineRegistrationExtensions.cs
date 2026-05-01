using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Simmy;
using Polly.Simmy.Fault;
using Polly.Simmy.Latency;
using Polly.Timeout;

namespace OrderFlow.Resilience;

/// <summary>
/// Registra pipelines de resiliência nomeadas no <see cref="IServiceCollection"/>.
/// Use <see cref="ResiliencePipelineKeys"/> para resgatar via
/// <c>ResiliencePipelineProvider&lt;string&gt;.GetPipeline&lt;HttpResponseMessage&gt;(key)</c>.
/// </summary>
public static class ResiliencePipelineRegistrationExtensions
{
    /// <summary>
    /// Registra a pipeline padrão para um cliente HTTP downstream com a ordem canônica:
    /// Outer Timeout → Bulkhead (RateLimiter) → Retry → Circuit Breaker → Per-attempt Timeout
    /// → (opcional) Chaos Fault/Latency.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="pipelineKey">Chave única (ver <see cref="ResiliencePipelineKeys"/>).</param>
    /// <param name="configurationSectionPath">Caminho de configuração (ex: "Resilience:CatalogClient").</param>
    public static IServiceCollection AddOrderFlowHttpPipeline(
        this IServiceCollection services,
        string pipelineKey,
        string configurationSectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationSectionPath);

        services.AddOptions<HttpResilienceOptions>(pipelineKey)
            .BindConfiguration(configurationSectionPath)
            .ValidateOnStart();

        services.AddResiliencePipeline<string, HttpResponseMessage>(pipelineKey, (builder, ctx) =>
        {
            var monitor = ctx.ServiceProvider.GetRequiredService<IOptionsMonitor<HttpResilienceOptions>>();
            var opts = monitor.Get(pipelineKey);

            BuildHttpPipeline(builder, opts);
        });

        return services;
    }

    /// <summary>Construção pura (sem DI) — útil para testes unitários.</summary>
    public static void BuildHttpPipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        HttpResilienceOptions opts)
    {
        // 1. Outer timeout — orçamento total da operação
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = opts.TotalRequestTimeout,
            Name = "outer-timeout",
        });

        // 2. Bulkhead — limita concorrência por dependência
        builder.AddRateLimiter(new RateLimiterStrategyOptions
        {
            DefaultRateLimiterOptions = new System.Threading.RateLimiting.ConcurrencyLimiterOptions
            {
                PermitLimit = opts.ConcurrencyLimit,
                QueueLimit = opts.ConcurrencyQueueLimit,
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
            },
            Name = "bulkhead",
        });

        // 3. Retry — apenas falhas transientes, exponencial com jitter.
        //    Pulado quando MaxRetryAttempts <= 0 (útil para isolar outras camadas em testes).
        if (opts.MaxRetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = args => TransientHttpPredicates.IsTransient(args.Outcome),
                MaxRetryAttempts = opts.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = opts.RetryBaseDelay,
                Name = "retry",
            });
        }

        // 4. Circuit Breaker — protege downstream em falhas sistêmicas
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = args => TransientHttpPredicates.IsTransient(args.Outcome),
            FailureRatio = opts.CircuitBreakerFailureRatio,
            SamplingDuration = opts.CircuitBreakerSamplingDuration,
            MinimumThroughput = opts.CircuitBreakerMinimumThroughput,
            BreakDuration = opts.CircuitBreakerBreakDuration,
            Name = "circuit-breaker",
        });

        // 5. Per-attempt timeout — fail-fast por tentativa
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = opts.AttemptTimeout,
            Name = "attempt-timeout",
        });

        // 6. Chaos (opcional) — injeção de falhas para validar a resiliência
        //    Sempre adicionado por ÚLTIMO para que as estratégias acima reajam ao caos.
        if (opts.Chaos.Enabled)
        {
            builder.AddChaosFault(new ChaosFaultStrategyOptions
            {
                InjectionRate = opts.Chaos.FaultInjectionRate,
                FaultGenerator = new FaultGenerator()
                    .AddException(() => new HttpRequestException("chaos: injected fault"))
                    .AddException(() => new TimeoutException("chaos: injected timeout")),
                Name = "chaos-fault",
            });

            builder.AddChaosLatency(new ChaosLatencyStrategyOptions
            {
                InjectionRate = opts.Chaos.LatencyInjectionRate,
                Latency = opts.Chaos.LatencyValue,
                Name = "chaos-latency",
            });
        }
    }
}
