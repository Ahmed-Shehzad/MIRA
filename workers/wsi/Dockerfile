FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY backend/*.csproj backend/
COPY workers/wsi/*.csproj workers/wsi/
RUN dotnet restore workers/wsi/HiveOrders.WsiWorker.csproj

COPY backend/ backend/
COPY workers/wsi/ workers/wsi/
RUN dotnet publish workers/wsi/HiveOrders.WsiWorker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "HiveOrders.WsiWorker.dll"]
