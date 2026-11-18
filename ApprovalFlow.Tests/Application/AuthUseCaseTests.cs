using ApprovalFlow.Application.Abstractions;
using ApprovalFlow.Application.Auth;
using ApprovalFlow.Application.Auth.Dtos;
using ApprovalFlow.Application.Exceptions;
using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;
using ApprovalFlow.Infrastructure.Persistence;
using ApprovalFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ApprovalFlow.Tests.Application;

public class AuthUseCaseTests
{
    private const string Password = "Secreta123";

    private readonly AppDbContext _context;
    private readonly PasswordHasher _hasher = new();
    private readonly FakeTokenGenerator _tokens = new();
    private readonly AuthUseCase _useCase;

    public AuthUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _useCase = new AuthUseCase(_context, _hasher, _tokens);
    }

    private async Task<User> SeedUserAsync(string email = "ana@empresa.com", bool active = true)
    {
        var user = User.Create("Ana Pérez", email, _hasher.Hash(Password), UserRole.Admin, null);
        if (!active)
            user.Deactivate();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task LoginAsync_ConCredencialesCorrectas_DevuelveTokenExpiracionYUsuario()
    {
        var user = await SeedUserAsync();

        var result = await _useCase.LoginAsync(new LoginDto { Email = "ana@empresa.com", Password = Password });

        Assert.Equal(FakeTokenGenerator.TokenValue, result.AccessToken);
        Assert.Equal(_tokens.ExpiresAt, result.ExpiresAt);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("ana@empresa.com", result.User.Email);
    }

    [Fact]
    public async Task LoginAsync_ComparaElEmailSinImportarMayusculasNiEspacios()
    {
        await SeedUserAsync();

        var result = await _useCase.LoginAsync(new LoginDto { Email = "  ANA@Empresa.COM ", Password = Password });

        Assert.Equal("ana@empresa.com", result.User.Email);
    }

    [Fact]
    public async Task LoginAsync_ConEmailInexistente_LanzaUnauthorizedException()
    {
        await AssertInvalidCredentialsAsync("nadie@empresa.com", Password);
    }

    [Fact]
    public async Task LoginAsync_ConContrasenaIncorrecta_LanzaUnauthorizedException()
    {
        await SeedUserAsync();

        await AssertInvalidCredentialsAsync("ana@empresa.com", "Incorrecta999");
    }

    [Fact]
    public async Task LoginAsync_ConUsuarioInactivo_LanzaUnauthorizedException()
    {
        await SeedUserAsync(active: false);

        await AssertInvalidCredentialsAsync("ana@empresa.com", Password);
    }

    [Fact]
    public async Task LoginAsync_ConCredencialesInvalidas_NoGeneraToken()
    {
        await SeedUserAsync();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _useCase.LoginAsync(new LoginDto { Email = "ana@empresa.com", Password = "Incorrecta999" }));

        Assert.Equal(0, _tokens.Calls);
    }

    private async Task AssertInvalidCredentialsAsync(string email, string password)
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _useCase.LoginAsync(new LoginDto { Email = email, Password = password }));

        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    private sealed class FakeTokenGenerator : IJwtTokenGenerator
    {
        public const string TokenValue = "token-de-prueba";

        public DateTime ExpiresAt { get; } = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public int Calls { get; private set; }

        public AccessToken Generate(User user)
        {
            Calls++;
            return new AccessToken(TokenValue, ExpiresAt);
        }
    }
}
