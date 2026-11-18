using System.ComponentModel.DataAnnotations;

namespace ApprovalFlow.Application.Users.Dtos;

public record ChangeOwnPasswordDto
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos {1} caracteres.")]
    [MaxLength(100, ErrorMessage = "La nueva contraseña no puede superar los {1} caracteres.")]
    public string NewPassword { get; init; } = string.Empty;
}
