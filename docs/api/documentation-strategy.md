# Documentation strategy

## Goals

1. **Correct by construction** — the reference, examples and schemas are generated from the running code and checked in
   CI, so they cannot silently drift from the implementation.
2. **Usable at three depths** — a 10-minute start, a task-oriented tutorial, and a complete reference.
3. **Version-controlled and reviewable** — everything is plain text in the repository, changes appear in pull requests.

## Audiences

| Reader | Entry point |
|---|---|
| Frontend / integration developer | [Getting started](guides/getting-started.md) → [tutorial](guides/integration-tutorial.md) → [reference](reference/core-api.md) |
| Backend developer joining the project | [Onboarding](guides/onboarding.md) → [architecture](architecture/overview.md) |
| Operator | [Architecture: deployment, observability](architecture/overview.md#deployment), [sandbox](tools/sandbox.md) |
| Reviewer / examiner | [README](README.md), this page, [quality checks](tools/quality.md) |

## Tools

| Need | Tool | Why |
|---|---|---|
| OpenAPI generation | `Microsoft.AspNetCore.OpenApi` (OpenAPI 3.1) + transformers in `OneLoaf.ServiceDefaults.OpenApi` | first-party, no extra package; XML docs → descriptions, bearer scheme, admin marking |
| Interactive docs | Swagger UI on every service (`/swagger`), Redoc page (`index.html`) | try requests with a token; readable three-service overview |
| Reference pages | `ApiDocsGenerator` → Markdown | renders on GitHub, searchable, diffable |
| Examples | recorded by `ApiDocsGenerator` from real calls | real responses for typical and edge cases |
| Lint | Spectral (`.spectral.yaml`) | standard OpenAPI rules + house conventions |
| Contract tests | `OpenApiContractTests`, integration tests | fail the build on undocumented changes |
| Diagrams | Mermaid in Markdown | rendered by GitHub, versioned as text |
| Mock server | Prism | contract-based mock for frontend work |
| Link check | lychee (offline mode) | broken relative links fail the docs workflow |

## Standards

- **Spec:** OpenAPI 3.1, one document per service, `info.version` = URL major version.
- **Errors:** RFC 9457 problem details everywhere ([taxonomy](guides/errors.md)).
- **Naming:** see [conventions](guides/conventions.md) — kebab-case paths, camelCase JSON, snake_case enums, PascalCase
  operation ids, units in field names.
- **Writing:** short sentences, present tense, second person in guides; every example is runnable and taken from a
  recorded exchange; no planning notes or internal tickets in public text (user story ids such as `US-14` are allowed
  in endpoint descriptions to trace requirements).
- **Formatting:** Markdown with fenced code blocks tagged by language (`http`, `json`, `powershell`, `javascript`),
  tables for parameters and statuses, LF line endings, generated files marked "do not edit".
- **File layout:** `reference/` (generated), `guides/`, `architecture/`, `examples/` (generated), `schemas/`
  (generated), `tools/`.

## Versioning of the documentation

The docs live next to the code and are versioned with it: the documentation of a release is the `docs/api` folder at
that release's tag. The API version scheme and deprecation policy are in [versioning](guides/versioning.md); every
user-visible change gets a [changelog](CHANGELOG.md) entry in the same pull request.

## Workflow for a change

1. Change the code and the XML docs of the touched records.
2. Regenerate (`ONELOAF_EXPORT_DOCS=1 …ApiDocsGenerator`) and review the diff of `reference/`, `examples/`, `schemas/`.
3. Update the affected guide and the changelog.
4. CI: Spectral, contract tests, link check.

## Known gaps

| Gap | Impact | Plan / mitigation |
|---|---|---|
| OpenAPI operations declare only the error statuses listed with `ProducesProblem`; some rare ones (e.g. `404` for a recipe id inside a batch body) are documented in the endpoint text and the error guide, not in the `responses` map | a generated client may not have a typed branch for them | all errors share one problem details schema, so generic handling works |
| No `Idempotency-Key` support for creates | retries of `POST` can create duplicates | documented read-back strategy ([idempotency](guides/idempotency.md)) |
| No server-sent streaming of assistant answers | a turn with tool calls takes several seconds | clients show progress; streaming is a possible extension |
| Examples contain ids and timestamps of the generation run | values differ between regenerations | they are illustrative; the shapes are checked |
| Photo upload for bake records (US-51) and saving a diverged batch as a variant (US-25) are not implemented | no endpoints | optional ("could") stories |
| Production URLs (`<domain>`) are placeholders | — | filled in when the server is deployed |
| No published SDKs | clients write their own HTTP layer | the OpenAPI documents work with any generator (e.g. NSwag, openapi-generator) |
