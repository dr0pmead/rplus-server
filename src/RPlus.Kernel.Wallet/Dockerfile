# RPlus.Kernel.Wallet — Digital Wallet
# Protocol: gRPC + HTTP (5005), Http1AndHttp2 for backward compat
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
RUN apk add --no-cache icu-libs krb5-libs libc6-compat
COPY Directory.Packages.props ./
COPY Directory.Build.props* ./
COPY nuget.config* ./
COPY LocalPackages/ LocalPackages/
COPY RPlus.SDK/ RPlus.SDK/
COPY RPlusGrpc/ RPlusGrpc/
COPY RPlus.Kernel/src/ RPlus.Kernel/src/
RUN dotnet restore RPlus.Kernel/src/RPlus.Kernel.Wallet/src/RPlus.Wallet.Api/RPlus.Wallet.Api.csproj
RUN dotnet build RPlus.SDK/RPlus.SDK.Contracts/RPlus.SDK.Contracts.csproj -c Release --no-restore || true
RUN dotnet build RPlusGrpc/RPlusGrpc.csproj -c Release --no-restore || true
RUN dotnet publish RPlus.Kernel/src/RPlus.Kernel.Wallet/src/RPlus.Wallet.Api/RPlus.Wallet.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN addgroup -S rplus && adduser -S rplus -G rplus
RUN apk add --no-cache icu-libs krb5-libs libc6-compat curl
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5005
ENV Kestrel__EndpointDefaults__Protocols=Http1AndHttp2
EXPOSE 5005
HEALTHCHECK --interval=30s --timeout=10s --retries=3 CMD curl -f http://localhost:5005/health 2>/dev/null || exit 1
USER rplus
ENTRYPOINT ["dotnet", "RPlus.Wallet.Api.dll"]
