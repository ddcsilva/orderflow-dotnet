# Fase 6 — Cache e Observabilidade

> **Objetivo:** Implementar cache distribuído com Redis, Output Caching, OpenTelemetry (traces + metrics), Serilog com enrichers e Health Checks completos.

> **Pré-requisito:** Fase 5 concluída (mensageria funcionando com RabbitMQ).

### 🎯 O que você vai aprender nesta fase

- Implementar **cache distribuído** com Redis (Cache-Aside pattern)
- Configurar **Output Caching** no ASP.NET Core para responses HTTP
- Instrumentar a aplicação com **OpenTelemetry** (traces + metrics)
- Configurar **Serilog** com enrichers e sinks (Seq, Console)
- Criar **Health Checks** (liveness + readiness) para infraestrutura
- Visualizar métricas no **Grafana** e logs no **Seq**

---

## Sumário

1. [Visão da Fase](#1-visão-da-fase)
2. [Decisões Arquiteturais](#2-decisões-arquiteturais)
3. [Conceitos](#3-conceitos)
4. [Passo a Passo de Implementação](#4-passo-a-passo-de-implementação)
5. [Código de Referência Completo](#5-código-de-referência-completo)
6. [Testes](#6-testes)
7. [Checkpoint](#7-checkpoint)

---

## 1. Visão da Fase

### O Que Vamos Construir

```
src/Services/Catalog/OrderFlow.Catalog.Infrastructure/
├── Caching/
│   └── CachedProductService.cs       ← Decorator com cache-aside

src/BuildingBlocks/OrderFlow.SharedKernel/
├── Observability/
│   ├── DiagnosticsConfig.cs           ← ActivitySource e Meters
│   └── OpenTelemetryExtensions.cs     ← Configuração centralizada

src/Services/Orders/OrderFlow.Orders.Api/
├── appsettings.json                    ← Health checks, OTLP config

docker-compose.yml
├── redis
├── seq (já existente)
├── grafana
├── otel-collector (opcional)
└── prometheus (opcional)
```

### Tópicos Cobertos

| Tópico | Detalhe |
|--------|---------|
| **Redis** | IDistributedCache, StackExchange.Redis |
| **Cache-Aside Pattern** | Decorator que verifica cache antes do banco |
| **Output Caching** | Cache de resposta HTTP diretamente |
| **Cache Invalidation** | Estratégias de expiração e invalidação |
| **OpenTelemetry** | Traces distribuídos, métricas, exporters |
| **Serilog Enrichers** | CorrelationId, UserId, MachineName |
| **Structured Logging** | Logs estruturados com Seq |
| **Health Checks** | Liveness, readiness, startup para cada dependência |
| **Compiled Queries** | EF Core queries pré-compiladas |

---

## 2. Decisões Arquiteturais

> 🤔 **Pense antes de ler:**
> 1. Cache de **output** (resposta HTTP inteira) vs cache de **dados** (objetos no Redis) — quando usar cada um?
> 2. Se duas instâncias da mesma API estão rodando, por que `IMemoryCache` (in-process) não funciona para ambas?
> 3. Como saber se uma request está lenta por causa do banco, do serviço externo, ou do próprio código? (Dica: distributed tracing.)
>
> Cache sem observabilidade é otimização às cegas. Observabilidade sem cache é diagnóstico sem cura. Juntos, são o kit de sobrevivência em produção.

### ADR-014: Redis como Cache Distribuído

> 🧠 **Analogia — A Agenda de Contatos no Celular:** Quando você precisa ligar pra alguém, não abre a lista telefônica da cidade toda vez (ir ao banco de dados). Você olha seus **contatos salvos** (cache). Se o número está lá, liga direto (cache HIT, < 1ms). Se não, pesquisa na lista, encontra, salva no celular e liga (cache MISS → consulta banco → salva no cache). Redis é essa agenda: rápido (in-memory), compartilhado entre dispositivos (distribuído) e com espaço limitado (eviction policy expulsa os contatos menos usados).

**Contexto:** Precisamos de cache compartilhado entre instâncias da mesma API (quando escalar horizontalmente).

**Decisão:** Redis via `IDistributedCache` com cache-aside pattern no Catalog API.

```
Request → CachedProductService
             │
             ├── Cache HIT  → return from Redis (< 1ms)
             │
             └── Cache MISS → ProductService → DB → set Redis → return
```

### ADR-015: Decorator Pattern para Cache

> 🧠 **Analogia — A Capa Protetora do Celular:** Seu celular (ProductService) funciona perfeitamente sozinho. A capa (CachedProductService) não muda o celular — ela **envolve** ele adicionando uma funcionalidade extra (proteção/cache). Você pode trocar a capa sem abrir o celular, ou usar o celular sem capa. **Esse é o Decorator**: uma camada que adiciona comportamento sem modificar o original. O `ProductService` nem sabe que está sendo cacheado.

**Decisão:** Usar o padrão Decorator ao invés de poluir o ProductService com lógica de cache.

```csharp
// Sem Decorator (POLUÍDO) ❌
public class ProductService
{
    public async Task<Product> GetById(Guid id)
    {
        var cached = await _cache.Get(id);   // Cache no service!
        if (cached != null) return cached;
        var product = await _repo.GetById(id);
        await _cache.Set(id, product);        // Cache no service!
        return product;
    }
}

// Com Decorator (LIMPO) ✅
public class CachedProductService : IProductService  // Mesmo contrato
{
    private readonly IProductService _inner;          // Decora o original
    private readonly IDistributedCache _cache;

    public async Task<Product> GetById(Guid id)
    {
        return await _cache.GetOrCreate(key, () => _inner.GetById(id));
    }
}
```

### ADR-016: OpenTelemetry para Observabilidade

**Decisão:** OpenTelemetry como padrão de observabilidade, exportando para Seq (logs), Prometheus/Grafana (metrics) e traces.

```
┌─────────────┐    ┌──────────────┐    ┌───────────┐
│  Orders API │    │ Identity API │    │  Worker   │
│  (traces,   │    │  (traces,    │    │ (traces,  │
│   metrics,  │    │   metrics,   │    │  metrics, │
│   logs)     │    │   logs)      │    │  logs)    │
└──────┬──────┘    └──────┬───────┘    └─────┬─────┘
       │                  │                  │
       └──────────────────┼──────────────────┘
                          │
                ┌─────────▼──────────┐
                │  OTLP Collector    │
                │  (ou direto)       │
                └────┬────────┬──────┘
                     │        │
              ┌──────▼──┐  ┌──▼────────┐
              │   Seq   │  │ Prometheus│
              │ (logs)  │  │ (metrics) │
              └─────────┘  └─────┬─────┘
                                 │
                          ┌──────▼──────┐
                          │  Grafana    │
                          │ (dashboards)│
                          └─────────────┘
```

---

## 3. Conceitos

### Cache-Aside Pattern

> 💡 **Uma regra de ouro do cache:** *"Há apenas duas coisas difíceis em ciência da computação: invalidação de cache e dar nome às coisas."* — Phil Karlton. O cache é simples de implementar e **muito** fácil de errar. Dados desatualizados, inconsistências entre instâncias, cache stampede... por isso adotamos estratégias claras de expiração e invalidação.

```
1. Client → GET /api/products/123
2. CachedProductService:
   a. Redis.Get("product:123")
      → HIT?  return cached data
      → MISS? continue
   b. ProductService.GetById(123) → SQL Server
   c. Redis.Set("product:123", data, TTL=5min)
   d. return data

Cache Invalidation:
  - Time-based: TTL de 5 minutos (cache se auto-expira)
  - Event-based: Quando produto é atualizado, remove chave do cache
  - Write-through: Update no banco + delete na chave Redis na mesma operação

Estratégia recomendada:
  - Leitura: Cache-Aside com TTL (auto-healing se missão de invalidação)
  - Escrita: Delete key + TTL como safety net (nunca SET direto no update)
  - Listagens: Output Cache com EvictByTag no POST/PUT/DELETE
```

### Distributed Tracing

> 🧠 **Analogia — O Rastreio do Pacote:** Quando você compra algo online, recebe um **código de rastreamento**. Com ele, você vê: "15:00 saiu do CD → 16:30 chegou na filial → 18:00 saiu pra entrega → 19:00 entregue". Cada etapa é um **Span** (operação individual), e o código de rastreamento é o **TraceId**. Distributed Tracing é isso: um TraceId que acompanha a requisição do browser até o último consumer, mostrando exatamente onde cada milissegundo foi gasto.

```
[Trace: abc-123]
  ├── [Span: HTTP POST /api/orders] (Orders API, 45ms)
  │     ├── [Span: MediatR CreateOrderCommand] (12ms)
  │     ├── [Span: EF Core SaveChanges] (8ms)
  │     └── [Span: MassTransit Publish] (5ms)
  │
  └── [Span: RabbitMQ Consumer] (Notification Worker, 15ms)
        └── [Span: Send Email] (10ms)

O TraceId (abc-123) é propagado automaticamente entre serviços via headers HTTP e message headers.
```

### Serilog Structured Logging

> 🧠 **Analogia — Ficha Médica vs Anotação em Guardanapo:** Log com string interpolation é escrever sintomas num guardanapo — você lê, mas não consegue filtrar "todos os pacientes com febre na ala B". Log estruturado é preencher uma ficha padronizada: cada campo (OrderId, UserId, Duration) é um campo pesquisável no Seq. Quando o sistema der problema às 3h da manhã e você precisar filtrar "todas as requisições do usuário X nos últimos 30 minutos", a ficha salva sua noite.

```csharp
// ❌ String interpolation — não estruturado
logger.LogInformation($"Order {orderId} confirmed at {DateTime.UtcNow}");
// Saída: "Order abc-123 confirmed at 2026-01-01T00:00:00Z"
// → Não é pesquisável por orderId no Seq!

// ✅ Structured logging — pesquisável
logger.LogInformation("Order {OrderId} confirmed at {ConfirmedAt}", orderId, DateTime.UtcNow);
// Seq indexa OrderId e ConfirmedAt como campos pesquisáveis
```

---

## 4. Passo a Passo de Implementação

### 4.1 Docker Compose — Redis e Grafana

Adicione ao `docker-compose.yml`:

```yaml
  redis:
    image: redis:7-alpine
    container_name: orderflow-redis
    ports:
      - "6379:6379"
    command: redis-server --maxmemory 128mb --maxmemory-policy allkeys-lru
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5
    volumes:
      - redis_data:/data

  grafana:
    image: grafana/grafana:latest
    container_name: orderflow-grafana
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_USER: admin
      GF_SECURITY_ADMIN_PASSWORD: admin
    volumes:
      - grafana_data:/var/lib/grafana

  prometheus:
    image: prom/prometheus:latest
    container_name: orderflow-prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./infra/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus

volumes:
  redis_data:
  grafana_data:
  prometheus_data:
```

### 4.2 Pacotes

```bash
# SharedKernel
dotnet add src/BuildingBlocks/OrderFlow.SharedKernel package OpenTelemetry.Extensions.Hosting
dotnet add src/BuildingBlocks/OrderFlow.SharedKernel package OpenTelemetry.Instrumentation.AspNetCore
dotnet add src/BuildingBlocks/OrderFlow.SharedKernel package OpenTelemetry.Instrumentation.Http
dotnet add src/BuildingBlocks/OrderFlow.SharedKernel package OpenTelemetry.Instrumentation.SqlClient
dotnet add src/BuildingBlocks/OrderFlow.SharedKernel package OpenTelemetry.Exporter.Prometheus.AspNetCore
dotnet add src/BuildingBlocks/OrderFlow.SharedKernel package OpenTelemetry.Exporter.OpenTelemetryProtocol

# Catalog Infrastructure
dotnet add src/Services/Catalog/OrderFlow.Catalog.Infrastructure package Microsoft.Extensions.Caching.StackExchangeRedis

# Orders API
dotnet add src/Services/Orders/OrderFlow.Orders.Api package AspNetCore.HealthChecks.SqlServer
dotnet add src/Services/Orders/OrderFlow.Orders.Api package AspNetCore.HealthChecks.Redis
dotnet add src/Services/Orders/OrderFlow.Orders.Api package AspNetCore.HealthChecks.RabbitMQ
dotnet add src/Services/Orders/OrderFlow.Orders.Api package AspNetCore.HealthChecks.UI.Client
```

---

## 5. Código de Referência Completo

### 5.1 OpenTelemetry — Configuração Centralizada

**`src/BuildingBlocks/OrderFlow.SharedKernel/Observability/DiagnosticsConfig.cs`**

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OrderFlow.SharedKernel.Observability;

public static class DiagnosticsConfig
{
    public const string ServiceName = "OrderFlow";

    // ActivitySource para traces customizados
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    // Meter para métricas customizadas
    public static readonly Meter Meter = new(ServiceName);

    // Contadores customizados
    public static readonly Counter<long> OrdersCreated = Meter.CreateCounter<long>(
        "orderflow.orders.created", "orders", "Total orders created");

    public static readonly Counter<long> OrdersConfirmed = Meter.CreateCounter<long>(
        "orderflow.orders.confirmed", "orders", "Total orders confirmed");

    public static readonly Histogram<double> OrderProcessingDuration = Meter.CreateHistogram<double>(
        "orderflow.orders.processing_duration", "ms", "Order processing duration");
}
```

**`src/BuildingBlocks/OrderFlow.SharedKernel/Observability/OpenTelemetryExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OrderFlow.SharedKernel.Observability;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddOrderFlowOpenTelemetry(
        this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.RecordException = true;
                })
                .AddSource(DiagnosticsConfig.ServiceName)
                .AddSource("MassTransit")
                .AddOtlpExporter()) // Exporta para OTLP collector ou Seq
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(DiagnosticsConfig.ServiceName)
                .AddPrometheusExporter()); // /metrics endpoint

        return services;
    }
}
```

### 5.2 Serilog com Enrichers

**Configuração no `Program.cs` de qualquer API:**

```csharp
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "OrderFlow.Orders.Api")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341"));
```

**Enricher customizado para CorrelationId:**

**`src/BuildingBlocks/OrderFlow.SharedKernel/Observability/CorrelationIdMiddleware.cs`**

```csharp
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace OrderFlow.SharedKernel.Observability;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
```

### 5.3 Redis Cache — Decorator Pattern

**`src/Services/Catalog/OrderFlow.Catalog.Infrastructure/Caching/CachedProductService.cs`**

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Infrastructure.Caching;

public sealed class CachedProductService(
    IProductService innerService,
    IDistributedCache cache,
    ILogger<CachedProductService> logger) : IProductService
{
    private static readonly DistributedCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"product:{id}";

        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache HIT for {CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<ProductDto>(cached);
        }

        logger.LogDebug("Cache MISS for {CacheKey}", cacheKey);

        var product = await innerService.GetByIdAsync(id, ct);
        if (product is not null)
        {
            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(product),
                DefaultOptions,
                ct);
        }

        return product;
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(
        string? term, int page, int pageSize, CancellationToken ct = default)
    {
        // Queries com paginação: cache por query params
        var cacheKey = $"products:search:{term ?? "all"}:{page}:{pageSize}";

        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache HIT for {CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<PagedResult<ProductDto>>(cached)!;
        }

        var result = await innerService.SearchAsync(term, page, pageSize, ct);

        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            },
            ct);

        return result;
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var id = await innerService.CreateAsync(request, ct);

        // Invalidar cache de listagem
        await InvalidateSearchCacheAsync(ct);

        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        await innerService.UpdateAsync(id, request, ct);

        // Invalidar cache do produto específico e da listagem
        await cache.RemoveAsync($"product:{id}", ct);
        await InvalidateSearchCacheAsync(ct);
    }

    private async Task InvalidateSearchCacheAsync(CancellationToken ct)
    {
        // IDistributedCache não suporta delete por pattern.
        // Estratégia: usar prefixo versionado no cache key.
        // Incrementar a versão invalida todos os keys anteriores (ficam orphaned até TTL expirar).
        await cache.SetStringAsync(
            "products:search:version",
            Guid.NewGuid().ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
            ct);

        logger.LogDebug("Search cache version rotated — previous entries will miss on next request");

        // Alternativa para produção com StackExchange.Redis direto:
        // var server = redis.GetServer(redis.GetEndPoints().First());
        // foreach (var key in server.Keys(pattern: "products:search:*"))
        //     await redis.GetDatabase().KeyDeleteAsync(key);
    }
}
```

**Registro do Decorator no DI:**

```csharp
// Em DependencyInjection.cs do Catalog
services.AddScoped<ProductService>();  // Implementação real
services.AddScoped<IProductService>(sp =>
    new CachedProductService(
        sp.GetRequiredService<ProductService>(),
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<ILogger<CachedProductService>>()));
```

### 5.4 Output Caching (ASP.NET Core Built-in)

Para endpoints read-only simples, use Output Caching:

```csharp
// Program.cs
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.Expire(TimeSpan.FromSeconds(30)));

    options.AddPolicy("Products", policy =>
        policy.Expire(TimeSpan.FromMinutes(5))
              .Tag("products"));
});

// Middleware
app.UseOutputCache();

// Controller
[HttpGet]
[OutputCache(PolicyName = "Products")]
public async Task<IActionResult> GetAll() { ... }

// Invalidação
[HttpPost]
public async Task<IActionResult> Create(...)
{
    // ... criar produto ...
    // Invalida cache tagged como "products"
    await _outputCacheStore.EvictByTagAsync("products", ct);
    return Ok();
}
```

### 5.5 Health Checks Completos

**`Program.cs` do Orders API:**

```csharp
// Registrar health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString,
        name: "sqlserver",
        tags: ["db", "ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
        name: "redis",
        tags: ["cache", "ready"])
    .AddRabbitMQ(
        new Uri(builder.Configuration.GetValue<string>("RabbitMQ:ConnectionString")
            ?? "amqp://orderflow:orderflow123@localhost:5672"),
        name: "rabbitmq",
        tags: ["messaging", "ready"]);

// Mapear endpoints de health check
// Liveness: "O processo está rodando?" — retorna 200 se o app responde (sem verificar dependências).
// Se falhar, o orquestrador (Kubernetes/Container Apps) reinicia o container.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Não executa nenhum health check — só responde 200
});

// Readiness: "O serviço está pronto para receber tráfego?" — verifica dependências (SQL, Redis, RabbitMQ).
// Se falhar, o load balancer para de enviar requests para esta instância.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Startup: "O serviço terminou de inicializar?" — verifica apenas o banco (migrations).
// Evita que liveness/readiness falhem durante startup lento.
app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### 5.6 Compiled Queries (EF Core Performance)

```csharp
// Em OrderRepository ou como static no DbContext
public static class OrderQueries
{
    public static readonly Func<OrdersDbContext, Guid, CancellationToken, Task<Order?>>
        GetByIdWithItems = EF.CompileAsyncQuery(
            (OrdersDbContext db, Guid id, CancellationToken ct) =>
                db.Orders
                    .Include(o => o.Items)
                    .FirstOrDefault(o => o.Id == id));

    public static readonly Func<OrdersDbContext, Guid, IAsyncEnumerable<Order>>
        GetByCustomerId = EF.CompileAsyncQuery(
            (OrdersDbContext db, Guid customerId) =>
                db.Orders
                    .Where(o => o.CustomerId == customerId)
                    .OrderByDescending(o => o.CreatedAt));
}

// Uso no repository:
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
{
    return await OrderQueries.GetByIdWithItems(dbContext, id, ct);
}
```

### 5.7 Métricas Customizadas no Handler

```csharp
using System.Diagnostics;
using OrderFlow.SharedKernel.Observability;

public sealed class CreateOrderCommandHandler(...) : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        using var activity = DiagnosticsConfig.ActivitySource.StartActivity("CreateOrder");
        activity?.SetTag("customer.id", request.CustomerId.ToString());

        var sw = Stopwatch.StartNew();

        // ... lógica existente ...

        sw.Stop();
        DiagnosticsConfig.OrdersCreated.Add(1);
        DiagnosticsConfig.OrderProcessingDuration.Record(sw.ElapsedMilliseconds);

        activity?.SetTag("order.id", order.Id.ToString());

        return Result<Guid>.Success(order.Id);
    }
}
```

### 5.8 Prometheus Configuration

**`infra/prometheus/prometheus.yml`**

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'orderflow-orders-api'
    static_configs:
      - targets: ['host.docker.internal:5000']
    metrics_path: '/metrics'

  - job_name: 'orderflow-catalog-api'
    static_configs:
      - targets: ['host.docker.internal:5002']
    metrics_path: '/metrics'

  - job_name: 'orderflow-identity-api'
    static_configs:
      - targets: ['host.docker.internal:5001']
    metrics_path: '/metrics'
```

---

## 6. Testes

### 6.1 Testes do Cache Decorator

**`tests/OrderFlow.Catalog.Infrastructure.Tests/Caching/CachedProductServiceTests.cs`**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Infrastructure.Caching;
using System.Text.Json;

namespace OrderFlow.Catalog.Infrastructure.Tests.Caching;

public class CachedProductServiceTests
{
    private readonly Mock<IProductService> _innerServiceMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly CachedProductService _sut;

    public CachedProductServiceTests()
    {
        _sut = new CachedProductService(
            _innerServiceMock.Object,
            _cacheMock.Object,
            Mock.Of<ILogger<CachedProductService>>());
    }

    [Fact]
    public async Task GetById_CacheHit_DoesNotCallInnerService()
    {
        var product = new ProductDto { Id = Guid.NewGuid(), Name = "Laptop" };
        var json = JsonSerializer.Serialize(product);

        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(json));

        var result = await _sut.GetByIdAsync(product.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Laptop");
        _innerServiceMock.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_CacheMiss_CallsInnerServiceAndSetsCache()
    {
        var productId = Guid.NewGuid();
        var product = new ProductDto { Id = productId, Name = "Laptop" };

        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _innerServiceMock.Setup(s => s.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _sut.GetByIdAsync(productId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Laptop");
        _innerServiceMock.Verify(s => s.GetByIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### 6.2 Testes de Health Check

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace OrderFlow.Orders.Api.Tests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LivenessCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## ⚠️ Erros Comuns em Cache e Observabilidade

| # | Erro | Consequência | Solução |
|---|---|---|---|
| 1 | **Cache sem TTL** | Dados ficam eternamente em cache — stale data para sempre | Sempre defina `AbsoluteExpirationRelativeToNow` ou `SlidingExpiration` |
| 2 | **Invalidar cache com `Remove` em vez de TTL** | Múltiplas instâncias: uma invalida, outra ainda serve o cache antigo | Prefira TTL curto + eventual consistency. Se precisa de consistência forte, use Redis Pub/Sub para invalidação |
| 3 | **Cache stampede** | TTL expira → 100 requests simultâneas vão ao banco | Use lock local ou `SemaphoreSlim` antes de popular o cache. MassTransit/Redis tem `DistributedLock` |
| 4 | **Logs sem correlation ID** | Impossível rastrear uma request across serviços | OpenTelemetry propaga `TraceId` automaticamente. Adicione `Activity.Current?.TraceId` nos logs |
| 5 | **Métricas sem labels** | `http_requests_total` sem `endpoint`, `status_code` = número inútil | Use labels/tags: `http_requests_total{endpoint="/api/orders", status="200"}` |
| 6 | **Output cache em endpoints com dados do usuário** | Usuário A vê dados do Usuário B | Output cache só para dados públicos. Para dados privados, use `VaryByHeader("Authorization")` ou não use output cache |

---

## 🔧 Troubleshooting — Fase 06

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| Redis connection timeout | Container não subiu ou connection string errada | `docker compose ps redis`. Connection string: `localhost:6379` (dev) |
| Cache HIT rate = 0% | Chave do cache diferente entre get e set (typo ou formato) | Padronize: `$"product:{productId}"`. Log a chave usada no get e no set |
| Jaeger não mostra traces | `OTEL_EXPORTER_OTLP_ENDPOINT` não configurado ou serviço não instrumentado | Verifique `AddOtlpExporter()` e `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` |
| Traces aparecem mas sem spans do EF Core | Instrumentação do EF Core não adicionada | `AddEntityFrameworkCoreInstrumentation()` no OpenTelemetry setup |
| Prometheus `/metrics` retorna 404 | Endpoint não mapeado | Verifique `app.MapPrometheusScrapingEndpoint()` no pipeline |
| Grafana "No data" nos dashboards | Prometheus não scraping ou datasource não configurado | Verifique `prometheus.yml` targets e Grafana datasource URL |

---

## 🔗 Conectando os Pontos

| Artefato | Origem | Transformação nesta fase |
|---------|--------|------------------------|
| Dapper queries | Fase 03 CQRS | Agora instrumentadas com traces — spans mostram tempo de cada query |
| MassTransit publish/consume | Fase 05 | Traces propagados entre publisher → RabbitMQ → consumer (distributed tracing) |
| Health checks | Fase 01 | Agora incluem Redis + RabbitMQ + SQL Server (health check completo) |

> **Preview Fase 07:** O Gateway YARP centralizará métricas de todas as APIs em um único ponto. O Grafana dashboard mostrará latência por rota do gateway, não por serviço individual.

---

## 7. Checkpoint

> 💡 **Por que isso importa no dia-a-dia?** Cache e observabilidade são os **superpoderes do Sênior em produção**. Sem cache, cada request vai ao banco — com 1000 req/s, o banco sofre e a latência explode. Sem observabilidade, quando o sistema fica lento às 3h da manhã, você está **cego** — não sabe qual serviço está demorando, qual query está lenta, quantos erros estão acontecendo. Cache reduz latência de 50ms para <1ms. OpenTelemetry mostra exatamente onde os milissegundos estão sendo gastos. **Juntos, são a diferença entre "o sistema está lento" e "encontrei o problema em 5 minutos".**

### Validação Completa

- [ ] **Redis rodando:** `docker compose up redis`
- [ ] **Cache-aside implementado:** CachedProductService decorator
- [ ] **Output Caching configurado:** Em endpoints read-only
- [ ] **OpenTelemetry:** Traces e metrics exportando
- [ ] **Serilog enrichers:** CorrelationId, MachineName, Environment
- [ ] **Structured logging:** Sem string interpolation em logs
- [ ] **Health checks:** /health/live, /health/ready, /health/startup
- [ ] **Prometheus endpoint:** /metrics acessível
- [ ] **Grafana dashboard:** Métricas visualizadas
- [ ] **Seq:** Logs chegando com correlation ID
- [ ] **Compiled Queries:** Em consultas frequentes do EF Core
- [ ] **Testes:** Cache HIT/MISS testados
- [ ] **Commit:** `feat(observability): add Redis cache, OpenTelemetry tracing and structured logging`

### Comandos de Verificação

```bash
# Subir infraestrutura
docker compose up -d redis grafana prometheus seq

# Verificar Redis
docker exec orderflow-redis redis-cli ping
# → PONG

# Verificar health checks
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready

# Verificar Prometheus metrics
curl http://localhost:5000/metrics

# Grafana: http://localhost:3000 (admin/admin)
# Seq: http://localhost:5341
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| Cache Service | `CacheService.cs` (abstração sobre IDistributedCache) |
| Cache Invalidation | `CacheInvalidationHandler.cs` (via domain events) |
| Output Cache | `OutputCacheConfig.cs` com policies por endpoint |
| OTel Traces | `OpenTelemetryConfig.cs` (ActivitySource, Jaeger exporter) |
| OTel Metrics | Custom meters para orders/min, cache hit ratio |
| Serilog Config | `SerilogConfig.cs` com enrichers (CorrelationId, UserId) |
| Health Checks | `HealthCheckConfig.cs` (SQL Server, RabbitMQ, Redis) |
| Grafana | Dashboard JSON para métricas de negócio |
| Docker | Redis, Seq, Grafana, Prometheus no `docker-compose.yml` |
| Testes | `CacheServiceTests.cs`, `HealthCheckTests.cs` |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 6

**1. "Explique o Cache-Aside Pattern."**
— O código verifica o cache primeiro. Se **hit**: retorna direto (rápido). Se **miss**: busca no banco, armazena no cache, retorna. Invalidação é crítica — no OrderFlow, domain events invalidam cache quando um pedido é atualizado. Alternativa: **Write-Through** (escrita atualiza cache + banco simultaneamente), mas é mais complexo. Cache-Aside é o padrão mais comum em APIs.

$$\text{Cache Hit Ratio} = \frac{\text{Hits}}{\text{Hits} + \text{Misses}} \times 100\%$$

> Alvo saudável: **> 80%** em endpoints de leitura frequente.

**2. "Qual a diferença entre Distributed Cache e Output Cache?"**
— **Distributed Cache** (Redis) armazena dados arbitrários — você controla o que entra/sai. **Output Cache** armazena **responses HTTP inteiras** — o middleware intercepta antes do handler e retorna 304/cached response. Use Distributed Cache para dados de domínio reutilizáveis (catálogo de produtos). Use Output Cache para endpoints GET idempotentes com alto tráfego.

**3. "O que é OpenTelemetry e por que não usar logs tradicionais?"**
— OpenTelemetry é um padrão **vendor-neutral** para **traces, metrics e logs**. Traces mostram o caminho de uma request através de múltiplos serviços (distributed tracing). Logs tradicionais mostram "o que aconteceu" mas não "como os serviços se comunicaram". Com OTel, você vê que um pedido levou 200ms no Orders API + 50ms no RabbitMQ + 100ms no Notification Worker = 350ms total.

**4. "Structured Logging vs log de texto — qual a diferença?"**
— Log de texto: `"Order 123 created by user 456"`. Structured log: `{"OrderId": 123, "UserId": 456, "Event": "OrderCreated"}`. Structured logs são **pesquisáveis**: filtre por OrderId, agrupe por UserId, calcule métricas. Serilog faz isso nativamente com template syntax: `Log.Information("Order {OrderId} created", orderId)` — o `{OrderId}` vira propriedade, não texto.

**5. "Health Check liveness vs readiness — qual a diferença?"**
— **Liveness** = "o processo está vivo?" (se falhar, Kubernetes reinicia o pod). **Readiness** = "o serviço está pronto para receber tráfego?" (se falhar, o load balancer para de enviar requests). Exemplo: API iniciando, Redis ainda conectando → liveness OK, readiness FAIL. Após conexão → ambos OK. Sem essa separação, um serviço lento seria reiniciado em vez de apenas removido do tráfego.

---

## 🔬 Aprofundamento Sênior

### A1. Cache Distribuído — Padrões Avançados

#### Cache Stampede (Thundering Herd)
Cache expira → 1000 requests batem no DB simultaneamente. Mitigação:

```csharp
// Single-flight com SemaphoreSlim por chave (in-process)
private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
{
    if (await _cache.GetAsync<T>(key) is { } cached) return cached;
    
    var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    await sem.WaitAsync();
    try
    {
        if (await _cache.GetAsync<T>(key) is { } reChecked) return reChecked;
        var fresh = await factory();
        await _cache.SetAsync(key, fresh, ttl);
        return fresh;
    }
    finally { sem.Release(); }
}
```

Distribuído? **Redis distributed lock** (RedLock) ou **probabilistic early refresh**.

#### Cache Invalidation por Evento
Cache-aside leak: dado mudou no DB, cache continua antigo até TTL. Solução: invalidar via evento.

```csharp
public class ProductUpdatedHandler(IDistributedCache cache)
    : INotificationHandler<ProductUpdatedEvent>
{
    public Task Handle(ProductUpdatedEvent evt, CancellationToken ct)
        => cache.RemoveAsync($"product:{evt.ProductId}", ct);
}
```

### A2. Output Caching (.NET 7+)

Substitui o velho Response Caching:

```csharp
builder.Services.AddOutputCache(opt =>
{
    opt.AddPolicy("Catalog", b => b
        .Expire(TimeSpan.FromMinutes(10))
        .Tag("products")
        .SetVaryByQuery("page", "size"));
});

app.MapGet("/products", ...).CacheOutput("Catalog");

// Invalidar por tag
await outputCacheStore.EvictByTagAsync("products", ct);
```

### A3. OpenTelemetry — Semantic Conventions

Usar **nomes padronizados** (CNCF) garante interop com qualquer backend:

```csharp
activity?.SetTag("http.request.method", "POST");
activity?.SetTag("http.response.status_code", 201);
activity?.SetTag("db.system", "mssql");
activity?.SetTag("messaging.system", "rabbitmq");
activity?.SetTag("messaging.destination.name", "orders.confirmed");
```

Lista completa: [opentelemetry.io/docs/specs/semconv](https://opentelemetry.io/docs/specs/semconv/).

### A4. Tail Sampling vs Head Sampling

- **Head:** decide no início do trace (mantém X%). Perde traces de erro raros.
- **Tail:** decide no fim. Pode manter **100% dos traces com erro ou alta latência** + 1% dos sucessos rápidos. Requer OTel Collector com tail sampling processor.

Padrão produção: tail sampling.

### A5. Correlation ID End-to-End

W3C Trace Context (`traceparent` header) é nativo em ASP.NET Core 10. Propaga automaticamente em HttpClient. Para mensageria, MassTransit propaga via headers RabbitMQ.

```csharp
// Logar TraceId em todo log
builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.WithProperty("Service", "Orders")
    .Enrich.WithSpan()              // adiciona TraceId, SpanId
    .WriteTo.Seq("http://seq:5341"));
```

### A6. Métricas Customizadas via `Meter`

```csharp
private static readonly Meter Meter = new("OrderFlow.Orders", "1.0");
private static readonly Histogram<double> OrderTotal = 
    Meter.CreateHistogram<double>("orders.total.amount", "BRL");

// No handler
OrderTotal.Record(order.Total.Amount, new TagList { { "customer.tier", customer.Tier } });
```

Grafana mostra distribuição de tickets por tier.

### 💼 Perguntas Sênior

**"Como evitar Cache Stampede?"** — Single-flight (in-process: SemaphoreSlim por chave; distribuído: Redis lock). Alternativa: **probabilistic early refresh** — cada request perto do TTL tem probabilidade crescente de regenerar antecipadamente, espalhando carga.

**"Diferencie head e tail sampling em distributed tracing."** — Head: decide manter trace no início (perde anomalias raras). Tail: decide no fim, baseado em duração/erro/atributos — mantém 100% dos problemas + amostra dos sucessos. Tail é o padrão profissional; exige OTel Collector.

---

> **Próximo passo:** Avance para `fase-07-gateway-docker.md`.
>
> 🚀 **Trilha Sênior:** [`fase-14-feature-flags-sre.md`](./fase-14-feature-flags-sre.md) — SLO/SLI, RED/USE, Error Budget.
