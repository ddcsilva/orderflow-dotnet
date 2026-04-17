# OrderFlow — Visão Geral da Solução

> **Versão:** 1.0  
> **Última atualização:** Abril 2026  
> **Runtime:** .NET 10 / C# 13  
> **Tipo:** Projeto de Portfólio Enterprise

---

## Sumário

1. [O Que é o OrderFlow](#1-o-que-é-o-orderflow)
2. [Por Que Este Projeto Existe](#2-por-que-este-projeto-existe)
3. [Visão Arquitetural](#3-visão-arquitetural)
4. [Microserviços e Responsabilidades](#4-microserviços-e-responsabilidades)
5. [Stack Tecnológico e Justificativas](#5-stack-tecnológico-e-justificativas)
6. [Estrutura da Solution](#6-estrutura-da-solution)
7. [Fluxos Principais](#7-fluxos-principais)
8. [Padrões Arquiteturais Aplicados](#8-padrões-arquiteturais-aplicados)
9. [Mapeamento: Vagas → Projeto](#9-mapeamento-vagas--projeto)
10. [Roadmap de Fases](#10-roadmap-de-fases)
11. [Convenções do Projeto](#11-convenções-do-projeto)
12. [Como Rodar Localmente](#12-como-rodar-localmente)
13. [Glossário](#13-glossário)
14. [Checklist de Competências](#14-checklist-de-competências)
15. [Perguntas Frequentes em Entrevistas](#15-perguntas-frequentes-em-entrevistas)

---

## 1. O Que é o OrderFlow

O **OrderFlow** é um sistema simplificado de gestão de pedidos estruturado em **microserviços**. Ele simula o fluxo real de um e-commerce mínimo:

```
Usuário se cadastra → Faz login → Consulta produtos → Cria pedido → Pedido muda de status → Notificação é disparada
```

O sistema é **intencionalmente simples no domínio** (produtos e pedidos), mas **rico em arquitetura e infraestrutura**. Isso permite que você foque em **como** construir, não em **o que** construir.

### O Que NÃO é o OrderFlow

- **Não é um e-commerce real** — Não tem carrinho, pagamento, frete, estoque em tempo real
- **Não é para produção** — É um laboratório de aprendizado
- **Não é over-engineering sem propósito** — Cada decisão tem uma justificativa didática mapeada a um tópico de vaga

---

## 2. Por Que Este Projeto Existe

### O Problema

As vagas enterprise de .NET no Brasil pedem um conjunto consistente de habilidades:

| Nível | O que pedem |
|-------|-------------|
| **Júnior** | C#, ASP.NET Core, EF Core, SQL Server, APIs REST, Git |
| **Pleno** | + Clean Architecture, testes, Docker, SOLID, LINQ avançado |
| **Sênior** | + Microserviços, CQRS, DDD, mensageria, cache, CI/CD, Azure |

### A Solução

Um **único projeto** que exercita **todos esses tópicos** de forma integrada e coerente, com complexidade controlada para ser desenvolvido por **uma pessoa**.

### O Diferencial em Entrevistas

Em vez de dizer *"eu estudei microserviços"*, você mostra o repositório e diz:

> *"Eu construí um sistema com 4 microserviços, API Gateway com YARP, comunicação assíncrona via RabbitMQ com MassTransit, CQRS com MediatR, domínio rico com DDD, cache distribuído com Redis, observabilidade com OpenTelemetry, testes de integração com Testcontainers, e CI/CD com GitHub Actions. Posso te mostrar qualquer decisão arquitetural e explicar o porquê."*

---

## 3. Visão Arquitetural

### Diagrama de Alto Nível

```
                            ┌──────────────────┐
                            │   Cliente (HTTP)  │
                            └────────┬─────────┘
                                     │
                            ┌────────▼─────────┐
                            │   API Gateway     │
                            │     (YARP)        │
                            │                   │
                            │ • Rate Limiting   │
                            │ • Routing         │
                            │ • Auth Forwarding │
                            └──┬─────┬──────┬──┘
                               │     │      │
              ┌────────────────┘     │      └────────────────┐
              │                      │                       │
     ┌────────▼────────┐   ┌────────▼────────┐   ┌─────────▼────────┐
     │  Identity API   │   │  Catalog API    │   │   Orders API     │
     │                 │   │                 │   │                  │
     │ ASP.NET Identity│   │ Controllers     │   │ Minimal APIs     │
     │ JWT + Refresh   │   │ CRUD Produtos   │   │ CQRS + MediatR   │
     │ Roles/Claims    │   │ Cache Redis     │   │ DDD (Aggregates) │
     │                 │   │ FluentValidation│   │ Domain Events    │
     │  ┌───────────┐  │   │  ┌───────────┐  │   │  ┌────────────┐  │
     │  │ SQL Server │  │   │  │ SQL Server │  │   │  │ SQL Server │  │
     │  │ (Identity) │  │   │  │ (Catalog)  │  │   │  │ (Orders)   │  │
     │  └───────────┘  │   │  └───────────┘  │   │  └────────────┘  │
     └─────────────────┘   └─────────────────┘   └────────┬─────────┘
                                                          │
                                                 Publica eventos via
                                                 Outbox Pattern
                                                          │
                                                 ┌────────▼─────────┐
                                                 │    RabbitMQ      │
                                                 │  Message Broker  │
                                                 └────────┬─────────┘
                                                          │
                                                 ┌────────▼─────────┐
                                                 │  Notification    │
                                                 │  Worker Service  │
                                                 │                  │
                                                 │  Consumers       │
                                                 │  Background Jobs │
                                                 │  Idempotência    │
                                                 └──────────────────┘
    
    Infraestrutura Transversal:
    ┌──────────────────────────────────────────────────────────────┐
    │  Redis (Cache) │ Serilog + Seq (Logs) │ OpenTelemetry       │
    │  Health Checks │ Docker Compose       │ GitHub Actions      │
    └──────────────────────────────────────────────────────────────┘
```

### Princípios Arquiteturais

| Princípio | Aplicação |
|-----------|-----------|
| **Database per Service** | Cada microserviço tem seu próprio SQL Server database. Sem queries cross-database. |
| **Comunicação Assíncrona** | Serviços nunca se chamam diretamente via HTTP. Toda integração é via eventos no RabbitMQ. |
| **Smart Endpoints, Dumb Pipes** | A lógica está nos serviços, não no broker. RabbitMQ só transporta. |
| **Design for Failure** | Retry, DLQ, idempotência, circuit breaker. Tudo falha eventualmente. |
| **Observability First** | Structured logging, distributed tracing, health checks desde o dia 1. |

---

## 4. Microserviços e Responsabilidades

### 4.1 Identity API

**Responsabilidade:** Autenticação e autorização centralizada.

| Aspecto | Detalhe |
|---------|---------|
| **Framework** | ASP.NET Core Identity |
| **Auth** | JWT Bearer + Refresh Tokens |
| **Endpoints** | Register, Login, Refresh, Revoke, GetProfile |
| **Banco** | SQL Server (tabelas do Identity) |
| **Padrão** | Minimal APIs + Service Layer |

**Por que um serviço separado?**  
Em microserviços, centralizar autenticação permite que os outros serviços validem tokens sem acessar o banco de identity. Os outros serviços recebem o JWT e validam a assinatura localmente.

### 4.2 Catalog API

**Responsabilidade:** Gerenciamento do catálogo de produtos e categorias.

| Aspecto | Detalhe |
|---------|---------|
| **Framework** | ASP.NET Core Controllers (para praticar os dois estilos) |
| **Patterns** | Clean Architecture (4 camadas), Repository Pattern |
| **Cache** | Redis via IDistributedCache (cache-aside) |
| **Validação** | FluentValidation |
| **Banco** | SQL Server + EF Core (Code First) |

**Por que Controllers aqui e Minimal APIs no Orders?**  
Propositalmente, para praticar os dois approaches. No mercado, você encontra os dois. O Catalog usa Controllers com versionamento; o Orders usa Minimal APIs com extensões tipadas.

### 4.3 Orders API — O Serviço de Referência

**Responsabilidade:** Gestão completa do ciclo de vida de pedidos.

| Aspecto | Detalhe |
|---------|---------|
| **Framework** | ASP.NET Core Minimal APIs |
| **Patterns** | Clean Architecture + DDD + CQRS |
| **Commands** | CreateOrder, AddOrderItem, ConfirmOrder, CancelOrder, ShipOrder |
| **Queries** | GetOrderById, GetOrdersByCustomer, GetOrderSummary |
| **Escrita** | EF Core (change tracking, transactions) |
| **Leitura** | Dapper (performance, queries otimizadas) |
| **Eventos** | Domain Events (MediatR) + Integration Events (MassTransit) |
| **Banco** | SQL Server |

**Este é o serviço onde você mais aprende.** Aqui estão concentrados DDD, CQRS, domain events, saga simplificada, outbox pattern — tudo que diferencia um dev pleno de um sênior.

### 4.4 Notification Worker

**Responsabilidade:** Consumir eventos e executar ações de notificação.

| Aspecto | Detalhe |
|---------|---------|
| **Tipo** | Worker Service (BackgroundService) |
| **Mensageria** | MassTransit Consumers via RabbitMQ |
| **Ações** | Log de notificações, simulação de e-mail, webhooks |
| **Patterns** | Consumer idempotente, Retry, DLQ |

**Por que não uma API?**  
Notificações são fire-and-forget. Não precisam de endpoints HTTP. Um Worker Service é mais leve e focado.

### 4.5 API Gateway (YARP)

**Responsabilidade:** Ponto único de entrada, roteamento e cross-cutting concerns.

| Aspecto | Detalhe |
|---------|---------|
| **Framework** | YARP (Yet Another Reverse Proxy) |
| **Features** | Rate limiting, routing, header forwarding, CORS |
| **Configuração** | JSON-based, hot-reloadable |

**Por que YARP e não Ocelot?**  
YARP é mantido pela Microsoft, tem melhor performance, e é o padrão recomendado para .NET atualmente. Ocelot está em modo manutenção.

---

## 5. Stack Tecnológico e Justificativas

### Decisões Técnicas (Architecture Decision Records)

#### ADR-001: .NET 10 / C# 13

**Contexto:** Precisamos do runtime mais moderno para portfólio.  
**Decisão:** .NET 10 com C# 13.  
**Justificativa:** Primary constructors, collection expressions, nullable reference types habilitado por padrão. É a versão que o mercado está adotando.  
**Alternativa descartada:** .NET 8 LTS — ainda muito usado, mas para portfólio queremos mostrar que estamos atualizados.

#### ADR-002: EF Core + Dapper (lado a lado)

**Contexto:** Precisamos de ORM para escrita e queries performáticas para leitura.  
**Decisão:** EF Core para Commands (escrita), Dapper para Queries (leitura).  
**Justificativa:** EF Core dá change tracking, migrations, configurações fluent. Dapper dá performance bruta para leituras complexas. No CQRS, cada lado tem necessidades diferentes.  
**Alternativa descartada:** Só EF Core — funciona, mas perdemos a oportunidade de demonstrar Dapper e ter performance real.

#### ADR-003: MassTransit sobre RabbitMQ Client

**Contexto:** Precisamos de mensageria entre serviços.  
**Decisão:** MassTransit como abstração sobre RabbitMQ.  
**Justificativa:** MassTransit oferece outbox pattern nativo, retry policies, saga state machine, e test harness. Usar o RabbitMQ.Client diretamente exigiria implementar tudo isso na mão.  
**Alternativa descartada:** RabbitMQ.Client direto — mais controle, mas muito boilerplate. NServiceBus — enterprise, mas pago em funcionalidades avançadas.

#### ADR-004: MediatR para CQRS

**Contexto:** Precisamos separar Commands e Queries de forma organizada.  
**Decisão:** MediatR como mediador in-process.  
**Justificativa:** Padrão de mercado, pipeline behaviors para cross-cutting (validation, logging, transactions), separação clara de handlers.  
**Alternativa descartada:** Wolverine — mais moderno e performático, mas MediatR é o que aparece nas vagas.

#### ADR-005: YARP como API Gateway

**Contexto:** Precisamos de um ponto único de entrada.  
**Decisão:** YARP (Yet Another Reverse Proxy).  
**Justificativa:** Mantido pela Microsoft, performance superior, configuração simples via JSON, hot-reload de rotas.  
**Alternativa descartada:** Ocelot — popular, mas em manutenção e com performance inferior.

#### ADR-006: SQL Server por serviço

**Contexto:** Cada serviço precisa de seu próprio banco (Database per Service pattern).  
**Decisão:** SQL Server em Docker, um database por serviço.  
**Justificativa:** SQL Server é o banco mais pedido nas vagas .NET no Brasil. Docker simplifica setup local.  
**Alternativa descartada:** PostgreSQL — excelente, mas SQL Server é mais alinhado com vagas .NET. SQLite — usado apenas em testes.

#### ADR-007: Serilog + Seq + OpenTelemetry

**Contexto:** Precisamos de logging estruturado e observabilidade.  
**Decisão:** Serilog para structured logging, Seq para visualização, OpenTelemetry para traces distribuídos.  
**Justificativa:** Serilog é o padrão .NET para logs estruturados. Seq é gratuito para dev local. OpenTelemetry é vendor-neutral.  
**Alternativa descartada:** Application Insights diretamente — vendor lock-in com Azure.

### Tabela Completa da Stack

| Camada | Tecnologia | Versão | Por Que |
|--------|-----------|--------|---------|
| Runtime | .NET | 10 | Mais recente, features modernas de C# 13 |
| Web Framework | ASP.NET Core | 10 | Controllers + Minimal APIs |
| ORM (escrita) | Entity Framework Core | 10 | Change tracking, migrations, Fluent API |
| ORM (leitura) | Dapper | 2.x | Performance bruta para queries CQRS |
| Banco | SQL Server | 2022 | Padrão do mercado .NET Brasil |
| Cache | Redis | 7.x | Cache distribuído, cache-aside pattern |
| Mensageria | RabbitMQ | 4.x | Message broker com MassTransit |
| Abstração MSG | MassTransit | 8.x | Outbox, retry, saga, test harness |
| Mediator | MediatR | 12.x | CQRS, pipeline behaviors |
| Validação | FluentValidation | 11.x | Validação fluent, integração MediatR |
| API Gateway | YARP | 2.x | Reverse proxy performático |
| Auth | ASP.NET Identity | 10 | JWT Bearer + Refresh Tokens |
| Logging | Serilog | 4.x | Structured logging |
| Log Viewer | Seq | Docker | Dashboard de logs |
| Telemetria | OpenTelemetry | 1.x | Traces, metrics, logs correlacionados |
| Testes | xUnit | 2.x | Framework principal de testes .NET |
| Mocking | Moq | 4.x | Mock de dependências |
| Assertions | FluentAssertions | 7.x | Assertions legíveis |
| Integration Test | WebApplicationFactory | 10 | Teste de API in-memory |
| Test Containers | Testcontainers | 4.x | Containers para tests de integração |
| Container | Docker + Compose | Latest | Orquestração local |
| CI/CD | GitHub Actions | Latest | Pipeline automatizado |
| Cloud | Azure | Latest | Deploy final |

---

## 6. Estrutura da Solution

```
OrderFlow/
│
├── src/
│   │
│   ├── ApiGateway/
│   │   └── OrderFlow.Gateway/                     # YARP reverse proxy
│   │       ├── Program.cs
│   │       ├── appsettings.json                   # Rotas YARP
│   │       └── Dockerfile
│   │
│   ├── Services/
│   │   │
│   │   ├── Identity/
│   │   │   ├── OrderFlow.Identity.Api/            # Endpoints de auth
│   │   │   │   ├── Endpoints/
│   │   │   │   ├── Program.cs
│   │   │   │   └── Dockerfile
│   │   │   ├── OrderFlow.Identity.Application/    # Use cases, DTOs
│   │   │   │   ├── Commands/
│   │   │   │   ├── DTOs/
│   │   │   │   └── Services/
│   │   │   └── OrderFlow.Identity.Infrastructure/ # EF Core + Identity
│   │   │       ├── Data/
│   │   │       ├── Identity/
│   │   │       └── DependencyInjection.cs
│   │   │
│   │   ├── Catalog/
│   │   │   ├── OrderFlow.Catalog.Api/             # Controllers REST
│   │   │   │   ├── Controllers/
│   │   │   │   ├── Middleware/
│   │   │   │   ├── Program.cs
│   │   │   │   └── Dockerfile
│   │   │   ├── OrderFlow.Catalog.Application/     # Services, validators
│   │   │   │   ├── Services/
│   │   │   │   ├── DTOs/
│   │   │   │   └── Validators/
│   │   │   ├── OrderFlow.Catalog.Domain/          # Entities puras
│   │   │   │   ├── Entities/
│   │   │   │   └── Interfaces/
│   │   │   └── OrderFlow.Catalog.Infrastructure/  # EF Core + Redis
│   │   │       ├── Data/
│   │   │       ├── Caching/
│   │   │       └── DependencyInjection.cs
│   │   │
│   │   ├── Orders/                                # ⭐ SERVIÇO REFERÊNCIA
│   │   │   ├── OrderFlow.Orders.Api/              # Minimal APIs
│   │   │   │   ├── Endpoints/
│   │   │   │   ├── Middleware/
│   │   │   │   ├── Program.cs
│   │   │   │   └── Dockerfile
│   │   │   ├── OrderFlow.Orders.Application/      # CQRS handlers
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreateOrder/
│   │   │   │   │   ├── ConfirmOrder/
│   │   │   │   │   ├── CancelOrder/
│   │   │   │   │   └── ShipOrder/
│   │   │   │   ├── Queries/
│   │   │   │   │   ├── GetOrderById/
│   │   │   │   │   └── GetOrdersByCustomer/
│   │   │   │   ├── Behaviors/
│   │   │   │   │   ├── ValidationBehavior.cs
│   │   │   │   │   ├── LoggingBehavior.cs
│   │   │   │   │   └── TransactionBehavior.cs
│   │   │   │   ├── DTOs/
│   │   │   │   └── IntegrationEvents/
│   │   │   ├── OrderFlow.Orders.Domain/           # DDD puro
│   │   │   │   ├── Aggregates/
│   │   │   │   │   └── OrderAggregate/
│   │   │   │   │       ├── Order.cs               # Aggregate Root
│   │   │   │   │       ├── OrderItem.cs           # Entity
│   │   │   │   │       └── OrderStatus.cs         # Value Object (state machine)
│   │   │   │   ├── ValueObjects/
│   │   │   │   │   ├── Money.cs
│   │   │   │   │   ├── Address.cs
│   │   │   │   │   └── OrderNumber.cs
│   │   │   │   ├── Events/
│   │   │   │   │   ├── OrderCreatedEvent.cs
│   │   │   │   │   ├── OrderConfirmedEvent.cs
│   │   │   │   │   └── OrderCancelledEvent.cs
│   │   │   │   ├── Exceptions/
│   │   │   │   └── Interfaces/
│   │   │   └── OrderFlow.Orders.Infrastructure/
│   │   │       ├── Data/
│   │   │       │   ├── OrdersDbContext.cs
│   │   │       │   ├── Configurations/
│   │   │       │   ├── Repositories/
│   │   │       │   └── Migrations/
│   │   │       ├── Queries/                       # Dapper queries
│   │   │       ├── Messaging/                     # MassTransit publishers
│   │   │       └── DependencyInjection.cs
│   │   │
│   │   └── Notification/
│   │       └── OrderFlow.Notification.Worker/     # Worker Service
│   │           ├── Consumers/
│   │           │   ├── OrderCreatedConsumer.cs
│   │           │   └── OrderConfirmedConsumer.cs
│   │           ├── Program.cs
│   │           └── Dockerfile
│   │
│   └── BuildingBlocks/
│       ├── OrderFlow.SharedKernel/                # Classes base compartilhadas
│       │   ├── Entity.cs
│       │   ├── AggregateRoot.cs
│       │   ├── ValueObject.cs
│       │   ├── IDomainEvent.cs
│       │   ├── IUnitOfWork.cs
│       │   └── Result.cs
│       └── OrderFlow.MessageContracts/            # Contratos de eventos
│           ├── OrderCreated.cs
│           ├── OrderConfirmed.cs
│           └── OrderCancelled.cs
│
├── tests/
│   ├── OrderFlow.Orders.Domain.Tests/             # Unit: domínio puro
│   ├── OrderFlow.Orders.Application.Tests/        # Unit: handlers CQRS
│   ├── OrderFlow.Orders.Api.Tests/                # Integration: endpoints
│   ├── OrderFlow.Catalog.Api.Tests/               # Integration: CRUD
│   └── OrderFlow.Identity.Api.Tests/              # Integration: auth flow
│
├── docker/
│   ├── docker-compose.yml                         # Orquestração completa
│   ├── docker-compose.override.yml                # Dev overrides
│   └── .env                                       # Variáveis de ambiente
│
├── .github/
│   └── workflows/
│       ├── ci.yml                                 # Build + Test + Lint
│       └── cd.yml                                 # Docker push + Deploy
│
├── docs/                                          # 📚 Documentação
│   ├── 00-visao-geral.md                          # Este documento
│   ├── fase-01-fundacao-estrutura.md
│   ├── fase-02-dominio-ddd.md
│   ├── fase-03-cqrs-application.md
│   ├── fase-04-autenticacao-seguranca.md
│   ├── fase-05-mensageria-async.md
│   ├── fase-06-cache-observabilidade.md
│   ├── fase-07-gateway-docker.md
│   ├── fase-08-cicd-cloud.md
│   └── adrs/                                      # Architecture Decision Records
│
├── OrderFlow.sln
├── .editorconfig
├── .gitignore
├── Directory.Build.props                          # Configurações MSBuild compartilhadas
├── Directory.Packages.props                       # Central Package Management
├── global.json                                    # Pin da versão do SDK
├── nuget.config                                   # Package sources
└── README.md
```

### Regra de Dependência (Dependency Rule)

```
Domain ← Application ← Infrastructure ← Api
  │                                        │
  └──────── SharedKernel ──────────────────┘
                  │
          MessageContracts
```

- **Domain** não referencia nenhum outro projeto (exceto SharedKernel)
- **Application** referencia apenas Domain
- **Infrastructure** referencia Application e Domain
- **Api** referencia todos (é o composition root)

---

## 7. Fluxos Principais

### Fluxo 1: Registro e Login

```
Cliente                Gateway              Identity API           SQL Server
  │                      │                      │                     │
  │ POST /auth/register  │                      │                     │
  │─────────────────────►│─────────────────────►│                     │
  │                      │                      │ Create User         │
  │                      │                      │────────────────────►│
  │                      │                      │◄────────────────────│
  │                      │◄─────────────────────│ { userId }          │
  │◄─────────────────────│ 201 Created          │                     │
  │                      │                      │                     │
  │ POST /auth/login     │                      │                     │
  │─────────────────────►│─────────────────────►│                     │
  │                      │                      │ Validate Password   │
  │                      │                      │────────────────────►│
  │                      │                      │◄────────────────────│
  │                      │                      │ Generate JWT        │
  │                      │◄─────────────────────│ { token, refresh }  │
  │◄─────────────────────│ 200 OK               │                     │
```

### Fluxo 2: Consultar Catálogo (com Cache)

```
Cliente                Gateway              Catalog API              Redis             SQL Server
  │                      │                      │                      │                  │
  │ GET /catalog/products│                      │                      │                  │
  │─────────────────────►│─────────────────────►│                      │                  │
  │                      │                      │ GET cache:products   │                  │
  │                      │                      │─────────────────────►│                  │
  │                      │                      │◄─────────────────────│                  │
  │                      │                      │ (cache MISS)         │                  │
  │                      │                      │ SELECT * FROM...     │                  │
  │                      │                      │─────────────────────────────────────────►│
  │                      │                      │◄─────────────────────────────────────────│
  │                      │                      │ SET cache:products   │                  │
  │                      │                      │─────────────────────►│                  │
  │                      │◄─────────────────────│ [products]           │                  │
  │◄─────────────────────│ 200 OK               │                      │                  │
```

### Fluxo 3: Criar Pedido (CQRS + Eventos)

```
Cliente         Gateway         Orders API          SQL Server       RabbitMQ        Notification
  │               │                │                    │               │               │
  │ POST /orders  │                │                    │               │               │
  │──────────────►│───────────────►│                    │               │               │
  │               │                │                    │               │               │
  │               │        ┌───────┴───────┐            │               │               │
  │               │        │ MediatR Send  │            │               │               │
  │               │        │ CreateOrder   │            │               │               │
  │               │        │   Command     │            │               │               │
  │               │        └───────┬───────┘            │               │               │
  │               │                │                    │               │               │
  │               │        ┌───────┴───────┐            │               │               │
  │               │        │ Validation    │            │               │               │
  │               │        │ Behavior      │            │               │               │
  │               │        └───────┬───────┘            │               │               │
  │               │                │                    │               │               │
  │               │        ┌───────┴───────┐            │               │               │
  │               │        │ Transaction   │            │               │               │
  │               │        │ Behavior      │            │               │               │
  │               │        └───────┬───────┘            │               │               │
  │               │                │                    │               │               │
  │               │        ┌───────┴───────┐            │               │               │
  │               │        │ Handler:      │            │               │               │
  │               │        │ Create Order  │            │               │               │
  │               │        │ + Domain Event│            │               │               │
  │               │        └───────┬───────┘            │               │               │
  │               │                │                    │               │               │
  │               │                │ SaveChanges +      │               │               │
  │               │                │ Outbox Insert      │               │               │
  │               │                │───────────────────►│               │               │
  │               │                │◄───────────────────│               │               │
  │               │                │                    │               │               │
  │               │                │ Publish (Outbox)   │               │               │
  │               │                │────────────────────────────────────►               │
  │               │                │                    │               │               │
  │               │◄───────────────│ 201 Created        │               │   Consume     │
  │◄──────────────│                │                    │               │──────────────►│
  │               │                │                    │               │   Notify      │
```

---

## 8. Padrões Arquiteturais Aplicados

### Clean Architecture

```
              ┌──────────────────────┐
              │      Api Layer       │   ← Frameworks, configuração, DI container
              │  (Composition Root)  │
              ├──────────────────────┤
              │  Infrastructure      │   ← Implementações: EF Core, Redis, MassTransit
              │  (Adapters)          │
              ├──────────────────────┤
              │  Application         │   ← Use cases: Commands, Queries, DTOs
              │  (Use Cases)         │
              ├──────────────────────┤
              │  Domain              │   ← Entidades, Value Objects, Domain Events
              │  (Enterprise Rules)  │   ← ZERO dependências externas
              └──────────────────────┘
```

**Quando usar:** Sistemas que precisam de longevidade, testabilidade e evolução independente de frameworks.  
**Quando NÃO usar:** CRUDs simples, protótipos, scripts.

### Domain-Driven Design (DDD) — Padrões Táticos

| Padrão | Onde no OrderFlow | O que é |
|--------|-------------------|---------|
| **Aggregate Root** | `Order` | Entidade raiz que protege invariantes do aggregate |
| **Entity** | `OrderItem` | Objeto com identidade própria dentro do aggregate |
| **Value Object** | `Money`, `Address`, `OrderNumber` | Objeto sem identidade, definido por seus atributos |
| **Domain Event** | `OrderCreatedEvent` | Algo que aconteceu no domínio, notifica side effects |
| **Repository** | `IOrderRepository` | Abstração para persistência do aggregate |
| **Domain Service** | (quando necessário) | Lógica que não pertence a nenhuma entity/VO |

### CQRS (Command Query Responsibility Segregation)

```
                    ┌─────────────────────────┐
                    │       MediatR Send       │
                    └────────────┬────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │                         │
             ┌──────▼──────┐          ┌───────▼──────┐
             │  Command    │          │   Query      │
             │  Pipeline   │          │   Pipeline   │
             │             │          │              │
             │ Validation  │          │              │
             │ Transaction │          │              │
             │ Logging     │          │              │
             └──────┬──────┘          └───────┬──────┘
                    │                         │
             ┌──────▼──────┐          ┌───────▼──────┐
             │  Handler    │          │  Handler     │
             │  (EF Core)  │          │  (Dapper)    │
             └──────┬──────┘          └───────┬──────┘
                    │                         │
             ┌──────▼──────┐          ┌───────▼──────┐
             │  Write DB   │          │  Read DB     │
             │  (full      │          │  (optimized  │
             │   model)    │          │   projections)│
             └─────────────┘          └──────────────┘
```

**Quando usar:** Quando leitura e escrita têm necessidades diferentes (modelo, performance, escalabilidade).  
**Quando NÃO usar:** CRUDs simples onde o modelo é idêntico para ler e escrever.

### Outbox Pattern

```
┌──────────────────────────────────────────┐
│           Mesma Transação do DB          │
│                                          │
│  1. INSERT INTO Orders (...)             │
│  2. INSERT INTO OutboxMessages (...)     │
│                                          │
│  COMMIT                                  │
└──────────────────────────────────────────┘
         │
         │ Background Job (MassTransit Outbox)
         ▼
┌──────────────────────────────────────────┐
│  SELECT FROM OutboxMessages              │
│  WHERE ProcessedAt IS NULL               │
│                                          │
│  Para cada mensagem:                     │
│    1. Publish no RabbitMQ                │
│    2. UPDATE ProcessedAt = NOW           │
└──────────────────────────────────────────┘
```

**Problema que resolve:** Sem outbox, se o app faz `SaveChanges()` e depois `Publish()`, o publish pode falhar e o sistema fica inconsistente.  
**Com outbox:** O evento é salvo na mesma transação que os dados. Se o publish falhar, o background job tenta de novo.

---

## 9. Mapeamento: Vagas → Projeto

| Tópico da Vaga | Onde no OrderFlow | Fase |
|----------------|-------------------|------|
| C# e fundamentos .NET | Todo o projeto — records, pattern matching, async/await, LINQ | 1-8 |
| ASP.NET Core APIs REST | Catalog API (Controllers) + Orders API (Minimal APIs) | 1, 3 |
| Entity Framework Core + LINQ | Catalog e Orders — migrations, configurações, queries | 1, 2, 3 |
| SQL Server + modelagem relacional | Banco de cada serviço, relacionamentos, índices | 1 |
| Arquitetura (Clean Arch / DDD) | Orders API com todas as camadas | 1, 2 |
| CQRS | Orders API — Commands via MediatR + Queries via Dapper | 3 |
| Git e controle de versão | Conventional commits, branching, PRs | 1-8 |
| Testes automatizados | Unit (domínio), Integration (API), Contract tests | 2, 3, 7 |
| Docker e conteinerização | Dockerfile por serviço + docker-compose completo | 1, 7 |
| Microsserviços | 4 serviços + Gateway + comunicação assíncrona | 5, 7 |
| Autenticação e autorização | Identity API com JWT, roles, policies, refresh token | 4 |
| Mensageria e filas | RabbitMQ + MassTransit para eventos | 5 |
| Cache e performance | Redis cache-aside, output caching, compiled queries | 6 |
| DevOps / CI/CD | GitHub Actions — build, test, Docker push | 8 |
| Cloud (Azure) | Azure Container Apps, SQL Database, Redis, Service Bus | 8 |
| SOLID e Clean Code | Aplicado em cada decisão de design | 1-8 |
| Logging e observabilidade | Serilog + OpenTelemetry + Health Checks | 1, 6 |
| Metodologias ágeis | GitHub Projects simulando sprints | 8 |

---

## 10. Roadmap de Fases

```
Fase 1 ─── Fase 2 ─── Fase 3 ─── Fase 4 ─── Fase 5 ─── Fase 6 ─── Fase 7 ─── Fase 8
  │          │          │          │          │          │          │          │
  ▼          ▼          ▼          ▼          ▼          ▼          ▼          ▼
Fundação   DDD       CQRS      Auth      Mensageria  Cache      Gateway    CI/CD
Estrutura  Domínio   MediatR   JWT       RabbitMQ    Redis      Docker     Azure
Catalog    Orders    Handlers  Identity  MassTransit  OTel      YARP       Deploy
EF Core    Tests     Dapper    Policies  Outbox      Serilog    Tests      Docs
```

| Fase | Nome | Semanas | Documento |
|------|------|---------|-----------|
| 1 | Fundação e Estrutura | 1-2 | `fase-01-fundacao-estrutura.md` |
| 2 | Domínio Rico e DDD | 3-4 | `fase-02-dominio-ddd.md` |
| 3 | CQRS e Application Layer | 5-6 | `fase-03-cqrs-application.md` |
| 4 | Autenticação e Segurança | 7-8 | `fase-04-autenticacao-seguranca.md` |
| 5 | Mensageria e Comunicação Assíncrona | 9-10 | `fase-05-mensageria-async.md` |
| 6 | Cache, Performance e Observabilidade | 11-12 | `fase-06-cache-observabilidade.md` |
| 7 | API Gateway, Docker e Integração | 13-14 | `fase-07-gateway-docker.md` |
| 8 | CI/CD, Cloud e Finalização | 15-16 | `fase-08-cicd-cloud.md` |

### Pré-requisitos por Fase

```
Fase 1: Nenhum (ponto de partida)
Fase 2: Fase 1 completa
Fase 3: Fases 1 e 2 completas
Fase 4: Fase 1 completa (pode ser paralelizada com 2-3)
Fase 5: Fases 2 e 3 completas
Fase 6: Fases 1-3 completas
Fase 7: Fases 1-6 completas
Fase 8: Fase 7 completa
```

---

## 11. Convenções do Projeto

### Git

| Convenção | Exemplo |
|-----------|---------|
| **Branch naming** | `feature/catalog-crud`, `fix/order-validation`, `docs/fase-01` |
| **Commit messages** | Conventional Commits: `feat(catalog): add product search endpoint` |
| **PR template** | O que mudou, por que mudou, como testar |

### Código C#

| Convenção | Regra |
|-----------|-------|
| **Nullable** | `<Nullable>enable</Nullable>` em todos os projetos |
| **Primary constructors** | Preferencia para DI em services |
| **Records** | Para DTOs, Commands, Queries, Events |
| **sealed** | Classes que não serão herdadas |
| **CancellationToken** | Todos os métodos async aceitam `CancellationToken ct = default` |
| **Naming** | PascalCase para públicos, _camelCase para privados, I prefix para interfaces |

### Testes

| Convenção | Regra |
|-----------|-------|
| **Naming** | `MethodName_StateUnderTest_ExpectedBehavior` |
| **Arrange/Act/Assert** | Cada test tem as 3 seções claramente separadas |
| **Uma assertion** | Idealmente, uma assertion lógica por test |
| **Test project** | Mesmo namespace + `.Tests` suffix |

### Docker

| Convenção | Regra |
|-----------|-------|
| **Multi-stage** | Cada Dockerfile usa multi-stage build |
| **Non-root** | Containers rodam com usuário não-root |
| **Health check** | Todo container tem health check configurado |
| **Tags** | `orderflow/service-name:latest` e `orderflow/service-name:v1.0.0` |

---

## 12. Como Rodar Localmente

### Pré-requisitos

- .NET 10 SDK
- Docker Desktop
- Git
- IDE: Visual Studio 2022 ou VS Code com C# Dev Kit
- EF Core CLI: `dotnet tool install --global dotnet-ef`

### Comando Rápido

```bash
# Clonar o repositório
git clone https://github.com/seu-usuario/OrderFlow.git
cd OrderFlow

# Subir infraestrutura (SQL Server, Redis, RabbitMQ, Seq)
docker-compose -f docker/docker-compose.yml up -d

# Rodar migrations
dotnet ef database update --project src/Services/Catalog/OrderFlow.Catalog.Infrastructure
dotnet ef database update --project src/Services/Orders/OrderFlow.Orders.Infrastructure
dotnet ef database update --project src/Services/Identity/OrderFlow.Identity.Infrastructure

# Rodar todos os serviços
dotnet run --project src/ApiGateway/OrderFlow.Gateway
dotnet run --project src/Services/Identity/OrderFlow.Identity.Api
dotnet run --project src/Services/Catalog/OrderFlow.Catalog.Api
dotnet run --project src/Services/Orders/OrderFlow.Orders.Api
dotnet run --project src/Services/Notification/OrderFlow.Notification.Worker

# Ou com Docker Compose (tudo junto)
docker-compose -f docker/docker-compose.yml --profile all up -d
```

### URLs Locais

| Serviço | URL | Swagger |
|---------|-----|---------|
| API Gateway | `http://localhost:5000` | — |
| Identity API | `http://localhost:5001` | `/swagger` |
| Catalog API | `http://localhost:5002` | `/swagger` |
| Orders API | `http://localhost:5003` | `/swagger` |
| Seq (logs) | `http://localhost:5341` | — |
| RabbitMQ UI | `http://localhost:15672` | — |

> **Nota sobre portas:** As portas 5000–5003 são para desenvolvimento local com `dotnet run` (via `launchSettings.json`). Com Docker Compose, todos os serviços escutam na porta **8080** internamente e são acessíveis apenas via **Gateway** em `http://localhost:8080`.

---

## 13. Glossário

| Termo | Definição |
|-------|-----------|
| **Aggregate** | Cluster de entidades e value objects com uma raiz que garante consistência |
| **Aggregate Root** | Entidade principal do aggregate, único ponto de acesso externo |
| **Bounded Context** | Limite lógico onde um modelo de domínio é consistente |
| **CQRS** | Command Query Responsibility Segregation — separar modelos de escrita e leitura |
| **Clean Architecture** | Arquitetura em camadas com dependência apontando para dentro (domínio) |
| **DDD** | Domain-Driven Design — abordagem de design focada no domínio do negócio |
| **Domain Event** | Notificação de que algo aconteceu no domínio |
| **Integration Event** | Evento publicado para outros serviços (cross-boundary) |
| **Outbox Pattern** | Salvar evento pendente na mesma transação que os dados |
| **Value Object** | Objeto definido por seus atributos, sem identidade própria |
| **Pipeline Behavior** | Middleware do MediatR que intercepta requests (cross-cutting) |
| **Saga** | Padrão para gerenciar transações distribuídas entre serviços |
| **Idempotência** | Processar a mesma mensagem múltiplas vezes sem efeitos duplicados |
| **DLQ** | Dead Letter Queue — fila para mensagens que falharam todas as retentativas |
| **Cache-aside** | Padrão onde a aplicação gerencia o cache (lê do cache, se miss vai ao banco) |
| **Rate Limiting** | Limitar quantidade de requests por período para proteger a API |
| **Health Check** | Endpoint que reporta se o serviço e suas dependências estão saudáveis |

---

## 14. Checklist de Competências

Use para acompanhar seu progresso. Cada item é praticado neste projeto:

| # | Competência | Onde no Projeto | Fase | Status |
|---|---|---|---|---|
| 1 | C# 13 (Primary Constructors, Records, Collection Expressions) | DTOs, Commands, Queries, DI | 01 | ⬜ |
| 2 | ASP.NET Core 10 (Controllers + Minimal APIs) | Catalog API + Orders API | 01 | ⬜ |
| 3 | Entity Framework Core 10 (Code First, Fluent API) | Repositories, Configurations, Migrations | 01 | ⬜ |
| 4 | SQL Server 2022 | Banco dedicado por serviço | 01 | ⬜ |
| 5 | Clean Architecture (4 camadas, Dependency Rule) | Estrutura de todos os serviços | 01 | ⬜ |
| 6 | DDD — Aggregates, Entities, Value Objects | Order, OrderItem, Money, Address | 02 | ⬜ |
| 7 | DDD — Domain Events + Integration Events | OrderCreatedEvent → MassTransit | 02 | ⬜ |
| 8 | Rich Domain Model (entidade com comportamento) | Order.AddItem(), Order.Confirm() | 02 | ⬜ |
| 9 | CQRS com MediatR (Commands + Queries separados) | CreateOrderCommand, GetOrderByIdQuery | 03 | ⬜ |
| 10 | Pipeline Behaviors (Validation, Logging, Transaction) | ValidationBehavior, TransactionBehavior | 03 | ⬜ |
| 11 | Dapper (leitura performática) | Queries com SQL puro no read side | 03 | ⬜ |
| 12 | Result Pattern (Error handling sem exceptions) | Result\<T\>, Error records | 03 | ⬜ |
| 13 | FluentValidation | Validators em Commands/Queries | 03 | ⬜ |
| 14 | ASP.NET Identity + JWT Bearer | Register, Login, Refresh, Revoke | 04 | ⬜ |
| 15 | Refresh Token Rotation + Revogação | Refresh Tokens hasheados no banco | 04 | ⬜ |
| 16 | Rate Limiting + CORS | Fixed/Sliding Window, Policy-based | 04 | ⬜ |
| 17 | RabbitMQ + MassTransit (Mensageria) | Producer/Consumer de eventos | 05 | ⬜ |
| 18 | Outbox Pattern (Transactional Messaging) | EF Core Outbox com MassTransit | 05 | ⬜ |
| 19 | Idempotência de Consumers | Redis-based IdempotentConsumerFilter | 05 | ⬜ |
| 20 | Dead Letter Queue + Retry Policies | Configuração MassTransit | 05 | ⬜ |
| 21 | Redis (Cache Distribuído, Cache-Aside) | IDistributedCache + StackExchange.Redis | 06 | ⬜ |
| 22 | OpenTelemetry (Traces + Metrics + Logs) | TracerProvider, MeterProvider | 06 | ⬜ |
| 23 | Serilog + Seq (Structured Logging) | Enrichers, Sinks, Correlation ID | 06 | ⬜ |
| 24 | Health Checks (Liveness + Readiness + Startup) | SQL, Redis, RabbitMQ checks | 06 | ⬜ |
| 25 | YARP (API Gateway, Reverse Proxy) | Routing, Rate Limiting, Transforms | 07 | ⬜ |
| 26 | Docker Multi-stage Builds | Dockerfiles otimizados (< 120MB) | 07 | ⬜ |
| 27 | Docker Compose (Orquestração completa) | 9 containers + healthchecks | 07 | ⬜ |
| 28 | xUnit + Moq + FluentAssertions (Testes) | Unit + Integration tests | Todas | ⬜ |
| 29 | Testcontainers (Integration Tests) | SQL Server + RabbitMQ em containers | 05-07 | ⬜ |
| 30 | GitHub Actions (CI/CD) | Build, Test, Docker Push, Deploy | 08 | ⬜ |
| 31 | Azure Container Apps + Bicep (IaC) | Deploy com infraestrutura como código | 08 | ⬜ |
| 32 | Conventional Commits + Git Flow | feat:, fix:, feature/*, main | Todas | ⬜ |

> **💡 Dica:** Ao concluir cada fase, volte aqui e marque (✅) as competências praticadas. Isso cria um mapa visual do seu progresso e mostra exatamente quais habilidades você pode defender em entrevistas.

---

## 15. Perguntas Frequentes em Entrevistas

Essas são as perguntas que recrutadores e tech leads fazem sobre os temas cobertos neste projeto. Cada resposta é construída para você articular com confiança.

**1. "Explique Clean Architecture. Quando usar e quando NÃO usar?"**
— Clean Architecture organiza o código em camadas concêntricas: Domain (regras de negócio puras) → Application (use cases) → Infrastructure (implementações: EF Core, Redis, MassTransit) → Api (composition root). A **Dependency Rule** diz que dependências só apontam para dentro — nunca o Domain referencia Infrastructure. **Usar:** sistemas que precisam de longevidade, testabilidade e evolução independente de frameworks. **NÃO usar:** CRUDs simples, protótipos, scripts — o overhead não compensa.

**2. "Qual a diferença entre Domain Events e Integration Events?"**
— **Domain Events** são in-process (MediatR) — notificam side effects dentro do mesmo serviço, na mesma transação (ex: `OrderCreatedEvent` dispara cálculo de total). **Integration Events** são cross-process (MassTransit/RabbitMQ) — notificam outros serviços de forma assíncrona (ex: `OrderCreatedIntegrationEvent` dispara notificação). O Outbox Pattern garante que o Integration Event só é publicado se a transação do banco commitou.

**3. "O que é CQRS e por que separar escrita de leitura?"**
— CQRS separa modelos de escrita (Commands via EF Core com change tracking e transações) e leitura (Queries via Dapper com SQL otimizado). **Por quê:** escrita e leitura têm necessidades diferentes — escrita precisa de consistência e validação; leitura precisa de performance e flexibilidade de projeção. **Trade-off:** mais complexidade (dois modelos), mas escalabilidade independente e queries sem N+1.

**4. "Como funciona o Outbox Pattern? Por que não publicar o evento direto?"**
— Se você salva no banco e publica no RabbitMQ separadamente, um pode falhar e o outro não (inconsistência). O Outbox Pattern salva o evento pendente **na mesma transação** que os dados. Um background job depois lê os eventos pendentes e publica no broker. Se a publicação falhar, o job retenta. Resultado: **garantia de at-least-once delivery** sem transação distribuída.

**5. "JWT Stateless vs Sessions — quando usar cada um?"**
— **JWT:** ideal para microserviços — qualquer instância valida o token localmente sem consultar banco central. Escalável. **Sessions:** ideal para monolitos — server armazena estado. **Trade-off do JWT:** não dá para revogar um token individual sem blacklist; tokens têm TTL fixo. No OrderFlow, usamos Refresh Token Rotation como mitigação — o access token vive 15min, o refresh token é de uso único.

**6. "Database per Service — e se eu precisar de dados de outro serviço?"**
— Sem JOINs cross-database. Duas opções: (1) **Comunicação síncrona** — chamar a API do outro serviço (acoplamento temporal). (2) **Eventos** — o serviço publica evento quando dado muda, o consumidor mantém uma cópia local (desnormalização). No OrderFlow, usamos eventos (opção 2) para máximo desacoplamento. **Trade-off:** consistência eventual.

**7. "Cite os 5 princípios SOLID com exemplos reais do OrderFlow."**
— **S (SRP):** `CreateOrderCommandHandler` faz apenas uma coisa — orquestra criação do pedido. **O (OCP):** `IOrderRepository` (interface) pode ter implementação EF Core ou in-memory sem alterar o handler. **L (LSP):** qualquer implementação de `IOrderRepository` funciona sem quebrar. **I (ISP):** `IOrderReadRepository` (Dapper) separado de `IOrderRepository` (EF Core). **D (DIP):** `CreateOrderCommandHandler` depende de `IOrderRepository` (abstração), não de `OrderRepository` (implementação concreta).

---

> **Próximo passo:** Abra `fase-01-fundacao-estrutura.md` para começar a implementação.
