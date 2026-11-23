# Linting, contract tests and generated documentation

| Check | Tool | Where it runs | Fails when |
|---|---|---|---|
| OpenAPI lint | Spectral 6 with `spectral:oas` + house rules in `.spectral.yaml` | `docs` workflow, locally with `npx` | invalid OpenAPI, missing summary, non-PascalCase operation id, unversioned or non-kebab path, non-camelCase property, error response not `application/problem+json`, operation without 2xx |
| Spec drift (contract) | `OpenApiContractTests` (xUnit, integration) | every service workflow | a service's generated OpenAPI differs from `docs/api/reference/*.openapi.json` |
| Behaviour contract | integration tests against PostgreSQL, RabbitMQ, Redis (Testcontainers) | every service workflow | an endpoint's status, body or side effect changes |
| Consumer contract (assistant → Core API) | `AssistantFlowTests`: the assistant's real HTTP client calls the in-process Core API | `assistant` workflow | a Core API response the tools depend on changes shape |
| Event contracts | JSON Schemas generated from the contract records (`schemas/events`) + consumer integration tests | `notification` workflow | a message no longer deserializes or is not delivered |
| Markdown links | lychee (offline) | `docs` workflow | a relative link in `docs/` is broken |
| Examples | `ApiDocsGenerator` records real exchanges | on demand | a scenario call fails unexpectedly |

## Commands

```powershell
npx @stoplight/spectral-cli@6.15.0 lint "docs/api/reference/*.openapi.json" --fail-severity=warn
```

```powershell
dotnet test tests/OneLoaf.IntegrationTests --filter "FullyQualifiedName~OpenApiContractTests"
```

```powershell
$env:ONELOAF_EXPORT_DOCS = "1"; dotnet test tests/OneLoaf.IntegrationTests --filter FullyQualifiedName~ApiDocsGenerator
```

## How the consumer contract works (Pact-like)

Pact records a consumer's expectations and replays them against the provider. Here both sides live in one repository
and run in one test process, so the integration test plays both roles at once: the assistant's production
`CoreApiClient` sends real requests to the real Core API (in-process test server, real database), and the test fails if
a tool cannot parse the answer. Together with the spec drift test this covers what a Pact broker would: the provider
cannot change a response the consumer relies on without a failing build.

## Load test

`tests/load/core-api.js` (k6) ramps to 20 virtual users and enforces p95 < 1 s and < 1 % errors over login, recipe
reads, cost, timeline and the library:

```powershell
k6 run -e BASE_URL=http://localhost:5080 tests/load/core-api.js
```
