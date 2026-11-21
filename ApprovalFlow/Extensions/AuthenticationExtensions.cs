using System.Security.Claims;
using System.Text;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApprovalFlow.Api.Extensions;

public static class AuthenticationExtensions
{
    private const string SessionInvalidItemKey = "approvalflow:session-invalid";

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
                    OnTokenValidated = ValidateSessionAsync,
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var sessionInvalid = context.HttpContext.Items.ContainsKey(SessionInvalidItemKey);
                        await WriteProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "No autenticado",
                            sessionInvalid
                                ? "La sesión ya no es válida. Inicia sesión de nuevo."
                                : "Se requiere un token válido.");
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

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;

        Guid? userId = Guid.TryParse(principal?.FindFirstValue("sub"), out var parsedUserId) ? parsedUserId : null;
        var hasRole = Enum.TryParse<UserRole>(principal?.FindFirstValue("role"), out var role);

        var departmentClaim = principal?.FindFirstValue("department_id");
        Guid? departmentId = null;
        var departmentValid = true;
        if (departmentClaim is not null)
        {
            if (Guid.TryParse(departmentClaim, out var parsedDepartmentId))
                departmentId = parsedDepartmentId;
            else
                departmentValid = false;
        }

        var valid = false;
        if (userId is not null && hasRole && departmentValid)
        {
            var sessionUseCase = context.HttpContext.RequestServices.GetRequiredService<IUserSessionUseCase>();
            valid = await sessionUseCase.ValidateAsync(
                userId.Value, role, departmentId, context.HttpContext.RequestAborted);
        }

        if (valid)
            return;

        context.HttpContext.Items[SessionInvalidItemKey] = true;

        context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AuthenticationExtensions).FullName!)
            .LogInformation("Sesión inválida para el usuario {UserId}.", userId);

        context.Fail("La sesión ya no es válida.");
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
