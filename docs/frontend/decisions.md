# Frontend decisions (ADR-F)

Short records: context → decision → consequences. Add a new record instead of rewriting an accepted one.

## ADR-F01 Blazor WebAssembly standalone SPA

Context: the approved topic fixes Blazor; the general FE requirements ask for an SPA. Decision: standalone WebAssembly
(no server render modes), PWA template. Consequences: static hosting behind Caddy, larger first load (mitigated by
trimming, compression, lazy assemblies, service-worker cache), no prerendering.

## ADR-F02 Fluxor for global state, component state for local

Decision: Fluxor slices per feature + app slices in Shared; `Loadable<T>` for every async value. Consequences:
predictable data flow, testable reducers/effects; slight boilerplate per action.

## ADR-F03 Feature modules as Razor class libraries, feature-sliced dependencies

Decision: `Host → Features.* → Shared`, no feature-to-feature references, enforced by an ArchUnitNET test.
Consequences: lazy loading becomes possible per feature; cross-feature communication only via routes and Shared actions.

## ADR-F04 Lazy loading of History, Assistant and Library

Decision: `BlazorWebAssemblyLazyLoad` + `LazyAssemblyLoader` in `Router.OnNavigateAsync`; Recipes and Production load
eagerly (used every session). Lazy features register their Fluxor feature at load time (`IFeatureModule`). A lazy
feature whose state is used only by its own pages keeps it in a scoped page service instead.

## ADR-F05 Own design-system component kit, no third-party UI library

Decision: `Ol*` components over `components.css`. Consequences: exact match with mockups and checked contrast; we own
accessibility of every component (tested with bUnit).

## ADR-F06 Hand-written contract records + contract tests on recorded examples

Context: OpenAPI 3.1 documents; generators handle 3.1 nullability unevenly. Decision: C# records in `Shared/Contracts`,
`System.Text.Json` source generation, a test that deserializes every recorded 2xx example with unmapped members
disallowed. Consequences: types are readable and trimming-safe; drift is caught by CI when the backend regenerates the
examples.

## ADR-F07 Token storage: memory + sessionStorage, no refresh

Context: the API issues 60-minute JWTs without refresh tokens. Decision: keep the token in memory and `sessionStorage`
(survives reload, not other tabs or browser restart); never `localStorage`. Warn 1 minute before expiry; on 401 go to
login with `returnUrl`. Consequences: users sign in again after an hour of inactivity per tab — acceptable for the pilot.

## ADR-F08 Same-origin production hosting

Decision: web container behind the existing Caddy at `/`; APIs stay at `/core`, `/assistant`, `/notify`. Development
uses CORS for `http://localhost:5090` only. Consequences: no CORS in production; one TLS certificate.

## ADR-F09 E2E tests with recorded API examples as route mocks

Decision: Playwright intercepts `**/api/v1/**` and answers from `docs/api/examples` fixtures; one optional full-stack
smoke test runs against docker compose. Consequences: fast, deterministic CI without Claude or a database; the
full-stack test proves the wiring before the defense.

## ADR-F10 Charts as small SVG components

Decision: no chart library; SVG components with tokens and a data-table alternative. Consequences: tiny payload,
accessible, themeable; limited to the three charts we need.

## ADR-F11 Assistant replies: an own Markdown subset rendered as elements

**Context.** The assistant answers in Markdown (lists, bold, headings). Shown as plain text the markers are noise; inserting
converted HTML would execute whatever the model writes.
**Decision.** `MarkdownParser` reads the subset the assistant uses (paragraphs, headings, bulleted and numbered lists,
fenced code, bold, italic, inline code, http/https links) into a tree of text pieces, and `MarkdownText` builds elements
from it with the render tree. No `MarkupString`, no HTML passes through; unknown syntax stays text.
**Consequences.** No new dependency and no download size; tables and nested lists are not rendered (they stay readable
text). A library such as Markdig was rejected because it produces HTML that must then be sanitised.

## ADR-F12 Stock figures and offers come from the API

**Context.** The stock of an ingredient, what a batch takes and what the stock is enough for are business numbers.
**Decision.** The client shows `stock`, `isLowStock` and the batch `stock` check (`shortages`, `maxLoaves`, `maxScale`) as
returned and sends only the baker's choice: the loaves or the `ingredientScale` offered by the API, and
`ingredientsUsed` when a batch is cancelled or deleted. After every purchase, correction or batch the list and the
ledger are loaded again instead of being updated in the client.
**Consequences.** Same rule as for cost and timeline (rule 1); one more request after each command.

## Dependency policy

| Used | Deliberately not used |
|---|---|
| Fluxor.Blazor.Web (+ ReduxDevTools in Debug), Microsoft.AspNetCore.Components.* , Microsoft.Extensions.Http, System.Text.Json, bunit, xunit.v3, Microsoft.Playwright, Deque.AxeCore.Playwright (e2e accessibility checks), ArchUnitNET | any UI component library (MudBlazor, Radzen, Syncfusion, Telerik, Blazorise, FluentUI), jQuery or any JS framework, Newtonsoft.Json, Blazored.LocalStorage (a small JS interop is used instead), AutoMapper, chart libraries, analytics/trackers, CDN-loaded scripts other than Google Fonts |

All package versions are managed centrally in `Directory.Packages.props`; lock files are committed
(`RestorePackagesWithLockFile`).
