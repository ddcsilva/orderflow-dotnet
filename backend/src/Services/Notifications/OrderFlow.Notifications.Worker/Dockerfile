FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]

COPY ["src/Services/Notifications/OrderFlow.Notifications.Worker/OrderFlow.Notifications.Worker.csproj", "src/Services/Notifications/OrderFlow.Notifications.Worker/"]
COPY ["src/BuildingBlocks/OrderFlow.Contracts/OrderFlow.Contracts.csproj", "src/BuildingBlocks/OrderFlow.Contracts/"]

RUN dotnet restore "src/Services/Notifications/OrderFlow.Notifications.Worker/OrderFlow.Notifications.Worker.csproj"

COPY src/ src/
WORKDIR /src/src/Services/Notifications/OrderFlow.Notifications.Worker
RUN dotnet publish -c Release -o /app/publish --no-restore

# Worker não precisa do ASP.NET runtime — base "runtime" é menor
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

LABEL org.opencontainers.image.title="OrderFlow Notifications Worker"

RUN groupadd -r app && useradd -r -g app app
USER app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OrderFlow.Notifications.Worker.dll"]
