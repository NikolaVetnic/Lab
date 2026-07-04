# ADR 0001: Internal CQRS Mediator as Building Blocks

## Context

Operations Center uses a CQRS-style application layer with commands and queries.

The project needs mediator-style request dispatching and pipeline behavior support, but this capability must remain internal and not depend on the external MediatR library.

Requirements for the current phase:

- keep request/handler abstractions internal to the repository;
- support command and query handlers with a unified dispatcher;
- support pipeline behaviors for cross-cutting concerns (validation/logging/etc.);
- keep DI registration simple and convention-based via assembly scanning;
- support packaging and reuse as internal NuGet packages.

## Decision

Implement an in-process internal mediator as reusable building blocks under:

- `apps/api/operations-center/src/BuildingBlocks/BuildingBlocks.Cqrs`
- `apps/api/operations-center/src/BuildingBlocks/BuildingBlocks.Cqrs.Abstractions`

Chosen package and namespace split:

- `BuildingBlocks.Cqrs.Abstractions`
  Provides contracts only.
- `BuildingBlocks.Cqrs`
  Provides the runtime mediator implementation and depends on abstractions.

Chosen abstractions:

- IRequest<TResponse>
- IRequestHandler<TRequest, TResponse>
- ICommand / ICommand<TResponse>
- ICommandHandler<TCommand> / ICommandHandler<TCommand, TResponse>
- IQuery<TResponse>
- IQueryHandler<TQuery, TResponse>
- IPipelineBehavior<TRequest, TResponse>
- ISender / IMediator
- Unit

Runtime namespace:

- `BuildingBlocks.Cqrs`

Abstractions namespace:

- `BuildingBlocks.Cqrs.Abstractions`

Dispatcher implementation:

- InProcessMediator resolves request handlers from DI.
- It composes all registered IPipelineBehavior<TRequest, TResponse> instances around handler execution.
- It supports ICommandHandler and IQueryHandler registrations through the IRequestHandler-based contracts.
- It throws explicit InvalidOperationException messages when handler contracts are missing or invalid.

DI registration:

- AddApplicationServices registers IMediator and ISender.
- Scrutor assembly scanning registers IRequestHandler<,> and IPipelineBehavior<,> implementations.
- Existing use case self-registration remains unchanged.

Packaging and consumption:

- Both projects are packable and versioned independently from external libraries.
- `BuildingBlocks.Cqrs` depends on `BuildingBlocks.Cqrs.Abstractions`.
- Application code references the runtime project, while handlers, requests, and behaviors can depend only on abstractions where appropriate.
- `OperationsCenter.Application` references abstractions directly and references runtime only for DI composition through `InProcessMediator`.
- Unit tests that assert mediator contracts reference abstractions directly instead of relying on transitive runtime references.
- The repository root contains a `NuGet.config` that points to the local package output folder so the solution can validate package-based consumption without an external feed.

Manual package build:

From the repository root:

```bash
cd apps/api/operations-center
dotnet pack src/BuildingBlocks/BuildingBlocks.Cqrs.Abstractions/BuildingBlocks.Cqrs.Abstractions.csproj --configuration Release --output ./artifacts/packages
dotnet pack src/BuildingBlocks/BuildingBlocks.Cqrs/BuildingBlocks.Cqrs.csproj --configuration Release --output ./artifacts/packages
```

This produces local `.nupkg` files under `apps/api/operations-center/artifacts/packages`.

Manual package reference:

1. Add the package folder as a NuGet source.

Example `NuGet.config` beside the consuming solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
 <packageSources>
  <clear />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  <add key="local-building-blocks" value="artifacts/packages" />
 </packageSources>
</configuration>
```

1. Reference the packages from the consuming project:

```xml
<ItemGroup>
 <PackageReference Include="BuildingBlocks.Cqrs.Abstractions" Version="0.1.0" />
 <PackageReference Include="BuildingBlocks.Cqrs" Version="0.1.0" />
</ItemGroup>
```

1. Restore/build normally with `dotnet restore` and `dotnet build`.

In this repository, the local package source is configured in the root `NuGet.config`, and consuming projects can use `PackageReference` instead of `ProjectReference` to validate the internal package flow end to end.

If the consumer only needs request/handler contracts, reference `BuildingBlocks.Cqrs.Abstractions` only.

Verification:

- Unit tests cover mediator registration, command dispatch, query dispatch, and behavior wrapping order.

## Consequences

Positive:

- No dependency on external mediator packages.
- Full control over internal contracts and behavior.
- Internal package publishing is straightforward because abstractions and implementation are already isolated.

Trade-offs:

- The team owns maintenance of mediator internals.
- Feature parity with third-party mediator libraries is not automatic.
- Reflection-based invocation in InProcessMediator has runtime overhead and requires good test coverage.
- Consumers must keep package versions aligned when using local or internal feeds.

## Alternatives considered

Use MediatR directly:

- Rejected because the implementation must be internal and package-independent.

Keep direct use-case invocation without mediator abstraction:

- Rejected because it weakens consistency for cross-cutting behavior composition.

Build a compile-time generated dispatcher:

- Deferred; could reduce reflection overhead later but increases complexity now.
