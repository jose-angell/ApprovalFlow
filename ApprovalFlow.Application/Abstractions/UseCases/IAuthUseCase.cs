using ApprovalFlow.Application.Auth.Dtos;

namespace ApprovalFlow.Application.Abstractions.UseCases;

public interface IAuthUseCase
{
    Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
}
