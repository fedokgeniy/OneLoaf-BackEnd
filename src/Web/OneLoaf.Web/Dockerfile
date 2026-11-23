# Web client image (Blazor WebAssembly SPA on nginx). Build from the repository root:
#   docker build -f src/Web/OneLoaf.Web/Dockerfile -t oneloaf-web .
# The image holds only static files: no secrets, no server-side code. API base URLs are relative (/core/, /assistant/,
# /notify/) and resolved against the origin, so the same image works behind any domain.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files and lock files only, so the layer is cached while the code changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Web/OneLoaf.Web/*.csproj src/Web/OneLoaf.Web/packages.lock.json src/Web/OneLoaf.Web/
COPY src/Web/OneLoaf.Web.Shared/*.csproj src/Web/OneLoaf.Web.Shared/packages.lock.json src/Web/OneLoaf.Web.Shared/
COPY src/Web/OneLoaf.Web.Features.Recipes/*.csproj src/Web/OneLoaf.Web.Features.Recipes/packages.lock.json src/Web/OneLoaf.Web.Features.Recipes/
COPY src/Web/OneLoaf.Web.Features.Production/*.csproj src/Web/OneLoaf.Web.Features.Production/packages.lock.json src/Web/OneLoaf.Web.Features.Production/
COPY src/Web/OneLoaf.Web.Features.History/*.csproj src/Web/OneLoaf.Web.Features.History/packages.lock.json src/Web/OneLoaf.Web.Features.History/
COPY src/Web/OneLoaf.Web.Features.Assistant/*.csproj src/Web/OneLoaf.Web.Features.Assistant/packages.lock.json src/Web/OneLoaf.Web.Features.Assistant/
COPY src/Web/OneLoaf.Web.Features.Library/*.csproj src/Web/OneLoaf.Web.Features.Library/packages.lock.json src/Web/OneLoaf.Web.Features.Library/
RUN dotnet restore src/Web/OneLoaf.Web/OneLoaf.Web.csproj --locked-mode

COPY src/Web/ src/Web/
RUN dotnet publish src/Web/OneLoaf.Web/OneLoaf.Web.csproj --configuration Release --no-restore --output /app

# Official nginx image variant that runs as the non-root user nginx (uid 101) and listens on 8080.
FROM nginxinc/nginx-unprivileged:alpine AS runtime
COPY src/Web/OneLoaf.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/wwwroot /usr/share/nginx/html
USER 101
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD ["wget", "-q", "--spider", "http://127.0.0.1:8080/index.html"]
