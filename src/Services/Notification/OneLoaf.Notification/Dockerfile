# Notification image. Build from the repository root:
#   docker build -f src/Services/Notification/OneLoaf.Notification/Dockerfile -t oneloaf-notification .
# Configuration comes only from environment variables (see .env.example); the image contains no secrets.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files and lock files only, so the layer is cached while the code changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/OneLoaf.Domain/*.csproj src/OneLoaf.Domain/packages.lock.json src/OneLoaf.Domain/
COPY src/OneLoaf.SharedKernel/*.csproj src/OneLoaf.SharedKernel/packages.lock.json src/OneLoaf.SharedKernel/
COPY src/OneLoaf.Infrastructure/*.csproj src/OneLoaf.Infrastructure/packages.lock.json src/OneLoaf.Infrastructure/
COPY src/OneLoaf.ServiceDefaults/*.csproj src/OneLoaf.ServiceDefaults/packages.lock.json src/OneLoaf.ServiceDefaults/
COPY src/OneLoaf.Messaging/*.csproj src/OneLoaf.Messaging/packages.lock.json src/OneLoaf.Messaging/
COPY src/OneLoaf.Messaging.RabbitMq/*.csproj src/OneLoaf.Messaging.RabbitMq/packages.lock.json src/OneLoaf.Messaging.RabbitMq/
COPY src/Services/Notification/OneLoaf.Notification.Application/*.csproj src/Services/Notification/OneLoaf.Notification.Application/packages.lock.json src/Services/Notification/OneLoaf.Notification.Application/
COPY src/Services/Notification/OneLoaf.Notification.Infrastructure/*.csproj src/Services/Notification/OneLoaf.Notification.Infrastructure/packages.lock.json src/Services/Notification/OneLoaf.Notification.Infrastructure/
COPY src/Services/Notification/OneLoaf.Notification/*.csproj src/Services/Notification/OneLoaf.Notification/packages.lock.json src/Services/Notification/OneLoaf.Notification/
RUN dotnet restore src/Services/Notification/OneLoaf.Notification/OneLoaf.Notification.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/Services/Notification/OneLoaf.Notification/OneLoaf.Notification.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 8080

# Non-root user provided by the .NET images.
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "OneLoaf.Notification.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "OneLoaf.Notification.dll"]
