# Fase 4 — Autenticação e Segurança

> **Objetivo:** Construir a Identity API com ASP.NET Identity, JWT Bearer + Refresh Tokens, Authorization Policies, rate limiting e CORS.

> **Pré-requisito:** Fase 3 concluída (CQRS funcional no Orders API).

### 🎯 O que você vai aprender nesta fase

- Configurar **ASP.NET Identity** com Entity Framework Core
- Implementar autenticação com **JWT Bearer** e **Refresh Tokens**
- Criar **Authorization Policies** baseadas em Claims e Roles
- Aplicar **Rate Limiting** (fixed window, sliding window, token bucket)
- Configurar **CORS** para consumo por SPAs
- Proteger endpoints com `[Authorize]` e policies customizadas

---

## Sumário

1. [Visão da Fase](#1-visão-da-fase)
2. [Decisões Arquiteturais](#2-decisões-arquiteturais)
3. [Conceitos de Segurança](#3-conceitos-de-segurança)
4. [Passo a Passo de Implementação](#4-passo-a-passo-de-implementação)
5. [Código de Referência Completo](#5-código-de-referência-completo)
6. [Testes](#6-testes)
7. [Checkpoint](#7-checkpoint)

---

## 1. Visão da Fase

### O Que Vamos Construir

```
src/Services/Identity/
├── OrderFlow.Identity.Api/
│   ├── Models/
│   │   ├── ApplicationUser.cs
│   │   └── RefreshToken.cs
│   ├── Data/
│   │   └── AppIdentityDbContext.cs
│   ├── Services/
│   │   ├── ITokenService.cs
│   │   ├── TokenService.cs
│   │   ├── IAuthService.cs
│   │   └── AuthService.cs
│   ├── DTOs/
│   │   ├── RegisterRequest.cs
│   │   ├── LoginRequest.cs
│   │   ├── RefreshTokenRequest.cs
│   │   ├── AuthResponse.cs
│   │   └── UserDto.cs
│   ├── Validators/
│   │   ├── RegisterRequestValidator.cs
│   │   └── LoginRequestValidator.cs
│   ├── Controllers/
│   │   └── AuthController.cs
│   └── Program.cs

src/BuildingBlocks/
└── OrderFlow.SharedKernel/
    └── Auth/
        ├── JwtSettings.cs
        └── ClaimsPrincipalExtensions.cs
```

### Tópicos Cobertos

| Tópico | Detalhe |
|--------|---------|
| **ASP.NET Identity** | UserManager, SignInManager, stores, password hashing |
| **JWT Bearer** | Access token com claims customizadas |
| **Refresh Tokens** | Rotação segura, revogação, armazenamento em banco |
| **Authorization Policies** | Role-based + Claims-based policies |
| **Rate Limiting** | Proteção contra brute force via middleware built-in |
| **CORS** | Cross-Origin Resource Sharing configurado |
| **Password Security** | Política de senhas, hashing com PBKDF2 |
| **Anti-forgery patterns** | Proteção contra token theft |

---

## 2. Decisões Arquiteturais

### ADR-009: Identity como Serviço Separado

**Contexto:** Autenticação pode ser centralizada ou distribuída.

**Decisão:** Identity API como microserviço separado que emite tokens JWT. Outros serviços apenas validam o token (não precisam de conexão com banco de identidade).

```
┌───────────┐     POST /auth/login      ┌──────────────┐
│  Client   │ ──────────────────────────▶│ Identity API │
│ (Browser/ │                            │              │
│  Mobile)  │ ◀─────────────────────────│ JWT + Refresh │
└───────┬───┘     { accessToken,         └──────────────┘
        │           refreshToken }
        │
        │  GET /api/orders
        │  Authorization: Bearer <JWT>
        │
┌───────▼───────┐                      ┌──────────────┐
│  API Gateway  │ ────────────────────▶│  Orders API  │
│    (YARP)     │  Forward + JWT       │  (valida JWT │
└───────────────┘                      │  localmente) │
                                       └──────────────┘
```

**Consequências:**
- (+) Outros serviços não precisam de banco de identidade
- (+) Escala independente
- (+) JWT é stateless — validação rápida
- (-) Token revocation precisa de estratégia (short-lived + refresh)

### ADR-010: Refresh Token Rotation

**Contexto:** Access tokens JWT são stateless mas não podem ser revogados. Se roubado, o atacante tem acesso até expirar.

**Decisão:** Access token com vida curta (15 min) + Refresh token com vida longa (7 dias) + rotação a cada uso.

```
Login → AccessToken (15min) + RefreshToken (7d)
                │
                ▼ (token expira)
Refresh → Novo AccessToken (15min) + Novo RefreshToken (7d)
          │ RefreshToken antigo é REVOGADO
          │
          ▼ (se alguém usa o refresh antigo)
ALERTA: Possível token theft → Revoga TODOS os tokens do usuário
```

---

## 3. Conceitos de Segurança

### JWT — Como Funciona

```
┌─────────────────────────────────────────────────────────────┐
│                        JWT TOKEN                            │
│                                                             │
│  Header (Base64)     Payload (Base64)      Signature        │
│  ┌───────────────┐   ┌──────────────────┐  ┌────────────┐ │
│  │ alg: HS256    │ . │ sub: user-id     │ .│ HMACSHA256(│ │
│  │ typ: JWT      │   │ email: user@..   │  │  header +  │ │
│  └───────────────┘   │ role: Admin      │  │  payload,  │ │
│                      │ exp: 1234567890  │  │  secret)   │ │
│                      │ iss: OrderFlow   │  └────────────┘ │
│                      └──────────────────┘                  │
└─────────────────────────────────────────────────────────────┘

Validação no Orders API (sem banco):
1. Verifica assinatura com a mesma chave/certificado
2. Verifica exp (expiração)
3. Verifica iss (issuer) e aud (audience)
4. Lê claims para authorization policies
```

### Fluxo Completo de Autenticação

```
1. Register:  POST /api/auth/register  → { fullName, email, password }
2. Login:     POST /api/auth/login     → { email, password }
              ← { accessToken, refreshToken, expiresAt }
3. Use API:   GET /api/orders          + Header: Authorization: Bearer <accessToken>
4. Refresh:   POST /api/auth/refresh   → { accessToken, refreshToken }
              ← { newAccessToken, newRefreshToken, expiresAt }
5. Logout:    POST /api/auth/logout    → revoga refresh tokens
```

---

## 4. Passo a Passo de Implementação

### 4.1 Criar o Projeto

```bash
# Identity API
dotnet new webapi -n OrderFlow.Identity.Api -o src/Services/Identity/OrderFlow.Identity.Api
dotnet sln add src/Services/Identity/OrderFlow.Identity.Api
dotnet add src/Services/Identity/OrderFlow.Identity.Api reference src/BuildingBlocks/OrderFlow.SharedKernel

# Pacotes
dotnet add src/Services/Identity/OrderFlow.Identity.Api package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add src/Services/Identity/OrderFlow.Identity.Api package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/Services/Identity/OrderFlow.Identity.Api package Microsoft.EntityFrameworkCore.Tools
dotnet add src/Services/Identity/OrderFlow.Identity.Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Services/Identity/OrderFlow.Identity.Api package FluentValidation.AspNetCore
dotnet add src/Services/Identity/OrderFlow.Identity.Api package Serilog.AspNetCore
dotnet add src/Services/Identity/OrderFlow.Identity.Api package Serilog.Sinks.Seq

# Testes
dotnet new xunit -n OrderFlow.Identity.Api.Tests -o tests/OrderFlow.Identity.Api.Tests
dotnet sln add tests/OrderFlow.Identity.Api.Tests
dotnet add tests/OrderFlow.Identity.Api.Tests reference src/Services/Identity/OrderFlow.Identity.Api
dotnet add tests/OrderFlow.Identity.Api.Tests package FluentAssertions
dotnet add tests/OrderFlow.Identity.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/OrderFlow.Identity.Api.Tests package Microsoft.EntityFrameworkCore.InMemory
```

---

## 5. Código de Referência Completo

### 5.1 SharedKernel — JWT Settings

**`src/BuildingBlocks/OrderFlow.SharedKernel/Auth/JwtSettings.cs`**

```csharp
namespace OrderFlow.SharedKernel.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}
```

**`src/BuildingBlocks/OrderFlow.SharedKernel/Auth/ClaimsPrincipalExtensions.cs`**

```csharp
using System.Security.Claims;

namespace OrderFlow.SharedKernel.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID claim not found.");
        return Guid.Parse(claim.Value);
    }

    public static string GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? throw new InvalidOperationException("Email claim not found.");
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole("Admin");
    }
}
```

### 5.2 Models

**`src/Services/Identity/OrderFlow.Identity.Api/Models/ApplicationUser.cs`**

```csharp
using Microsoft.AspNetCore.Identity;

namespace OrderFlow.Identity.Api.Models;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
```

**`src/Services/Identity/OrderFlow.Identity.Api/Models/RefreshToken.cs`**

```csharp
namespace OrderFlow.Identity.Api.Models;

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Foreign key
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
```

### 5.3 DbContext

**`src/Services/Identity/OrderFlow.Identity.Api/Data/AppIdentityDbContext.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Identity.Api.Models;

namespace OrderFlow.Identity.Api.Data;

public sealed class AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.HasMany(u => u.RefreshTokens).WithOne(r => r.User).HasForeignKey(r => r.UserId);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Token).HasMaxLength(500).IsRequired();
            entity.HasIndex(t => t.Token);
        });

        // Renomear tabelas do Identity para snake_case (opcional)
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
    }
}
```

### 5.4 DTOs e Validators

**`src/Services/Identity/OrderFlow.Identity.Api/DTOs/RegisterRequest.cs`**

```csharp
namespace OrderFlow.Identity.Api.DTOs;

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword);
```

**`src/Services/Identity/OrderFlow.Identity.Api/DTOs/LoginRequest.cs`**

```csharp
namespace OrderFlow.Identity.Api.DTOs;

public sealed record LoginRequest(string Email, string Password);
```

**`src/Services/Identity/OrderFlow.Identity.Api/DTOs/RefreshTokenRequest.cs`**

```csharp
namespace OrderFlow.Identity.Api.DTOs;

public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);
```

**`src/Services/Identity/OrderFlow.Identity.Api/DTOs/AuthResponse.cs`**

```csharp
namespace OrderFlow.Identity.Api.DTOs;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles);
```

**`src/Services/Identity/OrderFlow.Identity.Api/Validators/RegisterRequestValidator.cs`**

```csharp
using FluentValidation;
using OrderFlow.Identity.Api.DTOs;

namespace OrderFlow.Identity.Api.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
```

**`src/Services/Identity/OrderFlow.Identity.Api/Validators/LoginRequestValidator.cs`**

```csharp
using FluentValidation;
using OrderFlow.Identity.Api.DTOs;

namespace OrderFlow.Identity.Api.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
```

### 5.5 Token Service

**`src/Services/Identity/OrderFlow.Identity.Api/Services/ITokenService.cs`**

```csharp
using System.Security.Claims;
using OrderFlow.Identity.Api.Models;

namespace OrderFlow.Identity.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    (RefreshToken Token, string RawValue) GenerateRefreshToken(Guid userId);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    string HashToken(string rawToken);
}
```

**`src/Services/Identity/OrderFlow.Identity.Api/Services/TokenService.cs`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Identity.Api.Models;
using OrderFlow.SharedKernel.Auth;

namespace OrderFlow.Identity.Api.Services;

public sealed class TokenService(IOptions<JwtSettings> jwtSettings) : ITokenService
{
    private readonly JwtSettings _settings = jwtSettings.Value;

    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (RefreshToken Token, string RawValue) GenerateRefreshToken(Guid userId)
    {
        var randomBytes = new byte[32]; // 256 bits — suficiente per NIST guidelines
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var rawValue = Convert.ToBase64String(randomBytes);

        var refreshToken = new RefreshToken
        {
            Token = HashToken(rawValue), // Armazena HASH no banco, nunca o valor bruto
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            UserId = userId
        };

        return (refreshToken, rawValue);
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret)),
            ValidateLifetime = false // Permite tokens expirados
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        return principal;
    }
}
```

> **🔒 Segurança: Por que Hashear Refresh Tokens?**
>
> O refresh token é armazenado como um **hash SHA-256** no banco de dados, nunca em texto plano. Isso segue o mesmo princípio de senhas: se o banco de dados for comprometido, o atacante terá apenas os hashes — que são irreversíveis — e não poderá usá-los para se autenticar.
>
> **Fluxo:** Gerar 32 bytes aleatórios → Base64 (enviar ao cliente) → SHA-256 (armazenar no banco)
>
> O cliente recebe o valor bruto e o envia no refresh. O servidor hasheia o valor recebido e compara com o hash armazenado.

### 5.6 Auth Service

**`src/Services/Identity/OrderFlow.Identity.Api/Services/IAuthService.cs`**

> **⚠️ Acoplamento entre serviços:** A Identity API referencia `OrderFlow.Orders.Application.Common` para reutilizar `Result<T>`. Em produção, `Result<T>` e `Error` devem residir em `OrderFlow.SharedKernel` (basta mover os dois arquivos e atualizar os `namespace`/`using`). Mantemos essa simplificação aqui para evitar alterar dezenas de usings nos exemplos anteriores.

```csharp
using OrderFlow.Identity.Api.DTOs;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Identity.Api.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<Result> RevokeRefreshTokenAsync(Guid userId);
}
```

**`src/Services/Identity/OrderFlow.Identity.Api/Services/AuthService.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Identity.Api.Data;
using OrderFlow.Identity.Api.DTOs;
using OrderFlow.Identity.Api.Models;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Identity.Api.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    AppIdentityDbContext dbContext,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return Result<AuthResponse>.Failure(
                new Error("Auth.EmailTaken", "An account with this email already exists."));

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            UserName = request.Email.Trim().ToLowerInvariant()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Result<AuthResponse>.Failure(new Error("Auth.CreateFailed", errors));
        }

        await userManager.AddToRoleAsync(user, "Customer");

        logger.LogInformation("User registered: {Email}", user.Email);

        return await GenerateAuthResponse(user);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidCredentials", "Invalid email or password."));

        var isValidPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidCredentials", "Invalid email or password."));
        }

        logger.LogInformation("User logged in: {Email}", user.Email);

        return await GenerateAuthResponse(user);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidToken", "Invalid access token."));

        var userId = Guid.Parse(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await dbContext.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Result<AuthResponse>.Failure(
                new Error("Auth.UserNotFound", "User not found."));

        var hashedToken = tokenService.HashToken(request.RefreshToken);
        var existingToken = user.RefreshTokens
            .FirstOrDefault(t => t.Token == hashedToken);

        if (existingToken is null || !existingToken.IsActive)
        {
            // Possível token theft — revogar TODOS os tokens do usuário
            if (existingToken is not null && existingToken.IsRevoked)
            {
                logger.LogWarning("Token reuse detected for user {UserId}. Revoking all tokens.", userId);
                foreach (var token in user.RefreshTokens.Where(t => t.IsActive))
                {
                    token.RevokedAt = DateTime.UtcNow;
                }
                await dbContext.SaveChangesAsync();
            }

            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidRefreshToken", "Invalid or expired refresh token."));
        }

        // Rotação: revogar o token atual e gerar um novo
        existingToken.RevokedAt = DateTime.UtcNow;
        var (newRefreshToken, rawValue) = tokenService.GenerateRefreshToken(userId);
        existingToken.ReplacedByToken = newRefreshToken.Token; // Hash do novo token
        user.RefreshTokens.Add(newRefreshToken);

        await dbContext.SaveChangesAsync();

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            rawValue, // Retorna valor bruto (não o hash) para o cliente
            DateTime.UtcNow.AddMinutes(15),
            new UserDto(user.Id, user.FullName, user.Email!, roles.ToList())));
    }

    public async Task<Result> RevokeRefreshTokenAsync(Guid userId)
    {
        var user = await dbContext.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Result.Failure(new Error("Auth.UserNotFound", "User not found."));

        foreach (var token in user.RefreshTokens.Where(t => t.IsActive))
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);

        return Result.Success();
    }

    private async Task<Result<AuthResponse>> GenerateAuthResponse(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var (refreshToken, rawValue) = tokenService.GenerateRefreshToken(user.Id);

        // Salvar refresh token (hash armazenado no banco)
        user.RefreshTokens ??= [];
        user.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            rawValue, // Retorna valor bruto para o cliente
            DateTime.UtcNow.AddMinutes(15),
            new UserDto(user.Id, user.FullName, user.Email!, roles.ToList())));
    }
}
```

### 5.7 Controller

**`src/Services/Identity/OrderFlow.Identity.Api/Controllers/AuthController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Identity.Api.DTOs;
using OrderFlow.Identity.Api.Services;
using OrderFlow.SharedKernel.Auth;

namespace OrderFlow.Identity.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);

        return result.IsSuccess ? Ok(result.Value) : Unauthorized(result.Error);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var result = await authService.RefreshTokenAsync(request);

        return result.IsSuccess ? Ok(result.Value) : Unauthorized(result.Error);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();
        await authService.RevokeRefreshTokenAsync(userId);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var userId = User.GetUserId();
        var email = User.GetEmail();
        var name = User.Identity?.Name ?? "";
        var roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return Ok(new UserDto(userId, name, email, roles));
    }
}
```

### 5.8 Program.cs Completo

**`src/Services/Identity/OrderFlow.Identity.Api/Program.cs`**

```csharp
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Identity.Api.Data;
using OrderFlow.Identity.Api.Models;
using OrderFlow.Identity.Api.Services;
using OrderFlow.SharedKernel.Auth;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341"));

// JWT Settings
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

// DbContext
var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

// ASP.NET Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password policy
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppIdentityDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
        ClockSkew = TimeSpan.Zero // Sem tolerância de expiração
    };
});

// Authorization Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("CustomerOrAdmin", policy => policy.RequireRole("Customer", "Admin"));

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Limitar tentativas de login
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // Rate limit geral
    options.AddSlidingWindowLimiter("general", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 4;
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Middleware Pipeline (ORDEM IMPORTA!)
app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();  // ANTES do Authorization
app.UseAuthorization();

app.MapControllers()
    .RequireRateLimiting("general");

// Seed de roles
// Nota: RoleExistsAsync + CreateAsync não é atômico. Se duas instâncias rodarem
// simultaneamente, ambas podem passar no check e tentar criar a mesma role.
// O try-catch garante idempotência real via constraint do banco.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    string[] roles = ["Admin", "Customer"];
    foreach (var role in roles)
    {
        if (await roleManager.RoleExistsAsync(role))
            continue;

        try
        {
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role, NormalizedName = role.ToUpper() });
        }
        catch (DbUpdateException)
        {
            // Outra instância criou a role entre o check e o create — OK, segue.
        }
    }
}

app.Run();

public partial class Program;
```

### 5.9 appsettings.json

> ⚠️ **NUNCA commite secrets reais no repositório.** O `Secret` abaixo é apenas um placeholder para desenvolvimento local. Em produção, use **User Secrets** (`dotnet user-secrets`), **variáveis de ambiente** ou um **vault** (Azure Key Vault, AWS Secrets Manager). Adicione `appsettings.*.json` ao `.gitignore` ou use `appsettings.Development.json` para valores locais.

**`src/Services/Identity/OrderFlow.Identity.Api/appsettings.json`**

```json
{
  "ConnectionStrings": {
    "IdentityDb": "Server=localhost,1433;Database=OrderFlow_Identity;User Id=sa;Password=YourStr0ng!Pass;TrustServerCertificate=True"
  },
  "JwtSettings": {
    "Secret": "REPLACE-WITH-ENV-VAR-OR-USER-SECRETS-IN-REAL-PROJECTS",
    "Issuer": "OrderFlow.Identity",
    "Audience": "OrderFlow",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

### 5.10 Configurando JWT Validation nos Outros Serviços

Para o **Orders API** validar tokens emitidos pela Identity API, adicione a mesma configuração JWT:

**Adicione ao `Program.cs` do Orders API:**

```csharp
// Em src/Services/Orders/OrderFlow.Orders.Api/Program.cs

// Adicionar pacote: dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

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

// No pipeline:
app.UseAuthentication();
app.UseAuthorization();

// Nos controllers:
// [Authorize] ou [Authorize(Policy = "CustomerOrAdmin")]
```

---

## 6. Testes

### 6.1 Testes de Integração do Auth

**`tests/OrderFlow.Identity.Api.Tests/AuthControllerTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Identity.Api.Data;
using OrderFlow.Identity.Api.DTOs;
using OrderFlow.Identity.Api.Models;

namespace OrderFlow.Identity.Api.Tests;

public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace SQL Server com InMemory para testes
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<AppIdentityDbContext>(options =>
                    options.UseInMemoryDatabase("TestIdentityDb_" + Guid.NewGuid()));
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidRequest_ReturnsAuthResponse()
    {
        var request = new RegisterRequest("John Doe", "john@test.com", "Test@1234", "Test@1234");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.User.Email.Should().Be("john@test.com");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var request = new RegisterRequest("Jane", "jane@test.com", "Test@1234", "Test@1234");
        await _client.PostAsJsonAsync("/api/auth/register", request);

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        var registerRequest = new RegisterRequest("User", "login@test.com", "Test@1234", "Test@1234");
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest("login@test.com", "Test@1234");
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var registerRequest = new RegisterRequest("User", "wrong@test.com", "Test@1234", "Test@1234");
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest("wrong@test.com", "WrongPassword1!");
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokens()
    {
        var registerRequest = new RegisterRequest("User", "refresh@test.com", "Test@1234", "Test@1234");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var refreshRequest = new RefreshTokenRequest(auth!.AccessToken, auth.RefreshToken);
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        newAuth!.AccessToken.Should().NotBe(auth.AccessToken);
        newAuth.RefreshToken.Should().NotBe(auth.RefreshToken); // Rotação!
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUserInfo()
    {
        var registerRequest = new RegisterRequest("User", "me@test.com", "Test@1234", "Test@1234");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

---

## 7. Checkpoint

### Validação Completa

- [ ] **Projeto criado:** `OrderFlow.Identity.Api`
- [ ] **ASP.NET Identity configurado:** Users, Roles, password policy, lockout
- [ ] **JWT geração:** Access token com claims corretas
- [ ] **Refresh tokens:** Rotação, revogação, detecção de reuso
- [ ] **Endpoints funcionais:** register, login, refresh, logout, me
- [ ] **Rate limiting:** 10 req/min em endpoints de auth
- [ ] **CORS configurado:** Origins específicos
- [ ] **Authorization Policies:** AdminOnly, CustomerOrAdmin
- [ ] **Orders API integrado:** JWT validation configurada
- [ ] **Roles seeded:** Admin e Customer criados no startup
- [ ] **Validation:** FluentValidation nos requests
- [ ] **Testes passam:** `dotnet test tests/OrderFlow.Identity.Api.Tests`
- [ ] **Migration:** `dotnet ef migrations add InitialIdentity -p src/Services/Identity/OrderFlow.Identity.Api`
- [ ] **Commit:** `feat(identity): implement JWT auth with refresh token rotation and rate limiting`

### Comandos de Verificação

```bash
# Build
dotnet build src/Services/Identity/OrderFlow.Identity.Api

# Testes
dotnet test tests/OrderFlow.Identity.Api.Tests --verbosity normal

# Rodar
dotnet run --project src/Services/Identity/OrderFlow.Identity.Api

# Testar registro
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Admin","email":"admin@orderflow.com","password":"Admin@1234","confirmPassword":"Admin@1234"}'

# Testar login
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@orderflow.com","password":"Admin@1234"}'
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| Identity Model | `ApplicationUser.cs` (extends IdentityUser) |
| DbContext | `IdentityDbContext.cs` + migrations |
| JWT Service | `JwtTokenService.cs` (geração de access + refresh token) |
| Auth Endpoints | `AuthController.cs` ou Minimal API (Register, Login, Refresh, Revoke) |
| Policies | `OrderOwnerPolicy`, `AdminPolicy` (claims-based) |
| Rate Limiter | `RateLimitingConfig.cs` (fixed window + sliding window) |
| CORS Config | `CorsConfig.cs` ou Program.cs |
| Middleware | `ExceptionHandlingMiddleware.cs` (global error handling) |
| Testes | `JwtTokenServiceTests.cs`, `AuthEndpointTests.cs` |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 4

**1. "JWT vs Sessions — quando usar cada um?"**
— **JWT** é stateless: o token carrega claims, não precisa de armazenamento no servidor. Ideal para APIs consumidas por múltiplos clients (SPA, mobile, outros serviços). **Sessions** são stateful: requerem persistência (Redis, banco). Vantagem de sessions: revogação instantânea. Vantagem de JWT: escala horizontal sem shared state. No OrderFlow usamos JWT + Refresh Token para ter o melhor dos dois mundos.

**2. "Como funciona Refresh Token rotation e por que é importante?"**
— Cada refresh token só pode ser usado **uma vez**. Ao usar, gera-se um novo par (access + refresh). Se um atacante roubar o refresh token e tentar usá-lo após o usuário legítimo, a rotação detecta **token reuse** e revoga toda a família. Sem rotação, um refresh token roubado dá acesso indefinido.

**3. "Qual a diferença entre Authentication e Authorization?"**
— **Authentication** = "quem é você?" (valida credenciais, gera token). **Authorization** = "o que você pode fazer?" (verifica roles/claims/policies). No ASP.NET Core, `[Authorize]` exige autenticação; `[Authorize(Policy = "OrderOwner")]` exige autenticação **E** autorização específica. Policies são preferíveis a `[Authorize(Roles = "Admin")]` porque são mais flexíveis e testáveis.

**4. "Como implementar Rate Limiting no ASP.NET Core?"**
— O .NET 7+ tem Rate Limiting nativo via `Microsoft.AspNetCore.RateLimiting`. Configuramos políticas: **Fixed Window** (X requests por janela fixa), **Sliding Window** (janela deslizante), **Token Bucket** (taxa constante com burst). Aplica-se via `[EnableRateLimiting("policy")]` ou globalmente. Para APIs públicas, use por IP; para autenticadas, por usuário/claim.

**5. "Por que não armazenar secrets em appsettings.json?"**
— Porque `appsettings.json` entra no repositório. Secrets (connection strings, JWT signing key) devem vir de: **User Secrets** (dev), **Environment Variables** (containers), **Azure Key Vault** (produção). O ASP.NET Core faz merge de configuração: appsettings < env vars < Key Vault, então secrets de produção sobrescrevem sem alterar código.

---

> **Próximo passo:** Avance para `fase-05-mensageria-async.md` para implementar RabbitMQ com MassTransit, Outbox Pattern e o Notification Worker.
