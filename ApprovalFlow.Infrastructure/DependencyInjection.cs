using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Infrastructure.Persistence;
using ApprovalFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;

namespace ApprovalFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "La connection string 'DefaultConnection' no está configurada. " +
                "Defínela en appsettings.Development.json o en la configuración del entorno.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer)
                     && !string.IsNullOrWhiteSpace(o.Audience)
                     && !string.IsNullOrWhiteSpace(o.Key)
                     && Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinKeyBytes
                     && o.ExpirationMinutes > 0,
                "La sección 'Jwt' es inválida: Issuer, Audience y Key son obligatorios, " +
                "Key debe tener al menos 32 bytes y ExpirationMinutes debe ser mayor que 0.")
            .ValidateOnStart();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
