# Multi-stage Dockerfile for SentinelLog API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching layer
COPY SentinelLog.slnx ./
COPY src/SentinelLog.Domain/SentinelLog.Domain.csproj src/SentinelLog.Domain/
COPY src/SentinelLog.Application/SentinelLog.Application.csproj src/SentinelLog.Application/
COPY src/SentinelLog.Infrastructure/SentinelLog.Infrastructure.csproj src/SentinelLog.Infrastructure/
COPY src/SentinelLog.Api/SentinelLog.Api.csproj src/SentinelLog.Api/

# Restore dependencies
RUN dotnet restore src/SentinelLog.Api/SentinelLog.Api.csproj

# Copy the remaining source files and compile
COPY src/ src/
RUN dotnet publish src/SentinelLog.Api/SentinelLog.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user for security hardening
RUN groupadd -r sentinellog && useradd -r -g sentinellog sentinellog

COPY --from=build /app/publish .

# Create logs directory with write permissions for non-root user
RUN mkdir -p logs && chown -R sentinellog:sentinellog /app

USER sentinellog

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "SentinelLog.Api.dll"]
