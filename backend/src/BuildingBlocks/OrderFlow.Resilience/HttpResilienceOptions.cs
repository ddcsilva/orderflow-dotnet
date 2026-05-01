namespace OrderFlow.Resilience;

/// <summary>
/// Configuração externalizada (appsettings) de um pipeline HTTP.
/// Permite tunar retries/CB/timeouts por dependência sem recompilar.
/// </summary>
public sealed class HttpResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>Timeout total da operação, incluindo todos os retries (default 15s).</summary>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Timeout de cada tentativa individual (default 3s).</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Quantas requests simultâneas o bulkhead permite (default 100).</summary>
    public int ConcurrencyLimit { get; set; } = 100;

    /// <summary>Filas extras quando o limite de concorrência é atingido (default 0 — fail fast).</summary>
    public int ConcurrencyQueueLimit { get; set; }

    /// <summary>Número máximo de retries (default 3).</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Delay base do backoff exponencial (default 200ms → 200, 400, 800 + jitter).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Razão mínima de falhas para abrir o circuit breaker (default 0.5 = 50%).</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>Janela usada para amostragem do CB (default 10s).</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Mínimo de requisições na janela para o CB considerar abrir (default 8).</summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 8;

    /// <summary>Tempo que o CB fica Open antes de entrar em Half-Open (default 30s).</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Habilita injeção de chaos (latência + falhas). Use SOMENTE em Dev/Staging.</summary>
    public ChaosOptions Chaos { get; set; } = new();
}

public sealed class ChaosOptions
{
    /// <summary>Liga/desliga chaos.</summary>
    public bool Enabled { get; set; }

    /// <summary>Taxa de injeção de latência [0..1] (default 0.1 = 10%).</summary>
    public double LatencyInjectionRate { get; set; } = 0.1;

    /// <summary>Latência adicional injetada (default 5s).</summary>
    public TimeSpan LatencyValue { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Taxa de injeção de falhas [0..1] (default 0.05 = 5%).</summary>
    public double FaultInjectionRate { get; set; } = 0.05;
}
