using System.ComponentModel.DataAnnotations;
using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Application.Users.Dtos;

public record UpdateUserProfileDto
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(User.FullNameMaxLength, ErrorMessage = "El nombre completo no puede superar los {1} caracteres.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [MaxLength(User.EmailMaxLength, ErrorMessage = "El email no puede superar los {1} caracteres.")]
    public string Email { get; init; } = string.Empty;
}
