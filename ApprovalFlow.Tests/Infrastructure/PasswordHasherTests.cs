using ApprovalFlow.Infrastructure.Security;

namespace ApprovalFlow.Tests.Infrastructure;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_DevuelveValorDistintoDeLaContrasena()
    {
        var hash = _hasher.Hash("Secreta123");

        Assert.NotEqual("Secreta123", hash);
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void Verify_ConContrasenaCorrecta_DevuelveTrue()
    {
        var hash = _hasher.Hash("Secreta123");

        Assert.True(_hasher.Verify(hash, "Secreta123"));
    }

    [Fact]
    public void Verify_ConContrasenaIncorrecta_DevuelveFalse()
    {
        var hash = _hasher.Hash("Secreta123");

        Assert.False(_hasher.Verify(hash, "otra"));
    }

    [Fact]
    public void Hash_MismaContrasena_GeneraHashesDistintos()
    {
        Assert.NotEqual(_hasher.Hash("Secreta123"), _hasher.Hash("Secreta123"));
    }
}
