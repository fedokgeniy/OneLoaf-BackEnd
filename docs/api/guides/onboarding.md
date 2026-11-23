# Developer onboarding

For developers who work on the backend itself.

## Day 1

1. Install .NET SDK 10, Docker Desktop, Node 20+ (only for docs tooling).
2. Clone, then `dotnet build OneLoaf.slnx` (warnings are errors; central package management and lock files).
3. `docker compose up -d db rabbitmq redis` and `docker compose run --rm migrator` — see [getting started](getting-started.md).
4. Run the tests (Docker must run — integration tests start PostgreSQL, RabbitMQ and Redis containers):

   ```powershell
   dotnet test OneLoaf.slnx -s coverlet.runsettings --collect:"XPlat Code Coverage"
   ```

5. Read [architecture overview](../architecture/overview.md), then [concepts](concepts.md).

## Repository map

| Path | What |
|---|---|
| `src/OneLoaf.Domain` | Entities and enums (shared with the database project) |
| `src/OneLoaf.Infrastructure` | `OneLoafDbContext`, EF configurations, migrations, SQL scripts (functions, triggers) |
| `src/OneLoaf.SharedKernel` | `ICurrentUser`, application exceptions — no dependencies |
| `src/OneLoaf.ServiceDefaults` | Logging, correlation, problem details, OpenAPI, health, metrics, JWT validation |
| `src/OneLoaf.Messaging(.RabbitMq)` | Event contracts, publisher, consumers with retry and dead-letter |
| `src/Services/<Service>/` | `Api → Application → Domain`, `Infrastructure → Application` per service |
| `tests/` | unit tests per service, architecture tests, integration tests (also docs generator and contract tests), k6 |
| `deploy/` | production compose, Caddy, Prometheus, Alertmanager, Loki, Grafana |
| `docs/api` | this documentation |

## Adding an endpoint — checklist

1. Request/response records in `*.Application` with XML docs (`<param>` per property — they become OpenAPI descriptions).
2. A FluentValidation validator for the body/query; business rules in the service (`BusinessRuleException` → 422,
   `NotFoundException` → 404, `ConflictException` → 409).
3. Data access in `*.Infrastructure`, filtered by the current user's id; calculations only through the DB functions.
4. Map the endpoint with `.WithValidation<T>()`, `.WithName("VerbNoun")`, `.WithSummary`, `.WithDescription("US-xx …")`,
   `.ProducesProblem(...)` for every error status, authorization (or `.AllowAnonymous()`), `CancellationToken`.
5. Tests: unit tests for the logic, an integration test for the flow and isolation (`404` for another user).
6. Add calls for the new endpoint to `ApiDocsGenerator` (typical + edge case) and regenerate the docs:

   ```powershell
   $env:ONELOAF_EXPORT_DOCS = "1"; dotnet test tests/OneLoaf.IntegrationTests --filter FullyQualifiedName~ApiDocsGenerator
   ```

7. Lint: `npx @stoplight/spectral-cli@6.15.0 lint "docs/api/reference/*.openapi.json" --fail-severity=warn`.
8. Changelog entry under *Unreleased*; commit with a one-sentence message referencing the story.

## Rules that are easy to miss

- Never edit an existing migration; schema changes are new migrations plus regenerated `db/schema.sql`.
- No secrets in code or `appsettings.json` — environment variables or `dotnet user-secrets`.
- No magic strings: routes, policies, cache keys, routing keys, header names are constants.
- The Application layer never references EF Core, ASP.NET Core, RabbitMQ, Redis or Anthropic types (architecture tests).
- Logs are structured and never contain passwords, tokens, e-mails or chat content.
