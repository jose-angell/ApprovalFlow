using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Application.PurchaseRequests.Dtos;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Application.PurchaseRequests;

public class PurchaseRequestUseCase(IApplicationDbContext context) : IPurchaseRequestUseCase
{
    private readonly IApplicationDbContext _context = context;

    public async Task<PurchaseRequestDto> CreateAsync(Guid userId, CreatePurchaseRequestDto dto, CancellationToken ct = default)
    {
        var user = await GetActingUserAsync(userId, ct);
        var requestNumber = await GenerateRequestNumberAsync(ct);

        var request = PurchaseRequest.Create(requestNumber, dto.Title, dto.Justification, dto.TotalAmount, user);

        _context.PurchaseRequests.Add(request);
        await _context.SaveChangesAsync(ct);

        return PurchaseRequestDto.FromEntity(request);
    }

    public async Task<PurchaseRequestDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var user = await GetActingUserAsync(userId, ct);

        var request = await ApplyVisibility(_context.PurchaseRequests.AsNoTracking(), user)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"La solicitud '{id}' no existe.");

        return PurchaseRequestDto.FromEntity(request);
    }

    public async Task<IReadOnlyList<PurchaseRequestDto>> GetAllAsync(Guid userId, PurchaseRequestStatus? status, CancellationToken ct = default)
    {
        var user = await GetActingUserAsync(userId, ct);

        var query = ApplyVisibility(_context.PurchaseRequests.AsNoTracking(), user);
        if (status is not null)
            query = query.Where(r => r.Status == status);

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

        return requests.Select(PurchaseRequestDto.FromEntity).ToList();
    }

    public async Task<PurchaseRequestDto> UpdateAsync(Guid id, Guid userId, UpdatePurchaseRequestDto dto, CancellationToken ct = default)
    {
        var user = await GetActingUserAsync(userId, ct);
        var request = await GetVisibleTrackedAsync(id, user, ct);

        request.Update(user, dto.Title, dto.Justification, dto.TotalAmount);
        await _context.SaveChangesAsync(ct);

        return PurchaseRequestDto.FromEntity(request);
    }

    public async Task<PurchaseRequestDto> CancelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var user = await GetActingUserAsync(userId, ct);
        var request = await GetVisibleTrackedAsync(id, user, ct);

        request.Cancel(user);
        await _context.SaveChangesAsync(ct);

        return PurchaseRequestDto.FromEntity(request);
    }

    private async Task<User> GetActingUserAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException($"El usuario '{userId}' no existe.");
    }

    private static IQueryable<PurchaseRequest> ApplyVisibility(IQueryable<PurchaseRequest> query, User user)
    {
        return user.Role switch
        {
            UserRole.Requester => query.Where(r => r.RequesterId == user.Id),
            UserRole.Approver => user.DepartmentId is null
                ? query.Where(_ => false)
                : query.Where(r => r.DepartmentId == user.DepartmentId),
            _ => query
        };
    }

    private async Task<PurchaseRequest> GetVisibleTrackedAsync(Guid id, User user, CancellationToken ct)
    {
        return await ApplyVisibility(_context.PurchaseRequests, user).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"La solicitud '{id}' no existe.");
    }

    private async Task<string> GenerateRequestNumberAsync(CancellationToken ct)
    {
        var prefix = $"PR-{DateTime.UtcNow.Year}-";
        var count = await _context.PurchaseRequests.CountAsync(r => r.RequestNumber.StartsWith(prefix), ct);

        return $"{prefix}{count + 1:D6}";
    }
}
