FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["FCG.Notifications.API/FCG.Notifications.API.csproj", "FCG.Notifications.API/"]
COPY ["FCG.Notifications.Application/FCG.Notifications.Application.csproj", "FCG.Notifications.Application/"]
COPY ["FCG.Notifications.Infrastructure/FCG.Notifications.Infrastructure.csproj", "FCG.Notifications.Infrastructure/"]
RUN dotnet restore "FCG.Notifications.API/FCG.Notifications.API.csproj"

COPY . .

RUN dotnet build "FCG.Notifications.API/FCG.Notifications.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FCG.Notifications.API/FCG.Notifications.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FCG.Notifications.API.dll"]