namespace ApiGateway.API.Services;

public interface IUserStore
{
    /// Returns the user if found and the password matches, null otherwise.
    DemoUser? Validate(string username, string password);
}
