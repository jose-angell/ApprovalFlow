using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Application.Auth;

public class UserSessionUseCase(IApplicationDbContext context) : IUserSessionUseCase
{
    private readonly IApplicationDbContext _context = context;

    public async Task<bool> ValidateAsync(Guid userId, UserRole role, Guid? departmentId, CancellationToken ct = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Role, u.DepartmentId, u.IsActive })
            .FirstOrDefaultAsync(ct);

        return user is not null
            && user.IsActive
            && user.Role == role
            && user.DepartmentId == departmentId;
    }
}
