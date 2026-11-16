using System.Text.Json;
using ApprovalFlow.Api.Middleware;
using ApprovalFlow.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApprovalFlow.Tests.Api;

public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string, string> Casos => new()
    {
        { new NotFoundException("x"), 404, "Recurso no encontrado", "x" },
        { new ConflictException("x"), 409, "Conflicto", "x" },
        { new ForbiddenException("x"), 403, "Acceso denegado", "x" },
        { new UnauthorizedException("x"), 401, "No autenticado", "x" },
        { new ArgumentException("x"), 400, "Solicitud inválida", "x" },
        { new ArgumentNullException("p", "x"), 400, "Solicitud inválida", new ArgumentNullException("p", "x").Message },
        { new ArgumentOutOfRangeException("p", "x"), 400, "Solicitud inválida", new ArgumentOutOfRangeException("p", "x").Message },
        { new InvalidOperationException("x"), 409, "Operación no permitida", "x" },
        { new Exception("secreto"), 500, "Error interno del servidor", "Ocurrió un error inesperado." },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task TryHandleAsync_MapeaExcepcionAProblemDetails(
        Exception exception, int status, string title, string detail)
    {
        var (handler, context) = Crear();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        var body = Leer(context);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(status, root.GetProperty("status").GetInt32());
        Assert.Equal(title, root.GetProperty("title").GetString());
        Assert.Equal(detail, root.GetProperty("detail").GetString());
        Assert.Equal("GET /api/prueba", root.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task TryHandleAsync_500_NoExponeMensajeDeLaExcepcion()
    {
        var (handler, context) = Crear();

        await handler.TryHandleAsync(context, new Exception("secreto"), CancellationToken.None);

        Assert.DoesNotContain("secreto", Leer(context));
    }

    private static (GlobalExceptionHandler, DefaultHttpContext) Crear()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = "GET";
        context.Request.Path = "/api/prueba";
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            provider.GetRequiredService<IProblemDetailsService>());
        return (handler, context);
    }

    private static string Leer(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }
}
