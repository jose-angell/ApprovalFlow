using System.ComponentModel.DataAnnotations;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Application.Users.Dtos;

public record CreateUserDto
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(User.FullNameMaxLength, ErrorMessage = "El nombre completo no puede superar los {1} caracteres.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [MaxLength(User.EmailMaxLength, ErrorMessage = "El email no puede superar los {1} caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos {1} caracteres.")]
    [MaxLength(100, ErrorMessage = "La contraseña no puede superar los {1} caracteres.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "El rol indicado no es válido.")]
    public UserRole Role { get; init; }

    public Guid? DepartmentId { get; init; }
}
