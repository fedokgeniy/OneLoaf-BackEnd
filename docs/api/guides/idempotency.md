# Idempotency and safe retries

Network errors happen. This page says which requests can be repeated safely and what a repeat returns.

| Request | Repeat is safe? | Second call returns |
|---|---|---|
| Any `GET` | yes | the current state |
| `PUT /kitchen-settings`, `PUT /recipes/{id}`, `PUT /flours/{id}`, `PUT /ingredients/{id}`, `PUT /starters/{id}`, `PUT /batches/{id}` | yes (full replacement) | same result |
| `PUT /library/{id}/reactions/me` | yes (one reaction per user, replaced) | `204` |
| `DELETE /library/{id}/reactions/me` | yes | `204` |
| `POST /library/{id}/unpublish` | yes | `204` |
| `POST /push/subscriptions` | yes (upsert by endpoint) | `200` instead of `201` |
| `POST /recipes/{id}/archive` / `unarchive` | yes | `204` |
| `DELETE …/{id}` (recipes, flours, ingredients, starters, conversations, subscriptions) | effectively | `404` on the second call — treat as success |
| `POST /conversations/{id}/actions/{actionId}/confirm` | **once** | `409` — the batch was already created; the first response had its id |
| `POST /admin/reports/{id}/resolve` | **once** | `409` — already decided |
| `POST /batches/{id}/complete`, stage `start`/`complete` | **once** (state machine) | `409` — check `GET /batches/{id}` |
| `POST /batches`, `POST /recipes`, `POST /ingredients`, `POST …/prices`, `POST …/feedings`, `POST …/comments`, `POST /recipes/{id}/publish`, `POST /library/{id}/copy` | **no** | a second object (or `409` where names are unique) |

## Retrying a non-idempotent create

There is no `Idempotency-Key` header. After a timeout on a create:

1. Read back (`GET` the list, filtered by name or time) to see whether the first call succeeded.
2. Retry only if it did not.

Names are unique per user for flours and ingredients, so a duplicate create of those returns `409` instead of a copy.

## Assistant proposals

The assistant never writes on its own. It stores a proposal (`pendingAction`, 30-minute lifetime) and the client
confirms it. The confirmation deletes the proposal atomically in Redis before calling the Core API, so a double click
creates one batch, and the second click gets `409`.

## Events and reminders

Messages between services are delivered at least once. Consumers are idempotent: the reminder dispatcher continues only
while the reminder is still `scheduled` in the database, and the scheduler marks reminders in flight in Redis
(`SET NX`, 5 minutes) so the same reminder is not published twice. See [contracts](../architecture/contracts.md).
