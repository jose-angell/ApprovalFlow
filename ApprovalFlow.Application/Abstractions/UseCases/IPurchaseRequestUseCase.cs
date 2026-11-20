using ApprovalFlow.Application.PurchaseRequests.Dtos;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Application.Abstractions.UseCases;

public interface IPurchaseRequestUseCase
{
    Task<PurchaseRequestDto> CreateAsync(Guid userId, CreatePurchaseRequestDto dto, CancellationToken ct = default);
    Task<PurchaseRequestDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseRequestDto>> GetAllAsync(Guid userId, PurchaseRequestStatus? status, CancellationToken ct = default);
    Task<PurchaseRequestDto> UpdateAsync(Guid id, Guid userId, UpdatePurchaseRequestDto dto, CancellationToken ct = default);
    Task<PurchaseRequestDto> CancelAsync(Guid id, Guid userId, CancellationToken ct = default);
}
