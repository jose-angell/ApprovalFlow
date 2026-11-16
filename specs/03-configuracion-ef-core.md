# SPEC 03 — Configuración de EF Core con Fluent API y AppDbContext

> **Estado:** implementado
> **Depende de:** SPEC 01, SPEC 02
> **Fecha:** 2026-11-16
> **Objetivo:** Mapear `Department`, `User` y `PurchaseRequest` a PostgreSQL con Fluent API: una configuración por archivo en `ApprovalFlow.Infrastructure/Configurations`, el `AppDbContext` en `Infrastructure/Persistence` registrado en la API y la migración inicial aplicada.

## Alcance

**Dentro:**

- Paquetes NuGet que faltan:
  - `Npgsql.EntityFrameworkCore.PostgreSQL` (10.x) y `Microsoft.Extensions.Configuration.Abstractions` (10.x) en Infrastructure.
  - `Microsoft.EntityFrameworkCore.Design` 10.0.15 en la API.
- Referencia de proyecto `ApprovalFlow.Api` → `ApprovalFlow.Infrastructure`.
- `ApprovalFlow.Infrastructure/Configurations/DepartmentConfiguration.cs`, `UserConfiguration.cs` y `PurchaseRequestConfiguration.cs`. Cada uno implementa `IEntityTypeConfiguration<T>`.
- `ApprovalFlow.Infrastructure/Persistence/AppDbContext.cs`.
- `ApprovalFlow.Infrastructure/DependencyInjection.cs` con `AddInfrastructure(IConfiguration)`.
- Connection string `DefaultConnection` en `appsettings.Development.json`. En `appsettings.json` la clave queda vacía.
- Llamada a `AddInfrastructure` en `Program.cs`.
- Migración `InitialCreate` en `ApprovalFlow.Infrastructure/Persistence/Migrations/`, aplicada al Postgres de SPEC 01.
- Borrar `ApprovalFlow.Infrastructure/Class1.cs`.

**Fuera de alcance (para futuras specs):**

- Repositorios, Unit of Work e interfaces en Application.
- Propiedades de navegación en las entidades.
- Datos semilla (seed) de departamentos o del usuario Admin.
- Aplicar migraciones automáticamente al arrancar la API.
- Pruebas automáticas del modelo EF o pruebas de integración contra Postgres.
- Concurrencia optimista (`xmin` / row version).
- Generación de `RequestNumber`.
- Connection string para entornos distintos de desarrollo y gestión de secretos (user-secrets o variables de entorno).
- Healthcheck de la base de datos.

## Modelo de datos

Esta spec no cambia las entidades de SPEC 02. Solo define cómo se mapean. Las longitudes se toman de las constantes de cada entidad (`Department.NameMaxLength`, etc.) y no se repiten como números literales.

### Convenciones comunes

- Tablas y columnas en `snake_case`, con `ToTable(...)` y `HasColumnName(...)` explícitos.
- `Id`: clave primaria `id` con `ValueGeneratedNever()`, porque la entidad genera el `Guid`.
- Enums guardados como texto con `HasConversion<string>()` y `HasMaxLength(20)`.
- `DateTime` / `DateTime?` → `timestamp with time zone`, que es el tipo por defecto de Npgsql.
- Claves foráneas sin navegación: `HasOne<T>().WithMany().HasForeignKey(...)` con `OnDelete(DeleteBehavior.Restrict)`.

### `departments` (`DepartmentConfiguration`)

| Propiedad     | Columna       | Tipo / regla                         |
| ------------- | ------------- | ------------------------------------ |
| `Id`          | `id`          | PK `uuid`                            |
| `Name`        | `name`        | `varchar(100)`, requerido, **único** |
| `Description` | `description` | `varchar(500)`, nullable             |
| `IsActive`    | `is_active`   | `boolean`, requerido                 |
| `CreatedAt`   | `created_at`  | `timestamptz`, requerido             |

### `users` (`UserConfiguration`)

| Propiedad      | Columna         | Tipo / regla                                       |
| -------------- | --------------- | -------------------------------------------------- |
| `Id`           | `id`            | PK `uuid`                                          |
| `FullName`     | `full_name`     | `varchar(150)`, requerido                          |
| `Email`        | `email`         | `varchar(256)`, requerido, **único**               |
| `PasswordHash` | `password_hash` | `text`, requerido                                  |
| `Role`         | `role`          | `varchar(20)` (texto del enum), requerido          |
| `DepartmentId` | `department_id` | `uuid` nullable, FK → `departments.id`, `Restrict` |
| `IsActive`     | `is_active`     | `boolean`, requerido                               |
| `CreatedAt`    | `created_at`    | `timestamptz`, requerido                           |
| `UpdatedAt`    | `updated_at`    | `timestamptz`, nullable                            |

### `purchase_requests` (`PurchaseRequestConfiguration`)

| Propiedad       | Columna           | Tipo / regla                                          |
| --------------- | ----------------- | ----------------------------------------------------- |
| `Id`            | `id`              | PK `uuid`                                             |
| `RequestNumber` | `request_number`  | `varchar(30)`, requerido, **único**                   |
| `Title`         | `title`           | `varchar(150)`, requerido                             |
| `Justification` | `justification`   | `varchar(2000)`, requerido                            |
| `Comments`      | `comments`        | `varchar(1000)`, nullable                             |
| `TotalAmount`   | `total_amount`    | `numeric(18,2)` (`HasPrecision(18, 2)`), requerido    |
| `Status`        | `status`          | `varchar(20)` (texto del enum), requerido, con índice |
| `RequesterId`   | `requester_id`    | `uuid`, FK → `users.id`, `Restrict`                   |
| `DepartmentId`  | `department_id`   | `uuid`, FK → `departments.id`, `Restrict`             |
| `CreatedAt`     | `created_at`      | `timestamptz`, requerido                              |
| `UpdatedAt`     | `updated_at`      | `timestamptz`, nullable                               |
| `ReviewedAt`    | `reviewed_at`     | `timestamptz`, nullable                               |
| `ReviewedById`  | `reviewed_by_id`  | `uuid` nullable, FK → `users.id`, `Restrict`          |
| `ApprovedAt`    | `approved_at`     | `timestamptz`, nullable                               |
| `ApprovedById`  | `approved_by_id`  | `uuid` nullable, FK → `users.id`, `Restrict`          |
| `RejectedAt`    | `rejected_at`     | `timestamptz`, nullable                               |
| `RejectedById`  | `rejected_by_id`  | `uuid` nullable, FK → `users.id`, `Restrict`          |
| `CancelledAt`   | `cancelled_at`    | `timestamptz`, nullable                               |
| `CancelledById` | `cancelled_by_id` | `uuid` nullable, FK → `users.id`, `Restrict`          |

EF crea solo un índice no único en cada columna FK.

### `AppDbContext` (`ApprovalFlow.Infrastructure/Persistence/AppDbContext.cs`)

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    // OnModelCreating: modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

Namespaces: `ApprovalFlow.Infrastructure.Configurations` y `ApprovalFlow.Infrastructure.Persistence`.

### Configuración

`appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=approvalflow;Username=approvalflow_user;Password=approvalflow_dev"
}
```

`appsettings.json`: `"ConnectionStrings": { "DefaultConnection": "" }`.

`AddInfrastructure(IConfiguration configuration)` lee `DefaultConnection`. Si está vacía, lanza `InvalidOperationException` con un mensaje claro. Después registra `AddDbContext<AppDbContext>(o => o.UseNpgsql(cs))`.

## Plan de implementación

1. Agregar a Infrastructure los paquetes `Npgsql.EntityFrameworkCore.PostgreSQL` y `Microsoft.Extensions.Configuration.Abstractions` (versión 10.x alineada con EF Core 10.0.15). Borrar `Infrastructure/Class1.cs`. Comprobar con `dotnet build ApprovalFlow.slnx`.
2. Crear `Configurations/DepartmentConfiguration.cs`, `UserConfiguration.cs` y `PurchaseRequestConfiguration.cs` según las tablas de arriba. Comprobar con `dotnet build`.
3. Crear `Persistence/AppDbContext.cs` con los tres `DbSet` y `ApplyConfigurationsFromAssembly`. Comprobar con `dotnet build`.
4. Crear `DependencyInjection.cs` con `AddInfrastructure`. En la API, agregar la referencia a Infrastructure y el paquete `Microsoft.EntityFrameworkCore.Design` 10.0.15. Agregar `DefaultConnection` a los dos `appsettings`. Llamar a `builder.Services.AddInfrastructure(builder.Configuration)` en `Program.cs`. Comprobar con `dotnet build` y con `dotnet run` (la API arranca sin errores).
5. Generar la migración: `dotnet ef migrations add InitialCreate --project ApprovalFlow.Infrastructure --startup-project ApprovalFlow --output-dir Persistence/Migrations`. Revisar que el archivo generado tenga los nombres `snake_case`, los índices únicos y `ReferentialAction.Restrict`.
6. Con el contenedor de SPEC 01 levantado, aplicar con `dotnet ef database update --project ApprovalFlow.Infrastructure --startup-project ApprovalFlow`. Revisar con `docker exec -it approvalflow-postgres psql -U approvalflow_user -d approvalflow -c "\d purchase_requests"`.
7. Actualizar la sección "Known gaps" de `CLAUDE.md`: quitar los puntos que esta spec resuelve (Npgsql, referencia de la API y connection string).

## Criterios de aceptación

- [x] `dotnet build ApprovalFlow.slnx` termina sin errores ni warnings nuevos.
- [x] `ApprovalFlow.Infrastructure.csproj` referencia `Npgsql.EntityFrameworkCore.PostgreSQL` y `Microsoft.Extensions.Configuration.Abstractions`. `ApprovalFlow.Api.csproj` referencia `ApprovalFlow.Infrastructure` y `Microsoft.EntityFrameworkCore.Design`.
- [x] `ApprovalFlow.Domain.csproj` sigue sin `PackageReference` ni `ProjectReference`.
- [x] `ApprovalFlow.Application.csproj` no referencia Infrastructure ni EF Core.
- [x] Existen los tres archivos en `Infrastructure/Configurations/`, `Infrastructure/Persistence/AppDbContext.cs` e `Infrastructure/DependencyInjection.cs`. `Infrastructure/Class1.cs` ya no existe.
- [x] Cada configuración está en su propio archivo e implementa `IEntityTypeConfiguration<T>`. `AppDbContext.OnModelCreating` no contiene mapeos inline, solo `ApplyConfigurationsFromAssembly`.
- [x] Existe `Infrastructure/Persistence/Migrations/*_InitialCreate.cs`.
- [x] `dotnet ef database update` termina sin errores contra el contenedor `approvalflow-postgres`.
- [x] `\dt` en psql lista `departments`, `users`, `purchase_requests` y `__EFMigrationsHistory`.
- [x] `\d users` muestra `email character varying(256)`, `role character varying(20)` y un índice único en `email`.
- [x] `\d departments` muestra un índice único en `name`.
- [x] `\d purchase_requests` muestra `total_amount numeric(18,2)`, `created_at timestamp with time zone`, un índice único en `request_number`, un índice en `status` y seis FKs (`requester_id`, `department_id` y los cuatro `*_by_id`).
- [x] Ninguna FK es `ON DELETE CASCADE`. En `\d` aparecen como `ON DELETE RESTRICT`.
- [x] `dotnet run --project ApprovalFlow/ApprovalFlow.Api.csproj` arranca en Development sin excepciones.
- [x] Con `DefaultConnection` vacía, `AddInfrastructure` lanza `InvalidOperationException` al arrancar.
- [x] `dotnet test` sigue pasando todas las pruebas de SPEC 02.

## Decisiones

- **Sí:** una clase `IEntityTypeConfiguration<T>` por archivo, cargada con `ApplyConfigurationsFromAssembly`. Las configuraciones nuevas se registran solas.
- **No:** mapeos en `OnModelCreating`. Crecen sin orden.
- **Sí:** `snake_case` con `ToTable` y `HasColumnName` explícitos. Es lo idiomático en Postgres y no requiere comillas en SQL manual.
- **No:** el paquete `EFCore.NamingConventions`. Es una dependencia más y su compatibilidad con EF 10 es incierta.
- **No:** PascalCase por defecto. Obliga a usar comillas en SQL.
- **Sí:** enums como texto (`varchar(20)`). Se leen bien en la BD y no dependen de los números del enum.
- **Sí:** FKs reales sin navegación, todas `Restrict`, incluidas las de auditoría (`*_by_id`). La BD garantiza la integridad y no se pierde historial por un borrado.
- **No:** propiedades de navegación. SPEC 02 las dejó fuera y se mantiene así.
- **Sí:** índices únicos en `users.email`, `departments.name` y `purchase_requests.request_number`. Esto cierra la unicidad que SPEC 02 dejó pendiente.
- **Sí:** índice en `purchase_requests.status`, para filtrar por estado.
- **Sí:** `numeric(18,2)` para el monto. Coincide con la regla de 2 decimales de la entidad.
- **Sí:** `timestamptz` (por defecto en Npgsql). Las entidades usan `DateTime.UtcNow`. **No:** el switch legacy de timestamp.
- **Sí:** `ValueGeneratedNever()` en `Id`. La entidad genera el `Guid`.
- **Sí:** las longitudes salen de las constantes de las entidades, para que haya una sola fuente de verdad.
- **Sí:** extensión `AddInfrastructure(IConfiguration)` en Infrastructure. `Program.cs` queda limpio y la API no conoce Npgsql.
- **Sí:** la API referencia Infrastructure. Es la raíz de composición. Los controladores siguen hablando solo con Application.
- **Sí:** connection string en `appsettings.Development.json` con los valores del `.env` de SPEC 01. **No:** user-secrets por ahora, porque es una contraseña solo de desarrollo local.
- **Sí:** migración `InitialCreate` en `Persistence/Migrations`, junto al `AppDbContext`.
- **No:** pruebas automáticas del modelo en esta spec. Se verifica a mano con la migración y `psql \d`.
- **No:** aplicar migraciones al arrancar la API. Se aplican explícitamente con `dotnet ef database update`.

## Riesgos

| Riesgo                                                                              | Mitigación                                                                                          |
| ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| Versión de Npgsql incompatible con EF Core 10.0.15                                  | Usar la última `10.0.x` de Npgsql. Si hay conflicto, alinear EF Core a la versión que pide Npgsql.  |
| `dotnet ef` falla porque falta `Design` en el proyecto de arranque                  | El paso 4 lo agrega a la API antes de generar la migración.                                         |
| La contraseña de desarrollo queda en git                                            | Es la misma del `.env` local y no sirve fuera del contenedor. Los secretos para otros entornos van en otra spec. |
| Un `DateTime` con `Kind` distinto de `Utc` falla al guardar en `timestamptz`        | Las entidades usan `DateTime.UtcNow`. Npgsql lanza una excepción clara si llega otro `Kind`.        |
| Un FK `Restrict` impide borrar usuarios o departamentos con solicitudes             | Está previsto: las entidades se desactivan (`Deactivate`) y no se borran.                           |
| Se agregan más `IEntityTypeConfiguration` en el ensamblado y se aplican sin querer  | Solo se aceptan configuraciones en `Configurations/`. Se revisa en code review.                     |

## Qué **no** está en esta spec

- Repositorios, Unit of Work e interfaces en Application.
- Propiedades de navegación.
- Datos semilla.
- Migraciones automáticas al arrancar.
- Pruebas automáticas del modelo o de integración.
- Concurrencia optimista.
- Generación de `RequestNumber`.
- Secretos y connection strings para otros entornos.
- Healthcheck de la base de datos.

Cada uno de estos puntos, si se aborda, va en su propia spec.
