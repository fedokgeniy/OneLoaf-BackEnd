-- Used by the Down method of StarterFeedingIngredients: the feeding trigger function as the StarterIngredients
-- migration created it; the cost function and the trigger of the feeding ingredients are removed. Prices that
-- feedings wrote stay: they are ordinary price rows.

DROP TRIGGER IF EXISTS trg_starter_feedings_ingredients_before ON starter_feedings;
DROP FUNCTION IF EXISTS trg_starter_feedings_ingredients_before();
DROP FUNCTION IF EXISTS fn_starter_feeding_cost(uuid);

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

COMMENT ON FUNCTION trg_starter_feedings_stock() IS
    'Trigger: a feeding takes flour and water from the linked ingredients, returns the ripe starter that is left and adds the usable share at the expected peak.';
