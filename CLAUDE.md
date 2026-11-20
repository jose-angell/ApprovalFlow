# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ApprovalFlow is an API for controlling approval workflows. Initial roles: **Solicitante** (requester), **Aprobador** (approver), **Finanzas** (finance), plus **Administrador**. The project language/domain vocabulary is Spanish; keep naming consistent with whatever the user introduces.

Status: in development. Implemented: domain entities, EF Core + PostgreSQL, global error handling, user use cases, JWT authentication/login and `UsersController` with role-based authorization, department use cases and `DepartmentsController`, purchase request use cases and `PurchaseRequestsController`. The Finance two-level approval flow is not implemented (Finance is read-only).

## Commands

Solution file is `ApprovalFlow.slnx` (new XML solution format, .NET 10 SDK required).

```bash
dotnet restore ApprovalFlow.slnx
dotnet build ApprovalFlow.slnx
dotnet run --project ApprovalFlow/ApprovalFlow.Api.csproj        # run the API

# EF Core migrations (DbContext lives in Infrastructure, startup project is the API)
dotnet ef migrations add <Name> --project ApprovalFlow.Infrastructure --startup-project ApprovalFlow
dotnet ef database update      --project ApprovalFlow.Infrastructure --startup-project ApprovalFlow
```

Tests (`ApprovalFlow.Tests`, xUnit): `dotnet test`, single test with `dotnet test --filter "FullyQualifiedName~<Class>.<Method>"`. Integration tests live in `ApprovalFlow.Tests/Api/Integration/` (`WebApplicationFactory<Program>` + EF Core InMemory, no Postgres needed); run only them with `dotnet test --filter "FullyQualifiedName~Api.Integration"`. `ApiFactory` sets the `Testing` environment, passes `Jwt` settings via `UseSetting` and replaces the Npgsql registration (both `DbContextOptions<AppDbContext>` and `IDbContextOptionsConfiguration<AppDbContext>` must be removed in EF Core 10).

Swagger UI (Bearer scheme) is served at `/swagger` only in Development. `ApprovalFlow/ApprovalFlow.http` has login and `/api/users` requests with a `@token` variable.

## Architecture

N-layer Clean Architecture. Dependencies point inward toward Domain:

| Project | Folder | Responsibility | References |
|---|---|---|---|
| `ApprovalFlow.Domain` | `ApprovalFlow.Domain/` | Entities with their own validations and behavior methods (state transitions like approve/reject live on the entity, not in services). No external packages. | — |
| `ApprovalFlow.Application` | `ApprovalFlow.Application/` | Business logic / use cases; defines abstractions that Infrastructure implements. `Abstractions/` holds `IApplicationDbContext`, `IPasswordHasher` and `IJwtTokenGenerator` (with the `AccessToken` record); `Abstractions/UseCases/` groups all use case interfaces (`IUserUseCase`, `IAuthUseCase`, `IDepartmentUseCase`, `IPurchaseRequestUseCase`, ...). Each use case lives in its own feature folder (`Users/`, `Auth/`, `Departments/` and `PurchaseRequests/`, each with the use case and `Dtos/`); DTOs are `record`s annotated with DataAnnotations. `DependencyInjection.cs` exposes `AddApplication()`. References `Microsoft.EntityFrameworkCore` (abstractions only, no provider such as Npgsql). Custom exceptions live in `Exceptions/` (`NotFoundException`, `ConflictException`, `ForbiddenException`, `UnauthorizedException`); use cases throw them and know nothing about HTTP. | Domain |
| `ApprovalFlow.Infrastructure` | `ApprovalFlow.Infrastructure/` | Data access with EF Core + PostgreSQL; implements Application interfaces. `Security/` holds `PasswordHasher`, `JwtOptions` (validated at startup with `ValidateOnStart`: `Key` must be at least 32 bytes) and `JwtTokenGenerator` (`JsonWebTokenHandler`, HMAC-SHA256). `Persistence/AdminSeeder.cs` creates the first Admin at startup. | Application, Domain |
| `ApprovalFlow.Api` | `ApprovalFlow/` (note: folder name differs from project name) | Presentation: ASP.NET Core controllers, DI composition in `Program.cs`. `Middleware/GlobalExceptionHandler` (`IExceptionHandler`) maps exceptions to `ProblemDetails` responses. `Extensions/AuthenticationExtensions` configures JwtBearer (401/403 as `ProblemDetails`), `Extensions/ClaimsPrincipalExtensions` provides `GetUserId()`, `Authorization/Roles` holds role constants. Controllers: `AuthController` (`POST /api/auth/login`), `UsersController` (`/api/users`), `DepartmentsController` (`/api/departments`) and `PurchaseRequestsController` (`/api/purchase-requests`). | Application, Infrastructure |

Rules:
- Domain must not reference EF Core or any other layer.
- Application must not reference Infrastructure, Npgsql or ASP.NET Core; it depends on interfaces it owns. It may use EF Core through `IApplicationDbContext` (`DbSet<T>`, `AnyAsync`, `ToListAsync`).
- Do not create repositories or Unit of Work: use cases inject `IApplicationDbContext` as `_context` (`DbContext` already is both patterns).
- Controllers stay thin and delegate to Application use cases.
- Error handling is global: do not catch exceptions in controllers. Throw Application exceptions and let `GlobalExceptionHandler` map them (404/409/403/401). Domain's `ArgumentException` → 400 and `InvalidOperationException` → 409; any other exception → 500 with a generic message (details only in the log).

## Authentication and authorization

- JWT access tokens only (no refresh tokens, no revocation). Config section `Jwt` (`Issuer`, `Audience`, `Key`, `ExpirationMinutes`): `Key` is empty in `appsettings.json` and set in `appsettings.Development.json`; in other environments provide it via environment variables (`Jwt__Key`). The API does not start if it is missing or shorter than 32 bytes.
- Token claims: `sub`, `email`, `name`, `role` (enum name), `department_id` (only if the user has one), `jti`. JwtBearer uses `MapInboundClaims = false`, `NameClaimType = "name"`, `RoleClaimType = "role"`.
- Authorize with `[Authorize(Roles = Roles.Admin)]` (constants in `ApprovalFlow/Authorization/Roles.cs`); no named policies. `UsersController` is `[Authorize]` at class level: management actions are Admin-only, `/me` and `/me/password` are open to any authenticated user. `DepartmentsController` is also `[Authorize]` at class level: reads (`GET`) are open to any authenticated user, writes are Admin-only. Controllers only read `User.GetUserId()` and pass it to the use case; self-protection rules (an Admin cannot change their own role or deactivate themselves) live in `UserUseCase`. Department rules live in `DepartmentUseCase`: the name is unique case-insensitively (409) and a department with active users cannot be deactivated (409). `PurchaseRequestsController` is `[Authorize]` at class level: reads (`GET`) are open to any authenticated user; create, edit and `cancel` are Requester-only; `start-review`, `approve` and `reject` are Approver-only (wrong role → 403). State transitions are domain rules (409). Actions are `POST /{id}/<action>` and return 200 with the updated `PurchaseRequestDto`; `Status` is serialized as a number (1 = Pending, 2 = InReview, 3 = Approved, 4 = Rejected, 5 = Cancelled).
- Purchase request visibility lives in `PurchaseRequestUseCase` (one private filter used by reads and writes): Requester sees their own, Approver sees those of their department (none if they have no department), Finance and Admin see all. A request that is missing or not visible → the same 404. The acting user's role, department and active state are read from the DB, not from the token. `RequestNumber` is `PR-{yyyy}-{n:D6}` (UTC year, sequential per year, computed by counting; a concurrent duplicate hits the unique index and returns 500). `GET /api/purchase-requests` accepts an optional `?status=` and orders by `CreatedAt` descending, without pagination.
- Login failures (unknown email, wrong password, inactive user) all throw `UnauthorizedException("Credenciales inválidas.")`. The token stays valid until it expires even if the user is deactivated or changes role.
- Seed: section `SeedAdmin` (`FullName`, `Email`, `Password`). At startup `AdminSeeder` creates the first Admin only if `Email`/`Password` are set and no Admin exists. It does not run migrations: run `dotnet ef database update` first.
- `UserRole` is serialized as a number in JSON (1 = Admin, 2 = Requester, 3 = Approver, 4 = Finance).
