# Fase 1 — Fundação e Estrutura

> **Objetivo:** Criar a solution multi-projeto, configurar tooling e ter o **Catalog API** funcionando com CRUD completo, banco SQL Server em Docker, logging estruturado e health checks.

### 🎯 O que você vai aprender nesta fase

- Criar uma solution .NET multi-projeto com `dotnet new` e `dotnet sln`
- Configurar **Clean Architecture** (Domain → Application → Infrastructure → Api)
- Implementar **Entity Framework Core** com Code First, Fluent API e Migrations
- Criar uma **REST API** com Controllers, versionamento e paginação
- Aplicar **FluentValidation** para validação declarativa
- Configurar **Serilog + Seq** para structured logging
- Implementar **Health Checks** (liveness + readiness) — o "check-up médico" do seu serviço
- Usar **Docker Compose** para subir SQL Server e Seq localmente
- Entender **Central Package Management** com `Directory.Packages.props`

---

## Sumário

1. [Visão da Fase](#1-visão-da-fase)
2. [Decisões Arquiteturais](#2-decisões-arquiteturais)
3. [Passo a Passo de Implementação](#3-passo-a-passo-de-implementação)
4. [Código de Referência Completo](#4-código-de-referência-completo)
5. [Testes](#5-testes)
6. [Checkpoint](#6-checkpoint)

---

## 1. Visão da Fase

### O Que Vamos Construir

```
OrderFlow/
├── src/
│   ├── BuildingBlocks/
│   │   └── OrderFlow.SharedKernel/
│   └── Services/
│       └── Catalog/
│           ├── OrderFlow.Catalog.Api/
│           ├── OrderFlow.Catalog.Application/
│           ├── OrderFlow.Catalog.Domain/
│           └── OrderFlow.Catalog.Infrastructure/
├── tests/
│   └── OrderFlow.Catalog.Api.Tests/
├── docker/
│   ├── docker-compose.yml
│   └── .env
├── OrderFlow.sln
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
└── .gitignore
```

### O Que Você Vai Praticar

| Tópico | Detalhe |
|--------|---------|
| **Solution multi-projeto** | Organização de projetos em Clean Architecture |
| **Clean Architecture** | Domain → Application → Infrastructure → Api |
| **Entity Framework Core** | DbContext, Fluent API, Migrations, Code First |
| **ASP.NET Core Controllers** | REST APIs com versionamento |
| **FluentValidation** | Validação declarativa com regras de negócio |
| **Global Exception Handling** | Middleware para tratar erros de forma padronizada |
| **Serilog** | Structured logging com sinks |
| **Health Checks** | Readiness e liveness probes |
| **Docker** | SQL Server em container + docker-compose |
| **Central Package Management** | Directory.Packages.props |

---

## 2. Decisões Arquiteturais

> 🤔 **Pense antes de ler:**
> 1. Para um CRUD simples de produtos, Clean Architecture é realmente necessário? Qual seria a alternativa mais leve?
> 2. Se dois projetos na mesma solution usam versões diferentes do mesmo NuGet — o que pode dar errado em produção?
> 3. Por que logs estruturados (JSON com campos tipados) são mais úteis que `Console.WriteLine("error: " + message)`?
>
> Reflita sobre essas perguntas. A seção abaixo não apenas responde, mas explica *por que* cada decisão foi tomada — e quando a decisão oposta seria mais adequada.

### 2.1 Por Que Clean Architecture para o Catalog?

> 🧠 **Analogia — O Edifício Corporativo:** Imagine um prédio comercial bem planejado. O *subsolo* (Domain) tem a fundação e os cofres — ninguém de fora toca lá. O *térreo* (Application) é a recepção: coordena quem entra e sai, mas não guarda nada de valor. Os *andares de escritório* (Infrastructure) são onde o trabalho pesado acontece — banco de dados, emails, serviços externos. E a *fachada* (Api) é o que o público vê — bonita, mas se você demolir e reconstruir, o prédio continua de pé. Cada andar tem uma **responsabilidade clara** e se comunica por **portas e elevadores** (interfaces), não por buracos na parede.

**O Catalog é um serviço CRUD.** Para um CRUD puro, Clean Architecture pode parecer overengineering — e de fato seria em um projeto de vida curta. Mas estamos usando aqui por três motivos:

1. **Didático** — Praticamos a separação de camadas em um contexto simples antes de aplicar no Orders (que é complexo de verdade)
2. **Consistência** — Todos os serviços seguem a mesma estrutura, facilitando onboarding. Quando um dev novo entra no time, ele sabe exatamente onde procurar cada coisa — independente de qual serviço estiver mexendo
3. **Preparação para escala** — CRUDs simples frequentemente ganham regras de negócio com o tempo. Começar organizado evita a refatoração dolorosa que todo Sênior já viveu: "o projeto começou simples, virou uma bola de lama e agora ninguém quer mexer"

**Alternativa:** Para CRUDs que *realmente permanecerão simples*, Vertical Slice Architecture (pasta por feature) é mais prática — menos cerimônia e acoplamento naturalmente baixo entre features. Discutiremos isso na seção de Aprofundamento Sênior.

### 2.2 Por Que Controllers (e não Minimal APIs)?

No Catalog, usamos **Controllers** com atributos `[ApiController]`. No Orders (Fase 3), usaremos **Minimal APIs**. Razões:

| Controllers | Minimal APIs |
|-------------|-------------|
| Convenção de mercado, muitas vagas pedem | Mais moderno, .NET 7+ |
| Agrupamento natural por controller | Agrupamento por extensão de mapa |
| Swagger auto-discovery fácil | Precisa configurar manualmente |
| Filters e model binding robustos | Mais leve, menos overhead |

**Decisão:** Praticar os dois estilos no projeto. Controllers no Catalog (mais convencional), Minimal APIs no Orders (mais moderno).

### 2.3 Por Que Central Package Management?

> 🧠 **Analogia — O Cardápio Único do Restaurante:** Imagine que você tem 8 filiais de um restaurante. Cada filial compra seus próprios ingredientes, com marcas e versões diferentes. O prato "estrogonofe" fica diferente em cada lugar. O `Directory.Packages.props` é como um **caderno de compras central**: todas as filiais usam a mesma marca de creme de leite, a mesma versão do molho. Se precisar trocar fornecedor, muda num lugar só.

Em uma solution com muitos projetos, cada `.csproj` pode definir versões diferentes do mesmo pacote NuGet. Sem controle, você acaba com o `FluentValidation 11.9` no Catalog e `11.11` no Orders — e quando uma breaking change aparece, você nem percebe. O **Central Package Management** centraliza **todas** as versões em um único arquivo.

**Benefícios concretos:**
- **Uma única fonte de verdade** — PR de atualização de pacotes toca um arquivo só, facilita code review
- **Atualizações atômicas** — `dotnet outdated` + uma linha alterada = todos os projetos atualizados
- **Conflitos impossíveis** — não existe cenário onde projeto A usa uma versão e projeto B outra acidentalmente
- **Segurança** — Vulnerabilidade em um pacote? Atualiza uma linha, rebuilda tudo

### 2.4 Por Que SQL Server em Docker (e não SQLite)?

| SQL Server (Docker) | SQLite |
|---------------------|--------|
| Banco real do mercado | In-memory simplificado |
| Suporta queries avançadas (CTE, window functions) | Limitações em queries complexas |
| Comportamento idêntico à produção | Pode mascarar bugs |
| Testa migrations reais | Migrations podem falhar no SQL Server |

**Decisão:** SQL Server em Docker para desenvolvimento. SQLite apenas em testes de integração como fallback rápido.

### 2.5 Por Que Serilog (e não ILogger nativo)?

> 🧠 **Analogia — Ficha de Atendimento vs Bilhete Rabiscado:** Imagine um hospital. Um médico pode rabiscar "paciente com dor" num post-it (log não-estruturado). Ou pode preencher uma ficha padronizada: nome, data, sintomas, setor, prioridade (log estruturado). Quando você precisa filtrar "todos os pacientes com dor no setor B nas últimas 2 horas", o post-it é inútil — a ficha permite consultas instantâneas. **Structured logging é a ficha. String concatenada é o post-it.**

O `ILogger` do Microsoft.Extensions.Logging é a **abstração** — como uma interface. Serilog é o **provider** mais popular no ecossistema .NET por:

- **Structured logging** — Cada propriedade vira um campo pesquisável. `Log.Information("Order {OrderId} created", orderId)` permite buscar por `OrderId = abc-123` no Seq
- **Sinks** — Console, File, Seq, Elasticsearch, Application Insights, Grafana Loki... conecta a qualquer destino
- **Enrichers** — Adiciona automaticamente `CorrelationId`, `MachineName`, `ThreadId` — sem poluir seu código
- **Templates** — Formato de saída customizável por ambiente (JSON em produção, texto colorido em dev)

**Código limpo:** Seus services continuam usando `ILogger<T>` (a abstração do .NET). Serilog é configurado **apenas no Program.cs**. Se amanhã quiser trocar por OpenTelemetry Logging ou NLog, muda um arquivo. Seus 200 services não sabem e não se importam — isso é o poder da abstração.

---

## 3. Passo a Passo de Implementação

### 3.1 Criar a Estrutura Base

```bash
# Criar diretório raiz
mkdir OrderFlow
cd OrderFlow

# Inicializar git
git init

# Criar global.json para travar versão do SDK
dotnet new globaljson --sdk-version 10.0.100

# Criar solution
dotnet new sln -n OrderFlow

# Criar estrutura de pastas
mkdir -p src/BuildingBlocks
mkdir -p src/Services/Catalog
mkdir -p src/ApiGateway
mkdir -p tests
mkdir -p docker
mkdir -p docs
```

### 3.2 Criar os Projetos

```bash
# === SharedKernel (classlib) ===
dotnet new classlib -n OrderFlow.SharedKernel -o src/BuildingBlocks/OrderFlow.SharedKernel
dotnet sln add src/BuildingBlocks/OrderFlow.SharedKernel

# === Catalog Domain ===
dotnet new classlib -n OrderFlow.Catalog.Domain -o src/Services/Catalog/OrderFlow.Catalog.Domain
dotnet sln add src/Services/Catalog/OrderFlow.Catalog.Domain

# === Catalog Application ===
dotnet new classlib -n OrderFlow.Catalog.Application -o src/Services/Catalog/OrderFlow.Catalog.Application
dotnet sln add src/Services/Catalog/OrderFlow.Catalog.Application

# === Catalog Infrastructure ===
dotnet new classlib -n OrderFlow.Catalog.Infrastructure -o src/Services/Catalog/OrderFlow.Catalog.Infrastructure
dotnet sln add src/Services/Catalog/OrderFlow.Catalog.Infrastructure

# === Catalog API ===
dotnet new webapi -n OrderFlow.Catalog.Api -o src/Services/Catalog/OrderFlow.Catalog.Api --use-controllers
dotnet sln add src/Services/Catalog/OrderFlow.Catalog.Api

# === Catalog Integration Tests ===
dotnet new xunit -n OrderFlow.Catalog.Api.Tests -o tests/OrderFlow.Catalog.Api.Tests
dotnet sln add tests/OrderFlow.Catalog.Api.Tests
```

### 3.3 Adicionar Referências entre Projetos

```bash
# Domain referencia SharedKernel
dotnet add src/Services/Catalog/OrderFlow.Catalog.Domain reference src/BuildingBlocks/OrderFlow.SharedKernel

# Application referencia Domain
dotnet add src/Services/Catalog/OrderFlow.Catalog.Application reference src/Services/Catalog/OrderFlow.Catalog.Domain

# Infrastructure referencia Application e Domain
dotnet add src/Services/Catalog/OrderFlow.Catalog.Infrastructure reference src/Services/Catalog/OrderFlow.Catalog.Application
dotnet add src/Services/Catalog/OrderFlow.Catalog.Infrastructure reference src/Services/Catalog/OrderFlow.Catalog.Domain

# Api referencia todos
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api reference src/Services/Catalog/OrderFlow.Catalog.Application
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api reference src/Services/Catalog/OrderFlow.Catalog.Infrastructure

# Tests referencia Api
dotnet add tests/OrderFlow.Catalog.Api.Tests reference src/Services/Catalog/OrderFlow.Catalog.Api
```

### 3.4 Configurar Central Package Management

Crie `Directory.Packages.props` na raiz:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- ASP.NET Core -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="7.2.0" />

    <!-- Entity Framework Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />

    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="11.11.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />

    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageVersion Include="Serilog.Sinks.Seq" Version="9.0.0" />
    <PackageVersion Include="Serilog.Enrichers.Environment" Version="3.0.1" />
    <PackageVersion Include="Serilog.Enrichers.Thread" Version="4.0.0" />

    <!-- Health Checks -->
    <PackageVersion Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />

    <!-- Testing -->
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
    <PackageVersion Include="FluentAssertions" Version="7.2.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  </ItemGroup>
</Project>
```

### 3.5 Configurar Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

### 3.6 Configurar .editorconfig

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# Organização de usings
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

# Preferências de namespace
csharp_style_namespace_declarations = file_scoped:suggestion

# Preferências de var
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion

# Preferências de expression body
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion

# Naming conventions
dotnet_naming_rule.private_fields_must_be_camel_case.severity = warning
dotnet_naming_rule.private_fields_must_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_must_be_camel_case.style = camel_case_with_underscore

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_with_underscore.required_prefix = _
dotnet_naming_style.camel_case_with_underscore.capitalization = camel_case
```

### 3.7 Instalar Pacotes NuGet

```bash
# Catalog.Domain (sem dependências externas - apenas SharedKernel)

# Catalog.Application
dotnet add src/Services/Catalog/OrderFlow.Catalog.Application package FluentValidation
dotnet add src/Services/Catalog/OrderFlow.Catalog.Application package FluentValidation.DependencyInjectionExtensions

# Catalog.Infrastructure
dotnet add src/Services/Catalog/OrderFlow.Catalog.Infrastructure package Microsoft.EntityFrameworkCore
dotnet add src/Services/Catalog/OrderFlow.Catalog.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer

# Catalog.Api
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package Serilog.AspNetCore
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package Serilog.Sinks.Console
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package Serilog.Sinks.Seq
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package Serilog.Enrichers.Environment
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package Serilog.Enrichers.Thread
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package AspNetCore.HealthChecks.SqlServer
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package Swashbuckle.AspNetCore
dotnet add src/Services/Catalog/OrderFlow.Catalog.Api package Microsoft.EntityFrameworkCore.Design

# Tests
dotnet add tests/OrderFlow.Catalog.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/OrderFlow.Catalog.Api.Tests package Microsoft.EntityFrameworkCore.InMemory
dotnet add tests/OrderFlow.Catalog.Api.Tests package FluentAssertions
dotnet add tests/OrderFlow.Catalog.Api.Tests package Moq
```

### 3.8 Docker Compose para SQL Server

Crie `docker/docker-compose.yml`:

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: orderflow-sqlserver
    ports:
      - "1433:1433"
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${SA_PASSWORD:-OrderFlow@2026!}"
      MSSQL_PID: "Developer"
    volumes:
      - sqlserver-data:/var/lib/mssql/data
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$$SA_PASSWORD" -C -Q "SELECT 1" || exit 1
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    restart: unless-stopped

  seq:
    image: datalust/seq:latest
    container_name: orderflow-seq
    ports:
      - "5341:80"
    environment:
      ACCEPT_EULA: "Y"
    volumes:
      - seq-data:/data
    restart: unless-stopped

volumes:
  sqlserver-data:
  seq-data:
```

Crie `docker/.env`:

```env
SA_PASSWORD=OrderFlow@2026!
```

---

## 4. Código de Referência Completo

### Fluxo de Uma Request HTTP — De Ponta a Ponta

> 💡 **Antes de ver o código, entenda o fluxo.** Quando um `GET /api/v1/products/123` chega no Catalog API, ele percorre as 4 camadas da Clean Architecture. Visualize como uma carta passando por departamentos de uma empresa:

```
  Request HTTP (GET /api/v1/products/123)
         │
         ▼
  ┌─────────────────────┐
  │   Api Layer          │  Controller recebe a request, valida binding,
  │   (ProductsCtrl)     │  chama o Application Layer.
  └──────────┬──────────┘  Retorna: IActionResult (200, 404, 400...)
             │
             ▼
  ┌─────────────────────┐
  │   Application Layer  │  ProductService coordena: busca via repositório,
  │   (ProductService)   │  valida regras de aplicação, retorna DTO.
  └──────────┬──────────┘  NÃO tem SQL, NÃO tem HttpContext.
             │
             ▼
  ┌─────────────────────┐
  │   Domain Layer       │  Product entity com regras de negócio.
  │   (Product.cs)       │  "Preço não pode ser negativo" vive aqui.
  └──────────┬──────────┘  NÃO conhece banco, NÃO conhece HTTP.
             │
             ▼
  ┌─────────────────────┐
  │   Infrastructure     │  ProductRepository traduz para SQL via EF Core.
  │   (EF Core)          │  CatalogDbContext → SQL Server → dados voltam.
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │   SQL Server         │  Banco real em Docker.
  └─────────────────────┘
             │
  Response HTTP (200 OK + JSON)
```

> 🔑 **A Regra de Dependência:** As setas apontam para **dentro**. Api depende de Application. Application depende de Domain. Infrastructure depende de Application E Domain. Mas **Domain não depende de nada** — ele é o coração isolado do sistema. Se amanhã você trocar SQL Server por PostgreSQL, só Infrastructure muda. Domain e Application nem percebem.

### 4.1 SharedKernel — Classes Base

> 🧠 **Analogia — O DNA da Aplicação:** O SharedKernel é como o **DNA compartilhado** entre irmãos. Cada serviço (Catalog, Orders, Identity) é uma "pessoa" diferente com personalidade própria, mas todos carregam os mesmos genes fundamentais: o que é uma `Entity`, o que é um `DomainEvent`, como fazer `Equals`. Se cada serviço definisse isso sozinho, seria como reinventar o conceito de "ser humano" toda vez que nasce alguém. O SharedKernel garante que **as regras fundamentais são escritas uma vez e herdadas por todos**.

> ⚠️ **Armadilha Comum:** Cuidado para não transformar o SharedKernel em um "lixão compartilhado". Ele deve conter **apenas** conceitos que realmente são universais (Entity, ValueObject, interfaces de infraestrutura). Se algo é específico de Orders, pertence ao Orders.Domain. A regra de ouro: *"Se eu remover um serviço inteiro, o SharedKernel continua fazendo sentido?"*

**`src/BuildingBlocks/OrderFlow.SharedKernel/Entity.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public abstract class Entity
{
    public Guid Id { get; protected init; }

    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
```

**`src/BuildingBlocks/OrderFlow.SharedKernel/IDomainEvent.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
```

> **Por que não herdar de `MediatR.INotification` agora?**
>
> Nesta fase, o SharedKernel é a base de **todos** os serviços. Se adicionarmos MediatR aqui, forçamos uma dependência transitiva em projetos que não precisam dele (ex: Catalog). Na **Fase 3** (CQRS), quando o Orders API realmente precisar, atualizaremos esta interface para herdar de `INotification`. Isso segue o princípio YAGNI e mantém o SharedKernel leve.
>
> ```csharp
> // Fase 3 — após adicionar MediatR ao Orders:
> using MediatR;
> public interface IDomainEvent : INotification { DateTime OccurredOn { get; } }
> ```

**`src/BuildingBlocks/OrderFlow.SharedKernel/AuditableEntity.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; protected init; }
    public DateTime? UpdatedAt { get; protected set; }

    protected void SetUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**`src/BuildingBlocks/OrderFlow.SharedKernel/IRepository.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
```

**`src/BuildingBlocks/OrderFlow.SharedKernel/IUnitOfWork.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

### 4.2 Catalog Domain — Entities

**`src/Services/Catalog/OrderFlow.Catalog.Domain/Entities/Category.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Domain.Entities;

public sealed class Category : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<Product> _products = [];
    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    private Category() { } // EF Core

    public static Category Create(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim();
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}
```

**`src/Services/Catalog/OrderFlow.Catalog.Domain/Entities/Product.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Domain.Entities;

public sealed class Product : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }

    private Product() { } // EF Core

    public static Product Create(
        string name,
        string sku,
        decimal price,
        int stockQuantity,
        Guid categoryId,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        if (price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));

        if (stockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(stockQuantity));

        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            Price = price,
            StockQuantity = stockQuantity,
            CategoryId = categoryId,
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, decimal price, int stockQuantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (price < 0) throw new ArgumentException("Price cannot be negative.", nameof(price));
        if (stockQuantity < 0) throw new ArgumentException("Stock quantity cannot be negative.", nameof(stockQuantity));

        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        StockQuantity = stockQuantity;
        SetUpdated();
    }

    public void ChangeCategory(Guid newCategoryId)
    {
        if (newCategoryId == Guid.Empty)
            throw new ArgumentException("Category ID cannot be empty.", nameof(newCategoryId));

        CategoryId = newCategoryId;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public bool HasSufficientStock(int quantity) => StockQuantity >= quantity;

    public void DecreaseStock(int quantity)
    {
        if (!HasSufficientStock(quantity))
            throw new InvalidOperationException(
                $"Insufficient stock. Available: {StockQuantity}, Requested: {quantity}");

        StockQuantity -= quantity;
        SetUpdated();
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        StockQuantity += quantity;
        SetUpdated();
    }
}
```

### 4.3 Catalog Domain — Interfaces

**`src/Services/Catalog/OrderFlow.Catalog.Domain/Interfaces/IProductRepository.cs`**

```csharp
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        Guid? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default);
}
```

**`src/Services/Catalog/OrderFlow.Catalog.Domain/Interfaces/ICategoryRepository.cs`**

```csharp
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default);
}
```

### 4.4 Catalog Application — DTOs

**`src/Services/Catalog/OrderFlow.Catalog.Application/DTOs/ProductDto.cs`**

```csharp
namespace OrderFlow.Catalog.Application.DTOs;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    Guid CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateProductRequest(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    Guid CategoryId);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity);

public sealed record ProductSearchRequest(
    string? SearchTerm = null,
    Guid? CategoryId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int Page = 1,
    int PageSize = 20);
```

**`src/Services/Catalog/OrderFlow.Catalog.Application/DTOs/CategoryDto.cs`**

```csharp
namespace OrderFlow.Catalog.Application.DTOs;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int ProductCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateCategoryRequest(
    string Name,
    string? Description);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description);
```

**`src/Services/Catalog/OrderFlow.Catalog.Application/DTOs/PagedResult.cs`**

```csharp
namespace OrderFlow.Catalog.Application.DTOs;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
```

### 4.5 Catalog Application — Validators

**`src/Services/Catalog/OrderFlow.Catalog.Application/Validators/CreateProductValidator.cs`**

```csharp
using FluentValidation;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Application.Validators;

public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(50).WithMessage("SKU must not exceed 50 characters.")
            .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("SKU must contain only letters, numbers and hyphens.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be zero or positive.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity must be zero or positive.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required.");
    }
}
```

**`src/Services/Catalog/OrderFlow.Catalog.Application/Validators/CreateCategoryValidator.cs`**

```csharp
using FluentValidation;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Application.Validators;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}
```

### 4.6 Catalog Application — Services

**`src/Services/Catalog/OrderFlow.Catalog.Application/Interfaces/IProductService.cs`**

```csharp
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Application.Interfaces;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<PagedResult<ProductDto>> SearchAsync(ProductSearchRequest request, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

**`src/Services/Catalog/OrderFlow.Catalog.Application/Interfaces/ICategoryService.cs`**

```csharp
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Application.Interfaces;

public interface ICategoryService
{
    Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> GetAllActiveAsync(CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

**`src/Services/Catalog/OrderFlow.Catalog.Application/Services/ProductService.cs`**

```csharp
using FluentValidation;
using Microsoft.Extensions.Logging;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.Catalog.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Application.Services;

public sealed class ProductService(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator,
    ILogger<ProductService> logger) : IProductService
{
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct);
        return product is null ? null : MapToDto(product);
    }

    public async Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var product = await productRepository.GetBySkuAsync(sku, ct);
        return product is null ? null : MapToDto(product);
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(
        ProductSearchRequest request, CancellationToken ct = default)
    {
        var (items, totalCount) = await productRepository.SearchAsync(
            request.SearchTerm,
            request.CategoryId,
            request.MinPrice,
            request.MaxPrice,
            request.Page,
            request.PageSize,
            ct);

        return new PagedResult<ProductDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }

    public async Task<ProductDto> CreateAsync(
        CreateProductRequest request, CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, ct)
            ?? throw new KeyNotFoundException($"Category with ID '{request.CategoryId}' not found.");

        if (await productRepository.SkuExistsAsync(request.Sku, ct))
            throw new InvalidOperationException($"A product with SKU '{request.Sku}' already exists.");

        var product = Product.Create(
            request.Name,
            request.Sku,
            request.Price,
            request.StockQuantity,
            request.CategoryId,
            request.Description);

        await productRepository.AddAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Product created: {ProductId} - {ProductName}", product.Id, product.Name);

        return MapToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(
        Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var product = await productRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Product with ID '{id}' not found.");

        product.Update(request.Name, request.Description, request.Price, request.StockQuantity);

        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Product updated: {ProductId}", product.Id);

        return MapToDto(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Product with ID '{id}' not found.");

        product.Deactivate();
        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Product deactivated: {ProductId}", product.Id);
    }

    private static ProductDto MapToDto(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Sku,
        product.Price,
        product.StockQuantity,
        product.IsActive,
        product.CategoryId,
        product.Category?.Name,
        product.CreatedAt,
        product.UpdatedAt);
}
```

### 4.7 Catalog Infrastructure — EF Core

> 🧠 **Analogia — O Tradutor Simultâneo:** O EF Core funciona como um **tradutor** entre duas línguas: C# e SQL. Você escreve `context.Products.Where(p => p.Price > 100)` em C# e ele traduz para `SELECT * FROM Products WHERE Price > 100` em SQL. O `DbContext` é o **escritório do tradutor** — ele gerencia as conversas (conexões), lembra o que já foi dito (change tracking) e garante que nada se perca na tradução (migrations). A **Fluent API** é como um dicionário de regras especiais: "esse campo tem no máximo 200 caracteres", "esse é obrigatório", "esses dois estão relacionados".

**`src/Services/Catalog/OrderFlow.Catalog.Infrastructure/Data/CatalogDbContext.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Infrastructure.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        // Global query filter: soft delete
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.IsActive);
        modelBuilder.Entity<Category>().HasQueryFilter(c => c.IsActive);
    }
}
```

> 💡 **O que é Global Query Filter e por que usar?** O `HasQueryFilter` adiciona automaticamente um `WHERE IsActive = 1` em **toda** query EF Core para essa entidade. Assim, produtos desativados ficam "invisíveis" sem que cada query precise lembrar de filtrar. 
>
> **Três cuidados importantes:**
> 1. **Admin precisa ver desativados?** Use `.IgnoreQueryFilters()` em queries específicas de administração
> 2. **Performance:** O filtro adiciona condição ao SQL, mas o impacto é mínimo com índice filtrado: `CREATE INDEX IX_Products_Active ON Products(IsActive) WHERE IsActive = 1`
> 3. **Cascading:** Se um `Product` está filtrado (inactive), incluir `Products` via navigation property retorna lista vazia — mesmo que existam no banco. Isso pode confundir em relatórios

**`src/Services/Catalog/OrderFlow.Catalog.Infrastructure/Data/Configurations/ProductConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Catalog.Domain.Entities;

namespace OrderFlow.Catalog.Infrastructure.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.HasIndex(p => p.Sku)
            .IsUnique();

        builder.HasIndex(p => new { p.CategoryId, p.Name });

        builder.HasIndex(p => p.Price);

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignorar domain events (propriedade do Entity base)
        builder.Ignore(p => p.DomainEvents);
    }
}
```

**`src/Services/Catalog/OrderFlow.Catalog.Infrastructure/Data/Configurations/CategoryConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Catalog.Domain.Entities;

namespace OrderFlow.Catalog.Infrastructure.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.HasIndex(c => c.Name)
            .IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}
```

### 4.8 Catalog Infrastructure — Repositories

**`src/Services/Catalog/OrderFlow.Catalog.Infrastructure/Data/Repositories/ProductRepository.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.Catalog.Domain.Interfaces;

namespace OrderFlow.Catalog.Infrastructure.Data.Repositories;

public sealed class ProductRepository(CatalogDbContext context) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Product entity, CancellationToken ct = default)
    {
        await context.Products.AddAsync(entity, ct);
    }

    public void Update(Product entity)
    {
        context.Products.Update(entity);
    }

    public void Remove(Product entity)
    {
        context.Products.Remove(entity);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Sku == sku, ct);
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(
        Guid categoryId, CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        Guid? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p =>
                p.Name.Contains(searchTerm) ||
                p.Sku.Contains(searchTerm));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default)
    {
        return await context.Products
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Sku == sku, ct);
    }
}
```

### 4.9 Catalog Infrastructure — DI Registration

**`src/Services/Catalog/OrderFlow.Catalog.Infrastructure/DependencyInjection.cs`**

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Application.Services;
using OrderFlow.Catalog.Domain.Interfaces;
using OrderFlow.Catalog.Infrastructure.Data;
using OrderFlow.Catalog.Infrastructure.Data.Repositories;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("CatalogDb"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                }));

        // Unit of Work
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CatalogDbContext>());

        // Repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // Services
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<Application.Validators.CreateProductValidator>();

        return services;
    }
}
```

### 4.10 Catalog API — Controllers

**`src/Services/Catalog/OrderFlow.Catalog.Api/Controllers/ProductsController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;

namespace OrderFlow.Catalog.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    /// <summary>
    /// Search products with filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] ProductSearchRequest request,
        CancellationToken ct)
    {
        var result = await productService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a product by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var product = await productService.GetByIdAsync(id, ct);
        return product is not null ? Ok(product) : NotFound();
    }

    /// <summary>
    /// Get a product by SKU.
    /// </summary>
    [HttpGet("sku/{sku}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySku(string sku, CancellationToken ct)
    {
        var product = await productService.GetBySkuAsync(sku, ct);
        return product is not null ? Ok(product) : NotFound();
    }

    /// <summary>
    /// Create a new product.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken ct)
    {
        var product = await productService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /// <summary>
    /// Update an existing product.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken ct)
    {
        var product = await productService.UpdateAsync(id, request, ct);
        return Ok(product);
    }

    /// <summary>
    /// Soft-delete (deactivate) a product.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await productService.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

**`src/Services/Catalog/OrderFlow.Catalog.Api/Controllers/CategoriesController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;

namespace OrderFlow.Catalog.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var categories = await categoryService.GetAllActiveAsync(ct);
        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var category = await categoryService.GetByIdAsync(id, ct);
        return category is not null ? Ok(category) : NotFound();
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken ct)
    {
        var category = await categoryService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken ct)
    {
        var category = await categoryService.UpdateAsync(id, request, ct);
        return Ok(category);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await categoryService.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

### 4.11 Catalog API — Global Exception Handling Middleware

**`src/Services/Catalog/OrderFlow.Catalog.Api/Middleware/GlobalExceptionHandler.cs`**

```csharp
using System.Net;
using System.Text.Json;
using FluentValidation;

namespace OrderFlow.Catalog.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorResponse) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse(
                    "Validation Error",
                    validationEx.Errors.Select(e => e.ErrorMessage).ToArray())),

            KeyNotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ErrorResponse("Not Found", [notFoundEx.Message])),

            InvalidOperationException invalidOpEx => (
                HttpStatusCode.Conflict,
                new ErrorResponse("Business Rule Violation", [invalidOpEx.Message])),

            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse("Invalid Argument", [argEx.Message])),

            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse("Internal Server Error", ["An unexpected error occurred."]))
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning(exception, "Handled exception: {StatusCode} - {Message}",
                (int)statusCode, exception.Message);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

public sealed record ErrorResponse(string Title, string[] Errors);
```

### 4.12 Catalog API — Program.cs

**`src/Services/Catalog/OrderFlow.Catalog.Api/Program.cs`**

```csharp
using OrderFlow.Catalog.Api.Middleware;
using OrderFlow.Catalog.Infrastructure;
using OrderFlow.Catalog.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// === Serilog ===
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithThreadId()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.Seq(context.Configuration["Seq:Url"] ?? "http://localhost:5341");
});

// === Services ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "OrderFlow Catalog API",
        Version = "v1",
        Description = "API for managing product catalog and categories."
    });
});

// Catalog Infrastructure (EF Core, Repositories, Services, Validators)
builder.Services.AddCatalogInfrastructure(builder.Configuration);

// Global Exception Handler
builder.Services.AddTransient<GlobalExceptionHandler>();

// Health Checks
// 💡 Health Checks são como o "check-up médico" do seu serviço.
// Liveness = "O coração está batendo?" (processo vivo)
// Readiness = "O paciente está pronto pra receber visitas?" (dependências OK)
// Se o Liveness falhar → Kubernetes reinicia o container (ressuscita o paciente).
// Se o Readiness falhar → Load balancer para de enviar tráfego (paciente em recuperação).
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("CatalogDb")!,
        name: "sqlserver",
        tags: ["db", "ready"]);

var app = builder.Build();

// === Middleware Pipeline ===
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionHandler>();
app.UseSerilogRequestLogging();

app.MapControllers();

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false // Liveness: apenas verifica se o app responde
});

// === Auto-migrate in Development ===
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program;
```

### 4.13 Catalog API — appsettings.json

```json
{
  "ConnectionStrings": {
    "CatalogDb": "Server=localhost,1433;Database=OrderFlow_Catalog;User Id=sa;Password=OrderFlow@2026!;TrustServerCertificate=true"
  },
  "Seq": {
    "Url": "http://localhost:5341"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    }
  },
  "AllowedHosts": "*"
}
```

---

## 5. Testes

### 5.1 Conceitos de Testing

| Tipo | O que testa | Onde |
|------|------------|------|
| **Unit Test** | Lógica de domínio isolada | `Domain.Tests` |
| **Integration Test** | API completa, banco real | `Api.Tests` |
| **Naming Convention** | `MethodName_StateUnderTest_ExpectedBehavior` | Todos |

### 5.2 Integration Tests com WebApplicationFactory

**`tests/OrderFlow.Catalog.Api.Tests/CustomWebApplicationFactory.cs`**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Catalog.Infrastructure.Data;

namespace OrderFlow.Catalog.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove SQL Server registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CatalogDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Use InMemory database for tests
            services.AddDbContext<CatalogDbContext>(options =>
            {
                options.UseInMemoryDatabase("CatalogTestDb_" + Guid.NewGuid());
            });
        });

        builder.UseEnvironment("Testing");
    }
}
```

**`tests/OrderFlow.Catalog.Api.Tests/CategoriesControllerTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Api.Tests;

public class CategoriesControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ValidCategory_ReturnsCreated()
    {
        // Arrange
        var request = new CreateCategoryRequest("Electronics", "Electronic devices and gadgets");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Name.Should().Be("Electronics");
        category.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var createRequest = new CreateCategoryRequest("Books", "All kinds of books");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/categories", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoryDto>();

        // Act
        var response = await _client.GetAsync($"/api/v1/categories/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.Name.Should().Be("Books");
    }

    [Fact]
    public async Task GetById_NonExistentCategory_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/categories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateCategoryRequest("", null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

**`tests/OrderFlow.Catalog.Api.Tests/ProductsControllerTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Api.Tests;

public class ProductsControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<CategoryDto> CreateCategoryAsync()
    {
        var request = new CreateCategoryRequest("Test Category", "Category for testing");
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    [Fact]
    public async Task Create_ValidProduct_ReturnsCreated()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var request = new CreateProductRequest(
            "Laptop Pro",
            "High-performance laptop",
            "LAPTOP-001",
            2999.99m,
            10,
            category.Id);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product.Should().NotBeNull();
        product!.Name.Should().Be("Laptop Pro");
        product.Sku.Should().Be("LAPTOP-001");
        product.Price.Should().Be(2999.99m);
    }

    [Fact]
    public async Task Search_WithTerm_ReturnsFilteredResults()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Wireless Mouse", null, $"MOUSE-{Guid.NewGuid():N}"[..20], 49.99m, 100, category.Id));
        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Wireless Keyboard", null, $"KB-{Guid.NewGuid():N}"[..20], 79.99m, 50, category.Id));
        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Monitor 27\"", null, $"MON-{Guid.NewGuid():N}"[..20], 399.99m, 20, category.Id));

        // Act
        var response = await _client.GetAsync("/api/v1/products?searchTerm=Wireless");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountGreaterOrEqualTo(2);
        result.Items.Should().OnlyContain(p => p.Name.Contains("Wireless"));
    }

    [Fact]
    public async Task Create_DuplicateSku_ReturnsConflict()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var sku = $"DUP-{Guid.NewGuid():N}"[..15];

        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Product 1", null, sku, 10m, 1, category.Id));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Product 2", null, sku, 20m, 2, category.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_ExistingProduct_ReturnsNoContent()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "To Delete", null, $"DEL-{Guid.NewGuid():N}"[..15], 10m, 1, category.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<ProductDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/products/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft-deleted (filtered by query filter)
        var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

---

## ⚠️ Erros Comuns Nesta Fase

> Esses erros são cometidos por 8 em cada 10 devs na primeira implementação de Clean Architecture. Conheça-os antes de tropeçar neles.

| # | Erro | Consequência | Solução |
|---|---|---|---|
| 1 | **Referência circular** — Infrastructure referenciando Api, ou Domain referenciando Infrastructure | Compilação falha com "circular dependency detected" | Revise a Dependency Rule: `Domain ← Application ← Infrastructure ← Api`. Nunca no sentido contrário |
| 2 | **Lógica de negócio no Controller** — Controller com 30+ linhas de if/else | Controller vira God Class, impossível testar isoladamente | Se o controller tem mais que 5-10 linhas, extraia para Application/Service |
| 3 | **Esquecer `CancellationToken`** em métodos async | Requests canceladas continuam processando, desperdiçando CPU | Todo método async recebe e propaga `CancellationToken ct` como último parâmetro |
| 4 | **Copiar `.csproj` com versões diferentes** sem Central Package Management | FluentValidation 11.9 no Catalog, 11.11 no Orders → comportamentos diferentes | Use `Directory.Packages.props` desde o início |
| 5 | **Não testar `SearchAsync` com filtros combinados** | SQL gerado pelo EF Core com múltiplos `.Where()` pode gerar query lenta ou incorreta | Escreva testes com combinações: sem filtro, com 1, com 2, com todos |
| 6 | **Migration falha e dev apaga o banco** | Perde dados de desenvolvimento, não aprende a resolver conflitos | Aprenda `dotnet ef migrations remove`, `Script-Migration`, e rollback manual |

---

## 🔧 Troubleshooting — Fase 01

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| `SA_PASSWORD` rejeitada pelo SQL Server | Senha não atende complexidade (8+ chars, maiúscula, minúscula, número, especial) | Use `OrderFlow@2026!` ou similar |
| Porta 1433 já em uso | SQL Server local ou outro container usando a porta | `netstat -ano \| findstr 1433` para encontrar; mude a porta no compose |
| Migration falha com "Login failed" | Container não está healthy (demora ~10-30s) | `docker ps` deve mostrar `(healthy)`, não `(health: starting)` |
| Seq não abre no browser | Porta 5341 mapeada para porta 80 do container | Acesse `http://localhost:5341` (não 80) |
| `dotnet ef` não encontrado | Global tool não instalada | `dotnet tool install --global dotnet-ef` |
| "No project was found" no ef migrations | Path do `--project` ou `--startup-project` errado | Use caminhos relativos à raiz da solution |
| Swagger não mostra endpoints | Controller não tem `[ApiController]` ou route `[Route("api/v1/[controller]")]` | Verifique atributos no controller |
| Health check `/health/ready` retorna Unhealthy | SQL Server não acessível (connection string errada ou container down) | `docker compose logs sqlserver` para verificar |

> 💡 **Regra dos 3 passos para debugar qualquer problema nesta fase:** (1) `docker ps` — o container está rodando? (2) `docker compose logs <serviço>` — tem erro no log? (3) Verifique connection string no `appsettings.Development.json`. Em 90% dos casos, está em um desses três.

---

## 6. Checkpoint

> 💡 **Por que isso importa no dia-a-dia?** Em times reais, a Fase 1 é a fundação que define a **velocidade de todas as fases seguintes**. Um projeto mal estruturado na semana 1 gera débito técnico que multiplica por 10 o custo de cada feature futura. Sêniores sabem: investir 2 dias na estrutura correta economiza 2 meses de refatoração.

### Como Validar que a Fase 1 Está Completa

- [ ] **Solution compila sem erros:** `dotnet build`
- [ ] **Docker Compose sobe:** `docker-compose -f docker/docker-compose.yml up -d`
- [ ] **SQL Server está acessível** na porta 1433
- [ ] **Seq está acessível** em `http://localhost:5341`
- [ ] **Migrations rodam:** `dotnet ef database update --project src/Services/Catalog/OrderFlow.Catalog.Infrastructure --startup-project src/Services/Catalog/OrderFlow.Catalog.Api`
- [ ] **API responde:** `dotnet run --project src/Services/Catalog/OrderFlow.Catalog.Api`
- [ ] **Swagger acessível:** `http://localhost:5002/swagger`
- [ ] **CRUD de Categories funciona** via Swagger/Postman
- [ ] **CRUD de Products funciona** via Swagger/Postman
- [ ] **Health checks respondem:**
  - `GET /health/live` → 200
  - `GET /health/ready` → 200 (se SQL Server está OK)
- [ ] **Logs aparecem no Seq** em `http://localhost:5341`
- [ ] **Testes passam:** `dotnet test`
- [ ] **Commit:** `feat(catalog): implement Catalog API with CRUD, EF Core, and health checks`

### Comandos de Verificação

```bash
# Build completo
dotnet build OrderFlow.sln

# Rodar todos os testes
dotnet test OrderFlow.sln --verbosity normal

# Verificar health
curl http://localhost:5002/health/live
curl http://localhost:5002/health/ready

# Testar CRUD (com httpie ou curl)
# Criar categoria
curl -X POST http://localhost:5002/api/v1/categories \
  -H "Content-Type: application/json" \
  -d '{"name": "Electronics", "description": "Electronic devices"}'

# Listar categorias
curl http://localhost:5002/api/v1/categories
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| Solution .NET | `OrderFlow.sln` |
| Configurações MSBuild | `Directory.Build.props`, `Directory.Packages.props`, `global.json` |
| SharedKernel | `src/BuildingBlocks/OrderFlow.SharedKernel/` |
| Catalog Domain | `Product.cs`, `Category.cs`, `IProductRepository.cs` |
| Catalog Application | `ProductService.cs`, `CreateProductRequest.cs`, validators |
| Catalog Infrastructure | `CatalogDbContext.cs`, `ProductRepository.cs`, migrations |
| Catalog Api | `ProductsController.cs`, `CategoriesController.cs`, `Program.cs` |
| Docker Compose | `docker/docker-compose.yml` + `.env` |
| Testes | `OrderFlow.Catalog.Api.Tests/` |
| Config files | `.editorconfig`, `.gitignore`, `nuget.config` |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 1

**1. "O que é Clean Architecture e como ela difere de uma arquitetura em camadas tradicional?"**
— Na arquitetura em camadas tradicional, a dependência flui de cima para baixo: Presentation → Business → Data. O problema: Business depende de Data (implementação concreta). Na Clean Architecture, a dependência aponta para **dentro**: Api → Infrastructure → Application → Domain. O Domain é **puro** — zero dependências externas. Infrastructure implementa as interfaces definidas em Application (inversão de dependência). Resultado: você pode trocar SQL Server por PostgreSQL sem alterar uma linha de lógica de negócio.

**2. "Por que Central Package Management (Directory.Packages.props) e não versões no .csproj?"**
— Com 15+ projetos na solution, gerenciar versões de pacotes em cada `.csproj` vira pesadelo — um projeto com Serilog 4.0 e outro com 3.9 gera bugs sutis. O `Directory.Packages.props` centraliza **todas** as versões em um só lugar. Atualizar uma dependência é mudar **uma linha**. É o equivalente ao Maven BOM ou ao `gradle.properties` no mundo Java.

**3. "Qual a diferença entre Controllers e Minimal APIs? Quando usar cada um?"**
— Controllers seguem o padrão MVC: classes com rotas declarativas (`[Route]`), suporte a filtros, model binding robusto, versionamento fácil com `Asp.Versioning`. Minimal APIs usam delegates lambda direto no `Program.cs` ou extension methods — menos cerimônia, mais performance, ideal para microserviços focados. **No OrderFlow:** Catalog API usa Controllers (para praticar o estilo enterprise clássico); Orders API usa Minimal APIs (para praticar o estilo moderno). Na entrevista, mostre que domina **ambos**.

**4. "Por que SQL Server e não PostgreSQL para um projeto .NET?"**
— SQL Server é o banco mais pedido nas vagas .NET no Brasil (especialmente em bancos, fintechs e consultorias). EF Core suporta ambos igualmente. Para portfólio, SQL Server é mais estratégico para empregabilidade. Em produção, a escolha depende de custo (PostgreSQL é gratuito) e features específicas.

**5. "Explique como funciona EF Core Code First com Migrations."**
— Você define as entidades C# e suas configurações (Fluent API em `IEntityTypeConfiguration<T>`). O EF Core compara o modelo C# com o estado do banco e gera uma migration (classe C# com `Up()` e `Down()`). `dotnet ef database update` aplica as migrations pendentes. Vantagem: schema versionado no Git, reproduzível em qualquer ambiente. Risco: nunca edite uma migration já aplicada em produção — crie uma nova.

---

## 🔬 Aprofundamento Sênior

> Esta seção foi adicionada na **Reformulação Sênior 2026**. Cobre tópicos exigidos em vagas Sênior que vão além do Clean Architecture básico.

### A1. Vertical Slice Architecture — Quando Substitui Clean

Clean Architecture organiza por **camada técnica** (Domain, Application, Infra, Api). Vertical Slice organiza por **feature de negócio**.

```
Features/
  CreateOrder/
    CreateOrderEndpoint.cs
    CreateOrderHandler.cs
    CreateOrderValidator.cs
    CreateOrderRequest.cs
  GetOrderById/
    GetOrderByIdEndpoint.cs
    GetOrderByIdHandler.cs
    OrderDetailDto.cs
```

**Vantagens:**
- Cada feature é **autônoma** — adicionar feature não toca código existente
- Acoplamento alto **dentro** da slice (queremos!), baixo **entre** slices
- Onboarding mais rápido — dev novo lê a feature inteira em um diretório
- Excelente para **CRUD-heavy** systems

**Desvantagens:**
- Reuso menor — alguma duplicação consciente entre features
- Domínio compartilhado fica difuso — não há "camada de domínio" óbvia

**Quando usar Vertical Slice:**
- Sistema majoritariamente CRUD com regras leves
- Time grande com muitas features paralelas
- Microserviço pequeno (< 30 endpoints)

**Quando usar Clean:**
- Domínio rico com invariantes complexas
- Múltiplas formas de invocação (API, CLI, worker) compartilhando lógica
- Time investiu em DDD

> **Decisão híbrida:** Catalog usa Clean (domínio simples mas estável); Orders usa Clean+CQRS (domínio rico). Um futuro `Reports` API poderia usar Vertical Slice puro.

### A2. EF Core 10 — Recursos Avançados

#### Value Converters
Mapear value objects para colunas primitivas:

```csharp
builder.Property(p => p.Sku)
    .HasConversion(
        sku => sku.Value,
        value => Sku.Create(value));
```

#### Owned Entities (Value Objects no Schema)
```csharp
builder.OwnsOne(p => p.Price, price =>
{
    price.Property(m => m.Amount).HasColumnName("PriceAmount").HasPrecision(18, 2);
    price.Property(m => m.Currency).HasColumnName("PriceCurrency").HasMaxLength(3);
});
```

#### Interceptors — Cross-cutting Sem Repository Pattern
```csharp
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        foreach (var entry in eventData.Context!.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

#### Compiled Models — Startup mais Rápido
```bash
dotnet ef dbcontext optimize --output-dir Persistence/CompiledModels
```
Para apps AOT ou cold-start crítico.

### A3. Concurrency Token (Optimistic Locking)
```csharp
builder.Property(p => p.RowVersion).IsRowVersion();
// Em conflito, EF lança DbUpdateConcurrencyException — trate retornando 409 Conflict
```

### A4. Database per Service — Disciplina Crítica
Nunca compartilhar DB entre microserviços. Sintomas de violação:
- Migration de um serviço quebra outro
- Performance degrada porque outro serviço bloqueia tabela
- Schema vira monólito disfarçado

Solução para "preciso de dado de outro serviço": **eventos** (Fase 05) ou **API** (gRPC, Fase 13). Nunca SQL cross-service.

### 💼 Perguntas Sênior

**"Quando você abandonaria Clean Architecture por Vertical Slice?"** — Quando o sistema é CRUD-heavy, time é grande com features paralelas, e o overhead de 4 camadas + repositórios não paga ROI. Vertical Slice acopla código relacionado, desacopla features.

**"Como você evolui schema sem downtime?"** — Padrão **Expand → Migrate → Contract**: (1) adicionar nova coluna nullable, (2) deploy app que escreve em ambas, (3) migrar dados em background, (4) deploy app que lê só da nova, (5) remover coluna antiga. Nunca faz `RENAME` ou `DROP` em colunas usadas.

---

---

## 🔗 Conectando os Pontos

### O que construímos aqui que será usado nas próximas fases

| Artefato | Usado em | Como |
|----------|----------|------|
| `Entity`, `AuditableEntity` | **Fase 02** (DDD) | `AggregateRoot` herda de `Entity`, ganhando domain events |
| `IDomainEvent` | **Fase 02-03** | Ganha `: INotification` para integrar com MediatR |
| `IRepository<T>`, `IUnitOfWork` | **Fase 02-03** | Orders implementa com padrão de aggregate persistence |
| `Directory.Packages.props` | **Todas** | Cada novo pacote é adicionado aqui — um lugar só |
| Docker Compose | **Fase 05-08** | Ganha RabbitMQ, Redis, Prometheus, Grafana |
| Health Checks | **Fase 06-07** | OpenTelemetry e YARP usam para routing e observabilidade |
| Serilog | **Fase 06** | Ganha enrichers, correlation ID, sinks adicionais |

> 💡 **Preview da Fase 02:** Na próxima fase, o SharedKernel que criamos aqui vai ganhar `AggregateRoot` e `ValueObject` — as peças que faltam para modelar o domínio rico do Orders API. O `Product` simples que fizemos aqui será contrastado com o `Order` rico: invariantes, state machine, domain events. A diferença vai te mostrar *quando* Clean Architecture + DDD vale a pena — e quando é overengineering.

> **Próximo passo:** Avance para `fase-02-dominio-ddd.md` para implementar o domínio rico do Orders API.
>
> 🚀 **Trilha Sênior relacionada:** [`fase-10-performance-csharp-moderno.md`](./fase-10-performance-csharp-moderno.md) — EF Core compiled queries e benchmarking.
