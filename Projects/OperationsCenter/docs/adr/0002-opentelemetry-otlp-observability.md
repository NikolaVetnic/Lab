# ADR 0002: Use OpenTelemetry with OTLP for application observability

## Context

Operations Center needs vendor-neutral tracing, metrics and log correlation across
HTTP, PostgreSQL, SignalR and future services. The system will eventually run as a
distributed set of modules/services, so instrumentation must be consistent, portable
and decoupled from any specific observability vendor.

Requirements for the current phase:

- instrument incoming and outgoing HTTP, database activity and .NET runtime;
- provide a small set of application-owned custom traces and metrics for incident
  operations;
- correlate application logs with the active trace/span;
- export all signals through a single vendor-neutral protocol;
- never make normal API availability depend on telemetry infrastructure;
- avoid coupling application code to Grafana, Jaeger, Prometheus, Application Insights,
  Datadog, New Relic or any other vendor.

Constraints:

- the solution targets .NET 10 and intentionally uses no preview/prerelease packages;
- no telemetry backend (Collector, Prometheus, Grafana, Tempo, Loki, Jaeger) is
  introduced in this step.

## Decision

Use OpenTelemetry for application instrumentation and export telemetry through OTLP
toward an OpenTelemetry Collector that will be introduced later.

Preferred flow:

```
Operations Center API
        │ OTLP
        ▼
OpenTelemetry Collector (later)
        ├── traces backend
        ├── metrics backend
        └── logs backend
```

### Composition

- Strongly typed `OpenTelemetryOptions` bound from the `OpenTelemetry` configuration
  section, overridable through environment variables and disabled by default.
- A single registration extension `AddOperationsCenterObservability(configuration, logging, environment)`
  in `OperationsCenter.Api/Observability/` keeps `Program.cs` concise.
- A single application-owned `OperationsCenterTelemetry` type in
  `OperationsCenter.Application/Observability/` defines exactly one `ActivitySource`
  (`OperationsCenter`) and one `Meter` (`OperationsCenter`). It lives in the Application
  layer (not the API layer) because the incident use cases must emit these signals and
  the dependency direction is `Api -> Application`.

### Instrumentation

Automatic:

- ASP.NET Core incoming requests (health routes `/health` and `/ready` filtered out;
  exceptions recorded);
- outgoing `HttpClient`;
- PostgreSQL via the Npgsql `ActivitySource` (parameterized SQL only, no parameter
  values or connection strings);
- .NET runtime metrics plus ASP.NET Core and HttpClient metrics.

Custom traces:

- `incident.create` and `incident.status_change`.

Custom metrics:

- `operations_center.incidents.created` (tag `severity`), incremented only after
  successful persistence;
- `operations_center.incidents.status_changes` (tags `previous_status`, `new_status`),
  incremented only after a successful status transition.

### Logging

The existing `ILogger<T>` pipeline is preserved. When telemetry is enabled the
OpenTelemetry logging provider is added and exports only through OTLP, so logs within an
active trace gain trace/span correlation without duplicating console output.

### Export and resilience

- OTLP export uses SDK defaults (no custom retry logic).
- Export is disabled by default in `appsettings.json`, `.env.example` and Docker Compose.
- An unavailable Collector never fails startup, requests or health checks; export
  failures are logged and dropped.

### Scope exclusions

Process instrumentation is deferred because only a prerelease package exists and the
project uses no preview dependencies. No Collector, Prometheus, Grafana, Tempo, Loki,
Jaeger, dashboards, alerts or sampling policy are introduced in this step.

## Consequences

Positive:

- vendor-neutral instrumentation;
- consistent telemetry across current and future services;
- distributed tracing support;
- clear separation between application instrumentation and telemetry storage.

Tradeoffs:

- additional runtime packages;
- a telemetry backend (Collector and storage) must be operated later;
- sampling and retention decisions are deferred.
