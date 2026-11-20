using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApprovalFlow.Application.Departments.Dtos;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Tests.Api.Integration;

public class DepartmentsControllerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DepartmentsControllerTests(ApiFactory factory) => _factory = factory;

    private static string NewEmail() => $"{Guid.NewGuid():N}@empresa.com";

    private static string NewName() => $"Depto {Guid.NewGuid():N}";

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }

    private async Task<HttpClient> AdminClientAsync()
        => _factory.CreateClientFor(await _factory.SeedUserAsync(UserRole.Admin, NewEmail()));

    private async Task<HttpClient> RequesterClientAsync()
        => _factory.CreateClientFor(await _factory.SeedUserAsync(UserRole.Requester, NewEmail()));

    private static async Task<DepartmentDto> CreateDepartmentAsync(HttpClient adminClient, string? name = null)
    {
        var response = await adminClient.PostAsJsonAsync("/api/departments",
            new { name = name ?? NewName(), description = "Descripción" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DepartmentDto>())!;
    }

    // 401

    [Fact]
    public async Task Departments_SinToken_Devuelve401ProblemDetails()
    {
        var response = await _factory.CreateClient().GetAsync("/api/departments");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    // Lectura: cualquier autenticado

    [Fact]
    public async Task GetAll_ConTokenDeRequester_Devuelve200()
    {
        var admin = await AdminClientAsync();
        var created = await CreateDepartmentAsync(admin);
        var requester = await RequesterClientAsync();

        var response = await requester.GetAsync("/api/departments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<DepartmentDto>>();
        Assert.Contains(list!, d => d.Id == created.Id);
    }

    [Fact]
    public async Task GetById_ConTokenDeRequester_Devuelve200()
    {
        var admin = await AdminClientAsync();
        var created = await CreateDepartmentAsync(admin);
        var requester = await RequesterClientAsync();

        var response = await requester.GetAsync($"/api/departments/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DepartmentDto>();
        Assert.Equal(created.Id, dto!.Id);
    }

    [Fact]
    public async Task GetById_ConIdInexistente_Devuelve404ProblemDetails()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/departments/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    // Escritura: solo Admin

    [Fact]
    public async Task Create_ConTokenDeRequester_Devuelve403ProblemDetails()
    {
        var requester = await RequesterClientAsync();

        var response = await requester.PostAsJsonAsync("/api/departments", new { name = NewName() });

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ConTokenDeRequester_Devuelve403ProblemDetails()
    {
        var admin = await AdminClientAsync();
        var created = await CreateDepartmentAsync(admin);
        var requester = await RequesterClientAsync();

        var response = await requester.PutAsJsonAsync($"/api/departments/{created.Id}", new { name = NewName() });

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task ActivateYDeactivate_ConTokenDeRequester_Devuelve403ProblemDetails(string action)
    {
        var admin = await AdminClientAsync();
        var created = await CreateDepartmentAsync(admin);
        var requester = await RequesterClientAsync();

        var response = await requester.PostAsync($"/api/departments/{created.Id}/{action}", null);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    // Crear

    [Fact]
    public async Task Create_ComoAdmin_Devuelve201ConLocationYDto()
    {
        var admin = await AdminClientAsync();
        var name = NewName();

        var response = await admin.PostAsJsonAsync("/api/departments", new { name, description = "Área" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DepartmentDto>();
        Assert.Equal(name, created!.Name);
        Assert.True(created.IsActive);
        Assert.EndsWith($"/api/departments/{created.Id}", response.Headers.Location!.AbsolutePath);
        var get = await admin.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Create_ConNombreVacio_Devuelve400()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/departments", new { name = "" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ConNombreDuplicado_Devuelve409ProblemDetails()
    {
        var admin = await AdminClientAsync();
        var existing = await CreateDepartmentAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/departments", new { name = existing.Name.ToUpperInvariant() });

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    // Actualizar

    [Fact]
    public async Task Update_ComoAdmin_Devuelve200ConElDtoActualizado()
    {
        var admin = await AdminClientAsync();
        var created = await CreateDepartmentAsync(admin);
        var newName = NewName();

        var response = await admin.PutAsJsonAsync($"/api/departments/{created.Id}",
            new { name = newName, description = "Nueva" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DepartmentDto>();
        Assert.Equal(newName, dto!.Name);
        Assert.Equal("Nueva", dto.Description);
    }

    [Fact]
    public async Task Update_ConIdInexistente_Devuelve404ProblemDetails()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/departments/{Guid.NewGuid()}", new { name = NewName() });

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    // Activar y desactivar

    [Fact]
    public async Task Deactivate_SinUsuariosActivos_Devuelve204YActivateLoReactiva()
    {
        var admin = await AdminClientAsync();
        var created = await CreateDepartmentAsync(admin);

        var deactivate = await admin.PostAsync($"/api/departments/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var afterDeactivate = await admin.GetFromJsonAsync<DepartmentDto>($"/api/departments/{created.Id}");
        Assert.False(afterDeactivate!.IsActive);

        var activate = await admin.PostAsync($"/api/departments/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        var afterActivate = await admin.GetFromJsonAsync<DepartmentDto>($"/api/departments/{created.Id}");
        Assert.True(afterActivate!.IsActive);
    }

    [Fact]
    public async Task Deactivate_ConUsuariosActivosAsignados_Devuelve409ProblemDetails()
    {
        var admin = await AdminClientAsync();
        var requester = await _factory.SeedUserAsync(UserRole.Requester, NewEmail());

        var response = await admin.PostAsync($"/api/departments/{requester.DepartmentId}/deactivate", null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deactivate_ConIdInexistente_Devuelve404ProblemDetails()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsync($"/api/departments/{Guid.NewGuid()}/deactivate", null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }
}
