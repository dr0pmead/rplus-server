# ═══════════════════════════════════════════════════════════════════════════════
# RPlus.Kernel.Gateway — API Gateway (ONLY HTTP entry point)
# ═══════════════════════════════════════════════════════════════════════════════
#
# This is the ONLY service that accepts incoming HTTP traffic.
# All other services communicate via gRPC internally.
# Traefik → Gateway (HTTP/80) → Internal services (gRPC)
#
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
RUN apk add --no-cache icu-libs krb5-libs libc6-compat
COPY Directory.Packages.props ./
COPY Directory.Build.props* ./
COPY nuget.config* ./
COPY LocalPackages/ LocalPackages/
COPY RPlus.SDK/ RPlus.SDK/
COPY RPlusGrpc/ RPlusGrpc/
COPY Directory.Packages.props RPlus.Kernel/
COPY Directory.Build.props* RPlus.Kernel/
COPY LocalPackages/ RPlus.Kernel/LocalPackages/
COPY nuget.config* RPlus.Kernel/
COPY src/ RPlus.Kernel/src/
RUN dotnet restore RPlus.Kernel/src/RPlus.Kernel.Gateway/src/RPlus.Gateway.API/RPlus.Gateway.API.csproj
RUN dotnet build RPlus.SDK/RPlus.SDK.Contracts/RPlus.SDK.Contracts.csproj -c Release --no-restore || true
RUN dotnet build RPlusGrpc/RPlusGrpc.csproj -c Release --no-restore || true
RUN dotnet publish RPlus.Kernel/src/RPlus.Kernel.Gateway/src/RPlus.Gateway.API/RPlus.Gateway.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN addgroup -S rplus && adduser -S rplus -G rplus
RUN apk add --no-cache icu-libs krb5-libs libc6-compat curl
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false ASPNETCORE_ENVIRONMENT=Production

# ┌──────────────────────────────────────────────┐
# │  Gateway is HTTP ONLY — Traefik terminates   │
# │  TLS and forwards plaintext HTTP here.       │
# │  Kestrel uses Http1 (standard HTTP/1.1).     │
# └──────────────────────────────────────────────┘
ENV ASPNETCORE_URLS=http://+:80
ENV Kestrel__EndpointDefaults__Protocols=Http1
EXPOSE 80

HEALTHCHECK --interval=15s --timeout=5s --retries=5 CMD curl -f http://localhost:80/health 2>/dev/null || exit 1
USER rplus
ENTRYPOINT ["dotnet", "RPlus.Gateway.API.dll"]
