using ApprovalFlow.Application.Auth;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Tests.Application;

public class UserSessionUseCaseTests
{
    private readonly AppDbContext _context;
    private readonly UserSessionUseCase _useCase;

    public UserSessionUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _useCase = new UserSessionUseCase(_context);
    }

    private async Task<Department> AddDepartmentAsync(string name = "Compras")
    {
        var department = Department.Create(name, null);
        _context.Departments.Add(department);
        await _context.SaveChangesAsync();
        return department;
    }

    private async Task<User> AddUserAsync(UserRole role, Department? department, string email = "usuario@test.com")
    {
        var user = User.Create("Usuario", email, "hash", role, department);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task ValidateAsync_UsuarioActivoConMismoRolYDepartamento_DevuelveTrue()
    {
        var department = await AddDepartmentAsync();
        var user = await AddUserAsync(UserRole.Requester, department);

        var result = await _useCase.ValidateAsync(user.Id, UserRole.Requester, department.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_AdminSinDepartamentoYTokenSinDepartamento_DevuelveTrue()
    {
        var user = await AddUserAsync(UserRole.Admin, null);

        var result = await _useCase.ValidateAsync(user.Id, UserRole.Admin, null);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_UsuarioInexistente_DevuelveFalse()
    {
        var result = await _useCase.ValidateAsync(Guid.NewGuid(), UserRole.Admin, null);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_UsuarioInactivo_DevuelveFalse()
    {
        var department = await AddDepartmentAsync();
        var user = await AddUserAsync(UserRole.Requester, department);
        user.Deactivate();
        await _context.SaveChangesAsync();

        var result = await _useCase.ValidateAsync(user.Id, UserRole.Requester, department.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_RolDistinto_DevuelveFalse()
    {
        var department = await AddDepartmentAsync();
        var user = await AddUserAsync(UserRole.Requester, department);

        var result = await _useCase.ValidateAsync(user.Id, UserRole.Approver, department.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_DepartamentoDistinto_DevuelveFalse()
    {
        var department = await AddDepartmentAsync("Compras");
        var other = await AddDepartmentAsync("Ventas");
        var user = await AddUserAsync(UserRole.Requester, department);

        var result = await _useCase.ValidateAsync(user.Id, UserRole.Requester, other.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_TokenSinDepartamentoYUsuarioConDepartamento_DevuelveFalse()
    {
        var department = await AddDepartmentAsync();
        var user = await AddUserAsync(UserRole.Requester, department);

        var result = await _useCase.ValidateAsync(user.Id, UserRole.Requester, null);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_TokenConDepartamentoYUsuarioSinDepartamento_DevuelveFalse()
    {
        var department = await AddDepartmentAsync();
        var user = await AddUserAsync(UserRole.Admin, null);

        var result = await _useCase.ValidateAsync(user.Id, UserRole.Admin, department.Id);

        Assert.False(result);
    }
}
