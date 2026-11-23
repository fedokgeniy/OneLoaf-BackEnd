# Errors

Every error from every service is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details object with the
media type `application/problem+json`:

```json
{
  "type": "https://oneloaf.app/problems/conflict",
  "title": "The request conflicts with existing data.",
  "status": 409,
  "detail": "An account with this e-mail already exists.",
  "instance": "/api/v1/auth/register",
  "traceId": "00-e61b61c2ce5d90dc72018005e37e37de-336efdc2382f8283-02",
  "correlationId": "cb9380ef06984453a7e289c1bffa2f17"
}
```

| Field | Use it for |
|---|---|
| `type` | **Branching in code.** Stable URI per error kind (table below). |
| `status` | Same as the HTTP status. |
| `title` | Short human text of the kind; may change — do not parse. |
| `detail` | Specific human-readable reason, safe to show to the user (may be absent). |
| `errors` | Only for `validation`: field (camelCase, `items[0].loaves`) → messages. |
| `instance` | The request path. |
| `correlationId` | Quote it in bug reports; finds all log lines of the request across services. |
| `traceId` | W3C trace id of the request. |

## Error taxonomy

| `type` (prefix `https://oneloaf.app/problems/`) | Status | When | Client action | Retry? |
|---|---|---|---|---|
| `validation` | 400 | Body or query fails validation (FluentValidation): missing field, out of range, unknown code | Show `errors` next to the fields | no |
| `bad-request` | 400 | Malformed JSON, wrong type (`"3"` for a number), unparsable parameter, invalid value rejected by the database | Fix the request | no |
| `unauthorized` | 401 | Missing/expired/invalid token, wrong e-mail or password at login | Log in again | after login |
| `forbidden` | 403 | Role not sufficient, wrong password when deleting the account, database ownership rule | Hide the action | no |
| `not-found` | 404 | Object missing **or owned by another user** | Treat as gone | no |
| `conflict` | 409 | Duplicate (unique name/e-mail), object in use (recipe with variants), wrong state (stage already running, proposal used, report decided) | Refresh and decide | after refresh |
| `business-rule` | 422 | A domain rule: oven capacity not set, no device subscribed, rule enforced by a trigger (`detail` names it) | Show `detail` | no |
| `too-many-requests` | 429 | Rate limit (auth 10/min/IP, assistant 10 messages/min/user) or assistant budget (daily per user, monthly global) | Wait `Retry-After` seconds | yes |
| `service-unavailable` | 503 | Claude, push service, mail server or another dependency down | Fallback UI; retry after `Retry-After` | yes |
| `internal` | 500 | Unexpected error (logged with stack trace) | Report with `correlationId` | maybe |

Status codes per endpoint are listed in the [reference](../reference/core-api.md); the generated examples show real
responses for edge cases (duplicate, not found, invalid fields, wrong state).

## Validation errors

```json
{
  "type": "https://oneloaf.app/problems/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "email": ["'Email' is not a valid e-mail address."],
    "password": ["'Password' must be between 8 and 128 characters. You entered 5 characters."],
    "displayName": ["'Display Name' must not be empty."]
  }
}
```

Keys are JSON paths in camelCase. A request is validated completely before anything is saved.

## Database rules

Many invariants live in PostgreSQL (checks, triggers, procedures). Their SQLSTATE codes are mapped centrally:

| SQLSTATE | Name | Status / type |
|---|---|---|
| 23505, 23503 | unique / foreign key violation | 409 `conflict` |
| 55000 | object not in prerequisite state | 409 `conflict` |
| 23514 | check violation (also rules raised by triggers) | 422 `business-rule` |
| 23000 | integrity violation (ownership) | 403 `forbidden` |
| 22023 | invalid parameter value | 400 `bad-request` |
| P0002 | no data found | 404 `not-found` |

Rules raised by our own functions and triggers carry their message in `detail` (e.g. *A publication removed by a
moderator cannot be published again*). Violations of plain table constraints do not expose constraint names.

## Retry-After

`429` and `503` responses carry `Retry-After` in seconds when known (until the next minute for rate limits, until
midnight UTC for the daily assistant limit). See [rate limits](rate-limits.md).
