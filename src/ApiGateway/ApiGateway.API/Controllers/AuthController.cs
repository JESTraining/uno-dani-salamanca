using ApiGateway.API.Contracts;
using ApiGateway.API.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApiGateway.API.Controllers;

/// Minimal demo login (Phase 5) - see DemoUser.cs for why this exists
/// instead of a real identity system. Two seeded accounts: admin/Admin123!
/// (role Admin) and customer/Customer123! (role Customer).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserStore _userStore;
    private readonly IJwtTokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(IUserStore userStore, IJwtTokenService tokenService, IOptions<JwtOptions> jwtOptions)
    {
        _userStore = userStore;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        var user = _userStore.Validate(request.Username, request.Password);
        if (user is null)
            return Unauthorized(new { status = 401, title = "Invalid username or password." });

        var token = _tokenService.CreateToken(user);
        return Ok(new LoginResponse(token, "Bearer", _jwtOptions.ExpirationMinutes, user.Role));
    }
}
