using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderFlow.Identity.Api.Data;
using OrderFlow.Identity.Api.DTOs;
using OrderFlow.Identity.Api.Models;
using OrderFlow.SharedKernel.Auth;
using OrderFlow.SharedKernel.Common;

namespace OrderFlow.Identity.Api.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    AppIdentityDbContext dbContext,
    IOptions<JwtSettings> jwtSettings,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return Result<AuthResponse>.Failure(
                new Error("Auth.EmailTaken", "An account with this email already exists."));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            UserName = normalizedEmail
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
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidCredentials", "Invalid email or password."));

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Locked out user attempted login: {Email}", request.Email);
            return Result<AuthResponse>.Failure(
                new Error("Auth.LockedOut", "Account temporarily locked. Try again later."));
        }

        var isValidPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidCredentials", "Invalid email or password."));
        }

        await userManager.ResetAccessFailedCountAsync(user);
        logger.LogInformation("User logged in: {Email}", user.Email);

        return await GenerateAuthResponse(user);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var principal = tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidToken", "Invalid access token."));

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Result<AuthResponse>.Failure(
                new Error("Auth.InvalidToken", "Invalid access token."));

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

        existingToken.RevokedAt = DateTime.UtcNow;
        var (newRefreshToken, rawValue) = tokenService.GenerateRefreshToken(userId);
        existingToken.ReplacedByToken = newRefreshToken.Token;
        dbContext.RefreshTokens.Add(newRefreshToken);

        await dbContext.SaveChangesAsync();

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            rawValue,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
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

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            rawValue,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            new UserDto(user.Id, user.FullName, user.Email!, roles.ToList())));
    }
}
