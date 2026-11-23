# OneLoaf Web — frontend documentation

Blazor WebAssembly SPA + PWA for OneLoaf. Start with `architecture.md` (how the client is built) and `user-flows.md`
(how the user moves through it).

| Document | Contents |
|---|---|
| [architecture.md](architecture.md) | approach, context, solution layout, dependency rules, component model, state, routing, request pipeline |
| [screens-and-routes.md](screens-and-routes.md) | route table (page, auth, mockups, stories, endpoints), navigation, story coverage |
| [api-client.md](api-client.md) | base URLs, handler pipeline, error → UI mapping, contracts, typed services |
| [user-flows.md](user-flows.md) | navigation map and the main flows (sign-up, manual batch, assistant batch, analysis, starter, library, session) |
| [design-system-in-blazor.md](design-system-in-blazor.md) | CSS layers, component kit, themes/scale, size classes, keyboard, a11y, PWA |
| [tech-stack.md](tech-stack.md) | why Blazor WASM, Fluxor, own component kit; JS-tool equivalents |
| [decisions.md](decisions.md) | ADR-F01…F10, dependency policy |
| [testing.md](testing.md) | bUnit, contract, architecture, e2e, a11y, Lighthouse |
| `mockups/` | 87 artboards from the design canvas as static HTML (open in a browser) |
| `design-system/` | tokens, CSS, icons, logos, guideline pages, component pages |

The API contract and recorded examples are in [`../api/`](../api/README.md).
