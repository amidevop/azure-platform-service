# Multi-stage Dockerfile for Azure Platform Service
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY AzurePlatformService.sln ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Api/Api.csproj src/Api/
COPY src/Worker/Worker.csproj src/Worker/

# Restore dependencies
RUN dotnet restore AzurePlatformService.sln

# Copy source code
COPY src/ src/

# Build in Release mode
RUN dotnet build AzurePlatformService.sln -c Release --no-restore

# Stage 2: Publish API
FROM build AS publish-api
RUN dotnet publish src/Api/Api.csproj -c Release --no-build -o /app/api

# Stage 3: Publish Worker
FROM build AS publish-worker
RUN dotnet publish src/Worker/Worker.csproj -c Release --no-build -o /app/worker

# Stage 4: Runtime - API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish-api /app/api .
ENTRYPOINT ["dotnet", "AzurePlatformService.Api.dll"]

# Stage 5: Runtime - Worker
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS worker
WORKDIR /app
COPY --from=publish-worker /app/worker .
ENTRYPOINT ["dotnet", "AzurePlatformService.Worker.dll"]
