# RPlus.Kernel.Loyalty — Loyalty/Gamification Engine
# Protocol: gRPC only (5012 + 5013)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
RUN apk add --no-cache icu-libs krb5-libs libc6-compat
COPY Directory.Packages.props ./
COPY Directory.Build.props* ./
COPY nuget.config* ./
COPY LocalPackages/ LocalPackages/
COPY RPlus.SDK/ RPlus.SDK/
COPY RPlusGrpc/ RPlusGrpc/
COPY src/ src/
RUN dotnet restore src/RPlus.Kernel.Loyalty/RPlus.Loyalty.Api/RPlus.Loyalty.Api.csproj
RUN dotnet build RPlus.SDK/RPlus.SDK.Contracts/RPlus.SDK.Contracts.csproj -c Release --no-restore || true
RUN dotnet build RPlusGrpc/RPlusGrpc.csproj -c Release --no-restore || true
RUN dotnet publish src/RPlus.Kernel.Loyalty/RPlus.Loyalty.Api/RPlus.Loyalty.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN addgroup -S rplus && adduser -S rplus -G rplus
RUN apk add --no-cache icu-libs krb5-libs libc6-compat curl
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS="http://+:5012;http://+:5013"
ENV Kestrel__EndpointDefaults__Protocols=Http2
EXPOSE 5012 5013
HEALTHCHECK --interval=30s --timeout=10s --retries=3 CMD curl -f http://localhost:5012/health 2>/dev/null || exit 1
USER rplus
ENTRYPOINT ["dotnet", "RPlus.Loyalty.Api.dll"]
