using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;

namespace Gateway.API.Auth;

public class ActiveTokenRequirement : IAuthorizationRequirement;

public class ActiveTokenAuthorizationHandler(ITokenCacheService tokenCache) : AuthorizationHandler<ActiveTokenRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveTokenRequirement requirement)
    {
        var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (!string.IsNullOrEmpty(jti) && await tokenCache.IsTokenActiveAsync(jti))
        {
            context.Succeed(requirement);
        }
    }
}
