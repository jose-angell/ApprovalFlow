using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Application.PurchaseRequests;
using ApprovalFlow.Application.PurchaseRequests.Dtos;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Tests.Application;

public class PurchaseRequestUseCaseTests
{
    private readonly AppDbContext _context;
    private readonly PurchaseRequestUseCase _useCase;

    public PurchaseRequestUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _useCase = new PurchaseRequestUseCase(_context);
    }

    private static CreatePurchaseRequestDto NewDto(string title = "Laptops", string justification = "Renovación de equipos", decimal amount = 1500.50m) => new()
    {
        Title = title,
        Justification = justification,
        TotalAmount = amount
    };

    private async Task<Department> AddDepartmentAsync(string name = "Compras")
    {
        var department = Department.Create(name, null);
        _context.Departments.Add(department);
        await _context.SaveChangesAsync();
        return department;
    }

    private async Task<User> AddUserAsync(UserRole role, Department? department, bool active = true)
    {
        var user = User.Create("Usuario", $"{Guid.NewGuid():N}@test.com", "hash", role, department);
        if (!active)
            user.Deactivate();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    // El dominio no permite crear un Requester/Approver sin departamento, así que se quita por reflexión.
    private async Task RemoveDepartmentAsync(User user)
    {
        typeof(User).GetProperty(nameof(User.DepartmentId))!.SetValue(user, null);
        await _context.SaveChangesAsync();
    }

    private async Task<PurchaseRequestDto> CreateRequestAsync(User requester, string title = "Laptops")
    {
        // Separa los CreatedAt para que el orden descendente sea determinista.
        await Task.Delay(5);
        return await _useCase.CreateAsync(requester.Id, NewDto(title));
    }

    private async Task<PurchaseRequest> GetSavedAsync(Guid id)
        => await _context.PurchaseRequests.AsNoTracking().SingleAsync(r => r.Id == id);

    // CreateAsync

    [Fact]
    public async Task CreateAsync_ConRequesterActivoConDepartamento_GuardaSolicitudPending()
    {
        var department = await AddDepartmentAsync();
        var requester = await AddUserAsync(UserRole.Requester, department);

        var result = await _useCase.CreateAsync(requester.Id, NewDto("  Laptops  ", "  Renovación  ", 1500.50m));

        Assert.Equal(PurchaseRequestStatus.Pending, result.Status);
        Assert.Equal(requester.Id, result.RequesterId);
        Assert.Equal(department.Id, result.DepartmentId);
        Assert.Equal("Laptops", result.Title);
        Assert.Equal(1500.50m, result.TotalAmount);
        var saved = await GetSavedAsync(result.Id);
        Assert.Equal(PurchaseRequestStatus.Pending, saved.Status);
        Assert.Equal(requester.Id, saved.RequesterId);
    }

    [Fact]
    public async Task CreateAsync_PrimeraSolicitudDelAnio_RecibeNumeroUno()
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync());

        var result = await _useCase.CreateAsync(requester.Id, NewDto());

        Assert.Equal($"PR-{DateTime.UtcNow.Year}-000001", result.RequestNumber);
    }

    [Fact]
    public async Task CreateAsync_SegundaSolicitudDelAnio_RecibeNumeroDos()
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync());

        await _useCase.CreateAsync(requester.Id, NewDto());
        var second = await _useCase.CreateAsync(requester.Id, NewDto("Monitores"));

        Assert.Equal($"PR-{DateTime.UtcNow.Year}-000002", second.RequestNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.123)]
    public async Task CreateAsync_ConMontoInvalido_LanzaArgumentException(double amount)
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync());

        await Assert.ThrowsAsync<ArgumentException>(() => _useCase.CreateAsync(requester.Id, NewDto(amount: (decimal)amount)));
    }

    [Fact]
    public async Task CreateAsync_ConRequesterSinDepartamento_LanzaInvalidOperationException()
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync());
        await RemoveDepartmentAsync(requester);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.CreateAsync(requester.Id, NewDto()));
    }

    [Fact]
    public async Task CreateAsync_ConRequesterInactivo_LanzaInvalidOperationException()
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync(), active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.CreateAsync(requester.Id, NewDto()));
    }

    [Fact]
    public async Task CreateAsync_ConUsuarioInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.CreateAsync(Guid.NewGuid(), NewDto()));
    }

    // GetAllAsync

    [Fact]
    public async Task GetAllAsync_ComoRequester_DevuelveSoloSusSolicitudes()
    {
        var department = await AddDepartmentAsync();
        var requester1 = await AddUserAsync(UserRole.Requester, department);
        var requester2 = await AddUserAsync(UserRole.Requester, department);
        var own = await CreateRequestAsync(requester1);
        await CreateRequestAsync(requester2);

        var result = await _useCase.GetAllAsync(requester1.Id, null);

        var item = Assert.Single(result);
        Assert.Equal(own.Id, item.Id);
    }

    [Fact]
    public async Task GetAllAsync_ComoApprover_DevuelveSoloLasDeSuDepartamento()
    {
        var department1 = await AddDepartmentAsync("Compras");
        var department2 = await AddDepartmentAsync("Ventas");
        var requester1 = await AddUserAsync(UserRole.Requester, department1);
        var requester2 = await AddUserAsync(UserRole.Requester, department2);
        var approver = await AddUserAsync(UserRole.Approver, department1);
        var inDepartment = await CreateRequestAsync(requester1);
        await CreateRequestAsync(requester2);

        var result = await _useCase.GetAllAsync(approver.Id, null);

        var item = Assert.Single(result);
        Assert.Equal(inDepartment.Id, item.Id);
    }

    [Fact]
    public async Task GetAllAsync_ComoApproverSinDepartamento_DevuelveListaVacia()
    {
        var department = await AddDepartmentAsync();
        var requester = await AddUserAsync(UserRole.Requester, department);
        var approver = await AddUserAsync(UserRole.Approver, department);
        await CreateRequestAsync(requester);
        await RemoveDepartmentAsync(approver);

        var result = await _useCase.GetAllAsync(approver.Id, null);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.Admin)]
    public async Task GetAllAsync_ComoFinanceOAdmin_DevuelveTodas(UserRole role)
    {
        var department1 = await AddDepartmentAsync("Compras");
        var department2 = await AddDepartmentAsync("Ventas");
        var requester1 = await AddUserAsync(UserRole.Requester, department1);
        var requester2 = await AddUserAsync(UserRole.Requester, department2);
        var viewer = await AddUserAsync(role, role == UserRole.Admin ? null : department1);
        await CreateRequestAsync(requester1);
        await CreateRequestAsync(requester2);

        var result = await _useCase.GetAllAsync(viewer.Id, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_ConStatus_DevuelveSoloEseEstadoOrdenadasPorCreatedAtDescendente()
    {
        var department = await AddDepartmentAsync();
        var requester = await AddUserAsync(UserRole.Requester, department);
        var approver = await AddUserAsync(UserRole.Approver, department);
        var first = await CreateRequestAsync(requester, "Primera");
        var second = await CreateRequestAsync(requester, "Segunda");
        var third = await CreateRequestAsync(requester, "Tercera");
        await _useCase.StartReviewAsync(second.Id, approver.Id);

        var pending = await _useCase.GetAllAsync(approver.Id, PurchaseRequestStatus.Pending);
        var inReview = await _useCase.GetAllAsync(approver.Id, PurchaseRequestStatus.InReview);

        Assert.Equal([third.Id, first.Id], pending.Select(r => r.Id));
        Assert.Equal([second.Id], inReview.Select(r => r.Id));
    }

    [Fact]
    public async Task GetAllAsync_ConUsuarioInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.GetAllAsync(Guid.NewGuid(), null));
    }

    // GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ComoDueno_DevuelveLaSolicitud()
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync());
        var created = await CreateRequestAsync(requester);

        var result = await _useCase.GetByIdAsync(created.Id, requester.Id);

        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.RequestNumber, result.RequestNumber);
    }

    [Fact]
    public async Task GetByIdAsync_DeOtroRequester_LanzaNotFoundException()
    {
        var department = await AddDepartmentAsync();
        var owner = await AddUserAsync(UserRole.Requester, department);
        var other = await AddUserAsync(UserRole.Requester, department);
        var created = await CreateRequestAsync(owner);

        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.GetByIdAsync(created.Id, other.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ComoApproverDeOtroDepartamento_LanzaNotFoundException()
    {
        var department1 = await AddDepartmentAsync("Compras");
        var department2 = await AddDepartmentAsync("Ventas");
        var requester = await AddUserAsync(UserRole.Requester, department1);
        var approver = await AddUserAsync(UserRole.Approver, department2);
        var created = await CreateRequestAsync(requester);

        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.GetByIdAsync(created.Id, approver.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ComoApproverDelMismoDepartamento_DevuelveLaSolicitud()
    {
        var department = await AddDepartmentAsync();
        var requester = await AddUserAsync(UserRole.Requester, department);
        var approver = await AddUserAsync(UserRole.Approver, department);
        var created = await CreateRequestAsync(requester);

        var result = await _useCase.GetByIdAsync(created.Id, approver.Id);

        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ComoFinance_DevuelveSolicitudDeCualquierDepartamento()
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync("Compras"));
        var finance = await AddUserAsync(UserRole.Finance, await AddDepartmentAsync("Finanzas"));
        var created = await CreateRequestAsync(requester);

        var result = await _useCase.GetByIdAsync(created.Id, finance.Id);

        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ConSolicitudInexistente_LanzaNotFoundException()
    {
        var requester = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync());

        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.GetByIdAsync(Guid.NewGuid(), requester.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ConUsuarioInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    // Escenario común: Requester y Approver en el mismo departamento con una solicitud Pending.
    private async Task<(User Requester, User Approver, PurchaseRequestDto Request)> SetupPendingAsync()
    {
        var department = await AddDepartmentAsync();
        var requester = await AddUserAsync(UserRole.Requester, department);
        var approver = await AddUserAsync(UserRole.Approver, department);
        var request = await CreateRequestAsync(requester);
        return (requester, approver, request);
    }

    private async Task<(User Requester, User Approver, PurchaseRequestDto Request)> SetupInReviewAsync()
    {
        var (requester, approver, request) = await SetupPendingAsync();
        var inReview = await _useCase.StartReviewAsync(request.Id, approver.Id);
        return (requester, approver, inReview);
    }

    // UpdateAsync

    [Fact]
    public async Task UpdateAsync_DelDuenoEnPending_CambiaCamposYFijaUpdatedAt()
    {
        var (requester, _, request) = await SetupPendingAsync();

        var result = await _useCase.UpdateAsync(request.Id, requester.Id, new UpdatePurchaseRequestDto
        {
            Title = "  Monitores  ",
            Justification = "Nuevos monitores",
            TotalAmount = 300m
        });

        Assert.Equal("Monitores", result.Title);
        Assert.Equal("Nuevos monitores", result.Justification);
        Assert.Equal(300m, result.TotalAmount);
        Assert.NotNull(result.UpdatedAt);
        var saved = await GetSavedAsync(request.Id);
        Assert.Equal("Monitores", saved.Title);
        Assert.Equal(300m, saved.TotalAmount);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ConMontoInvalido_LanzaArgumentException()
    {
        var (requester, _, request) = await SetupPendingAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _useCase.UpdateAsync(request.Id, requester.Id, new UpdatePurchaseRequestDto
        {
            Title = "Monitores",
            Justification = "Nuevos monitores",
            TotalAmount = 0m
        }));
    }

    [Fact]
    public async Task UpdateAsync_EnEstadoDistintoDePending_LanzaInvalidOperationException()
    {
        var (requester, _, request) = await SetupInReviewAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.UpdateAsync(request.Id, requester.Id, new UpdatePurchaseRequestDto
        {
            Title = "Monitores",
            Justification = "Nuevos monitores",
            TotalAmount = 300m
        }));
    }

    [Fact]
    public async Task UpdateAsync_DeOtroRequester_LanzaNotFoundException()
    {
        var (_, _, request) = await SetupPendingAsync();
        var other = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync("Ventas"));

        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.UpdateAsync(request.Id, other.Id, new UpdatePurchaseRequestDto
        {
            Title = "Monitores",
            Justification = "Nuevos monitores",
            TotalAmount = 300m
        }));
    }

    // CancelAsync

    [Fact]
    public async Task CancelAsync_DelDuenoEnPending_DejaCancelledConCancelledById()
    {
        var (requester, _, request) = await SetupPendingAsync();

        var result = await _useCase.CancelAsync(request.Id, requester.Id);

        Assert.Equal(PurchaseRequestStatus.Cancelled, result.Status);
        Assert.Equal(requester.Id, result.CancelledById);
        Assert.NotNull(result.CancelledAt);
        var saved = await GetSavedAsync(request.Id);
        Assert.Equal(PurchaseRequestStatus.Cancelled, saved.Status);
        Assert.Equal(requester.Id, saved.CancelledById);
    }

    [Fact]
    public async Task CancelAsync_EnEstadoDistintoDePending_LanzaInvalidOperationException()
    {
        var (requester, _, request) = await SetupInReviewAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.CancelAsync(request.Id, requester.Id));
    }

    [Fact]
    public async Task CancelAsync_DeOtroRequester_LanzaNotFoundException()
    {
        var (_, _, request) = await SetupPendingAsync();
        var other = await AddUserAsync(UserRole.Requester, await AddDepartmentAsync("Ventas"));

        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.CancelAsync(request.Id, other.Id));
    }

    // StartReviewAsync

    [Fact]
    public async Task StartReviewAsync_ConApproverDelDepartamento_DejaInReviewConReviewedById()
    {
        var (_, approver, request) = await SetupPendingAsync();

        var result = await _useCase.StartReviewAsync(request.Id, approver.Id);

        Assert.Equal(PurchaseRequestStatus.InReview, result.Status);
        Assert.Equal(approver.Id, result.ReviewedById);
        Assert.NotNull(result.ReviewedAt);
        var saved = await GetSavedAsync(request.Id);
        Assert.Equal(PurchaseRequestStatus.InReview, saved.Status);
        Assert.Equal(approver.Id, saved.ReviewedById);
    }

    [Fact]
    public async Task StartReviewAsync_ConApproverDeOtroDepartamento_LanzaNotFoundException()
    {
        var (_, _, request) = await SetupPendingAsync();
        var otherApprover = await AddUserAsync(UserRole.Approver, await AddDepartmentAsync("Ventas"));

        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.StartReviewAsync(request.Id, otherApprover.Id));
    }

    [Fact]
    public async Task StartReviewAsync_ConApproverInactivo_LanzaInvalidOperationException()
    {
        var (_, approver, request) = await SetupPendingAsync();
        approver.Deactivate();
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.StartReviewAsync(request.Id, approver.Id));
    }

    [Fact]
    public async Task StartReviewAsync_ConRequesterDueno_LanzaInvalidOperationException()
    {
        var (requester, _, request) = await SetupPendingAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.StartReviewAsync(request.Id, requester.Id));
    }

    [Fact]
    public async Task StartReviewAsync_EnEstadoDistintoDePending_LanzaInvalidOperationException()
    {
        var (_, approver, request) = await SetupInReviewAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.StartReviewAsync(request.Id, approver.Id));
    }

    // ApproveAsync

    [Fact]
    public async Task ApproveAsync_EnInReviewSinComentario_DejaApprovedConCommentsNull()
    {
        var (_, approver, request) = await SetupInReviewAsync();

        var result = await _useCase.ApproveAsync(request.Id, approver.Id, new ApprovePurchaseRequestDto());

        Assert.Equal(PurchaseRequestStatus.Approved, result.Status);
        Assert.Equal(approver.Id, result.ApprovedById);
        Assert.NotNull(result.ApprovedAt);
        Assert.Null(result.Comments);
        var saved = await GetSavedAsync(request.Id);
        Assert.Equal(PurchaseRequestStatus.Approved, saved.Status);
        Assert.Null(saved.Comments);
    }

    [Fact]
    public async Task ApproveAsync_ConComentario_GuardaElComentarioRecortado()
    {
        var (_, approver, request) = await SetupInReviewAsync();

        var result = await _useCase.ApproveAsync(request.Id, approver.Id, new ApprovePurchaseRequestDto { Comments = "  Aprobada  " });

        Assert.Equal("Aprobada", result.Comments);
    }

    [Fact]
    public async Task ApproveAsync_EnPending_LanzaInvalidOperationException()
    {
        var (_, approver, request) = await SetupPendingAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ApproveAsync(request.Id, approver.Id, new ApprovePurchaseRequestDto()));
    }

    [Fact]
    public async Task ApproveAsync_ConApproverDeOtroDepartamento_LanzaNotFoundException()
    {
        var (_, _, request) = await SetupInReviewAsync();
        var otherApprover = await AddUserAsync(UserRole.Approver, await AddDepartmentAsync("Ventas"));

        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ApproveAsync(request.Id, otherApprover.Id, new ApprovePurchaseRequestDto()));
    }

    // RejectAsync

    [Fact]
    public async Task RejectAsync_EnInReview_DejaRejectedConRejectedByIdYComentarioRecortado()
    {
        var (_, approver, request) = await SetupInReviewAsync();

        var result = await _useCase.RejectAsync(request.Id, approver.Id, new RejectPurchaseRequestDto { Comments = "  Sin presupuesto  " });

        Assert.Equal(PurchaseRequestStatus.Rejected, result.Status);
        Assert.Equal(approver.Id, result.RejectedById);
        Assert.NotNull(result.RejectedAt);
        Assert.Equal("Sin presupuesto", result.Comments);
        var saved = await GetSavedAsync(request.Id);
        Assert.Equal(PurchaseRequestStatus.Rejected, saved.Status);
        Assert.Equal("Sin presupuesto", saved.Comments);
    }

    [Fact]
    public async Task RejectAsync_EnPending_LanzaInvalidOperationException()
    {
        var (_, approver, request) = await SetupPendingAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.RejectAsync(request.Id, approver.Id, new RejectPurchaseRequestDto { Comments = "No" }));
    }

    [Fact]
    public async Task RejectAsync_ConComentarioVacio_LanzaArgumentException()
    {
        var (_, approver, request) = await SetupInReviewAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _useCase.RejectAsync(request.Id, approver.Id, new RejectPurchaseRequestDto { Comments = "   " }));
    }
}
