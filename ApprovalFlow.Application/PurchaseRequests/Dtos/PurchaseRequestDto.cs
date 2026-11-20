using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Application.PurchaseRequests.Dtos;

public record PurchaseRequestDto
{
    public Guid Id { get; init; }
    public string RequestNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Justification { get; init; } = string.Empty;
    public string? Comments { get; init; }
    public decimal TotalAmount { get; init; }
    public PurchaseRequestStatus Status { get; init; }
    public Guid RequesterId { get; init; }
    public Guid DepartmentId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public Guid? ReviewedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? RejectedAt { get; init; }
    public Guid? RejectedById { get; init; }
    public DateTime? CancelledAt { get; init; }
    public Guid? CancelledById { get; init; }

    public static PurchaseRequestDto FromEntity(PurchaseRequest request) => new()
    {
        Id = request.Id,
        RequestNumber = request.RequestNumber,
        Title = request.Title,
        Justification = request.Justification,
        Comments = request.Comments,
        TotalAmount = request.TotalAmount,
        Status = request.Status,
        RequesterId = request.RequesterId,
        DepartmentId = request.DepartmentId,
        CreatedAt = request.CreatedAt,
        UpdatedAt = request.UpdatedAt,
        ReviewedAt = request.ReviewedAt,
        ReviewedById = request.ReviewedById,
        ApprovedAt = request.ApprovedAt,
        ApprovedById = request.ApprovedById,
        RejectedAt = request.RejectedAt,
        RejectedById = request.RejectedById,
        CancelledAt = request.CancelledAt,
        CancelledById = request.CancelledById
    };
}
