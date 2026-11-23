# User flows and navigation

## 1. Navigation map

```mermaid
flowchart TB
    start((Open app)) --> hasToken{valid token<br/>in session?}
    hasToken -- no --> login[/login/]
    hasToken -- yes --> home[/ Home/]
    start -.public link.-> library[/library/]
    login <--> register[/register/]
    login -->|success| ret{returnUrl?}
    ret -- yes --> back[requested page]
    ret -- no --> home

    subgraph nav [Main navigation — always visible]
      home
      recipes[/recipes/]
      batches[/batches — calendar/]
      starters[/starters/]
      assistant[/assistant/]
      library
      history[/history/]
      settings[/settings/]
    end

    recipes --> recipe[/recipes/:id/]
    recipes --> newRecipe[/recipes/new/]
    recipe --> edit[/recipes/:id/edit/]
    recipe --> compare[/recipes/:id/compare/]
    recipe -->|Plan batch| newBatch[/batches/new?recipeId=/]
    recipe -->|Publish| publish[[Publish dialog]]
    recipe -->|Create variant| variant[[Variant dialog]] --> edit
    batches --> newBatch --> batch[/batches/:id/]
    home -->|Open batch| batch
    batch -->|batch done| historyRec[/history/:id/]
    starters --> newStarter[/starters/new/]
    starters --> feed[[Log feeding sheet]]
    history --> historyRec
    historyRec -->|Ask the assistant| analysis[[Analysis panel]]
    assistant -->|Confirm plan| batch
    library --> libDetail[/library/:id/]
    libDetail -->|Copy to my recipes| recipe
    settings --> kitchen[/settings/kitchen/]
    settings --> ingredients[/ingredients/]
    settings -->|Sign out| login
    admin([admin]) --> reports[/admin/reports/]
```

Back navigation: browser back always works (real routes); detail pages also have a back button in the top bar on
compact. Deep links work after a reload (static host serves `index.html` for unknown paths).

## 2. Flow — sign up and first recipe (US-01, US-05, US-10, US-12, US-13)

```mermaid
flowchart LR
    a[/register/] -->|valid form| b[POST /auth/register] --> c[POST /auth/login] --> d[Home — empty states]
    d -->|"Set up your kitchen" card| e[/settings/kitchen/] -->|Save| d
    d -->|"Add ingredients"| f[/ingredients/] -->|New ingredient + price| f
    d -->|"New recipe"| g[/recipes/new/] -->|4 ingredients, 3 stages, Save| h[/recipes/:id/]
    h --> i{cost complete?}
    i -- no --> j[warning: prices missing → link to /ingredients] --> f
    i -- yes --> k[cost, timeline, figures shown]
```

Validation errors: client-side (required, ranges, min ≤ max duration) before the request, server `errors` mapped to the
same fields after it.

## 3. Flow — plan and bake a batch manually (US-20, US-23, US-24, US-31, US-32)

```mermaid
sequenceDiagram
    autonumber
    actor B as Baker
    participant UI as Web
    participant C as Core API
    B->>UI: Recipe → "Plan batch"
    UI->>UI: /batches/new?recipeId=…  (form prefilled)
    loop while editing (debounced 400 ms)
        UI->>C: POST /batches/preview
        C-->>UI: plan: ready window, cost, cycles, starter, stages
    end
    B->>UI: Create batch
    UI->>C: POST /batches
    C-->>UI: 201 batch (planned)
    UI->>UI: navigate /batches/:id, toast "Batch planned"
    B->>UI: Start (stage 1) … Complete
    UI->>C: POST …/stages/{id}/start · /complete · /extend {minutes:30}
    C-->>UI: updated batch (statuses, recalculated times)
    B->>UI: Complete last stage
    C-->>UI: batch done → bake record created by the DB
    UI->>UI: toast "Bake saved to history" + link /history/:id
    B->>UI: rating 4 + comment → Save
    UI->>C: PATCH /bake-records/{id}
```

Errors on the way: 409 "stage already running" → reload batch; 422 "oven capacity not set" → alert with link to kitchen.

## 4. Flow — batch through the assistant (UC-01: US-34, US-35)

```mermaid
sequenceDiagram
    autonumber
    actor B as Baker
    participant UI as Web (Assistant page)
    participant A as AI Assistant
    participant C as Core API
    B->>UI: "Create a batch of 3 white loaves for tomorrow at 12"
    UI->>A: POST /conversations/{id}/messages
    UI->>UI: typing indicator, composer stays usable
    A->>C: tools (find recipe, preview batch) with the baker's JWT
    A-->>UI: message + pendingAction{create_batch, plan, expiresAt} + disclaimer
    UI->>UI: PlanCard: amounts, starter, cycles, ready window, total cost — "Nothing is saved until you confirm"
    alt Confirm
        B->>UI: Confirm plan
        UI->>A: POST …/actions/{actionId}/confirm
        A-->>UI: {batchId}
        UI->>UI: dispatch BatchCreated → toast + "Open batch"
    else Change
        B->>UI: "Make it 4 loaves instead"
        UI->>A: POST …/messages (new plan replaces the old card)
    else Decline / expired
        UI->>A: POST …/decline  (or card shows "Expired — ask again")
    end
```

Alternatives: recipe not found → assistant lists similar recipes (rendered as text); 503 → alert + "Use the manual form"
(`/batches/new`); 429 → "limit reached" with the reset time from `usage/me`.

## 5. Flow — analyse a bake (UC-02: US-36)

`/history/:id` → "Ask the assistant" → `POST /assistant/api/v1/bake-records/{id}/analysis` → analysis panel with
"Informational only — based on your stage log and comment". 422 (no feedback yet) → focus the comment field with the
message "Add a comment about the result first". 503 → record unchanged, retry later.

## 6. Flow — starter feeding (US-27, US-28, US-29, US-30)

`/starters` → card "Log feeding" → sheet (compact) / dialog (medium+) with flour, water, kept, temperature, fed at (now)
→ `POST /starters/{id}/feedings` → card shows next feeding and expected peak; reminder is created by the backend.
Push permission is requested in Settings → Reminders, never automatically.

## 7. Flow — public library (US-39–US-42, LR-11)

Visitor: `/library` (search) → `/library/:id` → actions show "Sign in to copy, react and comment" → `/login?returnUrl=…`.
Baker: react (toggle), comment, **Copy to my recipes** → navigate to the new `/recipes/:id`; **Report** → dialog with a
reason. Owner on their publication: **Unpublish**. Admin: `/admin/reports` → open report → Resolve (keep) or Remove
publication.

## 8. Flow — session and errors

```mermaid
stateDiagram-v2
    [*] --> Anonymous
    Anonymous --> SignedIn: login 200
    SignedIn --> SignedIn: API 2xx / 4xx (not 401)
    SignedIn --> Expiring: 1 min before exp
    Expiring --> Anonymous: exp reached → toast, keep returnUrl
    SignedIn --> Anonymous: any 401 → toast "session ended"
    SignedIn --> Anonymous: Sign out (POST /auth/logout, clear token, clear SW cache)
    SignedIn --> Anonymous: Delete account confirmed
```
