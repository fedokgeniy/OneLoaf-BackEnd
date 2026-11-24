-- Used by the Down method of StarterIngredients: the stock functions, the feeding trigger function and v_low_stock
-- as the IngredientStock migration created them.

CREATE OR REPLACE FUNCTION fn_stock_balance(p_ingredient_id uuid)
RETURNS numeric
LANGUAGE sql STABLE STRICT
AS $$
    SELECT coalesce(sum(m.quantity), 0) FROM stock_movements m WHERE m.ingredient_id = p_ingredient_id;
$$;

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

CREATE OR REPLACE VIEW v_low_stock AS
SELECT i.owner_id, i.id AS ingredient_id, i.name, i.unit_code, fn_stock_balance(i.id) AS stock, i.reorder_level
FROM ingredients i
WHERE i.track_stock
  AND fn_stock_balance(i.id) < coalesce(i.reorder_level, 0);

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

DROP FUNCTION IF EXISTS fn_stock_balance_at(uuid, timestamptz);
