using System.ComponentModel.DataAnnotations;

namespace ApiGateway.API.Contracts;

public sealed record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresInMinutes, string Role);
