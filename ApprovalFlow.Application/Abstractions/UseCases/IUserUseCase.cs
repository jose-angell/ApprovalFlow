using ApprovalFlow.Application.Users.Dtos;

namespace ApprovalFlow.Application.Abstractions.UseCases;

public interface IUserUseCase
{
    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto> UpdateProfileAsync(Guid id, UpdateUserProfileDto dto, CancellationToken ct = default);
    Task<UserDto> ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken ct = default);
}
