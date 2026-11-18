using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Tests.Api.Integration;

public class AuthControllerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthControllerTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_ConCredencialesCorrectas_Devuelve200ConTokenYUsuarioSinPasswordHash()
    {
        await _factory.SeedUserAsync(UserRole.Admin, "login.ok@empresa.com");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "login.ok@empresa.com", password = ApiFactory.UserPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("accessToken").GetString()));
        Assert.True(root.TryGetProperty("expiresAt", out _));
        var user = root.GetProperty("user");
        Assert.Equal("login.ok@empresa.com", user.GetProperty("email").GetString());
        Assert.False(user.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_Devuelve401ProblemDetails()
    {
        await _factory.SeedUserAsync(UserRole.Admin, "login.mala@empresa.com");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "login.mala@empresa.com", password = "Incorrecta999" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("No autenticado", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_ConUsuarioInactivo_Devuelve401()
    {
        await _factory.SeedUserAsync(UserRole.Admin, "login.inactivo@empresa.com", active: false);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "login.inactivo@empresa.com", password = ApiFactory.UserPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_SinEmail_Devuelve400ValidationProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Email", out _));
    }
}
