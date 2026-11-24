-- A starter is a kind of ingredient: every starter has its own row in ingredients (category starter), so recipes use
-- it and its stock is the ripe starter. The name is the same in both tables and is kept in step by triggers.
-- A feeding returns what is left of the ripe starter to the jar and, at the expected peak, puts the usable share
-- of the fed starter into the stock; the rest stays for the next feeding.

------------------------------------------------------------------------------------------------
-- Stock at a moment: movements that are not available yet (ripening starter) do not count.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_stock_balance_at(p_ingredient_id uuid, p_at timestamptz)
RETURNS numeric
LANGUAGE sql STABLE STRICT
AS $$
    SELECT coalesce(sum(m.quantity), 0)
    FROM stock_movements m
    WHERE m.ingredient_id = p_ingredient_id
      AND (m.available_at IS NULL OR m.available_at <= p_at);
$$;

CREATE OR REPLACE FUNCTION fn_stock_balance(p_ingredient_id uuid)
RETURNS numeric
LANGUAGE sql STABLE STRICT
AS $$
    SELECT fn_stock_balance_at(p_ingredient_id, now());
$$;

-- A batch is checked against the stock at its planned start: a starter fed today counts for a batch tomorrow.
CREATE OR REPLACE FUNCTION fn_batch_stock_check(p_batch_id uuid)
RETURNS TABLE (ingredient_id uuid, name text, unit_code text, needed_full numeric, needed numeric, available numeric)
LANGUAGE sql STABLE STRICT
AS $$
    SELECT n.ingredient_id, i.name::text, i.unit_code::text, round(n.quantity, 3),
           round(n.quantity * b.ingredient_scale, 3), fn_stock_balance_at(i.id, greatest(b.planned_start_at, now()))
    FROM batches b
    CROSS JOIN LATERAL fn_batch_ingredient_needs(b.id) n
    JOIN ingredients i ON i.id = n.ingredient_id
    WHERE b.id = p_batch_id AND i.track_stock
    ORDER BY i.name;
$$;

CREATE OR REPLACE VIEW v_low_stock AS
SELECT i.owner_id, i.id AS ingredient_id, i.name, i.unit_code, fn_stock_balance(i.id) AS stock, i.reorder_level
FROM ingredients i
WHERE i.track_stock
  AND fn_stock_balance(i.id) < coalesce(i.reorder_level, 0);

------------------------------------------------------------------------------------------------
-- Starter and its ingredient.
------------------------------------------------------------------------------------------------
-- A new starter without an ingredient gets one: an unlinked starter ingredient of the owner with the same name is
-- adopted, otherwise an ingredient is created (grams, stock kept).
CREATE OR REPLACE FUNCTION trg_starters_ingredient_before()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    -- the application sends the empty uuid for "not set"
    IF NEW.ingredient_id = '00000000-0000-0000-0000-000000000000' THEN
        NEW.ingredient_id := NULL;
    END IF;
    IF NEW.ingredient_id IS NULL THEN
        SELECT i.id INTO NEW.ingredient_id
        FROM ingredients i
        WHERE i.owner_id = NEW.owner_id AND i.name = NEW.name AND i.category_code = 'starter'
          AND NOT EXISTS (SELECT 1 FROM starters s WHERE s.ingredient_id = i.id);
        IF NEW.ingredient_id IS NULL THEN
            INSERT INTO ingredients (owner_id, name, category_code, unit_code, track_stock)
            VALUES (NEW.owner_id, NEW.name, 'starter', 'g', true)
            RETURNING id INTO NEW.ingredient_id;
        ELSE
            UPDATE ingredients SET track_stock = true WHERE id = NEW.ingredient_id AND NOT track_stock;
        END IF;
    ELSE
        PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.ingredient_id), 'starter ingredient');
    END IF;
    RETURN NEW;
END;
$$;

-- One name in both tables: a change on either side is copied to the other.
CREATE OR REPLACE FUNCTION trg_starters_name_sync()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE ingredients SET name = NEW.name WHERE id = NEW.ingredient_id AND name <> NEW.name;
    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION trg_ingredients_starter_name_sync()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE starters SET name = NEW.name WHERE ingredient_id = NEW.id AND name <> NEW.name;
    RETURN NULL;
END;
$$;

-- A deleted starter takes its ingredient with it unless a recipe still uses the ingredient.
CREATE OR REPLACE FUNCTION trg_starters_ingredient_after_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    DELETE FROM ingredients i
    WHERE i.id = OLD.ingredient_id
      AND NOT EXISTS (SELECT 1 FROM recipe_ingredients ri WHERE ri.ingredient_id = i.id);
    RETURN NULL;
END;
$$;

-- Starters that existed before this migration get their ingredients.
DO $$
DECLARE
    s record;
    v_ingredient uuid;
BEGIN
    FOR s IN SELECT * FROM starters WHERE ingredient_id IS NULL LOOP
        SELECT i.id INTO v_ingredient
        FROM ingredients i
        WHERE i.owner_id = s.owner_id AND i.name = s.name
          AND NOT EXISTS (SELECT 1 FROM starters x WHERE x.ingredient_id = i.id);
        IF v_ingredient IS NULL THEN
            INSERT INTO ingredients (owner_id, name, category_code, unit_code, track_stock)
            VALUES (s.owner_id, s.name, 'starter', 'g', true)
            RETURNING id INTO v_ingredient;
        END IF;
        UPDATE starters SET ingredient_id = v_ingredient WHERE id = s.id;
    END LOOP;
END;
$$;

CREATE TRIGGER trg_starters_ingredient_before
    BEFORE INSERT ON starters
    FOR EACH ROW EXECUTE FUNCTION trg_starters_ingredient_before();

CREATE TRIGGER trg_starters_name_sync
    AFTER UPDATE OF name ON starters
    FOR EACH ROW EXECUTE FUNCTION trg_starters_name_sync();

CREATE TRIGGER trg_ingredients_starter_name_sync
    AFTER UPDATE OF name ON ingredients
    FOR EACH ROW EXECUTE FUNCTION trg_ingredients_starter_name_sync();

CREATE TRIGGER trg_starters_ingredient_after_delete
    AFTER DELETE ON starters
    FOR EACH ROW EXECUTE FUNCTION trg_starters_ingredient_after_delete();

------------------------------------------------------------------------------------------------
-- A feeding and the stock: flour and water are taken from the linked ingredients (as before); the ripe starter
-- that is left goes back into the jar; the usable share of the fed starter becomes stock at the expected peak.
-- Only the latest feeding moves the starter stock: a feeding logged afterwards for an earlier time does not.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_starter_feedings_stock()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    s        starters%ROWTYPE;
    v_factor numeric;
    v_track  boolean;
    v_left   numeric;
    v_ripe   numeric;
    v_at     timestamptz;
BEGIN
    SELECT * INTO s FROM starters WHERE id = NEW.starter_id;

    INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
    SELECT i.id, 'starter_feeding', -round(x.grams / u.factor_to_base, 3), (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id
    FROM (VALUES (s.flour_ingredient_id, NEW.flour_g), (s.water_ingredient_id, NEW.water_g)) AS x(ingredient_id, grams)
    JOIN ingredients i ON i.id = x.ingredient_id
    JOIN units u ON u.code = i.unit_code
    WHERE i.track_stock
      AND u.dimension IN ('mass', 'volume')
      AND round(x.grams / u.factor_to_base, 3) > 0;

    SELECT u.factor_to_base, i.track_stock AND u.dimension = 'mass' INTO v_factor, v_track
    FROM ingredients i JOIN units u ON u.code = i.unit_code
    WHERE i.id = s.ingredient_id;

    IF v_track AND NEW.fed_at >= (SELECT max(fed_at) FROM starter_feedings WHERE starter_id = NEW.starter_id) THEN
        -- starter of the previous feeding that has not ripened yet never reaches the stock
        DELETE FROM stock_movements
        WHERE ingredient_id = s.ingredient_id AND movement_type = 'starter_ripened' AND available_at > NEW.fed_at;

        v_left := fn_stock_balance_at(s.ingredient_id, NEW.fed_at);
        IF v_left > 0 THEN
            INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
            VALUES (s.ingredient_id, 'starter_refed', -v_left, (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id);
        END IF;

        v_at := coalesce(NEW.expected_peak_at, NEW.fed_at);
        v_ripe := round((NEW.flour_g + NEW.water_g + coalesce(NEW.starter_kept_g, 0)) * s.usable_share / v_factor, 3);
        IF v_ripe > 0 THEN
            INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id, available_at)
            VALUES (s.ingredient_id, 'starter_ripened', v_ripe, (v_at AT TIME ZONE 'UTC')::date, NEW.id, v_at);
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION fn_stock_balance_at(uuid, timestamptz) IS 'Stock of an ingredient at a moment: movements that are not available yet do not count.';
COMMENT ON FUNCTION fn_stock_balance(uuid) IS 'Stock of an ingredient now, in its unit.';
COMMENT ON FUNCTION fn_batch_stock_check(uuid) IS 'Needed (full and scaled) quantity and the stock at the planned start of every stock-kept ingredient of a batch.';
COMMENT ON FUNCTION trg_starters_ingredient_before() IS 'Trigger: a new starter gets its ingredient of the category starter (adopted by name or created).';
COMMENT ON FUNCTION trg_starters_name_sync() IS 'Trigger: renaming a starter renames its ingredient.';
COMMENT ON FUNCTION trg_ingredients_starter_name_sync() IS 'Trigger: renaming the ingredient of a starter renames the starter.';
COMMENT ON FUNCTION trg_starters_ingredient_after_delete() IS 'Trigger: a deleted starter takes its ingredient with it unless a recipe uses it.';
COMMENT ON FUNCTION trg_starter_feedings_stock() IS
    'Trigger: a feeding takes flour and water from the linked ingredients, returns the ripe starter that is left and adds the usable share at the expected peak.';

GRANT EXECUTE ON FUNCTION fn_stock_balance_at(uuid, timestamptz) TO app_read;
