using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApprovalFlow.Tests.Api.Integration;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string UserPassword = "Secreta123";
    public const string JwtKey = "clave-de-integracion-con-mas-de-32-bytes!!";
    public const string JwtIssuer = "ApprovalFlow.Tests";
    public const string JwtAudience = "ApprovalFlow.Tests";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting entra en la configuracion del host antes de que Program registre los servicios.
        var settings = new Dictionary<string, string>
        {
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
            ["Jwt:Key"] = JwtKey,
            ["Jwt:ExpirationMinutes"] = "60",
            ["ConnectionStrings:DefaultConnection"] = "Host=ficticio;Database=ficticio",
            ["SeedAdmin:Email"] = "",
            ["SeedAdmin:Password"] = ""
        };
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            // EF Core 10: hay que quitar tanto las opciones como la configuracion de opciones del proveedor.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task<User> SeedUserAsync(
        UserRole role, string email = "usuario@empresa.com", bool active = true, string password = UserPassword,
        Guid? departmentId = null)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        Department? department = null;
        if (departmentId is not null)
        {
            // Departamento compartido: permite sembrar varios usuarios en el mismo departamento.
            department = await context.Departments.SingleAsync(d => d.Id == departmentId);
        }
        else if (role != UserRole.Admin)
        {
            department = Department.Create($"Depto {Guid.NewGuid():N}", null);
            context.Departments.Add(department);
        }

        var user = User.Create("Usuario de prueba", email, hasher.Hash(password), role, department);
        if (!active)
            user.Deactivate();

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public string CreateToken(User user)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().Generate(user).Token;
    }

    public HttpClient CreateClientFor(User user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(user));
        return client;
    }
}
