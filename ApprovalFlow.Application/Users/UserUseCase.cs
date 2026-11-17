using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Application.Users.Dtos;
using ApprovalFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Application.Users;

public class UserUseCase(IApplicationDbContext context, IPasswordHasher passwordHasher) : IUserUseCase
{
    private readonly IApplicationDbContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        await EnsureEmailIsUniqueAsync(dto.Email, null, ct);
        var department = await ResolveDepartmentAsync(dto.DepartmentId, ct);
        var passwordHash = _passwordHasher.Hash(dto.Password);

        var user = User.Create(dto.FullName, dto.Email, passwordHash, dto.Role, department);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        return UserDto.FromEntity(user);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"El usuario '{id}' no existe.");

        return UserDto.FromEntity(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _context.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);

        return users.Select(UserDto.FromEntity).ToList();
    }

    private async Task EnsureEmailIsUniqueAsync(string email, Guid? excludeUserId, CancellationToken ct)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();

        var exists = await _context.Users.AnyAsync(
            u => u.Email == normalized && (excludeUserId == null || u.Id != excludeUserId), ct);

        if (exists)
            throw new ConflictException($"Ya existe un usuario con el email '{normalized}'.");
    }

    private async Task<Department?> ResolveDepartmentAsync(Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is null)
            return null;

        return await _context.Departments.FirstOrDefaultAsync(d => d.Id == departmentId, ct)
            ?? throw new NotFoundException($"El departamento '{departmentId}' no existe.");
    }
}
