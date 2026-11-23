# Pagination, filtering and sorting

## Page-based lists

Lists that can grow without bound are paged with `page` (from 1) and `pageSize` (1–100, default 20):

```http
GET /api/v1/recipes?search=country&page=2&pageSize=20
```

```json
{
  "items": [ { "id": "…", "title": "Country sourdough", "yieldLoaves": 1, "isVariant": false } ],
  "page": 2,
  "pageSize": 20,
  "totalCount": 23
}
```

| Field | Meaning |
|---|---|
| `items` | The page; an empty array past the last page (not `404`) |
| `page`, `pageSize` | Echo of the request |
| `totalCount` | Items over all pages; pages = `ceil(totalCount / pageSize)` |

`page=0` or `pageSize=101` is a `400 validation` error.

| Endpoint | Filters | Order |
|---|---|---|
| `GET /recipes` | `search` (title part), `includeArchived`, `minProteinPercent` | title |
| `GET /flours`, `GET /ingredients` | `search`; ingredients also `category` | name |
| `GET /batches` | `from`, `to` (planned start, `to` exclusive), `status` | planned start |
| `GET /starters` | — | name |
| `GET /starters/{id}/feedings` | — | newest first |
| `GET /bake-records` | `recipeId`, `from`, `to`, `minRating` | newest first |
| `GET /library` (anonymous) | `search` | newest first |
| `GET /library/{id}/comments` (anonymous) | — | oldest first |
| `GET /admin/reports` | `status` (default `open`) | oldest first |

## Limit-based lists

`GET /api/v1/reminders` (Notification) returns a plain array, ordered by due time, with `limit` 1–200 (default 50) and
`status`, `from`, `to` filters — use a later `from` to read the next window.

Short, naturally bounded lists return plain arrays without paging: reference data, conversations, messages of one
conversation, warnings, price history of one ingredient.

## Enum filters

Filter values are the same snake_case strings as in JSON bodies: `?status=in_progress`, `?status=open`. An unknown value
is a `400` that lists the allowed values.

## Consistency

Pages are computed per request (offset paging). If data changes between two requests, an item can move across a page
boundary; for a calendar, prefer a time window (`from`/`to`) over deep paging. The anonymous library catalog is cached
for up to 5 minutes; publishing, unpublishing or removing a recipe clears the cache immediately.
