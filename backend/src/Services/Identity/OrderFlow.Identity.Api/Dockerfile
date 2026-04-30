FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]

COPY ["src/Services/Identity/OrderFlow.Identity.Api/OrderFlow.Identity.Api.csproj", "src/Services/Identity/OrderFlow.Identity.Api/"]
COPY ["src/BuildingBlocks/OrderFlow.SharedKernel/OrderFlow.SharedKernel.csproj", "src/BuildingBlocks/OrderFlow.SharedKernel/"]

RUN dotnet restore "src/Services/Identity/OrderFlow.Identity.Api/OrderFlow.Identity.Api.csproj"

COPY src/ src/
WORKDIR /src/src/Services/Identity/OrderFlow.Identity.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

LABEL org.opencontainers.image.title="OrderFlow Identity API"

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

ENTRYPOINT ["dotnet", "OrderFlow.Identity.Api.dll"]
