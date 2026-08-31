using ApiGateway.API.Contracts;
using ApiGateway.API.Controllers;
using ApiGateway.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace ApiGateway.Tests.Unit;

public class AuthControllerTests
{
    private readonly Mock<IUserStore> _userStore = new();
    private readonly Mock<IJwtTokenService> _tokenService = new();
    private readonly JwtOptions _jwtOptions = new()
    {
        SigningKey = "unit-test-signing-key-at-least-32-bytes-long-000000",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        ExpirationMinutes = 60,
    };
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _sut = new AuthController(_userStore.Object, _tokenService.Object, Options.Create(_jwtOptions));
    }

    [Fact]
    public void Login_WithValidCredentials_ReturnsTokenAndRole()
    {
        var user = new DemoUser("admin", "hash", "Admin");
        _userStore.Setup(s => s.Validate("admin", "Admin123!")).Returns(user);
        _tokenService.Setup(t => t.CreateToken(user)).Returns("fake-jwt-token");

        var result = _sut.Login(new LoginRequest("admin", "Admin123!"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal("fake-jwt-token", response.AccessToken);
        Assert.Equal("Admin", response.Role);
        Assert.Equal(60, response.ExpiresInMinutes);
    }

    [Fact]
    public void Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        _userStore.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<string>())).Returns((DemoUser?)null);

        var result = _sut.Login(new LoginRequest("admin", "wrong-password"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        _tokenService.Verify(t => t.CreateToken(It.IsAny<DemoUser>()), Times.Never);
    }
}
