using ApprovalFlow.Application.Departments;
using ApprovalFlow.Application.Departments.Dtos;
using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Tests.Application;

public class DepartmentUseCaseTests
{
    private readonly AppDbContext _context;
    private readonly DepartmentUseCase _useCase;

    public DepartmentUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _useCase = new DepartmentUseCase(_context);
    }

    private static CreateDepartmentDto NewDto(string name = "Compras", string? description = null) => new()
    {
        Name = name,
        Description = description
    };

    private async Task AddUserAsync(Department department, string email, bool active = true)
    {
        var user = User.Create("Usuario", email, "hash", UserRole.Requester, department);
        if (!active)
            user.Deactivate();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    private async Task<Department> GetSavedAsync(Guid id)
        => await _context.Departments.AsNoTracking().SingleAsync(d => d.Id == id);

    // CreateAsync

    [Fact]
    public async Task CreateAsync_ConDatosValidos_GuardaDepartamentoActivoConNombreRecortado()
    {
        var result = await _useCase.CreateAsync(NewDto("  Compras  ", "Área de compras"));

        Assert.True(result.IsActive);
        Assert.Equal("Compras", result.Name);
        Assert.Equal("Área de compras", result.Description);
        var saved = await GetSavedAsync(result.Id);
        Assert.Equal("Compras", saved.Name);
    }

    [Theory]
    [InlineData("compras")]
    [InlineData("COMPRAS")]
    [InlineData("  Compras  ")]
    public async Task CreateAsync_ConNombreExistenteConOtraCapitalizacionOEspacios_LanzaConflictException(string name)
    {
        await _useCase.CreateAsync(NewDto("Compras"));

        await Assert.ThrowsAsync<ConflictException>(() => _useCase.CreateAsync(NewDto(name)));
    }

    [Fact]
    public async Task CreateAsync_ConNombreVacio_LanzaArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _useCase.CreateAsync(NewDto("   ")));
    }

    // GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ConIdExistente_DevuelveElDepartamento()
    {
        var created = await _useCase.CreateAsync(NewDto());

        var result = await _useCase.GetByIdAsync(created.Id);

        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Compras", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.GetByIdAsync(Guid.NewGuid()));
    }

    // GetAllAsync

    [Fact]
    public async Task GetAllAsync_DevuelveActivosEInactivosOrdenadosPorNombre()
    {
        await _useCase.CreateAsync(NewDto("Zeta"));
        var beta = await _useCase.CreateAsync(NewDto("Beta"));
        await _useCase.CreateAsync(NewDto("Alfa"));
        await _useCase.DeactivateAsync(beta.Id);

        var result = await _useCase.GetAllAsync();

        Assert.Equal(["Alfa", "Beta", "Zeta"], result.Select(d => d.Name));
        Assert.False(result.Single(d => d.Name == "Beta").IsActive);
    }

    // UpdateAsync

    [Fact]
    public async Task UpdateAsync_ConDatosValidos_ActualizaNombreYDescripcion()
    {
        var created = await _useCase.CreateAsync(NewDto("Compras", "Vieja"));

        var result = await _useCase.UpdateAsync(created.Id, new UpdateDepartmentDto { Name = "Adquisiciones", Description = "Nueva" });

        Assert.Equal("Adquisiciones", result.Name);
        Assert.Equal("Nueva", result.Description);
        var saved = await GetSavedAsync(created.Id);
        Assert.Equal("Adquisiciones", saved.Name);
    }

    [Fact]
    public async Task UpdateAsync_ConElNombreDeOtroDepartamento_LanzaConflictException()
    {
        await _useCase.CreateAsync(NewDto("Compras"));
        var other = await _useCase.CreateAsync(NewDto("Ventas"));

        await Assert.ThrowsAsync<ConflictException>(
            () => _useCase.UpdateAsync(other.Id, new UpdateDepartmentDto { Name = " compras " }));
    }

    [Fact]
    public async Task UpdateAsync_ConSuPropioNombreCambiandoMayusculas_NoLanza()
    {
        var created = await _useCase.CreateAsync(NewDto("Compras"));

        var result = await _useCase.UpdateAsync(created.Id, new UpdateDepartmentDto { Name = "COMPRAS" });

        Assert.Equal("COMPRAS", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _useCase.UpdateAsync(Guid.NewGuid(), new UpdateDepartmentDto { Name = "Compras" }));
    }

    // ActivateAsync / DeactivateAsync

    [Fact]
    public async Task DeactivateAsync_SinUsuariosActivos_DejaIsActiveEnFalseYActivateLoVuelveATrue()
    {
        var created = await _useCase.CreateAsync(NewDto());

        await _useCase.DeactivateAsync(created.Id);
        Assert.False((await GetSavedAsync(created.Id)).IsActive);

        await _useCase.ActivateAsync(created.Id);
        Assert.True((await GetSavedAsync(created.Id)).IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_ConUsuarioActivoAsignado_LanzaConflictExceptionYSigueActivo()
    {
        var created = await _useCase.CreateAsync(NewDto());
        var department = await _context.Departments.SingleAsync(d => d.Id == created.Id);
        await AddUserAsync(department, "ana@empresa.com");

        await Assert.ThrowsAsync<ConflictException>(() => _useCase.DeactivateAsync(created.Id));

        Assert.True((await GetSavedAsync(created.Id)).IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_ConSoloUsuariosInactivosAsignados_DesactivaElDepartamento()
    {
        var created = await _useCase.CreateAsync(NewDto());
        var department = await _context.Departments.SingleAsync(d => d.Id == created.Id);
        await AddUserAsync(department, "ana@empresa.com", active: false);

        await _useCase.DeactivateAsync(created.Id);

        Assert.False((await GetSavedAsync(created.Id)).IsActive);
    }

    [Fact]
    public async Task ActivateAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ActivateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeactivateAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.DeactivateAsync(Guid.NewGuid()));
    }
}
