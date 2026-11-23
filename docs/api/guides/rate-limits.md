# Rate limits and budgets

| Scope | Limit | Partition | Configuration |
|---|---|---|---|
| Core API auth endpoints (`/auth/register`, `/auth/login`) | 10 requests per 60 s window | client IP (from `X-Forwarded-For` set by Caddy) | `RateLimiting__Auth__PermitLimit`, `RateLimiting__Auth__WindowSeconds` |
| Assistant `POST /conversations/{id}/messages` and bake analysis | 10 per minute | user (`sub`) | `Assistant__MessagesPerMinute` |
| Assistant daily requests | 50 turns per day | user, UTC day | `Assistant__DailyRequestLimit` |
| Assistant daily tokens | 200 000 tokens per day | user, UTC day | `Assistant__DailyTokenLimit` |
| Assistant monthly budget | 4.50 USD per month (all users) | global | `Assistant__MonthlyBudgetUsd` |

Other endpoints have no request limit; the reverse proxy and the database pool protect them.

## Response

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 37
Content-Type: application/problem+json

{
  "type": "https://oneloaf.app/problems/too-many-requests",
  "title": "Too many requests.",
  "status": 429,
  "detail": "The daily assistant limit is reached. The calculator and forms keep working.",
  "correlationId": "…"
}
```

`Retry-After` is in seconds: the rest of the window for rate limits, until midnight UTC for daily limits, until the
first of the next month for the monthly budget. Requests are rejected immediately (no queueing).

## Seeing your usage

`GET /api/v1/usage/me` (Assistant) returns today's requests, tokens and cost against the limits — show it before the
user hits the limit:

```json
{ "day": "2026-11-23", "requests": 3, "requestLimit": 50, "tokens": 9240, "tokenLimit": 200000, "costUsd": 0.01218 }
```

## Client guidance

- Do not retry `429` in a loop; wait at least `Retry-After`, then retry once.
- When the assistant budget is reached, everything else keeps working — offer the planning form.
- The cost of a turn is computed from the configured prices per million tokens (`Assistant__Pricing__*`), and the
  system prompt and tool definitions are prompt-cached, so repeated turns are cheaper.
