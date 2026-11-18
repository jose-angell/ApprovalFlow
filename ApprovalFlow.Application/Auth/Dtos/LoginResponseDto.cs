using ApprovalFlow.Application.Users.Dtos;

namespace ApprovalFlow.Application.Auth.Dtos;

public record LoginResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserDto User { get; init; } = null!;
}
