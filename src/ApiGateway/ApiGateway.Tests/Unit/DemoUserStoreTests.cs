using ApiGateway.API.Services;

namespace ApiGateway.Tests.Unit;

public class DemoUserStoreTests
{
    private readonly DemoUserStore _sut = new();

    [Fact]
    public void Validate_WithCorrectAdminCredentials_ReturnsAdminUser()
    {
        var user = _sut.Validate("admin", "Admin123!");

        Assert.NotNull(user);
        Assert.Equal("admin", user.Username);
        Assert.Equal("Admin", user.Role);
    }

    [Fact]
    public void Validate_WithCorrectCustomerCredentials_ReturnsCustomerUser()
    {
        var user = _sut.Validate("customer", "Customer123!");

        Assert.NotNull(user);
        Assert.Equal("Customer", user.Role);
    }

    [Fact]
    public void Validate_UsernameIsCaseInsensitive_StillMatches()
    {
        var user = _sut.Validate("ADMIN", "Admin123!");

        Assert.NotNull(user);
    }

    [Fact]
    public void Validate_WithWrongPassword_ReturnsNull()
    {
        var user = _sut.Validate("admin", "wrong-password");

        Assert.Null(user);
    }

    [Fact]
    public void Validate_WithUnknownUsername_ReturnsNull()
    {
        var user = _sut.Validate("nobody", "whatever");

        Assert.Null(user);
    }
}
