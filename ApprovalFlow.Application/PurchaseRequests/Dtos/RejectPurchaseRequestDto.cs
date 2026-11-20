using System.ComponentModel.DataAnnotations;
using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Application.PurchaseRequests.Dtos;

public record RejectPurchaseRequestDto
{
    [Required(ErrorMessage = "El comentario es obligatorio.")]
    [MaxLength(PurchaseRequest.CommentsMaxLength, ErrorMessage = "El comentario no puede superar los {1} caracteres.")]
    public string Comments { get; init; } = string.Empty;
}
