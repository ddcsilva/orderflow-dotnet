# Fase 8 — CI/CD com GitHub Actions e Deploy na Azure

> **Objetivo:** Implementar pipeline de CI/CD automatizado com GitHub Actions, deploy na Azure (Container Apps), configuração de ambientes, secrets, e documentação final do projeto.

> **Pré-requisito:** Fase 7 concluída (Gateway + Docker).

### 🎯 O que você vai aprender nesta fase

- Criar **pipelines CI/CD** com GitHub Actions (build, test, publish)
- Configurar **Azure Container Apps** como plataforma de deploy
- Definir infraestrutura como código com **Bicep**
- Gerenciar **secrets** e variáveis de ambiente por ambiente
- Implementar **deploy automatizado** com estratégia de revisão
- Configurar **ambientes** (staging, production) com aprovação manual

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
.github/
└── workflows/
    ├── ci.yml                 ← Build + Test em todo PR
    ├── cd-staging.yml         ← Deploy automático para staging
    └── cd-production.yml      ← Deploy para prod (manual approval)

infra/
├── azure/
│   ├── main.bicep             ← Infraestrutura como código
│   └── parameters.json        ← Parâmetros por ambiente
└── scripts/
    └── seed-database.sh       ← Script de seed inicial

docs/
├── README.md                  ← Documentação do projeto (final)
└── CONTRIBUTING.md            ← Guia de contribuição
```

### Tópicos Cobertos

| Tópico | Detalhe |
|--------|---------|
| **GitHub Actions** | CI pipeline (build, test, lint) |
| **Container Registry** | GitHub Container Registry (ghcr.io) |
| **Continuous Deployment** | Deploy automatizado para staging |
| **Azure Container Apps** | Serverless containers na Azure |
| **Azure Bicep** | Infrastructure as Code |
| **Secrets Management** | GitHub Secrets + Azure Key Vault |
| **Environment Protection** | Approval gates para produção |
| **OpenAPI/Swagger** | Documentação de APIs |

---

## 2. Decisões Arquiteturais

### ADR-019: GitHub Actions para CI/CD

> 🧠 **Analogia — A Linha de Montagem da Fábrica:** Antigamente, cada carro era montado à mão — lento, inconsistente, sujeito a erros humanos. Henry Ford criou a **linha de montagem**: cada estação faz uma tarefa específica (soldar, pintar, testar) e o carro avança automaticamente. Se alguma estação detecta defeito, a linha **para**. CI/CD é a linha de montagem do software: build → test → analyze → publish → deploy. Cada push dispara a esteira. Se um teste falha, o deploy não acontece. **Nenhum humano toca no botão de deploy — a pipeline faz tudo.**

**Contexto:** Precisamos de um pipeline automatizado que garanta qualidade e faça deploy.

**Decisão:** GitHub Actions.

**Motivos:**
- Integração nativa com GitHub (repositório + PRs)
- Runners gratuitos para projetos open source
- Marketplace com actions prontas
- Matrix builds para múltiplas versões
- Environments com approval gates

### ADR-020: Azure Container Apps para Deploy

> 🧠 **Analogia — O Food Truck vs Restaurante Fixo:** AKS (Kubernetes) é abrir um **restaurante completo**: você controla tudo (cozinha, salão, estoque, funcionários), mas gerencia tudo também. Azure Container Apps é como ter um **food truck numa praça de alimentação gerenciada**: a praça cuida da limpeza, segurança, estacionamento; você só cuida do cardapio (seus containers). Quando não tem cliente, você fecha o truck (scale-to-zero = custo zero). Quando tem fila, abre mais trucks automaticamente. Para a maioria dos projetos, o food truck é mais que suficiente.

**Contexto:** Precisamos de um ambiente cloud para rodar os containers.

**Decisão:** Azure Container Apps (ACA).

```
┌─────────────────────────────────────────────────────────┐
│              Azure Container Apps Environment            │
│                                                          │
│  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌──────────┐ │
│  │ Gateway  │ │  Orders   │ │ Catalog  │ │ Identity │ │
│  │ (ingress)│ │  API      │ │  API     │ │  API     │ │
│  └────┬─────┘ └───────────┘ └──────────┘ └──────────┘ │
│       │                                                  │
│  ┌────┴─────┐                                           │
│  │ Worker   │                                            │
│  └──────────┘                                            │
│                                                          │
│  Managed Resources:                                      │
│  - Azure SQL Database                                    │
│  - Azure Cache for Redis                                 │
│  - Azure Service Bus (substituindo RabbitMQ)             │
│  - Azure Key Vault                                       │
│  - Azure Log Analytics                                   │
└─────────────────────────────────────────────────────────┘
```

**Por que Container Apps e não AKS?**
- Menor complexidade operacional (serverless)
- Scale-to-zero (custo baixo em desenvolvimento)
- Gerenciamento automático de infraestrutura
- Suporte a Dapr (futuro)
- Sem necessidade de gerenciar cluster Kubernetes

### ADR-021: Infrastructure as Code com Bicep

> 🧠 **Analogia — A Planta Baixa do Engenheiro:** Construir infraestrutura manualmente no portal Azure é como construir uma casa sem planta: você faz do jeito que lembra, cada vez fica diferente, e quando precisa replicar (“quero igual no ambiente de staging”), ninguém sabe exatamente o que foi feito. Bicep é a **planta baixa**: descreve *exatamente* o que deve existir. Precisa de um ambiente novo? Executa a planta. Precisa destruir? Executa sem a planta. Precisa auditar? Lê o código no Git. **Infraestrutura vira código, versionada, revisável, reproduzível.**

**Decisão:** Azure Bicep para provisionar infraestrutura.

**Por que Bicep e não Terraform?**
- Nativo Azure (first-party support)
- Sintaxe mais limpa que ARM Templates
- Intellisense no VS Code
- Sem state file para gerenciar
- Para um projeto Azure-focused, Bicep é ideal

---

## 3. Conceitos

> 💡 **Visão geral:** CI/CD e IaC são as práticas que separam **projetos de faculdade** de **software de produção**. Sem CI, bugs chegam ao main. Sem CD, deploys são manuais e arriscados. Sem IaC, ambientes são snowflakes irreproduzíveis. Senior developers não fazem deploy na sexta à noite clicando botões — eles fazem merge no main na segunda de manhã e a pipeline cuida do resto.

### Pipeline de CI/CD

```
    ┌──────────────┐
    │   Developer   │
    │  Push / PR    │
    └──────┬───────┘
           │
    ┌──────▼───────┐
    │   CI Pipeline │
    │              │
    │ 1. Checkout  │
    │ 2. Restore   │
    │ 3. Build     │
    │ 4. Test      │
    │ 5. Analyze   │
    └──────┬───────┘
           │ (merge to main)
    ┌──────▼───────┐
    │ CD Staging   │
    │              │
    │ 1. Build img │
    │ 2. Push GHCR │
    │ 3. Deploy    │
    └──────┬───────┘
           │ (manual approval)
    ┌──────▼───────┐
    │ CD Production│
    │              │
    │ 1. Pull img  │
    │ 2. Deploy    │
    └──────────────┘
```

### Ambientes

| Ambiente | Trigger | Approval | URL |
|----------|---------|----------|-----|
| Development | Local | - | localhost:8080 |
| Staging | merge to `main` | Automático | staging.orderflow.azurecontainerapps.io |
| Production | release tag | Manual (reviewer) | orderflow.azurecontainerapps.io |

---

## 4. Passo a Passo de Implementação

### 4.1 Configurar Ambientes no GitHub

1. Acesse **Settings → Environments** no repositório
2. Crie `staging` (sem proteção)
3. Crie `production` com **Required reviewers** (selecione 1+ pessoa)

### 4.2 Configurar Secrets no GitHub

**Settings → Secrets and variables → Actions:**

| Secret | Descrição |
|--------|-----------|
| `AZURE_CREDENTIALS` | Service Principal JSON para Azure CLI |
| `AZURE_SUBSCRIPTION_ID` | ID da subscription |
| `AZURE_RESOURCE_GROUP` | Nome do resource group |
| `SQL_ADMIN_PASSWORD` | Senha do Azure SQL |

### 4.3 Configurar Azure Service Principal

```bash
az login
az ad sp create-for-rbac \
    --name "orderflow-github-actions" \
    --role contributor \
    --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP> \
    --sdk-auth
```

Copie o JSON de saída para o secret `AZURE_CREDENTIALS`.

---

## 5. Código de Referência Completo

### 5.1 CI Pipeline

**`.github/workflows/ci.yml`**

```yaml
name: CI - Build & Test

on:
  pull_request:
    branches: [ main, develop ]
  push:
    branches: [ main ]

env:
  DOTNET_VERSION: '10.0.x'
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_NOLOGO: true

jobs:
  build-and-test:
    name: Build & Test
    runs-on: ubuntu-latest
    timeout-minutes: 15

    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: Y
          MSSQL_SA_PASSWORD: YourStr0ng!Pass
        ports:
          - 1433:1433
        options: >-
          --health-cmd "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStr0ng!Pass' -C -Q 'SELECT 1'"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/Directory.Packages.props') }}
          restore-keys: ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Unit Tests
        run: |
          dotnet test tests/OrderFlow.Orders.Domain.Tests \
            --no-build --configuration Release \
            --logger "trx;LogFileName=test-results.trx" \
            --collect:"XPlat Code Coverage"

      - name: Application Tests
        run: |
          dotnet test tests/OrderFlow.Orders.Application.Tests \
            --no-build --configuration Release \
            --logger "trx;LogFileName=test-results.trx"

      - name: Integration Tests
        run: |
          dotnet test tests/OrderFlow.IntegrationTests \
            --no-build --configuration Release \
            --logger "trx;LogFileName=test-results.trx"
        env:
          ConnectionStrings__OrdersDb: "Server=localhost;Database=OrderFlow_Tests;User Id=sa;Password=YourStr0ng!Pass;TrustServerCertificate=True"

      - name: Publish Test Results
        uses: dorny/test-reporter@v1
        if: success() || failure()
        with:
          name: Test Results
          path: '**/test-results.trx'
          reporter: dotnet-trx

      - name: Upload Coverage
        uses: codecov/codecov-action@v4
        if: github.event_name == 'push'
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
          files: '**/coverage.cobertura.xml'
```

### 5.2 CD Staging Pipeline

**`.github/workflows/cd-staging.yml`**

```yaml
name: CD - Deploy to Staging

on:
  push:
    branches: [ main ]

env:
  REGISTRY: ghcr.io
  IMAGE_PREFIX: ${{ github.repository_owner }}/orderflow

permissions:
  contents: read
  packages: write

jobs:
  build-images:
    name: Build & Push Docker Images
    runs-on: ubuntu-latest
    strategy:
      fail-fast: true
      matrix:
        service:
          - name: gateway
            dockerfile: src/ApiGateway/OrderFlow.Gateway/Dockerfile
          - name: identity-api
            dockerfile: src/Services/Identity/OrderFlow.Identity.Api/Dockerfile
          - name: catalog-api
            dockerfile: src/Services/Catalog/OrderFlow.Catalog.Api/Dockerfile
          - name: orders-api
            dockerfile: src/Services/Orders/OrderFlow.Orders.Api/Dockerfile
          - name: notification-worker
            dockerfile: src/Services/Notifications/OrderFlow.Notifications.Worker/Dockerfile

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Log in to GHCR
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-${{ matrix.service.name }}
          tags: |
            type=sha,prefix=
            type=raw,value=latest

      - name: Build and Push
        uses: docker/build-push-action@v5
        with:
          context: .
          file: ${{ matrix.service.dockerfile }}
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy-staging:
    name: Deploy to Staging
    runs-on: ubuntu-latest
    needs: build-images
    environment: staging

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy Gateway
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-gateway-staging
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-gateway:${{ github.sha }}

      - name: Deploy Identity API
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-identity-staging
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-identity-api:${{ github.sha }}

      - name: Deploy Catalog API
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-catalog-staging
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-catalog-api:${{ github.sha }}

      - name: Deploy Orders API
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-orders-staging
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-orders-api:${{ github.sha }}

      - name: Deploy Notification Worker
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-worker-staging
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-notification-worker:${{ github.sha }}

      - name: Smoke Test
        run: |
          GATEWAY_URL=$(az containerapp show \
            --name orderflow-gateway-staging \
            --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
            --query properties.configuration.ingress.fqdn -o tsv)

          echo "Gateway URL: https://$GATEWAY_URL"

          # Aguardar serviço ficar disponível
          for i in {1..30}; do
            if curl -sf "https://$GATEWAY_URL/health" > /dev/null 2>&1; then
              echo "Gateway is healthy!"
              exit 0
            fi
            echo "Waiting... ($i/30)"
            sleep 10
          done

          echo "Gateway health check failed!"
          exit 1
```

### 5.3 CD Production Pipeline

**`.github/workflows/cd-production.yml`**

```yaml
name: CD - Deploy to Production

on:
  release:
    types: [published]

env:
  REGISTRY: ghcr.io
  IMAGE_PREFIX: ${{ github.repository_owner }}/orderflow

permissions:
  contents: read
  packages: read

jobs:
  deploy-production:
    name: Deploy to Production
    runs-on: ubuntu-latest
    environment: production  # Requer approval manual

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Get Release Tag
        id: tag
        run: echo "version=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT

      - name: Deploy Gateway
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-gateway-prod
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-gateway:latest

      - name: Deploy Identity API
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-identity-prod
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-identity-api:latest

      - name: Deploy Catalog API
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-catalog-prod
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-catalog-api:latest

      - name: Deploy Orders API
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-orders-prod
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-orders-api:latest

      - name: Deploy Notification Worker
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: orderflow-worker-prod
          resourceGroup: ${{ secrets.AZURE_RESOURCE_GROUP }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-notification-worker:latest

      - name: Production Smoke Test
        run: |
          GATEWAY_URL=$(az containerapp show \
            --name orderflow-gateway-prod \
            --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
            --query properties.configuration.ingress.fqdn -o tsv)

          for i in {1..30}; do
            if curl -sf "https://$GATEWAY_URL/health" > /dev/null 2>&1; then
              echo "Production is healthy!"
              exit 0
            fi
            echo "Waiting... ($i/30)"
            sleep 10
          done
          exit 1
```

### 5.4 Azure Bicep — Infrastructure as Code

**`infra/azure/main.bicep`**

```bicep
@description('Nome do projeto')
param projectName string = 'orderflow'

@description('Ambiente (staging ou prod)')
@allowed(['staging', 'prod'])
param environment string = 'staging'

@description('Localização dos recursos')
param location string = resourceGroup().location

@description('Senha do SQL Admin')
@secure()
param sqlAdminPassword string
// Nota: @secure() impede que o valor apareça nos logs de deployment e no portal.
// Porém, o valor ainda é passado como texto na connection string do Container App.
// Para segurança máxima em produção, use Managed Identity + Azure Key Vault references.

@description('Prefixo do container registry (ex: ghcr.io/seu-usuario)')
param imagePrefix string

// ===== Variables =====
var suffix = '${projectName}-${environment}'
var logAnalyticsName = 'log-${suffix}'
var containerEnvName = 'cae-${suffix}'
var sqlServerName = 'sql-${suffix}'
var sqlDbName = 'sqldb-${suffix}'
var redisName = 'redis-${suffix}'
var serviceBusName = 'sb-${suffix}'

// ===== Log Analytics Workspace =====
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ===== Container Apps Environment =====
resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ===== Azure SQL =====
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'orderflowadmin'
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

// Allow Azure services
resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ===== Azure Cache for Redis =====
resource redis 'Microsoft.Cache/redis@2023-08-01' = {
  name: redisName
  location: location
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

// ===== Azure Service Bus =====
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

// ===== Container Apps =====
resource gatewayApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-gateway-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      containers: [
        {
          name: 'gateway'
          image: '${imagePrefix}/orderflow-gateway:latest'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: environment == 'prod' ? 'Production' : 'Staging' }
          ]
        }
      ]
      scale: {
        minReplicas: environment == 'prod' ? 1 : 0
        maxReplicas: environment == 'prod' ? 5 : 2
      }
    }
  }
}

resource catalogApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-catalog-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8080
      }
    }
    template: {
      containers: [
        {
          name: 'catalog-api'
          image: '${imagePrefix}/orderflow-catalog-api:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: environment == 'prod' ? 'Production' : 'Staging' }
            {
              name: 'ConnectionStrings__CatalogDb'
              value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=OrderFlow_Catalog;User ID=orderflowadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
            }
            {
              name: 'ConnectionStrings__Redis'
              value: '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
            }
          ]
        }
      ]
      scale: {
        minReplicas: environment == 'prod' ? 1 : 0
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: { metadata: { concurrentRequests: '50' } }
          }
        ]
      }
    }
  }
}

resource ordersApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-orders-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8080
      }
    }
    template: {
      containers: [
        {
          name: 'orders-api'
          image: '${imagePrefix}/orderflow-orders-api:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: environment == 'prod' ? 'Production' : 'Staging' }
            {
              name: 'ConnectionStrings__OrdersDb'
              value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDb.name};User ID=orderflowadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
            }
            {
              name: 'ConnectionStrings__Redis'
              value: '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
            }
          ]
        }
      ]
      scale: {
        minReplicas: environment == 'prod' ? 1 : 0
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: { metadata: { concurrentRequests: '50' } }
          }
        ]
      }
    }
  }
}

// ===== Outputs =====
output gatewayUrl string = gatewayApp.properties.configuration.ingress.fqdn
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output redisHostName string = redis.properties.hostName
output serviceBusEndpoint string = serviceBus.properties.serviceBusEndpoint
```

> **RabbitMQ → Azure Service Bus:** No desenvolvimento local (Docker), o OrderFlow usa **RabbitMQ**. Na Azure, o **Azure Service Bus** substitui o RabbitMQ como broker de mensagens. Isso funciona sem alteração de código graças ao **MassTransit**, que abstrai o transporte. Basta trocar a configuração:
>
> ```csharp
> // Local (appsettings.Development.json)
> cfg.UsingRabbitMq((ctx, rabbit) => { /* ... */ });
>
> // Azure (appsettings.Production.json)
> cfg.UsingAzureServiceBus((ctx, sb) => {
>     sb.Host(connectionString);
>     sb.ConfigureEndpoints(ctx);
> });
> ```
>
> Instale o pacote `MassTransit.Azure.ServiceBus.Core` e configure via `IConfiguration` para alternar automaticamente por ambiente.

### 5.5 Swagger / OpenAPI — Configuração por Serviço

Cada API já deve ter Swagger configurado. Exemplo reforçando a configuração:

**`src/Services/Orders/OrderFlow.Orders.Api/Extensions/SwaggerExtensions.cs`**

```csharp
using Microsoft.OpenApi.Models;

namespace OrderFlow.Orders.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddOrdersSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "OrderFlow - Orders API",
                Version = "v1",
                Description = "API para gerenciamento de pedidos do OrderFlow",
                Contact = new OpenApiContact
                {
                    Name = "OrderFlow Team",
                    Email = "dev@orderflow.com"
                }
            });

            // JWT Bearer
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header. Exemplo: 'Bearer {token}'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // XML comments
            var xmlFile = $"{typeof(SwaggerExtensions).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        return services;
    }

    public static WebApplication UseOrdersSwagger(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders API v1");
                options.RoutePrefix = "swagger";
            });
        }

        return app;
    }
}
```

### 5.6 README.md do Projeto

**`README.md`**

```markdown
# OrderFlow 🚀

Enterprise-grade order management system built with .NET 10 and microservices architecture.

## Architecture

```
Client → YARP Gateway → Identity API
                       → Catalog API
                       → Orders API → RabbitMQ → Notification Worker
```

## Tech Stack

| Category | Technology |
|----------|-----------|
| Runtime | .NET 10 / C# 13 |
| Architecture | Clean Architecture, DDD, CQRS |
| API Gateway | YARP Reverse Proxy |
| Messaging | RabbitMQ + MassTransit |
| Database | SQL Server + EF Core (write) + Dapper (read) |
| Cache | Redis |
| Auth | ASP.NET Identity + JWT |
| Observability | Serilog + Seq + OpenTelemetry |
| Containers | Docker + Docker Compose |
| CI/CD | GitHub Actions |
| Cloud | Azure Container Apps |

## Quick Start

### Prerequisites
- .NET 10 SDK
- Docker Desktop
- Git

### Run Locally

```bash
# Clone
git clone https://github.com/your-org/orderflow.git
cd orderflow

# Start infrastructure + services
docker compose up -d

# Apply migrations
dotnet ef database update --project src/Services/Orders/OrderFlow.Orders.Infrastructure \
    --startup-project src/Services/Orders/OrderFlow.Orders.Api

# Or run specific service in dev mode
dotnet run --project src/Services/Orders/OrderFlow.Orders.Api
```

### Endpoints

| Service | Local URL |
|---------|-----------|
| Gateway | http://localhost:8080 |
| Identity API | http://localhost:5001 |
| Catalog API | http://localhost:5002 |
| Orders API | http://localhost:5000 |
| Seq (Logs) | http://localhost:5342 |
| RabbitMQ Management | http://localhost:15672 |

### Run Tests

```bash
# All tests
dotnet test

# Only unit tests
dotnet test tests/OrderFlow.Orders.Domain.Tests

# Integration tests (requires Docker)
dotnet test tests/OrderFlow.IntegrationTests
```

## Project Structure

```
src/
├── ApiGateway/OrderFlow.Gateway/          # YARP reverse proxy
├── BuildingBlocks/
│   ├── OrderFlow.SharedKernel/            # Base classes, interfaces
│   └── OrderFlow.Contracts/               # Integration events
└── Services/
    ├── Identity/OrderFlow.Identity.Api/   # Auth + JWT
    ├── Catalog/OrderFlow.Catalog.Api/     # Product catalog
    ├── Orders/
    │   ├── OrderFlow.Orders.Api/          # REST API
    │   ├── OrderFlow.Orders.Application/  # CQRS handlers
    │   ├── OrderFlow.Orders.Domain/       # DDD aggregates
    │   └── OrderFlow.Orders.Infrastructure/ # EF Core, Dapper
    └── Notifications/
        └── OrderFlow.Notifications.Worker/ # MassTransit consumers
```

## Documentation

See [docs/](docs/) for detailed phase-by-phase documentation:

1. [Foundation & Structure](docs/fase-01-fundacao-estrutura.md)
2. [Domain (DDD)](docs/fase-02-dominio-ddd.md)
3. [CQRS & Application](docs/fase-03-cqrs-application.md)
4. [Authentication & Security](docs/fase-04-autenticacao-seguranca.md)
5. [Messaging & Async](docs/fase-05-mensageria-async.md)
6. [Cache & Observability](docs/fase-06-cache-observabilidade.md)
7. [Gateway & Docker](docs/fase-07-gateway-docker.md)
8. [CI/CD & Cloud](docs/fase-08-cicd-cloud.md)
```

---

## 6. Testes

### 6.1 Teste do CI Pipeline Localmente com `act`

```bash
# Instalar act (https://github.com/nektos/act)
# Testar CI localmente
act pull_request --container-architecture linux/amd64
```

### 6.2 Validar Bicep

```bash
# Login na Azure
az login

# Validar template Bicep
az deployment group validate \
    --resource-group orderflow-rg \
    --template-file infra/azure/main.bicep \
    --parameters environment=staging sqlAdminPassword='YourStr0ng!Pass'

# What-if (preview das mudanças)
az deployment group what-if \
    --resource-group orderflow-rg \
    --template-file infra/azure/main.bicep \
    --parameters environment=staging sqlAdminPassword='YourStr0ng!Pass'

# Deploy da infra
az deployment group create \
    --resource-group orderflow-rg \
    --template-file infra/azure/main.bicep \
    --parameters environment=staging sqlAdminPassword='YourStr0ng!Pass'
```

### 6.3 Smoke Tests Automatizados

**`tests/OrderFlow.SmokeTests/GatewayHealthTests.cs`**

```csharp
namespace OrderFlow.SmokeTests;

public class GatewayHealthTests
{
    private readonly HttpClient _client;

    public GatewayHealthTests()
    {
        var baseUrl = Environment.GetEnvironmentVariable("GATEWAY_URL")
            ?? "http://localhost:8080";
        _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    [Fact]
    public async Task Gateway_HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task Gateway_AuthRoute_IsReachable()
    {
        var response = await _client.GetAsync("/api/auth/nonexistent");

        // Esperamos 404, não 502 (gateway error)
        Assert.NotEqual(System.Net.HttpStatusCode.BadGateway, response.StatusCode);
    }
}
```

---

## 7. Checkpoint

> 💡 **Por que isso importa no dia-a-dia?** Esta é a fase que transforma seu **projeto local** em **software de verdade rodando na nuvem**. Um portfólio com CI/CD + IaC mostra ao recrutador que você pensa como um Sênior: não basta funcionar — precisa ser **automatizado, reproduzível e auditável**. Na entrevista, quando perguntarem "como você deploya?", a resposta não é "rodo publish e copio os arquivos" — é "merge no main dispara build, testes, push de imagem e deploy para staging automaticamente; produção requer approval".

### Validação Completa

- [ ] **CI Pipeline:** Build + test automáticos em PRs
- [ ] **CD Staging:** Deploy automático quando merge em `main`
- [ ] **CD Production:** Deploy com approval manual via release tags
- [ ] **Docker images no GHCR:** Push automático com tag SHA + latest
- [ ] **Matrix build:** Imagens construídas em paralelo
- [ ] **Azure Bicep:** Infra como código validada
- [ ] **Container Apps:** Serviços configurados com scaling
- [ ] **Swagger:** Documentação de API acessível em staging
- [ ] **README.md:** Documentação completa do projeto
- [ ] **Smoke tests:** Validação pós-deploy
- [ ] **Secrets gerenciados:** Nenhuma credencial hardcoded em pipelines
- [ ] **Environment protection:** Production com required reviewers
- [ ] **Commit:** `feat(cicd): add GitHub Actions pipelines and Azure Bicep infrastructure`

### Comandos Finais

```bash
# Validar TUDO localmente antes do push
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release

# Build Docker images
docker compose build

# Subir stack completa
docker compose up -d

# Testar fluxo completo
# 1. Registrar usuário
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"fullName":"João","email":"joao@test.com","password":"Joao@1234"}'

# 2. Login
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"joao@test.com","password":"Joao@1234"}' | jq -r '.accessToken')

# 3. Criar pedido
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"street":"Av Paulista","number":"1000","neighborhood":"Bela Vista","city":"São Paulo","state":"SP","zipCode":"01310100"}'

# 4. Verificar logs no Seq
open http://localhost:5342

# 5. Verificar mensagens no RabbitMQ
open http://localhost:15672  # orderflow / orderflow123
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| CI Workflow | `.github/workflows/ci.yml` (build, test, lint) |
| CD Workflow | `.github/workflows/cd.yml` (publish images, deploy) |
| Bicep Modules | `infra/main.bicep`, `modules/container-app.bicep`, `modules/acr.bicep` |
| Environment Config | `infra/parameters.staging.json`, `infra/parameters.production.json` |
| GitHub Secrets | `AZURE_CREDENTIALS`, `ACR_LOGIN_SERVER`, `ACR_USERNAME`, `ACR_PASSWORD` |
| Container Registry | Azure Container Registry (ACR) para imagens |
| Container Apps | Container App per service + environment |
| Log Analytics | Workspace para logs centralizados |
| Testes | Smoke tests no pipeline após deploy |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 8

**1. "Qual a diferença entre CI e CD?"**
— **CI** (Continuous Integration): a cada push, roda build + testes automaticamente. Detecta problemas **antes** do merge. **CD** (Continuous Deployment/Delivery): após CI passar, publica imagens e faz deploy automaticamente. **Delivery** = deploy para staging com aprovação manual para produção. **Deployment** = deploy automático até produção. No OrderFlow usamos Delivery (com gate de aprovação).

**2. "O que é IaC e por que usar Bicep?"**
— **Infrastructure as Code** define infraestrutura em arquivos versionáveis. Benefícios: reprodutibilidade (mesmo script = mesmo ambiente), auditoria (git blame), destruição/recriação rápida. **Bicep** é a linguagem nativa da Azure — mais concisa que ARM templates, com tipagem forte e módulos reutilizáveis. Alternativas: Terraform (multi-cloud), Pulumi (código geral).

**3. "Azure Container Apps vs AKS — quando usar cada um?"**
— **Container Apps** é serverless: você define a imagem, scaling rules e pronto. Sem gerenciar nodes, patches, networking. Ideal para microsserviços event-driven até ~50 serviços. **AKS** (Kubernetes manageado) dá controle total: service mesh, custom operators, GPU workloads. Complexidade muito maior. OrderFlow usa Container Apps porque não precisa de customização profunda de infra.

**4. "Como gerenciar secrets no pipeline de CI/CD?"**
— **Nunca** commits secrets no repositório. GitHub Actions usa **Repository Secrets** (encrypted, acessível via `${{ secrets.NAME }}`). Para Azure, use **Service Principal** com federated credentials (OIDC — sem password). Em runtime, Container Apps recebe secrets como **env vars** injetadas via Bicep. Key Vault para secrets que precisam de rotação automática.

**5. "O que são GitHub Actions Environments e reusable workflows?"**
— **Environments** (staging, production) definem: secrets específicos, protection rules (aprovação manual, wait timer), e deployment logs. **Reusable workflows** são templates chamados via `uses:` — evitam duplicação entre CI de múltiplos serviços. No OrderFlow: um workflow reusável para "build + test + publish image", chamado N vezes com parâmetros diferentes.

---

## Resumo do Projeto Completo — OrderFlow

| Fase | Tópicos | Arquivo |
|------|---------|---------|
| 0 | Visão Geral, ADRs, Glossário | `00-visao-geral.md` |
| 1 | Clean Architecture, SharedKernel, Docker Compose | `fase-01-fundacao-estrutura.md` |
| 2 | DDD, Aggregates, Value Objects, Domain Events | `fase-02-dominio-ddd.md` |
| 3 | CQRS, MediatR, Pipelines, EF Core + Dapper | `fase-03-cqrs-application.md` |
| 4 | Identity, JWT, Refresh Tokens, Rate Limiting | `fase-04-autenticacao-seguranca.md` |
| 5 | RabbitMQ, MassTransit, Outbox Pattern | `fase-05-mensageria-async.md` |
| 6 | Redis, OpenTelemetry, Serilog, Health Checks | `fase-06-cache-observabilidade.md` |
| 7 | YARP Gateway, Docker Multi-stage, Compose | `fase-07-gateway-docker.md` |
| 8 | GitHub Actions, Azure Container Apps, Bicep | `fase-08-cicd-cloud.md` |

---

## 🔬 Aprofundamento Sênior

### A1. Estratégias de Deploy — Além do Rolling Update

| Estratégia | Como Funciona | Risco | Quando |
|---|---|---|---|
| **Rolling** (default) | Substitui pods aos poucos | Baixo, mas tráfego em duas versões | Padrão |
| **Blue/Green** | 2 ambientes idênticos; swap completo | Zero — rollback instantâneo | Mudanças incompatíveis |
| **Canary** | 5% → 25% → 100% baseado em métricas | Baixo com gates | Features arriscadas |
| **Shadow** | Tráfego duplicado para v2 sem responder | Zero — apenas valida | Validar performance |

### A2. Canary Automatizado com Flagger ou Argo Rollouts

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Rollout
spec:
  strategy:
    canary:
      steps:
      - setWeight: 5
      - pause: { duration: 5m }
      - analysis:
          templates:
          - templateName: success-rate
          args:
          - name: service-name
            value: orders-canary
      - setWeight: 25
      - pause: { duration: 10m }
      - setWeight: 100
```

O **AnalysisTemplate** valida SLO (success rate, P95 latency) — se quebrar, **rollback automático**.

### A3. GitHub Actions — Reusable Workflows e Composite Actions

DRY entre 4 microserviços:

```yaml
# .github/workflows/_build-service.yml (reusable)
on:
  workflow_call:
    inputs:
      service-name: { required: true, type: string }

jobs:
  build:
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: dotnet test src/Services/${{ inputs.service-name }}
      - run: dotnet publish ...
```

```yaml
# .github/workflows/orders.yml
jobs:
  build:
    uses: ./.github/workflows/_build-service.yml
    with: { service-name: Orders }
```

### A4. Environments + Approvals

```yaml
jobs:
  deploy-prod:
    environment:
      name: production
      url: https://orderflow.com
    steps: [ ... ]
```

No GitHub: Settings → Environments → Production → **Required reviewers**. Deploy aguarda aprovação humana.

### A5. Bicep Avançado — Modules e Existing

```bicep
// modules/container-app.bicep
param serviceName string
param image string
param replicas int = 2

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: serviceName
  ...
}

// main.bicep
module orders 'modules/container-app.bicep' = {
  name: 'orders'
  params: { serviceName: 'orders', image: '...', replicas: 5 }
}
```

#### Existing — Referenciar recurso já criado
```bicep
resource kv 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: 'orderflow-kv'
  scope: resourceGroup('shared-rg')
}
```

### A6. Terraform/Pulumi — Quando Sair do Bicep

| Ferramenta | Quando |
|---|---|
| **Bicep** | 100% Azure, time .NET, simplicidade |
| **Terraform** | Multi-cloud, padrão de mercado, ampla comunidade |
| **Pulumi** | Quer IaC em **C#** (não DSL); reuso com SDK do app |

ADR sugerido: documentar a escolha.

### A7. Supply Chain Security

- **SBOM** (Software Bill of Materials) — `dotnet sbom-tool` ou Syft
- **Image signing** — Sigstore/Cosign assina imagens; deploy verifica
- **SLSA** levels — provenance attestations no CI

```yaml
- name: Generate SBOM
  run: syft ghcr.io/orderflow/orders:${{ github.sha }} -o spdx-json > sbom.json
- name: Sign image
  run: cosign sign --yes ghcr.io/orderflow/orders:${{ github.sha }}
```

### 💼 Perguntas Sênior

**"Qual estratégia de deploy escolher para uma feature arriscada?"** — Canary com analysis automatizado. 5% por 10min, validar success rate ≥ 99% e P95 ≤ baseline+10%. Se OK, sobe para 25% → 100%. Quebrou? Rollback automático. Flagger ou Argo Rollouts no K8s; Container Apps revisions no Azure.

**"Como você protege a supply chain de software?"** — (1) **SBOM** gerado no CI. (2) **Image signing** com Cosign. (3) Deploy **verifica assinatura** antes de subir. (4) **Dependabot** para deps. (5) **Trivy/Snyk** scan no CI. (6) Provenance **SLSA** levels 2-3 mínimo. Sem isso, atacante que comprometeu uma dep nuget pode injetar código.

---

> **🎓 Parabéns!** Você completou a trilha **Pleno** (Fases 01-08).
>
> 🚀 **Próxima parada:** [`fase-09-resiliencia-polly.md`](./fase-09-resiliencia-polly.md) — comece a **Trilha Sênior** (Fases 09-15) para fechar o gap das exigências de mercado 2026.
>
> 📋 Volte ao [`00-visao-geral.md`](./00-visao-geral.md#11-matriz-de-competências-pleno-vs-sênior) para acompanhar sua matriz de competências.
