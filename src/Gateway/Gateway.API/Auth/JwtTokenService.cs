using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.API.Auth;

public record GeneratedToken(string Token, string Jti, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    GeneratedToken GenerateToken(string userEmail);
}

public class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    public GeneratedToken GenerateToken(string userEmail)
    {
        var opts = options.Value;
        var jti = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(opts.ExpirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userEmail),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new GeneratedToken(new JwtSecurityTokenHandler().WriteToken(token), jti, expiresAt);
    }
}
