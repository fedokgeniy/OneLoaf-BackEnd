# Migrator image: applies the EF Core migrations as app_admin and exits. Build from the repository root:
#   docker build -f src/OneLoaf.DbMigrator/Dockerfile -t oneloaf-migrator .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/OneLoaf.Domain/*.csproj src/OneLoaf.Domain/packages.lock.json src/OneLoaf.Domain/
COPY src/OneLoaf.Infrastructure/*.csproj src/OneLoaf.Infrastructure/packages.lock.json src/OneLoaf.Infrastructure/
COPY src/OneLoaf.DbMigrator/*.csproj src/OneLoaf.DbMigrator/packages.lock.json src/OneLoaf.DbMigrator/
RUN dotnet restore src/OneLoaf.DbMigrator/OneLoaf.DbMigrator.csproj --locked-mode

COPY src/OneLoaf.Domain/ src/OneLoaf.Domain/
COPY src/OneLoaf.Infrastructure/ src/OneLoaf.Infrastructure/
COPY src/OneLoaf.DbMigrator/ src/OneLoaf.DbMigrator/
RUN dotnet publish src/OneLoaf.DbMigrator/OneLoaf.DbMigrator.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
USER $APP_UID
ENTRYPOINT ["dotnet", "OneLoaf.DbMigrator.dll"]
