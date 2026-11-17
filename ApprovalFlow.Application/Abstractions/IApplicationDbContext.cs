using ApprovalFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Department> Departments { get; }
    DbSet<PurchaseRequest> PurchaseRequests { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
