namespace ApiGateway.API.Services;

/// Bound from the "Jwt" configuration section, shared (same signing key,
/// issuer, and audience) with every downstream service that validates
/// tokens the Gateway issues.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int ExpirationMinutes { get; init; } = 60;
}
