using System.ComponentModel.DataAnnotations;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Application.Users.Dtos;

public record ChangeUserRoleDto
{
    [Required(ErrorMessage = "El rol es obligatorio.")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "El rol indicado no es válido.")]
    public UserRole Role { get; init; }

    public Guid? DepartmentId { get; init; }
}
