# API conventions

| Topic | Convention | Example |
|---|---|---|
| Base path | `/api/v{major}/` + plural resource nouns | `/api/v1/recipes/{id}/variants` |
| Path segments | lower-case kebab-case; ids are UUIDs | `/kitchen-settings`, `/stage-log` |
| Actions that are not CRUD | `POST` to a verb sub-resource | `POST /batches/{id}/complete`, `/library/{id}/copy` |
| "Me" resources | `me` instead of the own id | `GET /me`, `PUT /library/{id}/reactions/me` |
| JSON fields | camelCase | `plannedStartAt`, `loafWeightG` |
| Enums | snake_case strings, also in query strings | `"in_progress"`, `?status=want_to_bake` |
| Numbers | JSON numbers only (`"3"` is rejected); money and quantities are decimals | `"price": 1.20` |
| Units in names | suffix with the unit | `loafWeightG`, `temperatureC`, `ovenPowerKw`, `minDurationMinutes` |
| Instants | ISO 8601 with offset; the response keeps the offset of the request | `2026-12-05T08:00:00+02:00` |
| Dates | ISO 8601 date | `"asOf": "2026-11-15"` |
| Nullability | `null` means "not set"; absent optional request fields are `null` | `"loafWeightG": null` |
| Create | `201 Created` + `Location` + the created resource (or its id) | `POST /recipes` |
| Update | `PUT` replaces the whole resource and returns it; `PATCH` only for small partial updates | `PATCH /bake-records/{id}` |
| Delete / no result | `204 No Content` | `DELETE /flours/{id}`, `POST /recipes/{id}/archive` |
| Errors | RFC 9457 problem details | see [errors](errors.md) |
| Correlation | `X-Correlation-ID` request header is echoed (generated when absent) and propagated to other services and events | |
| Operation ids | PascalCase verb + noun, unique per service | `CreateRecipe`, `ListReminders` |
| OpenAPI tags | one per functional area | `Recipes`, `Production`, `Library` |

## Headers

| Header | Direction | Meaning |
|---|---|---|
| `Authorization: Bearer …` | request | [authentication](authentication.md) |
| `X-Correlation-ID` | request/response | trace one operation across services and logs |
| `Retry-After` | response | seconds to wait after `429` / `503` |
| `Location` | response | URL of a created resource |
| `traceparent` | request | W3C trace context (OpenTelemetry); propagated to outgoing calls and events |

## Time zones

Times are stored as UTC instants. Calculations that depend on the local day (for example the price date of a cost) use
the kitchen's IANA time zone from the kitchen settings. Send times with an explicit offset.
