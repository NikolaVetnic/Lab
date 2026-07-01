---
applyTo: "src/**/*.cs,tests/**/*.cs"
---

# .NET Instructions

## Code style

- Use `var` only when the type is obvious from the right-hand side.
- Prefer guard clauses over deeply nested conditionals.
- Keep methods focused and reasonably short.
- Avoid static mutable state.
- Avoid `.Result` and `.Wait()` on tasks.
- Prefer `async` all the way for I/O operations.
- Do not catch `Exception` unless there is a justified boundary-level reason.

## API design

- Use clear HTTP status codes.
- Return `201 Created` for successful resource creation where appropriate.
- Return `404 Not Found` only when the caller is allowed to know that the resource exists.
- Use `409 Conflict` for state conflicts.
- Use `400 Bad Request` for malformed requests.
- Use validation errors consistently through `ProblemDetails`.

## Persistence

- Keep persistence concerns out of domain entities.
- Do not expose EF Core entities directly through API responses.
- Add database migrations deliberately.
- Prefer explicit indexes for frequently queried fields.
- Consider concurrency handling for records that can be updated by multiple users.

## Observability

- Use structured logging:
  `logger.LogInformation("Incident {IncidentId} was created", incidentId);`
- Include correlation identifiers where available.
- Add tracing around external calls and message publishing.
- Do not log sensitive data.
