# OneLoaf

The bachelor project **"AI-assisted web application for managing recipes and assisting with homemade baking"
(OneLoaf)**: a private workspace for sourdough bakers with recipes and variants, ingredient prices and cost, production
batches, starters and reminders, baking history and an AI assistant that calls backend tools instead of doing arithmetic.

## Parts

- **Web client** - Blazor WebAssembly SPA + PWA (`src/Web`): recipes and the calculator, batches, calendar, starters,
  baking history, the assistant chat with plan confirmation, the public library, settings; installable and readable
  offline. History, Assistant and Library are downloaded on first use.
- **Core API** - all business data of the workspace and the shared library, calculations through the database functions,
  authentication (issues JWT).
- **AI Assistant** - chat with Claude and tool calling against the Core API on behalf of the user, plan confirmation.
- **Notification** - delivery of reminders and event notifications by Web Push and e-mail, driven by RabbitMQ.

## Stack

.NET 10 / ASP.NET Core, Blazor WebAssembly + Fluxor (client), EF Core 10 + Npgsql, PostgreSQL 16, RabbitMQ, Redis, Docker, GitHub Actions,
GitHub Container Registry, Anthropic Claude API.

## Documentation

- [API documentation](docs/api/README.md) - getting started, integration tutorial, reference of all endpoints with
  examples, authentication, errors, architecture and cross-service contracts
- [Database documentation](docs/db/README.md) - schema, migrations, tests, how to change the schema
- [Frontend documentation](docs/frontend/README.md) - architecture, routes and stories, API client, design system,
  decisions, testing

## Getting started

Requires Docker and the .NET 10 SDK. Passwords and keys come from `.env`, which is excluded from Git.

```bash
cp .env.example .env                # set the passwords and the JWT keys (generation command inside)
docker compose up -d --build        # PostgreSQL, migrator, RabbitMQ, Redis and the three services
```

The Core API listens on `http://localhost:5080`, the AI Assistant on `5081`, Notification on `5082` (Swagger UI at
`/swagger`, health at `/health/ready` on each). Production runs behind Caddy with `deploy/docker-compose.prod.yml`.
Database only: `docker compose up -d db && docker compose run --rm migrator`; more options in
[docs/db/README.md](docs/db/README.md#getting-started).

Web client against the services above: `dotnet run --project src/Web/OneLoaf.Web` (http://localhost:5090), or the
container with `docker compose --profile web up -d web` (http://localhost:8090).

Tests (integration tests need a running Docker engine):

```bash
dotnet test OneLoaf.slnx --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

Web end-to-end tests run against a published client (Playwright Chromium, API answers from `docs/api/examples`):

```bash
dotnet publish src/Web/OneLoaf.Web -c Release -o artifacts/web
pwsh tests/OneLoaf.Web.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
ONELOAF_E2E_WEB_ROOT=artifacts/web/wwwroot dotnet test tests/OneLoaf.Web.E2ETests
```

## Status

The web client and all three services are implemented (`/api/v1`, see the [changelog](docs/api/CHANGELOG.md)). The database submitted state is
tagged `db-submitted`. Deployment to a server is prepared (`deploy/`) but not yet performed.
