FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]

COPY ["src/Services/Orders/OrderFlow.Orders.Api/OrderFlow.Orders.Api.csproj", "src/Services/Orders/OrderFlow.Orders.Api/"]
COPY ["src/Services/Orders/OrderFlow.Orders.Application/OrderFlow.Orders.Application.csproj", "src/Services/Orders/OrderFlow.Orders.Application/"]
COPY ["src/Services/Orders/OrderFlow.Orders.Domain/OrderFlow.Orders.Domain.csproj", "src/Services/Orders/OrderFlow.Orders.Domain/"]
COPY ["src/Services/Orders/OrderFlow.Orders.Infrastructure/OrderFlow.Orders.Infrastructure.csproj", "src/Services/Orders/OrderFlow.Orders.Infrastructure/"]
COPY ["src/BuildingBlocks/OrderFlow.SharedKernel/OrderFlow.SharedKernel.csproj", "src/BuildingBlocks/OrderFlow.SharedKernel/"]
COPY ["src/BuildingBlocks/OrderFlow.Contracts/OrderFlow.Contracts.csproj", "src/BuildingBlocks/OrderFlow.Contracts/"]

RUN dotnet restore "src/Services/Orders/OrderFlow.Orders.Api/OrderFlow.Orders.Api.csproj"

COPY src/ src/
WORKDIR /src/src/Services/Orders/OrderFlow.Orders.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

LABEL org.opencontainers.image.title="OrderFlow Orders API"

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r app && useradd -r -g app app
USER app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "OrderFlow.Orders.Api.dll"]
