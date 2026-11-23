# Getting started

Run the whole backend on your machine and make your first authenticated call. About 10 minutes.

## Prerequisites

- Docker Desktop (or Docker Engine with Compose v2)
- .NET SDK 10 (only to run the services outside Docker or the tests)
- `curl` or any HTTP client (Swagger UI works too)

## 1. Configure

```powershell
Copy-Item .env.example .env
```

Fill in `.env`: the database, RabbitMQ and Redis passwords, and the JWT key pair. Generate the key pair once:

```powershell
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out jwt.key
```

```powershell
openssl pkey -in jwt.key -pubout -out jwt.pub
```

Put the PEM texts into `Jwt__SigningKeyPem` (private, Core API only) and `Jwt__PublicKeyPem` with `\n` instead of line
breaks. `Anthropic__ApiKey` is needed only for the AI Assistant; VAPID keys only for real Web Push. Nothing secret is
committed — `.env` is git-ignored.

## 2. Start

```powershell
docker compose up -d db rabbitmq redis
```

```powershell
docker compose run --rm migrator
```

```powershell
docker compose up -d --build core-api assistant notification
```

The migrator applies the schema as `app_admin`; the services connect as `oneloaf_app`. Optional test data (users
Anna, Boris, Clara, Dmitry and an administrator, with recipes, batches and a published recipe):

```powershell
docker compose exec db psql -U oneloaf_app -d oneloaf -f /oneloaf/db/seed/test_data.sql
```

Check that everything is up:

```powershell
curl.exe -s http://localhost:5080/health/ready
```

`/health/live` answers when the process runs; `/health/ready` also checks PostgreSQL (and RabbitMQ/Redis where used).

## 3. Register and log in

```powershell
curl.exe -s -X POST http://localhost:5080/api/v1/auth/register -H "Content-Type: application/json" -d '{\"email\":\"you@example.com\",\"password\":\"Rye-Bread-2026!\",\"displayName\":\"You\"}'
```

```json
{ "userId": "30663e7c-130f-4af7-a846-4dbf85602e4f" }
```

```powershell
curl.exe -s -X POST http://localhost:5080/api/v1/auth/login -H "Content-Type: application/json" -d '{\"email\":\"you@example.com\",\"password\":\"Rye-Bread-2026!\"}'
```

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "expiresAt": "2026-11-23T05:08:36+00:00",
  "user": { "id": "30663e7c-...", "email": "you@example.com", "displayName": "You", "role": "user" }
}
```

## 4. First authenticated call

```powershell
$token = "<accessToken from the login response>"
curl.exe -s http://localhost:5080/api/v1/me -H "Authorization: Bearer $token"
```

The same token works for the AI Assistant (`:5081`) and Notification (`:5082`) — they validate it with the public key.

## 5. Explore

- Swagger UI: <http://localhost:5080/swagger>, <http://localhost:5081/swagger>, <http://localhost:5082/swagger>
  — click **Authorize** and paste the token.
- The [`.http` files](../examples) contain one ready request per endpoint for VS Code REST Client / JetBrains.
- Without running anything: start a [mock server](../tools/mock-server.md) from the OpenAPI documents.

Next: the [integration tutorial](integration-tutorial.md) walks through a complete baking workflow.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `401` with a fresh token | Clock skew over 30 s, or the services have different `Jwt__Issuer`/`Jwt__Audience`/public key. |
| `/health/ready` is `503` | A dependency is down: `docker compose ps`; the response lists the failing check. |
| `429` on login | 10 auth requests per minute per IP; wait for `Retry-After` seconds. |
| Assistant container exits at start | `Anthropic__ApiKey` is empty (required option). |
| `GET /push/vapid-public-key` is `503` | `Notification__VapidPublicKey`/`PrivateKey` not set. |
