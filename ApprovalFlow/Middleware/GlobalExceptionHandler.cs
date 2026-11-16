using ApprovalFlow.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalFlow.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto", exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acceso denegado", exception.Message),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "No autenticado", exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, "Solicitud inválida", exception.Message),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operación no permitida", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor", "Ocurrió un error inesperado.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Error no controlado en {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Error {Status} en {Method} {Path}: {Message}",
                status, httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            }
        });

        return true;
    }
}
