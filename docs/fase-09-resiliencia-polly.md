# Fase 09 — Resiliência com Polly v8

> **Trilha:** Sênior | **Pré-requisitos:** Fases 01-08
> **Objetivo:** Tornar o Orders API resiliente a falhas transientes com **pipelines Polly v8**: retry exponencial com jitter, circuit breaker, bulkhead, timeout, hedging e fallback. Validar com chaos engineering (Simmy).

### 🎯 O que você vai aprender

- Por que **resiliência ≠ retry** — e o que significa de verdade
- A nova API Polly v8: **`ResiliencePipelineBuilder`** e **`ResiliencePipeline`**
- Os 6 padrões oficiais: **Retry, Circuit Breaker, Bulkhead, Timeout, Hedging, Fallback**
- Como **ordenar** estratégias num pipeline (ordem importa!)
- **Chaos Engineering** com Simmy — injetar falhas para validar a resiliência
- Telemetria de pipelines com `ResiliencePipelineRegistry` + OpenTelemetry
- Anti-padrões críticos: retry storms, retry sem timeout, CB sem isolamento

---

## Sumário

1. [Por Que Esta Fase Existe](#1-por-que-esta-fase-existe)
2. [Polly v7 → v8: O Que Mudou](#2-polly-v7--v8-o-que-mudou)
3. [Os 6 Padrões de Resiliência](#3-os-6-padrões-de-resiliência)
4. [Implementação no OrderFlow](#4-implementação-no-orderflow)
5. [Ordem Correta no Pipeline](#5-ordem-correta-no-pipeline)
6. [Chaos Engineering com Simmy](#6-chaos-engineering-com-simmy)
7. [Telemetria e Observabilidade](#7-telemetria-e-observabilidade)
8. [Anti-Padrões](#8-anti-padrões)
9. [💼 Perguntas de Entrevista](#9--perguntas-de-entrevista)

---

## 1. Por Que Esta Fase Existe

> **🤔 Pergunta Socrática:** *"O que acontece com o Orders API quando o Catalog API responde 503 por 30 segundos durante um deploy?"*

Sem resiliência: cada request do Orders trava aguardando timeout do HttpClient (~100s default), threads se acumulam, eventualmente o Orders cai por **resource exhaustion**. Uma falha de **1 serviço** vira **outage do sistema inteiro** — o famoso *cascading failure*.

Com Polly v8 corretamente configurado:
- Timeout de 3s **fail-fast** → libera thread imediatamente
- Retry exponencial com jitter (3 tentativas, 200ms→400ms→800ms)
- Circuit Breaker detecta falha sistêmica e **para de tentar** por 30s
- Bulkhead limita concorrência de chamadas ao Catalog em 50 paralelas
- Fallback: usa cache stale do produto, marca pedido como `PendingValidation`

Resultado: o Orders **degrada graciosamente** em vez de cair.

---

> 🤔 **Pense antes de ler:**
> 1. Se uma API externa demora 30s para responder, o que acontece com as 500 requests que estão esperando? E com o pool de threads?
> 2. Retry resolve **tudo**? Quando retry *piora* a situação? (Dica: pense no serviço destino sobrecarregado.)
> 3. Circuit Breaker Open → Half-Open → Closed: por que existe o estado **Half-Open** em vez de ir direto para Closed?

## 2. Polly v7 → v8: O Que Mudou

A API mudou completamente. Se você ainda usa `Policy.Handle<>().Retry(...)`, está em v7 (modo manutenção).

| v7 (legado) | v8 (atual) |
|---|---|
| `Policy.Handle<...>().RetryAsync(3)` | `new ResiliencePipelineBuilder().AddRetry(new RetryStrategyOptions { ... })` |
| `IAsyncPolicy<T>` | `ResiliencePipeline<T>` |
| Sync e Async **separados** | **Unificado** — uma pipeline serve ambos |
| Wrap manual: `Policy.WrapAsync(p1, p2)` | Encadeamento natural via fluent builder |
| `PolicyKey` para telemetria | Telemetria nativa via `ResiliencePipelineRegistry` |

```csharp
// ❌ v7 — não use mais
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// ✅ v8 — moderno
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(200)
    })
    .Build();
```

---

## 3. Os 6 Padrões de Resiliência

### 3.1 Retry — "Tente de novo, talvez foi pontual"

**Quando:** falhas **transientes** (timeout, 503, deadlock SQL, conexão derrubada).
**Quando NÃO:** falhas **determinísticas** (400 Bad Request, 401 Unauthorized, business validation).

```csharp
.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
{
    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
        .Handle<HttpRequestException>()
        .HandleResult(r => r.StatusCode is HttpStatusCode.ServiceUnavailable
                                       or HttpStatusCode.GatewayTimeout
                                       or HttpStatusCode.RequestTimeout),
    MaxRetryAttempts = 3,
    BackoffType = DelayBackoffType.Exponential,
    UseJitter = true,                      // 🔑 evita thundering herd
    Delay = TimeSpan.FromMilliseconds(200) // base — vira 200, 400, 800 + jitter
})
```

> **🔑 `UseJitter = true`** — sem isso, todos os clientes que falharam ao mesmo tempo retentam ao mesmo tempo. Resultado: **retry storm** que mata o serviço que estava se recuperando.

### 3.2 Circuit Breaker — "Para de tentar, deixa o serviço respirar"

Estados: **Closed** (normal) → **Open** (rejeita tudo) → **Half-Open** (testa).

```csharp
.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
{
    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
        .Handle<HttpRequestException>()
        .HandleResult(r => (int)r.StatusCode >= 500),
    FailureRatio = 0.5,                       // 50% das chamadas falhas
    SamplingDuration = TimeSpan.FromSeconds(10),
    MinimumThroughput = 8,                    // mínimo 8 chamadas para considerar
    BreakDuration = TimeSpan.FromSeconds(30), // tempo aberto antes de tentar half-open
    OnOpened = static args =>
    {
        Activity.Current?.SetTag("circuit.state", "open");
        return default;
    }
})
```

**Regra de ouro:** `BreakDuration` ≥ tempo típico de recuperação do serviço a jusante. Se for 5s e o downstream demora 30s, o CB abre/fecha em loop.

### 3.3 Timeout — "Fail fast, libera a thread"

```csharp
.AddTimeout(new TimeoutStrategyOptions
{
    Timeout = TimeSpan.FromSeconds(3),
    OnTimeout = static args =>
    {
        // log estruturado com TraceId
        return default;
    }
})
```

> **Anti-padrão:** retry **sem** timeout. Se cada tentativa demora 100s e você retenta 3x, são 5 minutos com a thread bloqueada. Sempre componha **Timeout dentro de Retry**.

### 3.4 Bulkhead — "Isole recursos por dependência"

Em Polly v8, bulkhead se chama **Rate Limiter** (`AddRateLimiter`) ou **Concurrency Limiter** via `System.Threading.RateLimiting`:

```csharp
.AddConcurrencyLimiter(
    permitLimit: 50,    // 50 chamadas paralelas ao Catalog
    queueLimit: 100)    // mais 100 esperando
```

**Cenário:** sem bulkhead, se o Catalog ficar lento, 1000 requests do Orders abrem 1000 conexões. O pool de conexões do Orders esgota — outros endpoints (que nem usam Catalog) param de responder.

### 3.5 Hedging — "Quem chegar primeiro, ganha"

Dispara N tentativas em paralelo (escalonadas) e retorna a primeira resposta bem-sucedida. Nova em Polly v8.

```csharp
.AddHedging(new HedgingStrategyOptions<HttpResponseMessage>
{
    MaxHedgedAttempts = 2,
    Delay = TimeSpan.FromMilliseconds(500), // dispara 2ª tentativa após 500ms
    ActionGenerator = args => async () =>
    {
        // pode rotear para outra réplica/região
        return await args.ActionContext.CallNextAsync();
    }
})
```

**Quando usar:** SLAs apertados onde latência da cauda (P99) importa muito. Trade-off: mais carga no downstream.

### 3.6 Fallback — "Plano B"

```csharp
.AddFallback(new FallbackStrategyOptions<Product?>
{
    ShouldHandle = new PredicateBuilder<Product?>().Handle<Exception>(),
    FallbackAction = async args =>
    {
        // tenta cache stale; se nada, retorna null sinalizando "indisponível"
        var stale = await _cache.GetStaleAsync<Product>(args.Context.OperationKey);
        return Outcome.FromResult(stale);
    }
})
```

---

## 4. Implementação no OrderFlow

### 4.1 Pipeline Reutilizável (BuildingBlocks)

Crie `src/BuildingBlocks/OrderFlow.Resilience/HttpResiliencePipelines.cs`:

```csharp
namespace OrderFlow.Resilience;

public static class HttpResiliencePipelines
{
    public const string CatalogClient = "catalog-client";
    public const string PaymentClient = "payment-client";

    public static IServiceCollection AddOrderFlowResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline<string, HttpResponseMessage>(CatalogClient, builder =>
        {
            builder
                .AddTimeout(TimeSpan.FromSeconds(10))           // 1️⃣ outer timeout (total)
                .AddConcurrencyLimiter(permitLimit: 100)        // 2️⃣ bulkhead
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = TransientHttpPredicate,
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(200)
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = TransientHttpPredicate,
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    MinimumThroughput = 8,
                    BreakDuration = TimeSpan.FromSeconds(30)
                })
                .AddTimeout(TimeSpan.FromSeconds(3));           // 5️⃣ per-attempt timeout
        });

        return services;
    }

    private static PredicateBuilder<HttpResponseMessage> TransientHttpPredicate =>
        new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>()
            .HandleResult(r => (int)r.StatusCode >= 500
                            || r.StatusCode is HttpStatusCode.RequestTimeout);
}
```

### 4.2 Uso no `HttpClient` Tipado

```csharp
// Program.cs do Orders.Api
builder.Services.AddOrderFlowResilience();

builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog:Url"]!);
});

// Consumindo a pipeline
public sealed class CatalogClient(
    HttpClient http,
    ResiliencePipelineProvider<string> pipelineProvider) : ICatalogClient
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline =
        pipelineProvider.GetPipeline<HttpResponseMessage>(HttpResiliencePipelines.CatalogClient);

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var response = await _pipeline.ExecuteAsync(
            async token => await http.GetAsync($"/api/products/{id}", token),
            ct);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Product>(ct)
            : null;
    }
}
```

### 4.3 Atalho com `Microsoft.Extensions.Http.Resilience`

A Microsoft empacotou Polly em uma extensão idiomática:

```csharp
builder.Services
    .AddHttpClient<ICatalogClient, CatalogClient>()
    .AddStandardResilienceHandler();   // ← retry + CB + timeout + bulkhead com defaults sensatos
```

> **Quando usar:** prototipagem ou serviços com necessidades padrão. Para controle fino (Catalog vs Payment com configs diferentes), use a pipeline manual.

---

## 5. Ordem Correta no Pipeline

A **ordem é semântica**, não cosmética. Estratégia adicionada **primeiro** envolve as seguintes:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Outer Timeout (15s) ── fail-safe global                  │
│   ┌─────────────────────────────────────────────────────┐   │
│   │ 2. Bulkhead (50 paralelas) ── isolamento             │   │
│   │   ┌─────────────────────────────────────────────┐   │   │
│   │   │ 3. Retry (3x exp+jitter) ── transientes      │   │   │
│   │   │   ┌────────────────────────────────────┐    │   │   │
│   │   │   │ 4. Circuit Breaker ── fail-fast    │    │   │   │
│   │   │   │   ┌──────────────────────────┐    │    │   │   │
│   │   │   │   │ 5. Per-attempt Timeout (3s)│   │    │   │   │
│   │   │   │   │   ┌────────────────┐     │    │    │   │   │
│   │   │   │   │   │  HttpClient    │     │    │    │   │   │
│   │   │   │   │   └────────────────┘     │    │    │   │   │
│   │   │   │   └──────────────────────────┘    │    │   │   │
│   │   │   └────────────────────────────────────┘    │   │   │
│   │   └─────────────────────────────────────────────┘   │   │
│   └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

**Por que esta ordem?**

| Posição | Razão |
|---|---|
| Timeout outer **primeiro** | Garante budget total (3 retries × 3s + jitter = ~12s; outer 15s) |
| Bulkhead **antes do retry** | Conta concorrência de **operações lógicas**, não de tentativas |
| Retry **antes do CB** | CB conta cada tentativa como falha — protege o downstream |
| CB **antes do timeout per-attempt** | CB não dispara em timeout do CB; usa timeout da operação |

> **Erro comum:** Retry **dentro** do CB faz o CB abrir mais devagar e o downstream sofrer mais. **Sempre** Retry → CB.

---

## 6. Chaos Engineering com Simmy

Simmy é a extensão de chaos do Polly. Injeta falhas em **runtime** para validar que sua resiliência funciona.

```csharp
// Apenas em ambientes não-produtivos
if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
{
    builder.Services.AddResiliencePipeline<string, HttpResponseMessage>("chaos", b =>
    {
        b.AddChaosLatency(new ChaosLatencyStrategyOptions
        {
            InjectionRate = 0.1,                        // 10% das chamadas
            Latency = TimeSpan.FromSeconds(5)
        })
        .AddChaosFault(new ChaosFaultStrategyOptions
        {
            InjectionRate = 0.05,                       // 5% das chamadas
            FaultGenerator = static args => new ValueTask<Outcome<Exception>?>(
                Outcome.FromResult<Exception>(new HttpRequestException("Injected chaos")))
        });
    });
}
```

**Práticas:**
- Rode na **staging** com tráfego sintético para validar dashboards e alertas
- Use **GameDays** mensais — time inteiro responde a falha injetada
- Aumente injection rate gradualmente (1% → 5% → 10%)

---

## 7. Telemetria e Observabilidade

Polly v8 emite **métricas e logs nativamente**:

```csharp
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("Polly"))           // métricas de retry/CB/etc.
    .WithTracing(t => t.AddSource("Polly"));          // spans de cada estratégia
```

Métricas chave para alertar no Grafana:

| Métrica | O que monitorar |
|---|---|
| `polly.strategy.events` | Contagem de retries, CB opens — pico = problema downstream |
| `polly.strategy.execution.duration` | P99 da latência efetiva — degradação |
| `polly.circuit_breaker.state` | Estado do CB ao longo do tempo |
| `polly.retry.attempts` | Distribuição de tentativas — alta = downstream ruim |

**Alertas sugeridos:**
- CB aberto por > 1 minuto
- Taxa de retry > 30% por 5 minutos
- Bulkhead rejection > 1% das requests

---

## 8. Anti-Padrões

### ❌ Retry sem timeout
```csharp
// 3 retries × 100s default = 5 minutos travando uma thread
.AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 })
```
✅ **Sempre** componha com `AddTimeout` per-attempt.

### ❌ Retry de erros 4xx
```csharp
// 401, 403, 404, 422 NÃO se resolvem com retry
.HandleResult(r => !r.IsSuccessStatusCode)
```
✅ Apenas **5xx + timeouts + connection errors**.

### ❌ Retry storm sem jitter
```csharp
// Sem jitter, 1000 clientes retentam ao mesmo segundo
new RetryStrategyOptions { UseJitter = false }
```
✅ `UseJitter = true` **sempre**.

### ❌ Circuit Breaker sem MinimumThroughput
```csharp
// Abre na primeira falha; em baixo tráfego = falsos positivos
new CircuitBreakerStrategyOptions { FailureRatio = 0.5 }  // sem MinimumThroughput
```
✅ Defina `MinimumThroughput` ≥ 8.

### ❌ Pipeline global compartilhado entre dependências
```csharp
// Catalog lento abre o CB que afeta também chamadas a Payment
services.AddResiliencePipeline<string, HttpResponseMessage>("global", ...);
```
✅ Uma pipeline **por dependência** — cada uma tem seu CB.

---

## ⚠️ Erros Comuns em Resiliência

| # | Erro | Consequência | Solução |
|---|---|---|---|
| 1 | **Retry sem jitter** | 100 clients fazem retry ao mesmo tempo → thundering herd | `UseJitter = true` ou `BackoffType = ExponentialWithJitter` |
| 2 | **Circuit Breaker com threshold muito alto** | CB só abre após 1000 falhas — dano já feito | Configure `FailureRatio` razoável (0.5) com `MinimumThroughput` (20) |
| 3 | **Retry em erros não-transientes** | 404 Not Found não vai virar 200 com retry — desperdício | Filtre: `ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(ex => ex.StatusCode >= 500)` |
| 4 | **Timeout sem cancelamento** | Timeout expira mas operação continua rodando no background | Propague `CancellationToken` do Polly. Use `TimeoutStrategy.Optimistic` |
| 5 | **Uma pipeline para todas as dependências** | CB do serviço A abre e bloqueia chamadas ao serviço B | Pipeline separada por dependência (keyed DI) |

---

## 🔧 Troubleshooting — Fase 09

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| Circuit Breaker nunca abre | `MinimumThroughput` maior que o tráfego real | Reduza para valor compatível com seu volume |
| "The circuit breaker is open" em todos os endpoints | Pipeline compartilhada entre dependências | Use pipeline separada (por named HttpClient) |
| Retry loop infinito | `MaxRetryAttempts = int.MaxValue` ou sem `MaxRetryAttempts` | Defina limite explícito (3-5 retries) |
| Timeout não funciona | `TimeoutStrategy.Pessimistic` mas operação não respeita token | Use `Optimistic` + propague `CancellationToken` |

---

## 9. 💼 Perguntas de Entrevista

**1. "Diferencie Retry e Circuit Breaker. Como combiná-los?"**
— Retry tenta de novo (assume falha transiente). CB **bloqueia** chamadas após N falhas (assume falha sistêmica). Combinação correta: **Retry dentro do CB** — o CB conta cada tentativa, abre se passar do limite. No Polly v8: `.AddRetry(...).AddCircuitBreaker(...)`.

**2. "Por que Bulkhead é importante em microserviços?"**
— Sem bulkhead, falha em **1 dependência** consome todo o pool de threads/conexões. Endpoints que nem usam essa dependência ficam indisponíveis. Bulkhead **isola cotas** por dependência — cascading failure não acontece.

**3. "O que é retry storm e como evitar?"**
— Quando muitos clientes retentam **simultaneamente** após uma falha — sobrecarrega o serviço que estava se recuperando. Mitigação: **jitter aleatório** no delay de retry. Em Polly v8: `UseJitter = true`.

**4. "Quando usar Hedging?"**
— SLAs apertados onde **latência da cauda (P99)** importa. Disparar 2-3 tentativas em paralelo escalonadas; primeira a responder vence. Trade-off: 2-3x mais carga no downstream — só vale para chamadas críticas.

**5. "Como você testaria que sua resiliência funciona?"**
— **Chaos Engineering**: usar Simmy para injetar latência (10% das chamadas +5s) e falhas (5% retorna 503). Rodar em staging com tráfego sintético, validar dashboards e alertas. **GameDays** mensais com o time respondendo a falhas reais injetadas.

**6. "Qual a ordem correta de estratégias num pipeline e por quê?"**
— `Timeout outer → Bulkhead → Retry → Circuit Breaker → Timeout per-attempt`. Ordem reflete: budget total > isolamento de recursos > resiliência transiente > proteção sistêmica > liberar thread rápido. Adicionada primeiro envolve as seguintes.

**7. "Você usaria `AddStandardResilienceHandler` em produção?"**
— Sim para serviços com SLAs padrão e dependências com comportamento típico. **Mas** quando você tem dependências com perfis muito diferentes (Payment crítico vs CMS opcional), pipeline manual permite tunar `MaxAttempts`, `Delay`, `BreakDuration` por dependência. Pragmatismo: comece com `Standard`, evolua por necessidade.

---

## Checkpoint

✅ Pipeline `catalog-client` configurada com retry + CB + bulkhead + timeout
✅ `Microsoft.Extensions.Http.Resilience` testado em ao menos 1 client
✅ Simmy injetando 5% de falhas em desenvolvimento
✅ Métricas Polly aparecem no Grafana
✅ CB abre quando você derruba o Catalog (`docker stop catalog-api`)
✅ Pedido continua sendo criado via fallback (cache stale ou status pendente)

➡️ **Próxima fase:** [`fase-10-performance-csharp-moderno.md`](./fase-10-performance-csharp-moderno.md) — Span\<T\>, ValueTask, Channels, BenchmarkDotNet, AOT.
