# Changelog and release notes

All notable changes to the OneLoaf backend API. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [semantic versioning](guides/versioning.md). Dates are UTC.

## [Unreleased]

### Added

- **Session refresh** (US-02): the sign-in sets a rotating refresh token as an `HttpOnly`, `SameSite=Strict` cookie;
  `POST /auth/refresh` exchanges it for a new access token. A token used again more than 30 seconds after its exchange
  revokes the whole sign-in. CORS allows credentials for the configured origins.
- **Ingredient stock**: a ledger of purchases (packs × pack size), batch consumption and returns, starter feedings and
  corrections; `POST /ingredients/{id}/records` stores a price and a purchase in one request (or only the price),
  `POST /ingredients/{id}/stock-adjustments`, `GET /ingredients/{id}/stock-movements`; ingredients show their stock and
  a low-stock flag. A batch preview reports shortages at its planned start with two offers (fewer loaves, scaled dough).
- **Prices can be corrected**: `PUT` and `DELETE /ingredients/{id}/prices/{priceId}`; one price per day.
- **Starter as an ingredient**: every starter has an ingredient (`ingredientId`), is fed from flour and water
  ingredients, and its usable share (`usableShare`, default two thirds) reaches the stock when it is ripe.
- **Photos of ingredients and recipes**: `PUT`, `GET` and `DELETE …/photo` (one photo each, same checks as crumb photos).
- **Recipe drafts**: `POST /recipes/drafts` creates a recipe together with its missing ingredients in one transaction;
  the assistant proposes such a recipe (`create_recipe` action) and saves it only after the confirmation.
- **Save a batch as a variant** (US-25): `POST /batches/{id}/save-as-variant` creates a variant of the batch item's recipe
  with the measured durations of finished stages and the changed or extended durations of the others; `422` when the
  batch followed the recipe.
- **Crumb photos** (US-51): upload (`multipart/form-data`, JPEG/PNG/WebP checked by file signature, up to 5 MB and 10 per
  record), list, download and delete; bake records show their photos and the list shows `photoCount`. Files live on a
  volume (`Storage__PhotosPath`), the database keeps the key.

### Changed

- `POST /auth/logout` no longer needs an access token: it revokes the refresh tokens of the sign-in and removes the
  cookie, so signing out works with an expired access token.
- Cancelling or deleting a batch takes `ingredientsUsed`: when false, the ingredients return to the stock.
- A starter requires its time to peak (`riseHours`).

### Fixed

- Security workflow: existing Trivy action tag, gitleaks allowlist for public example keys and placeholders.

## [1.0.0] — 2026-11-23

First complete version of all three services under `/api/v1`. Pull requests: phases 1–5.

### Added

- **Notification service**: push subscriptions (`POST/DELETE /push/subscriptions`, upsert by endpoint), VAPID public
  key, test push, `GET /reminders`; reminder pipeline (scheduler with `FOR UPDATE SKIP LOCKED` and Redis in-flight
  markers, RabbitMQ dispatcher, Web Push and e-mail channels, retry queue and dead-letter queue); pushes for planned and
  completed batches and starter feedings.
- **Shared library** (US-39–42): publish snapshots, anonymous search and detail, comments, reactions, copy into the
  workspace, reports; **moderation** (LR-11) for administrators.
- **Oven loading** suggestion (US-19) and **CSV export** of recipes and bake records (US-50).
- Redis-backed HybridCache in the Core API for reference data, calculations (invalidated on any write of the user) and
  library pages.
- OpenAPI documents with descriptions of every model and parameter, admin-only operations marked; relative server URL
  so Swagger UI works behind the reverse proxy prefix.
- Observability stack for production: Prometheus alerts, Alertmanager → Telegram, Loki, provisioned Grafana dashboard,
  metrics `oneloaf_reminders_*`, `oneloaf_ai_*`, `oneloaf_cache_*`.
- Security workflow (gitleaks, Trivy for dependencies, configuration and images) and a k6 load test.

### Changed

- Enum query parameters (`status` of batches, reports and reminders) take the same snake_case values as JSON
  (`?status=in_progress`); unknown values are a `400` listing the allowed ones. Previously such filters failed to bind.
- JSON numbers are strict: numeric strings (`"3"`) are rejected with `400`.
- Problem details of framework-generated errors (401, 403, unmatched routes, binding failures) use the OneLoaf problem
  types instead of the generic RFC 9110 links.
- Business rules enforced by database triggers return their message in `detail` (status `422` or `409`).

## [0.3.0] — 2026-11-22 (Phase 3)

### Added

- **AI Assistant service** (US-34–38): conversations, messages with Claude tool calling over the Core API with the
  baker's token, batch proposals confirmed or declined by the baker, number provenance check, bake analysis, usage and
  budgets (per-user daily limits, global monthly budget, 10 messages/minute).

## [0.2.0] — 2026-11-21 (Phase 2)

### Added

- Batches with stage execution (start, complete, skip, extend, change durations, reset), kneading plan, starters and
  feedings, baking history with ratings, stage log and statistics, RabbitMQ events `batch.planned`, `batch.completed`,
  `starter.fed`, `account.deleted`.

## [0.1.0] — 2026-11-20 (Phases 0–1)

### Added

- Core API foundation: registration, login (RS256 JWT), logout, profile, account deletion; problem details, correlation
  ids, health checks, metrics, Swagger UI.
- Workspace: kitchen settings, flours, ingredients with price history, recipes with variants, scaling and comparison,
  the calculator (cost, timeline, start window, production figures) backed by the database functions, warnings.
