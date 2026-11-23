# OneLoaf API documentation

OneLoaf is a web application for managing bread recipes and planning home baking. The backend is three HTTP services
behind one reverse proxy:

| Service | What it does | Local URL | Production URL | Reference |
|---|---|---|---|---|
| **Core API** | Accounts, workspace (flours, ingredients, prices, recipes, starters), calculator, batches, history, shared library, moderation | `http://localhost:5080` | `https://<domain>/core` | [Markdown](reference/core-api.md) · [OpenAPI](reference/core-api.openapi.json) |
| **AI Assistant** | Chat with Claude that plans batches with your own data, bake analysis, usage | `http://localhost:5081` | `https://<domain>/assistant` | [Markdown](reference/assistant.md) · [OpenAPI](reference/assistant.openapi.json) |
| **Notification** | Web Push subscriptions, reminders | `http://localhost:5082` | `https://<domain>/notify` | [Markdown](reference/notification.md) · [OpenAPI](reference/notification.openapi.json) |

Every service also serves its live OpenAPI document at `/openapi/v1.json` and Swagger UI at `/swagger`. For a
three-in-one browsable view of the version-controlled specs, open [`index.html`](index.html) (Redoc) through any static
file server, e.g. `npx http-server docs/api` → <http://localhost:8080>.

## Start here

1. [Getting started](guides/getting-started.md) — run the stack locally and make the first calls (10 minutes).
2. [Integration tutorial](guides/integration-tutorial.md) — step by step from registration to a planned batch,
   an assistant conversation and push reminders.
3. [Authentication](guides/authentication.md) — tokens, roles and how the services trust each other.
4. [Errors](guides/errors.md) — the problem details format and every error type.

## Folder layout

| Folder | Contents | Maintained |
|---|---|---|
| [`reference/`](reference) | OpenAPI 3.1 documents and the Markdown reference of every endpoint (parameters, status codes, request and response examples, data models) | generated |
| [`guides/`](guides) | Getting started, tutorial, concepts, authentication, errors, pagination, rate limits, idempotency, versioning, conventions, best practices, assistant, onboarding | by hand |
| [`architecture/`](architecture) | System context, components, request lifecycle, sequence and dependency diagrams, cross-service contracts | by hand |
| [`examples/`](examples) | Recorded request/response pairs per operation (JSON) and `.http` files for REST clients | generated |
| [`schemas/`](schemas) | JSON Schemas of the HTTP models per service and of the RabbitMQ event messages | generated |
| [`tools/`](tools) | Mock server, sandbox, linting and contract tests | by hand |

Also: [Changelog / release notes](CHANGELOG.md) · [Versioning policy](guides/versioning.md) ·
[Documentation strategy](documentation-strategy.md).

## How the generated parts stay correct

`reference/`, `examples/` and `schemas/` are produced by `ApiDocsGenerator` (an integration test) from the real services
running against PostgreSQL, RabbitMQ and Redis in containers. It calls **every endpoint** with typical and edge-case
requests and records what came back, so the examples are real responses, not hand-written ones.

```powershell
$env:ONELOAF_EXPORT_DOCS = "1"; dotnet test tests/OneLoaf.IntegrationTests --filter FullyQualifiedName~ApiDocsGenerator
```

`OpenApiContractTests` run in CI on every pull request and fail when a service's generated OpenAPI document differs from
the committed one, and the `docs` workflow lints the specs with Spectral (`.spectral.yaml`). See
[documentation strategy](documentation-strategy.md).
