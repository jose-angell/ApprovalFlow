using ApprovalFlow.Api.Authorization;
using ApprovalFlow.Api.Extensions;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalFlow.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserUseCase userUseCase) : ControllerBase
{
    private readonly IUserUseCase _userUseCase = userUseCase;

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct)
        => Ok(await _userUseCase.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _userUseCase.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto dto, CancellationToken ct)
    {
        var user = await _userUseCase.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserDto>> UpdateProfile(Guid id, UpdateUserProfileDto dto, CancellationToken ct)
        => Ok(await _userUseCase.UpdateProfileAsync(id, dto, ct));

    [HttpPut("{id:guid}/role")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserDto>> ChangeRole(Guid id, ChangeUserRoleDto dto, CancellationToken ct)
        => Ok(await _userUseCase.ChangeRoleAsync(id, dto, User.GetUserId(), ct));

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _userUseCase.ActivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _userUseCase.DeactivateAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordDto dto, CancellationToken ct)
    {
        await _userUseCase.ResetPasswordAsync(id, dto, ct);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken ct)
        => Ok(await _userUseCase.GetByIdAsync(User.GetUserId(), ct));

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeOwnPassword(ChangeOwnPasswordDto dto, CancellationToken ct)
    {
        await _userUseCase.ChangeOwnPasswordAsync(User.GetUserId(), dto, ct);
        return NoContent();
    }
}
