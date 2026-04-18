# OrderFlow — Visão Geral da Solução

> **Versão:** 2.0 — Reformulação Sênior-Grade
> **Última atualização:** Abril 2026
> **Runtime:** .NET 10 / C# 13
> **Tipo:** Projeto de Portfólio Enterprise alinhado às exigências de vagas Pleno/Sênior 2026

---

## Sumário

1. [Para Quem é Este Projeto](#1-para-quem-é-este-projeto)
2. [Alinhamento com o Mercado .NET 2026](#2-alinhamento-com-o-mercado-net-2026)
3. [O Que é o OrderFlow](#3-o-que-é-o-orderflow)
4. [Visão Arquitetural](#4-visão-arquitetural)
5. [Microserviços e Responsabilidades](#5-microserviços-e-responsabilidades)
6. [Stack Tecnológica Completa](#6-stack-tecnológica-completa)
7. [Architecture Decision Records (ADRs)](#7-architecture-decision-records-adrs)
8. [Estrutura da Solution](#8-estrutura-da-solution)
9. [Padrões Arquiteturais](#9-padrões-arquiteturais)
10. [Roadmap de 15 Fases](#10-roadmap-de-15-fases)
11. [Matriz de Competências Pleno vs Sênior](#11-matriz-de-competências-pleno-vs-sênior)
12. [Convenções do Projeto](#12-convenções-do-projeto)
13. [Como Rodar Localmente](#13-como-rodar-localmente)
14. [Glossário](#14-glossário)
15. [Perguntas de Entrevista — Sênior](#15-perguntas-de-entrevista--sênior)

---

## 1. Para Quem é Este Projeto

O OrderFlow foi reformulado em 2026 para mapear **um a um** os requisitos das vagas **Pleno e Sênior** de .NET no Brasil. Se você é:

- **Pleno** querendo subir para Sênior — vai praticar arquitetura, padrões avançados (CQRS, DDD tático, Outbox, Saga), resiliência (Polly v8), observabilidade (OpenTelemetry, SLO/SLI) e DevOps cloud-native.
- **Sênior** consolidando portfólio — vai ter um repositório que demonstra **decisão técnica** em todas as dimensões cobradas: performance, segurança, resiliência, escalabilidade, testabilidade.
- **Recrutador / Tech Lead** avaliando candidato — vai encontrar ADRs, métricas, testes de integração com Testcontainers, pipelines CI/CD reais e infraestrutura como código.

> **Filosofia:** *"Domínio simples, arquitetura rica."* O negócio (pedidos) cabe em uma página; a arquitetura cobre 32+ tópicos enterprise.

---

## 2. Alinhamento com o Mercado .NET 2026

Esta tabela mapeia os requisitos recorrentes em **descrições reais de vagas Pleno/Sênior** (Brasil, 2025–2026) ao escopo do OrderFlow:

### 2.1 Requisitos Pleno (3+ anos)

| Requisito do Mercado | Coberto em | Profundidade |
|---|---|---|
| C# moderno (12/13) + .NET 10 | Toda a solution | ⭐⭐⭐⭐⭐ |
| ASP.NET Core APIs REST (Controllers + Minimal) | Catalog API + Orders API | ⭐⭐⭐⭐⭐ |
| EF Core + LINQ avançado | Repositórios, queries compiladas, split queries | ⭐⭐⭐⭐⭐ |
| SQL Server + modelagem | Database per Service, índices, concurrency | ⭐⭐⭐⭐ |
| Git, Conventional Commits, PRs | CI bloqueia merge sem padrão | ⭐⭐⭐⭐ |
| xUnit + Moq + FluentAssertions | Unit + Integration tests | ⭐⭐⭐⭐⭐ |
| Docker + multi-stage builds | Imagens < 90MB com não-root | ⭐⭐⭐⭐⭐ |
| CI/CD básico | GitHub Actions completo | ⭐⭐⭐⭐⭐ |
| Cloud (Azure) básico | Container Apps + Bicep IaC | ⭐⭐⭐⭐ |
| SOLID + Clean Code | Aplicado e revisado em cada fase | ⭐⭐⭐⭐⭐ |

### 2.2 Requisitos Sênior (6–8+ anos)

| Requisito do Mercado | Coberto em | Profundidade |
|---|---|---|
| Microserviços + Database per Service | 4 serviços + Gateway | ⭐⭐⭐⭐⭐ |
| Mensageria (RabbitMQ, Kafka, SQS) | RabbitMQ + Kafka comparativo (Fase 13) | ⭐⭐⭐⭐⭐ |
| DDD tático (Aggregates, VOs, Eventos) | Orders bounded context | ⭐⭐⭐⭐⭐ |
| CQRS + Event Sourcing + CDC | CQRS na Fase 03; Event Sourcing/CDC na Fase 13 | ⭐⭐⭐⭐ |
| Cache distribuído (Redis) | Cache-aside + decorator pattern | ⭐⭐⭐⭐⭐ |
| Resiliência (Polly v8: retry, CB, bulkhead, hedging) | **Fase 09 dedicada** | ⭐⭐⭐⭐⭐ |
| Performance + profiling (BenchmarkDotNet, Span\<T\>) | **Fase 10 dedicada** | ⭐⭐⭐⭐⭐ |
| Async avançado (ValueTask, IAsyncEnumerable, Channels) | Fase 10 | ⭐⭐⭐⭐⭐ |
| Kubernetes + Helm + HPA | **Fase 11 dedicada** | ⭐⭐⭐⭐ |
| OAuth2 / OIDC / IdentityServer (Duende) | **Fase 12 dedicada** | ⭐⭐⭐⭐⭐ |
| gRPC para comunicação interna | Fase 13 | ⭐⭐⭐⭐ |
| Observabilidade (OTel, traces, métricas, SLO/SLI) | Fase 06 + **Fase 14 (SRE)** | ⭐⭐⭐⭐⭐ |
| Feature Flags (release independente) | Fase 14 | ⭐⭐⭐⭐ |
| Integração com IA (Semantic Kernel, AI Gateway) | **Fase 15 dedicada** | ⭐⭐⭐⭐ |
| Liderança técnica (ADRs, code review, mentoria) | ADRs em cada decisão; templates de PR | ⭐⭐⭐⭐ |
| Segurança (OWASP Top 10, secrets, hardening) | Fase 04 + Fase 12 | ⭐⭐⭐⭐⭐ |

### 2.3 O Que Diferencia Este Portfólio

| Sinal | Por Que Importa |
|---|---|
| **ADRs em cada decisão** | Demonstra pensamento crítico — sênior decide, não só implementa |
| **Trade-offs documentados** | Prova que você conhece *alternativas* (não só a solução escolhida) |
| **Testcontainers em todos os testes de integração** | Sinaliza maturidade DevOps — testes não dependem do "funciona no meu PC" |
| **Polly v8 + Chaos Engineering** | Praticamente ausente em portfólios típicos — diferenciação imediata |
| **OpenTelemetry com semantic conventions** | Padrão CNCF — aparece em vagas top-tier |
| **Bicep IaC + GitHub Environments com approval** | Mostra que você pensa em segurança de pipeline |
| **Duende IdentityServer real (não JWT artesanal)** | Identity-as-a-Service é o que rodízio empresarial usa |

---

## 3. O Que é o OrderFlow

Sistema simplificado de gestão de pedidos estruturado em microserviços. Simula o fluxo:

```
Usuário se cadastra → Login → Consulta produtos → Cria pedido → Status muda → Notificação dispara
```

### Escopo Intencionalmente Limitado

| O que **é** | O que **não é** |
|---|---|
| Laboratório arquitetural enterprise | E-commerce real (sem carrinho, frete, pagamento) |
| Demonstração de padrões e decisões técnicas | Substituto de produção (não tem SLA, suporte) |
| Repositório referenciável em entrevistas | Tutorial de "olá mundo" |
| Foco em *como* construir | Foco no *o quê* construir |

---

## 4. Visão Arquitetural

### 4.1 Diagrama de Alto Nível

```
                    ┌──────────────────────────┐
                    │   Cliente Web/Mobile      │
                    └──────────────┬───────────┘
                                   │ HTTPS + JWT
                          ┌────────▼─────────┐
                          │  API Gateway     │  YARP
                          │  (rate limit,    │
                          │   auth fwd, CORS)│
                          └─┬──────┬──────┬──┘
              ┌─────────────┘      │      └──────────────┐
              │                    │                     │
     ┌────────▼────────┐  ┌────────▼────────┐  ┌─────────▼────────┐
     │ Identity API    │  │ Catalog API     │  │  Orders API ⭐   │
     │ Duende IS       │  │ Controllers     │  │ Minimal API      │
     │ OAuth2 + OIDC   │  │ Cache-Aside     │  │ CQRS + DDD       │
     │ Refresh Rotation│  │ FluentValidation│  │ Outbox + Polly   │
     └────────┬────────┘  └────────┬────────┘  └────────┬─────────┘
              │                    │                    │
        ┌─────▼────┐         ┌─────▼────┐         ┌─────▼────┐
        │ SQL ID   │         │ SQL+Redis│         │ SQL Order│
        └──────────┘         └──────────┘         └──────────┘
                                                       │
                                              ┌────────▼─────────┐
                                              │   RabbitMQ       │
                                              │ (Outbox Consumer)│
                                              └────────┬─────────┘
                                                       │
                                              ┌────────▼─────────┐
                                              │ Notification     │
                                              │ Worker (idempot) │
                                              └──────────────────┘

  ─── INFRAESTRUTURA TRANSVERSAL ───────────────────────────────────
   Redis · Serilog · Seq · OpenTelemetry · Prometheus · Grafana
   Docker Compose · Testcontainers · GitHub Actions · Azure ACA
   Polly v8 · Feature Flags · Semantic Kernel (Fase 15)
```

### 4.2 Princípios Arquiteturais

| Princípio | Aplicação no OrderFlow |
|---|---|
| **Database per Service** | Cada microserviço tem schema isolado. Nunca há JOIN cross-database. |
| **Comunicação Assíncrona por padrão** | Serviços trocam dados via eventos. HTTP síncrono apenas via Gateway. |
| **Smart Endpoints, Dumb Pipes** | Lógica nos serviços; broker apenas transporta. |
| **Design for Failure** | Polly retry/CB, DLQ, idempotência, timeouts em tudo. |
| **Observability First** | Sem trace/log/metric, não merge. PRs validam telemetria. |
| **Secure by Default** | OAuth2/OIDC, secrets em Key Vault, OWASP Top 10 checado. |
| **Cloud-Native Ready** | 12-factor app, stateless, config externalizada. |

---

## 5. Microserviços e Responsabilidades

### 5.1 Identity API (Fase 04 + 12)

| Aspecto | Detalhe |
|---|---|
| Framework | ASP.NET Core + **Duende IdentityServer 7** |
| Protocolo | OAuth2 + OpenID Connect (Authorization Code + PKCE, Client Credentials) |
| Tokens | JWT assinado RS256 + Refresh Rotation com detecção de reuse |
| Banco | SQL Server (configuração persistida do IdentityServer) |
| Padrão | Identity-as-a-Service — outros serviços validam JWT localmente |

### 5.2 Catalog API (Fase 01 + 06)

| Aspecto | Detalhe |
|---|---|
| Framework | ASP.NET Core Controllers (versionamento, paginação, OData opcional) |
| Padrões | Clean Architecture · Decorator (cache) · Repository |
| Cache | Redis via `IDistributedCache` (cache-aside) + Output Caching |
| Validação | FluentValidation com pipeline behavior |
| Banco | SQL Server + EF Core 10 (Code First, value converters, owned types) |

### 5.3 Orders API ⭐ — Serviço de Referência (Fases 02, 03, 05, 09)

| Aspecto | Detalhe |
|---|---|
| Framework | ASP.NET Core **Minimal APIs** (route groups, typed results) |
| Padrões | Clean Architecture + DDD tático + CQRS + Outbox |
| Comandos | Create / AddItem / Confirm / Cancel / Ship (state machine validada) |
| Queries | Dapper com SQL otimizado, paginação keyset, projeções |
| Eventos | Domain Events (MediatR) + Integration Events (MassTransit Outbox) |
| Resiliência | Polly v8 pipelines em chamadas externas (Catalog, Pagamento mock) |

### 5.4 Notification Worker (Fase 05 + 09)

| Aspecto | Detalhe |
|---|---|
| Tipo | `BackgroundService` + MassTransit consumers |
| Idempotência | Tabela `ProcessedMessages` + filtro MassTransit (Redis-backed) |
| Resiliência | Retry exponencial + Circuit Breaker + DLQ |
| Observabilidade | TraceId propagado do producer (W3C Trace Context) |

### 5.5 API Gateway — YARP (Fase 07)

| Aspecto | Detalhe |
|---|---|
| Framework | YARP 2.x (mantido pela Microsoft) |
| Features | Routing, rate limiting, header forwarding, request transforms, CORS |
| Configuração | `appsettings.json` hot-reloadable + endpoint para health agregado |

---

## 6. Stack Tecnológica Completa

### 6.1 Runtime e Linguagem

| Camada | Tecnologia | Versão | Justificativa |
|---|---|---|---|
| Runtime | **.NET** | 10 | LTS, performance ~20% melhor que .NET 8, AOT maduro |
| Linguagem | **C#** | 13 | Primary constructors, collection expressions, params collections |
| Web | **ASP.NET Core** | 10 | Minimal APIs + Controllers, source generators |

### 6.2 Persistência

| Componente | Tecnologia | Uso |
|---|---|---|
| ORM (escrita) | **Entity Framework Core 10** | Commands, change tracking, migrations, value converters, interceptors |
| Micro-ORM (leitura) | **Dapper 2.x** | Queries CQRS, projeções otimizadas, paginação keyset |
| Banco relacional | **SQL Server 2022** | Padrão do mercado .NET BR; compatible com Azure SQL |
| Cache distribuído | **Redis 7.x** | `IDistributedCache`, distributed locks, idempotência |

### 6.3 Mensageria e Resiliência

| Componente | Tecnologia | Uso |
|---|---|---|
| Message Broker | **RabbitMQ 4.x** | Eventos de integração (eventos de domínio cruzando bounded contexts) |
| Streaming alternativo | **Kafka** (Fase 13 comparativa) | Event sourcing, CDC, alto throughput |
| Abstração | **MassTransit 8.x** | Outbox nativo, retry, saga state machine, test harness |
| Resiliência | **Polly 8.x** | Pipelines: retry, circuit breaker, bulkhead, timeout, hedging |
| In-process | **MediatR 12.x** | CQRS, pipeline behaviors |

### 6.4 Identity, Segurança e RPC

| Componente | Tecnologia | Uso |
|---|---|---|
| Identity Server | **Duende IdentityServer 7** | OAuth2 + OIDC completos |
| Validação | **FluentValidation 11.x** | Regras declarativas com integração MediatR |
| API Gateway | **YARP 2.x** | Reverse proxy + rate limiting + routing |
| RPC binário | **gRPC** + Protobuf | Comunicação interna entre serviços (Fase 13) |
| Feature Flags | **Microsoft.FeatureManagement** + Azure App Configuration | Release toggle, A/B (Fase 14) |

### 6.5 Observabilidade

| Componente | Tecnologia | Uso |
|---|---|---|
| Logs estruturados | **Serilog 4.x** | Sinks para console, Seq, Elasticsearch |
| Log viewer (dev) | **Seq** | Pesquisa por propriedade tipada |
| Telemetria distribuída | **OpenTelemetry 1.x** | Traces, metrics, logs com semantic conventions |
| Métricas viz | **Prometheus + Grafana** | Dashboards RED/USE, SLO/SLI |
| Tracing viz | **Jaeger / Tempo** | Traces distribuídos |
| Health Checks | **AspNetCore.HealthChecks** | Liveness, readiness, startup probes |

### 6.6 Testes e Qualidade

| Componente | Tecnologia | Uso |
|---|---|---|
| Test framework | **xUnit 2.x** | Unit + integration |
| Mocking | **NSubstitute 5.x** ou Moq | Mocks legíveis |
| Assertions | **FluentAssertions 7.x** | Assertions expressivas |
| Integration | **WebApplicationFactory** + **Testcontainers 4.x** | SQL/RabbitMQ/Redis reais em containers |
| Benchmarks | **BenchmarkDotNet** | Comparação Span vs string, Dapper vs EF (Fase 10) |
| Contract testing | **Pact .NET** | Validação de contratos entre serviços (Fase 13) |

### 6.7 DevOps e Cloud

| Componente | Tecnologia | Uso |
|---|---|---|
| Container | **Docker** + Compose | Dev local + builds multi-stage |
| Orquestração local | Docker Compose | 11+ containers com healthchecks |
| Orquestração prod | **Azure Container Apps** (default) ou **Kubernetes/Helm** (Fase 11) | Auto-scaling, blue-green |
| CI/CD | **GitHub Actions** | Build, test, push, deploy com environments |
| IaC | **Bicep** | Provisionamento Azure declarativo |
| Secrets | **Azure Key Vault** + GitHub Secrets | Sem segredos em repositório |
| AI | **Semantic Kernel** + AI Gateway pattern (Fase 15) | RAG, function calling, sumarização |

---

## 7. Architecture Decision Records (ADRs)

Cada decisão maior é registrada em ADR. Resumo das principais:

| ADR | Decisão | Trade-off Aceito |
|---|---|---|
| **ADR-001** | .NET 10 + C# 13 (não .NET 8 LTS) | Pacotes terceiros podem demorar a suportar |
| **ADR-002** | EF Core (write) + Dapper (read) | Duas tecnologias para gerenciar |
| **ADR-003** | MassTransit sobre RabbitMQ.Client | Camada extra de abstração; ganho enorme em outbox/retry/saga |
| **ADR-004** | MediatR para CQRS in-process | Licença comercial em uso intensivo desde v12; alternativa: Wolverine |
| **ADR-005** | YARP como Gateway | Sem features de Ocelot (multi-tenancy nativo) |
| **ADR-006** | SQL Server (não PostgreSQL) | Custo Azure SQL > Postgres; mas alinhado com vagas BR |
| **ADR-007** | Serilog + OpenTelemetry (não App Insights direto) | Boilerplate maior; vendor-neutral |
| **ADR-008** | **Duende IdentityServer** (não JWT artesanal) | Licença comercial acima de US$1M de receita; padrão de mercado |
| **ADR-009** | **Polly v8 pipelines** (não retry artesanal) | Curva de aprendizado; resiliência de produção |
| **ADR-010** | **gRPC para comunicação interna**, REST público | Dois protocolos; performance + contratos fortes internos |
| **ADR-011** | **Bicep** sobre Terraform/Pulumi | Lock-in Azure; menor curva para times .NET |
| **ADR-012** | Testcontainers em integration tests (não SQLite/in-memory) | Tests mais lentos; correção real de comportamento |
| **ADR-013** | **Outbox** sobre 2PC distribuído | Latência maior; sem coordenador distribuído frágil |
| **ADR-014** | **Semantic Kernel** (não LangChain.NET) | Comunidade menor; suporte first-party Microsoft |

> Cada ADR completo vive em `docs/adrs/NNN-titulo.md` no formato MADR.

---

## 8. Estrutura da Solution

```
OrderFlow/
├── src/
│   ├── ApiGateway/
│   │   └── OrderFlow.Gateway/                # YARP
│   ├── Services/
│   │   ├── Identity/                          # Duende IdentityServer
│   │   ├── Catalog/                           # Clean Arch + Cache
│   │   ├── Orders/                            # ⭐ DDD + CQRS + Outbox + Polly
│   │   └── Notification/                      # Worker + Consumers
│   └── BuildingBlocks/
│       ├── OrderFlow.SharedKernel/            # Entity, AggregateRoot, Result, ValueObject
│       ├── OrderFlow.MessageContracts/        # Eventos de integração
│       ├── OrderFlow.Resilience/              # Pipelines Polly reutilizáveis (Fase 09)
│       └── OrderFlow.Observability/           # OTel setup, logging extensions (Fase 06)
│
├── tests/
│   ├── *.UnitTests/                           # Domínio puro, handlers
│   ├── *.IntegrationTests/                    # Testcontainers (SQL, Rabbit, Redis)
│   ├── *.ContractTests/                       # Pact provider/consumer (Fase 13)
│   └── *.Benchmarks/                          # BenchmarkDotNet (Fase 10)
│
├── deploy/
│   ├── docker/                                # Compose multi-profile
│   ├── helm/                                  # Helm charts (Fase 11)
│   ├── bicep/                                 # IaC Azure (Fase 08)
│   └── k8s/                                   # Manifests Kubernetes (Fase 11)
│
├── .github/workflows/                         # ci.yml, cd.yml, security-scan.yml
│
├── docs/
│   ├── 00-visao-geral.md                      # Este documento
│   ├── fase-01..15-*.md                       # 15 fases progressivas
│   ├── orderflow-guide.html                   # Guia visual didático
│   └── adrs/                                  # ADRs no formato MADR
│
├── Directory.Packages.props                   # Central Package Management
├── Directory.Build.props                      # MSBuild config compartilhada
├── global.json                                # Pin SDK
├── .editorconfig                              # Code style enforced
└── .gitignore
```

### Dependency Rule

```
Domain ← Application ← Infrastructure ← Api
   │                                       │
   └──── SharedKernel ────────────────────┘
              │
        MessageContracts · Resilience · Observability
```

---

## 9. Padrões Arquiteturais

### Padrões Estruturais
- **Clean Architecture** — fronteiras explícitas, regra de dependência
- **Vertical Slice Architecture** — alternativa para serviços CRUD-heavy (Fase 01 deep dive)
- **Database per Service** — autonomia + ownership

### Padrões Táticos (DDD)
- **Aggregate Root**, **Entity**, **Value Object**, **Domain Service**
- **Domain Event** (MediatR) vs **Integration Event** (MassTransit)
- **Repository** + **Unit of Work** (atualizando o aggregate inteiro)
- **Specification Pattern** para queries complexas

### Padrões de Integração
- **CQRS** — modelos de leitura/escrita separados
- **Outbox Pattern** — at-least-once guarantee sem 2PC
- **Saga / Process Manager** — orquestração de transações distribuídas (Fase 05)
- **Idempotent Consumer** — exigência do at-least-once
- **Event Sourcing** + **CDC (Debezium)** — Fase 13 comparativa

### Padrões de Resiliência (Fase 09)
- **Retry** com backoff exponencial + jitter
- **Circuit Breaker** (closed → open → half-open)
- **Bulkhead** — isolamento de recursos
- **Timeout** — fail-fast
- **Hedging** — primeiro a responder vence
- **Fallback** — degradação graciosa

### Padrões de Observabilidade
- **The 3 Pillars** — Logs, Metrics, Traces
- **RED Method** (Rate, Errors, Duration) para serviços
- **USE Method** (Utilization, Saturation, Errors) para recursos
- **SLO/SLI/Error Budget** — Fase 14

### Padrões de Cloud-Native
- **12-Factor App** — config externalizada, stateless, logs como streams
- **Sidecar Pattern** — Dapr opcional na Fase 11
- **Strangler Fig** — quando migrar legado

---

## 10. Roadmap de 15 Fases

### Visão Macro

```
█ FUNDAÇÃO          █ DOMÍNIO          █ APPLICATION       █ SEGURANÇA
Fase 01             Fase 02             Fase 03             Fase 04
Clean Arch          DDD Tático          CQRS + MediatR      JWT + Refresh
EF Core 10          Aggregates          Dapper              Identity básico

█ INTEGRAÇÃO        █ CACHE/OBSERV.    █ EDGE              █ DEPLOY
Fase 05             Fase 06             Fase 07             Fase 08
RabbitMQ            Redis + OTel        YARP + Docker       GitHub Actions
Outbox + Saga       Serilog + Seq       Testcontainers      Azure ACA + Bicep

═══════════════ NOVO: TRILHA SÊNIOR (FASES 09-15) ═══════════════

█ RESILIÊNCIA       █ PERFORMANCE      █ ORQUESTRAÇÃO       █ IDENTITY AVANÇADA
Fase 09             Fase 10             Fase 11             Fase 12
Polly v8            Span/Channels       Kubernetes          Duende IdentityServer
Chaos Engineering   BenchmarkDotNet     Helm + HPA          OAuth2 + OIDC
                    AOT + Source Gen    Service Mesh

█ INTER-SERVIÇO     █ SRE                █ AI INTEGRATION
Fase 13             Fase 14              Fase 15
gRPC + Kafka        Feature Flags        Semantic Kernel
Event Sourcing      SLO/SLI/Budget       AI Gateway pattern
CDC (Debezium)      Pact contracts       RAG sobre catálogo
```

### Tabela Detalhada

| # | Fase | Foco | Documento | Nível |
|---|---|---|---|---|
| 01 | Fundação e Estrutura | Solution, Clean Arch, EF Core, Docker | `fase-01-fundacao-estrutura.md` | Pleno |
| 02 | Domínio Rico e DDD | Aggregates, VOs, Domain Events | `fase-02-dominio-ddd.md` | Pleno → Sênior |
| 03 | CQRS e Application | MediatR, Dapper, Pipeline Behaviors | `fase-03-cqrs-application.md` | Pleno → Sênior |
| 04 | Autenticação Básica | JWT, Refresh Token, Policies | `fase-04-autenticacao-seguranca.md` | Pleno |
| 05 | Mensageria Assíncrona | RabbitMQ, MassTransit, Outbox, Saga | `fase-05-mensageria-async.md` | Sênior |
| 06 | Cache e Observabilidade | Redis, OpenTelemetry, Serilog | `fase-06-cache-observabilidade.md` | Sênior |
| 07 | Gateway e Docker | YARP, multi-stage, Testcontainers | `fase-07-gateway-docker.md` | Pleno → Sênior |
| 08 | CI/CD e Cloud | GitHub Actions, Container Apps, Bicep | `fase-08-cicd-cloud.md` | Sênior |
| **09** | **Resiliência (Polly v8)** | Retry, CB, Bulkhead, Hedging, Chaos | `fase-09-resiliencia-polly.md` | **Sênior** |
| **10** | **Performance & C# Moderno** | Span, ValueTask, Channels, BenchmarkDotNet, AOT | `fase-10-performance-csharp-moderno.md` | **Sênior** |
| **11** | **Kubernetes & Service Mesh** | Manifests, HPA, Helm, Dapr/Linkerd | `fase-11-kubernetes-service-mesh.md` | **Sênior** |
| **12** | **OAuth2/OIDC com Duende IS** | Authorization Code + PKCE, Client Credentials | `fase-12-oauth2-identityserver.md` | **Sênior** |
| **13** | **gRPC, Kafka & Event Sourcing** | Comunicação binária, CDC, Outbox vs Sourcing | `fase-13-grpc-kafka-eventsourcing.md` | **Sênior** |
| **14** | **Feature Flags & SRE** | SLO/SLI/Error Budget, RED/USE, Pact | `fase-14-feature-flags-sre.md` | **Sênior** |
| **15** | **Integração com IA** | Semantic Kernel, AI Gateway, RAG | `fase-15-ai-integration.md` | **Sênior+** |

> **Pré-requisitos:** Fases 01-08 são lineares. Fases 09-15 podem ser estudadas em qualquer ordem após a 08.

---

## 11. Matriz de Competências Pleno vs Sênior

Marque conforme avança. Cada item rastreia uma habilidade exigida em vagas reais.

### Bloco A — Fundação (Pleno obrigatório)

| # | Competência | Onde | Status |
|---|---|---|---|
| 01 | C# 13 (primary ctors, collection expr, records, pattern matching) | Fases 01-08 | ⬜ |
| 02 | ASP.NET Core 10 (Controllers + Minimal APIs) | Fase 01, 03 | ⬜ |
| 03 | EF Core 10 (Code First, Fluent API, Migrations) | Fase 01 | ⬜ |
| 04 | LINQ avançado (deferred execution, projection, GroupBy) | Fase 03 | ⬜ |
| 05 | SQL Server modelagem (índices, FK, concurrency tokens) | Fase 01-02 | ⬜ |
| 06 | Git + Conventional Commits + PR template | Todas | ⬜ |
| 07 | xUnit + FluentAssertions + Moq/NSubstitute | Todas | ⬜ |
| 08 | Docker multi-stage + Compose | Fase 07 | ⬜ |
| 09 | SOLID + Clean Code (revisão por par obrigatória) | Todas | ⬜ |

### Bloco B — Arquitetura (Pleno → Sênior)

| # | Competência | Onde | Status |
|---|---|---|---|
| 10 | Clean Architecture (4 camadas + Dependency Rule) | Fase 01 | ⬜ |
| 11 | Vertical Slice Architecture (alternativa) | Fase 01 deep dive | ⬜ |
| 12 | DDD — Aggregates, Entities, Value Objects | Fase 02 | ⬜ |
| 13 | Domain Events vs Integration Events | Fase 02, 05 | ⬜ |
| 14 | Rich Domain Model (anti-anemic) | Fase 02 | ⬜ |
| 15 | CQRS com MediatR + Pipeline Behaviors | Fase 03 | ⬜ |
| 16 | Result Pattern (sem exceções para fluxo) | Fase 03 | ⬜ |
| 17 | Repository + Unit of Work no aggregate | Fase 02-03 | ⬜ |

### Bloco C — Segurança (Sênior)

| # | Competência | Onde | Status |
|---|---|---|---|
| 18 | JWT Bearer + Refresh Token Rotation | Fase 04 | ⬜ |
| 19 | OAuth2 + OIDC (Authorization Code + PKCE) | **Fase 12** | ⬜ |
| 20 | Duende IdentityServer (clients, scopes, claims) | **Fase 12** | ⬜ |
| 21 | OWASP Top 10 (mass assignment, SSRF, IDOR) | Fase 04 + 12 | ⬜ |
| 22 | Secrets Management (Key Vault, GitHub Secrets) | Fase 08 | ⬜ |
| 23 | Rate Limiting + CORS estrito | Fase 04, 07 | ⬜ |

### Bloco D — Integração (Sênior)

| # | Competência | Onde | Status |
|---|---|---|---|
| 24 | RabbitMQ + MassTransit (producers/consumers) | Fase 05 | ⬜ |
| 25 | Outbox Pattern (transactional messaging) | Fase 05 | ⬜ |
| 26 | Idempotent Consumer + DLQ + Retry | Fase 05 | ⬜ |
| 27 | Saga / Process Manager | Fase 05 | ⬜ |
| 28 | Kafka comparativo + Event Sourcing | **Fase 13** | ⬜ |
| 29 | gRPC + Protobuf entre serviços | **Fase 13** | ⬜ |
| 30 | Change Data Capture (Debezium) | **Fase 13** | ⬜ |
| 31 | Contract Testing (Pact) | **Fase 14** | ⬜ |

### Bloco E — Performance e Resiliência (Sênior)

| # | Competência | Onde | Status |
|---|---|---|---|
| 32 | Polly v8 — pipelines (retry, CB, bulkhead, hedging) | **Fase 09** | ⬜ |
| 33 | Chaos Engineering (Simmy, fault injection) | **Fase 09** | ⬜ |
| 34 | BenchmarkDotNet (memory, throughput) | **Fase 10** | ⬜ |
| 35 | Span\<T\>, Memory\<T\>, ArrayPool | **Fase 10** | ⬜ |
| 36 | ValueTask, IAsyncEnumerable, Channels | **Fase 10** | ⬜ |
| 37 | EF Core compiled queries, split queries, AsNoTracking | Fase 10 | ⬜ |
| 38 | AOT compilation + Source Generators | **Fase 10** | ⬜ |
| 39 | Cache distribuído + invalidação por evento | Fase 06 | ⬜ |

### Bloco F — Observabilidade (Sênior)

| # | Competência | Onde | Status |
|---|---|---|---|
| 40 | OpenTelemetry (traces, metrics, logs com OTLP) | Fase 06 | ⬜ |
| 41 | Serilog enrichers + correlation ID | Fase 06 | ⬜ |
| 42 | Health Checks (liveness/readiness/startup) | Fase 06 | ⬜ |
| 43 | RED + USE methods | **Fase 14** | ⬜ |
| 44 | SLO / SLI / Error Budget | **Fase 14** | ⬜ |
| 45 | Grafana dashboards + Prometheus alertas | Fase 06, 14 | ⬜ |

### Bloco G — Cloud-Native e DevOps (Sênior)

| # | Competência | Onde | Status |
|---|---|---|---|
| 46 | Docker multi-stage + non-root + scan | Fase 07 | ⬜ |
| 47 | Testcontainers em todos os integration tests | Fase 05-07 | ⬜ |
| 48 | GitHub Actions com environments + approvals | Fase 08 | ⬜ |
| 49 | Bicep IaC (parameter files, modules) | Fase 08 | ⬜ |
| 50 | Azure Container Apps (scaling rules, revisions) | Fase 08 | ⬜ |
| 51 | Kubernetes (manifests, HPA, ConfigMap, Secret) | **Fase 11** | ⬜ |
| 52 | Helm charts | **Fase 11** | ⬜ |
| 53 | Service Mesh awareness (Linkerd/Dapr) | **Fase 11** | ⬜ |
| 54 | Feature Flags com Microsoft.FeatureManagement | **Fase 14** | ⬜ |

### Bloco H — Modernização e IA (Sênior+)

| # | Competência | Onde | Status |
|---|---|---|---|
| 55 | Semantic Kernel (function calling, planners) | **Fase 15** | ⬜ |
| 56 | AI Gateway pattern (rate, cost, fallback) | **Fase 15** | ⬜ |
| 57 | RAG (Retrieval Augmented Generation) | **Fase 15** | ⬜ |
| 58 | Vector Database (Azure AI Search / Qdrant) | **Fase 15** | ⬜ |

> **58 competências mapeadas** a requisitos reais de vagas Pleno/Sênior 2026.

---

## 12. Convenções do Projeto

### Git
| Item | Regra |
|---|---|
| Branch | `feat/*`, `fix/*`, `chore/*`, `docs/*`, `refactor/*` |
| Commit | Conventional Commits — `feat(orders): add saga for cancellation` |
| PR | Template obrigatório: contexto, mudanças, screenshots, checklist |

### C#
| Item | Regra |
|---|---|
| Nullable | `<Nullable>enable</Nullable>` em todos os projetos |
| Primary constructors | Default em services e handlers |
| `sealed` | Toda classe que não é base — performance + intent |
| `CancellationToken` | Obrigatório em todo método async (último parâmetro) |
| `ConfigureAwait(false)` | Obrigatório em libs (não em ASP.NET Core handlers) |
| Records | DTOs, Commands, Queries, Events, Value Objects |
| Result\<T\> | Para fluxos esperados; exceções só para casos excepcionais |

### Testes
| Item | Regra |
|---|---|
| Naming | `Method_Scenario_ExpectedResult` |
| AAA | Arrange / Act / Assert separados visualmente |
| Cobertura mínima | 80% no Domain, 70% na Application |
| Integration | Testcontainers (nunca SQLite/in-memory para EF Core) |

---

## 13. Como Rodar Localmente

### Pré-requisitos
- .NET 10 SDK · Docker Desktop · Git · `dotnet ef` global tool

### Setup
```bash
git clone https://github.com/seu-usuario/OrderFlow.git
cd OrderFlow

# Sobe infraestrutura (SQL, Redis, RabbitMQ, Seq, Jaeger, Prometheus, Grafana)
docker compose -f deploy/docker/docker-compose.yml up -d

# Migrations
./scripts/migrate-all.ps1

# Roda tudo
docker compose -f deploy/docker/docker-compose.yml --profile all up -d
```

### URLs
| Serviço | URL |
|---|---|
| Gateway | `http://localhost:8080` |
| Identity (Duende) | `http://localhost:5001/.well-known/openid-configuration` |
| Catalog Swagger | `http://localhost:5002/swagger` |
| Orders Swagger | `http://localhost:5003/swagger` |
| Seq (logs) | `http://localhost:5341` |
| Jaeger (traces) | `http://localhost:16686` |
| Grafana | `http://localhost:3000` (admin/admin) |
| RabbitMQ UI | `http://localhost:15672` (guest/guest) |

---

## 14. Glossário

| Termo | Definição |
|---|---|
| **Aggregate** | Cluster de entidades + VOs com uma raiz garantindo consistência |
| **AOT** | Ahead-of-Time compilation — gera código nativo no build, sem JIT |
| **Bounded Context** | Limite lógico onde um modelo de domínio é coerente |
| **CDC** | Change Data Capture — captura mudanças do DB e publica como eventos |
| **CQRS** | Command Query Responsibility Segregation |
| **Circuit Breaker** | Padrão que abre o circuito após N falhas consecutivas |
| **Error Budget** | Quanto erro é tolerável antes de pausar features (SRE) |
| **Hedging** | Disparar múltiplas requisições em paralelo, primeiro a responder vence |
| **Idempotência** | Processar a mesma mensagem N vezes = 1 vez |
| **OIDC** | OpenID Connect — camada de identidade sobre OAuth2 |
| **Outbox** | Tabela transacional de eventos pendentes |
| **PKCE** | Proof Key for Code Exchange — proteção do Authorization Code Flow |
| **RED Method** | Rate, Errors, Duration — métricas de serviço |
| **SLO/SLI** | Service Level Objective / Indicator |
| **Saga** | Padrão de transação distribuída via eventos compensatórios |
| **Sidecar** | Container auxiliar que roda lado a lado (logs, mesh, secrets) |
| **USE Method** | Utilization, Saturation, Errors — métricas de recurso |
| **Vertical Slice** | Arquitetura por feature em vez de camada |

---

## 15. Perguntas de Entrevista — Sênior

> Selecionadas das 80+ perguntas distribuídas pelas 15 fases. Foco no nível Sênior.

### Arquitetura
**1. "Quando você usaria Vertical Slice em vez de Clean Architecture?"**
— Quando o sistema é majoritariamente CRUD com poucas regras transversais. Vertical Slice elimina o overhead de 4 camadas e organiza por **feature** (cada feature tem seu Endpoint + Handler + Validator + DTO + DataAccess). Acoplamento alto **dentro** da feature, baixo **entre** features. Trade-off: menos reuso, mais duplicação aceita conscientemente.

**2. "Como decidir o tamanho de um microserviço?"**
— Pelo **Bounded Context** (DDD), não pelo tamanho de código. Critérios: (1) **Cohesion** — o serviço resolve um problema de negócio coeso? (2) **Independência de deploy** — posso fazer deploy sem coordenar com outro time? (3) **Database ownership** — ele é dono de seus dados? Anti-padrão: serviço que precisa de JOIN com outro = limites errados.

### Resiliência
**3. "Diferença entre Circuit Breaker e Retry. Quando combinar?"**
— **Retry** tenta de novo pensando que é falha transiente. **Circuit Breaker** bloqueia chamadas após N falhas para dar tempo do serviço se recuperar. **Combinação correta:** Retry **dentro** do Circuit Breaker — o CB conta as falhas (incluindo retries) e abre se passar do limite. Polly v8: `pipelineBuilder.AddRetry(...).AddCircuitBreaker(...)`.

**4. "O que é Bulkhead Pattern e quando aplicar?"**
— Isolamento de recursos para que falha em um endpoint não consuma todas as conexões. Ex: pool de 100 conexões — se endpoint A consome todas, endpoint B trava. Bulkhead: A tem cota de 60, B de 40. **Quando:** quando você tem dependências externas com SLAs diferentes.

### Performance
**5. "Quando usar `ValueTask` em vez de `Task`?"**
— Quando o método pode completar **sincronamente** na maior parte das chamadas (ex: cache hit). `Task` aloca um objeto no heap; `ValueTask` é uma struct — sem alocação no caso síncrono. **Cuidado:** `ValueTask` não pode ser awaited mais de uma vez nem usada com `Task.WhenAll` sem `.AsTask()`.

**6. "O que é `IAsyncEnumerable<T>` e quando substitui `Task<List<T>>`?"**
— Streaming assíncrono de itens. Em vez de carregar 10k registros em memória, processa um a um conforme chegam. **Substitui Task\<List\<T\>\>** quando: (1) o consumidor processa item-a-item, (2) a fonte é grande/streaming (DB cursor, file, network), (3) latência do primeiro byte importa.

### Observabilidade e SRE
**7. "Diferencie SLO, SLI e SLA."**
— **SLI** (Indicator): métrica medida (ex: % requests < 200ms). **SLO** (Objective): meta interna (ex: 99% das requests < 200ms num mês). **SLA** (Agreement): contrato com cliente (ex: 99.9% uptime ou crédito). SLO < SLA sempre — você quer descobrir antes do cliente.

**8. "O que é Error Budget?"**
— `1 - SLO`. Se SLO é 99.9% mensal, error budget = 0.1% = 43min de erro/mês. Enquanto há budget, deploy livre. Budget esgotado, congela features e foca em confiabilidade.

### Identity e Segurança
**9. "Por que Authorization Code Flow + PKCE em SPA, e não Implicit Flow?"**
— Implicit retornava token na URL (vulnerável a histórico, referer, log). PKCE adiciona um **code verifier** dinâmico que o atacante não consegue interceptar mesmo se pegar o code. Padrão atual: **Authorization Code + PKCE** para qualquer client público (SPA, mobile, desktop).

**10. "Como você revogaria acesso de um usuário comprometido em um sistema com JWT?"**
— Você não revoga o JWT (ele expira em ~15min). Você: (1) revoga o **refresh token** no banco, (2) opcionalmente mantém uma **denylist** Redis com TTL = vida do JWT, (3) invalida sessão do IdentityServer. Sem refresh, em 15min o usuário está fora.

### Mensageria e Dados
**11. "Diferença entre RabbitMQ e Kafka. Quando usar cada?"**
— **RabbitMQ:** broker tradicional (smart broker, dumb consumer). Routing complexo, baixa latência, ack por mensagem. Ideal para tarefas assíncronas, comandos, eventos transacionais. **Kafka:** log distribuído (dumb broker, smart consumer). Alto throughput, replay, retenção longa, particionamento por chave. Ideal para event sourcing, analytics, CDC, streaming.

**12. "O que é Change Data Capture e por que substituir Outbox em alta escala?"**
— CDC lê o **transaction log do DB** (sem código de aplicação) e publica como evento. **Vantagem sobre Outbox:** zero impacto na transação de escrita, captura mudanças feitas por SQL direto, captura DELETEs físicos. **Trade-off:** infraestrutura extra (Debezium + Kafka), eventual consistency mais visível.

### Liderança Técnica
**13. "Como você convenceria seu time a adotar OpenTelemetry em vez de só Application Insights?"**
— Argumentos: (1) **Vendor neutrality** — exporta para qualquer backend (Datadog, New Relic, Grafana). (2) **Padrão CNCF** — semantic conventions consistentes entre linguagens. (3) **Custo** — pode ir para self-hosted. (4) **Migração futura** — sem reescrever instrumentação. Trade-off honesto: setup inicial maior; vale para sistemas que escalam ou multi-cloud.

**14. "Como você lidaria com legacy .NET Framework durante migração para .NET 10?"**
— **Strangler Fig Pattern**: novo desenvolvimento em .NET 10, legado coexistindo. (1) Identificar bounded contexts no monolito. (2) Extrair um serviço por vez para .NET 10 com gateway roteando o tráfego. (3) Migrar consumidores aos poucos. (4) Aposentar legado quando todos os endpoints saírem. Comunicação durante migração: REST se síncrono inevitável, eventos via broker quando possível.

---

## Próximo Passo

➡️ Comece em [`fase-01-fundacao-estrutura.md`](./fase-01-fundacao-estrutura.md), ou pule direto para a **trilha Sênior** em [`fase-09-resiliencia-polly.md`](./fase-09-resiliencia-polly.md) se você já domina os fundamentos.

> Para visualização interativa, abra [`orderflow-guide.html`](./orderflow-guide.html) no browser.
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
