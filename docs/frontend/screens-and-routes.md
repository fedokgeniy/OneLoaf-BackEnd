# Screens, routes and story coverage

Mockups are in `docs/frontend/mockups/` (open the `.html` files in a browser; they load the design-system CSS from
`../design-system/`). Each screen has Phone, Phone · dark, Tablet and Desktop boards unless noted. Phone boards show the
first screenful only; content that continues below (more fields, more cost lines, the stage list) belongs to the same
scrolling page.

Auth: **A** = signed-in baker, **P** = public (anonymous allowed), **Adm** = admin role.

## Route table

| Route | Page (feature) | Auth | Mockups | Stories | Endpoints |
|---|---|---|---|---|---|
| `/login` | `LoginPage` (Host) | P | `Login-*` | US-02 | `POST /core/api/v1/auth/login` |
| `/register` | `RegisterPage` (Host) | P | `Register-*` | US-01, LR-01/02 | `POST /auth/register` then login |
| `/` | `HomePage` (Host) | A | `Home-*`, `Home-Phone-16x9` | US-23, US-28, US-31 | `GET /me`, `GET /batches?status=running`, `GET /starters`, `GET /bake-records?pageSize=3`, `GET /bake-records/statistics`, `GET /notify/api/v1/reminders` |
| `/recipes` | `RecipesPage` (Recipes) | A | `Main` (phone), `Recipes-*`, `Recipes-Foldable`, `Recipes-Phone-offline`, `Calculator-Ultrawide` | US-05, US-07, US-37 (filter) | `GET /recipes?search=&includeArchived=&minProteinPercent=&page=` |
| `/recipes/new`, `/recipes/{id}/edit` | `RecipeEditorPage` (Recipes) | A | `RecipeEditor-*` | US-05, US-09, US-10 | `GET /recipes/{id}`, `POST /recipes`, `PUT /recipes/{id}`, `GET /ingredients`, `GET /reference/units`, `/reference/stage-types` |
| `/recipes/{id}` | `RecipeDetailPage` (Recipes) — tabs Ingredients · Stages · Cost · Timeline | A | `Calculator-*`, `Calculator-Desktop-dark` | US-06, US-14–18, US-47–49, US-52, US-11 | `GET /recipes/{id}`, `/scaled?loaves=`, `/cost?loaves=&asOf=`, `/production-figures?loaves=`, `/timeline?start=`, `/start-window?readyAt=` |
| `/recipes/{id}/variant` | `CreateVariantDialog` on the detail page | A | — (DS Dialog) | US-07 | `POST /recipes/{id}/variants` |
| `/recipes/{id}/compare` | `VariantComparePage` (Recipes) | A | `VariantCompare-*` | US-08 | `GET /recipes/{id}/comparison` |
| `/recipes/{id}/publish` | `PublishDialog` (Library, opened from the detail page) | A | `Publish-Tablet` | US-39 | `POST /recipes/{id}/publish` |
| `/ingredients` | `IngredientsPage` (Recipes) — list + detail pane with price history | A | `Ingredients-*` | US-10, US-11, US-12 | `GET/POST/PUT/DELETE /ingredients`, `GET/POST /ingredients/{id}/prices`, `GET /flours`, `GET /warnings` |
| `/settings/kitchen` | `KitchenPage` (Recipes) | A | `Kitchen-*` | US-13, US-47, US-48, US-49 | `GET/PUT /kitchen-settings`, `GET /reference/currencies` |
| `/batches` | `CalendarPage` (Production) — week view | A | `Calendar-*` | US-26 | `GET /batches?from=&to=`, `GET /notify/api/v1/reminders` |
| `/batches/new` | `NewBatchPage` (Production) | A | `NewBatch-*`, `NewBatch-Phone-21x9` | US-20, US-52, US-18, US-29 | `POST /batches/preview` (debounced), `POST /batches`, `GET /recipes/{id}/start-window?readyAt=` (Ready by mode), `GET /recipes`, `GET /starters` |
| `/batches/{id}` | `BatchPage` (Production) — stage list, current stage, stage log, figures | A | `Batch-*`, `Batch-Phone-landscape`, `Batch-Watch` | US-23, US-24, US-21, US-22, US-31, US-25 | `GET /batches/{id}`, `POST …/stages/{stageId}/start\|complete\|skip\|extend\|reset`, `PATCH …/stages/{stageId}`, `GET /batches/{id}/kneading-plan`, `POST /batches/{id}/complete\|cancel\|save-as-variant` |
| `/starters` | `StartersPage` (Production) — cards + feeding log | A | `Starters-*` | US-27, US-28, US-30 | `GET /starters`, `GET/POST /starters/{id}/feedings` |
| `/starters/new`, `/starters/{id}/edit` | `StarterEditorPage` (Production) | A | `NewStarter-*` | US-27, US-29 | `POST/PUT /starters` |
| `/history` | `HistoryPage` (History, lazy) — list + filters + stats | A | `History-*` | US-31, US-33 | `GET /bake-records?recipeId=&from=&to=&minRating=`, `GET /bake-records/statistics` |
| `/history/{id}` | `BakeRecordPage` (History) — rating, comment, stage log, photos, analysis | A | `History-Desktop` (detail pane) | US-32, US-36, US-51 | `GET/PATCH /bake-records/{id}`, `GET /bake-records/{id}/stage-log`, photos, `POST /assistant/api/v1/bake-records/{id}/analysis` |
| `/assistant`, `/assistant/{conversationId}` | `AssistantPage` (Assistant, lazy) | A | `Assistant-*`, `Assistant-Phone-keyboard` | US-34, US-35, US-37, US-38, LR-10, LR-12 | `GET/POST /conversations`, `GET/POST /conversations/{id}/messages`, `POST …/actions/{actionId}/confirm\|decline`, `GET /usage/me` |
| `/library` | `LibraryPage` (Library, lazy) | P | `Library-*` | US-40 | `GET /library?search=&page=` |
| `/library/{id}` | `LibraryDetailPage` (Library) | P (actions A) | `LibraryDetail-*` | US-40, US-41, US-42, LR-11 | `GET /library/{id}`, `GET/POST /library/{id}/comments`, `PUT/DELETE /library/{id}/reactions/me`, `POST /library/{id}/copy`, `POST /library/{id}/reports`, `POST /library/{id}/unpublish` (owner) |
| `/admin/reports` | `ModerationPage` (Library) | Adm | — (DS table + Dialog; no mockup) | LR-11 | `GET /admin/reports`, `POST /admin/reports/{id}/resolve`, `POST /admin/library/{id}/remove` |
| `/settings` | `SettingsPage` (Host) — Display, Reminders, Account | A | `Settings-*` | US-04, US-29, US-50, adaptive UI settings | `GET /me`, `DELETE /me`, `GET /export/*.csv`, push: `GET /notify/api/v1/push/vapid-public-key`, `POST/DELETE /push/subscriptions`, `POST /push/test` |
| `/offline` | `OfflinePage` (Host) | P | `Recipes-Phone-offline` (banner) | US-44 | — |
| `*` | `NotFoundPage` (Host) | P | — | — | — |

Components and states: `Components-States` board = the component library with all interactive states.

## Navigation

| Size class | Pattern | Items |
|---|---|---|
| compact (phones) | bottom bar, 5 items + top bar avatar menu | Home · Recipes · Batches · Starters · Assistant; the avatar menu (top bar "Profile and settings") holds Library, History, Ingredients, Kitchen, Settings, **Sign out** |
| medium (tablets, phone landscape) | navigation rail | the 8 items + Settings + **Sign out** at the bottom of the rail |
| expanded+ (desktop, ultrawide) | espresso sidebar | Home, Recipes, Batches, Starters, Assistant, Library, History, Settings, Sign out |
| watch | single column + menu button | current batch stage only |

## Story coverage (Must = M, Should = S, Could = C)

| Story | P | Screen | Notes |
|---|---|---|---|
| US-01 Register | M | `/register` | after 201 log in automatically; privacy notice checkbox required |
| US-02 Log in / out | M | `/login`, avatar menu, rail, sidebar | same message for wrong e-mail and wrong password (the API already returns one) |
| US-03 Own data only | M | all | 404 from the API → `NotFoundPage`; never show ids from other users |
| US-04 Delete account | S | `/settings` → Account | confirm dialog with password (`DELETE /me` body), then clear session |
| US-05 Recipe CRUD | M | `/recipes`, editor | list: search (debounced), paging ("Show more recipes"), archived filter, filter kept in the address bar; cards show title, "Variant of …", loaves, last change, Archived badge (the list API has no photo, cost or hydration); editor: ingredients and stages as editable lists with add/remove/reorder (buttons + keyboard, no drag-only) |
| US-06 Scale to N | M | recipe detail stepper | `GET /scaled?loaves=`; stepping is debounced (300 ms), 1–1000 loaves as the cost endpoint accepts |
| US-07 Variant | M | detail → Create variant | list shows "Variant of …"; editor marks inherited rows |
| US-08 Compare | S | `/recipes/{id}/compare` | "Changes only / All lines" segmented control |
| US-09 Optional, proof mode + °C | S | editor stage row | chips Warm/Cold + temperature field |
| US-10 Ingredients + units | M | `/ingredients`, editor | unit select from `/reference/units` |
| US-11 Flour catalog | S | `/ingredients` detail, recipe header | protein % shown in the recipe header ("Bread flour · 12.8 % protein"); the list API has no protein, so cards do not show it |
| US-12 Price history | M | `/ingredients` detail | 409 duplicate date → field error |
| US-13 Kitchen | M | `/settings/kitchen` | oven, mixer (bowl capacity), cost settings |
| US-14 Cost | M | detail → Cost tab | lines exactly as returned, including the API's `total` line; total tile = API `total`; incomplete cost lists `missingPrices` with a link to Ingredients |
| US-15 Prices as of | M | detail → "Prices as of" date | `asOf` param |
| US-16 Timeline + ready window | M | detail → Timeline tab, stats | "Start at" (default: next full hour) → `GET /timeline?start=`; Ready tile shows `readyFrom–readyTo` with "next day" |
| US-17 Kneading cycles | M | detail stats | |
| US-18 Starter amount | M | detail stats, new batch | |
| US-47 Packaging | M | cost table line | |
| US-48 Loss | M | cost table line | |
| US-49 Labor | S | cost table line (only when the API returns a `labor` line) + kitchen field | |
| US-52 Plan backwards | S | new batch "Plan by: Start time / Ready by" | `POST /batches/preview` has no `readyAt`: in *Ready by* mode call `GET /recipes/{id}/start-window?readyAt=` for each recipe, show the start window, use the latest common start as `plannedStartAt` (user may pick earlier inside the window), then preview |
| US-19 Oven loading | C | batch figures "(2 oven loads)" | `POST /calculations/oven-loading` |
| US-20 Create batch | M | `/batches/new` | preview on every change (debounce 400 ms) |
| US-23 Start / complete stage | M | `/batches/{id}` | optimistic disable, then reload batch |
| US-21 Adjust / reset stage | S | stage item overflow menu → dialog | `OlDialog` with min/max fields + "Reset to recipe" |
| US-22 Merge kneads | S | batch figures + `kneading-plan` | show "2 cycles · merged: …" explanation from the API |
| US-24 Extend +30 min | S | stage item | |
| US-26 Calendar | S | `/batches` | week navigation, today, status legend |
| US-25 Save as variant | C | batch overflow menu | |
| US-27 Create starter | M | `/starters/new` | |
| US-28 Feeding + next | M | `/starters` log feeding sheet | |
| US-29 Reminders | M | new batch checkbox, starter form, settings → push subscription | Web Push permission asked only after a user gesture |
| US-30 Expected peak | S | starter card | |
| US-31 Auto record | M | history list after batch complete | completing a batch resets the history list and statistics, so the new record appears on the next visit |
| US-32 Rating + comment | M | `/history/{id}` | 1–5 radio group in a fieldset (arrow keys, "4 of 5" labels) plus *No rating*; `PATCH` with null clears a value |
| US-33 Filter history | C | `/history` chips + filters | recipe chips from `/bake-records/statistics`, *Rating 4+* chip (`minRating=4`), *From* / *To* dates; per-recipe statistics table; paging ("Show more bakes") |
| US-51 Crumb photo | C | `/history/{id}` | JPEG / PNG / WebP up to 5 MB (checked before upload, 422 from the API shown inline); photos are fetched with the bearer token and shown as data URLs; delete per photo |
| US-34 Batch from chat | M | `/assistant` | plan card + Confirm / Decline; 409 marks the proposal expired; after Confirm a toast links to `/batches/{id}` and the batch lists reload |
| US-35 Numbers from tools | M | `/assistant` | show `unverifiedNumbers` warning if the API returns any |
| US-36 Analyse bake | M | `/history/{id}` → Ask the assistant | 422 shows the API `detail` and focuses the comment field; 429 / 503 inline; the analysis is stored by the assistant and shown with "Informational only" |
| US-37 Search recipes / flour filter | S | `/assistant`, `/recipes` | `/recipes`: "Minimum flour protein" segmented control (any, ≥ 11, ≥ 12, ≥ 13 %) → `minProteinPercent` |
| US-38 Substitutions | S | `/assistant` | render `disclaimer` under the answer ("Informational only") |
| US-39 Publish / unpublish | S | publish dialog, library detail (owner) | |
| US-40 Browse catalog | S | `/library` | must work signed out: show a "Sign in to copy and comment" bar instead of the app navigation |
| US-41 Comment, react | S | `/library/{id}` | |
| US-42 Copy | C | `/library/{id}` | then navigate to the new recipe |
| LR-11 Report / moderate | — | `/library/{id}` Report, `/admin/reports` | |
| US-43 360 px phone | M | all | Playwright at 360×640 |
| US-44 PWA offline | S | all | |
| US-50 CSV export | C | `/settings` → Export my data | download via `GET /export/*.csv` with the token (fetch → blob → save) |
