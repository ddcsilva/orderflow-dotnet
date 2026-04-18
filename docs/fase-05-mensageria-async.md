# Fase 5 — Mensageria Assíncrona com RabbitMQ e MassTransit

> **Objetivo:** Implementar comunicação assíncrona entre serviços usando RabbitMQ + MassTransit, com Outbox Pattern para garantia de entrega, consumers idempotentes, retry/DLQ e o Notification Worker.

> **Pré-requisito:** Fase 4 concluída (Identity API com JWT).

### 🎯 O que você vai aprender nesta fase

- Configurar **RabbitMQ** com MassTransit como abstração de mensageria
- Implementar **Outbox Pattern** para entrega garantida de eventos
- Criar **Consumers** idempotentes com deduplicação
- Configurar **Retry** com backoff exponencial e **Dead Letter Queue**
- Publicar **Integration Events** a partir de Domain Events
- Construir o **Notification Worker** como consumer de eventos

---

## Sumário

1. [Visão da Fase](#1-visão-da-fase)
2. [Decisões Arquiteturais](#2-decisões-arquiteturais)
3. [Conceitos de Mensageria](#3-conceitos-de-mensageria)
4. [Passo a Passo de Implementação](#4-passo-a-passo-de-implementação)
5. [Código de Referência Completo](#5-código-de-referência-completo)
6. [Testes](#6-testes)
7. [Checkpoint](#7-checkpoint)

---

## 1. Visão da Fase

### O Que Vamos Construir

```
src/Services/Orders/OrderFlow.Orders.Infrastructure/
├── Messaging/
│   ├── IntegrationEvents/
│   │   ├── OrderCreatedIntegrationEvent.cs
│   │   └── OrderConfirmedIntegrationEvent.cs
│   └── DomainEventToIntegrationMapper.cs

src/BuildingBlocks/
└── OrderFlow.Contracts/                    ← Contracts compartilhados
    ├── IntegrationEvents/
    │   ├── OrderCreated.cs
    │   ├── OrderConfirmed.cs
    │   └── OrderCancelled.cs
    └── OrderFlow.Contracts.csproj

src/Services/Notifications/
└── OrderFlow.Notifications.Worker/          ← NOVO
    ├── Consumers/
    │   ├── OrderCreatedConsumer.cs
    │   ├── OrderConfirmedConsumer.cs
    │   └── OrderCancelledConsumer.cs
    ├── Services/
    │   ├── INotificationService.cs
    │   └── EmailNotificationService.cs
    └── Program.cs
```

### Tópicos Cobertos

| Tópico | Detalhe |
|--------|---------|
| **RabbitMQ** | Broker de mensagens, exchanges, queues, bindings |
| **MassTransit** | Abstração sobre RabbitMQ, consumers, sagas |
| **Outbox Pattern** | Garantia de entrega atômica (EF Core + Outbox) |
| **Integration Events** | Comunicação entre bounded contexts |
| **Consumers** | Processamento de mensagens com retry automático |
| **Idempotência** | Consumers seguros para reprocessamento |
| **Dead Letter Queue** | Tratamento de mensagens que falham definitivamente |
| **Worker Service** | Background service para processamento assíncrono |

---

## 2. Decisões Arquiteturais

### ADR-011: MassTransit sobre RabbitMQ Client Puro

> 🧠 **Analogia — O Motorista de App vs Carro Próprio:** Usar `RabbitMQ.Client` direto é como ter **carro próprio**: você controla tudo (motor, rota, velocidade), mas também cuida de tudo (mecânico, seguro, multa). MassTransit é como usar um **app de transporte**: você diz "quero ir do ponto A ao B" e ele cuida da rota, retry se o motorista cancelar, aviso se houve problema, e você troca de empresa (de RabbitMQ para Azure Service Bus) sem mudar seu hábito. Para 99% dos projetos .NET, o app é a escolha certa.

**Contexto:** Podemos usar o `RabbitMQ.Client` diretamente ou o MassTransit como abstração.

**Decisão:** MassTransit.

**Justificativa:**
- Retry policies, circuit breaker e DLQ automáticos
- Serialização/deserialização automática
- Outbox pattern com EF Core built-in
- Topologia automática (cria exchanges e queues)
- Saga/State Machine se precisar futuramente
- É o padrão da indústria .NET

### ADR-012: Outbox Pattern

> 🧠 **Analogia — A Carta Registrada dos Correios:** Quando você envia uma carta normal, ela pode se perder no caminho e você nunca saberá. Agora, com **carta registrada**, o correio anota no sistema que existe uma carta para enviar *no momento em que você a entrega no balcão*. Mesmo que o caminhão quebre, a carta está registrada e será reenviada. O Outbox Pattern faz exatamente isso: em vez de publicar o evento direto no RabbitMQ (e torcer pra dar certo), salvamos o evento **junto com os dados de negócio na mesma transação do banco**. Um serviço de background fica varrendo o "outbox" e publicando as mensagens pendentes. **Nunca perde evento.**

**Contexto:** Ao confirmar um pedido, precisamos: (1) salvar no banco, (2) publicar evento. Se a publicação falhar depois do commit, perdemos o evento.

**Decisão:** Outbox Pattern — salva o evento como mensagem na mesma transação do banco. Um processo separado publica as mensagens pendentes.

```
                    ┌──────────────────────────────────────────┐
                    │           MESMA TRANSAÇÃO                │
                    │                                          │
   ConfirmOrder ──▶│  1. UPDATE Orders SET Status='Confirmed' │
     Handler       │  2. INSERT INTO OutboxMessages (event)   │
                    │  3. COMMIT                               │
                    └──────────────────┬───────────────────────┘
                                       │
                                       ▼
                    ┌──────────────────────────────────────────┐
                    │    MassTransit Outbox Delivery Service   │
                    │    (background, periodic)                │
                    │                                          │
                    │  1. SELECT FROM OutboxMessages           │
                    │  2. PUBLISH to RabbitMQ                  │
                    │  3. Mark as delivered                    │
                    └──────────────────────────────────────────┘
```

**Sem Outbox (PROBLEMA):**
```
1. SaveChanges() ✅ → Pedido confirmado no banco
2. Publish(event) ❌ → RabbitMQ caiu! Evento perdido!
   → Inconsistência: pedido confirmado mas ninguém sabe
```

**Com Outbox (SOLUÇÃO):**
```
1. SaveChanges() ✅ → Pedido + OutboxMessage salvos JUNTOS
2. Outbox Service → Publica eventualmente ao RabbitMQ
   → Se falhar, tenta de novo. NUNCA perde.
```

### ADR-013: Contracts como Projeto Compartilhado

**Contexto:** Consumers precisam conhecer o formato das mensagens para desserializar.

**Decisão:** Projeto `OrderFlow.Contracts` com records das integration events, referenciado por publishers e consumers.

```
OrderFlow.Contracts (shared)
  ├── OrderCreated { OrderId, OrderNumber, CustomerId, TotalAmount }
  ├── OrderConfirmed { OrderId, TotalAmount }
  └── OrderCancelled { OrderId, Reason }

Orders.Infrastructure ---references---> Contracts (publica)
Notifications.Worker  ---references---> Contracts (consome)
```

---

## 3. Conceitos de Mensageria

> 💡 **Antes de mergulhar:** Mensageria é como transformar uma empresa de telefonemas (síncronos: "fica na linha enquanto eu processo") em uma empresa de **emails** (assíncronos: "mando a mensagem, você processa quando puder, e me responde depois"). O preço? Complexidade. O ganho? **Resiliência** (se um serviço cair, as mensagens ficam na fila esperando) e **escala** (10 workers consumindo a mesma fila = 10x mais throughput).

### Exchange, Queue, Binding no RabbitMQ

> 🧠 **Analogia — Os Correios com Departamentos:** O **Exchange** é a central de triagem dos Correios. A **Queue** é a caixa de correio de cada destinatário. O **Binding** é a regra de encaminhamento ("cartas com CEP 01000 vão pra filial Centro"). Quando você publica uma mensagem, ela vai pro Exchange, que decide em quais Queues colocar baseado nos bindings. O consumer lê da sua Queue.

```
Producer ──▶ Exchange ──(routing)──▶ Queue ──▶ Consumer

Tipos de Exchange:
  • fanout:  Envia para TODAS as queues ligadas
  • direct:  Envia para queues com routing key exata
  • topic:   Envia para queues com routing key com padrão (*.order.#)

MassTransit usa fanout exchange por padrão (publish/subscribe).
Cada consumer type ganha sua própria queue.
```

> ⚠️ **Produção: Quorum Queues.** Por padrão, RabbitMQ cria **classic queues**. Para produção, use **quorum queues** (`x-queue-type: quorum`) — elas replicam dados entre nós do cluster, garantindo **alta disponibilidade** e **durabilidade** mesmo se um nó cair. No MassTransit, configure via `endpointConfigurator.SetQuorumQueue()` no ConsumerDefinition. Quorum queues são recomendadas para qualquer fila que não pode perder mensagens.

### At-Least-Once Delivery

> 🧠 **Analogia — O Entregador Insistente:** Imagine um entregador que só marca "entregue" quando você **assina o recibo**. Se ele bate na porta e você não abre, ele **volta amanhã**. E de novo. E de novo. Eventualmente você recebe. Mas às vezes ele volta mesmo depois que você já assinou (erro de sistema). Por isso, **você** precisa ser esperto: se já recebeu, não abre o pacote de novo. Isso é idempotência.

RabbitMQ garante entrega **pelo menos uma vez**. Isso significa que um consumer **pode receber a mesma mensagem mais de uma vez**. Por isso, consumers devem ser **idempotentes**.

```csharp
// ❌ NÃO idempotente — processa duplicata
public async Task Consume(ConsumeContext<OrderCreated> context)
{
    await _emailService.SendAsync(context.Message.CustomerEmail, "Pedido criado!");
    // Se receber duas vezes → envia email duas vezes!
}

// ✅ Idempotente — verifica se já processou
public async Task Consume(ConsumeContext<OrderCreated> context)
{
    if (await _repository.WasProcessedAsync(context.MessageId))
        return; // Já processou, ignora

    await _emailService.SendAsync(context.Message.CustomerEmail, "Pedido criado!");
    await _repository.MarkAsProcessedAsync(context.MessageId);
}
```

---

## 4. Passo a Passo de Implementação

### 4.1 Criar Projetos

```bash
# Contracts (shared)
dotnet new classlib -n OrderFlow.Contracts -o src/BuildingBlocks/OrderFlow.Contracts
dotnet sln add src/BuildingBlocks/OrderFlow.Contracts

# Notification Worker
dotnet new worker -n OrderFlow.Notifications.Worker -o src/Services/Notifications/OrderFlow.Notifications.Worker
dotnet sln add src/Services/Notifications/OrderFlow.Notifications.Worker
dotnet add src/Services/Notifications/OrderFlow.Notifications.Worker reference src/BuildingBlocks/OrderFlow.Contracts

# Pacotes — Orders Infrastructure
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure package MassTransit.RabbitMQ
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure package MassTransit.EntityFrameworkCore
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure reference src/BuildingBlocks/OrderFlow.Contracts

# Pacotes — Notification Worker
dotnet add src/Services/Notifications/OrderFlow.Notifications.Worker package MassTransit.RabbitMQ
dotnet add src/Services/Notifications/OrderFlow.Notifications.Worker package StackExchange.Redis
dotnet add src/Services/Notifications/OrderFlow.Notifications.Worker package Serilog.Extensions.Hosting
dotnet add src/Services/Notifications/OrderFlow.Notifications.Worker package Serilog.Sinks.Seq
dotnet add src/Services/Notifications/OrderFlow.Notifications.Worker package Serilog.Sinks.Console
```

### 4.2 Docker Compose — RabbitMQ

Adicione ao `docker-compose.yml` existente:

```yaml
  rabbitmq:
    image: rabbitmq:3-management-alpine
    container_name: orderflow-rabbitmq
    ports:
      - "5672:5672"     # AMQP
      - "15672:15672"   # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: orderflow
      RABBITMQ_DEFAULT_PASS: orderflow123
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  rabbitmq_data:
```

---

## 5. Código de Referência Completo

### 5.1 Contracts (Integration Events)

**`src/BuildingBlocks/OrderFlow.Contracts/IntegrationEvents/OrderCreated.cs`**

```csharp
namespace OrderFlow.Contracts.IntegrationEvents;

public sealed record OrderCreated
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime OccurredOn { get; init; }
}
```

**`src/BuildingBlocks/OrderFlow.Contracts/IntegrationEvents/OrderConfirmed.cs`**

```csharp
namespace OrderFlow.Contracts.IntegrationEvents;

public sealed record OrderConfirmed
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime OccurredOn { get; init; }
}
```

**`src/BuildingBlocks/OrderFlow.Contracts/IntegrationEvents/OrderCancelled.cs`**

```csharp
namespace OrderFlow.Contracts.IntegrationEvents;

public sealed record OrderCancelled
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime OccurredOn { get; init; }
}
```

### 5.2 Orders — Domain Event Handler que Publica Integration Event

Altere o handler existente para publicar via MassTransit:

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/EventHandlers/OrderCreatedDomainEventHandler.cs`**

```csharp
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Orders.EventHandlers;

public sealed class OrderCreatedDomainEventHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<OrderCreatedDomainEventHandler> logger)
    : INotificationHandler<OrderCreatedDomainEvent>
{
    public async Task Handle(OrderCreatedDomainEvent notification, CancellationToken ct)
    {
        logger.LogInformation(
            "Publishing OrderCreated integration event for OrderId={OrderId}",
            notification.OrderId);

        // TotalAmount é 0m neste momento porque o pedido acabou de ser criado
        // (itens são adicionados depois). O consumer deve tratar esse valor.
        // Se precisar do total real, publique o integration event no OrderConfirmed.
        await publishEndpoint.Publish(new OrderCreated
        {
            OrderId = notification.OrderId,
            OrderNumber = notification.OrderNumber,
            CustomerId = notification.CustomerId,
            TotalAmount = notification.TotalAmount,
            OccurredOn = notification.OccurredOn
        }, ct);
    }
}
```

**Novo: `OrderConfirmedDomainEventHandler.cs`**

```csharp
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Orders.EventHandlers;

public sealed class OrderConfirmedDomainEventHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<OrderConfirmedDomainEventHandler> logger)
    : INotificationHandler<OrderConfirmedDomainEvent>
{
    public async Task Handle(OrderConfirmedDomainEvent notification, CancellationToken ct)
    {
        logger.LogInformation(
            "Publishing OrderConfirmed integration event for OrderId={OrderId}",
            notification.OrderId);

        await publishEndpoint.Publish(new OrderConfirmed
        {
            OrderId = notification.OrderId,
            OrderNumber = notification.OrderNumber,
            TotalAmount = notification.TotalAmount,
            OccurredOn = notification.OccurredOn
        }, ct);
    }
}
```

### 5.3 Configuração MassTransit no Orders API com Outbox

**Atualizar `src/Services/Orders/OrderFlow.Orders.Infrastructure/DependencyInjection.cs`:**

```csharp
using System.Data;
using MassTransit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Orders.Application.Common.Interfaces;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Persistence.Repositories;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException("Connection string 'OrdersDb' not found.");

        // EF Core (Write side)
        services.AddDbContext<OrdersDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(OrdersDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(3);
            }));

        // Dapper (Read side)
        services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // DbContext como alias — necessário para TransactionBehavior (fase-03)
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

        // MassTransit + RabbitMQ + EF Core Outbox
        services.AddMassTransit(cfg =>
        {
            // Outbox com EF Core
            cfg.AddEntityFrameworkOutbox<OrdersDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();   // Publica mensagens do outbox automaticamente
            });

            // Aplica o outbox a todos os endpoints (consumer-side inbox para deduplicação)
            cfg.AddConfigureEndpointsCallback((context, name, endpointCfg) =>
            {
                endpointCfg.UseEntityFrameworkOutbox<OrdersDbContext>(context);
            });

            cfg.UsingRabbitMq((context, rabbitCfg) =>
            {
                rabbitCfg.Host(configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost", "/", h =>
                {
                    h.Username(configuration.GetValue<string>("RabbitMQ:Username") ?? "orderflow");
                    h.Password(configuration.GetValue<string>("RabbitMQ:Password") ?? "orderflow123");
                });

                rabbitCfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
```

### 5.4 Notification Worker — Consumers

**`src/Services/Notifications/OrderFlow.Notifications.Worker/Consumers/OrderCreatedConsumer.cs`**

```csharp
using MassTransit;
using OrderFlow.Contracts.IntegrationEvents;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderCreatedConsumer(
    ILogger<OrderCreatedConsumer> logger)
    : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Processing OrderCreated: OrderId={OrderId}, OrderNumber={OrderNumber}, CustomerId={CustomerId}",
            message.OrderId, message.OrderNumber, message.CustomerId);

        // TODO: Na vida real, buscar email do customer e enviar notificação
        // Simulação de envio de email
        await Task.Delay(100, context.CancellationToken); // Simula I/O

        logger.LogInformation(
            "Notification sent for new order {OrderNumber}", message.OrderNumber);
    }
}
```

**`src/Services/Notifications/OrderFlow.Notifications.Worker/Consumers/OrderConfirmedConsumer.cs`**

```csharp
using MassTransit;
using OrderFlow.Contracts.IntegrationEvents;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderConfirmedConsumer(
    ILogger<OrderConfirmedConsumer> logger)
    : IConsumer<OrderConfirmed>
{
    public async Task Consume(ConsumeContext<OrderConfirmed> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Processing OrderConfirmed: OrderId={OrderId}, Total={TotalAmount:C}",
            message.OrderId, message.TotalAmount);

        // Enviar confirmação por email
        await Task.Delay(100, context.CancellationToken);

        logger.LogInformation(
            "Confirmation notification sent for order {OrderId}", message.OrderId);
    }
}
```

**`src/Services/Notifications/OrderFlow.Notifications.Worker/Consumers/OrderCancelledConsumer.cs`**

```csharp
using MassTransit;
using OrderFlow.Contracts.IntegrationEvents;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderCancelledConsumer(
    ILogger<OrderCancelledConsumer> logger)
    : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Processing OrderCancelled: OrderId={OrderId}, Reason={Reason}",
            message.OrderId, message.Reason);

        // Enviar notificação de cancelamento
        await Task.Delay(100, context.CancellationToken);

        logger.LogInformation(
            "Cancellation notification sent for order {OrderId}", message.OrderId);
    }
}
```

### 5.5 Consumer com Idempotência Real

Para produção, adicione idempotência com um filtro. A implementação recomendada usa **Redis** (`SADD`/`SISMEMBER`), que funciona com múltiplas instâncias e sobrevive a restarts:

**`src/Services/Notifications/OrderFlow.Notifications.Worker/Filters/IdempotentConsumerFilter.cs`**

```csharp
using MassTransit;
using StackExchange.Redis;

namespace OrderFlow.Notifications.Worker.Filters;

/// <summary>
/// Filtro que garante processamento idempotente usando MessageId + Redis.
/// </summary>
public sealed class IdempotentConsumerFilter<T>(
    IConnectionMultiplexer redis,
    ILogger<IdempotentConsumerFilter<T>> logger)
    : IFilter<ConsumeContext<T>> where T : class
{
    private static readonly TimeSpan KeyExpiration = TimeSpan.FromHours(24);

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();
        var db = redis.GetDatabase();
        var key = $"processed:{typeof(T).Name}:{messageId}";

        // SETNX atômico: retorna true se a chave NÃO existia
        if (!await db.StringSetAsync(key, "1", KeyExpiration, When.NotExists))
        {
            logger.LogWarning(
                "Duplicate message detected: {MessageId} of type {MessageType}. Skipping.",
                messageId, typeof(T).Name);
            return;
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("idempotent-consumer");
    }
}
```

> 💡 **Alternativa simplificada (sem Redis):** Se você não tem Redis disponível, pode usar `HashSet<Guid>` em memória como fallback educacional. Porém, esse state é **perdido ao reiniciar** e não funciona com múltiplas instâncias. Outra opção é uma tabela `ProcessedMessages` no banco de dados com índice único no `MessageId`.

### 5.6 Consumer Definitions (Retry + Error Queue)

**`src/Services/Notifications/OrderFlow.Notifications.Worker/Consumers/OrderCreatedConsumerDefinition.cs`**

```csharp
using MassTransit;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderCreatedConsumerDefinition : ConsumerDefinition<OrderCreatedConsumer>
{
    public OrderCreatedConsumerDefinition()
    {
        EndpointName = "order-created-notifications";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Retry: 3 tentativas com intervalo exponencial
        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Intervals(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15));
            r.Ignore<ArgumentException>(); // Não retry para erros de validação
        });

        // Circuit breaker: abre após 5 falhas consecutivas
        endpointConfigurator.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 5;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });

        // Para usar o IdempotentConsumerFilter (seção 5.5), registre assim:
        // endpointConfigurator.UseFilter(
        //     new IdempotentConsumerFilter<OrderCreated>(redis, loggerFactory.CreateLogger<...>()));
    }
}
```

### 5.7 Notification Worker — Program.cs

**`src/Services/Notifications/OrderFlow.Notifications.Worker/Program.cs`**

```csharp
using MassTransit;
using OrderFlow.Notifications.Worker.Consumers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Serilog
builder.Services.AddSerilog(cfg => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341"));

// MassTransit
builder.Services.AddMassTransit(cfg =>
{
    cfg.SetKebabCaseEndpointNameFormatter();

    // Registrar todos os consumers do assembly
    cfg.AddConsumers(typeof(Program).Assembly);

    cfg.UsingRabbitMq((context, rabbitCfg) =>
    {
        rabbitCfg.Host(
            builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "orderflow");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "orderflow123");
        });

        rabbitCfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
```

**`src/Services/Notifications/OrderFlow.Notifications.Worker/appsettings.json`**

```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "orderflow",
    "Password": "orderflow123"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "MassTransit": "Information",
        "Microsoft": "Warning"
      }
    }
  }
}
```

### 5.8 Outbox Migration

O MassTransit EF Core Outbox precisa de tabelas no banco. Adicione à migration:

```bash
# Após configurar o outbox, crie migration
dotnet ef migrations add AddOutboxState \
  -p src/Services/Orders/OrderFlow.Orders.Infrastructure \
  -s src/Services/Orders/OrderFlow.Orders.Api

dotnet ef database update \
  -p src/Services/Orders/OrderFlow.Orders.Infrastructure \
  -s src/Services/Orders/OrderFlow.Orders.Api
```

Ou adicione manualmente ao `OrdersDbContext.OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

    // MassTransit Outbox tables
    modelBuilder.AddInboxStateEntity();
    modelBuilder.AddOutboxMessageEntity();
    modelBuilder.AddOutboxStateEntity();
}
```

---

## 6. Testes

### 6.1 Testes de Consumer com MassTransit Test Harness

```bash
dotnet new xunit -n OrderFlow.Notifications.Worker.Tests -o tests/OrderFlow.Notifications.Worker.Tests
dotnet sln add tests/OrderFlow.Notifications.Worker.Tests
dotnet add tests/OrderFlow.Notifications.Worker.Tests reference src/Services/Notifications/OrderFlow.Notifications.Worker
dotnet add tests/OrderFlow.Notifications.Worker.Tests package FluentAssertions
dotnet add tests/OrderFlow.Notifications.Worker.Tests package MassTransit.Testing
```

**`tests/OrderFlow.Notifications.Worker.Tests/Consumers/OrderCreatedConsumerTests.cs`**

```csharp
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Notifications.Worker.Consumers;

namespace OrderFlow.Notifications.Worker.Tests.Consumers;

public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task Consume_ValidMessage_ProcessesSuccessfully()
    {
        await using var provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<OrderCreatedConsumer>>())
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<OrderCreatedConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Publicar mensagem
        await harness.Bus.Publish(new OrderCreated
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-20260415-AB123",
            CustomerId = Guid.NewGuid(),
            TotalAmount = 250.00m,
            OccurredOn = DateTime.UtcNow
        });

        // Verificar que o consumer recebeu
        (await harness.Consumed.Any<OrderCreated>()).Should().BeTrue();

        var consumerHarness = harness.GetConsumerHarness<OrderCreatedConsumer>();
        (await consumerHarness.Consumed.Any<OrderCreated>()).Should().BeTrue();
    }

    [Fact]
    public async Task Consume_MultipleMessages_ProcessesAll()
    {
        await using var provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<OrderCreatedConsumer>>())
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<OrderCreatedConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Publicar 5 mensagens
        for (int i = 0; i < 5; i++)
        {
            await harness.Bus.Publish(new OrderCreated
            {
                OrderId = Guid.NewGuid(),
                OrderNumber = $"ORD-2026-{i:D5}",
                CustomerId = Guid.NewGuid(),
                TotalAmount = 100m * i,
                OccurredOn = DateTime.UtcNow
            });
        }

        // Aguardar processamento
        (await harness.Consumed.SelectAsync<OrderCreated>().Count()).Should().Be(5);
    }
}
```

### 6.2 Teste de Integração do Outbox

**`tests/OrderFlow.Orders.Application.Tests/EventHandlers/OrderCreatedDomainEventHandlerTests.cs`**

```csharp
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Orders.Application.Orders.EventHandlers;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Tests.EventHandlers;

public class OrderCreatedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_DomainEvent_PublishesIntegrationEvent()
    {
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<OrderCreatedDomainEventHandler>>();
        var handler = new OrderCreatedDomainEventHandler(publishEndpointMock.Object, loggerMock.Object);

        var domainEvent = new OrderCreatedDomainEvent(
            Guid.NewGuid(), "ORD-20260415-XYZ", Guid.NewGuid(), 0m);

        await handler.Handle(domainEvent, CancellationToken.None);

        publishEndpointMock.Verify(p => p.Publish(
            It.Is<OrderCreated>(e =>
                e.OrderId == domainEvent.OrderId &&
                e.OrderNumber == domainEvent.OrderNumber &&
                e.CustomerId == domainEvent.CustomerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

## 7. Checkpoint

> 💡 **Por que isso importa no dia-a-dia?** Mensageria é o que separa **monólitos acoplados** de **sistemas distribuídos resilientes**. Sem mensageria, se o serviço de notificação cai enquanto um pedido é confirmado, o cliente nunca recebe email. Com mensageria + outbox, o evento fica na fila esperando o serviço voltar — **zero perda de dados**. Em entrevistas sênior, a pergunta não é "você sabe usar RabbitMQ?" — é "como você garante que nenhum evento se perde entre serviços?". A resposta é Outbox + idempotência.

### Validação Completa

- [ ] **RabbitMQ rodando:** `docker compose up rabbitmq`
- [ ] **Management UI acessível:** http://localhost:15672 (orderflow/orderflow123)
- [ ] **Contracts criado:** Integration events compartilhados
- [ ] **MassTransit configurado:** No Orders API com RabbitMQ
- [ ] **Outbox Pattern:** EF Core Outbox configurado e com migration
- [ ] **Domain Event → Integration Event:** Handler publica via MassTransit
- [ ] **Notification Worker:** Consumers rodando e processando mensagens
- [ ] **Consumer Definitions:** Retry, circuit breaker, concurrency
- [ ] **Idempotência:** Filtro ou verificação de MessageId
- [ ] **Dead Letter Queue:** Mensagens que falham 3x vão para `_error` queue
- [ ] **Testes com Test Harness:** Consumers testados in-memory
- [ ] **Commit:** `feat(messaging): implement RabbitMQ with MassTransit, outbox pattern and notification worker`

### Comandos de Verificação

```bash
# Subir infraestrutura
docker compose up -d rabbitmq

# Rodar o worker (em terminal separado)
dotnet run --project src/Services/Notifications/OrderFlow.Notifications.Worker

# Rodar API
dotnet run --project src/Services/Orders/OrderFlow.Orders.Api

# Criar pedido (vai publicar evento)
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"...", "street":"Rua Teste", "number":"100", ...}'

# Verificar no RabbitMQ Management UI
# http://localhost:15672 → Queues → ver mensagens

# Testes
dotnet test tests/OrderFlow.Notifications.Worker.Tests --verbosity normal
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| Integration Events | `OrderCreatedIntegrationEvent.cs`, `OrderConfirmedIntegrationEvent.cs` |
| Event Publisher | `IntegrationEventPublisher.cs` (publica via MassTransit) |
| Outbox | `OutboxMessage.cs`, `OutboxProcessor.cs` (Background Service) |
| Consumers | `OrderCreatedConsumer.cs`, `OrderConfirmedConsumer.cs` |
| Idempotency | `ProcessedMessage.cs` entity + check no consumer |
| MassTransit Config | `MassTransitConfig.cs` (RabbitMQ, retry, DLQ) |
| Notification Worker | Projeto `OrderFlow.Notifications.Worker` (Hosted Service) |
| Docker | RabbitMQ no `docker-compose.yml` com management plugin |
| Testes | `OutboxProcessorTests.cs`, `ConsumerIdempotencyTests.cs` |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 5

**1. "O que é Outbox Pattern e por que é necessário?"**
— Sem Outbox, temos o problema de **dual write**: salvar no banco E publicar evento são operações distintas. Se o banco salva mas o RabbitMQ falha, o evento se perde. Outbox resolve: o evento é salvo **na mesma transação** que os dados (tabela `OutboxMessages`). Um Background Service lê e publica periodicamente. Pior caso: mensagem duplicada (resolve com idempotência).

**2. "Como garantir idempotência no consumer?"**
— Armazene o `MessageId` em uma tabela `ProcessedMessages`. Antes de processar, verifique se já existe. Se sim, retorne sucesso sem processar de novo. Isso funciona porque mensageria garante **at-least-once delivery** — nunca exactly-once. A lógica de negócio deve ser **idempotente por design**.

**3. "Qual a diferença entre Domain Event e Integration Event?"**
— **Domain Event** é local ao bounded context — `OrderCreatedEvent` é despachado e tratado no mesmo processo (via MediatR). **Integration Event** cruza fronteiras — `OrderCreatedIntegrationEvent` vai para o message broker (RabbitMQ) e é consumido por outros serviços. Domain Events são síncronos (in-process), Integration Events são assíncronos (inter-process).

**4. "O que é Dead Letter Queue e quando usar?"**
— DLQ é uma fila que recebe mensagens que **falharam após todas as tentativas de retry**. Em vez de descartar, a mensagem vai para a DLQ para análise posterior. Configure retries com **backoff exponencial** (1s, 2s, 4s, 8s) antes de mandar para DLQ. Use alarmes na DLQ para detectar problemas sistêmicos (ex: serviço externo fora do ar).

**5. "MassTransit vs usar RabbitMQ.Client direto — por quê?"**
— MassTransit é uma **abstração** que gerencia: serialização, retry, DLQ, saga/state machine, health checks, tracing. Com `RabbitMQ.Client` você implementa tudo manualmente. MassTransit também permite trocar de broker (RabbitMQ → Azure Service Bus) mudando **apenas configuração**. Trade-off: mais abstrato, curva de aprendizado inicial, mas ROI enorme em produção.

---

## 🔬 Aprofundamento Sênior

### A1. Saga: Orquestração vs Coreografia

| | Orquestração | Coreografia |
|---|---|---|
| Quem coordena | **State machine central** | Cada serviço reage a eventos |
| Visibilidade | Alta — fluxo num lugar | Baixa — espalhado |
| Acoplamento | Médio (todos conhecem o orquestrador) | Baixo (só conhecem eventos) |
| Debug | Fácil | Difícil (precisa tracing distribuído) |
| Quando | Fluxos complexos com decisões | Fluxos lineares simples |

**MassTransit Saga (orquestração):**

```csharp
public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    public State AwaitingPayment { get; private set; } = null!;
    public State Paid { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public OrderSaga()
    {
        InstanceState(s => s.CurrentState);
        Initially(
            When(OrderCreated)
                .Then(ctx => ctx.Saga.OrderId = ctx.Message.OrderId)
                .Publish(ctx => new RequestPayment(ctx.Saga.OrderId))
                .TransitionTo(AwaitingPayment));
        
        During(AwaitingPayment,
            When(PaymentSucceeded).TransitionTo(Paid),
            When(PaymentFailed).Publish(ctx => new CancelOrder(ctx.Saga.OrderId))
                                .TransitionTo(Failed));
    }
}
```

### A2. Outbox Avançado — Garantias e Trade-offs

Outbox dá **at-least-once**. Combinação com **idempotent consumer** chega perto de exactly-once.

```csharp
// Idempotência com Redis SETNX
public async Task ConsumeAsync(OrderConfirmed evt, CancellationToken ct)
{
    var key = $"processed:{evt.MessageId}";
    var firstTime = await _redis.StringSetAsync(key, "1",
        expiry: TimeSpan.FromDays(7),
        when: When.NotExists);
    if (!firstTime) return;   // duplicata — ignora
    
    await _handler.HandleAsync(evt, ct);
}
```

**Trade-off Outbox:** latência (worker poll) e contenção na tabela. Em alta escala (>10k events/s), considere **CDC** ([Fase 13](./fase-13-grpc-kafka-eventsourcing.md#7-change-data-capture-debezium)).

### A3. Exactly-Once é Mito

*"Exactly-once delivery is theoretically impossible in distributed systems."* — Kafka docs.

O que existe: **exactly-once processing** = at-least-once + idempotência. Aceite at-least-once e desenhe consumers idempotentes.

### A4. Versionamento de Eventos

Eventos são contratos — precisam evoluir sem quebrar consumers antigos:

```csharp
// V1
public record OrderConfirmedEvent(Guid OrderId, decimal Total);

// V2 — adiciona campo opcional, NÃO renomeia/remove
public record OrderConfirmedEventV2(Guid OrderId, decimal Total, string? Currency = "BRL");
```

**Regras:**
- Adicionar campo: ✅ com default
- Remover campo: ❌ — crie novo evento (V2) e mantenha publicação dupla por tempo
- Renomear: ❌ — mesmo argumento
- Mudar tipo: ❌ — novo evento

### A5. DLQ — Estratégia de Recuperação

Mensagem na DLQ = humano precisa decidir. Padrão de operação:

1. **Alerta** quando DLQ > N mensagens
2. **Dashboard** com mensagens da DLQ + erro
3. **Replay seletivo** após corrigir bug (botão na ferramenta interna)
4. **Postmortem** se DLQ acumulou — sintoma de qualidade

### A6. Schema Registry (Kafka)

Quando usar Kafka ([Fase 13](./fase-13-grpc-kafka-eventsourcing.md)), adote **Confluent Schema Registry** ou **Apicurio** com formato Avro/Protobuf:
- Compatibility check no produce — não publica evento incompatível
- Schema evolution governada (BACKWARD/FORWARD/FULL)

### 💼 Perguntas Sênior

**"Saga Orquestrada vs Coreografada — quando cada?"** — Orquestração para fluxos com 4+ etapas e decisões complexas (debug e visibilidade); Coreografia para fluxos lineares simples (acoplamento mínimo). Híbrido aceitável.

**"Como garantir ordem de eventos com múltiplos consumers?"** — RabbitMQ: 1 consumer por fila (perde paralelismo) ou message grouping. Kafka: particionar por chave (todos os eventos de `OrderId=X` na mesma partição = ordem garantida) + 1 consumer por partição.

---

> **Próximo passo:** Avance para `fase-06-cache-observabilidade.md`.
>
> 🚀 **Trilha Sênior:** [`fase-13-grpc-kafka-eventsourcing.md`](./fase-13-grpc-kafka-eventsourcing.md) — Kafka, Event Sourcing e CDC.
