# Operations Center – Agent Guidelines

## Project goal

Operations Center is a portfolio-grade operations platform for handling incidents,
tasks, notifications, files, audit events and real-time dashboards.

The project must demonstrate strong backend engineering, DevOps practices,
security, observability and maintainable architecture.

## Development approach

- Start as a modular monolith, not microservices.
- Keep module boundaries explicit so modules can later be extracted into services.
- Prefer vertical slices over large horizontal layers.
- Build working functionality incrementally.
- Do not introduce infrastructure unless a concrete feature needs it.
- Avoid unnecessary abstractions and speculative patterns.

## Core modules

- Identity
- Incidents
- Tasks
- Notifications
- Files
- Audit
- Dashboard
- Search

## Architecture principles

- Each module owns its domain logic and data access.
- Modules communicate through explicit contracts and domain/integration events.
- Do not access another module's database tables directly.
- Keep APIs backward compatible unless a deliberate breaking change is documented.
- Use async APIs for I/O.
- Use cancellation tokens for request and background-work boundaries.
- Use UTC for persisted timestamps.
- Use strongly typed identifiers where practical.
- Prefer immutable DTOs and request models.

## Security requirements

- Never commit secrets, tokens, passwords, connection strings or certificates.
- Use environment variables or local secret storage for development secrets.
- Validate authorization at endpoint and application boundaries.
- Apply least privilege.
- Treat audit records as append-only.
- Never log credentials, access tokens, personal data or complete request bodies.

## Quality requirements

Before marking a task complete:

1. Build the relevant solution.
2. Run relevant tests.
3. Add or update tests for behavior changes.
4. Check formatting and analyzer warnings.
5. Summarize changed files, validation performed and known limitations.

## Agent behavior

Before implementing a non-trivial change:

1. Inspect relevant files.
2. State the proposed approach briefly.
3. Identify affected modules and contracts.
4. Implement the smallest coherent change.
5. Run validation commands.
6. Do not modify unrelated files.

Ask for confirmation before:

- deleting data or files;
- changing database schemas in destructive ways;
- adding paid cloud services;
- changing authentication or authorization behavior broadly;
- introducing a major dependency;
- restructuring multiple modules.

## Formatting and linting

Before completing a change:

- Run `npm run format:check` for repository-wide formatting checks.
- Run `npm run lint:markdown` when Markdown files change.
- Run `dotnet format apps/api/OperationsCenter.sln --verify-no-changes` when C# files change.
- Do not reformat generated files, lock files, EF Core migrations or unrelated files.

## Documentation

Document meaningful architectural decisions in `docs/adr`.

ADR files should contain:

- Context
- Decision
- Consequences
- Alternatives considered
