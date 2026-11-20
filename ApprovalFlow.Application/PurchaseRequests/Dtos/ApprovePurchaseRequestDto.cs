using System.ComponentModel.DataAnnotations;
using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Application.PurchaseRequests.Dtos;

public record ApprovePurchaseRequestDto
{
    [MaxLength(PurchaseRequest.CommentsMaxLength, ErrorMessage = "El comentario no puede superar los {1} caracteres.")]
    public string? Comments { get; init; }
}
