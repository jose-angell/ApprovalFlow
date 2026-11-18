using System.Text;
using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApprovalFlow.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Generate(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email,
            ["name"] = user.FullName,
            ["role"] = user.Role.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };

        if (user.DepartmentId is { } departmentId)
        {
            claims["department_id"] = departmentId.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt,
            SigningCredentials = _credentials,
            Claims = claims
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
