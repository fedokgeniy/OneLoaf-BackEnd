-- Query plans of the most frequent queries on the volume data (db/seed/volume_data.sql).
-- Each query is expected to use the index named in the comment; see docs/db/design.md, section "Indexes".
--   psql -h <host> -U oneloaf_app -d oneloaf -f db/queries/explain_plans.sql

\set baker '''00000000-0000-4000-c000-000000000064'''

\echo '1. Price of an ingredient on a date (US-15) -> ux_ingredient_prices_ingredient_id_effective_from'
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY OFF)
SELECT * FROM fn_price_on_date((SELECT id FROM ingredients WHERE owner_id = :baker AND name = 'Flour'), '2026-06-15');

EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY OFF)
SELECT price FROM ingredient_prices
WHERE ingredient_id = (SELECT id FROM ingredients WHERE owner_id = :baker AND name = 'Flour')
  AND effective_from <= date '2026-06-15'
ORDER BY effective_from DESC LIMIT 1;

\echo '2. Production calendar of a baker for three weeks (US-26) -> ix_batches_owner_id_planned_start_at'
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY OFF)
SELECT id, name, status, planned_start_at FROM batches
WHERE owner_id = :baker AND planned_start_at >= '2026-06-01' AND planned_start_at < '2026-06-22'
ORDER BY planned_start_at;

\echo '3. Queue of due reminders (Notification service) -> ix_reminders_status_due_at'
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY OFF)
SELECT id, due_at FROM reminders
WHERE status = 'scheduled' AND due_at <= now() + interval '10 minutes'
ORDER BY due_at LIMIT 100;

\echo '4. Stages of a batch item in order (batch execution screen) -> ix_batch_stages_batch_item_id_sort_order'
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY OFF)
SELECT * FROM batch_stages
WHERE batch_item_id = (SELECT bi.id FROM batch_items bi JOIN batches b ON b.id = bi.batch_id
                       WHERE b.owner_id = :baker ORDER BY b.planned_start_at LIMIT 1)
ORDER BY sort_order;

\echo '5. Recipes of a baker by title (recipe list) -> ix_recipes_owner_id_title'
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY OFF)
SELECT id, title FROM recipes WHERE owner_id = :baker ORDER BY title;

\echo '6. Login by e-mail -> ux_users_normalized_email'
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY OFF)
SELECT id, password_hash FROM users WHERE normalized_email = 'LOAD.BAKER100@EXAMPLE.COM';
