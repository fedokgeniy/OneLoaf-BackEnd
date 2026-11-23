# Sandbox

The sandbox is the full stack on your machine with the seed data: real services, real database functions, real
RabbitMQ and Redis — only Claude and Web Push need keys.

```powershell
docker compose up -d --build
```

```powershell
docker compose exec db psql -U oneloaf_app -d oneloaf -f /oneloaf/db/seed/test_data.sql
```

## Seed data

| User | Role in the data |
|---|---|
| Anna (`anna.baker@example.com`) | experienced home baker: recipes with variants, prices with history, batches in every status, a starter, a published recipe, a chat |
| Boris | beginner: a recipe with a missing price, no oven settings (incomplete costs) |
| Clara | home-bakery owner: labour rate, another currency, an overdue batch, an unpublished recipe |
| Dmitry | library visitor: comments and reactions only |
| Admin | administrator: resolves content reports |

The seed passwords are random hashes; register your own account for interactive use, or use the integration test
fixture (`ApiFixture.CreateClientAs`) which signs tokens for seed users with the test key.

## Without external keys

| Feature | Without the key |
|---|---|
| AI Assistant | container stops at start without `Anthropic__ApiKey`; everything else works |
| Web Push | `GET /push/vapid-public-key` is `503`; reminders are marked failed with reason `no_target` |
| E-mail | e-mail reminders report no target |

Generate VAPID keys for local push with `npx web-push generate-vapid-keys`.

## Reset

```powershell
docker compose down -v
```

removes the containers and all volumes (database, broker, Redis).
