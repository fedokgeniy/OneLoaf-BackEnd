# Frontend testing

| Level | Project | Tools | Runs in CI |
|---|---|---|---|
| Component + unit | `tests/OneLoaf.Web.UnitTests` | xUnit v3, **bUnit**, fakes for API services | every push (`web.yml`) |
| Contract | same project, `Contracts/` | recorded `docs/api/examples/**/*.json` | every push |
| Architecture | same project, `ArchitectureTests.cs` | ArchUnitNET | every push |
| E2E | `tests/OneLoaf.Web.E2ETests` | **Playwright for .NET**, Chromium, route mocks from recorded examples | every push (headless) |
| Full-stack smoke (optional) | same project, trait `FullStack` | docker compose stack | manual / before the defense |
| Accessibility + Lighthouse | e2e project + Lighthouse CI (optional) | axe-core injected by Playwright; Lighthouse | e2e: every push; Lighthouse: manual, results recorded below |

Coverage gate for `OneLoaf.Web.Shared` and feature libraries: **70 %** lines (same threshold as the backend
`COVERAGE_THRESHOLD`), pages excluded where they only compose components.

## 1. Required by the topic: ≥ 3 key component tests (bUnit)

| Test class | Component | What it proves |
|---|---|---|
| `OlTextFieldTests` | `OlTextField` | label is linked to the input; error state sets `aria-invalid` + `aria-describedby`; suffix renders; two-way binding updates the model |
| `CostTableTests` | `CostTable` (Recipes) | renders every line returned by the API in order incl. electricity, packaging, loss, labor; shows the API total (not a client sum); "cost incomplete" warning lists missing prices; stacked rows on compact |
| `StageItemTests` | `OlStageItem` | upcoming → Start; running → Complete + "+30 min"; done → no actions; callbacks fire with the stage id; status word present (not colour only) |
| `PlanCardTests` | `PlanCard` (Assistant) | renders amounts, ready window and cost from `BatchDto`; Confirm and Change raise callbacks; expired action disables Confirm; disclaimer shown |
| `NewBatchFormTests` | `NewBatchPage` form | required fields, loaves 1–100, at least one recipe; server `errors["items[0].loaves"]` appears under the right field; submit disabled while saving |
| `LoginPageTests` | `LoginPage` | 401 shows one generic message; success dispatches `LoggedIn` and navigates to `returnUrl` |

Plus unit tests (no rendering): `RecipesReducersTests`, `ProductionEffectsTests` (fake `BatchesApi`, debounced preview
cancels the previous call), `ProblemDetailsHandlerTests` (each status → `ApiError.Kind`, 401 dispatches
`SessionExpired`, `Retry-After` parsed), `FormattingTests` (grams, money, durations, windows over midnight),
`ServerValidatorTests` (camelCase paths → form fields).

bUnit setup: `BunitContext` with `Services.AddFluxor(o => o.ScanAssemblies(...))` or a fake `IState<T>` for dumb
components; `JSInterop.Mode = JSRuntimeMode.Loose` for `device.js`; `BunitNavigationManager` for navigation asserts;
fake API services registered in `Services`.

## 2. Contract tests

`RecordedExamplesTests` — `[Theory]` over all example files: for each 2xx response with a body, deserialize into the
mapped record (`OperationId → Type` table in the test) with `UnmappedMemberHandling.Disallow`; fails when the backend
adds/renames a field or the client mistyped one. Also asserts that every operation the client calls has a mapping.

## 3. E2E (1–2 required; 2 planned + 1 optional)

| Test | Journey | Checks |
|---|---|---|
| `RecipeCostJourneyTests` | open `/login` → sign in → Recipes list → open "Country sourdough" → set loaves to 3 → Cost tab | API called with `loaves=3`; total equals the recorded `total`; electricity/packaging/loss lines visible; no axe violations on the page; screenshot at 360×640 and 1440×900 |
| `AssistantPlanJourneyTests` | signed in → Assistant → send the request → plan card → Confirm | confirm endpoint called once; toast "Batch saved" with link; navigating to the link opens `/batches/{id}` |
| `FullStackSmokeTests` (trait `FullStack`, optional) | real compose stack: register → kitchen → ingredient + price → recipe → cost | end-to-end wiring, CORS/Caddy routing |

Route mocks: a `RecordedApi` helper loads `docs/api/examples/<service>/<OperationId>.json` and registers
`page.RouteAsync("**/api/v1/recipes/*/cost*", …)` with the recorded status and body (ids in URLs are matched by pattern).
The app under test is `dotnet publish`-ed once per run (Release, trimmed — the same bundle as the image) and served by
the `WebAppFixture` (a `WebApplication` with static files and `MapFallbackToFile("index.html")` on a free local port).
The fixture reads the published `wwwroot` from `ONELOAF_E2E_WEB_ROOT`; without it the journeys are skipped, so the
backend workflows, which run every test project of the solution, are not affected. Recorded access tokens are redacted
and expire, so the journeys replace the token of the recorded login response with an unsigned token carrying the
recorded user's claims (the client reads claims only; the services verify signatures).

```powershell
dotnet publish src/Web/OneLoaf.Web -c Release -o artifacts/web
pwsh tests/OneLoaf.Web.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
$env:ONELOAF_E2E_WEB_ROOT = "$PWD/artifacts/web/wwwroot"; dotnet test tests/OneLoaf.Web.E2ETests
```

| Journey (implemented) | Checks |
|---|---|
| `SignInJourneyTests.SignIn_RecordedLogin_ShowsShellAndSignsOut` at 360×640, 768×1024, 1440×900 | protected route redirects to `/login`; recorded login; exactly one main navigation (bottom bar / rail / sidebar) with `aria-current="page"` on Home; screenshot; Sign out clears the token and returns to `/login` |
| `SignInJourneyTests.SignIn_WrongPassword_ShowsOneGenericMessage` | recorded 401 → one generic message in an alert, no redirect |
| `RecipeCostJourneyTests.RecipeCost_ThreeLoaves_ShowsRecordedTotalAndLines` at 360×640, 768×1024, 1440×900 | list → "Country sourdough" → loaves 3 → the cost request carries `loaves=3`; the total tile shows the recorded `total`; electricity, packaging and loss lines visible; full-page screenshot |
| `RecipesJourneyTests.Recipes_RecordedList_ShowsCardsAndSearches` at 360×640, 768×1024, 1440×900 | recorded `ListRecipes` → card linked to the recipe; full-page screenshot; typing a search sends `search=` to the API and puts it in the address bar |

## 4. Accessibility and responsive checks

- axe-core (`Deque.AxeCore.Playwright`) on the main pages in both e2e tests: zero serious/critical violations.
- Screenshots of key pages at 360×640 (16:9), 360×840 (21:9), 768×1024, 1440×900, 2560×1080, light and dark — attached
  as CI artifacts (visual review, not pixel diffs).
- Keyboard-only pass of the two journeys (Playwright `keyboard.press("Tab")`) for focus order and visible focus.

## 5. Lighthouse (Direction 3 bonus)

Run on the published app served by Caddy (compression on). Record results here:

| Page | Mobile perf / a11y / best practices / SEO | Desktop |
|---|---|---|
| `/login` | – | – |
| `/recipes` | – | – |
| `/recipes/{id}` | – | – |
