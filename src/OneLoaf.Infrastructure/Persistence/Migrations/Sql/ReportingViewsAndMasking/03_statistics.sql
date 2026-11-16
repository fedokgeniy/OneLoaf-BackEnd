-- Baking statistics per baker and recipe (E7, US-33, analysis by the assistant).
-- A materialized view: the aggregation over the whole history is computed once and refreshed
-- after a batch is finished, instead of on every opening of the history screen.

CREATE MATERIALIZED VIEW mv_bake_statistics AS
SELECT br.owner_id,
       coalesce(bri.recipe_id::text, 'title:' || bri.recipe_title)            AS recipe_key,
       bri.recipe_id,
       max(bri.recipe_title)                                                  AS recipe_title,
       count(DISTINCT br.id)                                                  AS bakes_count,
       sum(bri.loaves)                                                        AS loaves_total,
       round(avg(br.rating), 2)                                               AS avg_rating,
       count(br.rating)                                                       AS rated_count,
       round(sum(bri.cost) / nullif(sum(bri.loaves) FILTER (WHERE bri.cost IS NOT NULL), 0), 2) AS avg_cost_per_loaf,
       min(br.baked_on)                                                       AS first_baked_on,
       max(br.baked_on)                                                       AS last_baked_on
FROM bake_records br
JOIN bake_record_items bri ON bri.bake_record_id = br.id
GROUP BY br.owner_id, coalesce(bri.recipe_id::text, 'title:' || bri.recipe_title), bri.recipe_id
WITH DATA;

-- Unique index: required for REFRESH ... CONCURRENTLY (readers are not blocked during refresh)
CREATE UNIQUE INDEX ux_mv_bake_statistics_owner_id_recipe_key ON mv_bake_statistics (owner_id, recipe_key);

COMMENT ON MATERIALIZED VIEW mv_bake_statistics IS
    'Per baker and recipe: number of bakes, loaves, average rating, average cost per loaf, first and last bake. Refreshed by fn_refresh_bake_statistics().';

-- Only the owner of a materialized view may refresh it. This SECURITY DEFINER function lets the
-- application role refresh the statistics without owning the view; search_path is fixed for safety.
CREATE OR REPLACE FUNCTION fn_refresh_bake_statistics()
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_bake_statistics;
END;
$$;

COMMENT ON FUNCTION fn_refresh_bake_statistics() IS
    'Refreshes mv_bake_statistics concurrently. SECURITY DEFINER so that app_write can call it.';
