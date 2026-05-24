# syntax=docker/dockerfile:1.7
# Multi-stage: build .NET 10 self-contained + roda no host oficial do Azure Functions v4.
# O Functions Host na imagem base é independente da versao do worker .NET — ele lanca
# o executavel apontado por worker.config.json, que aqui sera o publish self-contained.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY src/FCG.Notifications.Application/FCG.Notifications.Application.csproj   FCG.Notifications.Application/
COPY src/FCG.Notifications.Infrastructure/FCG.Notifications.Infrastructure.csproj FCG.Notifications.Infrastructure/
COPY src/FCG.Notifications.Function/FCG.Notifications.Function.csproj          FCG.Notifications.Function/

RUN --mount=type=secret,id=nuget_auth_token \
    NUGET_AUTH_TOKEN="$(cat /run/secrets/nuget_auth_token 2>/dev/null || echo '')" \
    dotnet restore FCG.Notifications.Function/FCG.Notifications.Function.csproj

COPY src/FCG.Notifications.Application/   FCG.Notifications.Application/
COPY src/FCG.Notifications.Infrastructure/ FCG.Notifications.Infrastructure/
COPY src/FCG.Notifications.Function/      FCG.Notifications.Function/

RUN dotnet publish FCG.Notifications.Function/FCG.Notifications.Function.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --no-restore \
    -o /app/publish

# Imagem do Azure Functions Host v4 (mais recente disponivel para .NET isolated)
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

COPY --from=build /app/publish /home/site/wwwroot
