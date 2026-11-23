# Architecture overview

## System context

```mermaid
flowchart LR
    baker([Baker<br/>browser / installed PWA])
    visitor([Visitor<br/>anonymous])
    admin([Administrator])
    subgraph oneloaf[OneLoaf]
        web[Blazor Web App + PWA<br/><i>separate project</i>]
        api[OneLoaf backend<br/>Core API · AI Assistant · Notification]
    end
    claude[(Anthropic Claude API)]
    push[(Web Push services<br/>FCM · Mozilla · Apple)]
    smtp[(SMTP server)]
    tg[(Telegram<br/>alerts)]
    baker --> web
    visitor --> web
    admin --> web
    web -- HTTPS JSON, bearer JWT --> api
    api -- Messages API, tool use --> claude
    api -- VAPID Web Push --> push
    api -- e-mail reminders --> smtp
    api -- Alertmanager --> tg
    push -. notification .-> baker
```

## Containers

```mermaid
flowchart TB
    client[Client] -->|HTTPS| caddy[Caddy<br/>TLS, /core /assistant /notify, HSTS]
    subgraph services[Services .NET 10]
        core[Core API<br/>business data, calculations,<br/>auth, library, moderation]
        assistant[AI Assistant<br/>chat, tool calling, budget]
        notification[Notification<br/>scheduler, dispatcher, Web Push]
    end
    caddy --> core
    caddy --> assistant
    caddy --> notification
    assistant -->|HTTP, baker's JWT,<br/>X-Correlation-ID| core
    assistant -->|Messages API| claude[(Claude)]
    core --> pg[(PostgreSQL 16<br/>functions · procedures · triggers)]
    assistant --> pg
    notification --> pg
    core -->|batch.planned, batch.completed,<br/>starter.fed, account.deleted| mq[(RabbitMQ<br/>oneloaf.events)]
    notification -->|reminder.due| mq
    mq --> notification
    core --> redis[(Redis<br/>HybridCache L2)]
    assistant --> redis2[(Redis<br/>pending actions, usage)]
    notification --> redis3[(Redis<br/>in-flight markers)]
    notification --> webpush[(Web Push / SMTP)]
    services -.->|/metrics| prom[Prometheus] --> am[Alertmanager] --> tg[(Telegram)]
    services -.->|logs| loki[Loki]
    prom --> grafana[Grafana]
    loki --> grafana
```

`redis`, `redis2` and `redis3` are one Redis instance with separate key prefixes.

| Service | Owns | Talks to |
|---|---|---|
| **Core API** | users, workspace, calculations (via DB functions), batches, history, library, moderation | PostgreSQL, Redis cache, RabbitMQ (publish) |
| **AI Assistant** | conversations, messages, pending actions, usage | Core API (HTTP), Claude, PostgreSQL, Redis |
| **Notification** | push subscriptions, reminder delivery | PostgreSQL (reminders), RabbitMQ (publish + consume), Redis, Web Push, SMTP |

All three share one PostgreSQL database (schema owned by the migrator, services connect as `oneloaf_app`); each service
reads and writes only its own tables, except that reminders are created by Core API procedures and delivered by the
Notification service.

## Components inside a service

```mermaid
flowchart LR
    subgraph web[OneLoaf.CoreApi — web]
        program[Program.cs<br/>composition root]
        endpoints[Endpoints<br/>minimal APIs]
    end
    subgraph app[OneLoaf.CoreApi.Application]
        services[Use-case services<br/>RecipeService, BatchService, LibraryService …]
        ports[Ports<br/>IRecipeRepository, IRecipeCalculations,<br/>IEventPublisher, ICurrentUser]
        validators[FluentValidation validators]
    end
    subgraph infra[OneLoaf.CoreApi.Infrastructure]
        repos[EF Core repositories<br/>+ DB function gateway]
        cache[HybridCache decorators]
        jwt[JWT issuer, password hasher]
    end
    domain[OneLoaf.Domain<br/>entities, enums]
    shared[ServiceDefaults · SharedKernel · Messaging]
    endpoints --> services
    services --> ports
    services --> domain
    repos -.implements.-> ports
    cache -.implements.-> ports
    program --> infra
    program --> app
    web --> shared
    infra --> shared
```

Dependency rules (`Api → Application → Domain`, `Infrastructure → Application`; Application never references EF Core,
ASP.NET Core, RabbitMQ, Redis, Anthropic, Web Push or MailKit; services never reference each other) are checked by
`tests/OneLoaf.ArchitectureTests`.

## Project dependencies

```mermaid
flowchart BT
    Domain[OneLoaf.Domain]
    Kernel[OneLoaf.SharedKernel]
    Msg[OneLoaf.Messaging]
    Infra[OneLoaf.Infrastructure<br/>DbContext, migrations]
    Defaults[OneLoaf.ServiceDefaults]
    Rabbit[OneLoaf.Messaging.RabbitMq]
    CoreApp[CoreApi.Application] --> Domain & Kernel & Msg
    AsstApp[Assistant.Application] --> Domain & Kernel
    NotifApp[Notification.Application] --> Domain & Kernel & Msg
    Infra --> Domain
    Rabbit --> Msg & Defaults
    CoreInfra[CoreApi.Infrastructure] --> CoreApp & Infra & Defaults & Rabbit
    AsstInfra[Assistant.Infrastructure] --> AsstApp & Infra & Defaults
    NotifInfra[Notification.Infrastructure] --> NotifApp & Infra & Defaults & Rabbit
    Core[CoreApi] --> CoreApp & CoreInfra & Defaults
    Asst[Assistant] --> AsstApp & AsstInfra & Defaults
    Notif[Notification] --> NotifApp & NotifInfra & Defaults
```

## Request lifecycle

What happens to `POST /core/api/v1/batches`:

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant Caddy
    participant MW as Middleware pipeline
    participant E as Endpoint + filters
    participant S as BatchService
    participant DB as PostgreSQL
    participant MQ as RabbitMQ
    C->>Caddy: POST /core/api/v1/batches (Bearer, X-Correlation-ID)
    Caddy->>MW: POST /api/v1/batches (prefix stripped, X-Forwarded-For)
    Note over MW: forwarded headers → correlation id (scope, response header)<br/>→ request logging → exception handler → authentication (JWT)<br/>→ authorization (fallback: authenticated) → rate limiter → cache invalidation
    MW->>E: route match
    E->>E: FluentValidation filter (400 on failure)
    E->>S: CreateAsync(request, ct)
    S->>DB: recipes/starters owned by sub? (404 otherwise)
    S->>DB: INSERT batch + items; CALL sp_build_batch_plan (stages, reminders, cost)
    DB-->>S: committed
    S->>MQ: publish batch.planned (confirm, correlation-id, traceparent headers)
    S-->>E: BatchDto
    E-->>C: 201 Created, Location, BatchDto, X-Correlation-ID
    Note over MW: on any exception: GlobalExceptionHandler → problem details (status by type/SQLSTATE)
```

## Reminder pipeline

```mermaid
sequenceDiagram
    autonumber
    participant DB as PostgreSQL
    participant Sch as ReminderSchedulerWorker
    participant R as Redis
    participant MQ as RabbitMQ
    participant D as ReminderDispatcher
    participant WP as Web Push service
    Note over DB: sp_build_batch_plan / feeding trigger inserted reminders (scheduled, due_at)
    loop every 30 s
        Sch->>DB: SELECT … due_at <= now() FOR UPDATE SKIP LOCKED LIMIT 100
        Sch->>R: SET reminder:inflight:{id} NX EX 300
        Sch->>MQ: publish reminder.due
    end
    MQ->>D: notification.reminders
    D->>DB: still scheduled? (idempotency)
    D->>WP: push to every device (VAPID)
    alt delivered
        D->>DB: status = sent, sent_at, attempts + 1
    else push service down
        D->>DB: attempts + 1
        D->>MQ: notification.reminders.retry (TTL 30 s, x-attempt + 1)
        Note over D,MQ: after 5 attempts: status = failed, message → oneloaf.dead-letter (alert)
    else device gone (404/410)
        D->>DB: delete subscription
    end
```

The assistant planning flow (UC-01) is in the [assistant guide](../guides/assistant.md#conversation-flow-uc-01);
login is in [authentication](../guides/authentication.md#request-lifecycle).

## Deployment

```mermaid
flowchart LR
    gh[GitHub Actions<br/>per-service workflows] -->|images :sha, :latest| ghcr[(GHCR)]
    subgraph vm[Linux VM — docker compose]
        caddy2[caddy :80 :443]
        core2[core-api]
        asst2[assistant]
        notif2[notification]
        pg2[(postgres 16)]
        mq2[(rabbitmq)]
        rd2[(redis)]
        obs[prometheus · alertmanager · loki · grafana<br/>postgres-exporter · node-exporter]
        backup[db-backup<br/>daily pg_dump]
    end
    ghcr --> vm
    caddy2 --> core2 & asst2 & notif2
```

Only Caddy publishes ports. Configuration is environment variables written from GitHub Environment secrets
(`deploy/docker-compose.prod.yml`). Each service has its own workflow with path filters, so a change to one service
builds, tests and ships only that service; `security.yml` scans secrets, dependencies and images.

## Observability

| Signal | How |
|---|---|
| Logs | Serilog compact JSON to stdout and Loki; properties `Service`, `CorrelationId`, `UserId`, trace ids; no PII |
| Metrics | OpenTelemetry → `/metrics` (Prometheus): HTTP, runtime, Npgsql, plus `oneloaf_reminders_*`, `oneloaf_ai_*`, `oneloaf_cache_*` |
| Traces | W3C `traceparent` across HTTP calls and RabbitMQ headers |
| Dashboards | Grafana, provisioned from `deploy/grafana` |
| Alerts | `deploy/prometheus/alerts.yml` → Alertmanager → Telegram: service down, 5xx > 5 %, p95 > 1 s, reminder failures, dead letters, AI cost, PostgreSQL down, disk |
| Health | `/health/live` (process) and `/health/ready` (dependencies) on every service |
