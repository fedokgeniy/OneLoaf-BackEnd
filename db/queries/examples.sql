-- Typical queries of the services, written against the functions and views of the database.
-- Run after db/seed/test_data.sql (any role with access, e.g. oneloaf_app):
--   psql -h <host> -U oneloaf_app -d oneloaf -f db/queries/examples.sql

\set anna '''00000000-0000-4000-a001-0000000000a1'''
\set r1   '''00000000-0000-4000-2001-000000000001'''
\set r2   '''00000000-0000-4000-2001-000000000002'''

-- 1. Recipe card of a variant: full ingredient list, inherited lines marked (US-07, US-08)
SELECT ingredient_name, quantity, unit_code, is_optional, is_inherited
FROM v_recipe_effective_ingredients
WHERE recipe_id = :r2
ORDER BY sort_order;

-- 2. Cost of 3 loaves with today's prices and with prices as of 15 September (US-14, US-15)
SELECT line_no, line_type, description, quantity, unit_code, unit_price, amount, currency, is_complete
FROM fn_recipe_cost(:r1, 3, current_date);

SELECT 'today' AS prices, amount FROM fn_recipe_cost(:r1, 3, current_date) WHERE line_type = 'total'
UNION ALL
SELECT '2026-09-15', amount FROM fn_recipe_cost(:r1, 3, '2026-09-15') WHERE line_type = 'total';

-- 3. Timeline for a start tomorrow at 12:00 and the start window for "ready at 08:00" (US-16, US-52)
SELECT sort_order, name, planned_start_at, planned_end_min_at, planned_end_max_at
FROM fn_recipe_timeline(:r1, date_trunc('day', now()) + interval '1 day 12 hours');

SELECT * FROM fn_recipe_start_window(:r1, date_trunc('day', now()) + interval '1 day 8 hours');

-- 4. Kneading cycles and starter for 8 loaves (US-17, US-18)
SELECT fn_dough_weight_g(:r1, 8) AS dough_g, fn_kneading_cycles(:r1, 8) AS kneads, fn_starter_amount_g(:r1, 8) AS starter_g;

-- 5. Weekly production calendar of a baker (US-26); uses ix_batches_owner_id_planned_start_at
SELECT name, status, planned_start_at, planned_ready_from, planned_ready_to, total_cost
FROM batches
WHERE owner_id = :anna
  AND planned_start_at >= date_trunc('week', now()) - interval '1 week'
  AND planned_start_at <  date_trunc('week', now()) + interval '2 weeks'
ORDER BY planned_start_at;

-- 6. Stage log with deviations for the analysis by the assistant (US-36)
SELECT batch_name, recipe_title, stage_name, planned_min_minutes, planned_max_minutes, extended_minutes,
       actual_minutes, deviation_minutes, actual_temperature_c
FROM v_batch_stage_log
WHERE owner_id = :anna AND batch_status = 'done'
ORDER BY batch_item_id, sort_order;

-- 7. KPI-1: share of finished batches with a complete stage log
SELECT round(100.0 * count(*) FILTER (WHERE is_complete) / nullif(count(*), 0), 1) AS kpi1_percent
FROM v_batch_log_completeness
WHERE status = 'done';

-- 8. Baking history with filters (US-33) and statistics per recipe
SELECT br.baked_on, bri.recipe_title, bri.loaves, bri.cost, br.rating
FROM bake_records br JOIN bake_record_items bri ON bri.bake_record_id = br.id
WHERE br.owner_id = :anna AND br.rating >= 3
ORDER BY br.baked_on DESC;

SELECT recipe_title, bakes_count, loaves_total, avg_rating, avg_cost_per_loaf, last_baked_on
FROM mv_bake_statistics WHERE owner_id = :anna;

-- 9. Notification service: due reminders (queue), uses ix_reminders_status_due_at
SELECT id, kind, due_at, channel, message
FROM reminders
WHERE status = 'scheduled' AND due_at <= now() + interval '1 day'
ORDER BY due_at
LIMIT 100;

-- 10. Public catalog (US-40) and warnings for the baker (data quality)
SELECT title, author_name, yield_loaves, ingredient_count, comment_count, reaction_count
FROM v_public_catalog ORDER BY published_at DESC;

SELECT issue_code, entity_type, details FROM v_data_quality_issues WHERE owner_id = :anna;

-- 11. Price history of an ingredient with the change in percent (window function)
SELECT effective_from, price, per_quantity,
       round(100 * (price / lag(price) OVER w - 1), 1) AS change_percent
FROM ingredient_prices
WHERE ingredient_id = '00000000-0000-4000-1001-000000000001'
WINDOW w AS (ORDER BY effective_from)
ORDER BY effective_from;
