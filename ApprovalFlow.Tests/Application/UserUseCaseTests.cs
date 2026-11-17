using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Application.Users;
using ApprovalFlow.Application.Users.Dtos;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Persistence;
using ApprovalFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Tests.Application;

public class UserUseCaseTests
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher _hasher = new();
    private readonly UserUseCase _useCase;

    public UserUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _useCase = new UserUseCase(_context, _hasher);
    }

    private async Task<Department> SeedDepartmentAsync(bool active = true)
    {
        var department = Department.Create("Compras", null);
        if (!active)
            department.Deactivate();
        _context.Departments.Add(department);
        await _context.SaveChangesAsync();
        return department;
    }

    private static CreateUserDto NewUserDto(Guid? departmentId, string email = "ana@empresa.com",
        string fullName = "Ana Pérez", UserRole role = UserRole.Requester) => new()
    {
        FullName = fullName,
        Email = email,
        Password = "Secreta123",
        Role = role,
        DepartmentId = departmentId
    };

    private async Task<UserDto> SeedUserAsync(string email = "ana@empresa.com", string fullName = "Ana Pérez")
    {
        var department = await SeedDepartmentAsync();
        return await _useCase.CreateAsync(NewUserDto(department.Id, email, fullName));
    }

    // CreateAsync

    [Fact]
    public async Task CreateAsync_ConDatosValidos_GuardaUsuarioActivoConHashVerificable()
    {
        var department = await SeedDepartmentAsync();

        var result = await _useCase.CreateAsync(NewUserDto(department.Id));

        Assert.True(result.IsActive);
        Assert.Equal("ana@empresa.com", result.Email);
        Assert.Equal(department.Id, result.DepartmentId);
        var saved = await _context.Users.SingleAsync(u => u.Id == result.Id);
        Assert.True(_hasher.Verify(saved.PasswordHash, "Secreta123"));
    }

    [Fact]
    public async Task CreateAsync_ConEmailExistenteConOtraCapitalizacionYEspacios_LanzaConflictException()
    {
        var department = await SeedDepartmentAsync();
        await _useCase.CreateAsync(NewUserDto(department.Id));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _useCase.CreateAsync(NewUserDto(department.Id, "  ANA@Empresa.COM ", "Otra Persona")));
    }

    [Fact]
    public async Task CreateAsync_ConDepartamentoInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _useCase.CreateAsync(NewUserDto(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateAsync_RequesterSinDepartamento_LanzaArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _useCase.CreateAsync(NewUserDto(null, role: UserRole.Requester)));
    }

    [Fact]
    public async Task CreateAsync_ConDepartamentoInactivo_LanzaInvalidOperationException()
    {
        var department = await SeedDepartmentAsync(active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _useCase.CreateAsync(NewUserDto(department.Id)));
    }

    // GetByIdAsync / GetAllAsync

    [Fact]
    public async Task GetByIdAsync_ConUsuarioExistente_DevuelveElUsuario()
    {
        var created = await SeedUserAsync();

        var result = await _useCase.GetByIdAsync(created.Id);

        Assert.Equal(created.Id, result.Id);
        Assert.Equal("ana@empresa.com", result.Email);
    }

    [Fact]
    public async Task GetByIdAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAllAsync_DevuelveUsuariosOrdenadosPorFullName()
    {
        var department = await SeedDepartmentAsync();
        await _useCase.CreateAsync(NewUserDto(department.Id, "carlos@empresa.com", "Carlos"));
        await _useCase.CreateAsync(NewUserDto(department.Id, "ana@empresa.com", "Ana"));
        await _useCase.CreateAsync(NewUserDto(department.Id, "beto@empresa.com", "Beto"));

        var result = await _useCase.GetAllAsync();

        Assert.Equal(new[] { "Ana", "Beto", "Carlos" }, result.Select(u => u.FullName).ToArray());
    }

    // UpdateProfileAsync

    [Fact]
    public async Task UpdateProfileAsync_ConEmailDeOtroUsuario_LanzaConflictException()
    {
        var department = await SeedDepartmentAsync();
        await _useCase.CreateAsync(NewUserDto(department.Id, "ana@empresa.com", "Ana"));
        var other = await _useCase.CreateAsync(NewUserDto(department.Id, "beto@empresa.com", "Beto"));

        await Assert.ThrowsAsync<ConflictException>(() => _useCase.UpdateProfileAsync(other.Id,
            new UpdateUserProfileDto { FullName = "Beto", Email = "ANA@empresa.com" }));
    }

    [Fact]
    public async Task UpdateProfileAsync_ConSuPropioEmail_NoLanza()
    {
        var created = await SeedUserAsync();

        var result = await _useCase.UpdateProfileAsync(created.Id,
            new UpdateUserProfileDto { FullName = "Ana María", Email = "ana@empresa.com" });

        Assert.Equal("Ana María", result.FullName);
        Assert.Equal("ana@empresa.com", result.Email);
    }

    // ChangeRoleAsync

    [Fact]
    public async Task ChangeRoleAsync_AAdminSinDepartamento_DejaDepartmentIdNulo()
    {
        var created = await SeedUserAsync();

        var result = await _useCase.ChangeRoleAsync(created.Id,
            new ChangeUserRoleDto { Role = UserRole.Admin, DepartmentId = null });

        Assert.Equal(UserRole.Admin, result.Role);
        Assert.Null(result.DepartmentId);
    }

    // Activate / Deactivate

    [Fact]
    public async Task DeactivateAsync_DejaIsActiveEnFalseYActualizaUpdatedAt()
    {
        var created = await SeedUserAsync();

        await _useCase.DeactivateAsync(created.Id);

        var user = await _useCase.GetByIdAsync(created.Id);
        Assert.False(user.IsActive);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public async Task ActivateAsync_DejaIsActiveEnTrueYActualizaUpdatedAt()
    {
        var created = await SeedUserAsync();
        await _useCase.DeactivateAsync(created.Id);
        var deactivatedAt = (await _useCase.GetByIdAsync(created.Id)).UpdatedAt;
        await Task.Delay(5);

        await _useCase.ActivateAsync(created.Id);

        var user = await _useCase.GetByIdAsync(created.Id);
        Assert.True(user.IsActive);
        Assert.True(user.UpdatedAt > deactivatedAt);
    }

    // ResetPasswordAsync

    [Fact]
    public async Task ResetPasswordAsync_CambiaElHash()
    {
        var created = await SeedUserAsync();

        await _useCase.ResetPasswordAsync(created.Id, new ResetPasswordDto { NewPassword = "NuevaClave456" });

        var saved = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id);
        Assert.True(_hasher.Verify(saved.PasswordHash, "NuevaClave456"));
        Assert.False(_hasher.Verify(saved.PasswordHash, "Secreta123"));
    }

    // Usuario inexistente

    [Fact]
    public async Task UpdateProfileAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.UpdateProfileAsync(Guid.NewGuid(),
            new UpdateUserProfileDto { FullName = "Ana", Email = "ana@empresa.com" }));
    }

    [Fact]
    public async Task ChangeRoleAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ChangeRoleAsync(Guid.NewGuid(),
            new ChangeUserRoleDto { Role = UserRole.Admin }));
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

    [Fact]
    public async Task ResetPasswordAsync_ConIdInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ResetPasswordAsync(Guid.NewGuid(),
            new ResetPasswordDto { NewPassword = "NuevaClave456" }));
    }
}
