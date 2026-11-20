using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Departments.Dtos;
using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Application.Departments;

public class DepartmentUseCase(IApplicationDbContext context) : IDepartmentUseCase
{
    private readonly IApplicationDbContext _context = context;

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken ct = default)
    {
        await EnsureNameIsUniqueAsync(dto.Name, null, ct);

        var department = Department.Create(dto.Name, dto.Description);

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(ct);

        return DepartmentDto.FromEntity(department);
    }

    public async Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var department = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"El departamento '{id}' no existe.");

        return DepartmentDto.FromEntity(department);
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);

        return departments.Select(DepartmentDto.FromEntity).ToList();
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeDepartmentId, CancellationToken ct)
    {
        var trimmed = (name ?? string.Empty).Trim();
        var normalized = trimmed.ToLowerInvariant();

        var exists = await _context.Departments.AnyAsync(
            d => d.Name.ToLower() == normalized && (excludeDepartmentId == null || d.Id != excludeDepartmentId), ct);

        if (exists)
            throw new ConflictException($"Ya existe un departamento con el nombre '{trimmed}'.");
    }
}
