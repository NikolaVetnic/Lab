# Backend-specific rules

- Keep module boundaries explicit.
- Do not let one module access another module's persistence tables directly.
- Keep API endpoints thin.
- Use PostgreSQL through EF Core.
- Add tests for business rules and API behavior.
- Do not introduce RabbitMQ, Redis or Kubernetes dependencies unless the task explicitly requires them.
- Do not attempt to build solution to verify changes.