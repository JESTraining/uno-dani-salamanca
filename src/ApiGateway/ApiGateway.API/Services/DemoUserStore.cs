using Microsoft.AspNetCore.Identity;

namespace ApiGateway.API.Services;

public class DemoUserStore : IUserStore
{
    private readonly PasswordHasher<DemoUser> _hasher = new();
    private readonly IReadOnlyDictionary<string, DemoUser> _usersByUsername;

    public DemoUserStore()
    {
        _usersByUsername = BuildDemoUsers().ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
    }

    public DemoUser? Validate(string username, string password)
    {
        if (!_usersByUsername.TryGetValue(username, out var user))
            return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Success ? user : null;
    }

    private IEnumerable<DemoUser> BuildDemoUsers()
    {
        yield return Seed("admin", "Admin123!", "Admin");
        yield return Seed("customer", "Customer123!", "Customer");
    }

    private DemoUser Seed(string username, string plainTextPassword, string role)
    {
        var user = new DemoUser(username, PasswordHash: string.Empty, role);
        var hash = _hasher.HashPassword(user, plainTextPassword);
        return user with { PasswordHash = hash };
    }
}
