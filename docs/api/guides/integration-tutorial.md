# Integration tutorial: from an account to a baked loaf

This tutorial builds the main flow of a OneLoaf client step by step: authenticate, set up the workspace, calculate,
plan and run a batch, let the assistant plan the next one, and receive reminders. Every request below was taken from
the [recorded examples](../examples), so the shapes are exactly what the services accept and return.

Examples use `curl` with PowerShell line continuation; any HTTP client works. Base URLs are the local ones
(`5080` Core API, `5081` Assistant, `5082` Notification); in production prefix the paths with `/core`, `/assistant`
and `/notify` on your domain.

## Step 1 — Register and get a token

```http
POST /api/v1/auth/register
Content-Type: application/json

{ "email": "baker@example.com", "password": "Rye-Bread-2026!", "displayName": "Baker" }
```

`201 Created` with `{ "userId": "…" }`. The account starts with kitchen settings set to defaults. Passwords are 8–128
characters; an existing e-mail gives `409`.

```http
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "baker@example.com", "password": "Rye-Bread-2026!" }
```

```json
{ "accessToken": "eyJhbGciOiJSUzI1NiIs...", "expiresAt": "2026-11-23T05:08:36+00:00", "user": { "id": "…", "role": "user" } }
```

Send the token on every other call: `Authorization: Bearer <accessToken>`. It is valid for 60 minutes; when a call
returns `401`, log in again. Details: [authentication](authentication.md).

```javascript
// A minimal client used in the next steps (browser or Node 18+).
const core = 'http://localhost:5080/api/v1';
let token;
async function api(method, url, body) {
  const res = await fetch(url, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token && { Authorization: `Bearer ${token}` }) },
    body: body && JSON.stringify(body),
  });
  if (res.status === 204) return null;
  const json = await res.json();
  if (!res.ok) throw Object.assign(new Error(json.title), { problem: json }); // RFC 9457 problem details
  return json;
}
token = (await api('POST', `${core}/auth/login`, { email: 'baker@example.com', password: 'Rye-Bread-2026!' })).accessToken;
```

## Step 2 — Kitchen settings

Costs and timelines depend on the kitchen: oven power and capacity, electricity tariff, packaging, losses, labour rate,
currency and time zone.

```http
PUT /api/v1/kitchen-settings
Authorization: Bearer <token>

{ "ovenPowerKw": 2.5, "ovenCapacityLoaves": 4, "mixerBowlCapacityKg": 4.0, "electricityTariffPerKwh": 0.25,
  "packagingCostPerLoaf": 0.15, "lossRatePercent": 10, "laborRatePerHour": 12, "currency": "EUR", "timeZone": "Europe/Vilnius" }
```

Codes (currencies, units, stage types, ingredient categories, flour types) come from `GET /api/v1/reference/*`; they
change only with a new release, so cache them on the client. An unknown time zone or code is a `400` with the field in
`errors`.

## Step 3 — Flour, ingredients and prices

```http
POST /api/v1/flours
{ "name": "Bread flour T550", "flourTypeCode": "wheat", "proteinPercent": 12.5, "grind": "fine", "notes": null }
```

```http
POST /api/v1/ingredients
{ "name": "Bread flour", "categoryCode": "flour", "unitCode": "g", "flourId": "<flour id>" }
```

Create `Water` (`water`), `Salt` (`salt`) and `Rye starter` (`starter`) the same way, then give each a price:

```http
POST /api/v1/ingredients/<ingredient id>/prices
{ "price": 1.20, "perQuantity": 1000, "currency": "EUR", "effectiveFrom": "2026-10-24" }
```

Prices are a history: a new price with a later `effectiveFrom` does not overwrite the old one, and the cost of a past
bake uses the price valid on that day (`GET …/prices` shows the change percent).

## Step 4 — A recipe

```http
POST /api/v1/recipes
{
  "title": "Country sourdough", "description": "Open crumb, crisp crust.", "yieldLoaves": 1, "loafWeightG": 900, "notes": null,
  "ingredients": [
    { "ingredientId": "<bread flour>", "quantity": 500 },
    { "ingredientId": "<water>", "quantity": 350 },
    { "ingredientId": "<salt>", "quantity": 10 },
    { "ingredientId": "<rye starter>", "quantity": 100 }
  ],
  "stages": [
    { "name": "Mix", "stageTypeCode": "mix", "minDurationMinutes": 15, "maxDurationMinutes": 20, "activeMinutes": 15 },
    { "name": "Bulk fermentation", "stageTypeCode": "bulk_fermentation", "minDurationMinutes": 240, "maxDurationMinutes": 300, "temperatureC": 24 },
    { "name": "Shape", "stageTypeCode": "shape", "minDurationMinutes": 10, "maxDurationMinutes": 15, "activeMinutes": 10 },
    { "name": "Cold proof", "stageTypeCode": "proof", "minDurationMinutes": 600, "maxDurationMinutes": 720, "proofMode": "cold", "temperatureC": 4 },
    { "name": "Bake", "stageTypeCode": "bake", "minDurationMinutes": 40, "maxDurationMinutes": 45, "temperatureC": 240 }
  ]
}
```

`201` returns the full recipe. Ingredients and stages are saved in one transaction. Rules enforced by the database are
returned as `422` with the reason in `detail` — for example `proof_mode is allowed only for proof stages`.

Variants (`POST /recipes/{id}/variants`) inherit everything from the parent and store only differences; see
[concepts](concepts.md#recipes-and-variants).

## Step 5 — Calculate

All numbers are computed by PostgreSQL functions — the clients and the assistant never calculate.

| Question | Request |
|---|---|
| What do 3 loaves cost today? | `GET /api/v1/recipes/{id}/cost?loaves=3` (`asOf=YYYY-MM-DD` for another day) |
| When is it ready if I start at 08:00? | `GET /api/v1/recipes/{id}/timeline?start=2026-12-05T08:00:00%2B02:00` |
| When must I start to have it at 08:00? | `GET /api/v1/recipes/{id}/start-window?readyAt=…` |
| Dough weight, kneading cycles, starter amount | `GET /api/v1/recipes/{id}/production-figures?loaves=3` |
| How do I fill my oven? | `POST /api/v1/calculations/oven-loading` |

Times keep the offset you send (`+02:00` in, `+02:00` out); encode `+` as `%2B` in query strings. A cost with a missing
price has `isComplete: false` and lists the ingredient in `missingPrices` — show it, do not hide the total.

## Step 6 — Plan and run a batch

Preview first (nothing is saved), then create:

```http
POST /api/v1/batches/preview
{ "name": "Weekend bake", "plannedStartAt": "2026-12-05T08:00:00+02:00", "source": "manual",
  "items": [ { "recipeId": "<recipe>", "loaves": 3, "starterId": "<starter>", "remindToFeedStarter": true } ] }
```

`POST /api/v1/batches` with the same body returns `201` with every stage's planned window. The database also schedules
reminders (with `remindToFeedStarter`: feed the starter its time to peak before kneading, so it is ripe in time); they appear in `GET /api/v1/reminders` of the
Notification service.

While baking, drive the stages: `POST /batches/{id}/stages/{stageId}/start`, `/complete` (with the measured
temperature and a note), `/extend` (`{ "minutes": 30 }` when the dough needs more time), `/skip` for a pending stage,
or `PATCH` the planned durations of a pending stage. An action that does not fit the stage's state is `409`.
Finally `POST /batches/{id}/complete` returns `{ "bakeRecordId": "…" }` — the history record with planned vs actual
times. Rate it with `PATCH /bake-records/{id}` (`{ "rating": 4, "comment": "…" }`), attach crumb photos with
`POST /bake-records/{id}/photos` (multipart field `file`), and if the bake went better than the recipe, keep it:
`POST /batches/{id}/save-as-variant` creates a variant with the durations you really used.

## Step 7 — Let the assistant plan the next bake

```http
POST http://localhost:5081/api/v1/conversations
Authorization: Bearer <same token>

{ "title": null }
```

```http
POST http://localhost:5081/api/v1/conversations/<conversation id>/messages
{ "content": "Plan 2 loaves of my Country sourdough for next Saturday morning." }
```

The assistant calls Core API tools **with your token** (it sees only your data) and answers:

```json
{
  "message": { "role": "assistant", "content": "I am an AI assistant. I prepared the Saturday bake of Country sourdough; it costs 7.98 EUR. Please confirm it." },
  "pendingAction": { "actionId": "adfaa175-…", "kind": "create_batch", "expiresAt": "…", "plan": { "totalCost": 7.98, "items": [ … ] } },
  "toolCalls": [ { "name": "find_recipes", "succeeded": true }, { "name": "propose_batch", "succeeded": true } ],
  "unverifiedNumbers": [],
  "usage": { "inputTokens": 4790, "outputTokens": 220, "costUsd": 0.00625 }
}
```

Nothing is saved yet. Show `pendingAction.plan` and let the baker decide:

```http
POST http://localhost:5081/api/v1/conversations/<id>/actions/<actionId>/confirm   → 200 { "batchId": "…" }
POST http://localhost:5081/api/v1/conversations/<id>/actions/<actionId>/decline   → 204
```

A proposal can be confirmed once, within 30 minutes (`409` afterwards). If Claude is unavailable the message endpoint
returns `503` with `Retry-After`; fall back to the planning form of Step 6. More: [assistant guide](assistant.md).

## Step 8 — Push reminders

```javascript
const notify = 'http://localhost:5082/api/v1';
const { publicKey } = await api('GET', `${notify}/push/vapid-public-key`);   // anonymous
const registration = await navigator.serviceWorker.ready;
const subscription = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: publicKey });
await api('POST', `${notify}/push/subscriptions`, subscription.toJSON());       // 201 new device, 200 known device
await api('POST', `${notify}/push/test`);                                         // { "delivered": true }
```

The service worker receives `{ "title", "body", "url", "tag" }` as the push payload. Reminders are delivered at their
due time; a device the push service reports as gone is removed automatically.

## Where to go next

- [Concepts](concepts.md) — recipes, variants, batches, stages, reminders, library.
- [Errors](errors.md) and [best practices](best-practices.md) — what to do with each status code.
- [Endpoint reference](../reference/core-api.md) — all 88 Core API operations with examples.
