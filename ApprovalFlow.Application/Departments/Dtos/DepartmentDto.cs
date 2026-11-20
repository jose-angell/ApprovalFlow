using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Application.Departments.Dtos;

public record DepartmentDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    public static DepartmentDto FromEntity(Department department) => new()
    {
        Id = department.Id,
        Name = department.Name,
        Description = department.Description,
        IsActive = department.IsActive,
        CreatedAt = department.CreatedAt
    };
}
