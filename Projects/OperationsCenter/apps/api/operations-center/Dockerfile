FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY NuGet.config ./
COPY apps/api/operations-center/artifacts/packages ./apps/api/operations-center/artifacts/packages
COPY apps/api/operations-center/src/BuildingBlocks/BuildingBlocks.Cqrs.Abstractions/BuildingBlocks.Cqrs.Abstractions.csproj ./apps/api/operations-center/src/BuildingBlocks/BuildingBlocks.Cqrs.Abstractions/
COPY apps/api/operations-center/src/BuildingBlocks/BuildingBlocks.Cqrs/BuildingBlocks.Cqrs.csproj ./apps/api/operations-center/src/BuildingBlocks/BuildingBlocks.Cqrs/
COPY apps/api/operations-center/src/OperationsCenter/OperationsCenter.Api/OperationsCenter.Api.csproj ./apps/api/operations-center/src/OperationsCenter/OperationsCenter.Api/
COPY apps/api/operations-center/src/OperationsCenter/OperationsCenter.Application/OperationsCenter.Application.csproj ./apps/api/operations-center/src/OperationsCenter/OperationsCenter.Application/
COPY apps/api/operations-center/src/OperationsCenter/OperationsCenter.Contracts/OperationsCenter.Contracts.csproj ./apps/api/operations-center/src/OperationsCenter/OperationsCenter.Contracts/
COPY apps/api/operations-center/src/OperationsCenter/OperationsCenter.Domain/OperationsCenter.Domain.csproj ./apps/api/operations-center/src/OperationsCenter/OperationsCenter.Domain/
COPY apps/api/operations-center/src/OperationsCenter/OperationsCenter.Infrastructure/OperationsCenter.Infrastructure.csproj ./apps/api/operations-center/src/OperationsCenter/OperationsCenter.Infrastructure/

RUN dotnet restore ./apps/api/operations-center/src/OperationsCenter/OperationsCenter.Api/OperationsCenter.Api.csproj

FROM restore AS publish
COPY apps/api/operations-center/src ./apps/api/operations-center/src

RUN dotnet publish ./apps/api/operations-center/src/OperationsCenter/OperationsCenter.Api/OperationsCenter.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=6 \
    CMD wget -qO- http://127.0.0.1:8080/ready >/dev/null || exit 1

USER $APP_UID

ENTRYPOINT ["dotnet", "OperationsCenter.Api.dll"]
