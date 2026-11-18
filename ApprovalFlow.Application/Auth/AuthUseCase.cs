using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Auth.Dtos;
using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Application.Users.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Application.Auth;

public class AuthUseCase(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator) : IAuthUseCase
{
    private const string InvalidCredentialsMessage = "Credenciales inválidas.";

    private readonly IApplicationDbContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator = tokenGenerator;

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null
            || !user.IsActive
            || !_passwordHasher.Verify(user.PasswordHash, dto.Password))
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var token = _tokenGenerator.Generate(user);

        return new LoginResponseDto
        {
            AccessToken = token.Token,
            ExpiresAt = token.ExpiresAt,
            User = UserDto.FromEntity(user)
        };
    }
}
