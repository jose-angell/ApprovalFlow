using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApprovalFlow.Application.Users.Dtos;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Security;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ApprovalFlow.Tests.Api.Integration;

public class UsersControllerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UsersControllerTests(ApiFactory factory) => _factory = factory;

    private static string NewEmail() => $"{Guid.NewGuid():N}@empresa.com";

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(title, json.RootElement.GetProperty("title").GetString());
    }

    // 401

    [Fact]
    public async Task Users_SinToken_Devuelve401ProblemDetails()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autenticado");
    }

    [Fact]
    public async Task Users_ConTokenInvalido_Devuelve401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "no.es.un.token");

        var response = await client.GetAsync("/api/users/me");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autenticado");
    }

    [Fact]
    public async Task Users_ConTokenExpirado_Devuelve401()
    {
        var admin = await _factory.SeedUserAsync(UserRole.Admin, NewEmail());
        var options = new JwtOptions
        {
            Issuer = ApiFactory.JwtIssuer,
            Audience = ApiFactory.JwtAudience,
            Key = ApiFactory.JwtKey,
            ExpirationMinutes = -10
        };
        var expired = new JwtTokenGenerator(MsOptions.Create(options)).Generate(admin).Token;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // 403 por rol

    [Fact]
    public async Task Users_EndpointAdminConTokenDeRequester_Devuelve403ProblemDetails()
    {
        var requester = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());
        var client = _factory.CreateClientFor(requester);

        var response = await client.GetAsync("/api/users");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Acceso denegado");
    }

    [Fact]
    public async Task Users_GetAllConTokenDeAdmin_Devuelve200()
    {
        var admin = await _factory.SeedUserAsync(UserRole.Admin, NewEmail());
        var client = _factory.CreateClientFor(admin);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // /me

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Approver)]
    [InlineData(UserRole.Finance)]
    public async Task Me_ConTokenDeCualquierRol_Devuelve200ConElUsuarioDelToken(UserRole role)
    {
        var user = await _factory.SeedUserAsync(role, NewEmail());
        var client = _factory.CreateClientFor(user);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(user.Id, dto!.Id);
    }

    [Fact]
    public async Task MePassword_Correcto_Devuelve204YElLoginFuncionaConLaNueva()
    {
        var email = NewEmail();
        var user = await _factory.SeedUserAsync(UserRole.Requester, email);
        var client = _factory.CreateClientFor(user);

        var response = await client.PutAsJsonAsync("/api/users/me/password",
            new { currentPassword = ApiFactory.UserPassword, newPassword = "NuevaClave456" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "NuevaClave456" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = ApiFactory.UserPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task MePassword_ConContrasenaActualIncorrecta_Devuelve400()
    {
        var user = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());
        var client = _factory.CreateClientFor(user);

        var response = await client.PutAsJsonAsync("/api/users/me/password",
            new { currentPassword = "Incorrecta999", newPassword = "NuevaClave456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Crear

    [Fact]
    public async Task Create_ComoAdmin_Devuelve201ConLocationHaciaGetById()
    {
        var admin = await _factory.SeedUserAsync(UserRole.Admin, NewEmail());
        var client = _factory.CreateClientFor(admin);

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Nuevo Admin",
            email = NewEmail(),
            password = "Clave-Segura-123",
            role = (int)UserRole.Admin
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.EndsWith($"/api/users/{created!.Id}", response.Headers.Location!.AbsolutePath);
        var get = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Create_ConPasswordDe3Caracteres_Devuelve400ValidationProblemDetails()
    {
        var admin = await _factory.SeedUserAsync(UserRole.Admin, NewEmail());
        var client = _factory.CreateClientFor(admin);

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Nuevo Admin",
            email = NewEmail(),
            password = "abc",
            role = (int)UserRole.Admin
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Password", out _));
    }

    // Autoproteccion

    [Fact]
    public async Task Deactivate_SobreElPropioAdmin_Devuelve403()
    {
        var admin = await _factory.SeedUserAsync(UserRole.Admin, NewEmail());
        var client = _factory.CreateClientFor(admin);

        var response = await client.PostAsync($"/api/users/{admin.Id}/deactivate", null);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Acceso denegado");
    }

    [Fact]
    public async Task Deactivate_SobreOtroUsuario_Devuelve204()
    {
        var admin = await _factory.SeedUserAsync(UserRole.Admin, NewEmail());
        var other = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());
        var client = _factory.CreateClientFor(admin);

        var response = await client.PostAsync($"/api/users/{other.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
