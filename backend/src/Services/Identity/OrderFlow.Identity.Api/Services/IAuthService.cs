using OrderFlow.Identity.Api.DTOs;
using OrderFlow.SharedKernel.Common;

namespace OrderFlow.Identity.Api.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<Result> RevokeRefreshTokenAsync(Guid userId);
}
