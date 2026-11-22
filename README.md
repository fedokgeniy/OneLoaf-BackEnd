# OneLoaf - backend

Backend of the bachelor project **"AI-assisted web application for managing recipes and assisting with homemade baking"
(OneLoaf)**: a private workspace for sourdough bakers with recipes and variants, ingredient prices and cost, production
batches, starters and reminders, baking history and an AI assistant that calls backend tools instead of doing arithmetic.

## Services

- **Core API** - all business data of the workspace and the shared library, calculations through the database functions,
  authentication (issues JWT).
- **AI Assistant** - chat with Claude and tool calling against the Core API on behalf of the user, plan confirmation.
- **Notification** - delivery of reminders and event notifications by Web Push and e-mail, driven by RabbitMQ.

## Stack

.NET 10 / ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL 16, RabbitMQ, Redis, Docker, GitHub Actions,
GitHub Container Registry, Anthropic Claude API.

## Documentation

- [Database documentation](docs/db/README.md) - schema, migrations, tests, how to change the schema

## Getting started

Requires Docker and the .NET 10 SDK. Passwords come from `.env`, which is excluded from Git.

```bash
cp .env.example .env                # set the passwords
docker compose up -d db             # PostgreSQL 16; the first start creates the roles and the database
docker compose run --rm migrator    # applies the migrations as app_admin
```

More options (own PostgreSQL, SQL only, full verification): [docs/db/README.md](docs/db/README.md#getting-started).

## Status: in development

The database is complete (submitted state tagged `db-submitted`). The services are being built.
