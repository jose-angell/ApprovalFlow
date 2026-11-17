using ApprovalFlow.Application.Users.Dtos;

namespace ApprovalFlow.Application.Abstractions.UseCases;

public interface IUserUseCase
{
    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
}
