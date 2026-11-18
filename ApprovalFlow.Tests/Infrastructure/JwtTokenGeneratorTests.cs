using System.Text;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ApprovalFlow.Tests.Infrastructure;

public class JwtTokenGeneratorTests
{
    private static readonly JwtOptions Config = new()
    {
        Issuer = "ApprovalFlow.Tests",
        Audience = "ApprovalFlow.Tests.Client",
        Key = "clave-de-pruebas-con-al-menos-32-bytes!!",
        ExpirationMinutes = 45
    };

    private readonly JwtTokenGenerator _generator = new(MsOptions.Create(Config));

    private static User NewUser(UserRole role = UserRole.Admin, Department? department = null) =>
        User.Create("Ana Pérez", "ana@empresa.com", "hash", role, department);

    private static Task<TokenValidationResult> ValidateAsync(string token, string? key = null) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Config.Issuer,
            ValidAudience = Config.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Config.Key)),
            ValidateLifetime = true
        });

    [Fact]
    public async Task Generate_ProduceTokenValidoConIssuerAudienceYKeyConfigurados()
    {
        var result = _generator.Generate(NewUser());

        var validation = await ValidateAsync(result.Token);

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task Generate_ConOtraKey_ProduceTokenInvalido()
    {
        var result = _generator.Generate(NewUser());

        var validation = await ValidateAsync(result.Token, "otra-clave-distinta-con-32-bytes-o-mas!!");

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Generate_IncluyeLosClaimsEsperados()
    {
        var user = NewUser(UserRole.Admin);

        var token = new JsonWebToken(_generator.Generate(user).Token);

        Assert.Equal(user.Id.ToString(), token.GetClaim("sub").Value);
        Assert.Equal("ana@empresa.com", token.GetClaim("email").Value);
        Assert.Equal("Ana Pérez", token.GetClaim("name").Value);
        Assert.Equal("Admin", token.GetClaim("role").Value);
        Assert.True(Guid.TryParse(token.GetClaim("jti").Value, out _));
    }

    [Fact]
    public void Generate_ConUsuarioSinDepartamento_NoIncluyeDepartmentId()
    {
        var token = new JsonWebToken(_generator.Generate(NewUser(UserRole.Admin)).Token);

        Assert.False(token.TryGetClaim("department_id", out _));
    }

    [Fact]
    public void Generate_ConUsuarioConDepartamento_IncluyeDepartmentId()
    {
        var department = Department.Create("Compras", null);
        var user = NewUser(UserRole.Requester, department);

        var token = new JsonWebToken(_generator.Generate(user).Token);

        Assert.Equal(department.Id.ToString(), token.GetClaim("department_id").Value);
    }

    [Fact]
    public void Generate_ExpiresAtEsAhoraMasExpirationMinutes()
    {
        var before = DateTime.UtcNow;

        var result = _generator.Generate(NewUser());

        var after = DateTime.UtcNow;
        Assert.InRange(result.ExpiresAt,
            before.AddMinutes(Config.ExpirationMinutes),
            after.AddMinutes(Config.ExpirationMinutes));
    }

    [Fact]
    public void Generate_DosVecesProduceJtiDistintos()
    {
        var user = NewUser();

        var first = new JsonWebToken(_generator.Generate(user).Token).GetClaim("jti").Value;
        var second = new JsonWebToken(_generator.Generate(user).Token).GetClaim("jti").Value;

        Assert.NotEqual(first, second);
    }
}
