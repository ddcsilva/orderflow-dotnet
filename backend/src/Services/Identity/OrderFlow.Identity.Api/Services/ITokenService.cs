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
