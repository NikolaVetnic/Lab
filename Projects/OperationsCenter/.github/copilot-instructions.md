# GitHub Copilot Instructions – Operations Center

You are contributing to Operations Center, a portfolio-grade backend and platform engineering project.

## General behavior

- Prefer simple, readable and production-minded solutions.
- Do not generate placeholder implementations unless explicitly requested.
- Do not silently change public contracts.
- Do not add dependencies without explaining why they are needed.
- Keep changes small and focused.
- Use existing project conventions before introducing new ones.
- Explain assumptions when requirements are ambiguous.

## Implementation workflow

For non-trivial tasks:

1. Inspect the relevant code and project structure.
2. Propose a concise implementation plan.
3. Identify tests that should be added or updated.
4. Implement the change.
5. Run relevant build and test commands.
6. Report validation results and limitations.

## Backend conventions

- Use C# and modern .NET conventions.
- Use nullable reference types.
- Prefer file-scoped namespaces.
- Use dependency injection through constructors.
- Use `CancellationToken` for asynchronous I/O.
- Use `DateTimeOffset` for timestamps.
- Use `ProblemDetails` for HTTP API errors.
- Keep controllers/endpoints thin.
- Keep business logic inside application/domain code.
- Avoid generic repositories unless there is a concrete need.
- Use explicit request and response DTOs at API boundaries.
- Validate incoming requests.
- Use structured logging.

## Testing conventions

- Write unit tests for business rules.
- Write integration tests for persistence, APIs and module boundaries where valuable.
- Use descriptive test names:
  `Method_WhenCondition_ExpectedResult`
- Tests must not depend on execution order.
- Avoid real external services in unit tests.
- Prefer test containers or local dependencies for integration tests when needed.

## Security conventions

- Never hardcode secrets.
- Never log JWTs, authorization headers, passwords or sensitive payloads.
- Require authorization by default for protected functionality.
- Explicitly mark anonymous endpoints.
- Validate tenant, role and ownership boundaries where applicable.

## DevOps conventions

- Docker images should use multi-stage builds.
- Containers should run as non-root where practical.
- Health checks must be meaningful.
- CI should build, test, scan and publish artifacts.
- Kubernetes manifests and Helm charts should use configurable values.
- Infrastructure changes should be reproducible and version-controlled.

## Documentation expectations

When adding a new module or major capability, update relevant documentation:

- module responsibilities;
- local development instructions;
- API or event contract;
- architectural decision record when the decision is long-lived.
