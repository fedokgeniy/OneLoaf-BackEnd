# How the client works with the APIs

The full contract lives in `docs/api/`: OpenAPI 3.1 per service (`reference/*.openapi.json`, readable version in
`reference/*.md`), guides (`guides/authentication.md`, `errors.md`, `pagination.md`, `assistant.md`, `idempotency.md`) and
**recorded real exchanges** for all 102 operations (`examples/<service>/<OperationId>.json`: method, path, request body,
status, response body, typical case + edge cases). This file says how the Blazor client uses them.

## 1. Services and base URLs

| Service | Production (same origin, Caddy strips the prefix) | Development | Named HttpClient |
|---|---|---|---|
| Core API | `/core` | `http://localhost:5080` | `core` |
| AI Assistant | `/assistant` | `http://localhost:5081` | `assistant` |
| Notification | `/notify` | `http://localhost:5082` | `notification` |

`wwwroot/appsettings.json` (committed, no secrets):

```json
{ "Api": { "Core": "/core/", "Assistant": "/assistant/", "Notification": "/notify/" } }
```

`wwwroot/appsettings.Development.json`:

```json
{ "Api": { "Core": "http://localhost:5080/", "Assistant": "http://localhost:5081/", "Notification": "http://localhost:5082/" } }
```

Relative values are resolved against `builder.HostEnvironment.BaseAddress`. Paths in services are written without a
leading slash (`api/v1/recipes`) so they append to the base address.

## 2. Pipeline

```csharp
// Program.cs (sketch)
builder.Services.AddOptions<ServiceEndpoints>().Bind(builder.Configuration.GetSection("Api")).ValidateOnStart();
builder.Services.AddTransient<CorrelationIdHandler>()
                .AddTransient<AuthTokenHandler>()
                .AddTransient<ProblemDetailsHandler>()
                .AddTransient<LoggingHandler>();
foreach (var name in ServiceNames.All)               // "core", "assistant", "notification"
    builder.Services.AddHttpClient(name, (sp, c) => c.BaseAddress = sp.GetRequiredService<ServiceEndpoints>().For(name))
        .AddHttpMessageHandler<CorrelationIdHandler>()
        .AddHttpMessageHandler<AuthTokenHandler>()
        .AddHttpMessageHandler<ProblemDetailsHandler>()
        .AddHttpMessageHandler<LoggingHandler>();
builder.Services.AddScoped<ApiClientFactory>();      // ApiClient per service over IHttpClientFactory
```

| Handler | Does |
|---|---|
| `CorrelationIdHandler` | adds `X-Correlation-ID` (new GUID "N" per request); the same id is shown in error toasts |
| `AuthTokenHandler` | adds `Authorization: Bearer …` from `TokenStore` when a token exists and is not expired |
| `ProblemDetailsHandler` | for non-2xx reads `application/problem+json` into `ProblemDetailsDto`, wraps it in `ApiError` (attached to the response via `HttpRequestOptions`), and on **401** dispatches `SessionExpired` once |
| `LoggingHandler` | `ILogger` line: method, path template, status, elapsed ms (never headers or bodies) |

`ApiClient` (Shared/Api):

```csharp
public sealed class ApiClient(HttpClient http, ILogger<ApiClient> log)
{
    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default);
    public Task<ApiResult<T>> PostAsync<TBody, T>(string path, TBody body, CancellationToken ct = default);
    public Task<ApiResult> PostAsync<TBody>(string path, TBody body, CancellationToken ct = default);   // 204
    public Task<ApiResult<T>> PutAsync<TBody, T>(…); public Task<ApiResult> PatchAsync<TBody>(…);
    public Task<ApiResult> DeleteAsync(string path, CancellationToken ct = default);
    public Task<ApiResult<FileDownload>> DownloadAsync(string path, CancellationToken ct = default);   // CSV export, photos
}
```

- JSON: `System.Text.Json` with `JsonSerializerDefaults.Web` + `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`
  where the API uses snake-case codes (check the examples: `"status": "planned"`, `"kind": "create_batch"`). Use a
  **source-generated** `JsonSerializerContext` per service (trimming-safe, faster).
- Network failure (`HttpRequestException`, `TaskCanceledException` not caused by the caller) → `ApiError.Network`.
- `ApiResult<T>` is `Success(T Value)` or `Failure(ApiError Error)`; no exceptions for HTTP errors.

## 3. Errors → UI

`ApiError` = `Kind` (from the ProblemDetails `type` suffix), `Status`, `Title`, `Detail`, `FieldErrors`
(`IReadOnlyDictionary<string, string[]>`), `CorrelationId`, `RetryAfter`.

| `type` / status | Kind | UI behaviour |
|---|---|---|
| `validation` 400 | `Validation` | `ServerValidator` puts `FieldErrors` into the form's `ValidationMessageStore` (field names are camelCase paths like `items[0].loaves` — map them to the form model with a small path mapper); summary alert on top |
| `bad-request` 400 | `BadRequest` | toast "The request was not accepted" + `detail` (a client bug — log at Warning) |
| `unauthorized` 401 | `Unauthorized` | on login: inline "E-mail or password is wrong"; elsewhere: session cleared, toast "Your session has ended — please sign in again", redirect `/login?returnUrl=…` |
| `forbidden` 403 | `Forbidden` | toast `detail`; for delete account with a wrong password: field error on the password |
| `not-found` 404 | `NotFound` | detail pages render `NotFoundPage` content ("This item does not exist or is not yours"); list actions: toast and reload |
| `conflict` 409 | `Conflict` | show `detail` (duplicate name / stage already running / proposal used) and reload the affected state |
| `business-rule` 422 | `BusinessRule` | inline `OlAlert` with `detail` near the action (e.g. "Set the oven capacity first" with a link to `/settings/kitchen`) |
| `too-many-requests` 429 | `RateLimited` | disable the action for `Retry-After` seconds with a countdown; assistant: "Daily assistant limit reached" from `detail` |
| `service-unavailable` 503 | `Unavailable` | assistant: error alert + **"Use the manual form"** link to `/batches/new` (risk R1, UC-01 A3); push: explain; retry button after `Retry-After` |
| `internal` 500 | `Server` | toast "Something went wrong on our side" + correlation id (copy button) |
| network | `Network` | `Connectivity` → offline banner; writes disabled; reads from the service-worker cache where available |

Global last net: `<ErrorBoundary>` in `MainLayout` catches rendering exceptions, logs them with `ILogger`, shows a
recoverable error panel; `ErrorBoundary.Recover()` on navigation.

## 4. Contracts (data types)

The OpenAPI documents are **3.1** (type arrays for nullable). Code generators for C# handle 3.1 unevenly, so the request
and response types are **hand-written C# records** in `OneLoaf.Web.Shared/Contracts/{Core,Assistant,Notification}/`,
one file per resource, with nullable reference types matching the schema's `required` list.

Correctness is guaranteed by **contract tests**: `tests/OneLoaf.Web.UnitTests/Contracts/RecordedExamplesTests.cs` loads
every `docs/api/examples/**/*.json`, takes each `responseBody` of a 2xx case and deserializes it into the mapped record
type with `JsonSerializerOptions { UnmappedMemberHandling = Disallow }` and the source-generated context, then
re-serializes and compares the significant fields. A missing or misspelled property fails the test.

```csharp
public sealed record RecipeCostDto(
    Guid RecipeId, int Loaves, DateOnly AsOf, string Currency, bool IsComplete,
    IReadOnlyList<CostLineDto> Lines, decimal Total /* … exactly as in GetRecipeCost.json */);

public sealed record CostLineDto(int LineNo, string Type, Guid? IngredientId, string Description,
    decimal? Quantity, string? Unit, decimal? UnitPrice, decimal Amount /* … */);
```

- Money and quantities are `decimal` (the API sends decimals with many digits); dates `DateOnly`, instants
  `DateTimeOffset`, durations in minutes `int`.
- Codes from reference endpoints (`unit`, `stageTypeCode`, `currency`) stay `string` and are resolved against cached
  reference data; closed sets the client branches on (`batch.status`, stage `status`, `pendingAction.kind`, cost line
  `type`) become enums with the snake-case converter and an `Unknown` fallback member.
- The assistant's `pendingAction.plan` is a `JsonElement` in the OpenAPI document; for `kind = create_batch` it has the
  shape of `BatchDto` (see `examples/assistant/SendMessage.json`) and is deserialized into `BatchDto` immediately in
  `ConversationsApi` — the only `JsonElement` in the client.
- Paged lists: `PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`.
- Form models (`Features/*/Models/*Form.cs`) are separate mutable classes with DataAnnotations; mapping form ↔ request
  record is explicit (`ToRequest()`), never by reflection.

## 5. Typed API services (one per resource)

```csharp
public sealed class CalculatorApi(ApiClientFactory f)
{
    private readonly ApiClient _core = f.Core;
    public Task<ApiResult<RecipeCostDto>> GetCostAsync(Guid id, int loaves, DateOnly? asOf, CancellationToken ct) =>
        _core.GetAsync<RecipeCostDto>(Url.Build($"api/v1/recipes/{id}/cost", ("loaves", loaves), ("asOf", asOf)), ct);
}
```

| Service (feature) | Endpoints |
|---|---|
| `AccountApi` (Host) | `auth/register`, `auth/login`, `auth/logout`, `me` (GET, DELETE), `export/recipes.csv`, `export/bake-records.csv` |
| `ReferenceApi` (Shared, cached for the session) | `reference/units`, `currencies`, `stage-types`, `ingredient-categories`, `flour-types` |
| `RecipesApi` (Recipes) | `recipes` CRUD, `archive`, `unarchive`, `variants`, `comparison`, `scaled` |
| `CalculatorApi` (Recipes) | `cost`, `timeline`, `start-window`, `production-figures`, `calculations/oven-loading` |
| `IngredientsApi`, `FloursApi`, `KitchenApi` (Recipes) | `ingredients` + `prices`, `stock-movements`, `purchases`, `stock-adjustments`, `flours`, `kitchen-settings`, `warnings` |
| `BatchesApi` (Production) | `batches` list/get/create/update/preview/cancel/complete/save-as-variant/kneading-plan, stage start/complete/skip/extend/patch/reset |
| `StartersApi` (Production) | `starters` CRUD, `feedings` |
| `RemindersApi` (Shared — used by Home and Calendar) | notification `reminders` |
| `BakeRecordsApi` (History) | `bake-records` list/get/patch/statistics/stage-log/photos/analysis (PUT) |
| `InsightsApi` (History) | assistant `bake-records/{id}/analysis` (POST), `usage/me` |
| `ConversationsApi` (Assistant) | `conversations` list/create/delete, `messages` get/post, `actions/{id}/confirm`, `decline` |
| `LibraryApi` (Library) | `library` search/get/comments/reactions/copy/reports/unpublish, `recipes/{id}/publish` |
| `ModerationApi` (Library) | `admin/reports`, `resolve`, `admin/library/{id}/remove` |
| `PushApi` (Host settings) | `push/vapid-public-key`, `push/subscriptions`, `push/test` |

## 6. Rules that matter in the UI

- **No client arithmetic on business numbers** (rule 1). Totals, per-loaf cost, ready windows and cycles are displayed as
  returned. Formatting only: `1 500 g` (narrow no-break space thousands), `6.05 EUR`, `3–4 h`, `02:00–04:00` with
  "next day" when the date differs.
- Timestamps from the API are UTC (`+00:00`); display in the user's time zone from kitchen settings (`timeZone`), falling
  back to the browser's.
- No `Idempotency-Key`: disable a submit button while its request runs; after a timeout on create, reload the list
  before allowing a retry (see `guides/idempotency.md`).
- Assistant: first answer states it is an AI (LR-10) — rendered as returned; show `disclaimer` under the message;
  `unverifiedNumbers` non-empty → warning "Some numbers could not be checked against the calculator".
- Assistant calls can take up to ~10 s: show a typing indicator, keep the composer enabled for editing, cancel with
  a `CancellationToken` when the user leaves the page.
- Mock server for work without the backend: `docs/api/tools/mock-server.md` (Prism). E2E tests do not use Prism; they
  serve recorded examples through Playwright routing (`testing.md`).
