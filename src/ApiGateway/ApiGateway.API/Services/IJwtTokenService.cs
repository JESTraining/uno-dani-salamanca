namespace ApiGateway.API.Services;

public interface IJwtTokenService
{
    string CreateToken(DemoUser user);
}
