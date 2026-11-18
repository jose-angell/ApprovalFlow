using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApprovalFlow.Infrastructure.Persistence;

public static class AdminSeeder
{
    public const string SectionName = "SeedAdmin";
    private const string DefaultFullName = "Administrador";

    public static async Task SeedAdminAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminSeeder));
        var section = provider.GetRequiredService<IConfiguration>().GetSection(SectionName);

        var email = section["Email"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SeedAdmin no configurado: no se crea ningún usuario administrador.");
            return;
        }

        var context = provider.GetRequiredService<IApplicationDbContext>();

        if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin, ct))
        {
            logger.LogInformation("Ya existe un usuario administrador: no se ejecuta el seed.");
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
        {
            logger.LogWarning(
                "No se creó el administrador inicial: el email '{Email}' ya está en uso por otro usuario.",
                normalizedEmail);
            return;
        }

        var fullName = string.IsNullOrWhiteSpace(section["FullName"]) ? DefaultFullName : section["FullName"]!;
        var passwordHash = provider.GetRequiredService<IPasswordHasher>().Hash(password);

        var admin = User.Create(fullName, email, passwordHash, UserRole.Admin, null);
        context.Users.Add(admin);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Administrador inicial '{Email}' creado.", admin.Email);
    }
}
