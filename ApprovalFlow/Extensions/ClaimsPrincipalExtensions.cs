using System.Security.Claims;
using ApprovalFlow.Application.Exceptions;

namespace ApprovalFlow.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedException("Token sin identificador de usuario.");

        return userId;
    }
}
