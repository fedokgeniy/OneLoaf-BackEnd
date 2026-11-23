# Tech stack justification

## 1. Framework: Blazor WebAssembly (standalone) on .NET 10

| Option | For | Against | Verdict |
|---|---|---|---|
| **Blazor WebAssembly standalone** | Fixed by the approved topic application and the BA ("Blazor Web App with a mobile-first interface and PWA support"); real SPA running fully in the browser; the same language, analyzers, test framework and CI as the three backend services (one developer — risk R3); static hosting behind the existing Caddy; built-in PWA template; .NET 10 LTS until Nov 2028 | Larger first download than a JS SPA (mitigated: trimming, compression, lazy assemblies, caching by the service worker); smaller UI-library ecosystem (not needed — custom design system) | **chosen** |
| Blazor Web App (Server / Auto render modes) | prerendering, small initial payload | needs a server process and SignalR circuit per user; Auto mode doubles the mental model (server + WASM); a static SPA is enough for this app and fits "SPA" in the general requirements literally | rejected |
| React / Angular / Vue + TypeScript | largest ecosystems; literal match to the general course list | contradicts the approved topic; second language and toolchain for one developer; types would be duplicated from the C# backend by hand anyway | rejected (outside the approved topic) |

Why WebAssembly and not Server: the UI must keep working on a phone in the kitchen with a flaky connection (PWA offline
shell, US-44), and every interaction is a REST call to the APIs anyway — there is no server-side state to keep.

## 2. State management: Fluxor

| Option | Verdict |
|---|---|
| **Fluxor** (Flux/Redux for .NET: store, features, actions, reducers, effects, middleware, Redux DevTools) | **chosen** — the Redux pattern named in the requirements, actively maintained, targets current .NET, zero-boilerplate attributes, effects fit the async API calls, DevTools middleware helps the demo |
| Cascading parameters + scoped services only | fine for small apps; becomes event soup once the assistant, batches and home must react to the same change (`BatchCreated`) |
| Hand-written store with `IObservable` | reinventing Fluxor; no tooling |

Local state stays in components (form fields, toggles); see `architecture.md` §4 for the split.

## 3. UI kit: own component library from the OneLoaf design system

| Option | Verdict |
|---|---|
| **Custom `Ol*` components over `components.css`** | **chosen** — the design system and 87 mockups already exist and were checked against WCAG 2.1 AA and the adaptive UI requirements; the kit is a deliverable in itself (Direction 4 "custom component design kit") |
| MudBlazor / Radzen / FluentUI Blazor | Material/Fluent look would have to be overridden everywhere to match the approved design; extra payload; their contrast and target sizes differ from the checked tokens |
| Bootstrap | same overriding problem; jQuery-free but still a second grid system next to `ol-grid` |

## 4. "Bundler", "TypeScript", "ESLint + Prettier" — the .NET equivalents

The general requirements name JavaScript tools. Blazor does not use them, and adding them would not improve anything.
What fills each role:

| Requirement wording | What it is for | Blazor / .NET equivalent used here |
|---|---|---|
| Bundler (Vite / Webpack / CLI) | compile, tree-shake, minify, fingerprint, split code | .NET SDK WebAssembly build: Razor → C# → IL → Webcil `.wasm`; **IL trimming** (tree-shaking), **Brotli/gzip** precompression, **fingerprinted static web assets** with an import map, **lazy-loaded assemblies** (code splitting), optional AOT. `dotnet publish -c Release` emits the bundle in `wwwroot` |
| TypeScript (types) | static types for API data | C# 14 with **nullable reference types** and records; warnings are errors; contract tests against recorded API examples |
| ESLint | static analysis | Roslyn analyzers (`AnalysisLevel latest-recommended`), Razor compiler diagnostics, `TreatWarningsAsErrors`, ArchUnitNET dependency rules |
| Prettier | formatting | `.editorconfig` + `EnforceCodeStyleInBuild` + `dotnet format --verify-no-changes` in CI |
| Jest / Testing Library | component tests | **bUnit** (renders components, queries the DOM, fakes JS interop and navigation) + xUnit v3 |
| Cypress / Playwright | e2e | **Playwright for .NET** |

## 5. Other libraries

| Library | Use | Why |
|---|---|---|
| `Microsoft.AspNetCore.Components.WebAssembly` (+ `.Authentication` not used) | runtime | standard |
| `Microsoft.AspNetCore.Components.Authorization` | `AuthenticationStateProvider`, `AuthorizeView`, `[Authorize]` | route guards without a heavy OIDC library — the API uses plain JWT from `/auth/login` |
| `Microsoft.Extensions.Http` | `IHttpClientFactory`, handlers | central request pipeline |
| `Fluxor.Blazor.Web` (+ `Fluxor.Blazor.Web.ReduxDevTools` in Debug only) | state | §2 |
| `System.Text.Json` source generators | serialization | trimming-safe, fast |
| `bunit`, `xunit.v3`, `Microsoft.Playwright` (+ `Microsoft.Playwright.Xunit.v3` if available, else plain xUnit fixtures), `TngTech.ArchUnitNET.xUnitV3` | tests | already the repo's test stack + Blazor-specific tools |

No charting library: the three charts (history statistics, cost breakdown bar, batch timeline) are small SVG components
drawn from tokens — lighter than a JS chart library and fully accessible (a data table alternative under each chart).

## 6. Hosting and delivery

Static files in an `nginx:alpine` (or `caddy:alpine` file server) image `oneloaf-web`, published to GHCR by a new
`web.yml` workflow and served by the existing Caddy at the site root (`handle { reverse_proxy web:80 }`), next to `/core`,
`/assistant`, `/notify` — one origin, no CORS in production, HSTS and compression already configured in Caddy.
