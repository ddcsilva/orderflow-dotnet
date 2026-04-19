<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/C%23-13-239120?style=for-the-badge&logo=csharp&logoColor=white" alt="C# 13" />
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" alt="License" />
  <img src="https://img.shields.io/badge/Status-Em%20Progresso-orange?style=for-the-badge" alt="Em Progresso" />
</p>

<h1 align="center">OrderFlow</h1>

<p align="center">
  <strong>Microserviços .NET de nível enterprise — da arquitetura ao deploy em cloud.</strong>
  <br />
  Clean Architecture · DDD · CQRS · Event-Driven · Observabilidade · CI/CD
</p>

<p align="center">
  <a href="#sobre">Sobre</a> •
  <a href="#status-atual">Status</a> •
  <a href="#arquitetura">Arquitetura</a> •
  <a href="#como-rodar">Como Rodar</a> •
  <a href="#estrutura-do-projeto">Estrutura</a> •
  <a href="#documentação">Docs</a> •
  <a href="#roadmap">Roadmap</a> •
  <a href="#licença">Licença</a>
</p>

---

## Sobre

**OrderFlow** é um sistema de gerenciamento de pedidos construído com **microserviços .NET 10**, projetado para demonstrar padrões enterprise do mundo real exigidos no mercado .NET brasileiro.

O domínio é intencionalmente simples (produtos + pedidos). A arquitetura é intencionalmente rica — cada decisão mapeia uma competência cobrada em vagas de nível pleno/sênior.

```
Usuário se registra → Faz login → Navega catálogo → Cria pedido → Pedido transiciona → Notificação dispara
```

### O Que É

- Um **projeto de portfólio** demonstrando padrões enterprise .NET de ponta a ponta
- Um **laboratório de aprendizado** com 15 fases de documentação progressiva (PT-BR)
- Uma **implementação de referência** de Clean Architecture + DDD + CQRS + Event-Driven

### O Que NÃO É

- Um e-commerce de produção (sem carrinho, pagamentos, frete, controle de estoque)
- Over-engineering por si só — cada padrão tem justificativa documentada

---

## Status Atual

> **Fase 1 Completa — Fundação & Serviço de Catálogo**

O projeto está sendo construído incrementalmente ao longo de 15 fases. Cada fase adiciona novos serviços, padrões e infraestrutura.

| Fase | Tópico | Status |
|------|--------|--------|
| 1 | Fundação, Clean Architecture, Catalog API | **Concluída** |
| 2 | Padrões Táticos DDD (Domínio Orders) | Planejada |
| 3 | CQRS + MediatR + Dapper | Planejada |
| 4 | Autenticação (JWT, Identity) | Planejada |
| 5 | Mensageria (RabbitMQ, MassTransit, Outbox) | Planejada |
| 6 | Cache (Redis) + Observabilidade (OpenTelemetry) | Planejada |
| 7 | API Gateway (YARP) + Docker | Planejada |
| 8 | CI/CD (GitHub Actions) + Cloud (Azure) | Planejada |
| 9 | Resiliência (Polly v8) | Planejada |
| 10 | Performance + C# Moderno | Planejada |
| 11 | Kubernetes + Service Mesh | Planejada |
| 12 | OAuth2 / IdentityServer | Planejada |
| 13 | gRPC + Kafka + Event Sourcing | Planejada |
| 14 | Feature Flags + SRE | Planejada |
| 15 | Integração com IA | Planejada |

### O Que Está Implementado Agora

- **Catalog API** — CRUD completo de Produtos e Categorias (Controllers, Clean Architecture 4 camadas)
- **SharedKernel** — Entity base, AuditableEntity, IDomainEvent, IRepository, IUnitOfWork
- **Infraestrutura** — SQL Server + EF Core com configurações via Fluent API
- **Logging** — Serilog com sinks Console + Seq, enrichers (Environment, Thread)
- **Validação** — FluentValidation para todos os DTOs
- **Health Checks** — Endpoints de Liveness (`/health/live`) e Readiness (`/health/ready`)
- **Testes** — Testes de integração com WebApplicationFactory (Categories + Products)
- **Docker Compose** — SQL Server + Seq para desenvolvimento local
- **Gerenciamento Central de Pacotes** — Directory.Packages.props
- **Padronização de Build** — Directory.Build.props com TreatWarningsAsErrors, nullable, latest analysis

---

## Arquitetura

### Arquitetura Alvo (Visão Completa)

```
                          ┌──────────────────┐
                          │   Client (HTTP)  │
                          └────────┬─────────┘
                                   │
                          ┌────────▼─────────┐
                          │   API Gateway    │
                          │     (YARP)       │  Fase 7
                          │  Rate Limiting   │
                          │  Auth Forwarding │
                          └──┬─────┬──────┬──┘
                             │     │      │
            ┌────────────────┘     │      └───────────────┐
            │                      │                      │
   ┌────────▼─────────┐   ┌────────▼────────┐   ┌─────────▼────────┐
   │  Identity API    │   │  Catalog API    │   │   Orders API     │
   │  JWT + Refresh   │   │  Cache-Aside    │   │  CQRS + MediatR  │
   │  ASP.NET Identity│   │  Controllers    │   │  DDD Aggregates  │
   │                  │   │                 │   │                  │
   │  Fase 4          │   │  Fase 1         │   │  Fases 2-3       │
   │  ┌────────────┐  │   │  ┌────────────┐ │   │  ┌────────────┐  │
   │  │ SQL Server │  │   │  │ SQL Server │ │   │  │ SQL Server │  │
   │  └────────────┘  │   │  └────────────┘ │   │  └────────────┘  │
   └──────────────────┘   └─────────────────┘   └───────┬──────────┘
                                                        │
                                               Outbox Pattern
                                               Fase 5   │
                                               ┌────────▼─────────┐
                                               │    RabbitMQ      │
                                               └────────┬─────────┘
                                                        │
                                               ┌────────▼─────────┐
                                               │  Notification    │
                                               │  Worker Service  │  Fase 5
                                               └──────────────────┘

   ┌──────────────────────────────────────────────────────────────┐ 
   │  Redis (6) · Serilog + Seq (1) · OpenTelemetry (6)           │
   │  Docker Compose (1/7) · GitHub Actions (8) · Azure (8)       │
   └──────────────────────────────────────────────────────────────┘
```

### Decisões Arquiteturais

| # | Decisão | Justificativa |
|---|---------|---------------|
| ADR-001 | .NET 10 / C# 13 | Runtime mais recente, primary constructors, collection expressions |
| ADR-002 | EF Core (escrita) + Dapper (leitura) | Change tracking para commands, SQL puro para queries |
| ADR-003 | MassTransit sobre RabbitMQ Client | Outbox, retry, saga e test harness integrados |
| ADR-004 | MediatR para CQRS | Padrão de mercado, pipeline behaviors para cross-cutting |
| ADR-005 | YARP como Gateway | Mantido pela Microsoft, performance superior, hot-reload |
| ADR-006 | SQL Server por serviço | Padrão database-per-service; DB mais requisitado no mercado BR |
| ADR-007 | Serilog + Seq + OpenTelemetry | Logging estruturado + tracing distribuído vendor-neutral |

> Os ADRs 001, 006 e 007 (Serilog + Seq) estão ativos na implementação atual. Os demais serão realizados conforme suas respectivas fases forem construídas.

---

## Microserviços

| Serviço | Responsabilidade | Estilo | Status |
|---------|-----------------|--------|--------|
| **Catalog API** | Gerenciamento de produtos e categorias | Controllers | **Implementado** |
| **Orders API** | Gerenciamento do ciclo de vida de pedidos | Minimal APIs | Fase 2-3 |
| **Identity API** | Autenticação e autorização | Minimal APIs | Fase 4 |
| **Notification Worker** | Consumo de eventos e notificações | Worker Service | Fase 5 |
| **API Gateway** | Ponto único de entrada e roteamento | YARP | Fase 7 |

### Catalog API (Atual)

| Padrão | Detalhes |
|--------|----------|
| Clean Architecture | 4 camadas: Domain → Application → Infrastructure → Api |
| Repository Pattern | `IRepository<T>` genérico + `ICategoryRepository`, `IProductRepository` específicos |
| FluentValidation | Validators de Create/Update para Products e Categories |
| Serilog | Sinks Console + Seq, enrichers de Environment + Thread |
| Health Checks | Readiness (SQL Server) + Liveness |
| EF Core | Configurações via Fluent API, auto-migration em Development |
| Global Exception Handler | Middleware centralizado com respostas ProblemDetails |

---

## Stack Tecnológica

### Em Uso Atualmente

| Camada | Tecnologia | Versão |
|--------|-----------|--------|
| **Runtime** | .NET / C# | 10.0 / 13 |
| **Web** | ASP.NET Core (Controllers) | 10.0 |
| **ORM** | Entity Framework Core | 10.0.6 |
| **Banco de Dados** | SQL Server | 2022 |
| **Validação** | FluentValidation | 12.1.1 |
| **Logging** | Serilog + Seq | 10.0.0 |
| **Documentação de API** | Swashbuckle (Swagger) | 10.1.7 |
| **Testes** | xUnit + FluentAssertions + Moq | 3.2.2 / 8.9.0 / 4.20.72 |
| **Containers** | Docker Compose | Latest |

### Planejado (Fases Futuras)

| Tecnologia | Propósito | Fase |
|-----------|-----------|------|
| Dapper | Queries de leitura de alta performance | 3 |
| MediatR | Pipeline CQRS + behaviors | 3 |
| ASP.NET Identity + JWT | Autenticação e autorização | 4 |
| RabbitMQ + MassTransit | Mensageria assíncrona, outbox, retry | 5 |
| Redis | Cache distribuído (cache-aside) | 6 |
| OpenTelemetry | Tracing distribuído + métricas | 6 |
| YARP | API Gateway, reverse proxy | 7 |
| Testcontainers | Containers reais em testes de integração | 7 |
| GitHub Actions | Pipelines CI/CD | 8 |
| Azure Container Apps + Bicep | Deploy em cloud, IaC | 8 |
| Polly v8 | Resiliência (retry, circuit breaker) | 9 |

---

## Como Rodar

### Pré-requisitos

| Ferramenta | Versão | Obrigatório |
|-----------|--------|-------------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Sim |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | Sim |
| [Git](https://git-scm.com/) | 2.x+ | Sim |

### Início Rápido

```bash
# 1. Clone o repositório
git clone https://github.com/seu-usuario/orderflow.git
cd orderflow

# 2. Suba a infraestrutura (SQL Server + Seq)
docker compose -f backend/docker/docker-compose.yml up -d

# 3. Execute a Catalog API (migrations são aplicadas automaticamente em Development)
dotnet run --project backend/src/Services/Catalog/OrderFlow.Catalog.Api
```

A Catalog API inicia em `https://localhost:5050` (verifique `Properties/launchSettings.json` para a porta exata).

### URLs dos Serviços

| Serviço | URL | Observações |
|---------|-----|-------------|
| Catalog API | `https://localhost:5050` | Swagger UI disponível em `/swagger` |
| Seq (Logs) | `http://localhost:5341` | Visualizador de logs estruturados |
| SQL Server | `localhost,1433` | Senha do SA em `docker/.env` |

### Teste Rápido

```bash
# Listar produtos
curl https://localhost:5050/api/products

# Listar categorias
curl https://localhost:5050/api/categories

# Criar uma categoria
curl -X POST https://localhost:5050/api/categories \
  -H "Content-Type: application/json" \
  -d '{"name":"Eletrônicos","description":"Produtos eletrônicos"}'

# Health checks
curl https://localhost:5050/health/live
curl https://localhost:5050/health/ready
```

### Executar Testes

```bash
# Todos os testes
dotnet test backend/OrderFlow.slnx

# Testes de integração do Catalog
dotnet test backend/tests/OrderFlow.Catalog.Api.Tests
```

---

## Estrutura do Projeto

### Implementação Atual

```
OrderFlow/
├── backend/
│   ├── src/
│   │   ├── BuildingBlocks/
│   │   │   └── OrderFlow.SharedKernel/             # Entity, AuditableEntity, IRepository, IUnitOfWork
│   │   └── Services/
│   │       └── Catalog/
│   │           ├── OrderFlow.Catalog.Api/           # Controllers, Middleware, Health Checks, Serilog
│   │           ├── OrderFlow.Catalog.Application/   # DTOs, Services, Validators
│   │           ├── OrderFlow.Catalog.Domain/        # Entidades (Product, Category), Interfaces
│   │           └── OrderFlow.Catalog.Infrastructure/ # EF Core, DbContext, Repositórios
│   ├── tests/
│   │   └── OrderFlow.Catalog.Api.Tests/             # Testes de integração (WebApplicationFactory)
│   ├── docker/
│   │   ├── docker-compose.yml                       # SQL Server + Seq
│   │   └── .env                                     # Variáveis de ambiente
│   ├── OrderFlow.slnx                               # Arquivo de solution
│   ├── Directory.Build.props                        # MSBuild compartilhado (net10.0, nullable, warnings-as-errors)
│   ├── Directory.Packages.props                     # Gerenciamento Central de Pacotes
│   └── global.json                                  # Pin do SDK: 10.0.201
├── docs/                                            # Guia de aprendizado em 15 fases (PT-BR)
├── LICENSE
└── README.md
```

### Regra de Dependência

```
Domain ← Application ← Infrastructure ← Api
  │                                        │
  └──────── SharedKernel ──────────────────┘
```

> **Domain** tem zero dependências externas. **Application** referencia apenas Domain. **Infrastructure** implementa interfaces. **Api** é a composition root.

---

## Documentação

O projeto inclui um **guia de aprendizado em 15 fases** escrito em português, projetado como um tutorial progressivo da fundação ao deploy em cloud e além.

### Fases Fundamentais (1-8)

| Fase | Tópico | Documento |
|------|--------|-----------|
| 0 | Visão Geral, ADRs, Glossário | [`00-visao-geral.md`](docs/00-visao-geral.md) |
| 1 | Clean Architecture, SharedKernel, Docker | [`fase-01-fundacao-estrutura.md`](docs/fase-01-fundacao-estrutura.md) |
| 2 | DDD, Aggregates, Value Objects, Events | [`fase-02-dominio-ddd.md`](docs/fase-02-dominio-ddd.md) |
| 3 | CQRS, MediatR, Pipelines, Dapper | [`fase-03-cqrs-application.md`](docs/fase-03-cqrs-application.md) |
| 4 | JWT, Refresh Tokens, Rate Limiting | [`fase-04-autenticacao-seguranca.md`](docs/fase-04-autenticacao-seguranca.md) |
| 5 | RabbitMQ, MassTransit, Outbox Pattern | [`fase-05-mensageria-async.md`](docs/fase-05-mensageria-async.md) |
| 6 | Redis, OpenTelemetry, Serilog | [`fase-06-cache-observabilidade.md`](docs/fase-06-cache-observabilidade.md) |
| 7 | YARP Gateway, Docker, Testcontainers | [`fase-07-gateway-docker.md`](docs/fase-07-gateway-docker.md) |
| 8 | GitHub Actions, Azure, Bicep | [`fase-08-cicd-cloud.md`](docs/fase-08-cicd-cloud.md) |

### Fases Avançadas (9-15)

| Fase | Tópico | Documento |
|------|--------|-----------|
| 9 | Resiliência com Polly v8 | [`fase-09-resiliencia-polly.md`](docs/fase-09-resiliencia-polly.md) |
| 10 | Performance + C# Moderno | [`fase-10-performance-csharp-moderno.md`](docs/fase-10-performance-csharp-moderno.md) |
| 11 | Kubernetes + Service Mesh | [`fase-11-kubernetes-service-mesh.md`](docs/fase-11-kubernetes-service-mesh.md) |
| 12 | OAuth2 / IdentityServer | [`fase-12-oauth2-identityserver.md`](docs/fase-12-oauth2-identityserver.md) |
| 13 | gRPC + Kafka + Event Sourcing | [`fase-13-grpc-kafka-eventsourcing.md`](docs/fase-13-grpc-kafka-eventsourcing.md) |
| 14 | Feature Flags + SRE | [`fase-14-feature-flags-sre.md`](docs/fase-14-feature-flags-sre.md) |
| 15 | Integração com IA | [`fase-15-ai-integration.md`](docs/fase-15-ai-integration.md) |

Cada fase inclui:
- Objetivos de aprendizado — o que você vai construir e por quê
- Sumário de artefatos — todos os arquivos criados na fase
- Preparação para entrevistas — 5+ perguntas com respostas articuladas por fase
- Checklist de competências — progresso rastreável ao longo de todas as fases

---

## Testes

| Camada | Framework | O Que é Testado |
|--------|-----------|-----------------|
| **Integração** | xUnit + WebApplicationFactory + FluentAssertions | Pipeline HTTP completo: Controllers, Validação, EF Core, SQL Server |

```bash
# Executar todos os testes
dotnet test backend/OrderFlow.slnx

# Executar com cobertura
dotnet test backend/OrderFlow.slnx --collect:"XPlat Code Coverage"
```

> Conforme novas fases são construídas, os testes expandem para incluir testes unitários (Domain), testes de handlers (Application com Moq), e Testcontainers para infraestrutura real em CI.

---

## Padrões Implementados

| Padrão | Onde | Detalhes |
|--------|------|----------|
| Clean Architecture (4 camadas) | Serviço Catalog | Domain → Application → Infrastructure → Api |
| Repository Pattern | SharedKernel + Catalog.Infrastructure | `IRepository<T>` genérico com implementação EF Core |
| Unit of Work | SharedKernel + Catalog.Infrastructure | `IUnitOfWork` com `SaveChangesAsync` |
| Validação Centralizada | Catalog.Application | FluentValidation com integração via DI |
| Tratamento Global de Exceções | Catalog.Api | Middleware com respostas ProblemDetails |
| Logging Estruturado | Catalog.Api | Serilog com sink Seq e enrichers |
| Health Checks | Catalog.Api | Liveness + Readiness (SQL Server) |
| Auto-Migration | Catalog.Api | EF Core `MigrateAsync()` em Development |
| Gerenciamento Central de Pacotes | Toda a solution | Directory.Packages.props |

---

## Roadmap

```
Fase 1 ✅ ── Fase 2 ── Fase 3 ── Fase 4 ── Fase 5 ── Fase 6 ── Fase 7 ── Fase 8
    │           │          │         │          │         │         │         │
    ▼           ▼          ▼         ▼          ▼         ▼         ▼         ▼
Fundação      DDD       CQRS      Auth     Mensageria  Cache    Gateway   CI/CD
Catalog API   Orders    MediatR    JWT      RabbitMQ    Redis    YARP      Azure
SharedKernel  Domínio   Dapper    Identity  Outbox      OTel     Docker    Deploy

Fase 9 ── Fase 10 ── Fase 11 ── Fase 12 ── Fase 13 ── Fase 14 ── Fase 15
    │         │          │          │           │          │          │
    ▼         ▼          ▼          ▼           ▼          ▼          ▼
Resiliência Performance Kubernetes OAuth2/OIDC  gRPC      Feature    IA
Polly v8    Span<T>     Helm       Duende IS    Kafka     Flags      Semantic
Chaos Eng   Benchmarks  HPA        Token Mgmt   EventSrc  SRE        Kernel
```

---

## Contribuindo

Contribuições são bem-vindas! Siga estas convenções:

1. **Branches:** `feature/tópico`, `fix/tópico`, `docs/tópico`
2. **Commits:** [Conventional Commits](https://www.conventionalcommits.org/)
   ```
   feat(catalog): adicionar paginação ao endpoint de produtos
   fix(catalog): tratar nome de categoria duplicado
   docs(fase-02): adicionar diagrama de aggregates DDD
   ```
3. **Testes:** Todas as novas features devem incluir testes
4. **PRs:** Referencie a fase relacionada na descrição

---

## Licença

Este projeto está licenciado sob a [Licença MIT](LICENSE).

---

<p align="center">
  Construído como projeto de aprendizado e portfólio para o ecossistema enterprise .NET.
  <br />
  <strong>OrderFlow</strong> — da arquitetura ao deploy em cloud.
</p>
