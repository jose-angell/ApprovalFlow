using ApprovalFlow.Application.Departments.Dtos;

namespace ApprovalFlow.Application.Abstractions.UseCases;

public interface IDepartmentUseCase
{
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken ct = default);
    Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct = default);
}
