using System.ComponentModel.DataAnnotations;
using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Application.PurchaseRequests.Dtos;

public record UpdatePurchaseRequestDto
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    [MaxLength(PurchaseRequest.TitleMaxLength, ErrorMessage = "El título no puede superar los {1} caracteres.")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "La justificación es obligatoria.")]
    [MaxLength(PurchaseRequest.JustificationMaxLength, ErrorMessage = "La justificación no puede superar los {1} caracteres.")]
    public string Justification { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }
}
