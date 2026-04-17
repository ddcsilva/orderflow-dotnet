<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/C%23-13-239120?style=for-the-badge&logo=csharp&logoColor=white" alt="C# 13" />
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/RabbitMQ-MassTransit-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white" alt="RabbitMQ" />
  <img src="https://img.shields.io/badge/Azure-Container%20Apps-0078D4?style=for-the-badge&logo=microsoftazure&logoColor=white" alt="Azure" />
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" alt="License" />
</p>

<h1 align="center">🛒 OrderFlow</h1>

<p align="center">
  <strong>Enterprise-grade .NET microservices — from architecture to cloud deploy.</strong>
  <br />
  Clean Architecture · DDD · CQRS · Event-Driven · Observability · CI/CD
</p>

<p align="center">
  <a href="#-architecture">Architecture</a> •
  <a href="#-microservices">Microservices</a> •
  <a href="#-tech-stack">Tech Stack</a> •
  <a href="#-getting-started">Getting Started</a> •
  <a href="#-project-structure">Structure</a> •
  <a href="#-documentation">Docs</a> •
  <a href="#-license">License</a>
</p>

---

## 📖 About

**OrderFlow** is a fully-featured order management system built with **.NET 10 microservices**, designed to demonstrate real-world enterprise patterns used in the Brazilian .NET job market.

The domain is intentionally simple (products + orders). The architecture is intentionally rich — every decision maps to a skill demanded in senior-level .NET positions.

```
User registers → Logs in → Browses catalog → Creates order → Order transitions → Notification fires
```

### What This Is

- A **portfolio project** showcasing enterprise .NET patterns end-to-end
- A **learning lab** with 8 phases of progressive documentation (Portuguese 🇧🇷)
- A **reference implementation** of Clean Architecture + DDD + CQRS + Event-Driven

### What This Is NOT

- ❌ A production e-commerce (no cart, payments, shipping, stock management)
- ❌ Over-engineering for the sake of it — every pattern has a documented ADR

---

## 🏗 Architecture

```
                          ┌──────────────────┐
                          │   Client (HTTP)  │
                          └────────┬─────────┘
                                   │
                          ┌────────▼─────────┐
                          │   API Gateway    │
                          │     (YARP)       │
                          │  Rate Limiting   │
                          │  Auth Forwarding │
                          └──┬─────┬──────┬──┘
                             │     │      │
            ┌────────────────┘     │      └────────────────┐
            │                      │                       │
   ┌────────▼────────┐   ┌────────▼────────┐   ┌─────────▼────────┐
   │  Identity API   │   │  Catalog API    │   │   Orders API     │
   │  JWT + Refresh  │   │  Cache-Aside    │   │  CQRS + MediatR  │
   │  ASP.NET Identity│   │  Controllers   │   │  DDD Aggregates  │
   │  ┌───────────┐  │   │  ┌───────────┐  │   │  ┌────────────┐  │
   │  │ SQL Server │  │   │  │ SQL Server │  │   │  │ SQL Server │  │
   │  └───────────┘  │   │  └───────────┘  │   │  └────────────┘  │
   └─────────────────┘   └─────────────────┘   └────────┬─────────┘
                                                        │
                                               Outbox Pattern
                                                        │
                                               ┌────────▼─────────┐
                                               │    RabbitMQ      │
                                               └────────┬─────────┘
                                                        │
                                               ┌────────▼─────────┐
                                               │  Notification    │
                                               │  Worker Service  │
                                               └──────────────────┘

   ┌──────────────────────────────────────────────────────────────┐
   │  Redis · Serilog + Seq · OpenTelemetry · Health Checks      │
   │  Docker Compose · GitHub Actions · Azure Container Apps     │
   └──────────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| ADR-001 | .NET 10 / C# 13 | Latest runtime, primary constructors, collection expressions |
| ADR-002 | EF Core (write) + Dapper (read) | Best of both: change tracking for commands, raw SQL for queries |
| ADR-003 | MassTransit over RabbitMQ Client | Built-in outbox, retry, saga, test harness |
| ADR-004 | MediatR for CQRS | Industry standard, pipeline behaviors for cross-cutting |
| ADR-005 | YARP as Gateway | Microsoft-maintained, superior performance, hot-reload config |
| ADR-006 | SQL Server per service | Database-per-service pattern; most requested DB in BR .NET market |
| ADR-007 | Serilog + Seq + OpenTelemetry | Structured logging + vendor-neutral distributed tracing |

---

## 🧩 Microservices

| Service | Responsibility | Style | Key Patterns |
|---------|---------------|-------|--------------|
| **Identity API** | Authentication & authorization | Minimal APIs | ASP.NET Identity, JWT Bearer, Refresh Tokens, Rate Limiting |
| **Catalog API** | Product & category management | Controllers | Clean Architecture, Repository, Redis Cache-Aside, FluentValidation |
| **Orders API** ⭐ | Order lifecycle management | Minimal APIs | DDD, CQRS, Domain Events, Outbox Pattern, EF Core + Dapper |
| **Notification Worker** | Event consumption & notifications | Worker Service | MassTransit Consumers, Idempotency, Retry + DLQ |
| **API Gateway** | Single entry point & routing | YARP | Reverse Proxy, Rate Limiting, Header Forwarding, CORS |

> ⭐ **Orders API** is the reference service — it concentrates DDD, CQRS, domain events, and the outbox pattern.

---

## 🛠 Tech Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Runtime** | .NET 10 / C# 13 | Latest framework with modern language features |
| **Web** | ASP.NET Core 10 | Controllers (Catalog) + Minimal APIs (Orders, Identity) |
| **ORM** | EF Core 10 | Migrations, Fluent API, change tracking (write side) |
| **Micro ORM** | Dapper 2.x | High-performance queries (read side) |
| **Database** | SQL Server 2022 | One database per service |
| **Cache** | Redis 7.x | Distributed cache with cache-aside + output caching |
| **Messaging** | RabbitMQ 4.x + MassTransit 8.x | Async communication, outbox, retry, DLQ |
| **Mediator** | MediatR 12.x | CQRS pipeline, validation & logging behaviors |
| **Validation** | FluentValidation 11.x | Declarative validation rules |
| **Gateway** | YARP 2.x | Reverse proxy with hot-reload routes |
| **Auth** | ASP.NET Identity + JWT | Bearer tokens, refresh rotation, claims-based policies |
| **Logging** | Serilog 4.x + Seq | Structured logging with enrichers (CorrelationId, UserId) |
| **Telemetry** | OpenTelemetry 1.x | Distributed traces, metrics, Jaeger/Prometheus export |
| **Testing** | xUnit + FluentAssertions + Moq | Unit, integration, and contract tests |
| **Integration Tests** | Testcontainers 4.x + WebApplicationFactory | Real containers in tests |
| **Containers** | Docker + Compose | Multi-stage builds, full orchestration |
| **CI/CD** | GitHub Actions | Build → Test → Publish → Deploy |
| **Cloud** | Azure Container Apps + Bicep | Serverless containers, Infrastructure as Code |

---

## 🚀 Getting Started

### Prerequisites

| Tool | Version | Required |
|------|---------|----------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | ✅ |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | ✅ |
| [Git](https://git-scm.com/) | 2.x+ | ✅ |

### Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/seu-usuario/orderflow.git
cd orderflow

# 2. Start all infrastructure (SQL Server, Redis, RabbitMQ, Seq)
docker compose up -d

# 3. Apply migrations
dotnet ef database update -p src/Services/Identity/OrderFlow.Identity.Infrastructure -s src/Services/Identity/OrderFlow.Identity.Api
dotnet ef database update -p src/Services/Catalog/OrderFlow.Catalog.Infrastructure -s src/Services/Catalog/OrderFlow.Catalog.Api
dotnet ef database update -p src/Services/Orders/OrderFlow.Orders.Infrastructure -s src/Services/Orders/OrderFlow.Orders.Api

# 4. Run all services
dotnet run --project src/ApiGateway/OrderFlow.Gateway
dotnet run --project src/Services/Identity/OrderFlow.Identity.Api
dotnet run --project src/Services/Catalog/OrderFlow.Catalog.Api
dotnet run --project src/Services/Orders/OrderFlow.Orders.Api
dotnet run --project src/Services/Notification/OrderFlow.Notification.Worker
```

### Run with Docker Compose (recommended)

```bash
# Build and start everything
docker compose up --build -d

# Check all services are healthy
curl http://localhost:8080/health
```

### Service URLs

| Service | URL | Notes |
|---------|-----|-------|
| API Gateway | `http://localhost:8080` | Entry point for all requests |
| Identity API | `http://localhost:5001` | Auth endpoints |
| Catalog API | `http://localhost:5002` | Product CRUD |
| Orders API | `http://localhost:5000` | Order management |
| RabbitMQ Management | `http://localhost:15672` | `orderflow` / `orderflow123` |
| Seq (Logs) | `http://localhost:5341` | Structured log viewer |
| Grafana | `http://localhost:3000` | `admin` / `admin` |

### Smoke Test

```bash
# Register a user
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Test User","email":"test@orderflow.com","password":"Test@1234","confirmPassword":"Test@1234"}'

# Login and get JWT
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@orderflow.com","password":"Test@1234"}'

# Browse catalog
curl http://localhost:8080/api/catalog/products

# Create an order (use the token from login)
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{"customerId":"...","street":"Rua Teste","number":"100","neighborhood":"Centro","city":"São Paulo","state":"SP","zipCode":"01001000"}'
```

### Run Tests

```bash
# All tests
dotnet test

# Unit tests only (domain)
dotnet test tests/OrderFlow.Orders.Domain.Tests

# Integration tests (requires Docker)
dotnet test tests/OrderFlow.Orders.Api.Tests
```

---

## 📁 Project Structure

```
OrderFlow/
├── src/
│   ├── ApiGateway/
│   │   └── OrderFlow.Gateway/                  # YARP reverse proxy
│   ├── Services/
│   │   ├── Identity/
│   │   │   ├── OrderFlow.Identity.Api/         # Auth endpoints
│   │   │   ├── OrderFlow.Identity.Application/ # Use cases
│   │   │   └── OrderFlow.Identity.Infrastructure/
│   │   ├── Catalog/
│   │   │   ├── OrderFlow.Catalog.Api/          # REST Controllers
│   │   │   ├── OrderFlow.Catalog.Application/  # Services + Validators
│   │   │   ├── OrderFlow.Catalog.Domain/       # Pure entities
│   │   │   └── OrderFlow.Catalog.Infrastructure/
│   │   ├── Orders/                             # ⭐ Reference service
│   │   │   ├── OrderFlow.Orders.Api/           # Minimal APIs
│   │   │   ├── OrderFlow.Orders.Application/   # CQRS Handlers
│   │   │   ├── OrderFlow.Orders.Domain/        # DDD: Aggregates, VOs, Events
│   │   │   └── OrderFlow.Orders.Infrastructure/
│   │   └── Notification/
│   │       └── OrderFlow.Notification.Worker/  # Event consumers
│   └── BuildingBlocks/
│       ├── OrderFlow.SharedKernel/             # Base classes (Entity, VO, Result)
│       └── OrderFlow.MessageContracts/         # Integration event contracts
├── tests/
│   ├── OrderFlow.Orders.Domain.Tests/          # Unit: pure domain
│   ├── OrderFlow.Orders.Application.Tests/     # Unit: CQRS handlers
│   ├── OrderFlow.Orders.Api.Tests/             # Integration: endpoints
│   ├── OrderFlow.Catalog.Api.Tests/            # Integration: CRUD
│   └── OrderFlow.Identity.Api.Tests/           # Integration: auth flow
├── docker/
│   ├── docker-compose.yml                      # Full orchestration
│   ├── docker-compose.override.yml             # Dev overrides
│   └── .env                                    # Environment variables
├── infra/
│   ├── main.bicep                              # Azure IaC entry point
│   └── modules/                                # Bicep modules (ACR, Container Apps)
├── .github/workflows/
│   ├── ci.yml                                  # Build + Test + Lint
│   └── cd.yml                                  # Publish + Deploy
├── docs/                                       # 📚 Full learning guide (PT-BR)
├── OrderFlow.sln
├── Directory.Build.props                       # Shared MSBuild config
├── Directory.Packages.props                    # Central Package Management
└── global.json                                 # SDK version pin
```

### Dependency Rule

```
Domain ← Application ← Infrastructure ← Api
  │                                        │
  └──────── SharedKernel ──────────────────┘
```

> **Domain** has zero external dependencies. **Application** references only Domain. **Infrastructure** implements interfaces. **Api** is the composition root.

---

## 📚 Documentation

The project includes a comprehensive **8-phase learning guide** written in Portuguese (🇧🇷), designed as a progressive tutorial from zero to cloud deployment.

| Phase | Topic | Document |
|-------|-------|----------|
| 0 | Overview, ADRs, Glossary | [`00-visao-geral.md`](docs/00-visao-geral.md) |
| 1 | Clean Architecture, SharedKernel, Docker | [`fase-01-fundacao-estrutura.md`](docs/fase-01-fundacao-estrutura.md) |
| 2 | DDD, Aggregates, Value Objects, Events | [`fase-02-dominio-ddd.md`](docs/fase-02-dominio-ddd.md) |
| 3 | CQRS, MediatR, Pipelines, Dapper | [`fase-03-cqrs-application.md`](docs/fase-03-cqrs-application.md) |
| 4 | JWT, Refresh Tokens, Rate Limiting | [`fase-04-autenticacao-seguranca.md`](docs/fase-04-autenticacao-seguranca.md) |
| 5 | RabbitMQ, MassTransit, Outbox Pattern | [`fase-05-mensageria-async.md`](docs/fase-05-mensageria-async.md) |
| 6 | Redis, OpenTelemetry, Serilog | [`fase-06-cache-observabilidade.md`](docs/fase-06-cache-observabilidade.md) |
| 7 | YARP Gateway, Docker, Testcontainers | [`fase-07-gateway-docker.md`](docs/fase-07-gateway-docker.md) |
| 8 | GitHub Actions, Azure, Bicep | [`fase-08-cicd-cloud.md`](docs/fase-08-cicd-cloud.md) |

Each phase includes:
- 🎯 **Learning objectives** — what you'll build and why
- 📋 **Artifact summary** — every file created in the phase
- 💼 **Interview prep** — 5+ questions with articulated answers per phase (~40 total)
- ⬜ **Competency checklist** — trackable progress across all phases

---

## 🧪 Testing Strategy

| Layer | Framework | What's Tested | Example |
|-------|-----------|--------------|---------|
| **Domain** | xUnit + FluentAssertions | Aggregates, Value Objects, state machine | `Order.Confirm()` transitions, `Money` equality |
| **Application** | xUnit + Moq | Command/Query handlers, pipeline behaviors | `CreateOrderHandler` with mocked repo |
| **Integration** | WebApplicationFactory + Testcontainers | Full HTTP pipeline with real DB/Redis/RabbitMQ | POST `/api/orders` → 201 + event published |

```bash
# Run all tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📊 Patterns & Skills Demonstrated

| Pattern | Where | Phase |
|---------|-------|-------|
| Clean Architecture (4 layers) | All services | 1 |
| DDD Tactical Patterns | Orders API (Aggregate, VO, Events) | 2 |
| CQRS | Orders API (Commands vs Queries) | 3 |
| Result Pattern | SharedKernel → All handlers | 3 |
| Repository Pattern | All services | 1, 2 |
| Outbox Pattern | Orders → RabbitMQ | 5 |
| Cache-Aside | Catalog API → Redis | 6 |
| Idempotent Consumer | Notification Worker | 5 |
| API Gateway | YARP with transforms | 7 |
| Database per Service | SQL Server per microservice | 1 |
| Structured Logging | Serilog + enrichers | 6 |
| Distributed Tracing | OpenTelemetry + Jaeger | 6 |
| Health Checks | Liveness + Readiness | 6 |
| Multi-stage Docker | Optimized Dockerfiles | 7 |
| Infrastructure as Code | Azure Bicep | 8 |

---

## 🗺 Roadmap

```
Phase 1 ── Phase 2 ── Phase 3 ── Phase 4 ── Phase 5 ── Phase 6 ── Phase 7 ── Phase 8
   │          │          │          │          │          │          │          │
   ▼          ▼          ▼          ▼          ▼          ▼          ▼          ▼
Foundation   DDD       CQRS      Auth      Messaging   Cache     Gateway    CI/CD
Structure   Domain    MediatR    JWT       RabbitMQ    Redis     Docker     Azure
Catalog     Tests     Dapper    Identity   Outbox      OTel      YARP       Deploy
```

---

## 🤝 Contributing

Contributions are welcome! Please follow these conventions:

1. **Branching:** `feature/topic`, `fix/topic`, `docs/topic`
2. **Commits:** [Conventional Commits](https://www.conventionalcommits.org/)
   ```
   feat(orders): add cancel order command handler
   fix(identity): handle expired refresh token rotation
   docs(fase-03): update CQRS pipeline diagram
   ```
3. **Tests:** All new features must include tests
4. **PRs:** Reference the related phase/ADR in the description

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<p align="center">
  Built with 💜 as a learning & portfolio project for the .NET enterprise ecosystem.
  <br />
  <strong>OrderFlow</strong> — da arquitetura ao deploy em cloud.
</p>
