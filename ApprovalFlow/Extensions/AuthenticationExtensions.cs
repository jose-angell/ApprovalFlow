using System.Text;
using ApprovalFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApprovalFlow.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Se configura con IOptions<JwtOptions> (ya validado en AddInfrastructure)
        // para leer la configuración final, incluida la de WebApplicationFactory.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "No autenticado",
                            "Se requiere un token válido.");
                    },
                    OnForbidden = context => WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Acceso denegado",
                        "No tienes permisos para este recurso.")
                };
            });

        return services;
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string title, string detail)
    {
        httpContext.Response.StatusCode = status;

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            }
        });
    }
}
