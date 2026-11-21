using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApprovalFlow.Application.Auth.Dtos;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApprovalFlow.Tests.Api.Integration;

public class SessionValidationTests : IClassFixture<ApiFactory>
{
    private const string SessionInvalidDetail = "La sesión ya no es válida. Inicia sesión de nuevo.";
    private const string InvalidTokenDetail = "Se requiere un token válido.";

    private readonly ApiFactory _factory;

    public SessionValidationTests(ApiFactory factory) => _factory = factory;

    private static string NewEmail() => $"{Guid.NewGuid():N}@empresa.com";

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response, string detail)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("No autenticado", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(detail, json.RootElement.GetProperty("detail").GetString());
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> AdminClientAsync()
        => _factory.CreateClientFor(await _factory.SeedUserAsync(UserRole.Admin, NewEmail()));

    // Sesion valida: todo sigue igual

    [Fact]
    public async Task UsuarioActivoSinCambios_ConservaRespuestasPorRol()
    {
        var requester = _factory.CreateClientFor(await _factory.SeedUserAsync(UserRole.Requester, NewEmail()));
        var admin = await AdminClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await requester.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await requester.GetAsync("/api/departments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await requester.GetAsync("/api/purchase-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await requester.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/users")).StatusCode);
    }

    // Sesion invalidada

    [Fact]
    public async Task UsuarioDesactivado_TokenPrevio_Devuelve401ConSesionInvalida()
    {
        var user = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());
        var userClient = _factory.CreateClientFor(user);
        var admin = await AdminClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/users/me")).StatusCode);

        var deactivate = await admin.PostAsync($"/api/users/{user.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        await AssertUnauthorizedAsync(await userClient.GetAsync("/api/users/me"), SessionInvalidDetail);
    }

    [Fact]
    public async Task CambioDeRol_TokenPrevio_Devuelve401ConSesionInvalida()
    {
        var user = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());
        var userClient = _factory.CreateClientFor(user);
        var admin = await AdminClientAsync();

        var change = await admin.PutAsJsonAsync(
            $"/api/users/{user.Id}/role", new { role = UserRole.Approver, departmentId = user.DepartmentId });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        await AssertUnauthorizedAsync(await userClient.GetAsync("/api/users/me"), SessionInvalidDetail);
    }

    [Fact]
    public async Task CambioDeDepartamentoEnBd_TokenPrevio_Devuelve401ConSesionInvalida()
    {
        var user = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());
        var userClient = _factory.CreateClientFor(user);
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/users/me")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var other = Department.Create($"Depto {Guid.NewGuid():N}", null);
            context.Departments.Add(other);
            var saved = await context.Users.SingleAsync(u => u.Id == user.Id);
            saved.AssignDepartment(other);
            await context.SaveChangesAsync();
        }

        await AssertUnauthorizedAsync(await userClient.GetAsync("/api/users/me"), SessionInvalidDetail);
    }

    [Fact]
    public async Task UsuarioInexistente_TokenBienFirmado_Devuelve401ConSesionInvalida()
    {
        var ghost = User.Create("Fantasma", NewEmail(), "hash", UserRole.Admin, null);
        var client = ClientWithToken(_factory.CreateToken(ghost));

        await AssertUnauthorizedAsync(await client.GetAsync("/api/users/me"), SessionInvalidDetail);
    }

    [Fact]
    public async Task ApproverDegradadoARequester_TokenViejo_Devuelve401EnApprove()
    {
        var approver = await _factory.SeedUserAsync(UserRole.Approver, NewEmail());
        var oldClient = _factory.CreateClientFor(approver);
        var admin = await AdminClientAsync();

        var change = await admin.PutAsJsonAsync(
            $"/api/users/{approver.Id}/role", new { role = UserRole.Requester, departmentId = approver.DepartmentId });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var response = await oldClient.PostAsJsonAsync(
            $"/api/purchase-requests/{Guid.NewGuid()}/approve", new { comments = "ok" });

        await AssertUnauthorizedAsync(response, SessionInvalidDetail);
    }

    [Fact]
    public async Task TrasCambiarElRol_NuevoLogin_DevuelveTokenValido()
    {
        var email = NewEmail();
        var user = await _factory.SeedUserAsync(UserRole.Requester, email);
        var admin = await AdminClientAsync();
        var change = await admin.PutAsJsonAsync(
            $"/api/users/{user.Id}/role", new { role = UserRole.Approver, departmentId = user.DepartmentId });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = ApiFactory.UserPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;

        var me = await ClientWithToken(token).GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_ConBearerViejoDeUsuarioDesactivado_FuncionaIgual()
    {
        var email = NewEmail();
        var user = await _factory.SeedUserAsync(UserRole.Requester, email);
        var oldToken = _factory.CreateToken(user);
        var admin = await AdminClientAsync();
        await admin.PostAsync($"/api/users/{user.Id}/deactivate", null);
        await admin.PostAsync($"/api/users/{user.Id}/activate", null);

        var response = await ClientWithToken(oldToken).PostAsJsonAsync(
            "/api/auth/login", new { email, password = ApiFactory.UserPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Fallos de token normales: detail sin cambios

    [Fact]
    public async Task SinToken_Devuelve401ConDetailDeTokenInvalido()
    {
        await AssertUnauthorizedAsync(await _factory.CreateClient().GetAsync("/api/users/me"), InvalidTokenDetail);
    }

    [Fact]
    public async Task TokenMalFormado_Devuelve401ConDetailDeTokenInvalido()
    {
        var client = ClientWithToken("esto-no-es-un-jwt");

        await AssertUnauthorizedAsync(await client.GetAsync("/api/users/me"), InvalidTokenDetail);
    }

    [Fact]
    public async Task TokenExpirado_Devuelve401ConDetailDeTokenInvalido()
    {
        var user = await _factory.SeedUserAsync(UserRole.Admin, NewEmail());
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ApiFactory.JwtIssuer,
            Audience = ApiFactory.JwtAudience,
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = DateTime.UtcNow.AddHours(-1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtKey)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["role"] = user.Role.ToString()
            }
        };
        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        await AssertUnauthorizedAsync(await ClientWithToken(token).GetAsync("/api/users/me"), InvalidTokenDetail);
    }

    // El Admin que actua sobre otros conserva su sesion

    [Fact]
    public async Task AdminQueDesactivaOCambiaRolAOtro_SigueUsandoSuToken()
    {
        var admin = await AdminClientAsync();
        var target = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());
        var other = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/users/{target.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/users/me")).StatusCode);

        var change = await admin.PutAsJsonAsync(
            $"/api/users/{other.Id}/role", new { role = UserRole.Approver, departmentId = other.DepartmentId });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/users/me")).StatusCode);
    }

    // IUserSessionUseCase se resuelve desde el contenedor

    [Fact]
    public void IUserSessionUseCase_SeResuelveDesdeElContenedor()
    {
        using var scope = _factory.Services.CreateScope();

        var useCase = scope.ServiceProvider.GetService<ApprovalFlow.Application.Abstractions.UseCases.IUserSessionUseCase>();

        Assert.NotNull(useCase);
    }
}
