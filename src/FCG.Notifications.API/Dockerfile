FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY nuget.config .

COPY FCG.Notifications.API/FCG.Notifications.API.csproj FCG.Notifications.API/
COPY FCG.Notifications.Application/FCG.Notifications.Application.csproj FCG.Notifications.Application/
COPY FCG.Notifications.Infrastructure/FCG.Notifications.Infrastructure.csproj FCG.Notifications.Infrastructure/

RUN --mount=type=secret,id=nuget_token \
    NUGET_AUTH_TOKEN=$(cat /run/secrets/nuget_token) \
    dotnet restore FCG.Notifications.API/FCG.Notifications.API.csproj

COPY . .

RUN dotnet build FCG.Notifications.API/FCG.Notifications.API.csproj \
    -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
RUN dotnet publish FCG.Notifications.API/FCG.Notifications.API.csproj \
    -c $BUILD_CONFIGURATION --no-restore -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FCG.Notifications.API.dll"]
