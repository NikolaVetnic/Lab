# syntax=docker/dockerfile:1

FROM node:24-alpine AS frontend
WORKDIR /src

COPY Client/signalr-chat-client/package*.json ./Client/signalr-chat-client/
WORKDIR /src/Client/signalr-chat-client
RUN npm ci

WORKDIR /src
COPY Client/signalr-chat-client ./Client/signalr-chat-client
WORKDIR /src/Client/signalr-chat-client
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Api/SignalRChat.Api/SignalRChat.Api.csproj ./Api/SignalRChat.Api/
RUN dotnet restore ./Api/SignalRChat.Api/SignalRChat.Api.csproj

COPY Api ./Api
COPY --from=frontend /src/Client/signalr-chat-client/dist ./Api/SignalRChat.Api/wwwroot
RUN dotnet build ./Api/SignalRChat.Api/SignalRChat.Api.csproj \
    --configuration Release \
    --no-restore

FROM build AS publish
RUN dotnet publish ./Api/SignalRChat.Api/SignalRChat.Api.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SignalRChat.Api.dll"]
