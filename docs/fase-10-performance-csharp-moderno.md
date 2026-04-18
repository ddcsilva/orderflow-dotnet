# Fase 10 — Performance e C# Moderno

> **Trilha:** Sênior | **Pré-requisitos:** Fases 01-09
> **Objetivo:** Dominar as ferramentas de **alta performance** do .NET 10 / C# 13: `Span<T>`, `Memory<T>`, `ValueTask`, `IAsyncEnumerable`, `System.Threading.Channels`, `ArrayPool`, source generators e Native AOT. Validar tudo com **BenchmarkDotNet**.

### 🎯 O que você vai aprender

- Como **medir antes de otimizar** com BenchmarkDotNet (memory diagnoser)
- `Span<T>` e `Memory<T>` — quando substituem `string`/`byte[]` e por quê
- `ValueTask` vs `Task` — quando aloca, quando não
- `IAsyncEnumerable<T>` para streaming sem carregar tudo em memória
- `Channels` para producer/consumer com backpressure
- `ArrayPool<T>` para reutilizar buffers
- **Source generators** vs reflection (Regex, JSON, Logging)
- **Native AOT** — quando vale, quando não

---

## Sumário

1. [A Regra de Ouro: Meça Antes](#1-a-regra-de-ouro-meça-antes)
2. [BenchmarkDotNet — O Padrão Industrial](#2-benchmarkdotnet--o-padrão-industrial)
3. [`Span<T>` e `Memory<T>`](#3-spant-e-memoryt)
4. [`ValueTask` vs `Task`](#4-valuetask-vs-task)
5. [`IAsyncEnumerable<T>` — Streaming](#5-iasyncenumerablet--streaming)
6. [`Channels` — Producer/Consumer](#6-channels--producerconsumer)
7. [`ArrayPool<T>`](#7-arraypoolt)
8. [Source Generators](#8-source-generators)
9. [Native AOT](#9-native-aot)
10. [EF Core Performance](#10-ef-core-performance)
11. [💼 Perguntas de Entrevista](#11--perguntas-de-entrevista)

---

## 1. A Regra de Ouro: Meça Antes

> *"Premature optimization is the root of all evil."* — Donald Knuth

**Não otimize sem evidência.** O que parece lento muitas vezes não é. O que é gargalo costuma surpreender. Sempre:

1. Profilar (dotnet-trace, PerfView, Visual Studio Profiler)
2. Benchmark (BenchmarkDotNet) com baseline
3. Otimizar **um** ponto
4. Re-benchmark — confirmar ganho
5. Verificar legibilidade — o ganho compensa a complexidade?

**Métrica primária:** se o código não está num *hot path*, otimizá-lo é desperdício de tempo de engenheiro.

---

## 2. BenchmarkDotNet — O Padrão Industrial

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net100)]
public class StringConcatBenchmark
{
    private readonly string[] _items = Enumerable.Range(0, 100)
        .Select(i => i.ToString()).ToArray();

    [Benchmark(Baseline = true)]
    public string PlusOperator()
    {
        var result = "";
        foreach (var item in _items) result += item;
        return result;
    }

    [Benchmark]
    public string StringBuilder()
    {
        var sb = new StringBuilder();
        foreach (var item in _items) sb.Append(item);
        return sb.ToString();
    }

    [Benchmark]
    public string StringJoin() => string.Join(string.Empty, _items);

    [Benchmark]
    public string StringConcat() => string.Concat(_items);
}

// Program.cs
BenchmarkRunner.Run<StringConcatBenchmark>();
```

Saída típica (.NET 10):

| Method | Mean | Allocated | Ratio |
|---|---|---|---|
| PlusOperator | 12.3 µs | 28 KB | 1.00 |
| StringBuilder | 0.8 µs | 1.5 KB | 0.06 |
| StringJoin | 0.5 µs | 392 B | 0.04 |
| StringConcat | 0.4 µs | 392 B | 0.03 |

**Lições:**
- Sempre incluir `[MemoryDiagnoser]` — alocação importa tanto quanto tempo
- `Baseline = true` para comparar — número absoluto sem contexto é inútil
- Múltiplos jobs (`[SimpleJob(RuntimeMoniker.Net80)]` + Net100) para comparar runtimes

---

## 3. `Span<T>` e `Memory<T>`

`Span<T>` é uma struct que representa **uma janela contígua de memória** sem alocar. Funciona em stack, heap ou nativo.

```csharp
// ❌ Aloca substring
string GetCurrency(string price) => price.Substring(0, 3);

// ✅ Zero-alloc
ReadOnlySpan<char> GetCurrency(ReadOnlySpan<char> price) => price[..3];

// Uso
var price = "USD 99.99";
var currency = GetCurrency(price.AsSpan()); // sem alocação
```

**Quando usar:**
- Parsers (CSV, JSON manual, protocolos)
- Hot paths com manipulação de string/buffer
- Substituir alocações repetidas em loops

**Limitações de `Span<T>`:**
- **Não pode ir para heap** — só stack. Não pode ser campo de classe (só de struct ref).
- Não pode ser usado em `async`/`yield` — use `Memory<T>` se precisar.

```csharp
// ✅ Memory<T> pode ser awaited
public async Task ProcessAsync(ReadOnlyMemory<byte> buffer)
{
    await Task.Yield();
    var span = buffer.Span; // pega Span quando precisar
}
```

### Exemplo Real — Parser de Linha CSV

```csharp
public static int ParseInt(ReadOnlySpan<char> field) =>
    int.Parse(field, CultureInfo.InvariantCulture);

public static ProductLine Parse(ReadOnlySpan<char> line)
{
    var enumerator = line.Split(',');
    enumerator.MoveNext();
    var id = ParseInt(line[enumerator.Current]);
    enumerator.MoveNext();
    var name = line[enumerator.Current].ToString(); // só aloca o necessário
    enumerator.MoveNext();
    var price = decimal.Parse(line[enumerator.Current], CultureInfo.InvariantCulture);
    return new ProductLine(id, name, price);
}
```

Benchmark típico vs `string.Split`: **3-10x mais rápido**, **80%+ menos alocação**.

---

## 4. `ValueTask` vs `Task`

`Task` é uma classe — **sempre aloca** quando criada (a menos que cache `Task.CompletedTask`). `ValueTask` é struct — **zero-alloc** se o método completa síncronamente.

```csharp
public ValueTask<Product?> GetProductAsync(Guid id, CancellationToken ct)
{
    if (_cache.TryGetValue(id, out var cached))
        return ValueTask.FromResult(cached);  // zero-alloc no hot path

    return new ValueTask<Product?>(LoadFromDbAsync(id, ct));
}
```

**Use ValueTask quando:**
- Cache hit é **frequente** (>50% das chamadas)
- Método é chamado em **hot path**
- Você mediu — `Task` está no top de alocação

**NÃO use ValueTask quando:**
- Vai ser awaited **mais de uma vez** (UB!)
- Vai ser passado para `Task.WhenAll` — precisa `.AsTask()` (perde a vantagem)
- API pública genérica — `Task` é mais previsível

> Regra prática: 99% dos métodos async devem usar `Task`. ValueTask é otimização de hot path **medido**.

---

## 5. `IAsyncEnumerable<T>` — Streaming

Carregar 100k pedidos em `List<Order>` aloca tudo de uma vez. Streaming processa um a um:

```csharp
// ❌ Carrega tudo
public async Task<List<Order>> GetAllAsync()
{
    return await _db.Orders.ToListAsync();
}

// ✅ Streaming
public async IAsyncEnumerable<Order> GetAllAsync(
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var order in _db.Orders.AsAsyncEnumerable().WithCancellation(ct))
        yield return order;
}

// Consumindo
await foreach (var order in repository.GetAllAsync(ct))
{
    await ProcessAsync(order);  // memória estável, não cresce
}
```

**Em ASP.NET Core 10**, retorne diretamente:

```csharp
app.MapGet("/orders/stream", (OrderRepository repo, CancellationToken ct) =>
    repo.GetAllAsync(ct));  // serializa como NDJSON streaming
```

---

## 6. `Channels` — Producer/Consumer

`System.Threading.Channels` é uma fila assíncrona com **backpressure** built-in. Substitui `BlockingCollection` para cenários async.

```csharp
public class OrderProcessingService(IOrderHandler handler) : BackgroundService
{
    private readonly Channel<Order> _channel = Channel.CreateBounded<Order>(
        new BoundedChannelOptions(capacity: 100)
        {
            FullMode = BoundedChannelFullMode.Wait,  // backpressure!
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(Order order, CancellationToken ct) =>
        _channel.Writer.WriteAsync(order, ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var order in _channel.Reader.ReadAllAsync(ct))
        {
            await handler.HandleAsync(order, ct);
        }
    }
}
```

**Vantagens vs Queue+SemaphoreSlim:**
- Async nativo
- Backpressure: produtor espera quando cheia (não estoura memória)
- Otimizado para single/multi reader/writer

**Casos de uso:**
- Buffer entre HTTP intake e processamento pesado
- Pipeline de processamento (estágio A → B → C com filas entre)
- Substitui `BlockingCollection` em código async

---

## 7. `ArrayPool<T>`

Aluga arrays do pool em vez de alocar — para buffers temporários grandes.

```csharp
public async Task<int> ReadAsync(Stream stream, CancellationToken ct)
{
    var buffer = ArrayPool<byte>.Shared.Rent(8192);
    try
    {
        return await stream.ReadAsync(buffer, ct);
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);  // ⚠️ try/finally obrigatório
    }
}
```

**Quando usar:** buffers > 1KB, alocados muitas vezes por segundo.
**Cuidados:** array retornado pode ter dados "sujos"; sempre passe `clearArray: true` para Return se contiver segredos.

---

## 8. Source Generators

Geram código em **compile time** — sem reflection em runtime.

### 8.1 `[GeneratedRegex]`

```csharp
public partial class EmailValidator
{
    [GeneratedRegex(@"^[\w\.-]+@[\w\.-]+\.\w+$", RegexOptions.Compiled)]
    public static partial Regex EmailPattern();
}

// Uso: 2-5x mais rápido que Regex(string)
var isValid = EmailValidator.EmailPattern().IsMatch(email);
```

### 8.2 System.Text.Json Source Gen

```csharp
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(List<Order>))]
public partial class OrderJsonContext : JsonSerializerContext;

// Uso — sem reflection, AOT-friendly
var json = JsonSerializer.Serialize(order, OrderJsonContext.Default.Order);
```

### 8.3 LoggerMessage Source Gen

```csharp
public partial class OrderService(ILogger<OrderService> logger)
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information,
        Message = "Order {OrderId} confirmed for customer {CustomerId}")]
    private partial void LogOrderConfirmed(Guid orderId, Guid customerId);

    public void Confirm(Order order)
    {
        // zero alocação: sem boxing, sem string interpolation runtime
        LogOrderConfirmed(order.Id, order.CustomerId);
    }
}
```

> Padrão moderno: **sempre prefira source generators** para Regex, JSON e Logging em código que executa frequentemente.

---

## 9. Native AOT

Compila C# para código nativo no build — sem JIT, sem reflection (em geral), startup ~10x mais rápido, imagem ~70% menor.

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

**Quando vale:**
- Funções serverless (Azure Functions, AWS Lambda) — startup importa
- CLIs distribuídos
- Containers escalando rapidamente (cold start)
- Microserviços com SLA agressivo de inicialização

**Quando NÃO vale:**
- App usa muito reflection (EF Core ainda tem limitações; melhorando)
- Plugins dinâmicos
- Apps de longa duração — JIT eventualmente otimiza melhor que AOT

**Limitações em .NET 10:**
- EF Core: parcialmente suportado (compiled models obrigatório)
- ASP.NET Core: Minimal APIs OK; Controllers limitados
- Bibliotecas com `Activator.CreateInstance(type)` em runtime quebram

---

## 10. EF Core Performance

### 10.1 Compiled Queries

```csharp
private static readonly Func<AppDbContext, Guid, Task<Order?>> GetOrderById =
    EF.CompileAsyncQuery((AppDbContext db, Guid id) =>
        db.Orders.AsNoTracking().FirstOrDefault(o => o.Id == id));

// Uso: pula o expression tree compile a cada chamada
var order = await GetOrderById(_db, orderId);
```

### 10.2 Split Query

Evita explosão cartesiana em joins de coleções:

```csharp
var orders = await _db.Orders
    .AsSplitQuery()                          // 🔑
    .Include(o => o.Items)
    .Include(o => o.Notifications)
    .ToListAsync();
```

### 10.3 `AsNoTracking` + Projection

```csharp
// ❌ Carrega entidade inteira para retornar 2 campos
var dtos = await _db.Orders.Select(o => new { o.Id, o.Total }).ToListAsync();

// ✅ AsNoTracking + projection direta
var dtos = await _db.Orders
    .AsNoTracking()
    .Select(o => new OrderSummary(o.Id, o.Total))
    .ToListAsync();
```

### 10.4 Bulk Operations (.NET 10 nativo)

```csharp
// EF Core 10 — operações bulk nativas, sem extensões
await _db.Orders
    .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Expired), ct);

await _db.Orders.Where(o => o.Status == OrderStatus.Cancelled)
    .ExecuteDeleteAsync(ct);
```

---

## 11. 💼 Perguntas de Entrevista

**1. "Quando `ValueTask<T>` é melhor que `Task<T>`?"**
— Quando o método pode completar **sincronamente** na maioria das chamadas (cache hit, fast path). `Task` aloca; `ValueTask` é struct, zero-alloc no caso síncrono. Cuidados: não awaitar mais de uma vez, usar `.AsTask()` para `WhenAll`.

**2. "Diferença entre `Span<T>` e `Memory<T>`?"**
— `Span<T>` é ref struct, **só stack** — não pode ser campo de classe nem usada em async/yield. `Memory<T>` é struct normal, pode ir para heap, await, etc. Acesso via `.Span` para operações sincronas.

**3. "O que é backpressure e como `Channels` ajuda?"**
— Backpressure é o **produtor desacelerar** quando consumidor não dá conta. `BoundedChannel` com `FullMode = Wait` faz `WriteAsync` bloquear até ter espaço — naturalmente, sem código manual.

**4. "Quando usar `IAsyncEnumerable<T>` em vez de `Task<List<T>>`?"**
— Quando: (1) consumidor processa item-a-item, (2) fonte é grande (cursor DB, file, network), (3) latência do primeiro item importa. Memória fica **constante** em vez de crescer linearmente.

**5. "Source generators vs reflection — quais ganhos concretos?"**
— Compile-time gera código real → JIT otimiza melhor. Sem startup cost de descobrir tipos. Compatível com AOT. Logging gerado: **zero boxing** de int/Guid. JSON gerado: 1.5-3x mais rápido. Regex: 2-5x.

**6. "Quando você ativaria Native AOT?"**
— Cenários com **cold start crítico**: serverless, escalonamento horizontal agressivo, CLIs. Trade-off: limitações com reflection (EF parcial, AutoMapper sofre), build mais lento, troubleshoot mais difícil. Não vale para apps long-running onde JIT eventualmente otimiza melhor.

**7. "Como você abordaria uma reclamação de 'API lenta'?"**
— (1) **Reproduzir** com tráfego sintético. (2) **Profile** — dotnet-trace, dotnet-counters, distributed tracing. Identificar se é CPU, DB, rede, GC. (3) **Benchmark** o suspeito com BenchmarkDotNet. (4) **Otimizar** uma coisa: query (compiled, AsNoTracking), alocação (Span, ArrayPool), ou cache. (5) **Re-medir**. Não otimize sem dado.

---

## Checkpoint

✅ BenchmarkDotNet rodando para hot paths conhecidos
✅ Pelo menos 1 parser usando `Span<T>` (CSV ou similar)
✅ Source-generated logging em todo serviço
✅ EF Core com `AsNoTracking` + compiled queries em queries hot
✅ `IAsyncEnumerable` em endpoint de exportação
✅ Decisão documentada (ADR) sobre AOT — vale ou não para o projeto

➡️ **Próxima fase:** [`fase-11-kubernetes-service-mesh.md`](./fase-11-kubernetes-service-mesh.md)
