using Gateway.API.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Gateway.API.Controllers;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, DateTime ExpiresAtUtc);

[ApiController]
[Route("auth")]
public class AuthController(
    IOptions<FixedUserOptions> fixedUser,
    IJwtTokenService tokenService,
    ITokenCacheService tokenCache) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = fixedUser.Value;

        if (request.Email != user.Email || request.Password != user.Password)
        {
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        var generated = tokenService.GenerateToken(request.Email);
        await tokenCache.StoreActiveTokenAsync(generated.Jti, generated.ExpiresAtUtc);

        return Ok(new LoginResponse(generated.Token, generated.ExpiresAtUtc));
    }
}
