# Fase 13 — gRPC, Kafka e Event Sourcing / CDC

> **Trilha:** Sênior | **Pré-requisitos:** Fases 02 (DDD), 05 (Mensageria)
> **Objetivo:** Adicionar **gRPC** para comunicação interna binária, comparar **Kafka vs RabbitMQ**, introduzir **Event Sourcing** como modelo alternativo de persistência, e usar **Change Data Capture (Debezium)** para evoluir do Outbox em alta escala.

### 🎯 O que você vai aprender

- Quando usar **gRPC** em vez de REST/HTTP — e quando não
- Definir contratos com **Protobuf** + tooling .NET 10
- Streaming gRPC: client, server, bidirecional
- **Kafka** vs **RabbitMQ** — quando cada faz sentido
- **Event Sourcing** — fundamentos, snapshots, projections
- **CDC** (Change Data Capture) com Debezium — Outbox sem código
- **Outbox vs CDC vs Event Sourcing** — decisão arquitetural

---

## Sumário

1. [gRPC: Quando e Por Quê](#1-grpc-quando-e-por-quê)
2. [Definindo Contratos com Protobuf](#2-definindo-contratos-com-protobuf)
3. [gRPC no Orders → Catalog](#3-grpc-no-orders--catalog)
4. [Streaming gRPC](#4-streaming-grpc)
5. [Kafka vs RabbitMQ](#5-kafka-vs-rabbitmq)
6. [Event Sourcing — Conceito](#6-event-sourcing--conceito)
7. [Change Data Capture (Debezium)](#7-change-data-capture-debezium)
8. [Decisão: Outbox vs CDC vs Event Sourcing](#8-decisão-outbox-vs-cdc-vs-event-sourcing)
9. [💼 Perguntas de Entrevista](#9--perguntas-de-entrevista)

---

## 1. gRPC: Quando e Por Quê

| Aspecto | REST/JSON | gRPC |
|---|---|---|
| Protocolo | HTTP/1.1 ou 2 | **HTTP/2** obrigatório |
| Payload | JSON (texto) | **Protobuf** (binário) |
| Contrato | OpenAPI (descritivo) | **`.proto`** (gerado, fortemente tipado) |
| Performance | Baseline | **3-10x** mais rápido (latência + payload) |
| Streaming | SSE / WebSocket ad-hoc | **Nativo** (4 modos) |
| Browser | ✅ Direto | ⚠️ Precisa gRPC-Web ou Connect |
| Debug | Postman, curl | grpcurl, Bloom RPC |

### Quando usar
- Comunicação **interna** entre microserviços (Orders → Catalog)
- **High-throughput** ou baixa latência (telemetria, ML serving)
- Contratos fortes entre times

### Quando NÃO usar
- API **pública** consumida por browser (use REST ou GraphQL)
- Times sem familiaridade com Protobuf — curva de aprendizado
- Quando a infra (proxies, gateways) não suporta HTTP/2 nativamente

> **Padrão híbrido (recomendado):** REST/JSON externamente (pelo Gateway YARP) + gRPC interno entre serviços.

---

## 2. Definindo Contratos com Protobuf

`Protos/catalog.proto`:

```proto
syntax = "proto3";

option csharp_namespace = "OrderFlow.Catalog.Grpc";

package catalog;

service CatalogService {
  rpc GetProduct (GetProductRequest) returns (ProductReply);
  rpc CheckStock (CheckStockRequest) returns (StockReply);
  rpc StreamPriceUpdates (PriceStreamRequest) returns (stream PriceUpdate);
}

message GetProductRequest {
  string product_id = 1;
}

message ProductReply {
  string id = 1;
  string name = 2;
  string sku = 3;
  Money price = 4;
  int32 stock_quantity = 5;
}

message Money {
  string currency = 1;
  string amount = 2;       // string para precisão decimal
}

message CheckStockRequest {
  string product_id = 1;
  int32 quantity = 2;
}

message StockReply {
  bool available = 1;
  int32 current_stock = 2;
}

message PriceStreamRequest {
  repeated string product_ids = 1;
}

message PriceUpdate {
  string product_id = 1;
  Money new_price = 2;
  google.protobuf.Timestamp updated_at = 3;
}
```

### `.csproj` — Geração

```xml
<ItemGroup>
  <PackageReference Include="Grpc.AspNetCore" />
  <PackageReference Include="Grpc.Net.ClientFactory" />
  <PackageReference Include="Grpc.Tools" PrivateAssets="all" />
</ItemGroup>

<ItemGroup>
  <Protobuf Include="Protos\catalog.proto" GrpcServices="Both" />
</ItemGroup>
```

> **Convenção:** `.proto` em **um repo compartilhado** (ex: `OrderFlow.Contracts`) com versionamento — evita drift entre servidor e cliente.

### Regras de Backward Compatibility
- **Nunca** renomeie/remova campos — apenas adicione com **novo número**
- Use `reserved 5, 6;` para campos removidos
- `optional` em proto3 para distinguir "não enviado" vs "default"

---

## 3. gRPC no Orders → Catalog

### Server (Catalog API)

```csharp
public sealed class CatalogGrpcService(IProductRepository repo)
    : CatalogService.CatalogServiceBase
{
    public override async Task<ProductReply> GetProduct(
        GetProductRequest request, ServerCallContext context)
    {
        var product = await repo.GetByIdAsync(
            Guid.Parse(request.ProductId), context.CancellationToken);

        if (product is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Product not found"));

        return new ProductReply
        {
            Id = product.Id.ToString(),
            Name = product.Name,
            Sku = product.Sku,
            Price = new Money { Currency = product.Price.Currency, Amount = product.Price.Amount.ToString("F2") },
            StockQuantity = product.Stock
        };
    }
}

// Program.cs
builder.Services.AddGrpc(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());
app.MapGrpcService<CatalogGrpcService>();
```

### Client (Orders API)

```csharp
// Program.cs do Orders
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Services:Catalog:GrpcUrl"]!);
})
.AddStandardResilienceHandler()                 // Polly v8 funciona com gRPC
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    EnableMultipleHttp2Connections = true,
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
});

// Uso
public sealed class CatalogGrpcClient(CatalogService.CatalogServiceClient client)
{
    public async Task<bool> IsInStockAsync(Guid productId, int quantity, CancellationToken ct)
    {
        var reply = await client.CheckStockAsync(new CheckStockRequest
        {
            ProductId = productId.ToString(),
            Quantity = quantity
        }, cancellationToken: ct);

        return reply.Available;
    }
}
```

---

## 4. Streaming gRPC

Quatro modos:

| Modo | Caso de uso |
|---|---|
| Unary | Request/response simples (REST equivalent) |
| Server streaming | Push contínuo (stream de preços, notificações) |
| Client streaming | Upload em chunks, telemetria batch |
| Bidirecional | Chat, jogo, real-time collab |

### Server Streaming — Stream de Atualizações de Preço

```csharp
public override async Task StreamPriceUpdates(
    PriceStreamRequest request,
    IServerStreamWriter<PriceUpdate> responseStream,
    ServerCallContext context)
{
    await foreach (var update in _priceWatcher.SubscribeAsync(
        request.ProductIds.Select(Guid.Parse), context.CancellationToken))
    {
        await responseStream.WriteAsync(new PriceUpdate
        {
            ProductId = update.ProductId.ToString(),
            NewPrice = new Money { Currency = "USD", Amount = update.Price.ToString("F2") },
            UpdatedAt = Timestamp.FromDateTimeOffset(update.At)
        });
    }
}
```

---

## 5. Kafka vs RabbitMQ

| Critério | RabbitMQ | Kafka |
|---|---|---|
| Modelo | Message broker (smart broker, dumb consumer) | Distributed log (dumb broker, smart consumer) |
| Retenção | Mensagens deletadas após ack | **Retidas por tempo** (replay possível) |
| Throughput típico | ~50k msg/s por broker | **Milhões msg/s** com particionamento |
| Ordem | Por fila (1 consumer = ordem) | Por partição (chave) |
| Roteamento | Rico (exchanges: direct, topic, fanout, headers) | Por tópico/partição |
| Cenário ideal | Tarefas async, comandos, eventos transacionais | Event sourcing, CDC, analytics, streaming |
| Curva de aprendizado | Mais simples | Maior (partições, consumer groups, offsets) |
| Replay | ⚠️ Difícil (precisa requeue manual) | ✅ Nativo |
| Operação | Mais simples | Exige conhecimento (ZK/KRaft, partições) |

### Decisão Concreta para o OrderFlow

| Caso | Escolha |
|---|---|
| `OrderConfirmed → Notification` | **RabbitMQ** — ordem de eventos transacionais, baixo volume |
| `OrderEvents → Analytics`, replay para BI | **Kafka** — precisa retenção e replay |
| Stream de cliques (telemetria) | **Kafka** — alto volume |
| Worker email assíncrono | **RabbitMQ** — fila de tarefas, ack por mensagem |

> **Não é "ou" — é "e".** Sistemas reais usam ambos. OrderFlow começa com RabbitMQ; introduz Kafka para o pipeline de eventos analíticos.

### Setup Kafka com Confluent.Kafka

```csharp
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = "kafka:9092",
        Acks = Acks.All,                      // 🔑 durabilidade
        EnableIdempotence = true,             // 🔑 evita duplicatas em retry
        CompressionType = CompressionType.Lz4
    }).Build());

// Produzindo
await _producer.ProduceAsync("orders.events",
    new Message<string, string>
    {
        Key = orderId.ToString(),             // mesma chave → mesma partição → ordem
        Value = JsonSerializer.Serialize(@event)
    }, ct);
```

---

## 6. Event Sourcing — Conceito

Em vez de salvar o **estado atual** (`Orders` table), salva-se a **sequência de eventos** que produziram esse estado.

```
Tabela tradicional:
Orders { Id=123, Status=Shipped, Total=99.90, UpdatedAt=2026-04-15 }

Event Sourcing:
Events:
  1. OrderCreated   (id=123, customerId=42, at=2026-04-15 10:00)
  2. ItemAdded      (productId=A, qty=2)
  3. ItemAdded      (productId=B, qty=1)
  4. OrderConfirmed (at=2026-04-15 10:05)
  5. OrderShipped   (trackingCode="X123", at=2026-04-15 11:00)

Estado atual = reduce(events)
```

### Vantagens
- **Audit trail** completo de graça
- **Time travel** — estado em qualquer momento do passado
- **Projections** múltiplas (read models otimizados)
- **Eventos = contrato** — fácil integração

### Desvantagens
- Querys complexas exigem projections separadas
- Migração de schema de eventos = upcasting
- Mais complexo de operar
- Snapshots necessários para aggregates longos

### Snapshots

Recriar Order a partir de 10k eventos é caro. Solução: **snapshot** do estado a cada N eventos.

```
Eventos 1..100 → snapshot v1 → eventos 101..200 → snapshot v2 → eventos 201..220
```

Carregar = último snapshot + eventos posteriores.

### Quando usar
- Domínios com forte exigência de auditoria (financeiro, saúde, jurídico)
- Análise temporal (BI sobre estado histórico)
- CQRS com múltiplas read models distintas

### Quando NÃO usar
- CRUD simples
- Time não familiar (curva alta, refactor doloroso)
- Latência de leitura crítica (projections eventualmente consistentes)

### Bibliotecas .NET
- **Marten** (sobre PostgreSQL) — mais polido para times .NET
- **EventStoreDB** — produto dedicado
- **Wolverine** — framework moderno com event sourcing

---

## 7. Change Data Capture (Debezium)

CDC lê o **transaction log** (WAL no Postgres, CDC do SQL Server) e publica mudanças como eventos no Kafka — **sem código de aplicação**.

```
SQL Server INSERT → Transaction Log → Debezium Connector → Kafka topic → Consumer
```

### Vantagens sobre Outbox manual
- **Zero código** na aplicação — Debezium faz tudo
- Captura mudanças feitas por SQL direto, batch jobs, outras apps
- Captura **DELETEs físicos** (Outbox tradicional não captura)
- Throughput: limitado pelo WAL, não pelo app

### Desvantagens
- Infra extra: Debezium + Kafka Connect cluster
- Eventos refletem **schema do DB**, não modelo de domínio (vazamento)
- Schema evolution mais delicada

### Setup (Conceitual)

```yaml
# Debezium connector config para SQL Server
{
  "name": "orders-cdc",
  "config": {
    "connector.class": "io.debezium.connector.sqlserver.SqlServerConnector",
    "database.hostname": "sqlserver",
    "database.dbname": "OrdersDb",
    "table.include.list": "dbo.Orders,dbo.OrderItems",
    "topic.prefix": "orderflow",
    "schema.history.internal.kafka.bootstrap.servers": "kafka:9092"
  }
}
```

Cada mudança vira um evento Kafka:

```json
{
  "before": null,
  "after": { "id": "abc-123", "status": "Pending", "total": 99.90 },
  "op": "c",   // c=create, u=update, d=delete
  "ts_ms": 1714000000000
}
```

### Padrão Recomendado: Outbox + CDC

Use a tabela Outbox como **fonte de verdade** dos eventos de domínio (em vez de raw tables). Debezium captura **só a Outbox** — você ganha throughput **e** mantém eventos de domínio limpos.

---

## 8. Decisão: Outbox vs CDC vs Event Sourcing

| Necessidade | Outbox (Fase 05) | CDC (Debezium) | Event Sourcing |
|---|---|---|---|
| At-least-once entre DB e broker | ✅ | ✅ | N/A (eventos são fonte) |
| Sem 2PC | ✅ | ✅ | ✅ |
| Captura mudanças por SQL direto | ❌ | ✅ | ❌ |
| Histórico completo / auditoria | ⚠️ Apenas eventos publicados | ⚠️ Idem | ✅✅✅ |
| Time travel / replay | ❌ | ⚠️ Limitado | ✅ |
| Complexidade operacional | Baixa | Média (Debezium cluster) | Alta |
| Complexidade de modelagem | Baixa | Baixa | Alta |
| Throughput | Médio | Alto | Médio (snapshots ajudam) |
| **Quando começar** | Sempre que precisar de eventos transacionais | Ao escalar Outbox além de ~10k events/s | Domínio com auditoria forte ou múltiplas projections |

> **Regra prática:** **Outbox** sempre. Adicione **CDC** quando Outbox virar gargalo. **Event Sourcing** apenas se o domínio exigir.

---

## 9. 💼 Perguntas de Entrevista

**1. "Quando usar gRPC em vez de REST?"**
— gRPC para **comunicação interna** com baixa latência, contratos fortes (Protobuf), streaming. REST/JSON para APIs **públicas** (browser, terceiros). Padrão híbrido: REST externo via Gateway, gRPC entre microserviços.

**2. "Diferencie Kafka e RabbitMQ. Quando cada?"**
— RabbitMQ: broker tradicional, roteamento rico, ack por mensagem, baixa latência, fila descartada após consumo. Ideal para **comandos e eventos transacionais**. Kafka: log distribuído, retenção temporal, replay, particionamento, milhões msg/s. Ideal para **event sourcing, CDC, analytics, streaming**.

**3. "Por que ordem de mensagens é problemática em sistemas distribuídos?"**
— Múltiplos consumers paralelos perdem ordem. Soluções: (1) Partitioning por chave (Kafka — ordem garantida **por partição**). (2) RabbitMQ: 1 consumer por fila ou message grouping. (3) Aceitar fora de ordem e usar **vector clock** ou versão de evento.

**4. "O que é Event Sourcing? Quando vale o esforço?"**
— Persistir **eventos** em vez de estado. Estado = `reduce(eventos)`. Vale para: domínios com auditoria forte, time travel, múltiplas projections distintas. Não vale para: CRUD simples, time inexperiente, latência de leitura crítica.

**5. "Como CDC se compara ao Outbox?"**
— Outbox: tabela transacional + worker publica. **CDC** (Debezium): lê transaction log, sem código. Vantagens CDC: zero impacto no app, captura mudanças por SQL direto e DELETEs, throughput maior. Trade-off: infra extra (Debezium cluster), eventos refletem schema do DB. Combinação ótima: Debezium leu **só a tabela Outbox** — eventos de domínio limpos + throughput.

**6. "Snapshot em Event Sourcing — por quê e quando?"**
— Recriar aggregate a partir de 10k eventos é caro. Snapshot = estado materializado a cada N eventos. Carregar = snapshot mais recente + eventos posteriores. Quando: aggregate com > 100 eventos típicos, carregamento síncrono em hot path.

**7. "Como você lidaria com schema evolution em Kafka/Event Sourcing?"**
— Kafka: **Schema Registry** (Avro/Protobuf) com regras de compatibilidade (BACKWARD/FORWARD). Event Sourcing: **upcasting** — quando carrega evento v1, transforma para v2 em memória. **Nunca** edite eventos persistidos — transforme na leitura.

---

## Checkpoint

✅ Catalog API expondo gRPC `CatalogService` com Unary + Server streaming
✅ Orders chamando Catalog via gRPC com Polly resilience handler
✅ Tópico Kafka `orderflow.orders.events` recebendo eventos via Outbox→Debezium
✅ POC de Event Sourcing com Marten para um aggregate (opcional)
✅ ADR comparando Outbox vs CDC vs Event Sourcing para o projeto

➡️ **Próxima fase:** [`fase-14-feature-flags-sre.md`](./fase-14-feature-flags-sre.md)
