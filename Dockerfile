# ═══════════════════════════════════════════════════════════════════════════════
# RPlus.Kernel — Shared Dockerfile for all services
# ═══════════════════════════════════════════════════════════════════════════════
#
# Build context: repository root (f:\RPlus Framework\)
# Usage:
#   docker build -f RPlus.Kernel/Dockerfile \
#     --build-arg SERVICE_CSPROJ=RPlus.Kernel/src/RPlus.Kernel.Auth/src/RPlus.Auth.API/RPlus.Auth.API.csproj \
#     --build-arg SERVICE_DLL=RPlus.Auth.API.dll \
#     -t rplus-kernel-auth .
#
# Architecture:
#   - All services communicate via gRPC internally
#   - Only Gateway exposes HTTP (behind Traefik)
#   - Kestrel binds to Http2 only (except Gateway)
#
# ═══════════════════════════════════════════════════════════════════════════════

ARG DOTNET_SDK=mcr.microsoft.com/dotnet/sdk:10.0-alpine
ARG DOTNET_RUNTIME=mcr.microsoft.com/dotnet/aspnet:10.0-alpine

# ─── Stage 1: Restore ────────────────────────────────────────────────────────
FROM ${DOTNET_SDK} AS restore
WORKDIR /src

# Alpine dependencies for Kafka (librdkafka) and ICU
RUN apk add --no-cache icu-libs krb5-libs libc6-compat

# Copy solution and all csproj files for layer-cached restore
# SDK projects
COPY RPlus.SDK/RPlus.SDK.Core/RPlus.SDK.Core.csproj RPlus.SDK/RPlus.SDK.Core/
COPY RPlus.SDK/RPlus.SDK.Contracts/RPlus.SDK.Contracts.csproj RPlus.SDK/RPlus.SDK.Contracts/
COPY RPlus.SDK/RPlus.SDK.Eventing/RPlus.SDK.Eventing.csproj RPlus.SDK/RPlus.SDK.Eventing/
COPY RPlus.SDK/RPlus.SDK.Infrastructure/RPlus.SDK.Infrastructure.csproj RPlus.SDK/RPlus.SDK.Infrastructure/
COPY RPlus.SDK/RPlus.SDK.Access/RPlus.SDK.Access.csproj RPlus.SDK/RPlus.SDK.Access/
COPY RPlus.SDK/RPlus.SDK.Security/RPlus.SDK.Security.csproj RPlus.SDK/RPlus.SDK.Security/
COPY RPlus.SDK/RPlus.SDK.Integration/RPlus.SDK.Integration.csproj RPlus.SDK/RPlus.SDK.Integration/
COPY RPlus.SDK/RPlus.SDK.Loyalty/RPlus.SDK.Loyalty.csproj RPlus.SDK/RPlus.SDK.Loyalty/
COPY RPlus.SDK/RPlus.SDK.Hunter/RPlus.SDK.Hunter.csproj RPlus.SDK/RPlus.SDK.Hunter/
COPY RPlus.SDK/RPlus.SDK.AI/RPlus.SDK.AI.csproj RPlus.SDK/RPlus.SDK.AI/
COPY RPlusGrpc/RPlusGrpc.csproj RPlusGrpc/

# NuGet config + local packages
COPY Directory.Packages.props ./
COPY Directory.Build.props* ./
COPY nuget.config* ./
COPY LocalPackages/ LocalPackages/

# Kernel service csprojs (glob copy)
COPY RPlus.Kernel/src/ RPlus.Kernel/src/

# Restore target service
ARG SERVICE_CSPROJ
RUN dotnet restore ${SERVICE_CSPROJ}

# ─── Stage 2: Build ──────────────────────────────────────────────────────────
FROM restore AS build

# Copy all source code
COPY RPlus.SDK/ RPlus.SDK/
COPY RPlusGrpc/ RPlusGrpc/
COPY RPlus.Kernel/src/ RPlus.Kernel/src/

# Build SDK dependencies (proto generation + eventing types)
RUN dotnet build RPlus.SDK/RPlus.SDK.Contracts/RPlus.SDK.Contracts.csproj -c Release --no-restore || true
RUN dotnet build RPlus.SDK/RPlus.SDK.Eventing/RPlus.SDK.Eventing.csproj -c Release --no-restore || true
RUN dotnet build RPlus.SDK/RPlus.SDK.Infrastructure/RPlus.SDK.Infrastructure.csproj -c Release --no-restore || true
RUN dotnet build RPlusGrpc/RPlusGrpc.csproj -c Release --no-restore || true

# Publish service
ARG SERVICE_CSPROJ
RUN dotnet publish ${SERVICE_CSPROJ} -c Release -o /app/publish

# ─── Stage 3: Runtime ────────────────────────────────────────────────────────
FROM ${DOTNET_RUNTIME} AS final

# Non-root user for security
RUN addgroup -S rplus && adduser -S rplus -G rplus

WORKDIR /app

# Alpine runtime dependencies
RUN apk add --no-cache icu-libs krb5-libs libc6-compat curl

COPY --from=build /app/publish .

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/health 2>/dev/null || exit 1

USER rplus

ARG SERVICE_DLL=ExecuteService.dll
ENV SERVICE_DLL=${SERVICE_DLL}
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["sh", "-c", "dotnet ${SERVICE_DLL}"]
