using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Api.Authorization;

public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Requester = nameof(UserRole.Requester);
    public const string Approver = nameof(UserRole.Approver);
    public const string Finance = nameof(UserRole.Finance);
}
