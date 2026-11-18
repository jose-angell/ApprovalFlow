using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Application.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}
