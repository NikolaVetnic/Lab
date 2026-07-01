# Backend-specific rules

- Keep module boundaries explicit.
- Do not let one module access another module's persistence tables directly.
- Keep API endpoints thin.
- Use PostgreSQL through EF Core.
- Add tests for business rules and API behavior.
- Do not introduce RabbitMQ, Redis or Kubernetes dependencies unless the task explicitly requires them.
- Use primary constructors where possible, and classic constructors where necessary.
