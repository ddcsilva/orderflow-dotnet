# ========== BUILD STAGE ==========
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia metadados de build (centralized package mgmt + props)
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]

# Copia csprojs (camada cacheável de restore)
COPY ["src/ApiGateway/OrderFlow.Gateway/OrderFlow.Gateway.csproj", "src/ApiGateway/OrderFlow.Gateway/"]
COPY ["src/BuildingBlocks/OrderFlow.SharedKernel/OrderFlow.SharedKernel.csproj", "src/BuildingBlocks/OrderFlow.SharedKernel/"]

RUN dotnet restore "src/ApiGateway/OrderFlow.Gateway/OrderFlow.Gateway.csproj"

# Copia fontes e publica
COPY src/ src/
WORKDIR /src/src/ApiGateway/OrderFlow.Gateway
RUN dotnet publish -c Release -o /app/publish --no-restore

# ========== RUNTIME STAGE ==========
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

LABEL org.opencontainers.image.title="OrderFlow Gateway" \
      org.opencontainers.image.description="YARP Reverse Proxy"

# curl para HEALTHCHECK + non-root user
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r app && useradd -r -g app app
USER app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "OrderFlow.Gateway.dll"]
