# SPEC 08 — Casos de uso y controller de solicitudes de compra

> **Estado:** implementado
> **Depende de:** SPEC 02, SPEC 03, SPEC 04, SPEC 05, SPEC 06, SPEC 07
> **Fecha:** 2026-11-20
> **Objetivo:** Crear `PurchaseRequestUseCase` (crear, consultar, listar, editar, cancelar, iniciar revisión, aprobar y rechazar) con sus DTOs en `ApprovalFlow.Application/PurchaseRequests` y exponerlo en `PurchaseRequestsController` (`/api/purchase-requests`), con visibilidad y acciones según el rol del usuario.

## Alcance

**Dentro:**

- `ApprovalFlow.Application/Abstractions/UseCases/IPurchaseRequestUseCase.cs`.
- `ApprovalFlow.Application/PurchaseRequests/PurchaseRequestUseCase.cs`.
- `ApprovalFlow.Application/PurchaseRequests/Dtos/`, con un DTO por archivo: `CreatePurchaseRequestDto`, `UpdatePurchaseRequestDto`, `ApprovePurchaseRequestDto`, `RejectPurchaseRequestDto` y `PurchaseRequestDto`.
- Generación de `RequestNumber` con formato secuencial anual `PR-{yyyy}-{n:D6}`.
- Registrar `IPurchaseRequestUseCase` en `AddApplication()`.
- `ApprovalFlow/Controllers/PurchaseRequestsController.cs`.
- Parámetro opcional `Guid? departmentId` en `ApiFactory.SeedUserAsync` para sembrar Requester y Approver en el mismo departamento.
- Agregar las peticiones de `/api/purchase-requests` a `ApprovalFlow/ApprovalFlow.http`.
- Pruebas unitarias `ApprovalFlow.Tests/Application/PurchaseRequestUseCaseTests.cs` con EF Core InMemory.
- Pruebas de integración `ApprovalFlow.Tests/Api/Integration/PurchaseRequestsControllerTests.cs` con `ApiFactory`.
- Actualizar `CLAUDE.md`: estado del proyecto, carpeta `PurchaseRequests/`, controller y reglas de visibilidad.

**Fuera de alcance (para futuras specs):**

- Flujo de dos niveles con Finance. Finance solo lee.
- Bloquear la desactivación de un departamento con solicitudes `Pending` o `InReview`.
- Paginación, ordenamiento configurable y otros filtros además de `status`.
- Nombres de solicitante o departamento en el DTO (joins).
- Secuencia de Postgres para `RequestNumber` y mapear `DbUpdateException` a 409.
- Borrado físico de solicitudes.
- Ítems o líneas de detalle, adjuntos, moneda y notificaciones.
- Cambios en la entidad `PurchaseRequest`, su configuración EF o la base de datos.

## Modelo de datos

Esta spec no cambia entidades ni la base de datos. No hay migración nueva. Usa `PurchaseRequest` (SPEC 02) con `Create`, `Update`, `Cancel`, `StartReview`, `Approve`, `Reject` y las constantes `TitleMaxLength` (150), `JustificationMaxLength` (2000) y `CommentsMaxLength` (1000).

### DTOs (`ApprovalFlow.Application/PurchaseRequests/Dtos/`)

Namespace: `ApprovalFlow.Application.PurchaseRequests.Dtos`. Son `record` con propiedades `init`.

| DTO                          | Propiedades y anotaciones                                                                                                                                                         |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CreatePurchaseRequestDto`   | `Title` `[Required]` `[MaxLength(PurchaseRequest.TitleMaxLength)]` · `Justification` `[Required]` `[MaxLength(PurchaseRequest.JustificationMaxLength)]` · `TotalAmount` (`decimal`) |
| `UpdatePurchaseRequestDto`   | `Title`, `Justification` y `TotalAmount`, con las mismas anotaciones que en `CreatePurchaseRequestDto`                                                                            |
| `ApprovePurchaseRequestDto`  | `Comments` (`string?`) `[MaxLength(PurchaseRequest.CommentsMaxLength)]`                                                                                                           |
| `RejectPurchaseRequestDto`   | `Comments` `[Required]` `[MaxLength(PurchaseRequest.CommentsMaxLength)]`                                                                                                          |
| `PurchaseRequestDto` (resp.) | Todos los campos de la entidad: `Id`, `RequestNumber`, `Title`, `Justification`, `Comments`, `TotalAmount`, `Status`, `RequesterId`, `DepartmentId`, `CreatedAt`, `UpdatedAt`, `ReviewedAt`, `ReviewedById`, `ApprovedAt`, `ApprovedById`, `RejectedAt`, `RejectedById`, `CancelledAt`, `CancelledById`. Método estático `FromEntity(PurchaseRequest request)`. |

- `TotalAmount` no lleva anotación: el dominio valida que sea > 0 y con máximo 2 decimales (`ArgumentException` → 400).
- `Status` se serializa como número (1 = Pending, 2 = InReview, 3 = Approved, 4 = Rejected, 5 = Cancelled), igual que `UserRole`.

### `IPurchaseRequestUseCase` / `PurchaseRequestUseCase`

- La interfaz vive en `ApprovalFlow.Application/Abstractions/UseCases/` (namespace `ApprovalFlow.Application.Abstractions.UseCases`).
- La implementación vive en `ApprovalFlow.Application/PurchaseRequests/`, con constructor primario `(IApplicationDbContext context)` guardado como `_context`.
- Se registra como scoped en `AddApplication()`.
- Todos los métodos reciben `Guid userId` (el usuario que actúa, leído por el controller con `User.GetUserId()`) y `CancellationToken ct = default`.

| Método                                                                                                 | Comportamiento                                                                                                 |
| ------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------- |
| `Task<PurchaseRequestDto> CreateAsync(Guid userId, CreatePurchaseRequestDto dto, ...)`                 | Carga al usuario, genera `RequestNumber`, llama `PurchaseRequest.Create`, la agrega y guarda.                  |
| `Task<PurchaseRequestDto> GetByIdAsync(Guid id, Guid userId, ...)`                                     | Devuelve la solicitud si es visible para el usuario, con `AsNoTracking()`. Si no → `NotFoundException`.        |
| `Task<IReadOnlyList<PurchaseRequestDto>> GetAllAsync(Guid userId, PurchaseRequestStatus? status, ...)` | Devuelve las visibles, filtradas por `status` si llega, ordenadas por `CreatedAt` descendente. Sin paginación. |
| `Task<PurchaseRequestDto> UpdateAsync(Guid id, Guid userId, UpdatePurchaseRequestDto dto, ...)`        | Carga visible y llama `Update(user, ...)`.                                                                     |
| `Task<PurchaseRequestDto> CancelAsync(Guid id, Guid userId, ...)`                                      | Carga visible y llama `Cancel(user)`.                                                                          |
| `Task<PurchaseRequestDto> StartReviewAsync(Guid id, Guid userId, ...)`                                 | Carga visible y llama `StartReview(user)`.                                                                     |
| `Task<PurchaseRequestDto> ApproveAsync(Guid id, Guid userId, ApprovePurchaseRequestDto dto, ...)`      | Carga visible y llama `Approve(user, dto.Comments)`.                                                           |
| `Task<PurchaseRequestDto> RejectAsync(Guid id, Guid userId, RejectPurchaseRequestDto dto, ...)`        | Carga visible y llama `Reject(user, dto.Comments)`.                                                            |

Reglas comunes:

- Usuario que actúa: se carga de la BD por `userId`. Si no existe → `NotFoundException("El usuario '{id}' no existe.")`. Rol, departamento y estado activo salen de la BD, no de los claims del token.
- Visibilidad (helper privado sobre `IQueryable<PurchaseRequest>`):
  - `Requester` → `RequesterId == user.Id`.
  - `Approver` → `DepartmentId == user.DepartmentId`. Sin departamento → ninguna.
  - `Finance` y `Admin` → todas.
- Solicitud inexistente o no visible → `NotFoundException("La solicitud '{id}' no existe.")`. Mismo mensaje en ambos casos.
- `RequestNumber`: prefijo `PR-{año UTC actual}-`. Se cuenta con `CountAsync` las solicitudes cuyo `RequestNumber` empieza por ese prefijo, se suma 1 y se formatea con 6 dígitos (`PR-2026-000001`).
- Estado incorrecto, dueño distinto, aprobador de otro departamento, usuario inactivo o autoaprobación: los rechaza el dominio (`InvalidOperationException` → 409). El use case no repite esas validaciones.
- Título, justificación, monto o comentario inválidos: los rechaza el dominio (`ArgumentException` → 400).
- El use case no captura excepciones. Las mapea `GlobalExceptionHandler`.

### `PurchaseRequestsController` (`ApprovalFlow/Controllers/`)

`[ApiController]`, `[Route("api/purchase-requests")]` y `[Authorize]` a nivel de clase. Inyecta `IPurchaseRequestUseCase` como `_purchaseRequestUseCase`. Lee `User.GetUserId()` y lo pasa al use case. No captura excepciones.

| Verbo y ruta                                         | Autorización                           | Respuesta                                                     |
| ---------------------------------------------------- | -------------------------------------- | ------------------------------------------------------------- |
| `GET /api/purchase-requests?status={n}`              | cualquier autenticado                  | 200 `IReadOnlyList<PurchaseRequestDto>`                       |
| `GET /api/purchase-requests/{id:guid}`               | cualquier autenticado                  | 200 `PurchaseRequestDto`                                      |
| `POST /api/purchase-requests`                        | `[Authorize(Roles = Roles.Requester)]` | 201 `CreatedAtAction(nameof(GetById))` + `PurchaseRequestDto` |
| `PUT /api/purchase-requests/{id:guid}`               | `[Authorize(Roles = Roles.Requester)]` | 200 `PurchaseRequestDto`                                      |
| `POST /api/purchase-requests/{id:guid}/cancel`       | `[Authorize(Roles = Roles.Requester)]` | 200 `PurchaseRequestDto`                                      |
| `POST /api/purchase-requests/{id:guid}/start-review` | `[Authorize(Roles = Roles.Approver)]`  | 200 `PurchaseRequestDto`                                      |
| `POST /api/purchase-requests/{id:guid}/approve`      | `[Authorize(Roles = Roles.Approver)]`  | 200 `PurchaseRequestDto`                                      |
| `POST /api/purchase-requests/{id:guid}/reject`       | `[Authorize(Roles = Roles.Approver)]`  | 200 `PurchaseRequestDto`                                      |

## Plan de implementación

1. Crear los cinco DTOs en `Application/PurchaseRequests/Dtos/` y `PurchaseRequestDto.FromEntity`. Comprobar con `dotnet build ApprovalFlow.slnx`.
2. Crear `Abstractions/UseCases/IPurchaseRequestUseCase.cs` y `PurchaseRequests/PurchaseRequestUseCase.cs` con los helpers privados (usuario que actúa, filtro de visibilidad, carga visible y generación de `RequestNumber`), `CreateAsync`, `GetByIdAsync` y `GetAllAsync`. Registrar el use case en `AddApplication()`. Comprobar con `dotnet build`.
3. Completar `PurchaseRequestUseCase` con `UpdateAsync` y `CancelAsync`. Comprobar con `dotnet build`.
4. Completar `PurchaseRequestUseCase` con `StartReviewAsync`, `ApproveAsync` y `RejectAsync`. Comprobar con `dotnet build`.
5. Crear `ApprovalFlow.Tests/Application/PurchaseRequestUseCaseTests.cs` (creación, numeración, visibilidad y listado). Cada prueba usa un `AppDbContext` InMemory con una base de nombre único, como `DepartmentUseCaseTests`. Comprobar con `dotnet test --filter "FullyQualifiedName~PurchaseRequestUseCaseTests"`.
6. Ampliar `PurchaseRequestUseCaseTests` con edición, cancelación, revisión, aprobación y rechazo. Comprobar con el mismo filtro.
7. Crear `ApprovalFlow/Controllers/PurchaseRequestsController.cs`. Comprobar con `dotnet run` y probar en `/swagger`.
8. Agregar `Guid? departmentId = null` a `ApiFactory.SeedUserAsync`: si llega, se usa ese departamento en vez de crear uno nuevo. Comprobar que las pruebas de integración existentes siguen pasando.
9. Crear `ApprovalFlow.Tests/Api/Integration/PurchaseRequestsControllerTests.cs`. Requester y Approver se siembran en el mismo departamento con el parámetro del paso 8; los emails son únicos por prueba. Comprobar con `dotnet test --filter "FullyQualifiedName~Api.Integration"`.
10. Agregar las peticiones de solicitudes a `ApprovalFlow/ApprovalFlow.http` y actualizar `CLAUDE.md`.

## Criterios de aceptación

- [x] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [x] Existen `IPurchaseRequestUseCase.cs` en `Abstractions/UseCases/`, `PurchaseRequestUseCase.cs` en `PurchaseRequests/`, los cinco DTOs en `PurchaseRequests/Dtos/` y `PurchaseRequestsController.cs`.
- [x] `IPurchaseRequestUseCase` se resuelve desde el contenedor de la API.
- [x] Los DTOs de entrada tienen las DataAnnotations de la tabla.
- [x] `CreateAsync` de un Requester activo con departamento guarda la solicitud en `Pending`, con su `RequesterId` y `DepartmentId`.
- [x] La primera solicitud del año recibe `PR-{yyyy}-000001` y la segunda `PR-{yyyy}-000002`.
- [x] `CreateAsync` con `TotalAmount` 0 o con 3 decimales lanza `ArgumentException`.
- [x] `CreateAsync` de un Requester sin departamento o inactivo lanza `InvalidOperationException`.
- [x] `CreateAsync` con un `userId` inexistente lanza `NotFoundException`.
- [x] `GetAllAsync` como Requester devuelve solo sus solicitudes.
- [x] `GetAllAsync` como Approver devuelve solo las de su departamento.
- [x] `GetAllAsync` como Finance y como Admin devuelve todas.
- [x] `GetAllAsync` con `status` devuelve solo las de ese estado, ordenadas por `CreatedAt` descendente.
- [x] `GetByIdAsync` de una solicitud ajena (otro Requester u otro departamento) o inexistente lanza `NotFoundException`.
- [x] `UpdateAsync` del dueño en `Pending` cambia título, justificación y monto y fija `UpdatedAt`.
- [x] `UpdateAsync` y `CancelAsync` en estado distinto de `Pending` lanzan `InvalidOperationException`.
- [x] `CancelAsync` del dueño en `Pending` deja `Cancelled` con `CancelledById`.
- [x] `StartReviewAsync` de un Approver del departamento deja `InReview` con `ReviewedById`.
- [x] `ApproveAsync` en `InReview` deja `Approved` con `ApprovedById`. Sin comentario, `Comments` queda `null`.
- [x] `RejectAsync` en `InReview` deja `Rejected` con `RejectedById` y el comentario recortado.
- [x] `ApproveAsync` y `RejectAsync` en `Pending` lanzan `InvalidOperationException`.
- [x] `GET /api/purchase-requests` sin token → 401 `ProblemDetails`.
- [x] `POST /api/purchase-requests` como Requester → 201 con cabecera `Location` y el `PurchaseRequestDto`.
- [x] `POST /api/purchase-requests` como Approver, Finance o Admin → 403 `ProblemDetails`.
- [x] `POST /api/purchase-requests` con `Title` vacío → 400 (DataAnnotations). Con `TotalAmount` 0 → 400 (dominio).
- [x] `start-review`, `approve` y `reject` como Requester → 403 `ProblemDetails`.
- [x] `GET /api/purchase-requests/{id}` de una solicitud de otro Requester → 404 `ProblemDetails`.
- [x] Flujo completo vía HTTP: Requester crea → Approver del mismo departamento `start-review` → `approve` → 200 con `Status` 3.
- [x] `reject` sin `Comments` → 400. `approve` sobre una solicitud `Pending` → 409 `ProblemDetails`.
- [x] `start-review` por un Approver de otro departamento → 404.
- [x] `PUT` y `cancel` del dueño sobre una solicitud `InReview` → 409.
- [x] `GET /api/purchase-requests?status=1` como Finance devuelve solo solicitudes `Pending`.
- [x] `dotnet test` pasa todas las pruebas, tanto las anteriores como las nuevas.

## Decisiones

- **Sí:** todo el flujo (Requester y Approver) en una spec. El dominio ya tiene las transiciones; el use case solo orquesta. **No:** separar en dos specs.
- **Sí:** `RequestNumber` secuencial anual `PR-{yyyy}-{n:D6}` contando las del año. Es legible y no necesita migración. **No:** fecha + aleatorio (no es secuencial). **No:** secuencia de Postgres (requiere migración y no se comporta igual en InMemory).
- **Sí:** visibilidad por rol: Requester las suyas, Approver las de su departamento, Finance y Admin todas. **No:** Finance solo aprobadas. **No:** todos ven todo.
- **Sí:** solicitud no visible → 404 con el mismo mensaje que una inexistente. No revela que el id existe. **No:** 403.
- **Sí:** las acciones de escritura cargan la solicitud con el mismo filtro de visibilidad. Así un Approver de otro departamento recibe 404 antes de llegar al dominio.
- **Sí:** rol en el controller con `[Authorize(Roles = ...)]` (rol incorrecto → 403) y reglas finas en el dominio (→ 409). **No:** solo dominio, porque un rol incorrecto daría 409. **No:** `ForbiddenException` en el use case, porque duplica lo que ya hace `[Authorize]`.
- **Sí:** rol, departamento y estado activo del usuario se leen de la BD. El token puede estar desactualizado (SPEC 06) y el dominio necesita la entidad `User`.
- **Sí:** filtro opcional `?status=` y orden por `CreatedAt` descendente. **No:** paginación ni más filtros por ahora.
- **Sí:** `PurchaseRequestDto` con todos los campos de la entidad y `FromEntity`, como `DepartmentDto`. **No:** nombres de solicitante o departamento, porque requieren joins que nadie pidió.
- **Sí:** las acciones (`cancel`, `start-review`, `approve`, `reject`) son `POST /{id}/accion` y devuelven 200 con el DTO actualizado. El cliente ve el nuevo estado sin otra petición. **No:** 204 como en activate/deactivate de departamentos, ni `PATCH`.
- **Sí:** DTOs separados para aprobar (comentario opcional) y rechazar (comentario obligatorio), siguiendo las reglas del dominio. **No:** un único DTO de comentario.
- **Sí:** `Status` como número en JSON, igual que `UserRole`. **No:** `JsonStringEnumConverter` global en esta spec.
- **Sí:** parámetro opcional `departmentId` en `ApiFactory.SeedUserAsync`. Hoy cada usuario sembrado crea su propio departamento y el flujo de aprobación necesita uno compartido.
- **No:** bloquear la desactivación de departamentos con solicitudes pendientes. Queda para otra spec.
- **No:** Finance en el flujo de aprobación. Solo lectura, según SPEC 02.

## Riesgos

| Riesgo                                                                          | Mitigación                                                                                                                   |
| ------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Dos solicitudes creadas a la vez obtienen el mismo `RequestNumber`              | El índice único lo impide y responde 500. Es aceptable con el volumen actual. La secuencia o el mapeo a 409 va en otra spec. |
| Carrera entre leer el estado y guardar la transición (dos aprobadores a la vez) | El último en guardar gana. Concurrencia optimista (token de versión) va en otra spec con migración.                          |
| El conteo por prefijo asume que nunca se borran solicitudes                     | No hay borrado físico. Si se agrega, se cambia el cálculo a máximo sufijo.                                                   |
| El año del prefijo usa UTC y puede diferir del año local el 31/12 y el 1/1      | Aceptado. Es consistente con `CreatedAt`, que también es UTC.                                                                |
| `StartsWith` se traduce distinto en InMemory (.NET) y Postgres (`LIKE`)         | El prefijo no tiene comodines (`%`, `_`). Ambos dan el mismo resultado.                                                      |

## Qué **no** está en esta spec

- Flujo de aprobación con Finance.
- Bloqueo de desactivación de departamentos por solicitudes pendientes.
- Paginación, más filtros y nombres en el DTO.
- Secuencia de BD, mapeo de `DbUpdateException` a 409 y concurrencia optimista.
- Borrado físico, ítems, adjuntos, moneda y notificaciones.
- Cualquier cambio en la entidad o migración.

Cada uno de estos puntos, si se aborda, va en su propia spec.
