# Fase 7 — API Gateway (YARP) e Docker

> **Objetivo:** Implementar o API Gateway com YARP reverse proxy, Docker multi-stage builds para todos os serviços, docker-compose de orquestração completa e testes de integração com Testcontainers.

> **Pré-requisito:** Fase 6 concluída (cache e observabilidade).

### 🎯 O que você vai aprender nesta fase

- Implementar **API Gateway** com YARP como reverse proxy
- Configurar **rotas, clusters e transforms** no YARP
- Criar **Dockerfiles multi-stage** otimizados para cada serviço
- Orquestrar todos os serviços com **Docker Compose**
- Escrever **testes de integração** com Testcontainers
- Entender service discovery e load balancing no gateway

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
src/ApiGateway/
└── OrderFlow.Gateway/
    ├── Program.cs
    ├── appsettings.json               ← YARP routes config
    └── Dockerfile

src/Services/
├── Orders/OrderFlow.Orders.Api/Dockerfile
├── Catalog/OrderFlow.Catalog.Api/Dockerfile
├── Identity/OrderFlow.Identity.Api/Dockerfile
└── Notifications/OrderFlow.Notifications.Worker/Dockerfile

docker-compose.yml                      ← Orquestração completa
docker-compose.override.yml             ← Overrides para dev

tests/
└── OrderFlow.IntegrationTests/
    └── OrderFlowEndToEndTests.cs       ← Testcontainers
```

### Tópicos Cobertos

| Tópico | Detalhe |
|--------|---------|
| **YARP** | Reverse proxy, routing, load balancing, transforms |
| **API Gateway Pattern** | Ponto único de entrada, cross-cutting |
| **Docker Multi-stage** | Build images otimizadas (< 100MB) |
| **Docker Compose** | Orquestração de todos os serviços + infra |
| **Testcontainers** | Testes de integração com containers reais |
| **Container Security** | Non-root user, read-only filesystem |
| **Networking** | Docker networks, service discovery |

---

## 2. Decisões Arquiteturais

> 🤔 **Pense antes de ler:**
> 1. Por que colocar um **reverse proxy** na frente dos serviços em vez de expor cada API diretamente?
> 2. Se o Gateway cai, **tudo cai** — como mitigar esse single point of failure?
> 3. Multi-stage Docker build: por que a imagem final usa `aspnet` (runtime) e não `sdk`? Qual o impacto no tamanho?
>
> O Gateway é a porta de entrada do seu sistema. Docker é a embalagem. Juntos, transformam "funciona no meu PC" em "funciona em qualquer lugar".

### ADR-017: YARP como API Gateway

> 🧠 **Analogia — O Concierge do Hotel:** Imagine um hotel de luxo com vários serviços: restaurante, spa, business center. O hóspede não precisa saber onde fica cada um, nem como chegar — ele fala com o **concierge** na recepção, que encaminha cada pedido para o lugar certo. O concierge também faz triagem: verifica se o hóspede é VIP (autenticação), limita quantos pedidos por hora (rate limiting) e pode até traduzir o pedido se necessário (transforms). **YARP é o concierge**: um ponto único de entrada que roteia, protege e observa todo o tráfego.

**Contexto:** Precisamos de um ponto único de entrada para os clientes, com routing para os microserviços.

**Decisão:** YARP (Yet Another Reverse Proxy) da Microsoft, rodando como um ASP.NET Core app.

```
Client ──▶ YARP Gateway (:8080)
               │
               ├── /api/auth/*     ──▶ Identity API (:5001)
               ├── /api/products/* ──▶ Catalog API  (:5002)
               ├── /api/orders/*   ──▶ Orders API   (:5000)
               └── /health         ──▶ Gateway health
```

**Por que YARP e não Ocelot?**
- YARP é mantido pela Microsoft (vs comunidade)
- Performance superior (built on KESTREL)
- Configuração simplificada
- Suporte a gRPC e WebSockets
- HTTP/2 e HTTP/3

### ADR-018: Docker Multi-stage Builds

> 🧠 **Analogia — Fazer a Mala de Viagem:** Quando você cozinha em casa, precisa da cozinha inteira: fogão, panelas, ingredientes, especiarias (SDK ~700MB). Mas quando vai viajar, só leva a marmita pronta (runtime ~85MB). O multi-stage build é isso: o primeiro estágio (SDK) *cozinha* a aplicação; o segundo estágio (runtime) *empacota só o prato pronto*. Resultado: imagem de produção 6x menor, sem compilador, sem código-fonte, sem ferramentas de build — menos superfície de ataque, deploy mais rápido.

**Decisão:** Usar multi-stage builds para imagens mínimas em produção.

```dockerfile
# Stage 1: build (SDK completo — ~700MB)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
...

# Stage 2: runtime (apenas runtime — ~85MB)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
...

# Resultado: imagem final ~120MB (vs ~700MB sem multi-stage)
```

> **Dica — Pinning de Imagens:** As tags `sdk:10.0` e `aspnet:10.0` apontam para o patch mais recente e recebem atualizações de segurança automaticamente — ideal para desenvolvimento e CI. Em produção, considere fixar a versão completa (ex: `aspnet:10.0.1-jammy`) para **builds reproduzíveis**. Documente a política de atualização da equipe.

### ADR-019: API Versioning via Gateway

**Decisão:** Não implementar versionamento de API nesta fase.

**Justificativa:** O OrderFlow ainda está em v1. Quando precisar versionar:
- **Opção A (URL path):** YARP routes com `/api/v1/` e `/api/v2/` apontando para clusters diferentes
- **Opção B (Header):** `Asp.Versioning.Http` nos services individuais + YARP passando o header `api-version`
- **Recomendação:** URL path é mais simples para clientes e mais visível no gateway

> **Dica — Segurança Docker adicional:** Em produção, considere `read_only: true` no Compose + `tmpfs` para diretórios de escrita temporária. O non-root user + read-only filesystem reduz significativamente a superfície de ataque.

---

## 3. Conceitos

### YARP — Como Funciona

> 💡 **Visão geral:** YARP funciona com dois conceitos centrais: **Routes** ("quando a URL bater com esse padrão, encaminhe para...") e **Clusters** ("...este grupo de servidores"). Um Route casa com o request, um Cluster define para onde mandar. Transforms permitem modificar headers, paths e query strings no caminho. Pense em Routes como as placas de sinalização e Clusters como os destinos.

```
┌──────────────────────────────────────────────────┐
│                YARP Gateway                      │
│                                                  │
│  Routes (match request)                          │
│  ┌─────────────────────────────────────────┐    │
│  │ Route: identity-route                   │    │
│  │ Match: Path /api/auth/{**catch-all}     │    │
│  │ Cluster: identity-cluster               │    │
│  └─────────────────────────────────────────┘    │
│                                                  │
│  Clusters (destinations)                         │
│  ┌─────────────────────────────────────────┐    │
│  │ Cluster: identity-cluster               │    │
│  │ Destinations:                           │    │
│  │   - http://identity-api:8080            │    │
│  └─────────────────────────────────────────┘    │
│                                                  │
│  Pipeline: Auth → RateLimit → Transform → Proxy │
└──────────────────────────────────────────────────┘
```

### Docker Networking

> 🧠 **Analogia — O Condomínio Fechado:** Docker networks são como um condomínio: todos os moradores (containers) dentro do condomínio (network) se enxergam pelo nome (DNS: `orders-api`, `redis`, `sqlserver`). Quem está fora do muro não consegue entrar diretamente — só pela portaria (portas expostas). Containers em networks diferentes não se vêem, mesmo estando na mesma máquina física. Isso é **isolamento de rede**.

```
┌─────────────── Docker Network: orderflow-net ──────────────────┐
│                                                                  │
│  ┌──────────┐ ┌────────────┐ ┌───────────┐ ┌───────────────┐  │
│  │ gateway  │ │ orders-api │ │ catalog   │ │ identity-api  │  │
│  │  :8080   │ │  :8080     │ │  :8080    │ │  :8080        │  │
│  └────┬─────┘ └────────────┘ └───────────┘ └───────────────┘  │
│       │                                                          │
│  ┌────┴─────┐ ┌────────────┐ ┌───────┐ ┌─────┐ ┌──────────┐  │
│  │ worker   │ │ sqlserver  │ │ redis │ │ seq │ │ rabbitmq │  │
│  └──────────┘ │  :1433     │ │ :6379 │ │:5341│ │ :5672    │  │
│                └────────────┘ └───────┘ └─────┘ └──────────┘  │
│                                                                  │
│  Host expõe: gateway:8080, seq:5341, rabbitmq:15672             │
└──────────────────────────────────────────────────────────────────┘
```

---

## 4. Passo a Passo de Implementação

### 4.1 Criar Projeto do Gateway

```bash
dotnet new web -n OrderFlow.Gateway -o src/ApiGateway/OrderFlow.Gateway
dotnet sln add src/ApiGateway/OrderFlow.Gateway
dotnet add src/ApiGateway/OrderFlow.Gateway package Yarp.ReverseProxy
dotnet add src/ApiGateway/OrderFlow.Gateway package Serilog.AspNetCore
dotnet add src/ApiGateway/OrderFlow.Gateway package Serilog.Sinks.Seq
dotnet add src/ApiGateway/OrderFlow.Gateway package Microsoft.AspNetCore.Authentication.JwtBearer
```

---

## 5. Código de Referência Completo

### 5.1 YARP Gateway — Program.cs

**`src/ApiGateway/OrderFlow.Gateway/Program.cs`**

```csharp
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.SharedKernel.Auth;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .Enrich.WithProperty("Application", "OrderFlow.Gateway"));

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT (o gateway valida tokens para rotas protegidas)
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Rate Limiting global
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("gateway-global", opt =>
    {
        opt.PermitLimit = 200;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.MapHealthChecks("/health");

app.Run();
```

### 5.2 YARP — appsettings.json

**`src/ApiGateway/OrderFlow.Gateway/appsettings.json`**

```json
{
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": {
          "Path": "/api/auth/{**catch-all}"
        }
      },
      "catalog-route": {
        "ClusterId": "catalog-cluster",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        },
        "AuthorizationPolicy": "default"
      },
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": {
          "Path": "/api/orders/{**catch-all}"
        },
        "AuthorizationPolicy": "default"
      },
      "catalog-categories-route": {
        "ClusterId": "catalog-cluster",
        "Match": {
          "Path": "/api/categories/{**catch-all}"
        },
        "AuthorizationPolicy": "default"
      }
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": {
          "identity-api": {
            "Address": "http://localhost:5001/"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Path": "/health/live"
          }
        }
      },
      "catalog-cluster": {
        "Destinations": {
          "catalog-api": {
            "Address": "http://localhost:5002/"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Path": "/health/live"
          }
        }
      },
      "orders-cluster": {
        "Destinations": {
          "orders-api": {
            "Address": "http://localhost:5000/"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Path": "/health/live"
          }
        }
      }
    }
  },
  "JwtSettings": {
    "Secret": "SuperSecretKeyWithAtLeast32CharactersForHmacSha256!!",
    "Issuer": "OrderFlow.Identity",
    "Audience": "OrderFlow",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Yarp": "Information"
      }
    }
  }
}
```

> **Localhost vs Containers:** Os endereços `http://localhost:5001/` acima funcionam para desenvolvimento local (`dotnet run`). Quando rodando no Docker, o `docker-compose.yml` sobrescreve esses endereços usando variáveis de ambiente como `ReverseProxy__Clusters__identity-cluster__Destinations__identity-api__Address: http://identity-api:8080`. O Docker Compose resolve automaticamente nomes de containers (`identity-api`, `orders-api`) via DNS interno da rede `orderflow-net`.

### 5.3 Dockerfiles — Multi-stage

**`src/Services/Orders/OrderFlow.Orders.Api/Dockerfile`**

```dockerfile
# ========== BUILD STAGE ==========
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar arquivos de projeto e restaurar (layer caching!)
COPY ["src/Services/Orders/OrderFlow.Orders.Api/OrderFlow.Orders.Api.csproj", "Services/Orders/OrderFlow.Orders.Api/"]
COPY ["src/Services/Orders/OrderFlow.Orders.Application/OrderFlow.Orders.Application.csproj", "Services/Orders/OrderFlow.Orders.Application/"]
COPY ["src/Services/Orders/OrderFlow.Orders.Domain/OrderFlow.Orders.Domain.csproj", "Services/Orders/OrderFlow.Orders.Domain/"]
COPY ["src/Services/Orders/OrderFlow.Orders.Infrastructure/OrderFlow.Orders.Infrastructure.csproj", "Services/Orders/OrderFlow.Orders.Infrastructure/"]
COPY ["src/BuildingBlocks/OrderFlow.SharedKernel/OrderFlow.SharedKernel.csproj", "BuildingBlocks/OrderFlow.SharedKernel/"]
COPY ["src/BuildingBlocks/OrderFlow.Contracts/OrderFlow.Contracts.csproj", "BuildingBlocks/OrderFlow.Contracts/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

RUN dotnet restore "Services/Orders/OrderFlow.Orders.Api/OrderFlow.Orders.Api.csproj"

# Copiar todo o código e buildar
COPY src/ .
WORKDIR /src/Services/Orders/OrderFlow.Orders.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ========== RUNTIME STAGE ==========
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# OCI Labels (metadados da imagem)
LABEL org.opencontainers.image.source="https://github.com/seu-usuario/OrderFlow"
LABEL org.opencontainers.image.description="OrderFlow Orders API"

# Security: Non-root user + curl para HEALTHCHECK
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderFlow.Orders.Api.dll"]
```

**`src/Services/Identity/OrderFlow.Identity.Api/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/Identity/OrderFlow.Identity.Api/OrderFlow.Identity.Api.csproj", "Services/Identity/OrderFlow.Identity.Api/"]
COPY ["src/BuildingBlocks/OrderFlow.SharedKernel/OrderFlow.SharedKernel.csproj", "BuildingBlocks/OrderFlow.SharedKernel/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

RUN dotnet restore "Services/Identity/OrderFlow.Identity.Api/OrderFlow.Identity.Api.csproj"

COPY src/ .
WORKDIR /src/Services/Identity/OrderFlow.Identity.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appuser && useradd -r -g appuser appuser
USER appuser
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderFlow.Identity.Api.dll"]
```

**`src/Services/Catalog/OrderFlow.Catalog.Api/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/Catalog/OrderFlow.Catalog.Api/OrderFlow.Catalog.Api.csproj", "Services/Catalog/OrderFlow.Catalog.Api/"]
COPY ["src/Services/Catalog/OrderFlow.Catalog.Application/OrderFlow.Catalog.Application.csproj", "Services/Catalog/OrderFlow.Catalog.Application/"]
COPY ["src/Services/Catalog/OrderFlow.Catalog.Domain/OrderFlow.Catalog.Domain.csproj", "Services/Catalog/OrderFlow.Catalog.Domain/"]
COPY ["src/Services/Catalog/OrderFlow.Catalog.Infrastructure/OrderFlow.Catalog.Infrastructure.csproj", "Services/Catalog/OrderFlow.Catalog.Infrastructure/"]
COPY ["src/BuildingBlocks/OrderFlow.SharedKernel/OrderFlow.SharedKernel.csproj", "BuildingBlocks/OrderFlow.SharedKernel/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

RUN dotnet restore "Services/Catalog/OrderFlow.Catalog.Api/OrderFlow.Catalog.Api.csproj"

COPY src/ .
WORKDIR /src/Services/Catalog/OrderFlow.Catalog.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
LABEL org.opencontainers.image.source="https://github.com/seu-usuario/OrderFlow"
LABEL org.opencontainers.image.description="OrderFlow Catalog API"
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appuser && useradd -r -g appuser appuser
USER appuser
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderFlow.Catalog.Api.dll"]
```

**`src/Services/Notifications/OrderFlow.Notifications.Worker/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/Notifications/OrderFlow.Notifications.Worker/OrderFlow.Notifications.Worker.csproj", "Services/Notifications/OrderFlow.Notifications.Worker/"]
COPY ["src/BuildingBlocks/OrderFlow.Contracts/OrderFlow.Contracts.csproj", "BuildingBlocks/OrderFlow.Contracts/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

RUN dotnet restore "Services/Notifications/OrderFlow.Notifications.Worker/OrderFlow.Notifications.Worker.csproj"

COPY src/ .
WORKDIR /src/Services/Notifications/OrderFlow.Notifications.Worker
RUN dotnet publish -c Release -o /app/publish --no-restore

# Worker não precisa do ASP.NET Core runtime — basta o runtime base (menor)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Notifications.Worker.dll"]
```

**`src/ApiGateway/OrderFlow.Gateway/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/ApiGateway/OrderFlow.Gateway/OrderFlow.Gateway.csproj", "ApiGateway/OrderFlow.Gateway/"]
COPY ["src/BuildingBlocks/OrderFlow.SharedKernel/OrderFlow.SharedKernel.csproj", "BuildingBlocks/OrderFlow.SharedKernel/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

RUN dotnet restore "ApiGateway/OrderFlow.Gateway/OrderFlow.Gateway.csproj"

COPY src/ .
WORKDIR /src/ApiGateway/OrderFlow.Gateway
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appuser && useradd -r -g appuser appuser
USER appuser
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderFlow.Gateway.dll"]
```

### 5.4 Docker Compose Completo

**`.env` (template — copie para `.env` e ajuste os valores):**

```env
# Credenciais de infraestrutura (NÃO commite este arquivo!)
MSSQL_SA_PASSWORD=YourStr0ng!Pass
RABBITMQ_USER=orderflow
RABBITMQ_PASS=orderflow123
ASPNETCORE_ENVIRONMENT=Docker
JWT_SECRET=SuperSecretKeyWithAtLeast32CharactersForHmacSha256!!
```

> ⚠️ Adicione `.env` ao `.gitignore`. As senhas abaixo usam variáveis do `.env` para evitar hardcoding no Compose.

**`docker-compose.yml`**

```yaml
services:
  # ===== INFRASTRUCTURE =====
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: orderflow-sqlserver
    restart: unless-stopped
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD}"
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -Q "SELECT 1" -b -o /dev/null
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 30s
    networks:
      - orderflow-net

  redis:
    image: redis:7-alpine
    container_name: orderflow-redis
    restart: unless-stopped
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
    networks:
      - orderflow-net

  rabbitmq:
    image: rabbitmq:3-management-alpine
    container_name: orderflow-rabbitmq
    restart: unless-stopped
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASS}
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
      interval: 10s
      timeout: 5s
      retries: 10
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    networks:
      - orderflow-net

  seq:
    image: datalust/seq:2024.4
    container_name: orderflow-seq
    restart: unless-stopped
    ports:
      - "5341:5341"
      - "5342:80"
    environment:
      ACCEPT_EULA: "Y"
    volumes:
      - seq_data:/data
    networks:
      - orderflow-net

  # ===== APPLICATION SERVICES =====
  gateway:
    build:
      context: .
      dockerfile: src/ApiGateway/OrderFlow.Gateway/Dockerfile
    container_name: orderflow-gateway
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ASPNETCORE_URLS: http://+:8080
      # Override YARP clusters para nomes de containers
      ReverseProxy__Clusters__identity-cluster__Destinations__identity-api__Address: http://identity-api:8080
      ReverseProxy__Clusters__catalog-cluster__Destinations__catalog-api__Address: http://catalog-api:8080
      ReverseProxy__Clusters__orders-cluster__Destinations__orders-api__Address: http://orders-api:8080
    depends_on:
      identity-api:
        condition: service_healthy
      catalog-api:
        condition: service_healthy
      orders-api:
        condition: service_healthy
    networks:
      - orderflow-net

  identity-api:
    build:
      context: .
      dockerfile: src/Services/Identity/OrderFlow.Identity.Api/Dockerfile
    container_name: orderflow-identity-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__IdentityDb: "Server=sqlserver;Database=OrderFlow_Identity;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True"
      Serilog__WriteTo__1__Args__serverUrl: http://seq:5341
    depends_on:
      sqlserver:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 30s
    networks:
      - orderflow-net

  catalog-api:
    build:
      context: .
      dockerfile: src/Services/Catalog/OrderFlow.Catalog.Api/Dockerfile
    container_name: orderflow-catalog-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__CatalogDb: "Server=sqlserver;Database=OrderFlow_Catalog;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True"
      ConnectionStrings__Redis: redis:6379
      Serilog__WriteTo__1__Args__serverUrl: http://seq:5341
    depends_on:
      sqlserver:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 30s
    networks:
      - orderflow-net

  orders-api:
    build:
      context: .
      dockerfile: src/Services/Orders/OrderFlow.Orders.Api/Dockerfile
    container_name: orderflow-orders-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__OrdersDb: "Server=sqlserver;Database=OrderFlow_Orders;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True"
      ConnectionStrings__Redis: redis:6379
      RabbitMQ__Host: rabbitmq
      RabbitMQ__Username: ${RABBITMQ_USER}
      RabbitMQ__Password: ${RABBITMQ_PASS}
      Serilog__WriteTo__1__Args__serverUrl: http://seq:5341
    depends_on:
      sqlserver:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 30s
    networks:
      - orderflow-net

  notification-worker:
    build:
      context: .
      dockerfile: src/Services/Notifications/OrderFlow.Notifications.Worker/Dockerfile
    container_name: orderflow-notification-worker
    restart: unless-stopped
    environment:
      DOTNET_ENVIRONMENT: Docker
      RabbitMQ__Host: rabbitmq
      RabbitMQ__Username: ${RABBITMQ_USER}
      RabbitMQ__Password: ${RABBITMQ_PASS}
      Serilog__WriteTo__1__Args__serverUrl: http://seq:5341
    depends_on:
      rabbitmq:
        condition: service_healthy
    networks:
      - orderflow-net

volumes:
  sqlserver_data:
  redis_data:
  rabbitmq_data:
  seq_data:

networks:
  orderflow-net:
    driver: bridge
```

### 5.5 .dockerignore

**`.dockerignore`**

```
**/.git
**/.vs
**/.vscode
**/bin
**/obj
**/node_modules
**/.env
**/Dockerfile*
**/docker-compose*
**/*.md
**/tests
**/.dockerignore
```

---

## 6. Testes

### 6.1 Testcontainers — Testes de Integração End-to-End

```bash
dotnet new xunit -n OrderFlow.IntegrationTests -o tests/OrderFlow.IntegrationTests
dotnet sln add tests/OrderFlow.IntegrationTests
dotnet add tests/OrderFlow.IntegrationTests package Testcontainers
dotnet add tests/OrderFlow.IntegrationTests package Testcontainers.MsSql
dotnet add tests/OrderFlow.IntegrationTests package Testcontainers.Redis
dotnet add tests/OrderFlow.IntegrationTests package Testcontainers.RabbitMq
dotnet add tests/OrderFlow.IntegrationTests package FluentAssertions
dotnet add tests/OrderFlow.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/OrderFlow.IntegrationTests reference src/Services/Orders/OrderFlow.Orders.Api
```

**`tests/OrderFlow.IntegrationTests/OrdersApiContainerTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Orders.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace OrderFlow.IntegrationTests;

public class OrdersApiContainerTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private HttpClient _client = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _sqlContainer.StartAsync(),
            _redisContainer.StartAsync());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext para usar Testcontainer
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<OrdersDbContext>));
                    if (descriptor is not null) services.Remove(descriptor);

                    services.AddDbContext<OrdersDbContext>(options =>
                        options.UseSqlServer(_sqlContainer.GetConnectionString()));
                });
            });

        _client = _factory.CreateClient();

        // Ensure database created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await Task.WhenAll(
            _sqlContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task CreateOrder_WithRealDatabase_ReturnsCreated()
    {
        var request = new
        {
            customerId = Guid.NewGuid(),
            street = "Rua Teste",
            number = "100",
            neighborhood = "Centro",
            city = "São Paulo",
            state = "SP",
            zipCode = "01001000"
        };

        var response = await _client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetOrder_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAndRetrieveOrder_FullFlow_Works()
    {
        // Create
        var createRequest = new
        {
            customerId = Guid.NewGuid(),
            street = "Av Brasil",
            number = "500",
            neighborhood = "Centro",
            city = "Rio de Janeiro",
            state = "RJ",
            zipCode = "20040020"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        var orderId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Retrieve
        var getResponse = await _client.GetAsync($"/api/orders/{orderId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## ⚠️ Erros Comuns com Gateway e Docker

| # | Erro | Consequência | Solução |
|---|---|---|---|
| 1 | **YARP routing sem health check** | Gateway roteia para serviço que está down → 502 Bad Gateway | Configure `HealthCheck` no cluster YARP: `"HealthCheck": { "Active": { "Enabled": true } }` |
| 2 | **Dockerfile com `sdk` na imagem final** | Imagem de ~800MB em vez de ~200MB, inclui compilador desnecessário | Multi-stage build: `FROM sdk AS build` → `FROM aspnet AS final`. Imagem final só tem runtime |
| 3 | **Docker Compose sem `depends_on` com health check** | API sobe antes do SQL Server estar pronto → connection refused | Use `depends_on: sqlserver: condition: service_healthy` |
| 4 | **Porta hardcoded no serviço** | Conflito de portas quando dois serviços usam a mesma | Use `ASPNETCORE_URLS=http://+:80` no container. Mapeie portas diferentes no compose: `"5001:80"`, `"5002:80"` |
| 5 | **Gateway com JWT validation mas serviços sem** | Bypass: acesso direto ao serviço sem autenticação | Defense in depth: **ambos** validam JWT. Gateway é a primeira barreira, serviço é a segunda |
| 6 | **COPY . . antes de restore no Dockerfile** | Qualquer mudança de código invalida cache do `dotnet restore` (download de todos os packages) | `COPY *.csproj .` → `RUN dotnet restore` → `COPY . .` → `RUN dotnet publish` |

---

## 🔧 Troubleshooting — Fase 07

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| "Connection refused" no docker compose | Serviço referencia `localhost` em vez do nome do container | Use nomes de serviço do compose: `http://orders-api:80`, não `http://localhost:5003` |
| YARP retorna 404 para rota existente | `Match.Path` no YARP config não bate com a URL | YARP usa `{**catch-all}`. Verifique: `"/api/orders/{**remainder}"` |
| Build Docker falha "project not found" | Context path errado ou `.dockerignore` excluindo arquivos necessários | Verifique que o `docker build` roda na raiz da solution, não na pasta do projeto |
| Health check do compose trava | Endpoint `/health` retorna 503 | Verifique se todos os serviços dependentes estão rodando (SQL Server, Redis, RabbitMQ) |
| Container reinicia em loop | Crash na inicialização (migration falha, config ausente) | `docker logs <container>` para ver o erro. Comum: connection string faltando em env var |

---

## 🔗 Conectando os Pontos

| Artefato | Origem | Transformação nesta fase |
|---------|--------|------------------------|
| Todos os serviços (Orders, Catalog, Identity) | Fases 01-06 | Containerizados com multi-stage Dockerfiles |
| JWT validation | Fase 04 | Centralizada no Gateway + mantida nos serviços (defense in depth) |
| Health checks | Fases 01, 06 | Aggregados pelo Gateway para um único `/health` endpoint |
| OpenTelemetry | Fase 06 | Gateway adiciona trace headers automaticamente via YARP |

> **Preview Fase 08:** O `docker compose` que construímos aqui será o que o GitHub Actions usa para rodar testes de integração no CI. O Dockerfile multi-stage será o que o pipeline builda e pusha para o Azure Container Registry.

---

## 7. Checkpoint

> 💡 **Por que isso importa no dia-a-dia?** O Gateway + Docker é onde seu projeto sai do "funciona na minha máquina" para **"funciona em qualquer máquina"**. Na entrevista sênior, mostrar um `docker compose up` que sobe 5 serviços + infra em 30 segundos demonstra que você pensa em **operação**, não só em código. E o Gateway mostra que você entende **arquitetura distribuída**: ponto único de entrada, cross-cutting concerns centralizados, zero configuração nos clientes.

### Validação Completa

- [ ] **YARP Gateway funcional:** Routing para todos os serviços
- [ ] **JWT no Gateway:** Rotas protegidas validam token
- [ ] **Rate limiting no Gateway:** 200 req/min global
- [ ] **Dockerfiles criados:** Todos os serviços (4 APIs + 1 worker)
- [ ] **Multi-stage builds:** Imagens < 150MB
- [ ] **docker-compose.yml completo:** All services + infrastructure
- [ ] **Non-root containers:** Security best practice
- [ ] **Health checks no Docker:** Cada container com health check
- [ ] **Service discovery via DNS:** Containers se comunicam por nome
- [ ] **Testcontainers:** Testes com SQL Server e Redis reais
- [ ] **.dockerignore:** Evita copiar arquivos desnecessários
- [ ] **Commit:** `feat(infra): add YARP gateway, Dockerfiles and docker-compose orchestration`

### Comandos de Verificação

```bash
# Build de todas as imagens
docker compose build

# Subir tudo
docker compose up -d

# Verificar status
docker compose ps

# Logs do gateway
docker compose logs -f gateway

# Testar via gateway
curl http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@orderflow.com","password":"Admin@1234"}'

# Health check do gateway
curl http://localhost:8080/health

# Tamanho das imagens
docker images | grep orderflow

# Testes com Testcontainers (precisa Docker rodando)
dotnet test tests/OrderFlow.IntegrationTests --verbosity normal

# Derrubar tudo
docker compose down -v
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| Gateway Project | `OrderFlow.Gateway` (ASP.NET Core + YARP) |
| YARP Config | `yarp.json` ou `appsettings.json` (routes, clusters, transforms) |
| Dockerfiles | Multi-stage para cada serviço (build → publish → runtime) |
| Docker Compose | `docker-compose.yml` (todos os serviços + infra) |
| .dockerignore | Exclusões para build context otimizado |
| Health Aggregator | Gateway agrega health de todos os backends |
| Integration Tests | `OrderFlow.IntegrationTests` com Testcontainers |
| WebApplicationFactory | Custom factory para testes com containers reais |
| Testes | `GatewayRoutingTests.cs`, `OrdersApiIntegrationTests.cs` |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 7

**1. "Por que usar API Gateway e não expor cada serviço diretamente?"**
— O Gateway é o **único ponto de entrada** para clients externos. Benefícios: roteamento centralizado, autenticação única, rate limiting por client, agregação de health checks, CORS único. Sem gateway, o client precisa conhecer N URLs, cada serviço implementa CORS/auth separadamente, e mudanças de infra afetam clients. O gateway **isola** a topologia interna.

**2. "YARP vs Ocelot — por que YARP?"**
— YARP é mantido pela **equipe do ASP.NET** na Microsoft — usa o pipeline nativo de middleware (Kestrel, HttpClient factory, DI). Ocelot é open-source, maduro, mas reimplementa conceitos que o ASP.NET já tem. YARP é mais performante (menos alocações), configé via IConfiguration (hot reload), e suporta transforms (headers, path) nativamente.

**3. "Qual a vantagem de Docker multi-stage build?"**
— **Stage 1** (`sdk:10.0`): compila e publica — imagem grande com todo o SDK. **Stage 2** (`aspnet:10.0`): copia apenas o binário publicado — imagem final pequena (~80MB vs ~700MB). Resultado: imagens menores = pull mais rápido = deploy mais rápido = menos superfície de ataque. Além disso, o SDK não vai para produção.

$$\text{Redução} = 1 - \frac{\text{Imagem runtime (\~80MB)}}{\text{Imagem SDK (\~700MB)}} \approx 88\%$$

**4. "O que são Testcontainers e quando usar?"**
— Testcontainers sobe containers Docker **reais** durante testes. Em vez de mockar Redis, RabbitMQ e SQL Server, o teste usa instâncias reais descartáveis. Resultado: testes que **realmente** testam integração, não mocks. Trade-off: mais lento que unit tests e requer Docker rodando. Use para **integration tests** críticos, não para unit tests de domínio.

**5. "Como funciona service discovery em Docker Compose?"**
— No Docker Compose, cada serviço se registra com o **nome do service** como hostname. `http://orders-api:8080` resolve para o container do Orders API via DNS interno do Docker. Não precisa de Consul, Eureka ou service mesh. Para Kubernetes, o equivalente é `http://orders-api.namespace.svc.cluster.local`. YARP usa esses hostnames como cluster destinations.

---

## 🔬 Aprofundamento Sênior

### A1. YARP Avançado — Transforms e Custom Routing

```json
{
  "ReverseProxy": {
    "Routes": {
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": { "Path": "/api/orders/{**catch-all}" },
        "Transforms": [
          { "PathPattern": "/{**catch-all}" },
          { "RequestHeader": "X-Forwarded-User", "Set": "{user}" },
          { "ResponseHeaderRemove": "Server" }
        ]
      }
    },
    "Clusters": {
      "orders-cluster": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }
        },
        "Destinations": {
          "orders-1": { "Address": "http://orders-api:8080" }
        }
      }
    }
  }
}
```

**LoadBalancing:** `PowerOfTwoChoices` (escolhe 2 random, pega o menos carregado) é hoje o melhor algoritmo geral — mais distribuído que Round Robin, menos sobrecarga que Least Requests global.

### A2. gRPC Routing no YARP

YARP roteia gRPC nativamente — **HTTP/2 obrigatório**. Cluster com `HttpRequest.Version: 2`. Útil quando Gateway é também o limite de exposição interna.

### A3. Docker — Hardening de Imagens

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

# 1. Não-root
RUN addgroup -S app && adduser -S app -G app
USER app
WORKDIR /app

# 2. Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
  CMD wget --quiet --tries=1 --spider http://localhost:8080/health/live || exit 1

# 3. Read-only root filesystem (compose/k8s)
# securityContext: { readOnlyRootFilesystem: true }

# 4. Drop capabilities
# securityContext: { capabilities: { drop: ["ALL"] } }

# 5. Sem shell em imagem final (alpine ou distroless)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
# chiseled = sem shell, sem package manager — superfície de ataque mínima
```

### A4. Image Scanning no CI

```yaml
- name: Scan image with Trivy
  uses: aquasecurity/trivy-action@master
  with:
    image-ref: ghcr.io/orderflow/orders:${{ github.sha }}
    severity: 'CRITICAL,HIGH'
    exit-code: 1
```

Falha o build se houver CVE crítico. Combine com **Dependabot** para PRs automáticos de dep updates.

### A5. Testcontainers Patterns

#### Compartilhar container entre testes (CollectionFixture)
```csharp
[CollectionDefinition("sql")]
public class SqlCollection : ICollectionFixture<SqlContainerFixture> { }

[Collection("sql")]
public class OrderRepoTests(SqlContainerFixture sql) { ... }
```

#### Snapshot do banco entre testes
```csharp
// Antes do teste: BACKUP
await conn.ExecuteAsync("BACKUP DATABASE OrdersDb TO DISK = '/tmp/snap.bak'");
// Depois: RESTORE — muito mais rápido que recriar
```

### A6. Distroless / Chiseled Images

Microsoft lançou imagens **chiseled** (.NET 8+): sem shell, sem package manager, ~30MB. Reduz superfície de ataque drasticamente. Default para produção em 2026.

### 💼 Perguntas Sênior

**"Qual algoritmo de load balancing escolher?"** — `PowerOfTwoChoices` é o sweet spot moderno: escolhe 2 destinos random, pega o menos carregado. Distribuição quase ótima sem o overhead de Least Connections global. Padrão em Envoy, YARP, NGINX moderno.

**"Por que chiseled images?"** — Sem shell, sem package manager, sem usuário root: superfície de ataque ~zero. Atacante que ganhou execução não consegue executar `apt install` nem `bash`. Trade-off: debug em produção exige sidecar de debug. Padrão para 2026.

---

> **Próximo passo:** Avance para `fase-08-cicd-cloud.md`.
>
> 🚀 **Trilha Sênior:** [`fase-11-kubernetes-service-mesh.md`](./fase-11-kubernetes-service-mesh.md) — Kubernetes substitui Compose em produção.
