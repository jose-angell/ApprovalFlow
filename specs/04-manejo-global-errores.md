# SPEC 04 — Excepciones propias y handler global de errores

> **Estado:** aprobado
> **Depende de:** SPEC 02, SPEC 03
> **Fecha:** 2026-11-16
> **Objetivo:** Crear en `ApprovalFlow.Application/Exceptions` las excepciones `ConflictException`, `ForbiddenException`, `NotFoundException` y `UnauthorizedException`, y un `GlobalExceptionHandler` (`IExceptionHandler`) en la API que las registre con `ILogger` y las convierta en respuestas `ProblemDetails` con el código HTTP adecuado.

## Alcance

**Dentro:**

- Carpeta `ApprovalFlow.Application/Exceptions/` con `ConflictException.cs`, `ForbiddenException.cs`, `NotFoundException.cs` y `UnauthorizedException.cs`. Hay una clase por archivo. Cada una hereda de `System.Exception` y tiene un único constructor `(string message)`.
- Borrar `ApprovalFlow.Application/Class1.cs`.
- `ApprovalFlow/Middleware/GlobalExceptionHandler.cs`, que implementa `Microsoft.AspNetCore.Diagnostics.IExceptionHandler`.
- Mapeo de excepciones a códigos HTTP, que incluye las excepciones .NET que ya lanza el dominio (`ArgumentException` e `InvalidOperationException`).
- Respuesta en formato `ProblemDetails` (RFC 7807) escrita con `IProblemDetailsService`.
- Log con `ILogger<GlobalExceptionHandler>`: `Warning` para 4xx y `Error` para 500.
- Registro en `Program.cs`: `AddProblemDetails()`, `AddExceptionHandler<GlobalExceptionHandler>()` y `app.UseExceptionHandler()` como primer middleware del pipeline.
- Referencia de proyecto `ApprovalFlow.Tests` → `ApprovalFlow.Api`, `FrameworkReference` a `Microsoft.AspNetCore.App` en el proyecto de pruebas y pruebas xUnit del handler.

**Fuera de alcance (para futuras specs):**

- Cambiar el dominio para que lance excepciones propias. Sigue lanzando `ArgumentException` e `InvalidOperationException`.
- `ValidationException` con lista de errores por campo (`ValidationProblemDetails`) y FluentValidation.
- Más excepciones propias (`BadRequestException`, `DomainException`, etc.).
- Códigos de error propios del negocio (`errorCode`) dentro de la respuesta.
- Autenticación y autorización reales (JWT, políticas). Esta spec solo define cómo se responde un 401/403.
- Casos de uso, repositorios y controladores reales. Tampoco se borra `WeatherForecastController`.
- Mapear excepciones de EF Core o Npgsql (por ejemplo, una violación de índice único → 409).
- Logging estructurado externo (Serilog, OpenTelemetry).
- Localización o traducción de los mensajes.

## Modelo de datos

### Excepciones (`ApprovalFlow.Application/Exceptions/`)

Namespace: `ApprovalFlow.Application.Exceptions`.

```csharp
public sealed class NotFoundException(string message) : Exception(message);
```

`ConflictException`, `ForbiddenException` y `UnauthorizedException` siguen la misma forma. No tienen propiedades extra, constructores sin mensaje ni `innerException`.

### Mapeo (`GlobalExceptionHandler`)

| Excepción                          | Código HTTP                 | `title`                  | `detail`                             | Nivel de log |
| ---------------------------------- | --------------------------- | ------------------------ | ------------------------------------ | ------------ |
| `NotFoundException`                | 404 Not Found               | `"Recurso no encontrado"` | `exception.Message`                 | `Warning`    |
| `ConflictException`                | 409 Conflict                | `"Conflicto"`            | `exception.Message`                  | `Warning`    |
| `ForbiddenException`               | 403 Forbidden               | `"Acceso denegado"`      | `exception.Message`                  | `Warning`    |
| `UnauthorizedException`            | 401 Unauthorized            | `"No autenticado"`       | `exception.Message`                  | `Warning`    |
| `ArgumentException` (y derivadas)  | 400 Bad Request             | `"Solicitud inválida"`   | `exception.Message`                  | `Warning`    |
| `InvalidOperationException`        | 409 Conflict                | `"Operación no permitida"` | `exception.Message`                | `Warning`    |
| Cualquier otra                     | 500 Internal Server Error   | `"Error interno del servidor"` | `"Ocurrió un error inesperado."` | `Error`      |

- El mapeo se hace con un `switch` sobre el tipo de excepción. `ArgumentNullException` y `ArgumentOutOfRangeException` caen en `ArgumentException`.
- El 500 nunca expone `exception.Message` ni el stack trace, ni siquiera en Development. El detalle completo va solo al log.

### Respuesta (`ProblemDetails`)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Recurso no encontrado",
  "status": 404,
  "detail": "La solicitud 'abc' no existe.",
  "instance": "GET /api/purchase-requests/abc",
  "traceId": "00-..."
}
```

- `Content-Type: application/problem+json`.
- `type`: lo completa `AddProblemDetails()` según el `status`.
- `instance`: `"{Method} {Path}"` de la petición.
- `traceId`: lo agrega `AddProblemDetails()` desde `Activity.Current?.Id ?? HttpContext.TraceIdentifier`.
- `TryHandleAsync` fija `httpContext.Response.StatusCode`, llama a `IProblemDetailsService.TryWriteAsync` y devuelve `true`.

### Registro (`ApprovalFlow/Program.cs`)

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// ...
app.UseExceptionHandler(); // primera línea del pipeline
```

## Plan de implementación

1. Crear `ApprovalFlow.Application/Exceptions/` con las cuatro excepciones. Borrar `ApprovalFlow.Application/Class1.cs`. Comprobar con `dotnet build ApprovalFlow.slnx`.
2. Crear `ApprovalFlow/Middleware/GlobalExceptionHandler.cs` con constructor primario `(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService)` y el mapeo de la tabla. Comprobar con `dotnet build`.
3. Registrar en `Program.cs` `AddProblemDetails()`, `AddExceptionHandler<GlobalExceptionHandler>()` y `app.UseExceptionHandler()` antes de `UseHttpsRedirection`. Comprobar con `dotnet build` y con `dotnet run` (la API arranca sin errores).
4. En `ApprovalFlow.Tests.csproj`, agregar `<FrameworkReference Include="Microsoft.AspNetCore.App" />` y la referencia a `ApprovalFlow\ApprovalFlow.Api.csproj`. Crear `ApprovalFlow.Tests/Api/GlobalExceptionHandlerTests.cs`. Usar `DefaultHttpContext` con `Response.Body = new MemoryStream()`, un `IProblemDetailsService` real (obtenido de un `ServiceCollection` con `AddProblemDetails()` y `AddLogging()`) y `NullLogger<GlobalExceptionHandler>`. Agregar un caso por fila de la tabla de mapeo (`[Theory]`). Comprobar con `dotnet test`.
5. Actualizar `CLAUDE.md`: mencionar `Application/Exceptions` y el handler global en la sección de arquitectura.

## Criterios de aceptación

- [ ] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [ ] Existen `ConflictException.cs`, `ForbiddenException.cs`, `NotFoundException.cs` y `UnauthorizedException.cs` en `ApprovalFlow.Application/Exceptions/`, con namespace `ApprovalFlow.Application.Exceptions`. `ApprovalFlow.Application/Class1.cs` ya no existe.
- [ ] Cada excepción hereda de `System.Exception` y tiene un solo constructor público `(string message)`.
- [ ] `ApprovalFlow.Application.csproj` sigue sin `PackageReference` y sin referencias a Infrastructure o a ASP.NET Core.
- [ ] `ApprovalFlow.Domain.csproj` sigue sin `PackageReference` ni `ProjectReference`.
- [ ] `GlobalExceptionHandler` implementa `IExceptionHandler` y está en `ApprovalFlow/Middleware/`.
- [ ] `Program.cs` llama a `AddProblemDetails()`, `AddExceptionHandler<GlobalExceptionHandler>()` y `UseExceptionHandler()`. `UseExceptionHandler()` es el primer middleware.
- [ ] Una `NotFoundException("x")` produce 404, `Content-Type` `application/problem+json`, `title` `"Recurso no encontrado"` y `detail` `"x"`.
- [ ] `ConflictException` → 409, `ForbiddenException` → 403, `UnauthorizedException` → 401. En los tres, `detail` es el mensaje de la excepción.
- [ ] `ArgumentException` y `ArgumentNullException` → 400. `InvalidOperationException` → 409.
- [ ] Una `Exception` genérica con mensaje `"secreto"` produce 500, y el cuerpo no contiene `"secreto"`.
- [ ] Toda respuesta incluye `status`, `title`, `detail`, `instance` y `traceId`.
- [ ] `TryHandleAsync` devuelve `true` en todos los casos.
- [ ] `dotnet test` pasa todas las pruebas, tanto las de SPEC 02 como las nuevas de `GlobalExceptionHandlerTests`.
- [ ] `dotnet run --project ApprovalFlow/ApprovalFlow.Api.csproj` arranca sin excepciones.

## Decisiones

- **Sí:** excepciones en `ApprovalFlow.Application/Exceptions/` (en plural). **No:** carpeta `Exception`, porque el namespace `ApprovalFlow.Application.Exception` choca con `System.Exception` al heredar.
- **Sí:** una clase por archivo, `sealed`, con un único constructor `(string message)`. Es lo mínimo que se pidió. **No:** `innerException` ni propiedades extra por ahora.
- **Sí:** las excepciones heredan directamente de `System.Exception`. **No:** una clase base común (`AppException`). Con cuatro clases y un `switch` por tipo no hace falta.
- **Sí:** las excepciones viven en Application, porque las lanzan los casos de uso. Application no conoce HTTP. El mapeo a códigos vive solo en la API.
- **Sí:** `IExceptionHandler` + `UseExceptionHandler()`. Es el mecanismo nativo de .NET 8+ y se integra con `ProblemDetails`. **No:** un middleware propio con `try/catch` (`RequestDelegate`). Duplica lo que ya da el framework.
- **Sí:** `ProblemDetails` estándar (RFC 7807) con `IProblemDetailsService`. **No:** una clase propia `ErrorDetail`. El formato estándar ya trae `traceId` y `type` y lo entienden los clientes.
- **Sí:** mapear también `ArgumentException` → 400 e `InvalidOperationException` → 409. Así los errores del dominio de SPEC 02 no responden 500. **No:** 422 para `InvalidOperationException`. Se usa 409 para coincidir con `ConflictException`, porque son transiciones de estado no permitidas.
- **No:** cambiar el dominio para que lance excepciones propias. El dominio no puede referenciar Application.
- **Sí:** el 500 responde un mensaje genérico en todos los entornos. **No:** exponer `exception.Message` en Development. Evita filtrar datos internos por accidente.
- **Sí:** `Warning` para 4xx y `Error` (con la excepción) para 500. Los 4xx son errores esperados del cliente.
- **Sí:** el handler en `ApprovalFlow/Middleware/`. Funciona como parte del pipeline de middleware.
- **Sí:** pruebas unitarias del handler con `DefaultHttpContext`. **No:** `WebApplicationFactory` en esta spec. Requiere `Microsoft.AspNetCore.Mvc.Testing` y una configuración de BD para pruebas.
- **Sí:** el proyecto de pruebas referencia la API y `Microsoft.AspNetCore.App`. Es necesario para instanciar el handler.

## Riesgos

| Riesgo                                                                                       | Mitigación                                                                                                   |
| -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| `InvalidOperationException` también la lanza el framework (p. ej. DI mal configurada) y respondería 409 en vez de 500 | Se acepta por ahora. Cuando el dominio tenga excepciones propias, se quita este mapeo en otra spec.         |
| `ArgumentException` interna de librerías respondería 400 con un mensaje técnico              | Mismo caso. Se revisa cuando exista la spec de excepciones de dominio.                                       |
| Que `UseExceptionHandler()` no quede primero y algún middleware anterior no quede cubierto | El criterio de aceptación exige que sea la primera línea del pipeline.                                       |
| Que la referencia Tests → Api arrastre la configuración de Infrastructure (connection string) | Las pruebas no construyen el host. Solo instancian el handler y un `ServiceCollection` mínimo.                |
| La respuesta ya empezó a enviarse cuando ocurre la excepción                                 | `UseExceptionHandler` no puede reescribirla. Es el comportamiento estándar y se acepta.                       |

## Qué **no** está en esta spec

- Excepciones propias en el dominio.
- `ValidationException` y errores por campo.
- Más tipos de excepción o códigos de error del negocio.
- Autenticación y autorización reales.
- Casos de uso y controladores reales.
- Mapeo de excepciones de EF Core / Npgsql.
- Logging estructurado externo.
- Localización de mensajes.

Cada uno de estos puntos, si se aborda, va en su propia spec.
