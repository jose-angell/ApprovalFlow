using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApprovalFlow.Application.Departments.Dtos;
using ApprovalFlow.Application.PurchaseRequests.Dtos;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Tests.Api.Integration;

public class PurchaseRequestsControllerTests : IClassFixture<ApiFactory>
{
    private const string BaseUrl = "/api/purchase-requests";

    private readonly ApiFactory _factory;

    public PurchaseRequestsControllerTests(ApiFactory factory) => _factory = factory;

    private static string NewEmail() => $"{Guid.NewGuid():N}@empresa.com";

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }

    private static object NewBody(string title = "Laptops", decimal amount = 1500.50m) => new
    {
        title,
        justification = "Renovación de equipos",
        totalAmount = amount
    };

    // Crea un departamento nuevo (vía API, como Admin) para que Requester y Approver lo compartan.
    private async Task<Guid> NewDepartmentIdAsync()
    {
        var admin = _factory.CreateClientFor(await _factory.SeedUserAsync(UserRole.Admin, NewEmail()));
        var response = await admin.PostAsJsonAsync("/api/departments", new { name = $"Depto {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DepartmentDto>())!.Id;
    }

    private async Task<HttpClient> ClientAsync(UserRole role, Guid? departmentId = null)
        => _factory.CreateClientFor(await _factory.SeedUserAsync(role, NewEmail(), departmentId: departmentId));

    private static async Task<PurchaseRequestDto> CreateRequestAsync(HttpClient requester, string title = "Laptops")
    {
        var response = await requester.PostAsJsonAsync(BaseUrl, NewBody(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseRequestDto>())!;
    }

    private static async Task<PurchaseRequestDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseRequestDto>())!;
    }

    // 401

    [Fact]
    public async Task GetAll_SinToken_Devuelve401ProblemDetails()
    {
        var response = await _factory.CreateClient().GetAsync(BaseUrl);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    // POST: solo Requester

    [Fact]
    public async Task Create_ComoRequester_Devuelve201ConLocationYDto()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);

        var response = await requester.PostAsJsonAsync(BaseUrl, NewBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PurchaseRequestDto>();
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"{BaseUrl}/{dto!.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(PurchaseRequestStatus.Pending, dto.Status);
        Assert.Equal(departmentId, dto.DepartmentId);
        Assert.StartsWith($"PR-{DateTime.UtcNow.Year}-", dto.RequestNumber);
    }

    [Theory]
    [InlineData(UserRole.Approver)]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.Admin)]
    public async Task Create_ConRolDistintoDeRequester_Devuelve403ProblemDetails(UserRole role)
    {
        var client = await ClientAsync(role);

        var response = await client.PostAsJsonAsync(BaseUrl, NewBody());

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_ConTituloVacio_Devuelve400()
    {
        var requester = await ClientAsync(UserRole.Requester);

        var response = await requester.PostAsJsonAsync(BaseUrl, NewBody(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ConMontoCero_Devuelve400()
    {
        var requester = await ClientAsync(UserRole.Requester);

        var response = await requester.PostAsJsonAsync(BaseUrl, NewBody(amount: 0m));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    // Acciones de Approver con rol incorrecto

    [Theory]
    [InlineData("start-review")]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task AccionesDeApprover_ComoRequester_Devuelven403ProblemDetails(string action)
    {
        var requester = await ClientAsync(UserRole.Requester);

        var response = await requester.PostAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/{action}", new { comments = "x" });

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    // Visibilidad

    [Fact]
    public async Task GetById_DeSolicitudDeOtroRequester_Devuelve404ProblemDetails()
    {
        var departmentId = await NewDepartmentIdAsync();
        var owner = await ClientAsync(UserRole.Requester, departmentId);
        var other = await ClientAsync(UserRole.Requester, departmentId);
        var created = await CreateRequestAsync(owner);

        var response = await other.GetAsync($"{BaseUrl}/{created.Id}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ComoDueno_Devuelve200()
    {
        var requester = await ClientAsync(UserRole.Requester);
        var created = await CreateRequestAsync(requester);

        var dto = await ReadAsync(await requester.GetAsync($"{BaseUrl}/{created.Id}"));

        Assert.Equal(created.Id, dto.Id);
    }

    [Fact]
    public async Task GetAll_ComoRequester_DevuelveSoloLasSuyas()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester1 = await ClientAsync(UserRole.Requester, departmentId);
        var requester2 = await ClientAsync(UserRole.Requester, departmentId);
        var own = await CreateRequestAsync(requester1);
        await CreateRequestAsync(requester2);

        var response = await requester1.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<PurchaseRequestDto>>();
        Assert.Equal([own.Id], list!.Select(r => r.Id));
    }

    // Flujo completo

    [Fact]
    public async Task Flujo_CrearRevisarYAprobar_TerminaEnApproved()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var created = await CreateRequestAsync(requester);

        var inReview = await ReadAsync(await approver.PostAsync($"{BaseUrl}/{created.Id}/start-review", null));
        Assert.Equal(PurchaseRequestStatus.InReview, inReview.Status);

        var approved = await ReadAsync(await approver.PostAsJsonAsync($"{BaseUrl}/{created.Id}/approve", new { comments = "Aprobada" }));

        Assert.Equal(PurchaseRequestStatus.Approved, approved.Status);
        Assert.Equal("Aprobada", approved.Comments);
        Assert.NotNull(approved.ApprovedById);
    }

    [Fact]
    public async Task Approve_SinCuerpo_Devuelve200()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var created = await CreateRequestAsync(requester);
        await approver.PostAsync($"{BaseUrl}/{created.Id}/start-review", null);

        var approved = await ReadAsync(await approver.PostAsync($"{BaseUrl}/{created.Id}/approve", null));

        Assert.Equal(PurchaseRequestStatus.Approved, approved.Status);
        Assert.Null(approved.Comments);
    }

    [Fact]
    public async Task Flujo_Rechazar_TerminaEnRejected()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var created = await CreateRequestAsync(requester);
        await approver.PostAsync($"{BaseUrl}/{created.Id}/start-review", null);

        var rejected = await ReadAsync(await approver.PostAsJsonAsync($"{BaseUrl}/{created.Id}/reject", new { comments = "Sin presupuesto" }));

        Assert.Equal(PurchaseRequestStatus.Rejected, rejected.Status);
        Assert.Equal("Sin presupuesto", rejected.Comments);
    }

    [Fact]
    public async Task Reject_SinComments_Devuelve400()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var created = await CreateRequestAsync(requester);
        await approver.PostAsync($"{BaseUrl}/{created.Id}/start-review", null);

        var response = await approver.PostAsJsonAsync($"{BaseUrl}/{created.Id}/reject", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Approve_SobreSolicitudPending_Devuelve409ProblemDetails()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var created = await CreateRequestAsync(requester);

        var response = await approver.PostAsJsonAsync($"{BaseUrl}/{created.Id}/approve", new { });

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StartReview_PorApproverDeOtroDepartamento_Devuelve404ProblemDetails()
    {
        var requester = await ClientAsync(UserRole.Requester, await NewDepartmentIdAsync());
        var otherApprover = await ClientAsync(UserRole.Approver, await NewDepartmentIdAsync());
        var created = await CreateRequestAsync(requester);

        var response = await otherApprover.PostAsync($"{BaseUrl}/{created.Id}/start-review", null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    // Edición y cancelación

    [Fact]
    public async Task Update_ComoDuenoEnPending_Devuelve200ConDatosNuevos()
    {
        var requester = await ClientAsync(UserRole.Requester);
        var created = await CreateRequestAsync(requester);

        var updated = await ReadAsync(await requester.PutAsJsonAsync($"{BaseUrl}/{created.Id}", NewBody("Monitores", 300m)));

        Assert.Equal("Monitores", updated.Title);
        Assert.Equal(300m, updated.TotalAmount);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Cancel_ComoDuenoEnPending_Devuelve200Cancelled()
    {
        var requester = await ClientAsync(UserRole.Requester);
        var created = await CreateRequestAsync(requester);

        var cancelled = await ReadAsync(await requester.PostAsync($"{BaseUrl}/{created.Id}/cancel", null));

        Assert.Equal(PurchaseRequestStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledById);
    }

    [Fact]
    public async Task Update_EnInReview_Devuelve409ProblemDetails()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var created = await CreateRequestAsync(requester);
        await approver.PostAsync($"{BaseUrl}/{created.Id}/start-review", null);

        var response = await requester.PutAsJsonAsync($"{BaseUrl}/{created.Id}", NewBody("Monitores", 300m));

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancel_EnInReview_Devuelve409ProblemDetails()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var created = await CreateRequestAsync(requester);
        await approver.PostAsync($"{BaseUrl}/{created.Id}/start-review", null);

        var response = await requester.PostAsync($"{BaseUrl}/{created.Id}/cancel", null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    // Filtro por estado

    [Fact]
    public async Task GetAll_ConStatusPending_ComoFinance_DevuelveSoloPending()
    {
        var departmentId = await NewDepartmentIdAsync();
        var requester = await ClientAsync(UserRole.Requester, departmentId);
        var approver = await ClientAsync(UserRole.Approver, departmentId);
        var finance = await ClientAsync(UserRole.Finance);
        var pending = await CreateRequestAsync(requester, "Pendiente");
        var inReview = await CreateRequestAsync(requester, "En revision");
        await approver.PostAsync($"{BaseUrl}/{inReview.Id}/start-review", null);

        var response = await finance.GetAsync($"{BaseUrl}?status=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<PurchaseRequestDto>>();
        Assert.All(list!, r => Assert.Equal(PurchaseRequestStatus.Pending, r.Status));
        Assert.Contains(list!, r => r.Id == pending.Id);
        Assert.DoesNotContain(list!, r => r.Id == inReview.Id);
    }
}
