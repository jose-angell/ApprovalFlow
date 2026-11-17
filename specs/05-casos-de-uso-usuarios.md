# SPEC 05 — Casos de uso de usuarios en Application

> **Estado:** implementado
> **Depende de:** SPEC 02, SPEC 03, SPEC 04
> **Fecha:** 2026-11-17
> **Objetivo:** Crear en `ApprovalFlow.Application/Users` el caso de uso `UserUseCase` (crear, consultar, listar, editar perfil, cambiar rol, activar/desactivar y restablecer contraseña) con sus DTOs anotados con DataAnnotations, persistiendo directamente a través de `IApplicationDbContext`.

## Alcance

**Dentro:**

- Paquete `Microsoft.EntityFrameworkCore` 10.0.15 y `Microsoft.Extensions.DependencyInjection.Abstractions` 10.x en `ApprovalFlow.Application`. No se agrega Npgsql ni ningún proveedor.
- `ApprovalFlow.Application/Abstractions/IApplicationDbContext.cs`.
- `ApprovalFlow.Application/Abstractions/IPasswordHasher.cs`.
- `ApprovalFlow.Application/Users/IUserUseCase.cs` y `ApprovalFlow.Application/Users/UserUseCase.cs`.
- `ApprovalFlow.Application/Users/Dtos/` con un DTO por archivo: `CreateUserDto`, `UpdateUserProfileDto`, `ChangeUserRoleDto`, `ResetPasswordDto` y `UserDto`.
- `ApprovalFlow.Application/DependencyInjection.cs` con `AddApplication()`.
- `AppDbContext` implementa `IApplicationDbContext`. `AddInfrastructure` registra `IApplicationDbContext` y `IPasswordHasher`.
- `ApprovalFlow.Infrastructure/Security/PasswordHasher.cs` sobre `Microsoft.AspNetCore.Identity.PasswordHasher<User>` (paquete `Microsoft.Extensions.Identity.Core` 10.x en Infrastructure).
- Llamada a `builder.Services.AddApplication()` en `Program.cs`.
- Pruebas xUnit de `UserUseCase` con `Microsoft.EntityFrameworkCore.InMemory` y de `PasswordHasher`.
- Actualizar `CLAUDE.md`: Application referencia `Microsoft.EntityFrameworkCore` (solo abstracciones) y existen `Abstractions/` y `Users/`.

**Fuera de alcance (para futuras specs):**

- Autenticación JWT, endpoint de login, emisión y validación de tokens (SPEC 06).
- `UsersController` y cualquier endpoint HTTP de usuarios (SPEC 06).
- Autorización por rol (`[Authorize(Roles = ...)]`) (SPEC 06).
- Cambio de contraseña propio pidiendo la contraseña actual (SPEC 06, requiere usuario autenticado).
- Ejecutar las DataAnnotations. En esta spec solo se anotan los DTOs; las ejecutará `[ApiController]` en SPEC 06.
- Paginación, filtros y ordenamientos configurables del listado.
- Borrado físico de usuarios.
- Casos de uso de `Department` y `PurchaseRequest`.
- Repositorios y Unit of Work.
- Seed del usuario Admin.
- Mapeo de la violación del índice único de `users.email` (`DbUpdateException`) a 409.

## Modelo de datos

Esta spec no cambia entidades ni la base de datos. No hay migración nueva.

### `IApplicationDbContext` (`ApprovalFlow.Application/Abstractions/`)

Namespace: `ApprovalFlow.Application.Abstractions`.

```csharp
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Department> Departments { get; }
    DbSet<PurchaseRequest> PurchaseRequests { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

- `AppDbContext` ya expone los tres `DbSet` y `SaveChangesAsync`. Solo se agrega `: IApplicationDbContext`.
- Registro: `services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());`.

### `IPasswordHasher` (`ApprovalFlow.Application/Abstractions/`)

```csharp
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}
```

- Implementación `ApprovalFlow.Infrastructure.Security.PasswordHasher`, que envuelve `PasswordHasher<User>` (PBKDF2). `Verify` devuelve `true` para `Success` y `SuccessRehashNeeded`.
- Registro como singleton en `AddInfrastructure`.
- `Verify` no se usa en esta spec. Se deja listo para el login de SPEC 06.

### DTOs (`ApprovalFlow.Application/Users/Dtos/`)

Namespace: `ApprovalFlow.Application.Users.Dtos`. Son `record` con propiedades `init`.

| DTO                     | Propiedades y anotaciones                                                                                                                                                                                                                       |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CreateUserDto`         | `FullName` `[Required]` `[MaxLength(User.FullNameMaxLength)]` · `Email` `[Required]` `[EmailAddress]` `[MaxLength(User.EmailMaxLength)]` · `Password` `[Required]` `[MinLength(8)]` `[MaxLength(100)]` · `Role` (`UserRole`) `[Required]` `[EnumDataType(typeof(UserRole))]` · `DepartmentId` (`Guid?`) |
| `UpdateUserProfileDto`  | `FullName` y `Email`, con las mismas anotaciones que en `CreateUserDto`                                                                                                                                                                        |
| `ChangeUserRoleDto`     | `Role` `[Required]` `[EnumDataType(typeof(UserRole))]` · `DepartmentId` (`Guid?`)                                                                                                                                                              |
| `ResetPasswordDto`      | `NewPassword` `[Required]` `[MinLength(8)]` `[MaxLength(100)]`                                                                                                                                                                                 |
| `UserDto` (respuesta)   | `Id`, `FullName`, `Email`, `Role`, `DepartmentId`, `IsActive`, `CreatedAt`, `UpdatedAt`. **Nunca** incluye `PasswordHash`.                                                                                                                      |

- Las longitudes salen de las constantes de `User`, igual que en SPEC 03.
- `UserDto` tiene un método estático `FromEntity(User user)`.

### `IUserUseCase` / `UserUseCase` (`ApprovalFlow.Application/Users/`)

Namespace: `ApprovalFlow.Application.Users`. Constructor primario `(IApplicationDbContext context, IPasswordHasher passwordHasher)`, guardado como `_context` y `_passwordHasher`. Registro scoped en `AddApplication()`.

| Método                                                              | Comportamiento                                                                                                                         |
| ------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct)` | Valida email único y departamento, hashea `Password`, llama `User.Create`, agrega y guarda.                                            |
| `Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct)`          | Devuelve el usuario. Si no existe → `NotFoundException`.                                                                               |
| `Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)`     | Todos los usuarios ordenados por `FullName`, con `AsNoTracking()`. Sin paginación.                                                     |
| `Task<UserDto> UpdateProfileAsync(Guid id, UpdateUserProfileDto dto, CancellationToken ct)` | Valida email único excluyendo al propio usuario y llama `User.UpdateProfile`.                                   |
| `Task<UserDto> ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, CancellationToken ct)` | Resuelve el departamento y llama `User.ChangeRole`.                                                                       |
| `Task ActivateAsync(Guid id, CancellationToken ct)`                  | Llama `User.Activate`.                                                                                                                 |
| `Task DeactivateAsync(Guid id, CancellationToken ct)`                | Llama `User.Deactivate`.                                                                                                               |
| `Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken ct)` | Hashea `NewPassword` y llama `User.ChangePasswordHash`. No pide la contraseña actual.                                        |

Reglas comunes:

- `CancellationToken` con valor por defecto en todos los métodos.
- Unicidad de email: se normaliza con `Trim().ToLowerInvariant()` (igual que el dominio) y se comprueba con `AnyAsync` antes de guardar. Si existe → `ConflictException("Ya existe un usuario con el email '...'.")`.
- Usuario inexistente → `NotFoundException("El usuario '{id}' no existe.")`.
- `DepartmentId` informado pero inexistente → `NotFoundException("El departamento '{id}' no existe.")`. Con `DepartmentId` nulo se pasa `null` al dominio.
- Departamento inactivo, rol inválido o departamento nulo para un rol distinto de Admin: los rechaza el dominio (`InvalidOperationException` → 409, `ArgumentException` → 400). El use case no los duplica.
- El use case no captura excepciones. Las mapea `GlobalExceptionHandler` (SPEC 04).
- El use case no ejecuta las DataAnnotations.

### Registro

```csharp
// ApprovalFlow.Application/DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddScoped<IUserUseCase, UserUseCase>();
    return services;
}
```

`Program.cs`: `builder.Services.AddApplication();` antes de `AddInfrastructure`.

## Plan de implementación

1. Agregar a Application los paquetes `Microsoft.EntityFrameworkCore` 10.0.15 y `Microsoft.Extensions.DependencyInjection.Abstractions` 10.x. Crear `Abstractions/IApplicationDbContext.cs` e `Abstractions/IPasswordHasher.cs`. Comprobar con `dotnet build ApprovalFlow.slnx`.
2. Hacer que `AppDbContext` implemente `IApplicationDbContext`. Agregar a Infrastructure `Microsoft.Extensions.Identity.Core` 10.x y crear `Security/PasswordHasher.cs`. Registrar ambos en `AddInfrastructure`. Comprobar con `dotnet build`.
3. Crear `Users/Dtos/` con los cinco DTOs y `UserDto.FromEntity`. Comprobar con `dotnet build`.
4. Crear `Users/IUserUseCase.cs` y `Users/UserUseCase.cs` con `CreateAsync`, `GetByIdAsync` y `GetAllAsync`. Comprobar con `dotnet build`.
5. Completar `UserUseCase` con `UpdateProfileAsync`, `ChangeRoleAsync`, `ActivateAsync`, `DeactivateAsync` y `ResetPasswordAsync`. Comprobar con `dotnet build`.
6. Crear `ApprovalFlow.Application/DependencyInjection.cs` con `AddApplication()` y llamarlo en `Program.cs`. Comprobar con `dotnet run` (la API arranca y resuelve el contenedor sin errores).
7. Agregar `Microsoft.EntityFrameworkCore.InMemory` 10.0.15 a `ApprovalFlow.Tests`. Crear `ApprovalFlow.Tests/Infrastructure/PasswordHasherTests.cs` y `ApprovalFlow.Tests/Application/UserUseCaseTests.cs`. Cada prueba usa un `AppDbContext` InMemory con nombre de base único (`Guid.NewGuid().ToString()`) y el `PasswordHasher` real. Comprobar con `dotnet test`.
8. Actualizar `CLAUDE.md` (arquitectura: `Abstractions/`, `Users/`, EF Core en Application solo como abstracción y la regla de no usar repositorios/UoW).

## Criterios de aceptación

- [x] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [x] `ApprovalFlow.Application.csproj` referencia `Microsoft.EntityFrameworkCore` y no referencia Npgsql, Infrastructure ni ASP.NET Core.
- [x] `ApprovalFlow.Domain.csproj` sigue sin `PackageReference` ni `ProjectReference`.
- [x] Existen `Application/Abstractions/IApplicationDbContext.cs`, `Application/Abstractions/IPasswordHasher.cs`, `Application/Users/IUserUseCase.cs`, `Application/Users/UserUseCase.cs`, `Application/DependencyInjection.cs` y los cinco DTOs en `Application/Users/Dtos/`.
- [x] No existe ninguna interfaz ni clase de repositorio o Unit of Work. `UserUseCase` usa `_context` (`IApplicationDbContext`).
- [x] `AppDbContext` implementa `IApplicationDbContext`. `IApplicationDbContext`, `IPasswordHasher` e `IUserUseCase` se resuelven desde el contenedor de la API.
- [x] Todos los DTOs de entrada tienen las DataAnnotations de la tabla. `UserDto` no tiene propiedad `PasswordHash`.
- [x] `PasswordHasher.Hash("Secreta123")` devuelve un valor distinto de `"Secreta123"`, `Verify(hash, "Secreta123")` es `true` y `Verify(hash, "otra")` es `false`.
- [x] `CreateAsync` con datos válidos guarda el usuario, devuelve `IsActive = true` y el `PasswordHash` guardado verifica contra la contraseña original.
- [x] `CreateAsync` con un email ya existente (aunque difiera en mayúsculas o espacios) lanza `ConflictException`.
- [x] `CreateAsync` con un `DepartmentId` inexistente lanza `NotFoundException`.
- [x] `CreateAsync` con rol `Requester` y `DepartmentId` nulo lanza `ArgumentException` (del dominio).
- [x] `CreateAsync` con un departamento inactivo lanza `InvalidOperationException` (del dominio).
- [x] `GetByIdAsync` con un id inexistente lanza `NotFoundException`.
- [x] `GetAllAsync` devuelve los usuarios ordenados por `FullName`.
- [x] `UpdateProfileAsync` con el email de otro usuario lanza `ConflictException`. Con su propio email no lanza.
- [x] `ChangeRoleAsync` a `Admin` con `DepartmentId` nulo deja `DepartmentId = null`.
- [x] `DeactivateAsync` deja `IsActive = false` y `ActivateAsync` lo vuelve a `true`. Ambos actualizan `UpdatedAt`.
- [x] `ResetPasswordAsync` cambia el hash: la nueva contraseña verifica y la anterior no.
- [x] `UpdateProfileAsync`, `ChangeRoleAsync`, `ActivateAsync`, `DeactivateAsync` y `ResetPasswordAsync` con un id inexistente lanzan `NotFoundException`.
- [x] `dotnet test` pasa todas las pruebas, tanto las anteriores como las nuevas.
- [x] `dotnet run --project ApprovalFlow/ApprovalFlow.Api.csproj` arranca sin excepciones.

## Decisiones

- **Sí:** dividir en dos specs. SPEC 05 cubre Application y SPEC 06 cubre JWT, login, controller y roles. **No:** todo en una, porque toca tres áreas distintas.
- **Sí:** `IApplicationDbContext` inyectado como `_context` directamente en el use case. **No:** repositorios ni Unit of Work. Lo pidió el usuario; `DbContext` ya es ambos patrones.
- **Sí:** `Microsoft.EntityFrameworkCore` en Application para exponer `DbSet<T>` y usar `ToListAsync`/`AnyAsync`. Esto revierte el criterio de SPEC 03 "Application no referencia EF Core". **No:** exponer `IQueryable<T>` sin EF, porque se pierden las operaciones async.
- **Sí:** solo el paquete base de EF Core en Application. **No:** Npgsql en Application; el proveedor sigue en Infrastructure.
- **Sí:** `IApplicationDbContext` expone los tres `DbSet`. Así las siguientes specs no tienen que tocar la interfaz.
- **Sí:** `IPasswordHasher` propio en Application e implementación con `PasswordHasher<User>` de ASP.NET Identity en Infrastructure. **No:** BCrypt, porque es una dependencia externa más.
- **Sí:** `Verify` en `IPasswordHasher` desde ya. Es pequeño y lo necesita el login de SPEC 06.
- **Sí:** `IUserUseCase` + `UserUseCase` en `Application/Users/`. Facilita los mocks en las pruebas del controller de SPEC 06.
- **Sí:** interfaces en `Application/Abstractions/`. **No:** `Common/Interfaces/`.
- **Sí:** DTOs con DataAnnotations, un `record` por archivo en `Users/Dtos/`. **No:** FluentValidation.
- **Sí:** en esta spec las DataAnnotations solo se declaran. **No:** ejecutarlas con `Validator` en el use case. Las ejecutará `[ApiController]` en SPEC 06; mientras tanto el dominio sigue validando.
- **Sí:** email duplicado → `ConflictException` (409), comprobado antes de guardar. **No:** depender solo del índice único, porque produciría un 500 y InMemory no lo respeta.
- **Sí:** departamento o usuario inexistente → `NotFoundException` (404).
- **Sí:** reset administrativo de contraseña sin pedir la actual. **No:** cambio propio con contraseña actual; requiere usuario autenticado (SPEC 06).
- **Sí:** listado simple ordenado por `FullName`. **No:** paginación ni filtros por ahora.
- **No:** borrado físico. Las FKs son `Restrict` (SPEC 03); se usa `Deactivate`.
- **Sí:** pruebas con EF Core InMemory. **No:** SQLite (el mapeo es para Postgres) ni Testcontainers en esta spec.

## Riesgos

| Riesgo                                                                                     | Mitigación                                                                                                      |
| ------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| Carrera entre `AnyAsync` y `SaveChangesAsync` permite dos emails iguales a la vez          | El índice único de `users.email` lo impide en la BD; la `DbUpdateException` respondería 500. Su mapeo a 409 va en otra spec. |
| InMemory no respeta índices únicos ni FKs                                                  | Las reglas de unicidad y existencia se comprueban en el use case y se prueban ahí.                              |
| Sin controller, las DataAnnotations no se ejecutan y una contraseña de 3 caracteres pasaría | En SPEC 05 no hay entrada HTTP. SPEC 06 agrega `[ApiController]`, que las ejecuta.                             |
| EF Core en Application tienta a usar APIs de Npgsql o configuración de mapeo ahí           | Criterio de aceptación: Application no referencia Npgsql. Los mapeos siguen en `Infrastructure/Configurations`. |
| Versiones de `Microsoft.Extensions.Identity.Core` o EF desalineadas                        | Usar 10.x alineado con EF Core 10.0.15.                                                                          |

## Qué **no** está en esta spec

- JWT, login y autorización por rol.
- `UsersController` y endpoints HTTP.
- Cambio de contraseña propio con contraseña actual.
- Ejecución de las DataAnnotations.
- Paginación y filtros.
- Borrado físico.
- Casos de uso de `Department` y `PurchaseRequest`.
- Repositorios y Unit of Work.
- Seed del Admin.
- Mapeo de `DbUpdateException` a 409.

Cada uno de estos puntos, si se aborda, va en su propia spec.
