# SPEC 09 — Validación centralizada de sesión vigente

> **Estado:** aprobado
> **Depende de:** SPEC 05, SPEC 06
> **Fecha:** 2026-11-21
> **Objetivo:** Rechazar con 401 en cada petición autenticada los tokens cuyo usuario no existe, está inactivo o tiene en la BD un rol o un departamento distinto al del token. La validación se hace en `JwtBearerEvents.OnTokenValidated` mediante `IUserSessionUseCase`.

## Por qué existe esta spec

SPEC 06 decidió que el token sigue válido hasta expirar aunque el usuario se desactive o cambie de rol, y no consultar la BD en cada petición. En la práctica, un usuario desactivado o degradado conserva hasta 60 minutos permisos que ya no tiene. `[Authorize(Roles = ...)]` decide con el rol del token. Esta spec revierte esa decisión: un token solo sirve mientras describe al usuario tal como está en la BD.

## Alcance

**Dentro:**

- `ApprovalFlow.Application/Abstractions/UseCases/IUserSessionUseCase.cs`.
- `ApprovalFlow.Application/Auth/UserSessionUseCase.cs`, registrado como scoped en `AddApplication()`.
- Handler `OnTokenValidated` en `ApprovalFlow/Extensions/AuthenticationExtensions.cs` que lee `sub`, `role` y `department_id`, invoca el use case y llama `context.Fail(...)` si la sesión no es válida.
- `OnChallenge` con un `detail` distinto cuando el motivo es una sesión invalidada.
- Pruebas unitarias en `ApprovalFlow.Tests/Application/UserSessionUseCaseTests.cs`.
- Pruebas de integración en `ApprovalFlow.Tests/Api/Integration/SessionValidationTests.cs`.
- Actualizar `CLAUDE.md`, sección "Authentication and authorization".

**Fuera de alcance (para futuras specs):**

- Invalidar tokens al cambiar o resetear la contraseña (`SecurityStamp`/`TokenVersion`, claim nuevo y migración).
- Refresh tokens, logout y revocación explícita.
- Caché de la validación (`IMemoryCache` u otra).
- Invalidar el token por cambios de email o nombre.
- Refrescar los claims con los valores de la BD en vez de rechazar el token.
- Simplificar o quitar validaciones de los use cases existentes (`PurchaseRequestUseCase`, `UserUseCase`, etc.).
- Políticas de autorización con nombre o `FallbackPolicy`.

## Modelo de datos

Esta spec no cambia entidades ni la base de datos. No hay migración nueva. Usa `User` (SPEC 02) con `Id`, `Role`, `DepartmentId` e `IsActive`, y los claims definidos en SPEC 06.

### `IUserSessionUseCase` (`ApprovalFlow.Application/Abstractions/UseCases/`)

```csharp
public interface IUserSessionUseCase
{
    Task<bool> ValidateAsync(Guid userId, UserRole role, Guid? departmentId, CancellationToken ct = default);
}
```

### `UserSessionUseCase` (`ApprovalFlow.Application/Auth/`)

- Constructor primario `(IApplicationDbContext context)` guardado como `_context`.
- Hace una consulta por `Id` con `AsNoTracking()` y proyecta `Role`, `DepartmentId` e `IsActive`.
- Devuelve `true` solo si el usuario existe, `IsActive == true`, `Role == role` y `DepartmentId == departmentId`. Que ambos valores sean `null` cuenta como igual.
- No lanza excepciones por sesión inválida. Devuelve `false`.

### `OnTokenValidated` (`AuthenticationExtensions`)

- Se lee `sub` como `Guid` y `role` con `Enum.TryParse<UserRole>`. `department_id` se lee como `Guid?`: si el claim no está, vale `null`.
- Si `sub` o `role` faltan o no se pueden convertir, o si `department_id` está pero no es un `Guid` válido → sesión inválida.
- `IUserSessionUseCase` se resuelve desde `context.HttpContext.RequestServices` y recibe `context.HttpContext.RequestAborted`.
- Sesión inválida:
  - se marca `HttpContext.Items["approvalflow:session-invalid"] = true`;
  - se llama `context.Fail("La sesión ya no es válida.")`;
  - se registra un log `Information` con el `userId`, si se pudo leer.
- Rechazar el token en este evento ocurre antes de `[Authorize(Roles = ...)]`, así que aplica a todo endpoint protegido sin tocar los controllers.

### Respuesta 401

| Caso                                                           | `title`            | `detail`                                               |
| -------------------------------------------------------------- | ------------------ | ------------------------------------------------------ |
| Sin token, token mal formado, firma inválida o expirado        | `"No autenticado"` | `"Se requiere un token válido."` (sin cambios)         |
| Usuario inexistente, inactivo, o con rol/departamento distinto | `"No autenticado"` | `"La sesión ya no es válida. Inicia sesión de nuevo."` |

`OnChallenge` elige el `detail` según si existe la marca en `HttpContext.Items`. Usa el mismo `WriteProblemAsync` de hoy.

## Plan de implementación

1. Crear `Abstractions/UseCases/IUserSessionUseCase.cs` y `Auth/UserSessionUseCase.cs`, y registrarlos en `AddApplication()`. Comprobar con `dotnet build ApprovalFlow.slnx`.
2. Crear `ApprovalFlow.Tests/Application/UserSessionUseCaseTests.cs` con InMemory y una base de nombre único por prueba, igual que `DepartmentUseCaseTests`. Comprobar con `dotnet test --filter "FullyQualifiedName~UserSessionUseCaseTests"`.
3. En `AuthenticationExtensions`, agregar `OnTokenValidated` con la lectura de claims, la llamada al use case, la marca en `Items` y el log. Ajustar `OnChallenge` para elegir el `detail`. Comprobar que la suite existente sigue pasando con `dotnet test --filter "FullyQualifiedName~Api.Integration"`.
4. Crear `ApprovalFlow.Tests/Api/Integration/SessionValidationTests.cs` con los casos de los criterios de aceptación. Los cambios de estado se hacen por HTTP como Admin (`deactivate`, `role`) o directamente en `AppDbContext` dentro de un scope de `ApiFactory`. Comprobar con `dotnet test --filter "FullyQualifiedName~SessionValidationTests"`.
5. Actualizar `CLAUDE.md`:
   - reemplazar "The token stays valid until it expires even if the user is deactivated or changes role." por la nueva regla;
   - mencionar `IUserSessionUseCase` en `Abstractions/UseCases/` y la validación en `OnTokenValidated`.

## Criterios de aceptación

- [ ] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [ ] Existen `IUserSessionUseCase.cs` en `Abstractions/UseCases/` y `UserSessionUseCase.cs` en `Auth/`. `IUserSessionUseCase` se resuelve desde el contenedor de la API.
- [ ] `ApprovalFlow.Application.csproj` sigue sin referenciar ASP.NET Core, Npgsql ni Infrastructure.
- [ ] `ValidateAsync` devuelve `true` para un usuario activo con el mismo rol y departamento, incluido el caso de Admin con `departmentId` `null`.
- [ ] `ValidateAsync` devuelve `false` si el usuario no existe, si está inactivo, si el rol difiere o si el departamento difiere (incluido `null` frente a un valor).
- [ ] Un token válido de un usuario activo y sin cambios sigue respondiendo igual que antes (200/403 según el rol) en `/api/users/me`, `/api/departments` y `/api/purchase-requests`.
- [ ] Tras `POST /api/users/{id}/deactivate` hecho por un Admin, el token previo del usuario desactivado → 401 `ProblemDetails` con `detail` `"La sesión ya no es válida. Inicia sesión de nuevo."`.
- [ ] Tras `PUT /api/users/{id}/role` hecho por un Admin, el token previo de ese usuario → 401 con el mismo `detail`.
- [ ] Si el departamento del usuario cambia en la BD, su token previo → 401 con el mismo `detail`.
- [ ] Un token firmado correctamente para un usuario que no existe en la BD → 401 con el mismo `detail`.
- [ ] Un Requester degradado desde Approver, con su token viejo, recibe 401 en `POST /api/purchase-requests/{id}/approve` y no 403 ni 200.
- [ ] Después de cambiarle el rol, un nuevo `POST /api/auth/login` del usuario devuelve un token que responde 200 en `/api/users/me`.
- [ ] Sin token, con un token mal formado o con un token expirado se sigue devolviendo 401 con `detail` `"Se requiere un token válido."`.
- [ ] El Admin que desactiva o cambia el rol a otro usuario puede seguir usando su propio token.
- [ ] `dotnet test` pasa todas las pruebas, tanto las anteriores como las nuevas.

## Decisiones

- **Sí:** la validación va en `JwtBearerEvents.OnTokenValidated`. Corre una vez por petición, antes de la autorización por rol, y cubre todo endpoint protegido sin tocar los controllers. **No:** `IAuthorizationHandler` como política, porque `[Authorize(Roles)]` seguiría usando el rol del token. **No:** un middleware propio, porque habría que replicar la detección de `[AllowAnonymous]`.
- **Sí:** si el rol o el departamento difieren, se rechaza con 401 y el cliente vuelve a iniciar sesión. El token siempre refleja la BD o no sirve. **No:** reescribir los claims con los valores de la BD.
- **Sí:** 401 con `title` `"No autenticado"` y un `detail` propio. El cliente sabe que debe reautenticarse y lo distingue de un token expirado. **No:** 403, porque el cliente no entendería que debe volver a iniciar sesión. **No:** un 401 genérico sin distinción.
- **Sí:** sin caché. Es una consulta por PK con proyección por petición, y los cambios se aplican al instante. **No:** `IMemoryCache`, porque reabre una ventana de permisos desactualizados y exige invalidación.
- **Sí:** `IUserSessionUseCase` en `Abstractions/UseCases/` con la implementación en `Application/Auth/`, siguiendo el patrón de use cases. **No:** un método en `AuthUseCase`, que mezclaría el login con la validación por petición. **No:** una abstracción tipo servicio.
- **Sí:** se comparan existencia, `IsActive`, `Role` y `DepartmentId`, que son los datos que deciden la autorización y la visibilidad. **No:** email ni nombre, porque obligarían a volver a iniciar sesión tras editar el perfil.
- **Sí:** el use case devuelve `bool` y no lanza excepciones. El evento de JwtBearer no pasa por `GlobalExceptionHandler`.
- **Sí:** la marca en `HttpContext.Items` sirve para elegir el `detail` en `OnChallenge`. **No:** inspeccionar el tipo de `AuthenticateFailure`, porque los fallos de token normales también lo rellenan.
- **Sí:** se mantienen las validaciones de los use cases (rol, departamento e `IsActive` leídos de la BD en `PurchaseRequestUseCase`). Es defensa en profundidad y el dominio necesita la entidad `User`.
- **No:** invalidar tokens al cambiar la contraseña. Requiere `SecurityStamp` y una migración, y va en otra spec.
- **Esta spec revierte** la decisión de SPEC 06 "No: consultar la BD por petición".

## Riesgos

| Riesgo                                                                                        | Mitigación                                                                                                                              |
| --------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| Una consulta extra a la BD por cada petición autenticada                                      | Consulta por PK con `AsNoTracking` y proyección. Es aceptable con el volumen actual. La caché va en otra spec si hace falta.            |
| Una petición a un endpoint `[AllowAnonymous]` (login) con un Bearer viejo dispara la consulta | `Fail` deja la petición sin autenticar y no hay challenge. El login funciona igual. Se acepta la consulta extra.                        |
| Una petición en curso termina con permisos que se revocaron mientras se procesaba             | Aceptado. La validación ocurre al inicio de cada petición.                                                                              |
| Un `department_id` en otro formato (p. ej. `Guid` con llaves) se rechaza por error            | `JwtTokenGenerator` emite `Guid.ToString()` y se parsea con `Guid.TryParse`. Las pruebas de integración cubren el caso con departamento. |
| Con la BD caída, todas las peticiones autenticadas fallan                                     | Ya pasaba en los endpoints que consultan la BD. La excepción del evento la registra el pipeline y responde 500.                         |

## Qué **no** está en esta spec

- Invalidación por cambio o reset de contraseña (`SecurityStamp`).
- Refresh tokens, logout y revocación.
- Caché de la validación.
- Invalidación por email o nombre.
- Refresco de claims.
- Cambios en los use cases existentes.
- Políticas con nombre.

Cada uno de estos puntos, si se aborda, va en su propia spec.
