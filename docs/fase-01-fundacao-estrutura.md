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

**Código limpo:** Seus services continuam usando `ILogger<T>` (a abstração do .NET). Serilog é configurado **apenas no `HostingExtensions.cs`** via extension method. Se amanhã quiser trocar por OpenTelemetry Logging ou NLog, muda um arquivo. Seus 200 services não sabem e não se importam — isso é o poder da abstração.

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

> 💡 **Nota sobre o template xunit:** O comando `dotnet new xunit` cria um projeto com dependências padrão (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`). Essas versões padrão podem estar desatualizadas. Como usamos **Central Package Management**, as versões do `Directory.Packages.props` sobrescrevem as do template. Além disso, substituiremos `xunit` (v2, deprecated) por `xunit.v3` (v3) na seção de pacotes.

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
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="10.1.7" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.6" />

    <!-- Entity Framework Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.6" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.6" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.6" />

    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />

    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.1.1" />
    <PackageVersion Include="Serilog.Sinks.Seq" Version="9.0.0" />
    <PackageVersion Include="Serilog.Enrichers.Environment" Version="3.0.1" />
    <PackageVersion Include="Serilog.Enrichers.Thread" Version="4.0.0" />

    <!-- Health Checks -->
    <PackageVersion Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />

    <!-- Testing -->
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.6" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.6" />
    <PackageVersion Include="FluentAssertions" Version="8.9.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.4.0" />
    <PackageVersion Include="coverlet.collector" Version="10.0.0" />
  </ItemGroup>
</Project>
```

> 🔬 **O que cada pacote faz — Guia de Referência**
>
> **ASP.NET Core:**
> | Pacote | Responsabilidade |
> |--------|-----------------|
> | `Swashbuckle.AspNetCore` | Gera documentação **Swagger/OpenAPI** automaticamente a partir dos controllers. Cria a UI interativa em `/swagger` para testar endpoints |
> | `Microsoft.AspNetCore.OpenApi` | Pacote nativo da Microsoft para anotações OpenAPI. Vem por padrão no template `webapi` e complementa o Swashbuckle com metadata de endpoints |
>
> **Entity Framework Core:**
> | Pacote | Responsabilidade |
> |--------|-----------------|
> | `Microsoft.EntityFrameworkCore` | O **ORM principal** — mapeia classes C# para tabelas SQL, gerencia change tracking, traduz LINQ para SQL |
> | `Microsoft.EntityFrameworkCore.SqlServer` | **Provider** específico para SQL Server — traduz as queries para T-SQL e configura tipos como `decimal(18,2)`, `nvarchar(200)` |
> | `Microsoft.EntityFrameworkCore.Design` | Ferramentas **design-time** para gerar migrations via CLI (`dotnet ef migrations add`). Só necessário no projeto que roda o `dotnet ef` |
> | `Microsoft.EntityFrameworkCore.Tools` | Ferramentas para o **Package Manager Console** do Visual Studio (`Add-Migration`, `Update-Database`). Alternativa ao CLI para quem usa VS |
>
> **Validação:**
> | Pacote | Responsabilidade |
> |--------|-----------------|
> | `FluentValidation` | Framework de validação **fluente** — define regras como `RuleFor(x => x.Name).NotEmpty().MaximumLength(200)` em vez de Data Annotations |
> | `FluentValidation.DependencyInjectionExtensions` | Integra FluentValidation com o **DI container** do ASP.NET Core — registra automaticamente todos os validators via `AddValidatorsFromAssembly` |
>
> **Logging:**
> | Pacote | Responsabilidade |
> |--------|-----------------|
> | `Serilog.AspNetCore` | Integra Serilog ao pipeline do ASP.NET Core — registra via `AddSerilog()`, adiciona request logging (`UseSerilogRequestLogging`) |
> | `Serilog.Sinks.Console` | **Sink** que escreve logs no console. Em dev, exibe com cores e formato legível; em containers, usa JSON para coleta automatizada |
> | `Serilog.Sinks.Seq` | **Sink** que envia logs para o **Seq** — uma UI web para buscar, filtrar e analisar logs estruturados (como um "Google para seus logs") |
> | `Serilog.Enrichers.Environment` | **Enricher** que adiciona `MachineName` e `EnvironmentName` automaticamente a cada log — útil para distinguir logs de dev/staging/prod |
> | `Serilog.Enrichers.Thread` | **Enricher** que adiciona `ThreadId` — ajuda a rastrear problemas de concorrência ("qual thread gerou esse erro?") |
>
> **Health Checks:**
> | Pacote | Responsabilidade |
> |--------|-----------------|
> | `AspNetCore.HealthChecks.SqlServer` | Verifica conectividade com o SQL Server. Usado no endpoint `/health/ready` para confirmar que o banco está acessível |
>
> **Testing:**
> | Pacote | Responsabilidade |
> |--------|-----------------|
> | `Microsoft.AspNetCore.Mvc.Testing` | Fornece `WebApplicationFactory<T>` — sobe a aplicação **inteira em memória** para testes de integração sem precisar de servidor real |
> | `Microsoft.EntityFrameworkCore.InMemory` | Provider de banco **in-memory** — substitui SQL Server nos testes. Rápido mas não testa SQL real (constraints, migrations) |
> | `FluentAssertions` | Assertions **legíveis** — `result.Should().Be(200)` em vez de `Assert.Equal(200, result)`. Mensagens de erro muito melhores |
> | `Moq` | Framework de **mocking** — cria implementações fake de interfaces para unit tests (`Mock<IProductRepository>`) |
> | `xunit.v3` | Framework de **testes** principal. A v3 é a versão mais recente, substituindo o `xunit` (v2) que está deprecated. Suporta `[Fact]`, `[Theory]`, paralelismo |
> | `xunit.runner.visualstudio` | **Test runner** que integra xUnit com Visual Studio e `dotnet test` — descobre e executa os testes |
> | `Microsoft.NET.Test.Sdk` | **SDK de testes** da Microsoft — infraestrutura base que conecta frameworks de teste (xUnit, NUnit) ao `dotnet test` e ao Visual Studio Test Explorer |
> | `coverlet.collector` | **Code coverage** — mede quais linhas do código são exercitadas pelos testes. Gera relatórios que mostram % de cobertura por classe/método |

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

> 🔬 **O que cada configuração do `.editorconfig` significa**
>
> O `.editorconfig` é um **contrato de formatação** que IDEs (Visual Studio, VS Code, Rider) leem automaticamente. Sem ele, cada dev formata do seu jeito — tabs vs spaces, CRLF vs LF — e PRs ficam poluídos com mudanças de whitespace.
>
> **Configurações globais `[*]` (todos os arquivos):**
>
> | Configuração | Valor | O que faz |
> |---|---|---|
> | `indent_style = space` | Espaços | Usa **espaços** para indentação (não tabs). Padrão do ecossistema .NET |
> | `indent_size = 4` | 4 espaços | Cada nível de indentação = 4 espaços. Padrão C# |
> | `end_of_line = crlf` | `\r\n` | Final de linha Windows. Em projetos cross-platform, use `lf` |
> | `charset = utf-8` | UTF-8 | Encoding universal — suporta acentos, emojis, caracteres especiais |
> | `trim_trailing_whitespace = true` | Remove | Apaga espaços em branco **no final das linhas** — evita diffs desnecessários |
> | `insert_final_newline = true` | Adiciona | Garante **linha em branco no final** de todo arquivo — convenção POSIX que evita warnings em Git |
>
> **Configurações C# `[*.cs]`:**
>
> | Configuração | Valor | O que faz |
> |---|---|---|
> | `dotnet_sort_system_directives_first` | `true` | Coloca `using System.*` antes de outros namespaces nos imports |
> | `dotnet_separate_import_directive_groups` | `false` | Não adiciona linha em branco entre grupos de `using` |
> | `csharp_style_namespace_declarations` | `file_scoped` | Prefere `namespace X;` (uma linha) em vez de `namespace X { }` (bloco inteiro) — economiza um nível de indentação |
> | `csharp_style_var_for_built_in_types` | `false` | Prefere `int x = 5` em vez de `var x = 5` — tipos primitivos ficam mais claros explícitos |
> | `csharp_style_var_when_type_is_apparent` | `true` | Prefere `var product = new Product()` — quando o tipo já está visível à direita, `var` reduz repetição |
> | `csharp_style_expression_bodied_methods` | `when_on_single_line` | Métodos de uma linha podem usar `=>`: `public int Sum() => a + b;` |
> | `csharp_style_expression_bodied_properties` | `true` | Propriedades calculadas usam `=>`: `public string Full => $"{First} {Last}";` |
>
> **Naming conventions (a parte mais útil):**
>
> ```ini
> dotnet_naming_rule.private_fields_must_be_camel_case.severity = warning
> ```
> Se um campo privado não seguir a convenção, a IDE mostra **warning** (sublinhado amarelo). Isso padroniza o time sem depender de code reviews manuais.
>
> ```ini
> dotnet_naming_style.camel_case_with_underscore.required_prefix = _
> dotnet_naming_style.camel_case_with_underscore.capitalization = camel_case
> ```
> Campos privados devem começar com `_` e usar camelCase: `_productRepository`, `_logger`, `_domainEvents`. Essa é a convenção oficial da Microsoft para .NET.

### 3.7 Instalar Pacotes NuGet

> ⚠️ **Importante — Central Package Management (CPM):** Como o `Directory.Packages.props` está habilitado, os arquivos `.csproj` **não podem** ter atributo `Version=` nos `PackageReference`. As versões ficam **exclusivamente** no `Directory.Packages.props` (seção 3.4). Além disso, o template `dotnet new xunit` e `dotnet new webapi` criam pacotes com `Version=` inline, que precisam ser corrigidos antes de compilar.

**Passo 1 — Corrigir pacotes do template (Api e Tests)**

O template `webapi` adicionou `Microsoft.AspNetCore.OpenApi` com versão inline, e o template `xunit` adicionou `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` e `coverlet.collector` com versões inline. Precisamos remover essas versões e substituir `xunit` (v2, deprecated) por `xunit.v3`.

Edite `src/Services/Catalog/OrderFlow.Catalog.Api/OrderFlow.Catalog.Api.csproj` — remova o `Version="..."` do `Microsoft.AspNetCore.OpenApi`:

```xml
<!-- ❌ ANTES (gerado pelo template) -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.5" />

<!-- ✅ DEPOIS (compatível com CPM) -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" />
```

Edite `tests/OrderFlow.Catalog.Api.Tests/OrderFlow.Catalog.Api.Tests.csproj` — remova versões inline e substitua `xunit` por `xunit.v3`:

```xml
<!-- ❌ ANTES (gerado pelo template xunit) -->
<PackageReference Include="coverlet.collector" Version="6.0.4" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />

<!-- ✅ DEPOIS (compatível com CPM + xunit.v3) -->
<PackageReference Include="coverlet.collector">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="xunit.v3" />
<PackageReference Include="xunit.runner.visualstudio">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

**Passo 2 — Adicionar pacotes de cada camada**

Com o CPM configurado, usamos `dotnet add package` **sem especificar versão** — o SDK busca a versão automaticamente do `Directory.Packages.props`:

```bash
# Catalog.Domain (sem dependências externas — apenas SharedKernel)

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

**Passo 3 — Verificar a compilação**

```bash
dotnet clean
dotnet restore
dotnet build
```

Se aparecer erro **NU1008** ("Projects that use central package management should not define the version on the PackageReference items"), significa que algum `.csproj` ainda tem `Version=` inline. Verifique e remova o atributo `Version` do `PackageReference` que o erro indica.

> 💡 **Por que `xunit.v3` em vez de `xunit`?** O pacote `xunit` (v2) foi descontinuado. O `xunit.v3` traz melhorias significativas: suporte nativo a `IAsyncLifetime`, melhor paralelismo, assertions mais ricas, e compatibilidade com .NET 8+/10+. A API de testes (`[Fact]`, `[Theory]`, `IClassFixture<T>`) permanece a mesma — a migração é apenas trocar o pacote.

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

> 🔬 **Docker Compose — O que é, para que serve e o que cada configuração significa**
>
> **O que é Docker Compose?** É uma ferramenta que define e orquestra **múltiplos containers** com um único arquivo YAML. Em vez de rodar `docker run` com 15 flags para cada serviço, você declara tudo no `docker-compose.yml` e sobe tudo com `docker compose up -d`.
>
> **Para que serve no OrderFlow?** Em vez de instalar SQL Server e Seq na sua máquina (o que polui o sistema operacional e cria conflitos de versão), cada serviço roda **isolado** em seu container. Quando não precisar mais, `docker compose down` remove tudo sem deixar rastro.
>
> **Explicação linha a linha:**
>
> ```yaml
> services:                          # Declara os containers que serão criados
> ```
>
> **SQL Server:**
> | Configuração | O que faz |
> |---|---|
> | `image: mcr.microsoft.com/mssql/server:2022-latest` | Usa a imagem oficial do SQL Server 2022 da Microsoft. `mcr` = Microsoft Container Registry |
> | `container_name: orderflow-sqlserver` | Nome fixo do container — facilita referência em logs e comandos (`docker logs orderflow-sqlserver`) |
> | `ports: "1433:1433"` | Mapeia porta `1433` do host para `1433` do container. Assim, `localhost:1433` no seu código se conecta ao SQL Server dentro do container |
> | `ACCEPT_EULA: "Y"` | Aceita a licença do SQL Server (obrigatório) |
> | `MSSQL_SA_PASSWORD: "${SA_PASSWORD:-OrderFlow@2026!}"` | Senha do admin. `${SA_PASSWORD:-...}` lê do arquivo `.env` ou usa o valor padrão após `:-` |
> | `MSSQL_PID: "Developer"` | Edição do SQL Server — Developer é gratuita e tem todas as features do Enterprise |
> | `volumes: sqlserver-data:/var/lib/mssql/data` | **Named volume** — dados do banco ficam persistidos mesmo se o container for recriado. Sem isso, `docker compose down` apagaria tudo |
> | `healthcheck: test: sqlcmd ... "SELECT 1"` | Tenta executar `SELECT 1` a cada 10s. Se falhar 5x, o container é marcado como `unhealthy`. O `start_period: 30s` dá tempo para o SQL Server inicializar |
> | `restart: unless-stopped` | Reinicia automaticamente o container se ele crashar — exceto se você fez `docker compose stop` manualmente |
>
> **Seq (Log Server):**
> | Configuração | O que faz |
> |---|---|
> | `image: datalust/seq:latest` | Imagem oficial do Seq — UI web para visualizar logs estruturados |
> | `ports: "5341:80"` | A porta 80 interna do Seq é mapeada para `5341` no host. Acesse `http://localhost:5341` para ver a UI |
> | `ACCEPT_EULA: "Y"` | Aceita a licença (Seq é gratuito para desenvolvimento single-user) |
> | `volumes: seq-data:/data` | Persiste os logs mesmo se recriar o container |
>
> **Volumes:**
> ```yaml
> volumes:
>   sqlserver-data:    # Volume nomeado para dados do SQL Server
>   seq-data:          # Volume nomeado para dados do Seq
> ```
> **Named volumes** são gerenciados pelo Docker e ficam em `\\wsl$\docker-desktop-data\...` (Windows) ou `/var/lib/docker/volumes/` (Linux). São mais seguros que bind mounts porque o Docker controla permissões e lifecycle.
>
> **Arquivo `.env`:**
> O Docker Compose lê automaticamente o `.env` no mesmo diretório. Isso evita hardcoding de senhas no YAML — o `.env` fica no `.gitignore` para não ir para o repositório.
>
> **Comandos essenciais:**
> ```bash
> docker compose up -d           # Sobe tudo em background (-d = detached)
> docker compose down            # Para e remove containers (volumes ficam)
> docker compose down -v         # Para, remove containers E volumes (cuidado!)
> docker compose logs sqlserver  # Vê logs do SQL Server
> docker compose ps              # Status dos containers
> ```

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

> 📂 **Por que os arquivos estão todos na raiz, sem subpastas?**
>
> O SharedKernel tem apenas 5 arquivos (`Entity.cs`, `AuditableEntity.cs`, `IDomainEvent.cs`, `IRepository.cs`, `IUnitOfWork.cs`), todos no mesmo namespace `OrderFlow.SharedKernel`. Criar pastas como `Entities/`, `Interfaces/`, `Events/` para 1-2 arquivos cada seria over-engineering — adiciona navegação desnecessária sem benefício real. A regra prática: **crie pastas quando tiver 5+ arquivos do mesmo tipo**. Quando o SharedKernel crescer (ValueObject base, Result pattern, novos eventos), reorganize. Até lá, a raiz plana é a solução mais simples que funciona (KISS).

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

> 🏛️ **Nota Arquitetural — Por que uma classe base `Entity`?**
>
> **Decisão:** Toda entidade do sistema herda de `Entity`, centralizando identidade (`Id`) e domain events.
>
> **Alternativas consideradas:**
> - *Interface `IEntity`* — mais flexível para herança múltipla, mas perde a implementação compartilhada de `Equals`, `GetHashCode` e domain events. Cada entidade reimplementaria esse código.
> - *Sem classe base* — cada entidade define seu próprio `Id`. Funciona para sistemas pequenos, mas gera inconsistência em microserviços (qual o tipo do Id? `int`? `Guid`? `string`?).
> - *Generic `Entity<TId>`* — permite `Entity<int>`, `Entity<string>`, etc. Mais flexível, mas adiciona complexidade genérica em toda a codebase. Para o OrderFlow, `Guid` como identidade universal é suficiente.
>
> **Trade-off aceito:** Acoplamento a `Guid` como tipo de Id em troca de simplicidade e consistência cross-service.

> 🔬 **Nota de Engenharia — Dissecando o código linha a linha**
>
> ```csharp
> public abstract class Entity
> ```
> `abstract` impede instanciação direta — você nunca cria `new Entity()`, apenas entidades concretas como `Product` ou `Order`. Isso é o **Template Method Pattern**: a classe base define o "esqueleto" (identidade, igualdade, eventos), e as filhas preenchem os detalhes.
>
> ```csharp
> public Guid Id { get; protected init; }
> ```
> **`protected init`** é a combinação perfeita aqui: `init` permite atribuir o valor apenas durante a inicialização do objeto (factory method ou object initializer), e `protected` garante que somente a própria classe e suas filhas podem setar — código externo nunca altera o Id. Isso é **imutabilidade seletiva**: protege contra alteração acidental sem impedir a criação.
>
> ```csharp
> private readonly List<IDomainEvent> _domainEvents = [];
> public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
> ```
> **Estrutura de dados:** Internamente é um `List<T>` (array dinâmico, O(1) para `Add`, O(n) para `Contains`). Externamente, é exposto como `IReadOnlyCollection<T>` — o consumidor pode iterar e contar, mas **não pode adicionar, remover ou limpar**. Isso é o **princípio do menor privilégio** aplicado a coleções. O `[]` é a **collection expression** do C# 12 — equivale a `new List<IDomainEvent>()` mas mais idiomático.
>
> ```csharp
> if (Id == Guid.Empty || other.Id == Guid.Empty)
>     return false;
> ```
> **Guarda essencial:** Entidades com `Guid.Empty` (não persistidas ainda) nunca são iguais — mesmo que ambas tenham Id zero. Sem isso, dois objetos novos seriam considerados iguais antes de terem identidade, o que quebraria lógica em coleções como `HashSet<Entity>`.
>
> **Padrão aplicado:** **Identity Equality** (DDD) — entidades são iguais pelo **Id**, não pelos campos. Dois `Product` com mesmo nome/preço mas Ids diferentes são produtos diferentes. Isso contrasta com **Value Objects**, onde igualdade é por valor (dois `Money(100, "BRL")` são iguais).

**`src/BuildingBlocks/OrderFlow.SharedKernel/IDomainEvent.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
```

> 🔬 **Nota de Engenharia — Interface mínima, poder máximo**
>
> Esta interface tem **uma única propriedade**. Isso é intencional — é o **Interface Segregation Principle (ISP)** do SOLID na prática. A interface define *o contrato mínimo* que todo evento de domínio deve cumprir: "quando aconteceu?". Metadata adicional (quem causou, correlation id) será adicionada por enrichers, não pela interface. Se amanhã precisar de `EventId` ou `CausedBy`, é melhor criar `IDomainEventEnvelope` do que poluir a interface base — isso evita recompilação de todos os eventos existentes.
>
> **Por que `DateTime` e não `DateTimeOffset`?** Em sistemas distribuídos, `DateTimeOffset` preserva timezone. Para o OrderFlow (que usa `DateTime.UtcNow` consistentemente), `DateTime` em UTC é suficiente e mais leve. Se o sistema tivesse múltiplos fusos horários como input, `DateTimeOffset` seria a escolha correta.

> 💡 **Princípio YAGNI — "You Aren't Gonna Need It"**
>
> YAGNI é um princípio de engenharia de software que diz: **não implemente algo até que você realmente precise**. O nome vem do inglês *"You Aren't Gonna Need It"* (Você Não Vai Precisar Disso). A ideia é simples: é tentador antecipar funcionalidades futuras ("e se um dia precisarmos de X?"), mas isso gera código que ninguém usa, aumenta a complexidade, e dificulta mudanças — porque agora você mantém código *real* para um cenário *imaginário*. O custo de manter código desnecessário é sempre maior que o custo de adicioná-lo quando realmente for preciso. Na prática: resolva o problema de **hoje** com a solução mais simples possível. Quando o problema de **amanhã** chegar, você terá mais contexto para resolvê-lo da forma certa.

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

> 🔬 **Nota de Engenharia — Herança proposital**
>
> `AuditableEntity : Entity` adiciona **apenas** auditoria temporal. Nem toda entidade precisa de `CreatedAt`/`UpdatedAt` — por isso a separação. Na Fase 02, `Order` herdará de `Entity` diretamente (via `AggregateRoot`), enquanto `Product` aqui herda de `AuditableEntity`.
>
> ```csharp
> public DateTime CreatedAt { get; protected init; }  // Setado uma vez, nunca muda
> public DateTime? UpdatedAt { get; protected set; }   // Null = nunca atualizado
> ```
> **`protected init` vs `protected set`:** `CreatedAt` é imutável após criação (a data de criação nunca muda), por isso `init`. `UpdatedAt` muda a cada operação, por isso `set`. O `?` (nullable) indica que pode ser null — um objeto recém-criado nunca foi atualizado.
>
> 🏛️ **Decisão arquitetural:** Auditoria no domínio vs interceptor. Aqui, `SetUpdated()` é chamado explicitamente nos métodos de mutação. Alternativa: usar um `SaveChangesInterceptor` do EF Core que detecta `Modified` entries automaticamente (mostrado no Aprofundamento Sênior). O trade-off: explícito é mais visível e testável; interceptor é menos verboso mas "mágico".

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

> 🏛️ **Nota Arquitetural — Repository genérico: herói ou vilão?**
>
> **Decisão:** Um `IRepository<T>` genérico define operações CRUD comuns. Repositórios específicos (`IProductRepository`) herdam dele e adicionam queries de domínio.
>
> **O debate:** Muitos arquitetos criticam repositórios genéricos porque expõem operações que nem toda entidade deveria ter (ex: `Remove` em entidades que usam soft delete). A alternativa é **não ter** genérico e definir apenas as operações relevantes em cada repositório.
>
> **Por que usamos aqui:** No Catalog, todas as entidades compartilham CRUD. O genérico elimina boilerplate. Se uma entidade específica não deveria ter `Remove`, a implementação pode lançar `NotSupportedException` — mas isso viola o Liskov Substitution Principle. Uma abordagem mais limpa seria interfaces menores: `IReadRepository<T>`, `IWriteRepository<T>`.
>
> **Constraint `where T : Entity`:** Garante que somente classes que herdam de `Entity` podem ser repositórios. Isso é **type safety em nível de design** — impossível criar `IRepository<string>`.
>
> 🔬 **Engenharia — Sync vs Async nos métodos:**
> - `AddAsync` é async porque EF Core pode gerar o Id via banco (sequences). Para Guid local, seria síncrono, mas a interface prevê outros bancos.
> - `Update` e `Remove` são **síncronos** — EF Core apenas marca a entidade como `Modified`/`Deleted` no change tracker. A operação real no banco acontece no `SaveChangesAsync`.

**`src/BuildingBlocks/OrderFlow.SharedKernel/IUnitOfWork.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

> 🏛️ **Nota Arquitetural — Unit of Work: quem controla a transação?**
>
> **O padrão:** Unit of Work agrupa múltiplas operações de repositório em uma única transação. Em vez de cada repositório fazer `SaveChanges`, o **service** coordena: "add Product, update Category, depois salva tudo atomicamente".
>
> **Por que interface e não classe concreta?** A Application Layer precisa chamar `SaveChangesAsync` mas **não deve conhecer** EF Core. A interface permite que o domínio/aplicação orquestre transações sem depender da infraestrutura. Na prática, `CatalogDbContext` implementa `IUnitOfWork` — o `SaveChangesAsync` do DbContext **já é** um Unit of Work nativo.
>
> **Retorno `Task<int>`:** O `int` indica quantas linhas foram afetadas no banco. Útil para assertions em testes: `var affected = await uow.SaveChangesAsync(); affected.Should().Be(1);`

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

> 🏛️ **Nota Arquitetural — Factory Method: por que não usar `new Category()`?**
>
> **Decisão:** O construtor é `private Category() { }` e a criação passa pelo **static factory method** `Category.Create(...)`.
>
> **Raciocínio:** O construtor parameterless existe exclusivamente para o EF Core materializar objetos do banco. Ele nunca deve ser chamado no código de negócio. O factory method garante que **toda** criação passa por validação (`ThrowIfNullOrWhiteSpace`). Se o construtor fosse público, seria possível criar `new Category()` com `Name = ""` — uma entidade inválida.
>
> **Alternativa:** Construtor com parâmetros obrigatórios — funciona, mas factory methods são mais expressivos (`Category.Create` comunica intenção melhor que `new Category`) e permitem retornar `Result<Category>` no futuro sem quebrar a API.
>
> 🔬 **Nota de Engenharia — Padrões aplicados linha a linha:**
>
> ```csharp
> public sealed class Category : AuditableEntity
> ```
> **`sealed`** impede herança. Regra pragmática: sele todas as classes a menos que você **projete** para extensão. Isso permite ao JIT otimizar chamadas virtuais (devirtualization) e comunica: "esta classe é um ponto final".
>
> ```csharp
> public string Name { get; private set; } = string.Empty;
> ```
> **`private set`** permite mutação apenas de dentro da classe. O `= string.Empty` é o **null safety pattern** — evita `NullReferenceException` sem usar nullable reference (`string?`). Propriedade que **deve** ter valor usa `string`; que **pode** ser vazia usa `string?` (como `Description`).
>
> ```csharp
> private readonly List<Product> _products = [];
> public IReadOnlyCollection<Product> Products => _products.AsReadOnly();
> ```
> **Encapsulamento de coleção** — mesmo padrão da `Entity.DomainEvents`. A coleção de produtos é manipulada internamente pelo EF Core (que popula via reflection) e exposta como somente-leitura. **Nunca exponha `List<T>` diretamente** — qualquer consumidor poderia chamar `.Add()` ou `.Clear()` sem passar pelas regras de negócio.
>
> ```csharp
> ArgumentException.ThrowIfNullOrWhiteSpace(name);
> ```
> **Guard clause** do .NET 8+ — substitui `if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(...)`. Mais conciso e padronizado. Lança `ArgumentException` automaticamente com o nome do parâmetro. Prefira sempre os métodos `ThrowIf*` nativos.

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
            throw new ArgumentException("O preço não pode ser negativo.", nameof(price));

        if (stockQuantity < 0)
            throw new ArgumentException("A quantidade em estoque não pode ser negativa.", nameof(stockQuantity));

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
        if (price < 0)
            throw new ArgumentException("O preço não pode ser negativo.", nameof(price));
        if (stockQuantity < 0)
            throw new ArgumentException("A quantidade em estoque não pode ser negativa.", nameof(stockQuantity));

        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        StockQuantity = stockQuantity;
        SetUpdated();
    }

    public void ChangeCategory(Guid newCategoryId)
    {
        if (newCategoryId == Guid.Empty)
            throw new ArgumentException("O ID da categoria não pode ser vazio.", nameof(newCategoryId));

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
                $"Estoque insuficiente. Disponível: {StockQuantity}, Solicitado: {quantity}");

        StockQuantity -= quantity;
        SetUpdated();
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade deve ser positiva.", nameof(quantity));

        StockQuantity += quantity;
        SetUpdated();
    }
}
```

> 🏛️ **Nota Arquitetural — Lógica de estoque: domínio vs serviço?**
>
> **Decisão:** `DecreaseStock` e `IncreaseStock` vivem na **entidade**, não no service.
>
> **Por que no domínio:** Estoque é uma **regra de invariante** — "nunca pode ficar negativo". Se essa validação estivesse no `ProductService`, outro service (ex: `OrderService` na Fase 02) poderia esquecer de validar e gerar estoque negativo. Colocando na entidade, **é impossível violar a regra** independente de quem chama.
>
> **Trade-off:** Entidades com muita lógica podem ficar pesadas (God Object). A regra pragmática: se a operação modifica **apenas campos da própria entidade**, pertence à entidade. Se coordena **múltiplas entidades** ou **serviços externos**, pertence ao Application Service ou Domain Service.
>
> 🔬 **Nota de Engenharia — Anatomia da entidade mais rica**
>
> ```csharp
> public string Sku { get; private set; } = string.Empty;
> ```
> SKU (Stock Keeping Unit) é um identificador de negócio. Diferente do `Id` (identidade técnica), o SKU vem do mundo real. Ter ambos é comum em DDD: `Id` para o banco, `SKU` para o usuário.
>
> ```csharp
> public Guid CategoryId { get; private set; }
> public Category? Category { get; private set; }
> ```
> **Duas propriedades para uma relação** — isso é o padrão EF Core para **foreign key + navigation property**. `CategoryId` é o FK (Guid, sempre preenchido). `Category?` é o navigation property (nullable porque pode não ser carregado via `Include`). **Nunca confunda nullable do navigation com nullable da relação** — a relação é obrigatória, mas o EF Core pode não ter carregado o objeto.
>
> ```csharp
> Sku = sku.Trim().ToUpperInvariant(),
> ```
> **Normalização no ponto de entrada** — SKUs são case-insensitive no negócio (ABC-001 = abc-001). `ToUpperInvariant()` garante consistência no banco. `Trim()` remove espaços acidentais. Isso evita bugs de comparação e duplicatas.
>
> ```csharp
> public bool HasSufficientStock(int quantity) => StockQuantity >= quantity;
> ```
> **Query method puro** — não muda estado, apenas consulta. Separar a verificação (`Has*`) da ação (`Decrease*`) segue o **Command-Query Separation (CQS)**: métodos ou retornam dados ou mudam estado, nunca ambos. O service pode chamar `HasSufficientStock` para exibir UI antes de tentar `DecreaseStock`.
>
> ```csharp
> if (!HasSufficientStock(quantity))
>     throw new InvalidOperationException(...);
> StockQuantity -= quantity;
> ```
> **Guard clause + operação atômica.** Em cenários de concorrência (dois pedidos simultâneos), essa validação em memória **não é suficiente** — precisaria de concurrency token ou lock pessimista no banco. Isso será abordado na Fase 03 com `RowVersion`.

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

> 🔬 **Nota de Engenharia — Retorno por Tupla vs DTO dedicado**
>
> ```csharp
> Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(...)
> ```
> O retorno é uma **tupla nomeada** `(Items, TotalCount)`. Alternativa seria um `SearchResult<T>` dedicado. Tupla é boa para retornos de 2-3 campos; acima disso, crie um tipo. Tuplas nomeadas são valores (struct), sem alocação no heap — microscopicamente mais eficiente.
>
> **`CancellationToken ct = default`** aparece em **todo método async**. Isso permite que o caller cancele a operação (ex: usuário fecha a aba do browser → controller propaga cancelamento → repository aborta a query). O `= default` é conveniência — se não passar, usa `CancellationToken.None`.

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

> 🏛️ **Nota Arquitetural — Repositórios específicos: projetando para o domínio**
>
> Compare `IProductRepository` (5 métodos extras) com `ICategoryRepository` (2 métodos). O repositório reflete a **complexidade do domínio** — produtos têm busca paginada, filtro por preço, lookup por SKU. Categorias são mais simples.
>
> **Regra de design:** O repositório específico vive na **Domain Layer** porque define queries que o domínio precisa. A implementação vive na **Infrastructure Layer** porque usa EF Core. Isso é a **Regra de Dependência** da Clean Architecture: domínio define o contrato, infraestrutura implementa.

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

> 🏛️ **Nota Arquitetural — Records como DTOs: imutabilidade por design**
>
> **Decisão:** Todos os DTOs são `sealed record` (não `class`).
>
> **Por que record?** Records são **imutáveis por padrão** — após criação, os campos não mudam. Isso é perfeito para DTOs que fluem da API para o Service e vice-versa. Records geram automaticamente: `Equals`, `GetHashCode`, `ToString`, desconstrução, e `with` expressions. Isso elimina boilerplate e evita bugs de igualdade.
>
> **Separação de concerns nos DTOs:**
> - `ProductDto` = **output** (inclui `Id`, `CreatedAt`, campo computado `CategoryName`)
> - `CreateProductRequest` = **input** (sem `Id` — o servidor gera; sem `IsActive` — sempre começa ativo)
> - `UpdateProductRequest` = **input parcial** (sem `Sku` — SKU é imutável após criação; sem `CategoryId` — usa `ChangeCategory` separado)
> - `ProductSearchRequest` = **query params** (todos opcionais com `= null`/defaults, para binding via `[FromQuery]`)
>
> 🔬 **Engenharia — Positional record syntax:**
> ```csharp
> public sealed record ProductDto(Guid Id, string Name, ...);
> ```
> Essa é a **positional syntax** — o compilador gera um construtor, propriedades `init`, e desconstrução. É equivalente a escrever ~30 linhas de classe com propriedades. O `sealed` nos records impede herança e habilita otimizações do compilador.

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

> 🔬 **Nota de Engenharia — Generic record com computed properties**
>
> ```csharp
> public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
> ```
> `PagedResult<T>` é um **record genérico** — funciona com `ProductDto`, `CategoryDto`, ou qualquer tipo. O `T` é resolvido em compile-time, sem boxing ou reflection.
>
> ```csharp
> public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
> ```
> **Computed property (expression-bodied)** — recalculada a cada acesso. O cast `(double)` é necessário porque divisão inteira trunca: `7 / 3 = 2`, mas `7.0 / 3 = 2.33`, e `Ceiling(2.33) = 3`. Sem o cast, uma página ficaria faltando.
>
> **Por que `IReadOnlyList<T>` e não `IEnumerable<T>`?** `IReadOnlyList` garante acesso por índice (`Items[0]`) e `Count` sem materializar. `IEnumerable` permitiria lazy evaluation — perigoso em DTOs serializados para JSON, pois a query ao banco poderia já ter sido fechada (connection disposed).

> 💡 **Em palavras simples — O que o PagedResult faz e para que serve?**
>
> Imagine que sua loja tem 10.000 produtos. Quando o usuário abre a página de produtos, você **não quer** devolver todos de uma vez — seria lento, gastaria memória e a tela ficaria impossível de navegar. A solução é **paginação**: devolver os produtos em "páginas", tipo 20 por vez.
>
> O `PagedResult<T>` é justamente o "envelope" que carrega uma página de resultados. Ele guarda:
> - **`Items`** — a lista de itens daquela página (ex: os 20 produtos da página 3)
> - **`TotalCount`** — o total de itens que existem no banco (ex: 10.000)
> - **`Page`** — em qual página você está (ex: 3)
> - **`PageSize`** — quantos itens cabem por página (ex: 20)
>
> E calcula automaticamente:
> - **`TotalPages`** — quantas páginas existem no total (10.000 ÷ 20 = 500 páginas)
> - **`HasPreviousPage`** — se existe página anterior (página 3 → sim)
> - **`HasNextPage`** — se existe próxima página (página 3 de 500 → sim)
>
> O `<T>` (genérico) significa que funciona com **qualquer tipo**: `PagedResult<ProductDto>` para produtos, `PagedResult<CategoryDto>` para categorias — o mesmo código serve para todos. É como um envelope padrão dos Correios: funciona para qualquer carta, não precisa de um envelope diferente para cada destinatário.

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
            .NotEmpty().WithMessage("O nome do produto é obrigatório.")
            .MaximumLength(200).WithMessage("O nome do produto não pode exceder 200 caracteres.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("O SKU é obrigatório.")
            .MaximumLength(50).WithMessage("O SKU não pode exceder 50 caracteres.")
            .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("O SKU deve conter apenas letras, números e hífens.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("O preço deve ser zero ou positivo.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("A quantidade em estoque deve ser zero ou positiva.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("A categoria é obrigatória.");
    }
}
```

> 🏛️ **Nota Arquitetural — Validação em camadas: onde validar?**
>
> O OrderFlow valida em **dois níveis**:
> 1. **FluentValidation na Application Layer** — valida formato, limites, campos obrigatórios (regras "sintáticas")
> 2. **Guard clauses na Entity** — valida invariantes de negócio (regras "semânticas")
>
> **Por que dois níveis?** O Validator verifica *input do usuário* antes de chegar à entidade. A entidade valida suas *invariantes* independente da origem. Se amanhã a criação vier de um message broker (não de um controller), a entidade ainda se protege.
>
> **Alternativa:** Validação só na entidade — funciona, mas perde mensagens de erro amigáveis e coleção de erros (FluentValidation retorna **todos** os erros de uma vez; exceptions retornam um por vez).
>
> 🔬 **Engenharia — FluentValidation fluent API:**
> ```csharp
> RuleFor(x => x.Sku)
>     .NotEmpty()
>     .MaximumLength(50)
>     .Matches(@"^[A-Za-z0-9\-]+$")
> ```
> **Encadeamento fluente (builder pattern)** — cada método retorna o builder, permitindo chamadas em cadeia. A **regex** `^[A-Za-z0-9\-]+$` valida: `^` início, `[A-Za-z0-9\-]+` um ou mais caracteres alfanuméricos ou hífen, `$` fim. Isso impede caracteres especiais que quebrariam URLs (`/api/products/sku/ABC-001`).

**src/Services/Catalog/OrderFlow.Catalog.Application/Validators/CreateCategoryValidator.cs**

```csharp
using FluentValidation;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Application.Validators;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da categoria é obrigatório.")
            .MaximumLength(100).WithMessage("O nome da categoria não pode exceder 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("A descrição não pode exceder 500 caracteres.")
            .When(x => x.Description is not null);
    }
}
```

> 🔬 **Engenharia — Validação condicional:**
> ```csharp
> .When(x => x.Description is not null)
> ```
> O `.When()` aplica a regra **somente** se a condição for verdadeira. Sem ele, `MaximumLength` falharia em `null` (ou validaria desnecessariamente). O `is not null` é **pattern matching** do C# — equivale a `!= null` mas mais idiomático e null-safe.

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

> 🏛️ **Nota Arquitetural — Service Interface: a fronteira da Application Layer**
>
> **Decisão:** Controllers dependem de `IProductService`, nunca de `ProductService` diretamente.
>
> **Por quê?** Três razões:
> 1. **Testabilidade** — testes do controller usam mock de `IProductService`, sem precisar de banco real
> 2. **Inversão de dependência** — a API Layer depende de uma abstração (interface), não da implementação
> 3. **Substituibilidade** — pode trocar para `CachedProductService` (decorator) sem alterar controllers
>
> **Retornos `Task<ProductDto?>` vs `Task<ProductDto>`:** Nullable (`?`) em Gets indica "pode não existir". Non-nullable em Create/Update indica "sempre retorna" — se falhar, lança exception. Esse contrato comunica sem documentação extra.

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

public sealed partial class ProductService(
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
            ?? throw new KeyNotFoundException($"Categoria com ID '{request.CategoryId}' não foi encontrada.");

        if (await productRepository.SkuExistsAsync(request.Sku, ct))
            throw new InvalidOperationException($"Já existe um produto com o SKU '{request.Sku}'.");

        var product = Product.Create(
            request.Name,
            request.Sku,
            request.Price,
            request.StockQuantity,
            request.CategoryId,
            request.Description);

        await productRepository.AddAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);

        LogProductCreated(logger, product.Id, product.Name);

        return MapToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(
        Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var product = await productRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Produto com ID '{id}' não foi encontrado.");

        product.Update(request.Name, request.Description, request.Price, request.StockQuantity);

        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(ct);

        LogProductUpdated(logger, product.Id);

        return MapToDto(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Produto com ID '{id}' não foi encontrado.");

        product.Deactivate();
        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(ct);

        LogProductDeactivated(logger, product.Id);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Produto criado: {ProductId} - {ProductName}")]
    private static partial void LogProductCreated(ILogger logger, Guid productId, string productName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Produto atualizado: {ProductId}")]
    private static partial void LogProductUpdated(ILogger logger, Guid productId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Produto desativado: {ProductId}")]
    private static partial void LogProductDeactivated(ILogger logger, Guid productId);
}
```

> 🏛️ **Nota Arquitetural — Application Service: o orquestrador**
>
> **Papel do ProductService:** Não contém lógica de negócio — ele **orquestra**: valida input → busca entidades → chama métodos do domínio → persiste → retorna DTO. Se a lógica de estoque estiver aqui em vez da entidade, é sinal de **Anemic Domain Model** (anti-pattern).
>
> **Decisão — Exceptions como controle de fluxo:**
> O service lança `KeyNotFoundException`, `ValidationException`, `InvalidOperationException` que são capturadas pelo `GlobalExceptionHandler`. Alternativa: **Result Pattern** (`Result<ProductDto>`, `Result.Failure("not found")`), que evita exceptions para fluxos esperados (não encontrado não é "excepcional"). O OrderFlow evolui para Result Pattern na Fase 05.
>
> 🔬 **Nota de Engenharia — Dissecando o fluxo do `CreateAsync`**
>
> ```csharp
> public sealed partial class ProductService(
>     IProductRepository productRepository,
>     ICategoryRepository categoryRepository,
>     IUnitOfWork unitOfWork,
>     IValidator<CreateProductRequest> createValidator,
>     ...
> ) : IProductService
> ```
> **Primary constructor (C# 12) + `partial`** — os parâmetros do construtor viram campos implícitos da classe. Equivale a 6 campos `private readonly` + construtor com assignments. Mais conciso sem perder funcionalidade. Cada parâmetro é injetado pelo DI container. O `partial` é necessário para que os métodos `[LoggerMessage]` (source-generated) sejam gerados em compile-time.
>
> ```csharp
> var validation = await createValidator.ValidateAsync(request, ct);
> if (!validation.IsValid) throw new ValidationException(validation.Errors);
> ```
> **Validate-first** — falha rápido antes de qualquer I/O (banco). Se o request é inválido, nem busca a categoria. Isso segue o princípio **fail fast**: quanto antes falhar, menos recurso desperdiça.
>
> ```csharp
> var category = await categoryRepository.GetByIdAsync(request.CategoryId, ct)
>     ?? throw new KeyNotFoundException(...);
> ```
> **Null-coalescing throw** — o `??` verifica nulidade e lança em uma linha. Sem isso, seriam 3 linhas (if/null/throw). Verifica existência da categoria **antes** de criar o produto — integridade referencial no nível da aplicação.
>
> ```csharp
> await productRepository.AddAsync(product, ct);
> await unitOfWork.SaveChangesAsync(ct);
> ```
> **Duas operações, uma transação** — `AddAsync` marca no change tracker, `SaveChangesAsync` persiste. Se `SaveChanges` falhar (ex: unique constraint no SKU), nada é salvo. Isso é a **atomicidade** do Unit of Work.
>
> ```csharp
> LogProductCreated(logger, product.Id, product.Name);
> ```
> **Structured logging via source generation** — `LogProductCreated` é gerado em compile-time pelo atributo `[LoggerMessage]`. O template `"Product created: {ProductId} - {ProductName}"` captura valores como propriedades estruturadas no Serilog, permitindo filtrar no Seq: `ProductId = "abc-123"`. Diferente de `$"Product created: {product.Id}"` (interpolação de string), o Serilog recebe campos tipados. A source generation evita boxing de parâmetros e alocações desnecessárias em runtime — e satisfaz os warnings CA1848/CA1873 quando `TreatWarningsAsErrors=true`.
>
> ```csharp
> product.Deactivate();           // NÃO context.Products.Remove(product)
> productRepository.Update(product);  // marca como Modified, não Deleted
> ```
> **Soft delete** — o `DeleteAsync` não deleta nada! Chama `Deactivate()` (seta `IsActive = false`) e salva. Combinado com o `HasQueryFilter` do EF Core, o produto "desaparece" das queries normais mas permanece no banco para auditoria e relatórios.

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

> 🏛️ **Nota Arquitetural — DbContext como Unit of Work nativo**
>
> ```csharp
> public sealed class CatalogDbContext(...) : DbContext(options), IUnitOfWork
> ```
> **Decisão:** O DbContext implementa `IUnitOfWork`. Isso evita criar uma classe wrapper separada, porque o `SaveChangesAsync` do EF Core **já é** transacional — ou salva tudo ou nada.
>
> **Alternativa:** Uma classe `UnitOfWork` que envolve o DbContext e expõe repositórios como propriedades (`uow.Products.Add(...)`, `uow.Commit()`). Mais explícito mas mais boilerplate. Para o Catalog, a abordagem direta é suficiente.
>
> ```csharp
> public DbSet<Product> Products => Set<Product>();
> ```
> **Expression-bodied property** que retorna `Set<Product>()`. Diferente de `{ get; set; }` — não há backing field. Cada acesso chama `Set<T>()`, que retorna o DbSet do cache interno. Isso é equivalente a `public DbSet<Product> Products { get; set; } = null!;` mas sem o setter público.
>
> ```csharp
> modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
> ```
> **Convention over Configuration** — busca automaticamente todas as classes que implementam `IEntityTypeConfiguration<T>` no assembly. Assim, adicionar uma nova entidade requer apenas criar a classe de configuração, sem alterar o DbContext. Isso é o **Open/Closed Principle**: aberto para extensão (nova config), fechado para modificação (DbContext). O `HasQueryFilter` adiciona automaticamente um `WHERE IsActive = 1` em **toda** query EF Core para essa entidade. Assim, produtos desativados ficam "invisíveis" sem que cada query precise lembrar de filtrar. 
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

> 🏛️ **Nota Arquitetural — Fluent API vs Data Annotations**
>
> **Decisão:** Toda configuração de banco está na Fluent API (arquivos `*Configuration.cs`), não em atributos na entidade (`[MaxLength(200)]`, `[Required]`).
>
> **Por quê?** Atributos no domínio criariam dependência do `System.ComponentModel.DataAnnotations` (infraestrutura) na Domain Layer — viola a Regra de Dependência da Clean Architecture. A Fluent API mantém configuração de banco **onde ela pertence**: na Infrastructure.
>
> 🔬 **Nota de Engenharia — Índices e suas estratégias**
>
> ```csharp
> builder.HasIndex(p => p.Sku).IsUnique();
> ```
> **Unique index** — o banco garante unicidade do SKU. Mesmo que o application service verifique `SkuExistsAsync` antes, race conditions podem permitir duplicatas simultâneas. O unique index é a **última linha de defesa** — se dois requests passarem pela validação juntos, o segundo falha no banco com `DbUpdateException`.
>
> ```csharp
> builder.HasIndex(p => new { p.CategoryId, p.Name });
> ```
> **Composite index** — otimiza queries que filtram por `CategoryId` **e** `Name` simultaneamente. A ordem importa: `CategoryId` primeiro porque é mais seletivo (filtra uma categoria), `Name` depois (filtra dentro dela). Essa é a query mais comum: "produtos da categoria X cujo nome contém Y".
>
> ```csharp
> builder.HasIndex(p => p.Price);
> ```
> **Single column index** — otimiza range queries (`WHERE Price >= 100 AND Price <= 500`). Sem esse índice, o banco faria table scan.
>
> ```csharp
> builder.Property(p => p.Price).HasPrecision(18, 2);
> ```
> **`decimal(18, 2)`** — 18 dígitos no total, 2 após a vírgula. Para dinheiro, **nunca use `float` ou `double`** — eles causam erros de arredondamento. `0.1 + 0.2 != 0.3` em floating point, mas funciona corretamente com `decimal`.
>
> ```csharp
> .OnDelete(DeleteBehavior.Restrict);
> ```
> **Restrict** impede deletar uma categoria que tem produtos. Alternativas: `Cascade` (deleta produtos junto — perigoso), `SetNull` (orphan products — inconsistente). `Restrict` é o mais seguro: força o usuário a mover/deletar produtos antes de remover a categoria.

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

> 🔬 **Engenharia — Unique index no nome da categoria:**
> ```csharp
> builder.HasIndex(c => c.Name).IsUnique();
> ```
> Garante que não existam duas categorias com o mesmo nome. Compare com o Product, que tem unique no **SKU** mas não no **Name** (dois produtos podem ter o mesmo nome, desde que SKUs diferentes). Essas decisões refletem **regras de negócio**: categorias são identificadas pelo nome, produtos pelo SKU.

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
        var normalizedSku = sku.Trim().ToUpperInvariant();
        return await context.Products
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Sku == normalizedSku, ct);
    }
}
```

> 🏛️ **Nota Arquitetural — Repository como adaptador**
>
> Na Clean Architecture, o repository é um **Adapter** (padrão Ports & Adapters): a interface (port) está no Domain, a implementação (adapter) está na Infrastructure. Se amanhã trocar EF Core por Dapper para queries de leitura, **apenas esta classe muda** — domínio e application ficam intactos.
>
> 🔬 **Nota de Engenharia — EF Core query patterns linha a linha**
>
> ```csharp
> return await context.Products
>     .Include(p => p.Category)
>     .FirstOrDefaultAsync(p => p.Id == id, ct);
> ```
> **`Include` = JOIN no SQL.** Sem ele, `product.Category` seria `null` (lazy loading está desabilitado por padrão no EF Core). `FirstOrDefaultAsync` retorna o primeiro match ou `null` — traduzido para `SELECT TOP 1 ... WHERE Id = @id`.
>
> **`GetByIdAsync` sem `AsNoTracking`** — intencional! Queries de escrita (Get → Update → Save) precisam do change tracker ativo para detectar mudanças. Queries de leitura (`GetAllAsync`, `SearchAsync`) usam `AsNoTracking` porque não vão modificar os objetos — economiza memória e CPU.
>
> ```csharp
> var query = context.Products
>     .Include(p => p.Category)
>     .AsNoTracking()
>     .AsQueryable();
> ```
> **Query composicional** — o `IQueryable` é como um **SQL builder**. Cada `.Where()` adiciona condição mas **não executa**. A query real vai ao banco apenas no `ToListAsync()` ou `CountAsync()`. Isso é **deferred execution** — permite compor filtros dinamicamente sem múltiplas queries.
>
> ```csharp
> if (!string.IsNullOrWhiteSpace(searchTerm))
>     query = query.Where(p => p.Name.Contains(searchTerm) || p.Sku.Contains(searchTerm));
> ```
> **Filtro condicional** — o `if` adiciona `WHERE` apenas se o parâmetro foi fornecido. No SQL gerado, isso vira `WHERE Name LIKE '%termo%' OR Sku LIKE '%termo%'`. **Cuidado com performance:** `Contains` gera `LIKE '%...%'` que **não usa índice** (full table scan). Para busca textual eficiente, considere Full-Text Search do SQL Server (Fase 05).
>
> ```csharp
> .Skip((page - 1) * pageSize)
> .Take(pageSize)
> ```
> **Paginação offset-based** — simples de implementar mas com trade-off: páginas altas são lentas (`OFFSET 10000` ainda precisa "pular" 10000 rows). Para datasets enormes, **keyset pagination** (`WHERE Id > @lastId ORDER BY Id TAKE 20`) é mais eficiente. Para o Catalog com ~milhares de produtos, offset é adequado.
>
> ```csharp
> var normalizedSku = sku.Trim().ToUpperInvariant();
> .IgnoreQueryFilters()
> .AnyAsync(p => p.Sku == normalizedSku, ct);
> ```
> **`IgnoreQueryFilters()`** aqui é proposital — ao verificar se um SKU existe, precisa checar **inclusive produtos inativos**. Se não ignorasse, um produto desativado com SKU "ABC-001" ficaria invisível, e a criação de outro "ABC-001" passaria na validação mas falharia no unique index do banco. A **normalização** (`Trim().ToUpperInvariant()`) garante que `"abc-001"`, `"ABC-001"` e `" ABC-001 "` sejam tratados como o mesmo SKU — essencial para consistência com o `Product.Create` que armazena o SKU normalizado. Sem isso, o InMemory provider (case-sensitive) não detectaria duplicatas que o SQL Server (case-insensitive por padrão) detectaria.

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

> 🏛️ **Nota Arquitetural — Composition Root: onde tudo se conecta**
>
> Este arquivo é o **Composition Root** do microserviço — o único lugar que conhece **todas as camadas**. Ele registra quem implementa o quê. O `HostingExtensions.AddApiServices()` chama `AddCatalogInfrastructure()` e pronto — não precisa saber de `ProductRepository` ou `CatalogDbContext`.
>
> **Por que extension method em `IServiceCollection`?** Encapsula toda a configuração do módulo em um método fluente. Se o Catalog crescer, pode dividir em `AddCatalogRepositories()`, `AddCatalogServices()`, etc. O mesmo padrão é aplicado no `HostingExtensions` para manter o `Program.cs` limpo e legível.
>
> 🔬 **Nota de Engenharia — Lifetimes e suas implicações**
>
> ```csharp
> services.AddDbContext<CatalogDbContext>(...)  // Scoped por padrão
> services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CatalogDbContext>());
> services.AddScoped<IProductRepository, ProductRepository>();
> services.AddScoped<IProductService, ProductService>();
> ```
>
> **Tudo é `Scoped`** — uma instância por request HTTP. Isso é crucial:
> - **DbContext Scoped** = todos os repositórios e services de um request compartilham o **mesmo** DbContext. Sem isso, `productRepository.Add()` e `unitOfWork.SaveChanges()` usariam contextos diferentes e nada seria salvo.
> - **`AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CatalogDbContext>())`** — factory registration. Em vez de criar novo `IUnitOfWork`, pega o **mesmo `CatalogDbContext`** já registrado. Assim, `IUnitOfWork` e `CatalogDbContext` são a mesma instância no request.
>
> | Lifetime | Instância por... | Uso típico |
> |---|---|---|
> | **Transient** | Cada injeção | Stateless, leve (validators) |
> | **Scoped** | Request HTTP | DbContext, repos, services |
> | **Singleton** | Aplicação inteira | Cache, HttpClient, configuração |
>
> ```csharp
> sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), ...)
> ```
> **Resiliência built-in** — se o SQL Server estiver temporariamente indisponível (reinício, failover), o EF Core tenta novamente até 3x com backoff. Sem isso, uma query falharia imediatamente em transient errors.
>
> ```csharp
> services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();
> ```
> **Assembly scanning** — FluentValidation encontra automaticamente todos os validators no assembly. Assim como `ApplyConfigurationsFromAssembly` do EF Core, segue o Open/Closed Principle.

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

> 🏛️ **Nota Arquitetural — Controller magro, Service gordo**
>
> **Decisão:** O controller não tem lógica — apenas roteia HTTP para Service e retorna status codes. Toda lógica vive no `ProductService`.
>
> **Por quê?** Controllers testáveis via unit test são possíveis, mas frágeis (dependem de `ControllerBase`, `HttpContext`). Services são POCO (Plain Old C# Objects) — fáceis de testar sem infraestrutura web. Se migrar de controller-based para Minimal APIs, apenas a camada de roteamento muda.
>
> 🔬 **Nota de Engenharia — REST conventions e binding**
>
> ```csharp
> [Route("api/v1/[controller]")]
> ```
> **Versionamento na URL** — `v1` permite que `v2` coexista sem quebrar clientes existentes. `[controller]` substitui pelo nome da classe sem "Controller": `ProductsController` → `api/v1/products`.
>
> ```csharp
> [HttpGet("{id:guid}")]
> public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
> ```
> **Route constraint `{id:guid}`** — rejeita requests com Id não-Guid antes de chegar ao method. `GET /api/v1/products/abc` retorna 404 sem executar código. O `CancellationToken ct` é injetado automaticamente pelo ASP.NET Core quando o client desconecta — propaga cancelamento pela stack inteira.
>
> ```csharp
> return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
> ```
> **HTTP 201 Created** com header `Location: /api/v1/products/{id}` — padrão REST para criação. O client sabe onde buscar o recurso criado. `nameof(GetById)` é compile-time safe — se renomear o método, o compilador avisa.
>
> ```csharp
> [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
> [ProducesResponseType(StatusCodes.Status404NotFound)]
> ```
> **Metadata para Swagger** — essas annotations geram a documentação OpenAPI automaticamente, mostrando quais status codes e tipos de resposta cada endpoint retorna.

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

> 🔬 **Engenharia — Controllers "espelhos":** Note como `CategoriesController` segue a **mesma estrutura** do `ProductsController` — mesmos verbos HTTP, mesmos patterns de retorno. Isso é consistência de API: o consumidor que aprendeu a usar Products já sabe usar Categories. APIs previsíveis reduzem a curva de aprendizado.

### 4.11 Catalog API — Global Exception Handling Middleware

**`src/Services/Catalog/OrderFlow.Catalog.Api/Middleware/GlobalExceptionHandler.cs`**

```csharp
using System.Net;
using System.Text.Json;
using FluentValidation;

namespace OrderFlow.Catalog.Api.Middleware;

public sealed partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
                    "Erro de Validação",
                    validationEx.Errors.Select(e => e.ErrorMessage).ToArray())),

            KeyNotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ErrorResponse("Não Encontrado", [notFoundEx.Message])),

            InvalidOperationException invalidOpEx => (
                HttpStatusCode.Conflict,
                new ErrorResponse("Violação de Regra de Negócio", [invalidOpEx.Message])),

            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse("Argumento Inválido", [argEx.Message])),

            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse("Erro Interno do Servidor", ["Ocorreu um erro inesperado."]))
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            LogUnhandledException(logger, exception, exception.Message);
        }
        else
        {
            LogHandledException(logger, exception, (int)statusCode, exception.Message);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);

        await context.Response.WriteAsync(json);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Exceção não tratada: {ExceptionMessage}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string exceptionMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exceção tratada: {StatusCode} - {ExceptionMessage}")]
    private static partial void LogHandledException(ILogger logger, Exception exception, int statusCode, string exceptionMessage);
}

public sealed record ErrorResponse(string Title, string[] Errors);
```

> 🏛️ **Nota Arquitetural — Middleware vs Exception Filter**
>
> **Decisão:** Tratamento global de exceções via `IMiddleware`, não via `IExceptionFilter` do MVC.
>
> **Diferença:** Middleware intercepta **qualquer** exceção no pipeline HTTP (inclusive de outros middlewares). Exception Filter intercepta apenas exceções de controllers/actions. Para uma API que pode ter exceções em autenticação, rate limiting, ou custom middleware, o middleware é mais abrangente.
>
> **Alternativa moderna (.NET 8+):** `IExceptionHandler` com `app.UseExceptionHandler()` — funciona como middleware mas com API mais limpa e suporte a `ProblemDetails` nativo. O OrderFlow evolui para isso na Fase 05.
>
> 🔬 **Nota de Engenharia — Pattern matching com switch expression**
>
> ```csharp
> var (statusCode, errorResponse) = exception switch
> {
>     ValidationException validationEx => (HttpStatusCode.BadRequest, ...),
>     KeyNotFoundException notFoundEx => (HttpStatusCode.NotFound, ...),
>     InvalidOperationException => (HttpStatusCode.Conflict, ...),
>     _ => (HttpStatusCode.InternalServerError, ...)
> };
> ```
> **Switch expression + desconstrução de tupla** — o C# resolve o tipo da exception e retorna status code + resposta em uma expressão. O `_` é o **discard pattern** (catch-all). A ordem importa: tipos mais específicos primeiro, genéricos depois.
>
> **Segurança importante:** Para `InternalServerError`, a mensagem genérica `"Ocorreu um erro inesperado."` **esconde detalhes internos**. Nunca exponha stack traces ou mensagens internas ao client — isso é information disclosure (OWASP). O log completo vai para o Serilog/Seq para debugging.
>
> ```csharp
> LogUnhandledException(logger, exception, exception.Message);
> ```
> Apenas erros 500 usam `LogError` (via `LogUnhandledException`). Erros 4xx usam `LogWarning` (via `LogHandledException`) — eles são **esperados** (input inválido, recurso não encontrado). Isso evita alertas falsos no monitoramento.
>
> **Por que `LoggerMessage` source-generated?** O atributo `[LoggerMessage]` gera código em compile-time que evita boxing de parâmetros e alocações de string em runtime. Com `TreatWarningsAsErrors=true`, os warnings CA1848/CA1873 bloqueiam o build se usar `logger.LogInformation()` diretamente — a classe precisa ser `partial` para que o source generator funcione.
>
> **Por que `JsonSerializerOptions` estático?** Instanciar `new JsonSerializerOptions` a cada request é custoso (reflection + cache interno). O campo `private static readonly` é criado uma vez e reutilizado. O warning CA1869 bloqueia o build se instanciar inline.

### 4.12 Catalog API — Extension Methods de Hosting

**`src/Services/Catalog/OrderFlow.Catalog.Api/Extensions/HostingExtensions.cs`**

```csharp
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Catalog.Api.Middleware;
using OrderFlow.Catalog.Infrastructure;
using OrderFlow.Catalog.Infrastructure.Data;
using Serilog;

namespace OrderFlow.Catalog.Api.Extensions;

internal static class HostingExtensions
{
    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider",
        Justification = "Serilog gerencia formatação de strings internamente — falso positivo")]
    internal static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.Seq(builder.Configuration["Seq:Url"] ?? "http://localhost:5341");
        });

        return builder;
    }

    internal static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "OrderFlow Catalog API",
                Version = "v1",
                Description = "API para gerenciamento do catálogo de produtos e categorias."
            });
        });

        builder.Services.AddCatalogInfrastructure(builder.Configuration);
        builder.Services.AddTransient<GlobalExceptionHandler>();

        builder.Services.AddHealthChecks()
            .AddSqlServer(
                builder.Configuration.GetConnectionString("CatalogDb")!,
                name: "sqlserver",
                tags: ["db", "ready"]);

        return builder;
    }

    internal static WebApplication ConfigurePipeline(this WebApplication app)
    {
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
            Predicate = _ => false
        });

        return app;
    }

    internal static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
```

> 🏛️ **Nota Arquitetural — Extension Methods para um Program.cs limpo**
>
> O `HostingExtensions` extrai toda a configuração do `Program.cs` para métodos de extensão, seguindo o mesmo padrão que o `AddCatalogInfrastructure` na camada Infrastructure. Cada método tem **uma responsabilidade**:
> - **`AddSerilog()`** — configura o pipeline de logging
> - **`AddApiServices()`** — registra controllers, Swagger, DI, health checks
> - **`ConfigurePipeline()`** — configura middlewares e endpoints
> - **`ApplyMigrationsAsync()`** — aplica migrations em desenvolvimento
>
> **Por que `internal static`?** A classe é `internal` porque é um detalhe de implementação do projeto Api — nenhum outro projeto precisa acessá-la. Os métodos são `static` porque são extension methods puros, sem estado.
>
> 🔬 **Nota de Engenharia — `[SuppressMessage]` vs `#pragma warning`**
>
> ```csharp
> [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider",
>     Justification = "Serilog gerencia formatação de strings internamente — falso positivo")]
> ```
>
> O atributo `[SuppressMessage]` é **superior** ao `#pragma warning disable/restore` por três motivos:
> 1. **Declarativo** — aparece como metadata no método, não como diretiva de pré-processador
> 2. **Auto-documentado** — o campo `Justification` explica *por que* a supressão é necessária
> 3. **Escopo preciso** — suprime apenas para o método decorado, enquanto `#pragma` afeta todo o bloco de código entre disable/restore
>
> Os métodos `WriteTo.Console()` e `WriteTo.Seq()` do Serilog aceitam format strings sem `IFormatProvider`. Com `TreatWarningsAsErrors=true`, o warning CA1305 bloquearia o build — mas o Serilog gerencia formatação internamente, tornando a supressão segura.
>
> **`builder.Services.AddSerilog()` vs `builder.Host.UseSerilog()`** — a API moderna `AddSerilog` (Serilog.AspNetCore 10+) registra o Serilog como logging provider via `IServiceCollection`, alinhado com o padrão de DI do .NET. O lambda `(services, loggerConfig)` dá acesso ao `IServiceProvider`, permitindo `ReadFrom.Services(services)` para enrichers baseados em DI.

### 4.13 Catalog API — Program.cs

**`src/Services/Catalog/OrderFlow.Catalog.Api/Program.cs`**

```csharp
using OrderFlow.Catalog.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilog();
builder.AddApiServices();

var app = builder.Build();

app.ConfigurePipeline();
await app.ApplyMigrationsAsync();

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program;
```

> 🏛️ **Nota Arquitetural — O Program.cs como mapa da aplicação**
>
> Com extension methods, o `Program.cs` se torna um **índice** de 15 linhas que conta a história completa do serviço:
> 1. **`AddSerilog()`** — logging configurado primeiro para capturar erros de startup
> 2. **`AddApiServices()`** — registra tudo que a aplicação precisa (DI container)
> 3. **`ConfigurePipeline()`** — middleware pipeline + endpoints
> 4. **`ApplyMigrationsAsync()`** — inicialização de runtime
>
> Cada extension method encapsula **uma área de responsabilidade**. Quando o projeto cresce, você pode dividir `AddApiServices()` em `AddSwagger()`, `AddHealthChecks()`, etc. — sem poluir o `Program.cs`.
>
> **Decisão — Auto-migrate apenas em Development:** `MigrateAsync()` aplica migrations pendentes automaticamente. Em produção, migrations devem ser aplicadas via CI/CD pipeline ou ferramenta dedicada — nunca na startup, pois pode causar downtime em deploys com múltiplas instâncias (duas instâncias tentando migrar ao mesmo tempo).
>
> 🔬 **Nota de Engenharia — Detalhes que importam**
>
> ```csharp
> builder.Services.AddTransient<GlobalExceptionHandler>();
> ```
> **Transient, não Scoped** — middlewares que implementam `IMiddleware` são resolvidos do DI a cada request. `Transient` garante que não há estado compartilhado entre requests. Se fosse `Singleton`, um `ILogger<T>` injetado poderia ter problemas com scoped services.
>
> ```csharp
> app.UseMiddleware<GlobalExceptionHandler>();
> app.UseSerilogRequestLogging();
> ```
> **Ordem dos middlewares** — o exception handler **antes** do request logging significa que exceções são capturadas e convertidas em respostas HTTP **antes** do Serilog logar o request. Assim, o log mostra `200 OK` ou `400 Bad Request`, não `500` para erros de validação.
>
> ```csharp
> app.MapHealthChecks("/health/live", new()
> {
>     Predicate = _ => false
> });
> ```
> **Liveness check vazio** — `Predicate = _ => false` ignora **todos** os health checks registrados. O endpoint apenas verifica se o processo responde HTTP. Se o processo estiver vivo, retorna 200. Kubernetes usa isso para decidir se reinicia o container.
>
> ```csharp
> public partial class Program;
> ```
> **`partial class`** permite que `WebApplicationFactory<Program>` nos testes de integração acesse o entry point. Sem isso, a classe `Program` gerada pelo top-level statements seria `internal` e inacessível de outros projetos.

### 4.14 Catalog API — appsettings.json

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

> 🔬 **Nota de Engenharia — Configuração por ambiente**
>
> **`Serilog.MinimumLevel.Override`:** Reduz verbosidade de logs internos. `Microsoft.AspNetCore: Warning` suprime logs como "Request starting HTTP/1.1 GET /api/..." (que são `Information`). Sem isso, cada request geraria 3-4 linhas de log do framework, poluindo a saída. Logs do **seu código** continuam em `Information`.
>
> **`TrustServerCertificate=true`:** Aceita certificados auto-assinados. **Apenas para desenvolvimento!** Em produção, configure certificados TLS válidos. Sem essa flag, a conexão falha porque o SQL Server em Docker usa certificado self-signed.

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
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderFlow.Catalog.Infrastructure.Data;

namespace OrderFlow.Catalog.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "CatalogTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove todos os registros de DbContext e suas opções
            services.RemoveAll<CatalogDbContext>();
            services.RemoveAll<DbContextOptions<CatalogDbContext>>();

            // Remove health checks que dependem do SQL Server
            services.RemoveAll<Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck>();

            // Registra CatalogDbContext com provider InMemory usando seu próprio service provider
            // (evita conflito com serviços do provider SqlServer no container de DI)
            services.AddScoped(_ =>
            {
                var options = new DbContextOptionsBuilder<CatalogDbContext>()
                    .UseInMemoryDatabase(_dbName)
                    .Options;
                return new CatalogDbContext(options);
            });
        });

        builder.UseEnvironment("Testing");
    }
}
```

> 🏛️ **Nota Arquitetural — WebApplicationFactory: teste de integração real**
>
> **O que faz:** Sobe a aplicação **inteira** em memória — DI container, middleware pipeline, controllers — mas substitui o banco real por InMemory. É o mais próximo de testar "em produção" sem precisar de infraestrutura externa.
>
> **Decisão — InMemory vs Testcontainers:** InMemory é rápido mas não testa SQL real (constraints, migrations, query filters podem se comportar diferente). Na Fase 05, o OrderFlow migra para **Testcontainers** que sobe um SQL Server real em Docker para cada test run. Para Fase 01, InMemory é suficiente e mais didático.
>
> 🔬 **Engenharia — Service replacement pattern:**
> ```csharp
> services.RemoveAll<CatalogDbContext>();
> services.RemoveAll<DbContextOptions<CatalogDbContext>>();
> ```
> **RemoveAll + manual registration** — remove **todos** os registros do `CatalogDbContext` e suas opções (incluindo os serviços internos do provider SqlServer). Em seguida, registra manualmente via `AddScoped` com `new DbContextOptionsBuilder<CatalogDbContext>()` — isso evita o conflito de providers EF Core (SqlServer + InMemory registrados simultaneamente via `AddDbContext`), que causa o erro "Services for database providers 'SqlServer', 'InMemory' have been registered". O `Guid.NewGuid()` no nome do banco garante isolamento entre testes.

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

    private async Task<CategoryDto> CreateCategoryAsync(string? name = null, string? description = null)
    {
        var request = new CreateCategoryRequest(
            name ?? $"Categoria {Guid.NewGuid():N}"[..30],
            description ?? "Descrição de teste");
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    [Fact]
    public async Task Create_ValidCategory_ReturnsCreated()
    {
        // Arrange
        var name = $"Eletrônicos {Guid.NewGuid():N}"[..30];
        var request = new CreateCategoryRequest(name, "Dispositivos eletrônicos");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Name.Should().Be(name);
        category.Id.Should().NotBeEmpty();
        category.IsActive.Should().BeTrue();
        category.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAll_WithActiveCategories_ReturnsOk()
    {
        // Arrange
        await CreateCategoryAsync();
        await CreateCategoryAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
        categories!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetById_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var created = await CreateCategoryAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/categories/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.Name.Should().Be(created.Name);
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
    public async Task Update_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var created = await CreateCategoryAsync();
        var updateRequest = new UpdateCategoryRequest("Nome Atualizado", "Descrição atualizada");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/categories/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        updated!.Name.Should().Be("Nome Atualizado");
        updated.Description.Should().Be("Descrição atualizada");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ExistingCategory_ReturnsNoContent()
    {
        // Arrange
        var created = await CreateCategoryAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/categories/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/v1/categories/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var category = await CreateCategoryAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new CreateCategoryRequest(category.Name, null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

> 🔬 **Nota de Engenharia — Anatomia dos testes de integração**
>
> ```csharp
> public class CategoriesControllerTests(CustomWebApplicationFactory factory)
>     : IClassFixture<CustomWebApplicationFactory>
> ```
> **`IClassFixture<T>`** — xUnit cria **uma** instância do factory para toda a classe de testes. `factory.CreateClient()` retorna um `HttpClient` que faz requests para a aplicação in-memory. **Primary constructor** injeta o fixture.
>
> **Helper method `CreateCategoryAsync()`** — encapsula a criação de categorias com nomes únicos via `Guid.NewGuid()`. A asserção `Should().Be(HttpStatusCode.Created)` no helper garante fail-fast: se o setup falha, o erro é claro e imediato, não uma falha confusa no Assert principal.
>
> **Padrão AAA (Arrange-Act-Assert):**
> ```csharp
> // Arrange — prepara dados
> var name = $"Eletrônicos {Guid.NewGuid():N}"[..30];
> // Act — executa a ação
> var response = await _client.PostAsJsonAsync("/api/v1/categories", request);
> // Assert — verifica resultado
> response.StatusCode.Should().Be(HttpStatusCode.Created);
> ```
> Cada teste segue essa estrutura. **FluentAssertions** (`Should().Be()`) é mais legível que `Assert.Equal()` — e gera mensagens de erro melhores quando falha.
>
> **Cobertura de cenários (8 testes):**
> - Happy path: Create, GetAll, GetById, Update, Delete
> - Edge cases: NotFound (ID inexistente), Conflict (nome duplicado), BadRequest (validação)
>
> **Naming convention: `Method_State_ExpectedBehavior`**
> - `Create_ValidCategory_ReturnsCreated` — o que faz, em que condição, o que espera
> - `Delete_ExistingCategory_ReturnsNoContent` — cenário de soft delete end-to-end

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
        var request = new CreateCategoryRequest(
            $"Categoria Teste {Guid.NewGuid():N}"[..30],
            "Categoria para testes");
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    private async Task<ProductDto> CreateProductAsync(Guid categoryId, string? name = null, string? sku = null)
    {
        var request = new CreateProductRequest(
            name ?? "Produto Teste",
            "Descrição de teste",
            sku ?? $"SKU-{Guid.NewGuid():N}"[..20],
            99.99m,
            10,
            categoryId);
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    [Fact]
    public async Task Create_ValidProduct_ReturnsCreated()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var sku = $"LAPTOP-{Guid.NewGuid():N}"[..20];
        var request = new CreateProductRequest(
            "Laptop Pro",
            "Laptop de alta performance",
            sku,
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
        product.Sku.Should().Be(sku.ToUpperInvariant());
        product.Price.Should().Be(2999.99m);
        product.IsActive.Should().BeTrue();
        product.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public async Task GetById_ExistingProduct_ReturnsOk()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);

        // Act
        var response = await _client.GetAsync($"/api/v1/products/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product!.Id.Should().Be(created.Id);
        product.Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task GetById_NonExistentProduct_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_WithTerm_ReturnsFilteredResults()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        await CreateProductAsync(category.Id, "Wireless Mouse", $"MOUSE-{Guid.NewGuid():N}"[..20]);
        await CreateProductAsync(category.Id, "Wireless Keyboard", $"KB-{Guid.NewGuid():N}"[..20]);
        await CreateProductAsync(category.Id, "Monitor 27 polegadas", $"MON-{Guid.NewGuid():N}"[..20]);

        // Act
        var response = await _client.GetAsync("/api/v1/products?searchTerm=Wireless");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Items.Should().OnlyContain(p => p.Name.Contains("Wireless"));
    }

    [Fact]
    public async Task Update_ExistingProduct_ReturnsOk()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);
        var updateRequest = new UpdateProductRequest(
            "Nome Atualizado",
            "Descrição atualizada",
            199.99m,
            50);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/products/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProductDto>();
        updated!.Name.Should().Be("Nome Atualizado");
        updated.Price.Should().Be(199.99m);
        updated.StockQuantity.Should().Be(50);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_DuplicateSku_ReturnsConflict()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var sku = $"DUP-{Guid.NewGuid():N}"[..15];
        await CreateProductAsync(category.Id, sku: sku);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Produto 2", null, sku, 20m, 2, category.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var request = new CreateProductRequest(
            "",
            null,
            $"SKU-{Guid.NewGuid():N}"[..15],
            10m,
            1,
            category.Id);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InvalidCategoryId_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateProductRequest(
            "Produto Teste",
            null,
            $"SKU-{Guid.NewGuid():N}"[..15],
            10m,
            1,
            Guid.NewGuid());

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingProduct_ReturnsNoContent()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/products/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verifica soft-delete (filtrado pelo query filter)
        var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

> 🔬 **Nota de Engenharia — Testes que verificam comportamento, não implementação**
>
> **Helper methods com asserções** — tanto `CreateCategoryAsync` quanto `CreateProductAsync` incluem `Should().Be(HttpStatusCode.Created)`. Isso implementa o padrão **fail-fast no setup**: se a criação falha, o erro é imediato e claro, sem propagar uma falha confusa no Assert principal do teste.
>
> ```csharp
> var response = await _client.DeleteAsync($"/api/v1/products/{created.Id}");
> response.StatusCode.Should().Be(HttpStatusCode.NoContent);
> var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}");
> getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
> ```
> **Teste de soft delete end-to-end** — deleta o produto (soft delete), depois tenta buscar. O Global Query Filter faz o produto "desaparecer" do GET, provando que o fluxo completo funciona: Controller → Service → Entity.Deactivate → SaveChanges → QueryFilter esconde.
>
> **Cobertura de cenários (9 testes):**
> - Happy path: Create, GetById, Search, Update, Delete
> - Edge cases: NotFound (ID inexistente), Conflict (SKU duplicado), BadRequest (validação), NotFound (categoria inexistente)
>
> ```csharp
> $"SKU-{Guid.NewGuid():N}"[..20]
> ```
> **Dados únicos por teste** — `Guid.NewGuid()` gera unicidade, `:N` remove hífens, `[..20]` corta no limite do SKU. Sem isso, testes rodando em paralelo poderiam ter colisão de SKU e falhas intermitentes. O mesmo padrão é aplicado a nomes de categorias.
>
> ```csharp
> product.Sku.Should().Be(sku.ToUpperInvariant());
> ```
> **Verifica normalização** — o teste confirma que o SKU é armazenado em maiúsculas independente do input, validando a regra de negócio `Sku = sku.ToUpperInvariant()` na entidade Product.

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
| Catalog Api | `ProductsController.cs`, `CategoriesController.cs`, `Program.cs`, `Extensions/HostingExtensions.cs` |
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
