# RPlus.WalletAdapter — Kafka-to-Wallet gRPC Bridge
# Protocol: Kafka consumer only (no exposed ports)
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
RUN dotnet restore src/RPlus.WalletAdapter/RPlus.WalletAdapter/RPlus.WalletAdapter.csproj
RUN dotnet build RPlus.SDK/RPlus.SDK.Contracts/RPlus.SDK.Contracts.csproj -c Release --no-restore || true
RUN dotnet build RPlusGrpc/RPlusGrpc.csproj -c Release --no-restore || true
RUN dotnet publish src/RPlus.WalletAdapter/RPlus.WalletAdapter/RPlus.WalletAdapter.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN addgroup -S rplus && adduser -S rplus -G rplus
RUN apk add --no-cache icu-libs krb5-libs libc6-compat
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false ASPNETCORE_ENVIRONMENT=Production
# No ports — pure Kafka consumer
USER rplus
ENTRYPOINT ["dotnet", "ExecuteService.dll"]
