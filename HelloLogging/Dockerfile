# syntax=docker/dockerfile:1
# Build from the repo root so NuGet.config is inside the build context:
#   docker build -f HelloLogging/Dockerfile -t hellologging-api:local .

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the files that affect restore first, so source edits reuse the cached restore layer.
COPY NuGet.config .
COPY HelloLogging/HelloLogging.csproj HelloLogging/
RUN dotnet restore HelloLogging/HelloLogging.csproj

COPY HelloLogging/ HelloLogging/
RUN dotnet publish HelloLogging/HelloLogging.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The aspnet image listens on 8080 (ASPNETCORE_HTTP_PORTS=8080) and ships a non-root 'app' user.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "HelloLogging.dll"]
