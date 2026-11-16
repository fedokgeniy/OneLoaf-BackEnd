-- Deterministic calculations of the baking calculator (E4).
-- The Core API and the AI assistant tools call these functions; the language model never computes numbers itself (US-35).

-- Price of an ingredient valid on a date (US-15): the latest price with effective_from <= date.
CREATE OR REPLACE FUNCTION fn_price_on_date(p_ingredient_id uuid, p_as_of date)
RETURNS TABLE (price numeric, per_quantity numeric, currency character(3), effective_from date)
LANGUAGE sql STABLE STRICT
AS $$
    SELECT ip.price, ip.per_quantity, ip.currency, ip.effective_from
    FROM ingredient_prices ip
    WHERE ip.ingredient_id = p_ingredient_id
      AND ip.effective_from <= p_as_of
    ORDER BY ip.effective_from DESC
    LIMIT 1;
$$;

COMMENT ON FUNCTION fn_price_on_date(uuid, date) IS
    'Price row valid on the given date: latest effective_from <= date (US-15). Uses ux_ingredient_prices_ingredient_id_effective_from.';

-- Timeline of a recipe for a start time (US-16).
-- Each stage starts when the previous stages have taken their minimum time and may end
-- between start + min and start + max of the cumulative durations.
CREATE OR REPLACE FUNCTION fn_recipe_timeline(p_recipe_id uuid, p_start timestamptz)
RETURNS TABLE (
    recipe_stage_id    uuid,
    sort_order         integer,
    name               text,
    stage_type_code    text,
    min_minutes        integer,
    max_minutes        integer,
    planned_start_at   timestamptz,
    planned_end_min_at timestamptz,
    planned_end_max_at timestamptz
)
LANGUAGE sql STABLE STRICT
AS $$
    WITH s AS (
        SELECT e.*,
               sum(e.min_duration_minutes) OVER w AS cum_min,
               sum(e.max_duration_minutes) OVER w AS cum_max
        FROM fn_recipe_effective_stages(p_recipe_id) e
        WINDOW w AS (ORDER BY e.sort_order, e.line_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
    )
    SELECT s.line_id,
           s.sort_order,
           s.name,
           s.stage_type_code,
           s.min_duration_minutes,
           s.max_duration_minutes,
           p_start + make_interval(mins => (s.cum_min - s.min_duration_minutes)::int),
           p_start + make_interval(mins => s.cum_min::int),
           p_start + make_interval(mins => s.cum_max::int)
    FROM s
    ORDER BY s.sort_order, s.line_id;
$$;

COMMENT ON FUNCTION fn_recipe_timeline(uuid, timestamptz) IS
    'Planned start and end window of every stage for a given start time (US-16).';

-- Ready window for a start time (US-16): start + total min .. start + total max.
CREATE OR REPLACE FUNCTION fn_recipe_ready_window(p_recipe_id uuid, p_start timestamptz)
RETURNS TABLE (ready_from timestamptz, ready_to timestamptz)
LANGUAGE sql STABLE STRICT
AS $$
    SELECT p_start + make_interval(mins => coalesce(sum(min_duration_minutes), 0)::int),
           p_start + make_interval(mins => coalesce(sum(max_duration_minutes), 0)::int)
    FROM fn_recipe_effective_stages(p_recipe_id);
$$;

-- Start window for a desired ready time, planning backwards (US-52).
CREATE OR REPLACE FUNCTION fn_recipe_start_window(p_recipe_id uuid, p_ready_at timestamptz)
RETURNS TABLE (start_from timestamptz, start_to timestamptz)
LANGUAGE sql STABLE STRICT
AS $$
    SELECT p_ready_at - make_interval(mins => coalesce(sum(max_duration_minutes), 0)::int),
           p_ready_at - make_interval(mins => coalesce(sum(min_duration_minutes), 0)::int)
    FROM fn_recipe_effective_stages(p_recipe_id);
$$;

-- Dough weight in grams for N loaves. Mass units are converted with units.factor_to_base;
-- liquids in ml are counted 1 ml = 1 g (water-based liquids); pieces are not counted; optional lines are excluded.
CREATE OR REPLACE FUNCTION fn_dough_weight_g(p_recipe_id uuid, p_loaves integer)
RETURNS numeric
LANGUAGE sql STABLE STRICT
AS $$
    SELECT coalesce(sum(e.quantity * u.factor_to_base * p_loaves / h.yield_loaves), 0)
    FROM fn_recipe_effective_ingredients(p_recipe_id) e
    JOIN ingredients i ON i.id = e.ingredient_id
    JOIN units u ON u.code = i.unit_code
    CROSS JOIN fn_recipe_effective_header(p_recipe_id) h
    WHERE NOT e.is_optional
      AND u.dimension IN ('mass', 'volume');
$$;

-- Number of kneading cycles for the mixer bowl of the recipe owner (US-17).
-- NULL when the bowl capacity is not configured.
CREATE OR REPLACE FUNCTION fn_kneading_cycles(p_recipe_id uuid, p_loaves integer)
RETURNS integer
LANGUAGE sql STABLE STRICT
AS $$
    SELECT CASE
               WHEN ks.mixer_bowl_capacity_kg IS NULL THEN NULL
               ELSE greatest(1, ceil(fn_dough_weight_g(p_recipe_id, p_loaves) / (ks.mixer_bowl_capacity_kg * 1000)))::int
           END
    FROM recipes r
    LEFT JOIN kitchen_settings ks ON ks.user_id = r.owner_id
    WHERE r.id = p_recipe_id;
$$;

-- Starter to prepare for N loaves, grams (US-18): starter lines of the recipe scaled to N loaves.
CREATE OR REPLACE FUNCTION fn_starter_amount_g(p_recipe_id uuid, p_loaves integer)
RETURNS numeric
LANGUAGE sql STABLE STRICT
AS $$
    SELECT coalesce(sum(e.quantity * u.factor_to_base * p_loaves / h.yield_loaves), 0)
    FROM fn_recipe_effective_ingredients(p_recipe_id) e
    JOIN ingredients i ON i.id = e.ingredient_id
    JOIN units u ON u.code = i.unit_code
    CROSS JOIN fn_recipe_effective_header(p_recipe_id) h
    WHERE i.category_code = 'starter' AND NOT e.is_optional;
$$;

-- Cost of N loaves of a recipe with prices valid on a date (US-14, US-15, US-47, US-48, US-49).
-- Lines: one per mandatory ingredient, electricity, packaging, labor (if a labor rate is set), loss, total.
-- Every amount is rounded to the minor units of the currency; the total is the sum of the rounded lines.
-- A line whose input is missing (no price on the date, price in another currency, no oven settings)
-- has amount NULL and is_complete = false; the total is then marked incomplete (UC-01, A4).
CREATE OR REPLACE FUNCTION fn_recipe_cost(p_recipe_id uuid, p_loaves integer, p_as_of date)
RETURNS TABLE (
    line_no       integer,
    line_type     text,
    ingredient_id uuid,
    description   text,
    quantity      numeric,
    unit_code     text,
    unit_price    numeric,
    amount        numeric,
    currency      character(3),
    is_complete   boolean
)
LANGUAGE plpgsql STABLE STRICT
AS $$
#variable_conflict use_column
DECLARE
    h            record;
    ks           kitchen_settings%ROWTYPE;
    v_currency   character(3);
    v_minor      integer;
    v_no         integer := 0;
    v_subtotal   numeric := 0;
    v_complete   boolean := true;
    v_amount     numeric;
    v_oven_min   numeric;
    v_active_min numeric;
    v_loads      integer;
    line         record;
    pr           record;
BEGIN
    IF p_loaves <= 0 THEN
        RAISE EXCEPTION 'Number of loaves must be positive, got %', p_loaves USING ERRCODE = '22023';
    END IF;

    SELECT * INTO h FROM fn_recipe_effective_header(p_recipe_id);
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Recipe % not found', p_recipe_id USING ERRCODE = 'P0002';
    END IF;

    SELECT * INTO ks FROM kitchen_settings WHERE user_id = h.owner_id;
    v_currency := coalesce(ks.currency, 'EUR');
    SELECT minor_units INTO v_minor FROM currencies WHERE code = v_currency;

    -- Ingredient lines
    FOR line IN
        SELECT e.ingredient_id, i.name, i.unit_code, e.quantity * p_loaves / h.yield_loaves AS qty
        FROM fn_recipe_effective_ingredients(p_recipe_id) e
        JOIN ingredients i ON i.id = e.ingredient_id
        WHERE NOT e.is_optional
        ORDER BY e.sort_order, e.line_id
    LOOP
        v_no := v_no + 1;
        SELECT * INTO pr FROM fn_price_on_date(line.ingredient_id, p_as_of);
        IF FOUND AND pr.currency = v_currency THEN
            v_amount := round(line.qty * pr.price / pr.per_quantity, v_minor);
            v_subtotal := v_subtotal + v_amount;
            RETURN QUERY SELECT v_no, 'ingredient', line.ingredient_id, line.name::text, line.qty, line.unit_code::text,
                                pr.price / pr.per_quantity, v_amount, v_currency, true;
        ELSE
            v_complete := false;
            RETURN QUERY SELECT v_no, 'ingredient', line.ingredient_id, line.name::text, line.qty, line.unit_code::text,
                                NULL::numeric, NULL::numeric, v_currency, false;
        END IF;
    END LOOP;

    -- Electricity: oven power x oven time (maximum durations) x tariff x number of oven loads
    SELECT coalesce(sum(max_duration_minutes) FILTER (WHERE uses_oven), 0),
           coalesce(sum(active_minutes), 0)
    INTO v_oven_min, v_active_min
    FROM fn_recipe_effective_stages(p_recipe_id);

    v_no := v_no + 1;
    IF v_oven_min = 0 THEN
        RETURN QUERY SELECT v_no, 'electricity', NULL::uuid, 'Electricity (no oven stages)', 0::numeric, 'kWh',
                            ks.electricity_tariff_per_kwh, 0::numeric, v_currency, true;
    ELSIF ks.oven_power_kw IS NULL OR ks.electricity_tariff_per_kwh IS NULL THEN
        v_complete := false;
        RETURN QUERY SELECT v_no, 'electricity', NULL::uuid, 'Electricity (oven power or tariff not set)', NULL::numeric, 'kWh',
                            NULL::numeric, NULL::numeric, v_currency, false;
    ELSE
        v_loads := ceil(p_loaves::numeric / coalesce(ks.oven_capacity_loaves, p_loaves))::int;
        v_amount := round(ks.oven_power_kw * v_oven_min / 60 * v_loads * ks.electricity_tariff_per_kwh, v_minor);
        v_subtotal := v_subtotal + v_amount;
        RETURN QUERY SELECT v_no, 'electricity', NULL::uuid,
                            format('Electricity: %s oven load(s)', v_loads),
                            round(ks.oven_power_kw * v_oven_min / 60 * v_loads, 3), 'kWh',
                            ks.electricity_tariff_per_kwh, v_amount, v_currency, true;
    END IF;

    -- Packaging per loaf (US-47)
    v_no := v_no + 1;
    v_amount := round(coalesce(ks.packaging_cost_per_loaf, 0) * p_loaves, v_minor);
    v_subtotal := v_subtotal + v_amount;
    RETURN QUERY SELECT v_no, 'packaging', NULL::uuid, 'Packaging', p_loaves::numeric, 'pcs',
                        coalesce(ks.packaging_cost_per_loaf, 0), v_amount, v_currency, true;

    -- Labor: active minutes of all stages x hourly rate (US-49, only when a rate is set)
    IF ks.labor_rate_per_hour IS NOT NULL THEN
        v_no := v_no + 1;
        v_amount := round(v_active_min / 60 * ks.labor_rate_per_hour, v_minor);
        v_subtotal := v_subtotal + v_amount;
        RETURN QUERY SELECT v_no, 'labor', NULL::uuid, 'Labor', round(v_active_min / 60, 3), 'h',
                            ks.labor_rate_per_hour, v_amount, v_currency, true;
    END IF;

    -- Loss rate on the subtotal (US-48)
    v_no := v_no + 1;
    v_amount := round(v_subtotal * coalesce(ks.loss_rate_percent, 0) / 100, v_minor);
    RETURN QUERY SELECT v_no, 'loss', NULL::uuid, format('Loss %s%%', coalesce(ks.loss_rate_percent, 0)),
                        v_subtotal, NULL::text, coalesce(ks.loss_rate_percent, 0) / 100, v_amount, v_currency, true;

    v_no := v_no + 1;
    RETURN QUERY SELECT v_no, 'total', NULL::uuid, 'Total', p_loaves::numeric, 'loaves',
                        NULL::numeric, v_subtotal + v_amount, v_currency, v_complete;
END;
$$;

COMMENT ON FUNCTION fn_recipe_cost(uuid, integer, date) IS
    'Cost breakdown of N loaves with prices valid on a date: ingredients, electricity, packaging, labor, loss, total (US-14, US-15, US-47-49).';

COMMENT ON FUNCTION fn_recipe_ready_window(uuid, timestamptz) IS 'Ready window for a start time: start + total minimum .. start + total maximum duration (US-16).';
COMMENT ON FUNCTION fn_recipe_start_window(uuid, timestamptz) IS 'Start window for a desired ready time, planning backwards (US-52).';
COMMENT ON FUNCTION fn_dough_weight_g(uuid, integer) IS 'Dough weight in grams for N loaves; ml counted as g, pieces and optional lines excluded.';
COMMENT ON FUNCTION fn_kneading_cycles(uuid, integer) IS 'Number of kneading cycles for N loaves and the mixer bowl of the owner; NULL if the bowl is not set (US-17).';
COMMENT ON FUNCTION fn_starter_amount_g(uuid, integer) IS 'Starter to prepare for N loaves, grams (US-18).';
