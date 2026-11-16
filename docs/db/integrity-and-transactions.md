# Data integrity and transactions

How the OneLoaf database guarantees correct data and how changes are executed.
Every rule is covered by tests (`db/tests/constraint_tests.sql` - 71 checks, `db/tests/business_tests.sql` - 64).

## 1. Levels of integrity

| Level | Means | Examples |
|---|---|---|
| Domain of a value | Data type | `numeric(12,4)` for prices, `numeric(12,3)` for quantities, `timestamptz` for points in time, `date` for price and bake dates, `uuid` for keys, `jsonb` for documents |
| | `NOT NULL` | All required attributes; NULL is allowed only where a missing value has a meaning (no price, stage not started, no labor rate) |
| | `CHECK` | Ranges (`loss_rate_percent BETWEEN 0 AND 100`, `rating BETWEEN 1 AND 5`, `min <= max`), formats (e-mail, currency code), value lists of statuses, consistency of columns (`done` requires `completed_at`, `removed` requires `removed_reason`), JSON structure, strength of the password hash |
| | Reference tables | `unit_code`, `category_code`, `flour_type_code`, `stage_type_code`, `currency` are foreign keys to reference tables |
| Entity | `PRIMARY KEY` | Every table; composite for `recipe_reactions` |
| | `UNIQUE` | E-mail (regardless of case), names within an owner, price per date, one history record per batch, endpoint of a push subscription |
| | Partial `UNIQUE` | One scheduled feeding reminder per starter; a variant changes a line of the parent at most once |
| References | `FOREIGN KEY` | 53 foreign keys; delete policy in section 2 |
| Business rules | Triggers | Workspace isolation, variant rules, life cycles, immutable snapshots - section 3 |
| Multi-step operations | Transactions and procedures | Section 4 |

## 2. Delete policy

| Relationship | ON DELETE | Meaning |
|---|---|---|
| Whole workspace to `users` | `CASCADE` | Deleting an account deletes all data of the user (US-04, LR-06) |
| Child rows to their parent (recipe lines, batch items and stages, feedings, messages, comments, reactions, photos) | `CASCADE` | Rows do not exist without their parent |
| Optional references: `ingredients.flour_id`, `starters.flour_id`, `batch_items.starter_id`, `batch_stages.recipe_stage_id`, `bake_records.batch_id`, `bake_record_items.recipe_id`, `published_recipes.source_recipe_id`, `recipes.copied_from_published_recipe_id`, `content_reports.resolved_by_id` | `SET NULL` | Deleting the linked object does not destroy history, snapshots and publications |
| References to reference tables | `NO ACTION` | A used reference row cannot be deleted |
| `recipe_ingredients.ingredient_id`, `batch_items.recipe_id`, `recipes.parent_recipe_id` | `NO ACTION DEFERRABLE INITIALLY DEFERRED` | An ingredient used in a recipe, a recipe used in a batch and a parent with variants cannot be deleted. The check runs at the end of the transaction, so deleting a whole account, where the cascade removes both the referencing and the referenced rows, succeeds |

## 3. Rules in triggers

A column constraint checks one row. Rules that need other tables are implemented by `BEFORE` triggers (error before the
write) and `AFTER` triggers (consequences of a change in the same transaction).

**Workspace isolation (US-03, NFR-03).** A foreign key only proves that a row exists. The triggers `trg_*_validate`
additionally prove that the linked row belongs to **the same user**: the ingredient of a recipe line, the recipe and starter
of a batch, the parent of a variant, the flour of an ingredient and starter, the targets of a reminder, the batch of a history
record, the source of a publication. A violation raises `23000`. A bug in the application cannot link data of two bakers.

**Recipe variants.** No cycles and no depth over 10; the yield of a variant equals the yield of its parent; only lines that
exist in the parent (including its own parents) can be replaced or removed; the description, weight and notes of a variant
change only through the checked `variant_overrides`.

**Life cycles.**

```
batch:        planned -> in_progress -> done
              planned -> cancelled
              in_progress -> cancelled                  (done and cancelled are final)
stage:        pending -> running -> done                (driven by the actual times)
              pending -> skipped
publication:  published <-> unpublished
              published | unpublished -> removed        (final)
```

- A batch is created only as `planned`; an invalid transition raises `23514`.
- The start of the first stage moves the batch to `in_progress` and sets `actual_start_at`.
- The transition to `done` sets `completed_at` and **in the same transaction** creates the history record with a cost
  snapshot on the bake date (US-31); `done` and `cancelled` cancel the scheduled reminders of the batch.
- A starter feeding computes the next feeding and the peak (US-28, US-30) and replaces the reminder (US-29).
- A published snapshot is immutable; a publication removed by a moderator cannot be restored.

## 4. Transactions

**Isolation level: READ COMMITTED** (the PostgreSQL default). The data of a baker is changed mostly by the baker, and
conflicting concurrent changes are rare; where they are possible, row locks and unique constraints are used (see below).
Every statement is atomic: together with all fired triggers and cascades it either runs completely or leaves no trace.

**Atomic operations**

| Operation | What is in one transaction | How it is ensured |
|---|---|---|
| Saving a recipe (US-05) | recipe + lines + stages | One `SaveChanges` transaction of EF Core in the Core API |
| Batch plan (US-20, UC-01 step 8) | items; stages from the recipe; kneading cycles; starter; reminders; ready window; cost | `CALL sp_build_batch_plan` - one statement; the batch and item rows are locked with `SELECT ... FOR UPDATE` so that two requests cannot rebuild the plan at the same time |
| Stage extension (US-24) | the stage; all following stages; reminders; ready window of the batch | `CALL sp_extend_batch_stage`; the stage is locked with `FOR UPDATE` |
| Finishing a batch (US-31) | status, `completed_at`; history record and its items with the cost; cancelling reminders | `UPDATE batches SET status = 'done'` and triggers in the same statement |
| New price (US-12) | new history row | One insert; uniqueness of `(ingredient_id, effective_from)` prevents two prices on a date even for concurrent requests |
| Feeding (US-28, US-29) | feeding record; schedule; cancelling the old and creating the new reminder | Insert and triggers; the partial unique index guarantees one scheduled reminder |
| Publishing (US-39) | building the JSON snapshot from the working tables and inserting it | `CALL sp_publish_recipe`; the snapshot is consistent with the recipe at the moment of the statement |
| Account deletion (US-04) | all rows of the user in all tables | Cascade and deferred protective keys checked at commit |
| Anonymization (GDPR) | deleting the workspace, replacing personal data and the password hash | `CALL sp_anonymize_user` |
| Test data | the whole data set | `db/seed/test_data.sql` runs in `BEGIN ... COMMIT` |

**Protection against races.** Where two concurrent requests could create a duplicate, the database decides, not a check in
the application: one history record per batch (`UNIQUE` and `ON CONFLICT DO NOTHING` in `fn_create_bake_record`), one price
per date, one scheduled feeding reminder, one reaction per user and publication, a unique e-mail.

**Deferred constraints.** Three protective foreign keys are `DEFERRABLE INITIALLY DEFERRED` and are checked at `COMMIT`.
The tests force the check earlier with `SET CONSTRAINTS ALL IMMEDIATE`.

## 5. Database errors and API responses

The Core API maps SQLSTATE codes to HTTP responses, so a violated database rule is a clear error for the client, not a crash:

| SQLSTATE | Source | HTTP |
|---|---|---|
| `23505` unique_violation | duplicate e-mail, name, price on a date | 409 Conflict |
| `23503` foreign_key_violation | reference to a missing row, deletion of a used row | 409 Conflict |
| `23514` check_violation | range, format, invalid status transition, variant rule | 422 Unprocessable Entity |
| `23000` integrity_constraint_violation | reference to data of another user | 403 Forbidden |
| `22023` invalid_parameter_value | invalid procedure argument (number of loaves, minutes) | 400 Bad Request |
| `P0002` no_data_found | object of a procedure not found | 404 Not Found |
| `55000` object_not_in_prerequisite_state | action not allowed in the current status (re-planning a started batch) | 409 Conflict |
| `42501` insufficient_privilege | missing privilege of the role | 500 (configuration error, does not occur normally) |

## 6. Data quality that cannot be a constraint

Some rules depend on the current time or on data of several tables and cannot be a constraint: an ingredient of an active
recipe has no price, a price is in another currency, a recipe has no ingredients or stages, a planned batch is overdue, a
finished batch has an incomplete stage log (KPI-1), a starter has not been fed for a long time. They are found by the view
`v_data_quality_issues`: the application shows warnings to the baker (for example UC-01 A4 - incomplete cost), and the report
for support is built under the role `app_read`. The data quality expectations of every table are in the
[data dictionary](data-dictionary.md) (marks [C] and [M]).
