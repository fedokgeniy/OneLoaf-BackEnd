# The design system in Blazor

Source of truth: `docs/frontend/design-system/` — `tokens.json` (all values), `tokens.css` (generated custom properties,
light + dark), `components.css` (all `ol-*` classes), `icons/*.svg` (31 icons), `logos/*.svg`, guideline pages
(`README.md`, `layout-and-devices.md`, `accessibility-and-settings.md`, `requirements-compliance.md`) and one page per
component (`components/*.md`). Mockups (`mockups/*.html`) show every screen built from exactly these classes — copy the
markup structure, not the inline `style` attributes (the boards use some inline layout for the static drawing; in the
app that layout becomes `.razor.css` with tokens).

## 1. CSS architecture (three layers)

| Layer | File | Rule |
|---|---|---|
| 1 Tokens | `wwwroot/css/tokens.css` (copy of `design-system/tokens.css`) | only custom properties; themes via `[data-theme="dark"]`; never edited by hand — regenerate from `tokens.json` if a token changes |
| 2 Components | `wwwroot/css/components.css` (copy of `design-system/components.css`) | the `ol-*` classes; shared by all features; changes are design-system changes (update the copy in `docs/` too) |
| 3 App + isolation | `wwwroot/css/app.css` (shell, page grid helpers, transitions) + `*.razor.css` per component | tokens only, `em` breakpoints, no hex, no px font sizes; `::deep` only to style child `Ol*` components on purpose |

`index.html` loads `css/tokens.css`, `css/components.css`, `css/app.css`, then `OneLoaf.Web.styles.css` (the bundled CSS
isolation output). Fonts: `components.css` imports Google Fonts (Cormorant Garamond, DM Sans); add
`<link rel="preconnect">` for both font hosts. A `scripts/sync-design-system.ps1` copies the three files and the icons
from `docs/frontend/design-system/` into `wwwroot` so there is one source.

## 2. Component kit (`OneLoaf.Web.Shared/Ui`)

Each component wraps the classes of one design-system component page, owns its ARIA, exposes parameters for every state
shown on the `Components-States` board, and has a bUnit test for the states that matter.

| Razor component | DS page / classes | Key parameters | States to support |
|---|---|---|---|
| `OlButton` | Button · `.ol-btn` `--primary/--secondary/--ghost/--danger`, `--lg`, `--block`, `.is-loading` | `Variant`, `Size`, `Block`, `Loading`, `Disabled`, `Type`, `OnClick`, `Icon`, `ChildContent` | default, hover, focus, pressed, disabled, loading (`aria-busy`, label kept for screen readers) |
| `OlIconButton` | `.ol-icon-btn` | `Icon`, `Label` (required → `aria-label`), `Danger` | as button; 44px target |
| `OlLink` | `.ol-link` | `Href`, `ChildContent` | 44px hit area |
| `OlIcon` | `.ol-icon` + `icons/*.svg` | `Name` (enum `IconName`), `Size` | decorative by default (`aria-hidden`) |
| `OlTextField` | TextField · `.ol-field` `.ol-input` `.ol-field__suffix` `__help` `__error` | `Label`, `Value`/`ValueChanged` (InputBase<T>), `Suffix`, `Help`, `Placeholder`, `InputMode` | default, hover, focus, error (`aria-invalid`, `aria-describedby` → error id), disabled |
| `OlNumberField` | same, `.ol-num` | `Min`, `Max`, `Step` + stepper buttons (`Fewer loaves` / `More loaves`) | as above |
| `OlSelect`, `OlTextArea`, `OlDateField`, `OlTimeField` | `.ol-select`, `.ol-textarea` | InputBase<T> | as above |
| `OlCheckbox`, `OlRadioGroup`, `OlSwitch` | SelectionControls · `.ol-check` `.ol-switch` | `Label`, value | checked, unchecked, disabled, focus |
| `OlSegmented` | SegmentedControl · `.ol-segmented` `--fill` | `Options`, `Value`, `Fill`, `Label` | selected (`aria-pressed`), hover, focus |
| `OlTabs` / `OlTab` | Tabs · `.ol-tabs` `.ol-tab` | `ActiveId`, tab list with roving tabindex, arrow keys | selected, focus |
| `OlChip` | Chip · `.ol-chip` | `Pressed`, `OnToggle` | selected, default, hover, disabled; wraps on narrow screens |
| `OlStatusBadge` | StatusBadge · `.ol-badge--planned/running/done/overdue/neutral` | `Status` | word + dot, never colour alone |
| `OlAlert` | Alert · `.ol-alert--info/success/warning/danger` | `Kind`, `Title`, `ChildContent`, `Action` | `role="alert"` for danger, `status` otherwise |
| `OlToast` (+ `OlToastHost` in `Shared/State/Notifications`) | `.ol-toast` | `OlToast`: message, reference, action, `OnDismiss`; `OlToastHost` connects it to the `Notifications` slice (outside `Ui`, which stays free of `IState<>`) | `aria-live="polite"` region, one toast at a time (no overlap), auto-dismiss 6 s (errors stay until closed, `role="alert"`) |
| `OlCard` | RecipeCard · `.ol-card` `__media` `__body` `__overline` `__title` `__meta` `__foot`, `--interactive` | `Href`, `Media`, slots | media 3:2 with `object-fit: cover`; whole card is one link |
| `OlStatTile`, `OlStats` | StatTile · `.ol-stat` | `Label`, `Value`, `Hint` | skeleton variant |
| `OlTable` | CostTable · `.ol-table-wrap` `.ol-table` | columns + rows template; stacked rows on compact | header `scope`, caption (visually hidden) |
| `OlStageItem` | StageItem · `.ol-stage` `.is-running/.is-done/.is-skipped` | stage view model, `OnStart`, `OnComplete`, `OnExtend` | upcoming, running, done, skipped; actions one full-width row |
| `OlDialog` | Dialog · `.ol-dialog` `.ol-scrim` `__handle` | `Open`, `Title`, `OnClose`, `Foot` | bottom sheet on compact, centred on medium+; focus trap, Esc, return focus |
| `OlEmptyState` | EmptyState · `.ol-empty` | `Title`, `Text`, `Action` | |
| `OlSkeleton` | Skeleton · `.ol-skeleton` | `Variant` (text, card, row, stat, chat) , `Count` | `aria-hidden`, parent has `aria-busy="true"` |
| `OlTopBar` | TopBar · `.ol-topbar` | `Title`, `Back`, `Actions` | |
| `BottomBar`, `NavRail`, `Sidebar` (Layout) | Navigation · `.ol-nav--bottom/--rail/--sidebar` | items from `NavItems` | `aria-current="page"` via `NavLink` |
| `ChatThread`, `ChatMessage`, `Composer`, `PlanCard`, `TypingIndicator` (Assistant feature) | AssistantChat · `.ol-chat` `.ol-msg` `.ol-composer` `.ol-plan` `.ol-typing` | | thread starts at the top and grows down; composer sticky above keyboard |
| `DisplaySettingsPanel` (Host) | DisplaySettings | | theme, scale, reduce motion; live preview |

Icons: generate `IconName` and an `Icons.razor` switch from `icons/*.svg` once (inline SVG so they inherit
`currentColor`); do not load them as `<img>`.

## 3. Themes, scale, motion

- `index.html` runs a tiny inline script **before** Blazor starts: reads `oneloaf.display` from `localStorage`
  (`{theme, scale, motion}`), sets `data-theme`, `data-scale`, `data-motion` on `<html>`; `theme = system` →
  `prefers-color-scheme`. No flash of the wrong theme.
- `Display` slice mirrors these values; `SetTheme` / `SetScale` / `SetReduceMotion` effects call `device.js` to update the
  attributes and `localStorage`. Scale steps: 90 / 100 / 125 / 150 (`html { font-size: calc(100% * var(--ol-user-scale)) }`).
- Transitions in `app.css` are wrapped in `@media (prefers-reduced-motion: no-preference)` and disabled when
  `[data-motion="reduce"]`.
- `<meta name="theme-color">` updated by `device.js` for light/dark.

## 4. Size classes and the shell

`device.js` registers `matchMedia` listeners for the size-class queries of `layout-and-devices.md` (`37.5em`, `64em`,
`90em`, `160em`, phone landscape `(orientation: landscape) and (max-height: 30em)`), `pointer: coarse`, `hover`,
`prefers-*`, `horizontal-viewport-segments: 2`, and `visualViewport` resize; it calls a `[JSInvokable]` method on a
`DotNetObjectReference<DeviceContext>` which dispatches `DeviceChanged`.

`AppShell` (Layout) chooses the navigation component from `Display.SizeClass`:

| SizeClass | Navigation | Main area |
|---|---|---|
| Watch | menu button → full-screen list | single column |
| Compact | `OlTopBar` + `BottomBar` (5 items) + avatar menu | single column, bottom padding = bar height + safe area |
| Medium (and phone landscape) | `NavRail` | 2-column grids, list–detail stays stacked unless the page opts in |
| Expanded / Large | `Sidebar` | list–detail split (`.ol-split`) |
| Ultra | `Sidebar` | four panes on the recipe screen (`Calculator-Ultrawide`) |

CSS does all layout (`.ol-shell`, `.ol-split`, `.ol-cards`, `.ol-show-compact`, `.ol-show-medium`); C# only decides which
navigation component and whether a dialog is a sheet. Pre-render the CSS-driven version first so nothing jumps when
`DeviceContext` reports.

## 5. Keyboard and viewport

- `<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, interactive-widget=resizes-content">`
- Full-height screens use `min-height: 100dvh`; the composer and sticky submit bars use
  `padding-bottom: max(var(--space-3), env(safe-area-inset-bottom))`.
- Fallback for browsers without `interactive-widget`: `device.js` sets `--ol-keyboard-inset` from
  `window.innerHeight - visualViewport.height`; the composer adds it to its bottom offset.
- When the composer gets focus the thread scrolls so the last message stays visible (`scrollIntoView({block: "end"})`).
- Chat thread order: oldest at the top; the thread starts at the top of the panel and grows downwards (short threads
  are not anchored to the bottom).

## 6. Accessibility checklist per component / page

- Landmarks: `header`, `nav` (label "Main"), `main` (one per page, `id="main"` + skip link), `aside` for side panels.
- Page title: `<PageTitle>` = "Recipe name · OneLoaf"; focus moves to the `h1` after navigation (`FocusOnNavigate`).
- Forms: label for every field, `aria-describedby` for help and errors, error summary with links to fields on submit.
- Live regions: toasts (`polite`), assistant replies (`polite`), stage timers (no live updates more often than once a
  minute), validation summary (`assertive` only on submit).
- Dialogs: `role="dialog"`, `aria-modal`, labelled by the title, focus trap, Esc closes, focus returns to the opener.
- Lists of actions on cards: one primary per card; destructive actions need confirmation.
- Contrast: only token pairs from the design system (checked); never put `--primary` text on `--surface-sunken`.
- Keyboard: every action reachable; tabs and segmented controls with arrow keys; no drag-only interaction (reordering
  ingredients/stages has Move up / Move down buttons).

## 7. PWA

- `manifest.webmanifest`: name OneLoaf, `display: standalone`, theme colours light/dark, icons 192/512 + maskable
  (from `logos/heart-terra.svg`).
- `service-worker.published.js`: the template's asset cache + a runtime cache for `GET /core/api/v1/recipes*` and
  `/recipes/{id}*` (stale-while-revalidate, max 50 entries, cleared on logout); never caches `auth/*` or non-GET.
- Offline: `Connectivity` slice from `navigator.onLine` + failed fetches; banner from `Recipes-Phone-offline`; write
  actions disabled with a tooltip "You're offline".
- Web Push: Settings → Reminders asks permission only on the user's click, subscribes with the VAPID key from
  `GET /notify/api/v1/push/vapid-public-key`, posts the subscription; the service worker shows the notification and
  focuses / opens `/batches/{id}` or `/starters` from its data.
