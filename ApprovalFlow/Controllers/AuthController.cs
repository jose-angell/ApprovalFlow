using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthUseCase authUseCase) : ControllerBase
{
    private readonly IAuthUseCase _authUseCase = authUseCase;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto, CancellationToken ct)
    {
        var response = await _authUseCase.LoginAsync(dto, ct);
        return Ok(response);
    }
}
