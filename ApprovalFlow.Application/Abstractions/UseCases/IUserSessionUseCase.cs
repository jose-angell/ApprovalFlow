using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Application.Abstractions.UseCases;

public interface IUserSessionUseCase
{
    Task<bool> ValidateAsync(Guid userId, UserRole role, Guid? departmentId, CancellationToken ct = default);
}
