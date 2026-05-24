# syntax=docker/dockerfile:1.7
# Imagem do SDK .NET 10 para build self-contained
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaura dependências antes do build (otimiza camadas Docker)
COPY nuget.config ./
COPY src/FCG.Notifications.Application/FCG.Notifications.Application.csproj  FCG.Notifications.Application/
COPY src/FCG.Notifications.Infrastructure/FCG.Notifications.Infrastructure.csproj FCG.Notifications.Infrastructure/
COPY src/FCG.Notifications.Function/FCG.Notifications.Function.csproj         FCG.Notifications.Function/

# Token do GitHub Packages para resolver os pacotes FCG.* (passado via --secret id=nuget_auth_token)
RUN --mount=type=secret,id=nuget_auth_token \
    NUGET_AUTH_TOKEN="$(cat /run/secrets/nuget_auth_token 2>/dev/null || echo '')" \
    dotnet restore FCG.Notifications.Function/FCG.Notifications.Function.csproj

# Copia o restante e publica self-contained para Linux x64
COPY src/FCG.Notifications.Application/  FCG.Notifications.Application/
COPY src/FCG.Notifications.Infrastructure/ FCG.Notifications.Infrastructure/
COPY src/FCG.Notifications.Function/      FCG.Notifications.Function/

RUN dotnet publish FCG.Notifications.Function/FCG.Notifications.Function.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --no-restore \
    -p:PublishReadyToRun=true \
    -o /app/publish

# Runtime image (custom runtime base do AL2023; o executável bootstrap implementa o Lambda Runtime API)
FROM public.ecr.aws/lambda/provided:al2023

COPY --from=build /app/publish/ ${LAMBDA_RUNTIME_DIR}/

CMD ["bootstrap"]
