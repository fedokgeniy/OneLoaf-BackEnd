-- Stock of ingredients: purchases, consumption by batches and starter feedings, corrections.
-- The stock of an ingredient is the sum of stock_movements.quantity (signed, in the ingredient unit).
-- Only ingredients with track_stock = true are consumed and checked; the application switches the flag on with the
-- first purchase or correction and the user can switch it off again.

------------------------------------------------------------------------------------------------
-- Workspace isolation: a movement and the batch or feeding it belongs to are in one workspace;
-- the ingredients a starter is fed with belong to the owner of the starter.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_stock_movements_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_owner uuid;
BEGIN
    SELECT owner_id INTO v_owner FROM ingredients WHERE id = NEW.ingredient_id;
    IF NEW.batch_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(v_owner, (SELECT owner_id FROM batches WHERE id = NEW.batch_id), 'batch');
    END IF;
    IF NEW.feeding_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(v_owner,
            (SELECT s.owner_id FROM starter_feedings f JOIN starters s ON s.id = f.starter_id WHERE f.id = NEW.feeding_id),
            'starter feeding');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_stock_movements_validate
    BEFORE INSERT OR UPDATE OF ingredient_id, batch_id, feeding_id ON stock_movements
    FOR EACH ROW EXECUTE FUNCTION trg_stock_movements_validate();

CREATE OR REPLACE FUNCTION trg_starters_stock_links_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.flour_ingredient_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.flour_ingredient_id), 'flour ingredient');
    END IF;
    IF NEW.water_ingredient_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.water_ingredient_id), 'water ingredient');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_starters_stock_links_validate
    BEFORE INSERT OR UPDATE OF flour_ingredient_id, water_ingredient_id, owner_id ON starters
    FOR EACH ROW EXECUTE FUNCTION trg_starters_stock_links_validate();

------------------------------------------------------------------------------------------------
-- Stock of an ingredient, in its unit.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_stock_balance(p_ingredient_id uuid)
RETURNS numeric
LANGUAGE sql STABLE STRICT
AS $$
    SELECT coalesce(sum(m.quantity), 0) FROM stock_movements m WHERE m.ingredient_id = p_ingredient_id;
$$;

-- Ingredients a batch needs for the full recipe quantities: every mandatory line of every item scaled to its loaves.
CREATE OR REPLACE FUNCTION fn_batch_ingredient_needs(p_batch_id uuid)
RETURNS TABLE (ingredient_id uuid, quantity numeric)
LANGUAGE sql STABLE STRICT
AS $$
    SELECT e.ingredient_id, sum(e.quantity * bi.loaves / h.yield_loaves)
    FROM batch_items bi
    CROSS JOIN LATERAL fn_recipe_effective_ingredients(bi.recipe_id) e
    CROSS JOIN LATERAL fn_recipe_effective_header(bi.recipe_id) h
    WHERE bi.batch_id = p_batch_id AND NOT e.is_optional
    GROUP BY e.ingredient_id;
$$;

-- What a batch takes from the stock, for the ingredients whose stock is kept:
-- needed_full for the full recipe, needed for batches.ingredient_scale, available = stock now.
CREATE OR REPLACE FUNCTION fn_batch_stock_check(p_batch_id uuid)
RETURNS TABLE (ingredient_id uuid, name text, unit_code text, needed_full numeric, needed numeric, available numeric)
LANGUAGE sql STABLE STRICT
AS $$
    SELECT n.ingredient_id, i.name::text, i.unit_code::text, round(n.quantity, 3),
           round(n.quantity * b.ingredient_scale, 3), fn_stock_balance(i.id)
    FROM batches b
    CROSS JOIN LATERAL fn_batch_ingredient_needs(b.id) n
    JOIN ingredients i ON i.id = n.ingredient_id
    WHERE b.id = p_batch_id AND i.track_stock
    ORDER BY i.name;
$$;

------------------------------------------------------------------------------------------------
-- Take the ingredients of a batch from the stock. Called after the plan is built or rebuilt:
-- the earlier consumption of the batch is replaced, so the stock changes by the difference.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE sp_apply_batch_stock(p_batch_id uuid)
LANGUAGE plpgsql
AS $$
DECLARE
    v_on date;
BEGIN
    SELECT (planned_start_at AT TIME ZONE 'UTC')::date INTO v_on FROM batches WHERE id = p_batch_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Batch % not found', p_batch_id USING ERRCODE = 'P0002';
    END IF;

    DELETE FROM stock_movements WHERE batch_id = p_batch_id AND movement_type = 'batch_consumption';

    INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, batch_id)
    SELECT c.ingredient_id, 'batch_consumption', -c.needed, v_on, p_batch_id
    FROM fn_batch_stock_check(p_batch_id) c
    WHERE c.needed > 0;
END;
$$;

-- Give back what a batch took and has not returned yet (the batch was cancelled or deleted before the dough was mixed).
CREATE OR REPLACE PROCEDURE sp_return_batch_stock(p_batch_id uuid)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, batch_id)
    SELECT m.ingredient_id, 'batch_return', -sum(m.quantity), current_date, p_batch_id
    FROM stock_movements m
    WHERE m.batch_id = p_batch_id AND m.movement_type IN ('batch_consumption', 'batch_return')
    GROUP BY m.ingredient_id
    HAVING sum(m.quantity) < 0;
END;
$$;

------------------------------------------------------------------------------------------------
-- A starter feeding takes its flour and water from the stock of the ingredients linked to the starter.
-- Feedings are logged in grams; mass and volume units are converted (1 ml = 1 g), pieces are not consumed.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_starter_feedings_stock()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
    SELECT i.id, 'starter_feeding', -round(x.grams / u.factor_to_base, 3), (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id
    FROM starters s
    CROSS JOIN LATERAL (VALUES (s.flour_ingredient_id, NEW.flour_g), (s.water_ingredient_id, NEW.water_g)) AS x(ingredient_id, grams)
    JOIN ingredients i ON i.id = x.ingredient_id
    JOIN units u ON u.code = i.unit_code
    WHERE s.id = NEW.starter_id
      AND i.track_stock
      AND u.dimension IN ('mass', 'volume')
      AND round(x.grams / u.factor_to_base, 3) > 0;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_starter_feedings_stock
    AFTER INSERT ON starter_feedings
    FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_stock();

------------------------------------------------------------------------------------------------
-- Ingredients below their reorder level (or below zero when no level is set).
------------------------------------------------------------------------------------------------
CREATE VIEW v_low_stock AS
SELECT i.owner_id, i.id AS ingredient_id, i.name, i.unit_code, fn_stock_balance(i.id) AS stock, i.reorder_level
FROM ingredients i
WHERE i.track_stock
  AND fn_stock_balance(i.id) < coalesce(i.reorder_level, 0);

COMMENT ON VIEW v_low_stock IS 'Ingredients with kept stock that are below their reorder level, or below zero when no level is set.';

------------------------------------------------------------------------------------------------
-- The batch plan takes batches.ingredient_scale into account: kneading cycles, starter amount and the
-- ingredient lines of the cost shrink with the scale; electricity, packaging and labor do not.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE sp_build_batch_plan(p_batch_id uuid)
LANGUAGE plpgsql
AS $$
DECLARE
    b          batches%ROWTYPE;
    ks         kitchen_settings%ROWTYPE;
    item       record;
    v_total    numeric := 0;
    v_complete boolean := true;
    v_cost     record;
    v_first    timestamptz;
    v_minor    integer;
    v_subtotal numeric;
    v_ok       boolean;
    v_currency character(3);
BEGIN
    SELECT * INTO b FROM batches WHERE id = p_batch_id FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Batch % not found', p_batch_id USING ERRCODE = 'P0002';
    END IF;
    IF b.status <> 'planned' THEN
        RAISE EXCEPTION 'Only a planned batch can be (re)planned; batch % is %', p_batch_id, b.status USING ERRCODE = '55000';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM batch_items WHERE batch_id = p_batch_id) THEN
        RAISE EXCEPTION 'Batch % has no items', p_batch_id USING ERRCODE = '23514';
    END IF;

    SELECT * INTO ks FROM kitchen_settings WHERE user_id = b.owner_id;
    SELECT minor_units INTO v_minor FROM currencies WHERE code = b.currency;

    FOR item IN SELECT * FROM batch_items WHERE batch_id = p_batch_id ORDER BY sort_order FOR UPDATE LOOP
        DELETE FROM reminders WHERE batch_item_id = item.id AND status = 'scheduled';
        DELETE FROM batch_stages WHERE batch_item_id = item.id;

        INSERT INTO batch_stages (batch_item_id, recipe_stage_id, sort_order, name, stage_type_code,
                                  planned_min_minutes, planned_max_minutes, planned_start_at, planned_end_min_at, planned_end_max_at, status, extended_minutes, is_overridden)
        SELECT item.id, t.recipe_stage_id, t.sort_order, t.name, t.stage_type_code,
               t.min_minutes, t.max_minutes, t.planned_start_at, t.planned_end_min_at, t.planned_end_max_at, 'pending', 0, false
        FROM fn_recipe_timeline(item.recipe_id, b.planned_start_at) t;

        UPDATE batch_items
        SET kneading_cycles  = CASE
                                   WHEN b.ingredient_scale = 1 THEN fn_kneading_cycles(item.recipe_id, item.loaves)
                                   WHEN ks.mixer_bowl_capacity_kg IS NULL THEN NULL
                                   ELSE greatest(1, ceil(fn_dough_weight_g(item.recipe_id, item.loaves) * b.ingredient_scale
                                                         / (ks.mixer_bowl_capacity_kg * 1000)))::int
                               END,
            starter_amount_g = nullif(fn_starter_amount_g(item.recipe_id, item.loaves) * b.ingredient_scale, 0)
        WHERE id = item.id;

        IF b.ingredient_scale = 1 THEN
            SELECT * INTO v_cost FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) WHERE line_type = 'total';
            IF v_cost.is_complete AND v_cost.currency = b.currency THEN
                v_total := v_total + v_cost.amount;
            ELSE
                v_complete := false;
            END IF;
        ELSE
            SELECT coalesce(sum(CASE WHEN c.line_type = 'ingredient' THEN round(c.amount * b.ingredient_scale, v_minor) ELSE c.amount END)
                                FILTER (WHERE c.line_type NOT IN ('loss', 'total')), 0),
                   bool_and(c.is_complete),
                   max(c.currency) FILTER (WHERE c.line_type = 'total')
            INTO v_subtotal, v_ok, v_currency
            FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) c;
            IF v_ok AND v_currency = b.currency THEN
                v_total := v_total + v_subtotal + round(v_subtotal * coalesce(ks.loss_rate_percent, 0) / 100, v_minor);
            ELSE
                v_complete := false;
            END IF;
        END IF;

        IF item.starter_reminder_minutes IS NOT NULL THEN
            SELECT min(planned_start_at) INTO v_first
            FROM batch_stages WHERE batch_item_id = item.id AND stage_type_code = 'mix';
            v_first := coalesce(v_first, b.planned_start_at);
            INSERT INTO reminders (owner_id, kind, starter_id, batch_item_id, due_at, channel, status, message, attempts)
            VALUES (b.owner_id, 'before_kneading', item.starter_id, item.id,
                    v_first - make_interval(mins => item.starter_reminder_minutes), 'push', 'scheduled',
                    format('Prepare the starter: kneading starts in %s min', item.starter_reminder_minutes), 0);
        END IF;
    END LOOP;

    UPDATE batches
    SET planned_ready_from = s.ready_from,
        planned_ready_to   = s.ready_to,
        total_cost         = CASE WHEN v_complete THEN v_total END
    FROM (SELECT max(bs.planned_end_min_at) AS ready_from, max(bs.planned_end_max_at) AS ready_to
          FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
          WHERE bi.batch_id = p_batch_id) s
    WHERE id = p_batch_id;
END;
$$;

COMMENT ON FUNCTION trg_stock_movements_validate() IS 'Trigger: a stock movement, its batch and its feeding belong to one workspace.';
COMMENT ON FUNCTION trg_starters_stock_links_validate() IS 'Trigger: the ingredients a starter is fed with belong to the owner of the starter.';
COMMENT ON FUNCTION trg_starter_feedings_stock() IS 'Trigger: a feeding takes its flour and water from the stock of the linked ingredients.';
COMMENT ON FUNCTION fn_stock_balance(uuid) IS 'Stock of an ingredient in its unit: sum of its stock movements.';
COMMENT ON FUNCTION fn_batch_ingredient_needs(uuid) IS 'Ingredient quantities of a batch for the full recipe quantities, summed over its items.';
COMMENT ON FUNCTION fn_batch_stock_check(uuid) IS 'Needed (full and scaled) and available quantity of every stock-kept ingredient of a batch.';
COMMENT ON PROCEDURE sp_apply_batch_stock(uuid) IS 'Replaces the stock consumption of a batch with the consumption of its current plan.';
COMMENT ON PROCEDURE sp_return_batch_stock(uuid) IS 'Returns to the stock what a batch took and has not returned yet.';
COMMENT ON PROCEDURE sp_build_batch_plan(uuid) IS
    'Builds the stage plan, kneading cycles, starter amount, reminders, ready window and total cost of a planned batch, scaled by ingredient_scale (US-16-18, US-20, US-29).';

-- Privileges: the application role gets its rights from the default privileges; the reporting role reads the
-- ledger without the free-text note (column privileges).
GRANT SELECT (id, ingredient_id, movement_type, quantity, occurred_on, batch_id, feeding_id, pack_count, pack_size,
              total_cost, currency, created_at)
    ON stock_movements TO app_read;
-- A view gets the default table privileges of app_write; like the other views it is read-only.
REVOKE INSERT, UPDATE, DELETE ON v_low_stock FROM app_write;
GRANT SELECT ON v_low_stock TO app_write, app_read;
GRANT EXECUTE ON FUNCTION fn_stock_balance(uuid), fn_batch_ingredient_needs(uuid), fn_batch_stock_check(uuid) TO app_read;
