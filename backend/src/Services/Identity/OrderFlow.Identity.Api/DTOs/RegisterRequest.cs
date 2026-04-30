namespace OrderFlow.Identity.Api.DTOs;

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword);
