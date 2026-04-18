# Fase 12 — OAuth2 / OIDC com Duende IdentityServer

> **Trilha:** Sênior | **Pré-requisitos:** Fase 04 (JWT básico)
> **Objetivo:** Substituir o JWT artesanal por um **Identity Provider real** — Duende IdentityServer 7. Implementar **OAuth 2.1** com **Authorization Code + PKCE**, **Client Credentials**, **Refresh Token Rotation** e mapear claims/scopes para autorização do Orders e Catalog.

### 🎯 O que você vai aprender

- Os **5 fluxos OAuth2** — qual usar para SPA, mobile, server-to-server, device
- **OIDC** = OAuth2 + camada de identidade (id_token, userinfo)
- **PKCE** — por que é obrigatório em qualquer client público hoje
- Configurar **Duende IdentityServer 7** — clients, scopes, resources, persistence
- **Refresh Token Rotation** com detecção de reuse
- Mapeamento Resource Server: validar JWT + autorizar por scope/claim/policy
- Quando NÃO usar IdentityServer — alternativas (Auth0, Azure AD B2C, Keycloak)

---

## Sumário

1. [Por Que Duende em vez de JWT Artesanal](#1-por-que-duende-em-vez-de-jwt-artesanal)
2. [OAuth2 vs OIDC — Diferença Real](#2-oauth2-vs-oidc--diferença-real)
3. [Os 5 Fluxos e Quando Usar](#3-os-5-fluxos-e-quando-usar)
4. [PKCE — Não Negociável](#4-pkce--não-negociável)
5. [Setup do Duende IdentityServer](#5-setup-do-duende-identityserver)
6. [Resource Server (Orders, Catalog)](#6-resource-server-orders-catalog)
7. [Refresh Token Rotation](#7-refresh-token-rotation)
8. [Autorização com Scopes, Claims e Policies](#8-autorização-com-scopes-claims-e-policies)
9. [Alternativas a Duende](#9-alternativas-a-duende)
10. [💼 Perguntas de Entrevista](#10--perguntas-de-entrevista)

---

## 1. Por Que Duende em vez de JWT Artesanal

A Fase 04 implementou JWT manualmente — bom para aprender, **inseguro para produção**:

| Problema do JWT artesanal | Solução com IdP |
|---|---|
| Sem revogação real | Refresh rotation + introspection |
| Assinatura HS256 (chave compartilhada) | RS256 com JWKS endpoint |
| Sem rotação de chaves | Key Rolling automático |
| Sem padrões de discovery | `.well-known/openid-configuration` |
| Sem suporte a SSO | OIDC + cookies session |
| Federação impossível | OIDC Federation (Google, AAD, etc.) |
| Cada serviço reinventa | Padrão do mercado, biblioteca testada |

**Duende** é o sucessor comercial do IdentityServer4. Licença gratuita até **US$1M de receita anual** ou para projetos pessoais — perfeito para portfólio.

---

## 2. OAuth2 vs OIDC — Diferença Real

| | OAuth 2.1 | OIDC |
|---|---|---|
| Foco | **Autorização** (delegação de acesso) | **Autenticação** (saber quem é) |
| Token retornado | `access_token` (opaque ou JWT) | `access_token` + **`id_token`** (JWT obrigatório) |
| Pergunta que responde | "Posso acessar essa API?" | "Quem é o usuário e ele se autenticou?" |
| Padrão usado para | API-to-API, scopes | Login (SSO, "Login com Google") |

> **Mnemônico:** OAuth pergunta *"o que você pode fazer?"*; OIDC pergunta *"quem é você?"*.

OIDC é uma **camada sobre OAuth2** — adiciona id_token, endpoint userinfo e scopes padronizados (`openid`, `profile`, `email`).

---

## 3. Os 5 Fluxos e Quando Usar

| Fluxo | Para quem | Status |
|---|---|---|
| **Authorization Code + PKCE** | SPA, mobile, desktop, web server-side | ✅ Padrão atual |
| **Client Credentials** | Server-to-server (sem usuário) | ✅ |
| **Device Code** | Smart TV, CLI, IoT (sem teclado) | ✅ |
| **Refresh Token** | Renovação silenciosa (junto com os de cima) | ✅ |
| **Resource Owner Password** | Apenas legado/migração | ⚠️ Desencorajado |
| ❌ Implicit Flow | SPA antigo | ❌ **Removido no OAuth 2.1** |
| ❌ Hybrid Flow | Web app antigo | ❌ Substituído por Code + PKCE |

### Decisão Rápida

```
Tem usuário humano?
├── Sim → Authorization Code + PKCE
└── Não (server-to-server) → Client Credentials

Sem teclado (TV, CLI)?
└── Device Code

Quer renovar sem login?
└── Adicione Refresh Token (com rotation)
```

---

## 4. PKCE — Não Negociável

PKCE (*Proof Key for Code Exchange*) protege o Authorization Code de interceptação.

### Como Funciona

1. Client gera `code_verifier` aleatório (43-128 chars)
2. Calcula `code_challenge = BASE64URL(SHA256(code_verifier))`
3. Envia `code_challenge` na request `/authorize`
4. Authorization Server guarda `code_challenge`
5. Client troca o code por token enviando `code_verifier` original
6. Server verifica `SHA256(code_verifier) == code_challenge`

**Por quê:** mesmo se atacante interceptar o `code` (via deep link, log, referer), sem o `code_verifier` original ele não consegue trocar.

> **OAuth 2.1:** PKCE é **obrigatório** para todos os clients (público e confidencial), não só públicos.

---

## 5. Setup do Duende IdentityServer

### Pacote
```
dotnet add package Duende.IdentityServer
dotnet add package Duende.IdentityServer.EntityFramework
```

### `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddIdentityServer(options =>
    {
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseInformationEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;

        options.EmitStaticAudienceClaim = true;
        options.KeyManagement.Enabled = true;       // 🔑 rotação automática de chaves
    })
    .AddConfigurationStore(opts =>
    {
        opts.ConfigureDbContext = b =>
            b.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb"));
    })
    .AddOperationalStore(opts =>
    {
        opts.ConfigureDbContext = b =>
            b.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb"));
        opts.EnableTokenCleanup = true;
        opts.TokenCleanupInterval = 3600;
    })
    .AddAspNetIdentity<ApplicationUser>();

builder.Services.AddAuthentication()
    .AddGoogle("Google", options =>                 // federação opcional
    {
        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
        options.ClientId = builder.Configuration["Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
    });

var app = builder.Build();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.Run();
```

### Configuração — Resources, Scopes, Clients

```csharp
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        new IdentityResource("roles", "User roles", ["role"])
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new("orders.read",  "Read orders"),
        new("orders.write", "Create/update orders"),
        new("catalog.read", "Read catalog"),
        new("admin",        "Admin operations")
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new("orders-api", "Orders API")
        {
            Scopes = ["orders.read", "orders.write"],
            UserClaims = ["role", "email"]
        },
        new("catalog-api", "Catalog API")
        {
            Scopes = ["catalog.read"],
            UserClaims = ["role"]
        }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // SPA (Authorization Code + PKCE)
        new()
        {
            ClientId = "orderflow-spa",
            ClientName = "OrderFlow Web App",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,            // public client
            AllowedScopes = ["openid", "profile", "email", "roles", "orders.read", "orders.write", "catalog.read", "offline_access"],
            RedirectUris = ["https://app.orderflow.com/callback"],
            PostLogoutRedirectUris = ["https://app.orderflow.com/"],
            AllowedCorsOrigins = ["https://app.orderflow.com"],
            AllowOfflineAccess = true,              // libera refresh_token
            RefreshTokenUsage = TokenUsage.OneTimeOnly,    // 🔑 rotation
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 30 * 24 * 3600,  // 30 dias
            AccessTokenLifetime = 15 * 60                  // 15 min
        },

        // Worker (Client Credentials)
        new()
        {
            ClientId = "notification-worker",
            ClientName = "Notification Worker",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = [new Secret("S3cr3tInVault!".Sha256())],
            AllowedScopes = ["catalog.read"]
        },

        // CLI Admin (Device Code)
        new()
        {
            ClientId = "orderflow-cli",
            AllowedGrantTypes = GrantTypes.DeviceFlow,
            RequireClientSecret = false,
            AllowedScopes = ["openid", "profile", "admin"]
        }
    ];
}
```

---

## 6. Resource Server (Orders, Catalog)

Os APIs **validam** JWTs emitidos pelo Duende — não emitem.

```csharp
// Orders.Api/Program.cs
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"];
        options.Audience = "orders-api";
        options.MapInboundClaims = false;            // 🔑 não renomeie claims
        options.TokenValidationParameters.ValidTypes = ["at+jwt"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanReadOrders", p =>
        p.RequireAuthenticatedUser()
         .RequireClaim("scope", "orders.read"));

    options.AddPolicy("CanWriteOrders", p =>
        p.RequireAuthenticatedUser()
         .RequireClaim("scope", "orders.write"));

    options.AddPolicy("Admin", p =>
        p.RequireAuthenticatedUser()
         .RequireClaim("scope", "admin")
         .RequireClaim("role", "admin"));
});

// Em endpoints
app.MapGet("/orders", ...).RequireAuthorization("CanReadOrders");
app.MapPost("/orders", ...).RequireAuthorization("CanWriteOrders");
```

> **`MapInboundClaims = false`** — sem isso, a Microsoft renomeia `sub` para `nameidentifier`, `name` para os longos namespaces SOAP. Pesadelo de debug.

---

## 7. Refresh Token Rotation

Cada uso do refresh **emite um novo** e **invalida o anterior**. Se atacante reusar refresh já consumido, **toda a árvore de tokens** daquele usuário é revogada (detecção de reuse).

```csharp
// Configuração já feita no Client acima:
RefreshTokenUsage = TokenUsage.OneTimeOnly         // OneTimeOnly = rotation
RefreshTokenExpiration = TokenExpiration.Sliding   // estende a cada uso
```

### Por que importa
- Roubo de refresh é **mais perigoso** que de access — vida longa
- Rotation: token roubado vira inútil ao primeiro uso legítimo
- Reuse detection: alerta + revogação total

### Fluxo
```
Cliente: refresh_token=R1 → IS
IS:      access_token=A2, refresh_token=R2; R1 invalidado
Atacante usa R1 → IS detecta reuse → revoga R1 e R2 (cadeia inteira)
```

---

## 8. Autorização com Scopes, Claims e Policies

| Conceito | O Que É | Exemplo |
|---|---|---|
| **Scope** | O que o **client** pode pedir (delegação) | `orders.write` |
| **Claim** | Atributo do **usuário** ou token | `role=admin`, `tenant=acme` |
| **Policy** | Regra ASP.NET combinando scopes/claims | "tem orders.write **E** role=manager" |

### Policy Combinada

```csharp
options.AddPolicy("OrderManager", policy =>
    policy.RequireAuthenticatedUser()
          .RequireClaim("scope", "orders.write")
          .RequireRole("manager")
          .RequireAssertion(ctx =>
              ctx.User.HasClaim(c => c.Type == "tenant"
                                  && c.Value == "acme")));
```

### Resource-Based Authorization

Quando autorização depende do **dado**: "usuário só pode ver pedidos do próprio tenant":

```csharp
public sealed class OrderTenantHandler(IHttpContextAccessor http)
    : AuthorizationHandler<SameTenantRequirement, Order>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx,
        SameTenantRequirement req,
        Order order)
    {
        var userTenant = ctx.User.FindFirst("tenant")?.Value;
        if (userTenant == order.TenantId.ToString())
            ctx.Succeed(req);
        return Task.CompletedTask;
    }
}

// Uso
var auth = await _authService.AuthorizeAsync(User, order, "SameTenant");
if (!auth.Succeeded) return Forbid();
```

---

## 9. Alternativas a Duende

| Opção | Quando |
|---|---|
| **Duende IdentityServer** | Quer controle total, on-prem, projeto < US$1M ou pago |
| **Azure AD B2C / Entra External ID** | Already-on-Azure, federação enterprise, MFA managed |
| **Auth0 / Okta** | SaaS, 100% gerenciado, mais caro |
| **Keycloak** | Open source completo, Java; alternativa não-Microsoft |
| **ASP.NET Core Identity puro** | App monolítico simples — sem OIDC nativo |
| **OpenIddict** | Alternativa gratuita ao Duende, mais lib que produto, curva maior |

> **ADR sugerido:** documentar essa decisão é exatamente o tipo de coisa que diferencia portfólio sênior.

---

## 10. 💼 Perguntas de Entrevista

**1. "Diferencie OAuth2 e OIDC."**
— OAuth2 é **autorização** (delegação de acesso a APIs). OIDC adiciona uma **camada de autenticação** sobre OAuth2 — emite `id_token` (JWT) + endpoint `userinfo`. OAuth diz "o que pode fazer"; OIDC diz "quem é".

**2. "Por que Authorization Code + PKCE em vez de Implicit Flow para SPA?"**
— Implicit retornava token diretamente na URL — vulnerável a histórico, referer, logs. PKCE protege o code com `code_verifier` dinâmico — mesmo interceptando o code, atacante não consegue trocar sem o verifier. **OAuth 2.1 removeu Implicit** — Code + PKCE é o único caminho.

**3. "Como revogar acesso de usuário comprometido com JWT?"**
— Você não revoga JWT (expira em ~15min). Revoga: (1) o **refresh token** no IS, (2) opcional **denylist Redis** com TTL=vida do JWT, (3) invalida sessão no IdP. Em ~15min usuário está fora.

**4. "O que é Refresh Token Rotation e detecção de reuse?"**
— Cada uso do refresh emite **um novo** e invalida o anterior. Se atacante usar refresh já consumido, IS detecta e **revoga toda a cadeia** daquele usuário. Mitiga roubo de refresh — token roubado vira inútil ao primeiro uso legítimo.

**5. "Diferença entre scope e claim?"**
— **Scope** = o que o **client** está autorizado a pedir (`orders.write`). **Claim** = atributo do **usuário** ou token (`role=admin`, `tenant=acme`). Authorization combina ambos via Policy.

**6. "Como autorizar 'usuário só vê próprios pedidos'?"**
— **Resource-based authorization** — não dá pra resolver no token (n usuários × m pedidos = explosão). Implementa `AuthorizationHandler<TRequirement, TResource>` que recebe o pedido e compara `User.Claim("sub")` com `order.UserId`.

**7. "Quando você NÃO usaria Duende IdentityServer?"**
— (1) Já estamos no Azure e organização paga AAD B2C — usar mais barato. (2) Time pequeno sem expertise em IdM — preferir SaaS (Auth0, Okta) para focar no core. (3) Receita > US$1M — licença Duende cara; avaliar OpenIddict ou Keycloak. (4) App monolítico simples sem OIDC — ASP.NET Identity puro basta.

---

## Checkpoint

✅ IdentityServer rodando com EF persistence e KeyManagement habilitado
✅ Login Authorization Code + PKCE funcional via SPA de teste
✅ Worker Notification autenticando com Client Credentials
✅ Orders/Catalog validando JWT via JWKS endpoint
✅ Refresh rotation com one-time-use
✅ ADR escrito comparando Duende vs Azure AD B2C vs Auth0

➡️ **Próxima fase:** [`fase-13-grpc-kafka-eventsourcing.md`](./fase-13-grpc-kafka-eventsourcing.md)
