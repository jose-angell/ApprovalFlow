# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ApprovalFlow is an API for controlling approval workflows. Initial roles: **Solicitante** (requester), **Aprobador** (approver), **Finanzas** (finance), plus **Administrador**. The project language/domain vocabulary is Spanish; keep naming consistent with whatever the user introduces.

Status: early skeleton. Projects exist but mostly contain template code (`Class1.cs`, `WeatherForecastController`). No test project exists yet.

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

Tests (once a test project exists): `dotnet test`, single test with `dotnet test --filter "FullyQualifiedName~<Class>.<Method>"`.

## Architecture

N-layer Clean Architecture. Dependencies point inward toward Domain:

| Project | Folder | Responsibility | References |
|---|---|---|---|
| `ApprovalFlow.Domain` | `ApprovalFlow.Domain/` | Entities with their own validations and behavior methods (state transitions like approve/reject live on the entity, not in services). No external packages. | — |
| `ApprovalFlow.Application` | `ApprovalFlow.Application/` | Business logic / use cases; defines abstractions (repository interfaces, etc.) that Infrastructure implements. | Domain |
| `ApprovalFlow.Infrastructure` | `ApprovalFlow.Infrastructure/` | Data access with EF Core + PostgreSQL; implements Application interfaces. | Application, Domain |
| `ApprovalFlow.Api` | `ApprovalFlow/` (note: folder name differs from project name) | Presentation: ASP.NET Core controllers, DI composition in `Program.cs`. | Application |

Rules:
- Domain must not reference EF Core or any other layer.
- Application must not reference Infrastructure; it depends on interfaces it owns.
- Controllers stay thin and delegate to Application use cases.
