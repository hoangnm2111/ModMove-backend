# ModMove.WebApi image. Built by .github/workflows/backend-cd.yml, run by deploy/docker-compose.yml.
# The projects target net8.0, so the 8.0 SDK can publish the WebApi project (the .slnx file needs SDK 9, not used here).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Project files first: restore stays cached until a package changes.
COPY src/ModMove.Domain/ModMove.Domain.csproj src/ModMove.Domain/
COPY src/ModMove.Application/ModMove.Application.csproj src/ModMove.Application/
COPY src/ModMove.Infrastructure/ModMove.Infrastructure.csproj src/ModMove.Infrastructure/
COPY src/ModMove.WebApi/ModMove.WebApi.csproj src/ModMove.WebApi/
RUN dotnet restore src/ModMove.WebApi/ModMove.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/ModMove.WebApi/ModMove.WebApi.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
LABEL org.opencontainers.image.source="https://github.com/hoangnm2111/ModMove-backend"
WORKDIR /app
COPY --from=build /app .
# Non-root user of the official image; Kestrel listens on 8080. Secrets come only from environment variables.
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ModMove.WebApi.dll"]
