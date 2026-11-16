# SPEC 02 — Entidades y enums del dominio

> **Estado:** aprobado
> **Depende de:** —
> **Fecha:** 2026-11-15
> **Objetivo:** Crear en `ApprovalFlow.Domain` las entidades `Department`, `User` y `PurchaseRequest` y los enums `UserRole` y `PurchaseRequestStatus`, con sus validaciones y transiciones de estado protegidas dentro de la entidad y cubiertas por pruebas unitarias.

## Alcance

**Dentro:**

- Carpeta `ApprovalFlow.Domain/Enums/` con `UserRole.cs` y `PurchaseRequestStatus.cs`.
- Carpeta `ApprovalFlow.Domain/Entities/` con `Department.cs`, `User.cs` y `PurchaseRequest.cs`.
- Validaciones de cada entidad: campos obligatorios, longitudes, formato de email, monto.
- Métodos de comportamiento: alta, edición, activación/desactivación y transiciones de estado de la solicitud.
- Reglas de autorización de dominio dentro de las entidades: rol del actor, usuario activo, departamento activo, mismo departamento y que el solicitante no se apruebe a sí mismo.
- Borrar `ApprovalFlow.Domain/Class1.cs`.
- Referencia de proyecto `ApprovalFlow.Tests` → `ApprovalFlow.Domain` y pruebas xUnit de las reglas.

**Fuera de alcance (para futuras specs):**

- Excepciones de dominio propias y handler global de errores en la API.
- Líneas de detalle de la solicitud (`PurchaseRequestItem`).
- Configuración de EF Core (`IEntityTypeConfiguration`, `DbContext`, migraciones, índices únicos).
- Generación de `RequestNumber` (formato y secuencia).
- Unicidad de `Email`, `Department.Name` y `RequestNumber` (la revisan Application o la BD).
- Hash de contraseñas y autenticación (`LastLoginAt`, JWT).
- Casos de uso de Application, repositorios y controladores.
- Flujo de dos niveles con Finance y moneda/divisa.

## Modelo de datos

### Enums (`ApprovalFlow.Domain/Enums/`)

```csharp
public enum UserRole { Admin = 1, Requester = 2, Approver = 3, Finance = 4 }

public enum PurchaseRequestStatus { Pending = 1, InReview = 2, Approved = 3, Rejected = 4, Cancelled = 5 }
```

### Department (`ApprovalFlow.Domain/Entities/Department.cs`)

| Propiedad     | Tipo       | Regla                                       |
| ------------- | ---------- | ------------------------------------------- |
| `Id`          | `Guid`     | `Guid.NewGuid()` al crearse.                |
| `Name`        | `string`   | Obligatorio, se aplica trim, máx. 100.      |
| `Description` | `string?`  | Opcional, se aplica trim, máx. 500.         |
| `IsActive`    | `bool`     | `true` al crearse.                          |
| `CreatedAt`   | `DateTime` | `DateTime.UtcNow` al crearse.               |

Métodos: `static Create(name, description)`, `Update(name, description)`, `Activate()`, `Deactivate()`.

### User (`ApprovalFlow.Domain/Entities/User.cs`)

| Propiedad      | Tipo        | Regla                                                                                     |
| -------------- | ----------- | ----------------------------------------------------------------------------------------- |
| `Id`           | `Guid`      | `Guid.NewGuid()` al crearse.                                                              |
| `FullName`     | `string`    | Obligatorio, se aplica trim, máx. 150.                                                    |
| `Email`        | `string`    | Obligatorio, trim + minúsculas, máx. 256, formato válido según `System.Net.Mail.MailAddress`. |
| `PasswordHash` | `string`    | Obligatorio y no vacío. La entidad no calcula el hash.                                    |
| `Role`         | `UserRole`  | Debe ser un valor definido del enum.                                                      |
| `DepartmentId` | `Guid?`     | Obligatorio salvo para `Admin`. El departamento debe estar activo.                        |
| `IsActive`     | `bool`      | `true` al crearse.                                                                        |
| `CreatedAt`    | `DateTime`  | `DateTime.UtcNow` al crearse.                                                             |
| `UpdatedAt`    | `DateTime?` | `null` al crearse. Se actualiza en cada método que cambia datos.                          |

Métodos: `static Create(fullName, email, passwordHash, role, Department? department)`, `UpdateProfile(fullName, email)`, `ChangePasswordHash(passwordHash)`, `ChangeRole(role, Department? department)`, `AssignDepartment(Department department)`, `Activate()`, `Deactivate()`.

Se recibe `Department` (y no un `Guid`) para comprobar `IsActive`. `DepartmentId` toma `department.Id`.

### PurchaseRequest (`ApprovalFlow.Domain/Entities/PurchaseRequest.cs`)

| Propiedad       | Tipo                    | Regla                                                       |
| --------------- | ----------------------- | ----------------------------------------------------------- |
| `Id`            | `Guid`                  | `Guid.NewGuid()` al crearse.                                |
| `RequestNumber` | `string`                | Obligatorio, se aplica trim, máx. 30. Llega ya generado.    |
| `Title`         | `string`                | Obligatorio, se aplica trim, máx. 150.                      |
| `Justification` | `string`                | Obligatorio, se aplica trim, máx. 2000.                     |
| `Comments`      | `string?`               | Comentario del Approver, máx. 1000.                         |
| `TotalAmount`   | `decimal`               | `> 0` y máximo 2 decimales.                                 |
| `Status`        | `PurchaseRequestStatus` | `Pending` al crearse.                                       |
| `RequesterId`   | `Guid`                  | Id del User que crea la solicitud.                          |
| `DepartmentId`  | `Guid`                  | Se copia de `requester.DepartmentId`.                       |
| `CreatedAt`     | `DateTime`              | `DateTime.UtcNow` al crearse.                               |
| `UpdatedAt`     | `DateTime?`             | Se actualiza en `Update` y en cada transición.              |
| `ReviewedAt`    | `DateTime?`             | Lo fija `StartReview`.                                      |
| `ReviewedById`  | `Guid?`                 | Lo fija `StartReview`.                                      |
| `ApprovedAt`    | `DateTime?`             | Lo fija `Approve`.                                          |
| `ApprovedById`  | `Guid?`                 | Lo fija `Approve`.                                          |
| `RejectedAt`    | `DateTime?`             | Lo fija `Reject`.                                           |
| `RejectedById`  | `Guid?`                 | Lo fija `Reject`.                                           |
| `CancelledAt`   | `DateTime?`             | Lo fija `Cancel`.                                           |
| `CancelledById` | `Guid?`                 | Lo fija `Cancel`.                                           |

Nombres corregidos respecto al pedido original: `PurchaseRequestSatus` → `Status`, `Justifiction` → `Justification`, `CreateAt` → `CreatedAt`, `ReviewAt` → `ReviewedAt`.

Métodos y reglas:

| Método                                                  | Estado origen → destino | Actor permitido                                                         | Otras reglas                         |
| ------------------------------------------------------- | ----------------------- | ----------------------------------------------------------------------- | ------------------------------------ |
| `static Create(requestNumber, title, justification, totalAmount, User requester)` | — → `Pending` | `Requester` activo con `DepartmentId`                    | —                                    |
| `Update(User requester, title, justification, totalAmount)` | `Pending` → `Pending` | El dueño (`RequesterId`), activo                                      | —                                    |
| `StartReview(User approver)`                            | `Pending` → `InReview`  | `Approver` activo, del mismo departamento y distinto del solicitante    | —                                    |
| `Approve(User approver, string? comments)`              | `InReview` → `Approved` | `Approver` activo, del mismo departamento y distinto del solicitante    | `comments` es opcional.              |
| `Reject(User approver, string comments)`                | `InReview` → `Rejected` | `Approver` activo, del mismo departamento y distinto del solicitante    | `comments` es obligatorio y no vacío. |
| `Cancel(User requester)`                                | `Pending` → `Cancelled` | El dueño (`RequesterId`), activo                                        | —                                    |

`Approved`, `Rejected` y `Cancelled` son estados finales: ningún método los cambia.

### Convenciones comunes

- Setters `private`. El estado solo cambia mediante métodos.
- Constructor `private` sin parámetros para EF Core futuro. Las instancias válidas se crean con `static Create(...)`.
- Fechas en UTC con `DateTime.UtcNow`.
- Errores: `ArgumentException` (o `ArgumentNullException` / `ArgumentOutOfRangeException`) para datos inválidos. `InvalidOperationException` para una transición o un actor no permitido.
- Sin propiedades de navegación: las relaciones se expresan solo con `Guid`.
- `ApprovalFlow.Domain` sigue sin paquetes NuGet.

## Plan de implementación

1. Crear `Enums/UserRole.cs` y `Enums/PurchaseRequestStatus.cs`. Borrar `Class1.cs`. Agregar a `ApprovalFlow.Tests.csproj` la referencia de proyecto a `ApprovalFlow.Domain`. Comprobar con `dotnet build ApprovalFlow.slnx`.
2. Crear `Entities/Department.cs` con `Create`, `Update`, `Activate` y `Deactivate`. Agregar `ApprovalFlow.Tests/Domain/DepartmentTests.cs` con pruebas de validación y de activación/desactivación. Comprobar con `dotnet test`.
3. Crear `Entities/User.cs` con todas sus validaciones y métodos. Agregar `ApprovalFlow.Tests/Domain/UserTests.cs` con pruebas de email, departamento opcional solo para Admin, departamento inactivo y `UpdatedAt`. Comprobar con `dotnet test`.
4. Crear `Entities/PurchaseRequest.cs` con propiedades, `Create` y `Update`. Agregar `ApprovalFlow.Tests/Domain/PurchaseRequestTests.cs` con pruebas de creación y edición. Comprobar con `dotnet test`.
5. Agregar a `PurchaseRequest` los métodos `StartReview`, `Approve`, `Reject` y `Cancel` con sus reglas de actor. Ampliar `PurchaseRequestTests.cs` con un caso válido y los casos inválidos de cada transición. Comprobar con `dotnet test`.

## Criterios de aceptación

- [ ] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [ ] `ApprovalFlow.Domain.csproj` no tiene ningún `PackageReference` ni `ProjectReference`.
- [ ] Existen `ApprovalFlow.Domain/Enums/UserRole.cs`, `Enums/PurchaseRequestStatus.cs`, `Entities/Department.cs`, `Entities/User.cs` y `Entities/PurchaseRequest.cs`. `Class1.cs` ya no existe.
- [ ] Ninguna propiedad de las tres entidades tiene setter público.
- [ ] `Department.Create` con nombre vacío o de más de 100 caracteres lanza `ArgumentException`.
- [ ] `User.Create` normaliza `"  Ana@Mail.COM "` a `"ana@mail.com"` y lanza `ArgumentException` con `"no-es-email"`.
- [ ] `User.Create` con rol `Requester` y sin departamento lanza excepción. Con rol `Admin` y sin departamento crea el usuario con `DepartmentId == null`.
- [ ] `User.Create` o `AssignDepartment` con un `Department` inactivo lanza `InvalidOperationException`.
- [ ] `PurchaseRequest.Create` con un usuario que no es `Requester`, que está inactivo o cuyo departamento es nulo lanza `InvalidOperationException`.
- [ ] `PurchaseRequest.Create` con `TotalAmount` igual a `0`, negativo o con 3 decimales (`10.123m`) lanza `ArgumentException`.
- [ ] Una solicitud recién creada tiene `Status == Pending`, `DepartmentId == requester.DepartmentId` y `CreatedAt.Kind == DateTimeKind.Utc`.
- [ ] `Update` y `Cancel` lanzan `InvalidOperationException` si el actor no es el dueño o si el estado no es `Pending`.
- [ ] `StartReview`, `Approve` y `Reject` lanzan `InvalidOperationException` si el actor no es `Approver`, si está inactivo, si es de otro departamento o si es el propio solicitante.
- [ ] `Approve` y `Reject` lanzan `InvalidOperationException` si el estado no es `InReview`.
- [ ] `Reject` con `comments` vacío lanza `ArgumentException`.
- [ ] Cada transición válida fija su par `XxxAt` / `XxxById` y `UpdatedAt`.
- [ ] Desde `Approved`, `Rejected` o `Cancelled`, cualquier método de transición o `Update` lanza `InvalidOperationException`.
- [ ] `dotnet test` pasa todas las pruebas.

## Decisiones

- **Sí:** flujo de un solo nivel con el Approver (Pending → InReview → Approved/Rejected). Finance no participa en las transiciones de esta spec.
- **No:** flujo de dos niveles con Finance. Si se necesita, va en su propia spec.
- **Sí:** las reglas de rol, usuario activo, mismo departamento y no aprobar la propia solicitud viven en la entidad, que recibe el `User` que actúa. Así se sigue la regla de CLAUDE.md: el comportamiento está en la entidad.
- **No:** dejar esas reglas en Application. Se dispersarían y se podrían saltar.
- **Sí:** cualquier Approver del departamento puede aprobar o rechazar, aunque no sea el que inició la revisión.
- **Sí:** `Id` de tipo `Guid` generado por la entidad. La identidad existe antes de guardar y los ids no son adivinables.
- **No:** `int` autoincremental.
- **Sí:** `TotalAmount` se escribe directamente. **No:** `PurchaseRequestItem` en esta spec.
- **Sí:** `RequestNumber` llega ya generado. El formato y la secuencia se definen con la persistencia.
- **Sí:** `Comments` es del Approver: opcional al aprobar y obligatorio al rechazar. **No:** campos separados `RejectionReason` / `CancellationReason`.
- **Sí:** guardar los ids de actores (`ReviewedById`, `ApprovedById`, `RejectedById`, `CancelledById`) para auditoría.
- **Sí:** el solicitante solo cancela en `Pending`. **No:** cancelar en `InReview` ni que lo haga el Admin.
- **Sí:** la solicitud toma el departamento del solicitante y no se puede elegir.
- **Sí:** `User.DepartmentId` es opcional solo para `Admin`.
- **Sí:** solo el rol `Requester` crea solicitudes.
- **Sí:** `DateTime.UtcNow` dentro de la entidad. **No:** pasar la hora como parámetro ni usar `TimeProvider`. Se aceptó que las fechas exactas no se pueden fijar en las pruebas: se comprueba `Kind == Utc` y que no sean nulas.
- **Sí:** excepciones .NET por ahora. Las excepciones propias y el handler global van en otra spec y las reemplazarán.
- **Sí:** valores de enums en inglés con números explícitos desde `1`, para que `default(enum)` (`0`) no sea un valor válido.
- **Sí:** se renombran los campos con errores de escritura del pedido original (`Status`, `Justification`, `CreatedAt`, `ReviewedAt`).
- **Sí:** sin propiedades de navegación. Se decidirán en la spec de EF Core.
- **No:** unicidad de email, nombre o folio en la entidad. La entidad no ve otras instancias.

## Riesgos

| Riesgo                                                                        | Mitigación                                                                                              |
| ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `DateTime.UtcNow` interno impide probar fechas exactas                        | Las pruebas comprueban `Kind == Utc` y que la fecha no sea nula. Si hace falta, se cambia a `TimeProvider` en otra spec. |
| Cambiar después a excepciones propias rompe las pruebas que esperan tipos .NET | La spec de excepciones actualiza las aserciones al mismo tiempo.                                         |
| EF Core necesita constructor sin parámetros y setters accesibles              | Constructor `private` sin parámetros y setters `private`, que EF Core soporta.                          |
| Un `User` desactualizado (rol o departamento ya cambiados) llega a la entidad | Application debe cargar el `User` actual antes de llamar al método. Se documentará en la spec de casos de uso. |

## Qué **no** está en esta spec

- Excepciones de dominio propias y handler global.
- `PurchaseRequestItem`.
- Configuración de EF Core, `DbContext` y migraciones.
- Generación de `RequestNumber`.
- Unicidad de datos.
- Hash de contraseñas y autenticación.
- Casos de uso, repositorios y controladores.
- Flujo de aprobación con Finance.

Cada uno de estos puntos, si se aborda, va en su propia spec.
