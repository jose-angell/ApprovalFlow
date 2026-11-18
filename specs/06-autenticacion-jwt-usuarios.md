# SPEC 06 — Autenticación JWT, login, UsersController y autorización por rol

> **Estado:** aprobado
> **Depende de:** SPEC 04, SPEC 05
> **Fecha:** 2026-11-18
> **Objetivo:** Emitir tokens JWT desde `POST /api/auth/login`, validarlos en la API y exponer `UsersController` protegido por rol (Admin para la gestión, cualquier autenticado para `/me`), con seed del primer Admin y Swagger con Bearer en Development.

## Alcance

**Dentro:**

- Sección `Jwt` (`Issuer`, `Audience`, `Key`, `ExpirationMinutes`) en `appsettings.json` con `Key` vacía, y valor de desarrollo de `Key` en `appsettings.Development.json`.
- `ApprovalFlow.Infrastructure/Security/JwtOptions.cs`, validado al arrancar (`ValidateOnStart`). Si `Key` falta o tiene menos de 32 bytes, la API no arranca.
- `ApprovalFlow.Application/Abstractions/IJwtTokenGenerator.cs` y el record `AccessToken`.
- `ApprovalFlow.Infrastructure/Security/JwtTokenGenerator.cs` con `Microsoft.IdentityModel.JsonWebTokens` 8.x.
- `ApprovalFlow.Application/Auth/AuthUseCase.cs`, `Abstractions/UseCases/IAuthUseCase.cs` y `Auth/Dtos/` (`LoginDto`, `LoginResponseDto`).
- Cambios en `UserUseCase`: `ChangeOwnPasswordAsync` (nuevo, con `ChangeOwnPasswordDto`) y parámetro `currentUserId` en `ChangeRoleAsync` y `DeactivateAsync` para impedir que un Admin se cambie el rol o se desactive a sí mismo.
- Autenticación JwtBearer en la API (`Microsoft.AspNetCore.Authentication.JwtBearer` 10.x) en `ApprovalFlow/Extensions/AuthenticationExtensions.cs`, con respuestas `ProblemDetails` en el challenge (401) y en el forbidden (403).
- `ApprovalFlow/Authorization/Roles.cs` con constantes de rol y `ApprovalFlow/Extensions/ClaimsPrincipalExtensions.cs` con `GetUserId()`.
- `ApprovalFlow/Controllers/AuthController.cs` y `ApprovalFlow/Controllers/UsersController.cs`.
- Seed del primer Admin al arrancar: `ApprovalFlow.Infrastructure/Persistence/AdminSeeder.cs` con la sección `SeedAdmin` (`FullName`, `Email`, `Password`).
- Swagger UI con esquema Bearer solo en Development (`Swashbuckle.AspNetCore` 10.x).
- Borrar `ApprovalFlow/Controllers/WeatherForecastController.cs` y `ApprovalFlow/WeatherForecast.cs`.
- Actualizar `ApprovalFlow/ApprovalFlow.http` con login y llamadas a `/api/users` con Bearer.
- Pruebas unitarias (`JwtTokenGenerator`, `AuthUseCase`, cambios de `UserUseCase`) y de integración con `WebApplicationFactory` + EF Core InMemory.
- Actualizar `CLAUDE.md` (Auth, seguridad JWT, controllers y seed).

**Fuera de alcance (para futuras specs):**

- Refresh tokens, logout y revocación de tokens.
- Revalidar `IsActive` o el rol contra la BD en cada petición.
- Políticas de autorización con nombre o reglas por recurso (p. ej. "solo el dueño de la solicitud").
- Controllers de `Department` y `PurchaseRequest`.
- Bloqueo de cuenta por intentos fallidos y rate limiting del login.
- Recuperación de contraseña por email.
- Paginación y filtros del listado de usuarios.
- Mapeo de `DbUpdateException` (índice único de email) a 409.
- Gestión de secretos con user-secrets o Key Vault.

## Modelo de datos

Esta spec no cambia entidades ni la base de datos. No hay migración nueva.

### Configuración

```json
// appsettings.json
"Jwt": { "Issuer": "ApprovalFlow", "Audience": "ApprovalFlow", "Key": "", "ExpirationMinutes": 60 },
"SeedAdmin": { "FullName": "", "Email": "", "Password": "" }
```

- `appsettings.Development.json` define `Jwt:Key` (≥ 32 caracteres) y `SeedAdmin` con `FullName = "Administrador"`, `Email = "admin@approvalflow.local"` y una contraseña de desarrollo.

### `JwtOptions` (`ApprovalFlow.Infrastructure/Security/`)

```csharp
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 60;
}
```

- Se registra en `AddInfrastructure` con `AddOptions<JwtOptions>().Bind(...).Validate(...).ValidateOnStart()`. Reglas: `Issuer`, `Audience` y `Key` no vacíos, `Key` de al menos 32 bytes UTF-8 y `ExpirationMinutes > 0`.

### `IJwtTokenGenerator` (`ApprovalFlow.Application/Abstractions/`)

```csharp
public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}
```

- `JwtTokenGenerator` firma con HMAC-SHA256 usando `JsonWebTokenHandler`. Se registra como singleton en `AddInfrastructure`.
- `ExpiresAt` = `DateTime.UtcNow + ExpirationMinutes`.

Claims del token:

| Claim           | Valor                                                        |
| --------------- | ------------------------------------------------------------ |
| `sub`           | `User.Id`                                                    |
| `email`         | `User.Email`                                                 |
| `name`          | `User.FullName`                                              |
| `role`          | Nombre del enum: `Admin`, `Requester`, `Approver`, `Finance` |
| `department_id` | `User.DepartmentId`, solo si no es nulo                      |
| `jti`           | `Guid.NewGuid()`                                             |

### DTOs

Namespace `ApprovalFlow.Application.Auth.Dtos`:

| DTO                | Propiedades y anotaciones                                                   |
| ------------------ | --------------------------------------------------------------------------- |
| `LoginDto`         | `Email` `[Required]` `[EmailAddress]` · `Password` `[Required]`             |
| `LoginResponseDto` | `AccessToken` (`string`), `ExpiresAt` (`DateTime`, UTC), `User` (`UserDto`) |

Namespace `ApprovalFlow.Application.Users.Dtos`:

| DTO                    | Propiedades y anotaciones                                                                       |
| ---------------------- | ----------------------------------------------------------------------------------------------- |
| `ChangeOwnPasswordDto` | `CurrentPassword` `[Required]` · `NewPassword` `[Required]` `[MinLength(8)]` `[MaxLength(100)]` |

### `IAuthUseCase` / `AuthUseCase` (`ApprovalFlow.Application/Auth/`)

Constructor primario `(IApplicationDbContext context, IPasswordHasher passwordHasher, IJwtTokenGenerator tokenGenerator)`. Registro scoped en `AddApplication()`.

| Método                                                                            | Comportamiento                                                                                                                      |
| --------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)` | Normaliza el email (`Trim().ToLowerInvariant()`), busca el usuario con `AsNoTracking()`, verifica la contraseña y genera el token. |

- Email inexistente, contraseña incorrecta o usuario inactivo → `UnauthorizedException("Credenciales inválidas.")`. El mismo mensaje en los tres casos.

### Cambios en `IUserUseCase` / `UserUseCase`

| Método                                                                                                  | Cambio                                                                                                                                                                                                              |
| ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, Guid currentUserId, CancellationToken ct = default)`   | Si `id == currentUserId` → `ForbiddenException("No puedes cambiar tu propio rol.")`.                                                                                                                                |
| `DeactivateAsync(Guid id, Guid currentUserId, CancellationToken ct = default)`                          | Si `id == currentUserId` → `ForbiddenException("No puedes desactivar tu propio usuario.")`.                                                                                                                         |
| `ChangeOwnPasswordAsync(Guid userId, ChangeOwnPasswordDto dto, CancellationToken ct = default)` (nuevo) | Si `CurrentPassword` no verifica → `ArgumentException("La contraseña actual no es correcta.")` (400). Si verifica, hashea `NewPassword` y llama `User.ChangePasswordHash`. Usuario inexistente → `NotFoundException`. |

- La comprobación de "sobre sí mismo" va antes de buscar al usuario.
- Las pruebas existentes de `ChangeRoleAsync` y `DeactivateAsync` se adaptan a la nueva firma.

### Autenticación en la API (`ApprovalFlow/Extensions/AuthenticationExtensions.cs`)

- `AddJwtAuthentication(this IServiceCollection, IConfiguration)` configura `JwtBearer` con `MapInboundClaims = false` y `TokenValidationParameters`: issuer, audience, firma y vida validados, `ClockSkew = 30 s`, `NameClaimType = "name"`, `RoleClaimType = "role"`.
- `JwtBearerEvents.OnChallenge`: `HandleResponse()` y escribe `ProblemDetails` 401 con `title` `"No autenticado"` y `detail` `"Se requiere un token válido."`.
- `JwtBearerEvents.OnForbidden`: escribe `ProblemDetails` 403 con `title` `"Acceso denegado"` y `detail` `"No tienes permisos para este recurso."`.
- Ambos usan `IProblemDetailsService`, igual que `GlobalExceptionHandler` (mismo `instance` y `traceId`).
- `Program.cs`: `app.UseAuthentication()` antes de `app.UseAuthorization()`.

### `Roles` (`ApprovalFlow/Authorization/Roles.cs`)

```csharp
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Requester = nameof(UserRole.Requester);
    public const string Approver = nameof(UserRole.Approver);
    public const string Finance = nameof(UserRole.Finance);
}
```

### `ClaimsPrincipalExtensions.GetUserId()`

Lee el claim `sub` y lo convierte a `Guid`. Si falta o no es válido → `UnauthorizedException("Token sin identificador de usuario.")`.

### Endpoints

`AuthController` (`[ApiController]`, `[Route("api/auth")]`):

| Método y ruta          | Autorización       | Respuesta              |
| ---------------------- | ------------------ | ---------------------- |
| `POST /api/auth/login` | `[AllowAnonymous]` | 200 `LoginResponseDto` |

`UsersController` (`[ApiController]`, `[Route("api/users")]`, `[Authorize]` a nivel de clase):

| Método y ruta                              | Autorización | Use case                         | Respuesta                  |
| ------------------------------------------ | ------------ | -------------------------------- | -------------------------- |
| `GET /api/users`                           | Admin        | `GetAllAsync`                    | 200 `UserDto[]`            |
| `GET /api/users/{id:guid}`                 | Admin        | `GetByIdAsync`                   | 200 `UserDto`              |
| `POST /api/users`                          | Admin        | `CreateAsync`                    | 201 `UserDto` + `Location` |
| `PUT /api/users/{id:guid}`                 | Admin        | `UpdateProfileAsync`             | 200 `UserDto`              |
| `PUT /api/users/{id:guid}/role`            | Admin        | `ChangeRoleAsync`                | 200 `UserDto`              |
| `POST /api/users/{id:guid}/activate`       | Admin        | `ActivateAsync`                  | 204                        |
| `POST /api/users/{id:guid}/deactivate`     | Admin        | `DeactivateAsync`                | 204                        |
| `POST /api/users/{id:guid}/reset-password` | Admin        | `ResetPasswordAsync`             | 204                        |
| `GET /api/users/me`                        | Autenticado  | `GetByIdAsync(User.GetUserId())` | 200 `UserDto`              |
| `PUT /api/users/me/password`               | Autenticado  | `ChangeOwnPasswordAsync`         | 204                        |

- "Admin" = `[Authorize(Roles = Roles.Admin)]` por acción. Las acciones `/me` solo heredan `[Authorize]`.
- El controller solo lee `User.GetUserId()` y delega. No captura excepciones.
- `[ApiController]` ejecuta las DataAnnotations: un DTO inválido responde 400 `ValidationProblemDetails`.

### Seed del Admin (`ApprovalFlow.Infrastructure/Persistence/AdminSeeder.cs`)

- Extensión `Task SeedAdminAsync(this IServiceProvider services, CancellationToken ct = default)`, llamada en `Program.cs` después de `Build()` dentro de un scope.
- Lee `SeedAdmin`. Si `Email` o `Password` están vacíos → no hace nada (log `Information`).
- Si ya existe algún usuario con `Role == Admin` → no hace nada.
- Si el email ya está en uso por otro usuario → no crea nada y registra `Warning`.
- Si no, crea `User.Create(FullName, Email, hash, UserRole.Admin, null)` y guarda. `FullName` vacío → `"Administrador"`.
- No ejecuta migraciones.

### Swagger (solo Development)

- `AddEndpointsApiExplorer()` + `AddSwaggerGen` con `SecurityDefinition` `Bearer` (`SecuritySchemeType.Http`, `scheme = "bearer"`, `BearerFormat = "JWT"`) y requisito global.
- `UseSwagger()` + `UseSwaggerUI()` dentro de `if (app.Environment.IsDevelopment())`. UI en `/swagger`.

## Plan de implementación

1. Agregar la sección `Jwt` a `appsettings.json` y `appsettings.Development.json`. Crear `Application/Abstractions/IJwtTokenGenerator.cs` (con `AccessToken`), `Infrastructure/Security/JwtOptions.cs` y `Infrastructure/Security/JwtTokenGenerator.cs` (paquete `Microsoft.IdentityModel.JsonWebTokens` 8.x). Registrar opciones y generador en `AddInfrastructure`. Comprobar con `dotnet build ApprovalFlow.slnx` y `dotnet run` (arranca; con `Jwt:Key` vacía falla al arrancar).
2. Crear `Application/Auth/Dtos/LoginDto.cs`, `LoginResponseDto.cs`, `Abstractions/UseCases/IAuthUseCase.cs` y `Auth/AuthUseCase.cs`. Registrar en `AddApplication()`. Comprobar con `dotnet build`.
3. Crear `Users/Dtos/ChangeOwnPasswordDto.cs`. Agregar `currentUserId` a `ChangeRoleAsync` y `DeactivateAsync`, y el método `ChangeOwnPasswordAsync`. Adaptar `UserUseCaseTests` a las nuevas firmas. Comprobar con `dotnet test`.
4. Agregar `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x a la API. Crear `Extensions/AuthenticationExtensions.cs`, `Extensions/ClaimsPrincipalExtensions.cs` y `Authorization/Roles.cs`. En `Program.cs`, llamar `AddJwtAuthentication` y `UseAuthentication()` antes de `UseAuthorization()`. Comprobar con `dotnet run`.
5. Crear `Controllers/AuthController.cs`. Borrar `WeatherForecastController.cs` y `WeatherForecast.cs`. Comprobar con `dotnet run` y un `POST /api/auth/login` con credenciales incorrectas → 401 `ProblemDetails`.
6. Crear `Controllers/UsersController.cs` con las diez acciones. Comprobar con `dotnet run`: `GET /api/users` sin token → 401 `ProblemDetails`.
7. Agregar la sección `SeedAdmin` a ambos appsettings. Crear `Infrastructure/Persistence/AdminSeeder.cs` y llamarlo en `Program.cs`. Comprobar con `dotnet run` contra Postgres: se crea el Admin, el login devuelve token y `GET /api/users` con ese token → 200.
8. Agregar `Swashbuckle.AspNetCore` 10.x a la API y configurar Swagger con Bearer solo en Development. Comprobar abriendo `/swagger` y usando el botón Authorize.
9. Actualizar `ApprovalFlow/ApprovalFlow.http` con login, `GET /api/users`, `GET /api/users/me`, `POST /api/users` y `PUT /api/users/me/password` usando una variable `@token`.
10. Crear `Tests/Infrastructure/JwtTokenGeneratorTests.cs` y `Tests/Application/AuthUseCaseTests.cs`. Agregar a `UserUseCaseTests` los casos de autoprotección y `ChangeOwnPasswordAsync`. Comprobar con `dotnet test`.
11. Agregar `Microsoft.AspNetCore.Mvc.Testing` 10.x a `ApprovalFlow.Tests` y `public partial class Program;` al final de `Program.cs`. Crear `Tests/Api/Integration/ApiFactory.cs` (entorno `Testing`, configuración en memoria con `Jwt`, connection string ficticia, sin `SeedAdmin`; reemplaza el registro Npgsql de `AppDbContext` por InMemory con nombre único), `AuthControllerTests.cs` y `UsersControllerTests.cs`. Comprobar con `dotnet test`.
12. Actualizar `CLAUDE.md`: `Auth/`, `IJwtTokenGenerator`, `Infrastructure/Security`, controllers, `Roles`, `AdminSeeder`, Swagger y comando de pruebas de integración.

## Criterios de aceptación

- [ ] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [ ] `ApprovalFlow.Application.csproj` no referencia ASP.NET Core, JwtBearer, `Microsoft.IdentityModel.*`, Npgsql ni Infrastructure.
- [ ] `ApprovalFlow.Domain.csproj` sigue sin `PackageReference` ni `ProjectReference`.
- [ ] Ya no existen `WeatherForecastController.cs` ni `WeatherForecast.cs`.
- [ ] Con `Jwt:Key` vacía o de menos de 32 bytes, la API no arranca (falla `ValidateOnStart`).
- [ ] `JwtTokenGenerator.Generate` produce un token válido con el issuer, audience y key configurados, con los claims `sub`, `email`, `name`, `role` y `jti`, y `ExpiresAt` ≈ ahora + `ExpirationMinutes`.
- [ ] El claim `department_id` aparece solo si el usuario tiene departamento.
- [ ] `AuthUseCase.LoginAsync` con credenciales correctas devuelve token, `ExpiresAt` y `UserDto`. El email se compara sin importar mayúsculas ni espacios.
- [ ] `LoginAsync` con email inexistente, contraseña incorrecta o usuario inactivo lanza `UnauthorizedException` con el mensaje `"Credenciales inválidas."` en los tres casos.
- [ ] `ChangeRoleAsync` y `DeactivateAsync` con `id == currentUserId` lanzan `ForbiddenException` y no modifican al usuario.
- [ ] `ChangeOwnPasswordAsync` con contraseña actual incorrecta lanza `ArgumentException`. Con la correcta, la nueva verifica y la anterior no.
- [ ] `POST /api/auth/login` correcto → 200 con `accessToken`, `expiresAt` y `user`, y `user` no contiene `passwordHash`.
- [ ] `POST /api/auth/login` incorrecto → 401 `application/problem+json` con `title` `"No autenticado"`.
- [ ] `POST /api/auth/login` sin `email` → 400 `ValidationProblemDetails`.
- [ ] Cualquier endpoint de `/api/users` sin token o con token inválido/expirado → 401 `ProblemDetails`.
- [ ] Endpoints Admin de `/api/users` con token de `Requester` → 403 `ProblemDetails` con `title` `"Acceso denegado"`.
- [ ] `GET /api/users/me` con token de cualquier rol → 200 con el usuario del token.
- [ ] `PUT /api/users/me/password` correcto → 204, y el login posterior funciona con la nueva contraseña.
- [ ] `POST /api/users` como Admin → 201 con cabecera `Location` hacia `GET /api/users/{id}`.
- [ ] `POST /api/users` con `password` de 3 caracteres → 400 `ValidationProblemDetails`.
- [ ] `POST /api/users/{id}/deactivate` sobre el propio Admin → 403.
- [ ] Con `SeedAdmin` configurado y sin Admins en la BD, al arrancar se crea un Admin activo con ese email. Al arrancar de nuevo no se crea otro.
- [ ] Con `SeedAdmin` vacío, el arranque no crea usuarios ni falla.
- [ ] En Development, `/swagger` carga y muestra el botón Authorize con esquema Bearer. Fuera de Development, `/swagger` responde 404.
- [ ] `dotnet test` pasa todas las pruebas, tanto las anteriores como las nuevas (unitarias e integración).
- [ ] `dotnet run --project ApprovalFlow/ApprovalFlow.Api.csproj` arranca sin excepciones.

## Decisiones

- **Sí:** una sola spec para JWT, login, controller y roles, como se decidió en SPEC 05. **No:** dividir, porque el login sin controller ni roles no se verifica de punta a punta.
- **Sí:** solo access token con vigencia de 60 minutos. **No:** refresh tokens ni logout; requieren tabla y migración (otra spec).
- **Sí:** `Jwt:Key` en `appsettings.Development.json`, vacía en `appsettings.json`, igual que la connection string. **No:** user-secrets ni `.env`, por ahora.
- **Sí:** `ValidateOnStart` con key de al menos 32 bytes. Falla rápido en vez de en el primer login.
- **Sí:** `IJwtTokenGenerator` en `Application/Abstractions` e implementación en `Infrastructure/Security`. Application no conoce librerías de tokens.
- **Sí:** `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`). **No:** `JwtSecurityTokenHandler`, que está en modo legado.
- **Sí:** `AuthUseCase` separado en `Application/Auth/`. **No:** `LoginAsync` dentro de `UserUseCase`; son responsabilidades distintas.
- **Sí:** 401 con `"Credenciales inválidas."` para email inexistente, contraseña incorrecta y usuario inactivo. **No:** 403 para inactivo; revelaría qué emails existen.
- **Sí:** el token sigue válido hasta expirar aunque se desactive al usuario o cambie su rol. **No:** consultar la BD por petición. La vigencia corta lo mitiga.
- **Sí:** claims `sub`, `email`, `name`, `role` (nombre del enum), `department_id` y `jti`, con `MapInboundClaims = false`. Nombres cortos y estables.
- **Sí:** `[Authorize(Roles = Roles.Admin)]` con constantes basadas en `nameof(UserRole.X)`. **No:** políticas con nombre; no hacen falta en este alcance.
- **Sí:** gestión de usuarios solo Admin, y `/me` + `/me/password` para cualquier autenticado.
- **Sí:** un Admin no puede cambiarse el rol ni desactivarse a sí mismo → `ForbiddenException` (403). El controller pasa `currentUserId` al use case; la regla vive en Application.
- **Sí:** contraseña actual incorrecta en `/me/password` → 400. **No:** 401, porque el cliente lo interpretaría como sesión caducada.
- **Sí:** `ProblemDetails` en el challenge y el forbidden de JwtBearer, con los mismos `title` que `GlobalExceptionHandler`. Respuestas consistentes.
- **Sí:** seed del primer Admin al arrancar desde la sección `SeedAdmin`, solo si no existe ningún Admin. **No:** `HasData` en una migración, porque obliga a un hash fijo en el código.
- **Sí:** Swagger con Swashbuckle y esquema Bearer solo en Development. **No:** `Microsoft.AspNetCore.OpenApi` + Scalar.
- **Sí:** pruebas unitarias más integración con `WebApplicationFactory` + InMemory. **No:** Testcontainers por ahora.
- **Sí:** borrar `WeatherForecast`, que es plantilla.

## Riesgos

| Riesgo                                                                                                                                         | Mitigación                                                                                                                         |
| ---------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Token válido tras desactivar al usuario o cambiarle el rol                                                                                     | Vigencia de 60 minutos. La revalidación por petición va en otra spec.                                                              |
| `Jwt:Key` y la contraseña de `SeedAdmin` de desarrollo quedan en el repo                                                                       | Solo en `appsettings.Development.json`. En otros entornos se definen por variables de entorno. `appsettings.json` las deja vacías. |
| `AddInfrastructure` registra Npgsql y en EF Core 10 quitar el registro exige eliminar también `IDbContextOptionsConfiguration<AppDbContext>`   | `ApiFactory` elimina ambos registros antes de agregar InMemory. Si no, las pruebas de integración fallan al resolver el contexto.  |
| Sin `MapInboundClaims = false`, `role` y `sub` se remapean a URIs largas y `[Authorize(Roles)]` no encuentra el rol                            | Se fija `MapInboundClaims = false`, `RoleClaimType = "role"` y `NameClaimType = "name"`. Lo cubre la prueba de 403/200 por rol.    |
| La ruta `me` choca con `{id}`                                                                                                                  | Las rutas por id usan la restricción `{id:guid}`.                                                                                  |
| Diferencia de tiempo de respuesta entre email inexistente y contraseña incorrecta revela cuentas                                               | Se acepta por ahora. Rate limiting y bloqueo van en otra spec.                                                                     |
| El seed corre en cada arranque y falla si la BD no está migrada                                                                                | El seed no migra. El error se ve en el arranque. Hay que ejecutar `dotnet ef database update` antes.                               |
| Swashbuckle 10.x usa `Microsoft.OpenApi` 2.x y cambia la API de `OpenApiSecurityScheme`                                                        | Seguir la documentación de la versión instalada. Lo cubre el criterio de `/swagger` en Development.                                |

## Qué **no** está en esta spec

- Refresh tokens, logout y revocación.
- Revalidación de usuario o rol por petición.
- Políticas con nombre y autorización por recurso.
- Controllers de `Department` y `PurchaseRequest`.
- Bloqueo de cuenta y rate limiting.
- Recuperación de contraseña por email.
- Paginación y filtros de usuarios.
- Mapeo de `DbUpdateException` a 409.
- User-secrets o Key Vault.

Cada uno de estos puntos, si se aborda, va en su propia spec.
