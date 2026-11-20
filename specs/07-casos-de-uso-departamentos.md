# SPEC 07 — Casos de uso y controller de departamentos

> **Estado:** aprobado
> **Depende de:** SPEC 02, SPEC 03, SPEC 04, SPEC 05, SPEC 06
> **Fecha:** 2026-11-19
> **Objetivo:** Crear `DepartmentUseCase` (crear, consultar, listar, editar, activar y desactivar) con sus DTOs en `ApprovalFlow.Application/Departments` y exponerlo en `DepartmentsController` (`/api/departments`), con lectura para cualquier usuario autenticado y escritura solo para Admin.

## Alcance

**Dentro:**

- `ApprovalFlow.Application/Abstractions/UseCases/IDepartmentUseCase.cs`.
- `ApprovalFlow.Application/Departments/DepartmentUseCase.cs`.
- `ApprovalFlow.Application/Departments/Dtos/`, con un DTO por archivo: `CreateDepartmentDto`, `UpdateDepartmentDto` y `DepartmentDto`.
- Registrar `IDepartmentUseCase` en `AddApplication()`.
- `ApprovalFlow/Controllers/DepartmentsController.cs`.
- Agregar las peticiones de `/api/departments` a `ApprovalFlow/ApprovalFlow.http`.
- Pruebas unitarias `ApprovalFlow.Tests/Application/DepartmentUseCaseTests.cs` con EF Core InMemory.
- Pruebas de integración `ApprovalFlow.Tests/Api/Integration/DepartmentsControllerTests.cs` con `ApiFactory`.
- Actualizar `CLAUDE.md`: estado del proyecto, carpeta `Departments/` y controller.

**Fuera de alcance (para futuras specs):**

- Casos de uso y controller de `PurchaseRequest`.
- Bloquear la desactivación por solicitudes de compra pendientes.
- Borrado físico de departamentos.
- Paginación, filtros y ordenamientos configurables del listado.
- Conteos (usuarios o solicitudes por departamento) en `DepartmentDto`.
- Mapear la violación del índice único de `departments.name` (`DbUpdateException`) a 409.
- Cambios en la entidad `Department`, su configuración EF o la base de datos.

## Modelo de datos

Esta spec no cambia entidades ni la base de datos. No hay migración nueva. Usa `Department` (SPEC 02) con `Department.Create`, `Update`, `Activate`, `Deactivate` y las constantes `NameMaxLength` (100) y `DescriptionMaxLength` (500).

### DTOs (`ApprovalFlow.Application/Departments/Dtos/`)

Namespace: `ApprovalFlow.Application.Departments.Dtos`. Son `record` con propiedades `init`.

| DTO                         | Propiedades y anotaciones                                                                                                              |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `CreateDepartmentDto`       | `Name` `[Required]` `[MaxLength(Department.NameMaxLength)]` · `Description` (`string?`) `[MaxLength(Department.DescriptionMaxLength)]` |
| `UpdateDepartmentDto`       | `Name` y `Description`, con las mismas anotaciones que en `CreateDepartmentDto`                                                        |
| `DepartmentDto` (respuesta) | `Id`, `Name`, `Description`, `IsActive`, `CreatedAt`. Método estático `FromEntity(Department department)`.                             |

### `IDepartmentUseCase` / `DepartmentUseCase`

- La interfaz vive en `ApprovalFlow.Application/Abstractions/UseCases/` (namespace `ApprovalFlow.Application.Abstractions.UseCases`), igual que `IUserUseCase`.
- La implementación vive en `ApprovalFlow.Application/Departments/`, con constructor primario `(IApplicationDbContext context)` guardado como `_context`.
- Se registra como scoped en `AddApplication()`.

| Método                                                                                               | Comportamiento                                                                                                          |
| ---------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken ct = default)`           | Valida que el nombre sea único, llama `Department.Create`, lo agrega y guarda.                                          |
| `Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)`                          | Devuelve el departamento, activo o inactivo, con `AsNoTracking()`. Si no existe → `NotFoundException`.                  |
| `Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct = default)`                     | Devuelve todos (activos e inactivos) ordenados por `Name`, con `AsNoTracking()`. Sin paginación.                        |
| `Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentDto dto, CancellationToken ct = default)`  | Valida que el nombre sea único excluyendo al propio departamento y llama `Department.Update`.                           |
| `Task ActivateAsync(Guid id, CancellationToken ct = default)`                                        | Llama `Department.Activate`. Es idempotente.                                                                            |
| `Task DeactivateAsync(Guid id, CancellationToken ct = default)`                                      | Si hay algún usuario con `DepartmentId == id` e `IsActive` → `ConflictException`. Si no, llama `Department.Deactivate`. |

Reglas comunes:

- Unicidad de nombre: se normaliza con `Trim().ToLowerInvariant()`. Se comprueba con `AnyAsync(d => d.Name.ToLower() == normalized && ...)` antes de guardar.
- Nombre duplicado → `ConflictException("Ya existe un departamento con el nombre '{nombre recortado}'.")`.
- Departamento inexistente → `NotFoundException("El departamento '{id}' no existe.")`. Es el mismo mensaje que usa `UserUseCase`.
- Desactivar con usuarios activos → `ConflictException("El departamento '{id}' tiene usuarios activos asignados.")`.
- Nombre vacío o demasiado largo: lo rechaza el dominio (`ArgumentException` → 400). El use case no repite esa validación.
- El use case no captura excepciones. Las mapea `GlobalExceptionHandler`.

### `DepartmentsController` (`ApprovalFlow/Controllers/`)

`[ApiController]`, `[Route("api/departments")]` y `[Authorize]` a nivel de clase. Inyecta `IDepartmentUseCase` como `_departmentUseCase`. No captura excepciones.

| Verbo y ruta                                 | Autorización                       | Respuesta                                                |
| -------------------------------------------- | ---------------------------------- | -------------------------------------------------------- |
| `GET /api/departments`                       | cualquier autenticado              | 200 `IReadOnlyList<DepartmentDto>`                       |
| `GET /api/departments/{id:guid}`             | cualquier autenticado              | 200 `DepartmentDto`                                      |
| `POST /api/departments`                      | `[Authorize(Roles = Roles.Admin)]` | 201 `CreatedAtAction(nameof(GetById))` + `DepartmentDto` |
| `PUT /api/departments/{id:guid}`             | `[Authorize(Roles = Roles.Admin)]` | 200 `DepartmentDto`                                      |
| `POST /api/departments/{id:guid}/activate`   | `[Authorize(Roles = Roles.Admin)]` | 204                                                      |
| `POST /api/departments/{id:guid}/deactivate` | `[Authorize(Roles = Roles.Admin)]` | 204                                                      |

## Plan de implementación

1. Crear los tres DTOs en `Application/Departments/Dtos/` y `DepartmentDto.FromEntity`. Comprobar con `dotnet build ApprovalFlow.slnx`.
2. Crear `Abstractions/UseCases/IDepartmentUseCase.cs` y `Departments/DepartmentUseCase.cs` con `CreateAsync`, `GetByIdAsync`, `GetAllAsync` y el helper privado de unicidad de nombre. Registrar el use case en `AddApplication()`. Comprobar con `dotnet build`.
3. Completar `DepartmentUseCase` con `UpdateAsync`, `ActivateAsync` y `DeactivateAsync`, incluida la comprobación de usuarios activos. Comprobar con `dotnet build`.
4. Crear `ApprovalFlow.Tests/Application/DepartmentUseCaseTests.cs`. Cada prueba usa un `AppDbContext` InMemory con una base de nombre único, como `UserUseCaseTests`. Comprobar con `dotnet test --filter "FullyQualifiedName~DepartmentUseCaseTests"`.
5. Crear `ApprovalFlow/Controllers/DepartmentsController.cs`. Comprobar con `dotnet run` y probar en `/swagger`.
6. Crear `ApprovalFlow.Tests/Api/Integration/DepartmentsControllerTests.cs`. Se crean departamentos vía `POST` como Admin, con nombres únicos (`Guid`). Para el 409 de desactivación se usa el departamento que crea `SeedUserAsync(UserRole.Requester)`. Comprobar con `dotnet test --filter "FullyQualifiedName~Api.Integration"`.
7. Agregar las peticiones de departamentos a `ApprovalFlow/ApprovalFlow.http` y actualizar `CLAUDE.md`.

## Criterios de aceptación

- [ ] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [ ] Existen `IDepartmentUseCase.cs` en `Abstractions/UseCases/`, `DepartmentUseCase.cs` en `Departments/`, los tres DTOs en `Departments/Dtos/` y `DepartmentsController.cs`.
- [ ] `IDepartmentUseCase` se resuelve desde el contenedor de la API.
- [ ] `CreateDepartmentDto` y `UpdateDepartmentDto` tienen las DataAnnotations de la tabla.
- [ ] `CreateAsync` con datos válidos guarda el departamento con `IsActive = true` y el nombre recortado.
- [ ] `CreateAsync` con un nombre existente que difiere solo en mayúsculas o espacios lanza `ConflictException`.
- [ ] `CreateAsync` con nombre vacío lanza `ArgumentException` (del dominio).
- [ ] `GetByIdAsync` con un id inexistente lanza `NotFoundException`.
- [ ] `GetAllAsync` devuelve activos e inactivos ordenados por `Name`.
- [ ] `UpdateAsync` con el nombre de otro departamento lanza `ConflictException`. Con su propio nombre (aunque cambien las mayúsculas) no lanza.
- [ ] `DeactivateAsync` sin usuarios activos deja `IsActive = false` y `ActivateAsync` lo vuelve a `true`.
- [ ] `DeactivateAsync` con un usuario activo asignado lanza `ConflictException` y el departamento sigue activo.
- [ ] `DeactivateAsync` con solo usuarios inactivos asignados desactiva el departamento.
- [ ] `UpdateAsync`, `ActivateAsync` y `DeactivateAsync` con un id inexistente lanzan `NotFoundException`.
- [ ] `GET /api/departments` sin token → 401 `ProblemDetails`.
- [ ] `GET /api/departments` y `GET /api/departments/{id}` con token de `Requester` → 200.
- [ ] `POST`, `PUT`, `activate` y `deactivate` con token de `Requester` → 403 `ProblemDetails`.
- [ ] `POST /api/departments` como Admin → 201 con cabecera `Location` y el `DepartmentDto`.
- [ ] `POST /api/departments` con `Name` vacío → 400 (DataAnnotations).
- [ ] `POST /api/departments` con un nombre duplicado → 409 `ProblemDetails`.
- [ ] `GET /api/departments/{id}` inexistente → 404 `ProblemDetails`.
- [ ] `POST /api/departments/{id}/deactivate` con usuarios activos → 409. Sin usuarios activos → 204.
- [ ] `dotnet test` pasa todas las pruebas, tanto las anteriores como las nuevas.

## Decisiones

- **Sí:** use case y controller en una misma spec. Lo pidió el usuario: el patrón ya está asentado por SPEC 05 y 06. **No:** separarlos como se hizo con usuarios.
- **Sí:** unicidad de nombre sin distinguir mayúsculas, comprobada en el use case antes de guardar. **No:** depender solo del índice único, porque distingue mayúsculas y una violación daría 500. Además InMemory no lo respeta.
- **Sí:** comparar con `ToLower()` en la consulta (Npgsql lo traduce a `lower()`). **No:** cambiar el dominio para guardar el nombre en minúsculas, porque se pierde cómo lo escribió el usuario.
- **Sí:** bloquear la desactivación con 409 si hay usuarios activos asignados. Así no quedan usuarios activos colgando de un departamento inactivo. **No:** desactivar en cascada a los usuarios.
- **No:** tener en cuenta las `PurchaseRequest` al desactivar. Aún no tienen casos de uso; su regla va en su propia spec.
- **Sí:** el listado devuelve todos los departamentos ordenados por `Name`, con `IsActive` en el DTO. **No:** filtros ni paginación por ahora.
- **Sí:** lectura para cualquier usuario autenticado, porque todos los roles necesitan ver los departamentos. **Sí:** escritura solo para Admin con `[Authorize(Roles = Roles.Admin)]`. **No:** políticas con nombre.
- **Sí:** `CreateDepartmentDto` y `UpdateDepartmentDto` separados, igual que en Users. **No:** un único `SaveDepartmentDto`.
- **Sí:** `DepartmentDto` básico (`Id`, `Name`, `Description`, `IsActive`, `CreatedAt`). **No:** conteos de usuarios, porque son consultas extra que nadie pidió.
- **Sí:** activate/deactivate por `POST /{id}/activate|deactivate` con 204, como en `UsersController`. **No:** `PATCH` ni `DELETE`.
- **No:** borrado físico. Las FKs son `Restrict` (SPEC 03); se usa `Deactivate`.
- **Sí:** `IDepartmentUseCase` en `Abstractions/UseCases/`, según la convención actual de `CLAUDE.md`.

## Riesgos

| Riesgo                                                                          | Mitigación                                                                                              |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Carrera entre `AnyAsync` y `SaveChangesAsync` con dos nombres iguales a la vez  | El índice único lo impide para nombres idénticos y respondería 500. Su mapeo a 409 va en otra spec.     |
| Dos nombres que solo difieren en mayúsculas pueden pasar la BD en una carrera   | Es poco probable con un solo Admin. Un índice sobre `lower(name)` iría en otra spec con migración.      |
| Carrera entre la comprobación de usuarios activos y la desactivación            | Es aceptable: el dominio ya impide asignar usuarios nuevos a un departamento inactivo.                  |
| `ToLower()` se traduce distinto en InMemory (.NET) y Postgres (`lower()`)       | Ambos resuelven bien los nombres ASCII y con acentos comunes. Las pruebas cubren mayúsculas y espacios. |

## Qué **no** está en esta spec

- Casos de uso y controller de `PurchaseRequest`.
- Bloqueo por solicitudes de compra pendientes.
- Borrado físico.
- Paginación, filtros y conteos.
- Mapeo de `DbUpdateException` a 409.
- Índice único insensible a mayúsculas en BD y cualquier migración.

Cada uno de estos puntos, si se aborda, va en su propia spec.
