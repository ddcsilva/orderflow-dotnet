namespace OrderFlow.Identity.Api.DTOs;

public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);
