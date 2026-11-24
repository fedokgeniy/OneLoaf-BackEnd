# OneLoaf database design

This document describes the design decisions: choice of the DBMS, data layers, normalization, the model of recipe
variants, reference data, JSON columns, indexes, logic in the database and versioning of the structure.
Tables and columns are described in the [data dictionary](data-dictionary.md), relationships in the
[logical schema](logical-schema.md), integrity and transactions in [integrity-and-transactions.md](integrity-and-transactions.md),
roles and data protection in [security.md](security.md).

## Contents

1. [Choice of the DBMS](#choice-of-the-dbms)
2. [Data layers](#data-layers)
3. [Normalization](#normalization)
4. [Recipes and variants](#recipes-and-variants)
5. [Reference tables and closed value sets](#reference-tables-and-closed-value-sets)
6. [JSON columns](#json-columns)
7. [Indexes](#indexes)
8. [Logic in the database](#logic-in-the-database)
9. [Versioning of the structure](#versioning-of-the-structure)

## Choice of the DBMS

**PostgreSQL 16**, a relational DBMS for a transactional (OLTP) workload. The choice follows from the domain:

| Property of the domain | What is needed from the DBMS | How PostgreSQL provides it |
|---|---|---|
| The data of a baker is strongly connected: recipe - lines - ingredients - prices; batch - items - stages | Foreign keys, cascading deletes, joins | Full referential integrity, `ON DELETE CASCADE / SET NULL`, deferred checks |
| Money and quantities (cost, grams, percentages) | Exact arithmetic | `numeric(p,s)` without rounding errors |
| Multi-step operations (batch plan, finishing a batch, deleting an account) | ACID transactions | Transactions, procedures, row locks |
| Isolation of user data (US-03) | Rules that the application cannot bypass | Triggers, CHECK constraints, roles and privileges on tables and columns |
| Recipe snapshots in the library, log of assistant tools | Documents inside a relational model | `jsonb` with structure checked by functions |
| Calculations on a date and time windows | Dates, intervals, window functions | `date`, `timestamptz`, `interval`, `WINDOW` |

**One DBMS, no NoSQL or analytical DBMS.** All data of the project is transactional data of the users of one application.
The volume is small (hundreds of bakers, thousands of batches a year), and the analytics consist of the personal statistics
of a baker and reports for support, which fit into views and one materialized view (see the [volume test](#indexes)).
A document DBMS would not provide the referential integrity that the cost calculation and the data isolation rely on;
ClickHouse or another warehouse would add ETL and a second source of truth without any gain at this volume.
SQLite is not suitable: it has no roles and privileges, no `jsonb`, no deferred constraints and no procedures; therefore
PostgreSQL is used in every environment, including local development (Docker).

## Data layers

The project is an OLTP system with one storage layer. A `raw - staging - data mart` pipeline is not needed: the data is not
loaded from external sources but entered by users through the application, and it passes the constraints and triggers
immediately, so it is already clean. Instead of data marts there is a layer of views on top of the normalized tables:

| Layer | Objects | Used by |
|---|---|---|
| Storage (3NF) | 31 tables | Core API, AI Assistant, Notification service (role `app_write`) |
| Logic | functions, procedures, triggers | all services; calculations for the assistant tools |
| Views | `v_recipe_effective_ingredients`, `v_ingredient_current_prices`, `v_batch_stage_log`, `v_batch_log_completeness`, `v_public_catalog`, `v_data_quality_issues`, `v_users_masked` | application screens, assistant, reports (`app_read`) |
| Aggregates | `mv_bake_statistics` (materialized) | history screen, analysis by the assistant, reports |

## Normalization

The schema is in **third normal form**.

- **1NF.** All values are atomic: recipe ingredients and stages are rows of separate tables, not lists in a column; the price
  history is one row per change. The exception is three `jsonb` columns that are complete documents by nature
  (see [JSON columns](#json-columns)).
- **2NF.** In tables with a composite key no non-key attribute depends on a part of the key: in `recipe_reactions`
  (PK `published_recipe_id, user_id`) the column `reaction` depends on the whole pair. Other tables have a surrogate `uuid` key,
  and natural keys are enforced by unique indexes (`users.normalized_email`, `(owner_id, name)` of flours, ingredients and
  starters, `(ingredient_id, effective_from)` of prices).
- **3NF.** There are no transitive dependencies: names and properties of units, currencies, stage types and categories live in
  reference tables and are not repeated in rows; the owner of child rows (`recipe_ingredients`, `batch_stages`,
  `starter_feedings`) is not stored in them but derived through the parent; a price is not stored in the recipe but taken from
  the history for the required date.

**Deliberate denormalization.** Several columns could formally be derived from other data, but they are stored because they
record the state at the moment of an event and must not change retroactively. All of them are written by the database,
not by the application:

| Column | Derivable from | Why it is stored |
|---|---|---|
| `batch_stages.name`, `stage_type_code`, `planned_min_minutes`, `planned_max_minutes` | recipe stage | The plan of a batch must not change when the baker edits the recipe; a duration can be changed for one batch only (US-21) |
| `batches.total_cost`, `planned_ready_from/to` | recipes, prices, stages | Snapshot of the plan at the time it was saved; recalculated only by `sp_build_batch_plan` |
| `bake_records.total_loaves`, `total_cost`; `bake_record_items.recipe_title`, `cost` | items, recipe, prices | The baking history is a fact of the past: it must not change when the recipe is edited or deleted or when prices change (US-31) |
| `starter_feedings.next_feeding_at`, `expected_peak_at` | feeding interval and rise time of the starter | The schedule is fixed at feeding time; changing the starter settings does not rewrite past records |
| `reminders.owner_id` | starter / batch | The Notification service selects the queue by owner without joins; consistency is checked by the trigger `trg_reminders_validate` |
| `published_recipes.snapshot` | recipe and its lines | The published version is immutable and separate from the working copy (US-39) |

## Recipes and variants

Requirement US-07: a variant of a recipe stores **only the changes** and inherits everything else.

- `recipes.parent_recipe_id` references the parent; the chain can have several levels (a variant of a variant); cycles and a
  depth over 10 are rejected by a trigger.
- Changed ingredients and stages are ordinary rows of `recipe_ingredients` / `recipe_stages` of the variant:
  a row with `replaces_item_id` **replaces** a row of the parent, a row with `is_removed = true` **removes** it, a row without a
  reference is **added**.
- Changed scalar fields (description, loaf weight, notes) are stored in `variant_overrides`. A present key means the value is
  changed (also to `null`); a missing key means it is inherited. The own `description`, `loaf_weight_g`, `notes` of a variant are
  empty (CHECK), so the source of a value is always unambiguous.
- The yield (`yield_loaves`) of a variant equals the yield of its parent: inherited quantities are written for the same yield.

The full composition is assembled by the functions `fn_recipe_effective_ingredients`, `fn_recipe_effective_stages`,
`fn_recipe_effective_header` and the view `v_recipe_effective_ingredients` (with the flag `is_inherited` for the comparison
screen, US-08). The replacement references are ordinary foreign keys, so comparing a variant with its parent is a join,
and deleting a line of the parent deletes its replacement by cascade.

## Reference tables and closed value sets

- **Reference tables** are used for classifications that have attributes and can grow: `units` (dimension, conversion factor),
  `currencies` (digits after the decimal point), `stage_types` (fermentation, default equipment), `ingredient_categories`
  (flour, liquid), `flour_types`. References are foreign keys with `NO ACTION`: a used row cannot be deleted. The reference data
  is fixed, inserted by the `InitialSchema` migration (EF Core `HasData`) and changed only by a new migration; the services
  can only read it.
- **CHECK with a list of values** is used for closed sets of states that the code and triggers are built on: statuses of
  batches, stages, reminders, publications and reports; roles; kinds of reminders and reactions; proofing and feeding modes.
  A new value here always requires a code change, so it is added by a migration together with the code.
  Values are stored as `snake_case` text and are readable without decoding.

## JSON columns

JSON is used in three columns. For each of them the structure is documented and **checked by the database**, so the JSON is
not an unstructured document:

| Column | Why a document and not tables | Structure and check |
|---|---|---|
| `recipes.variant_overrides` | It must distinguish "value not changed" (no key) from "changed to empty" (key = `null`); columns cannot express this | Object with optional keys `description` (string or null), `loafWeightG` (number > 0 or null), `notes` (string or null). CHECK `fn_is_valid_variant_overrides`; variants only |
| `published_recipes.snapshot` | Immutable copy of a recipe at publication time: read as a whole, never updated in parts, independent of the working tables of the author | `schemaVersion` = 1, `yieldLoaves` (integer > 0), `loafWeightG`, `ingredients[]` (at least 1; `name`, `category`, `unit`, `quantity` >= 0, `isOptional`), `stages[]` (`name`, `stageType`, `minMinutes` <= `maxMinutes`, `temperatureC`, `proofMode`, `isOptional`). CHECK `fn_is_valid_recipe_snapshot`; built by `sp_publish_recipe`; changes are blocked by a trigger |
| `chat_messages.tool_payload` | Arguments and results of the backend tool calls of the assistant; every tool has its own schema defined in the AI Assistant service. It is a log for audit and for checking US-35 (numbers in the answer = numbers from the tool); no queries are built on it | JSON object, only together with `tool_name` (CHECK `ck_chat_messages_tool_payload`) |

If the snapshot structure has to change, `schemaVersion` is increased and the check function accepts both versions.

## Indexes

Every index serves a specific query of the application or an integrity check.
Primary keys and unique constraints are indexed automatically; besides them:

| Index | Query / purpose |
|---|---|
| `ux_ingredient_prices_ingredient_id_effective_from` | Price on a date (US-15): `WHERE ingredient_id = ? AND effective_from <= ? ORDER BY effective_from DESC LIMIT 1` - a backward index scan returning one row. Also forbids two prices on the same date |
| `ix_batches_owner_id_planned_start_at` | Weekly production calendar of a baker (US-26) - a time range within an owner |
| `ix_batches_owner_id_status` | Active batches of a baker (planned / in_progress) |
| `ix_reminders_status_due_at` | Queue of the Notification service: `WHERE status = 'scheduled' AND due_at <= now() ORDER BY due_at LIMIT n` |
| `ux_reminders_starter_id_scheduled_feeding` (partial) | At most one scheduled feeding reminder per starter |
| `ix_batch_stages_batch_item_id_sort_order`, `ix_batch_items_batch_id_sort_order`, `ix_recipe_ingredients_recipe_id_sort_order`, `ix_recipe_stages_recipe_id_sort_order` | Ordered child rows (batch screen, recipe card) without sorting |
| `ix_recipes_owner_id_title` | Recipe list of a baker by title |
| `ix_bake_records_owner_id_baked_on` | Baking history by date (US-33) |
| `ix_chat_messages_conversation_id_created_at`, `ix_chat_conversations_owner_id_updated_at` | Message feed of a conversation and list of conversations |
| `ix_published_recipes_status_published_at`, `ix_published_recipes_title` | Public catalog: newest publications, search by title (US-40) |
| `ux_users_normalized_email` | Login by e-mail regardless of case; no duplicates |
| `ux_recipe_ingredients_recipe_id_replaces_item_id`, `ux_recipe_stages_recipe_id_replaces_stage_id` (partial) | A variant changes a line of the parent at most once |
| `ux_mv_bake_statistics_owner_id_recipe_key` | Required by `REFRESH MATERIALIZED VIEW CONCURRENTLY` (refresh without blocking readers) |
| Indexes on the other foreign keys (`ix_<table>_<fk>`) | Cascading account deletion and `NO ACTION` checks do not scan child tables fully; joins by key |

**Volume test.** `db/seed/volume_data.sql` adds 300 bakers with a year of activity (about 22000 prices, 6000 batches,
24000 stages, 6000 reminders), and `db/queries/explain_plans.sql` shows the plans. Result (run in CI with `VOLUME=1`):

| Query | Plan |
|---|---|
| Price on a date | `Index Scan Backward using ux_ingredient_prices_ingredient_id_effective_from`, 1 row |
| Calendar of a baker for 3 weeks | `Index Scan using ix_batches_owner_id_planned_start_at` |
| Reminder queue | `Limit` over `Index Scan using ix_reminders_status_due_at` |
| Stages of a batch item | `Bitmap Index Scan on ix_batch_stages_batch_item_id_sort_order` |
| Recipes of a baker by title | `Index Scan using ix_recipes_owner_id_title` (no sort) |
| Login by e-mail | `Index Scan using ux_users_normalized_email` |

## Logic in the database

The database implements the part of the business logic that must work **the same way for every service** and cannot be
bypassed: integrity rules, life cycles, deterministic calculations and multi-step operations. The full catalog with
descriptions is in the [data dictionary](data-dictionary.md#functions-and-procedures); the SQL is in
`src/OneLoaf.Infrastructure/Persistence/Migrations/Sql/`.

| Kind | Objects | Requirements |
|---|---|---|
| Calculation functions | `fn_price_on_date`, `fn_recipe_cost`, `fn_recipe_timeline`, `fn_recipe_ready_window`, `fn_recipe_start_window`, `fn_kneading_cycles`, `fn_starter_amount_g`, `fn_dough_weight_g` | US-14 - US-18, US-47 - US-49, US-52. The tools of the AI assistant call these functions through the Core API: numbers come from deterministic code, not from the model (US-35, NFR-08) |
| Recipe functions | `fn_recipe_effective_ingredients`, `fn_recipe_effective_stages`, `fn_recipe_effective_header` | US-07, US-08 |
| Check functions (in CHECK) | `fn_is_strong_password_hash`, `fn_is_valid_variant_overrides`, `fn_is_valid_recipe_snapshot` | NFR-04, JSON structure |
| Integrity triggers | 12 triggers for workspace isolation and variant consistency | US-03, NFR-03, US-07 |
| Process triggers | life cycle of batches and stages, automatic history record, feeding schedule, publication rules, `updated_at` | US-20, US-23, US-28 - US-31, US-39 |
| Procedures | `sp_build_batch_plan`, `sp_extend_batch_stage`, `sp_publish_recipe`, `sp_anonymize_user` | US-16 - US-20, US-24, US-29, US-39, GDPR |
| Views | 8 views and `mv_bake_statistics` | US-07, US-12, US-23, US-33, US-36, US-40, KPI-1, data quality, low stock, masking |
| Stock of ingredients | `fn_stock_balance`, `fn_stock_balance_at`, `fn_starter_feeding_cost`, `fn_batch_ingredient_needs`, `fn_batch_stock_check`, `sp_apply_batch_stock`, `sp_return_batch_stock`, trigger `trg_starter_feedings_stock` | Purchases, consumption by batches and feedings, returns, ripening of a starter |
| Starter as an ingredient | triggers `trg_starters_ingredient_before`, `trg_starters_name_sync`, `trg_ingredients_starter_name_sync`, `trg_starters_ingredient_after_delete` | One record for the baker: the starter and its ingredient share the name and the life cycle |

Every value is checked by `db/tests/business_tests.sql` against the acceptance criteria of the business analysis
(for example, the cost of 3 loaves is 11.22 and equals the sum of the lines; the ready window for a start at 12:00 is
02:00-04:00).

## Versioning of the structure

The database structure is created and changed **only by EF Core migrations** stored in Git.

**Migration chain**

| # | Migration | What it does |
|---|---|---|
| 1 | `InitialSchema` | 29 tables, keys, indexes, CHECK constraints, reference data (generated by EF Core from the model) |
| 2 | `DeferrableProtectiveForeignKeys` | Three protective foreign keys are checked at the end of the transaction (account deletion) |
| 3 | `CoreFunctions` | Check and calculation functions, variant functions |
| 4 | `IntegrityTriggers` | CHECK constraints with functions, isolation and process triggers, `updated_at` |
| 5 | `BusinessProcedures` | Procedures for the batch plan, stage extension, publishing, anonymization |
| 6 | `ReportingViewsAndMasking` | Masking, views, materialized statistics |
| 7 | `AccessPrivileges` | Privileges of the roles and default privileges for future objects |
| 8 | `IngredientStock` | Stock ledger `stock_movements`, stock switch and reorder level of an ingredient, ingredients a starter is fed with, ingredient scale of a batch; stock functions, procedures, trigger and the view `v_low_stock`; the batch plan takes the scale into account |
| 9 | `StarterIngredients` | A starter is a kind of ingredient (`starters.ingredient_id`, one name kept in step by triggers, usable share); ripened starter becomes stock at its peak (`stock_movements.available_at`, `fn_stock_balance_at`); the batch stock check uses the stock at the planned start; photo columns of ingredients and recipes |
| 10 | `RefreshTokens` | Table `refresh_tokens`: SHA-256 hashes of the rotating refresh tokens with the family of one sign-in, the expiry and the rotation and revocation times; `sp_anonymize_user` also deletes the tokens of the user |
| 11 | `StarterFeedingIngredients` | A feeding names the flour and the water it used (`starter_feedings.flour_ingredient_id`, `water_ingredient_id`, default: the usual ones of the starter); `fn_starter_feeding_cost` and the feeding trigger store the cost of the fed starter as the price of its ingredient |
| 12 | `StarterFeedingReminder` | The reminder of a batch item with a starter tells the baker to feed it: `sp_build_batch_plan` writes it for the time to peak of the starter before the first kneading stage (the lead the Core API stores in `batch_items.starter_reminder_minutes`) |

**Rules**

1. **The model is the source of the table structure.** A change of an entity or configuration is followed by
   `dotnet ef migrations add <Name>`; EF Core generates the migration and updates the model snapshot
   (`OneLoafDbContextModelSnapshot.cs`).
2. **Programmable objects** (functions, triggers, views, privileges) are written as SQL scripts in
   `Persistence/Migrations/Sql/<MigrationName>/` and executed by their migration. A script belongs to one migration;
   a change of a function or view is a new `CREATE OR REPLACE` script in a new migration.
3. **A released migration is never edited.** A fix is a new migration. The first seven migrations were assembled
   before the first deployment of the database; further changes only add migrations, as `IngredientStock` does: it
   replaces `sp_build_batch_plan` with `CREATE OR REPLACE` instead of editing the script of `BusinessProcedures`.
4. **Every migration has a `Down` method**; rollback: `dotnet ef database update <PreviousMigration>`.
5. **The history of applied migrations** is stored in the table `__ef_migrations_history` (identifier and EF Core version of
   every applied migration); migrations are applied strictly in order and never twice.
6. **The DDL script `db/schema.sql`** is an idempotent script of all migrations (`dotnet ef migrations script --idempotent`),
   regenerated on every change. It can be run without .NET; already applied migrations are skipped.
7. **CI checks** that all migrations roll back with their `Down` methods to an empty database and apply again, that
   `schema.sql` matches the migrations, that the model has no changes without a migration (`has-pending-model-changes`),
   that the database built from the migrations and the one built from `schema.sql` are identical, that the data dictionary
   and the logical schema are generated from the current schema, and runs all tests on a clean database.
8. **New objects and privileges.** Default privileges give `app_write` access to new tables and functions, `app_read` gets
   nothing; the audit `db/tests/security_audit.sql` fails if no access decision is recorded for a new table
   (see [security.md](security.md)).
