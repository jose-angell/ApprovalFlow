using Microsoft.OpenApi;

namespace ApprovalFlow.Api.Extensions;

public static class SwaggerExtensions
{
    private const string BearerSchemeName = "Bearer";

    public static IServiceCollection AddSwaggerWithBearer(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Pega el token JWT obtenido en POST /api/auth/login."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = []
            });
        });

        return services;
    }
}
