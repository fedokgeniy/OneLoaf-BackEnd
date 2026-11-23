# Best practices for clients

## Requests

- **Send `X-Correlation-ID`** (a UUID per user action). The services return it, put it in every log line, pass it to
  other services and into events. Show it in error dialogs ("Support code").
- **Send instants with an offset** (`2026-12-05T08:00:00+02:00`) and URL-encode `+` as `%2B` in query strings.
- **Use snake_case enum values** in bodies and query strings (`in_progress`, `want_to_bake`).
- **Send numbers as numbers.** `"loaves": "3"` is rejected.
- **Preview before you write**: `POST /batches/preview` returns the full plan without saving anything.

## Responses

- **Branch on problem `type`, not on `title` or status alone** — e.g. a `409` can be a duplicate or a wrong state.
- **Ignore unknown fields** and tolerate new enum values; they are added without a new API version.
- **Show incomplete results honestly**: a cost with `isComplete: false` lists `missingPrices`; ask the baker to add them.
- **Treat `404` as "not yours or gone"** — the API does not distinguish, by design.

## Retries

| Status | Do |
|---|---|
| `429`, `503` | wait `Retry-After`, retry once; for the assistant offer the form-based path meanwhile |
| network error on `GET`/`PUT`/`DELETE` | retry with backoff (1 s, 2 s, 4 s) |
| network error on a create (`POST`) | read back before retrying — see [idempotency](idempotency.md) |
| `401` | log in again, then repeat the request once |
| `400`, `404`, `409`, `422` | do not retry unchanged |

## Caching

- Reference data (`/reference/*`) changes only with a release — cache it for the session.
- Calculation results are cached server-side per user and invalidated by any of the user's writes; do not add a long
  client cache on top, a stale cost after a price change is confusing.
- The anonymous library is cached for up to 5 minutes server-side.

## Security

- Keep the access token in memory; never put it in URLs or logs.
- Never send another user's id expecting access — isolation is by token only.
- The assistant's proposals change data only after an explicit confirmation in your UI; never auto-confirm.

## Performance

- Use paging (`pageSize` ≤ 100) and time windows for calendars.
- The p95 target is under 1 s for API calls at 20 concurrent users (see `tests/load/core-api.js`); assistant turns with
  tool calls take several seconds — show progress.
