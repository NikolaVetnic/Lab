# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Operations Center is a portfolio-grade operations platform (incidents, tasks, notifications, files, audit, dashboards). Built as a **modular monolith** with explicit module boundaries so modules can later be extracted into services. The README is in Norwegian; code, comments, and identifiers are in English.

This directory is one project inside the larger `Lab` monorepo (git root is `/Users/nikolavetnic/Projects/Lab`). Work only within this project's paths unless told otherwise.

## Critical build gotcha: internal NuGet packages

The CQRS building blocks (`BuildingBlocks.Cqrs`, `BuildingBlocks.Cqrs.Abstractions`) are consumed as **local NuGet packages** (`PackageReference` version `0.1.0`), not project references. `NuGet.config` points at `apps/api/operations-center/artifacts/packages`. If you change those projects — or on a clean checkout where `artifacts/packages/` is empty — you must (re)pack them before restore/build will succeed:

```bash
./scripts/build-internal-nugets.sh   # dotnet pack → artifacts/packages
```

The .NET solution is `apps/api/operations-center/OperationsCenter.slnx` (new XML `.slnx` format, not `.sln`). Note: root `package.json`'s `lint:dotnet` script references a stale `apps/api/OperationsCenter.sln` path.

## Common commands

Backend (from repo root or `apps/api/operations-center`):

```bash
./scripts/test-api.sh                        # restore + build (Release) + run all tests
./scripts/start-api.sh                        # start Postgres, apply EF migrations, run API locally
dotnet test apps/api/operations-center/OperationsCenter.slnx   # all tests
dotnet test apps/api/operations-center/OperationsCenter.slnx --filter "FullyQualifiedName~IncidentTelemetryTests"   # single test class
dotnet test ... --filter "Name~Method_WhenCondition_ExpectedResult"                                                # single test
```

Frontend (from `apps/web`):

```bash
npm run dev        # vite dev server
npm run build      # tsc --noEmit + vite build
npm run lint       # eslint
```

Lint / format (from repo root):

```bash
npm run lint                # format:check + lint:markdown + lint:dotnet
npm run format:check        # prettier
dotnet format apps/api/operations-center/OperationsCenter.slnx --verify-no-changes   # C# format check
```

Full stack via Docker Compose (from repo root):

```bash
cp .env.example .env
docker compose up --build          # postgres, migrations, api, web, otel-collector, prometheus
npm run smoke:compose              # end-to-end smoke test against the compose stack
SMOKE_KEEP_STACK=1 npm run smoke:compose   # keep containers up after test for debugging
```

Local URLs (compose): web `:8080`, API `:5000` (`/health`, `/ready`, `/swagger`, `/openapi/v1.json`), Postgres `:5432`, Prometheus `:9090`, collector metrics `:8889/metrics`.

## Backend architecture

Clean-architecture-style layering under `apps/api/operations-center/src/OperationsCenter/` (dependency direction points inward toward Domain):

- **`OperationsCenter.Domain`** — entities, enums, domain rules (Incidents, Identity, Audit). No persistence or framework concerns.
- **`OperationsCenter.Application`** — CQRS commands/queries organized as **vertical slices** (`Incidents/Commands/CreateIncident/…`, `Incidents/Queries/ListIncidents/…`), each with its own handler and contracts. Also holds pipeline behaviors, telemetry abstractions, and DI wiring.
- **`OperationsCenter.Infrastructure`** — EF Core (`Persistence`), Identity implementation, dev seeding. Owns migrations.
- **`OperationsCenter.Contracts`** — cross-boundary contracts (e.g. `Realtime`).
- **`OperationsCenter.Api`** — thin ASP.NET Core controllers, SignalR hubs (`/hubs/operations`), OpenTelemetry startup, seed-data entry point.
- **`src/BuildingBlocks/`** — reusable internal infra (the CQRS mediator).

### CQRS mediator (no MediatR — see ADR 0001)

An in-process mediator (`InProcessMediator`) dispatches `IRequest<T>` to `IRequestHandler<,>` (commands/queries), with `IPipelineBehavior<,>` for cross-cutting concerns. Handlers and behaviors are auto-registered via **Scrutor assembly scanning** in `ApplicationServiceCollectionExtensions.AddApplicationServices` — a new command/query handler in the Application assembly is wired up automatically, no manual registration needed.

### Module boundary rules

- A module owns its domain logic and data access; **never access another module's DB tables directly**.
- Modules communicate through explicit contracts and domain/integration events.
- Keep controllers/endpoints thin; business logic lives in Application/Domain.
- Don't add RabbitMQ, Redis, or Kubernetes deps unless the task explicitly requires them.

### Conventions

- Nullable reference types, file-scoped namespaces, constructor DI (primary constructors preferred).
- `CancellationToken` on all async I/O; async all the way (no `.Result`/`.Wait()`).
- `DateTimeOffset`, UTC for persisted timestamps.
- Explicit request/response DTOs at API boundaries; never expose EF entities in responses.
- `ProblemDetails` for HTTP errors; deliberate status codes (201 create, 409 conflict, 400 malformed).
- Test naming: `Method_WhenCondition_ExpectedResult`. Tests must not depend on execution order or on seeded data (integration tests create their own data).

## Frontend architecture

React + TypeScript + Vite under `apps/web/src`, organized by feature (`auth/`, `incidents/`, `realtime/`, `layout/`, `api/`).

- Keep API calls in typed clients under `api/` (`apiClient.ts`, `authApi.ts`, `incidentsApi.ts`), not inside components.
- SignalR connection handling lives in a reusable service layer (`realtime/operationsHub.ts`, `OperationsRealtimeProvider.tsx`, `useOperationsRealtime.ts`).
- Handle loading, empty, and error states explicitly.
- In compose, nginx reverse-proxies `/api/*` and `/hubs/*` to the API, so the frontend uses relative URLs — don't hardcode `localhost`.

## Auth & seed data

- JWT bearer auth via `POST /auth/login`. Roles gate incident/audit endpoints: `Incidents.Read` (Admin/Operator/Viewer), `Incidents.Write` (Admin/Operator).
- Dev seeding runs **only in Development and only explicitly** (`dotnet run --project src/OperationsCenter/OperationsCenter.Api -- --seed`, or `./scripts/seed-dev-data.sh`). Seeds idempotent demo users (`admin@/operator@/viewer@operations-center.local`, passwords from `DEV_SEED_*` env vars) and demo incidents. The compose `migrations` service runs this same seed flow.

## Observability

Vendor-neutral OpenTelemetry (traces/metrics/logs) exported via OTLP to a collector — app code is not coupled to any vendor (see ADR 0002). Custom `ActivitySource`/`Meter` are both named `OperationsCenter`. Custom metrics (`operations_center_incidents_created_total`, `..._status_changes_total`) increment only **after** successful persistence, and metric tags are kept low-cardinality (never incident/user IDs or emails). Disabled by default for non-container local runs (`OpenTelemetry__Enabled`), enabled in compose. An unreachable collector never affects request handling or health.

## Before completing a change

1. Build the affected solution and run relevant tests (`./scripts/test-api.sh`).
2. Add/update tests for behavior changes.
3. Run format/lint checks for the files you touched (`dotnet format … --verify-no-changes` for C#, `npm run format:check`, `npm run lint:markdown` for Markdown).
4. Don't reformat generated files, lock files, or EF migrations.
5. Document long-lived architectural decisions as an ADR in `docs/adr/`.

Ask for confirmation before: deleting data/files, destructive schema changes, broad auth changes, adding a major dependency, or restructuring multiple modules.
