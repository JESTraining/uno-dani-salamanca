using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApiGateway.API.Services;
using Microsoft.Extensions.Options;

namespace ApiGateway.Tests.Unit;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(JwtOptions? options = null)
    {
        options ??= new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long-000000",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 30,
        };
        return new JwtTokenService(Options.Create(options));
    }

    [Fact]
    public void CreateToken_IncludesUsernameAsSubjectClaim()
    {
        var service = CreateService();
        var user = new DemoUser("admin", "irrelevant-hash", "Admin");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));

        Assert.Equal("admin", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public void CreateToken_IncludesRoleClaim()
    {
        var service = CreateService();
        var user = new DemoUser("admin", "irrelevant-hash", "Admin");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));

        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void CreateToken_SetsConfiguredIssuerAndAudience()
    {
        var options = new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long-000000",
            Issuer = "MyIssuer",
            Audience = "MyAudience",
            ExpirationMinutes = 30,
        };
        var service = CreateService(options);
        var user = new DemoUser("customer", "irrelevant-hash", "Customer");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));

        Assert.Equal("MyIssuer", token.Issuer);
        Assert.Contains("MyAudience", token.Audiences);
    }

    [Fact]
    public void CreateToken_SetsExpirationBasedOnConfiguredMinutes()
    {
        var options = new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long-000000",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 5,
        };
        var service = CreateService(options);
        var user = new DemoUser("admin", "irrelevant-hash", "Admin");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));

        var expectedExpiry = DateTime.UtcNow.AddMinutes(5);
        Assert.True(Math.Abs((token.ValidTo - expectedExpiry).TotalSeconds) < 30);
    }
}
