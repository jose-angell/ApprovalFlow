using System.ComponentModel.DataAnnotations;
using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Application.Departments.Dtos;

public record CreateDepartmentDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(Department.NameMaxLength, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(Department.DescriptionMaxLength, ErrorMessage = "La descripción no puede superar los {1} caracteres.")]
    public string? Description { get; init; }
}
