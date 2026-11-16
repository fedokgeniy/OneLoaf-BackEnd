-- Views that hide recurring joins and calculations from the services.

-- Current price of every ingredient (US-12): the price valid today.
CREATE VIEW v_ingredient_current_prices AS
SELECT i.owner_id,
       i.id                          AS ingredient_id,
       i.name                        AS ingredient_name,
       i.unit_code,
       p.price,
       p.per_quantity,
       round(p.price / p.per_quantity, 6) AS unit_price,
       p.currency,
       p.effective_from
FROM ingredients i
LEFT JOIN LATERAL fn_price_on_date(i.id, current_date) p ON true;

COMMENT ON VIEW v_ingredient_current_prices IS 'Price of each ingredient valid today; NULL price = no price yet (US-12).';

-- Full ingredient list of every recipe and variant (US-07, US-08).
CREATE VIEW v_recipe_effective_ingredients AS
SELECT r.owner_id,
       r.id                         AS recipe_id,
       r.title                      AS recipe_title,
       r.parent_recipe_id,
       e.line_id,
       e.ingredient_id,
       i.name                       AS ingredient_name,
       i.category_code,
       e.quantity,
       i.unit_code,
       e.is_optional,
       e.sort_order,
       (e.origin_recipe_id <> r.id) AS is_inherited
FROM recipes r
CROSS JOIN LATERAL fn_recipe_effective_ingredients(r.id) e
JOIN ingredients i ON i.id = e.ingredient_id;

COMMENT ON VIEW v_recipe_effective_ingredients IS
    'Effective ingredient lines of every recipe; is_inherited = line comes from a parent recipe (US-07, US-08).';

-- Stage log with deviations from the plan (US-23, US-36, KPI-1).
-- deviation_minutes: how much the actual duration is outside the planned range of the stage
-- [planned_min_minutes, planned_max_minutes + extended_minutes]
-- (positive = longer than planned, negative = shorter than planned, 0 = within the range).
CREATE VIEW v_batch_stage_log AS
SELECT b.owner_id,
       b.id                                  AS batch_id,
       b.name                                AS batch_name,
       b.status                              AS batch_status,
       bi.id                                 AS batch_item_id,
       h.title                               AS recipe_title,
       bs.id                                 AS batch_stage_id,
       bs.sort_order,
       bs.name                               AS stage_name,
       bs.stage_type_code,
       bs.status                             AS stage_status,
       bs.planned_start_at,
       bs.planned_end_min_at,
       bs.planned_end_max_at,
       bs.actual_start_at,
       bs.actual_end_at,
       bs.extended_minutes,
       bs.actual_temperature_c,
       round(extract(epoch FROM bs.actual_start_at - bs.planned_start_at) / 60)    AS start_delay_minutes,
       bs.planned_min_minutes,
       bs.planned_max_minutes,
       a.actual_minutes,
       CASE
           WHEN a.actual_minutes IS NULL THEN NULL
           WHEN a.actual_minutes > bs.planned_max_minutes + bs.extended_minutes
               THEN a.actual_minutes - (bs.planned_max_minutes + bs.extended_minutes)
           WHEN a.actual_minutes < bs.planned_min_minutes
               THEN a.actual_minutes - bs.planned_min_minutes
           ELSE 0
       END                                   AS deviation_minutes
FROM batches b
JOIN batch_items bi ON bi.batch_id = b.id
JOIN batch_stages bs ON bs.batch_item_id = bi.id
CROSS JOIN LATERAL (SELECT round(extract(epoch FROM bs.actual_end_at - bs.actual_start_at) / 60)::integer AS actual_minutes) a
CROSS JOIN LATERAL fn_recipe_effective_header(bi.recipe_id) h;

COMMENT ON VIEW v_batch_stage_log IS 'Planned vs actual stage times with start delay and deviation from the planned window (US-23, US-36).';

-- KPI-1: share of finished batches with a complete stage log.
CREATE VIEW v_batch_log_completeness AS
SELECT b.owner_id,
       b.id                                                                    AS batch_id,
       b.status,
       b.completed_at,
       count(bs.id)                                                            AS stages_total,
       count(bs.id) FILTER (WHERE bs.status = 'skipped'
                               OR (bs.actual_start_at IS NOT NULL AND bs.actual_end_at IS NOT NULL)) AS stages_logged,
       count(bs.id) > 0
       AND count(bs.id) = count(bs.id) FILTER (WHERE bs.status = 'skipped'
                               OR (bs.actual_start_at IS NOT NULL AND bs.actual_end_at IS NOT NULL)) AS is_complete
FROM batches b
LEFT JOIN batch_items bi ON bi.batch_id = b.id
LEFT JOIN batch_stages bs ON bs.batch_item_id = bi.id
GROUP BY b.owner_id, b.id, b.status, b.completed_at;

COMMENT ON VIEW v_batch_log_completeness IS 'Per batch: number of stages and of stages with full actual times; basis of KPI-1.';

-- Public catalog (US-40): only published snapshots and public profile data, no private tables exposed.
CREATE VIEW v_public_catalog AS
SELECT pr.id                                         AS published_recipe_id,
       pr.title,
       pr.description,
       u.display_name                                AS author_name,
       pr.published_at,
       (pr.snapshot ->> 'yieldLoaves')::integer      AS yield_loaves,
       jsonb_array_length(pr.snapshot -> 'ingredients') AS ingredient_count,
       jsonb_array_length(pr.snapshot -> 'stages')   AS stage_count,
       (SELECT count(*) FROM recipe_comments c
        WHERE c.published_recipe_id = pr.id AND NOT c.is_removed) AS comment_count,
       (SELECT count(*) FROM recipe_reactions r WHERE r.published_recipe_id = pr.id) AS reaction_count
FROM published_recipes pr
JOIN users u ON u.id = pr.author_id
WHERE pr.status = 'published';

COMMENT ON VIEW v_public_catalog IS 'Published recipes with author name and comment/reaction counts (US-40).';

-- Data quality issues that constraints cannot express (they depend on time or on several tables).
-- Used by the application to warn the baker and by monitoring (see docs/db/data-dictionary.md).
CREATE VIEW v_data_quality_issues AS
-- an ingredient used in an active recipe has no price today -> cost is incomplete (UC-01, A4)
SELECT DISTINCT r.owner_id, 'missing_price'::text AS issue_code, 'ingredient'::text AS entity_type,
       i.id AS entity_id, format('No price for "%s" used in "%s"', i.name, r.title) AS details
FROM recipes r
CROSS JOIN LATERAL fn_recipe_effective_ingredients(r.id) e
JOIN ingredients i ON i.id = e.ingredient_id
WHERE NOT r.is_archived AND NOT e.is_optional
  AND NOT EXISTS (SELECT 1 FROM fn_price_on_date(i.id, current_date))
UNION ALL
-- the latest price is in another currency than the kitchen settings
SELECT i.owner_id, 'price_currency_mismatch', 'ingredient', i.id,
       format('Price of "%s" is in %s, kitchen currency is %s', i.name, p.currency, ks.currency)
FROM ingredients i
JOIN kitchen_settings ks ON ks.user_id = i.owner_id
CROSS JOIN LATERAL fn_price_on_date(i.id, current_date) p
WHERE p.currency <> ks.currency
UNION ALL
SELECT r.owner_id, 'recipe_without_ingredients', 'recipe', r.id, format('Recipe "%s" has no ingredients', r.title)
FROM recipes r
WHERE NOT r.is_archived AND NOT EXISTS (SELECT 1 FROM fn_recipe_effective_ingredients(r.id))
UNION ALL
SELECT r.owner_id, 'recipe_without_stages', 'recipe', r.id, format('Recipe "%s" has no stages', r.title)
FROM recipes r
WHERE NOT r.is_archived AND NOT EXISTS (SELECT 1 FROM fn_recipe_effective_stages(r.id))
UNION ALL
-- a planned batch whose start passed more than a day ago and was never started
SELECT b.owner_id, 'overdue_planned_batch', 'batch', b.id,
       format('Batch "%s" was planned for %s and not started', b.name, b.planned_start_at)
FROM batches b
WHERE b.status = 'planned' AND b.planned_start_at < now() - interval '1 day'
UNION ALL
-- a finished batch without a complete stage log (KPI-1)
SELECT c.owner_id, 'incomplete_stage_log', 'batch', c.batch_id,
       format('%s of %s stages have actual times', c.stages_logged, c.stages_total)
FROM v_batch_log_completeness c
WHERE c.status = 'done' AND NOT c.is_complete
UNION ALL
-- an active starter that has not been fed for two feeding intervals
SELECT s.owner_id, 'starter_feeding_overdue', 'starter', s.id,
       format('Starter "%s" last fed %s', s.name, coalesce(f.last_fed::text, 'never'))
FROM starters s
LEFT JOIN LATERAL (SELECT max(fed_at) AS last_fed FROM starter_feedings WHERE starter_id = s.id) f ON true
WHERE s.is_active
  AND (f.last_fed IS NULL OR f.last_fed < now() - make_interval(hours => 2 * s.feeding_interval_hours));

COMMENT ON VIEW v_data_quality_issues IS
    'Data quality problems that depend on time or several tables: missing prices, empty recipes, overdue batches, incomplete stage logs, overdue feedings.';
