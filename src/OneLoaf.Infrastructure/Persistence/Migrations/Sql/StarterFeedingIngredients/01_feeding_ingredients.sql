-- A feeding names the flour and the water it used: a starter can be fed with another flour than usual. The usual
-- flour and water of the starter are the default. The price of the starter is what it was fed: after every feeding
-- the cost of one unit of starter is stored as the price of its ingredient, so recipes with a starter have a cost.

-- Feedings recorded before this migration used the flour and water of their starter.
UPDATE starter_feedings f
SET flour_ingredient_id = s.flour_ingredient_id, water_ingredient_id = s.water_ingredient_id
FROM starters s
WHERE s.id = f.starter_id;

COMMENT ON COLUMN starter_feedings.flour_ingredient_id IS 'Flour ingredient this feeding used; the usual flour of the starter when not chosen.';
COMMENT ON COLUMN starter_feedings.water_ingredient_id IS 'Water ingredient this feeding used; the usual water of the starter when not chosen.';

------------------------------------------------------------------------------------------------
-- Default and ownership of the ingredients of a feeding.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_starter_feedings_ingredients_before()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    s starters%ROWTYPE;
BEGIN
    SELECT * INTO s FROM starters WHERE id = NEW.starter_id;
    NEW.flour_ingredient_id := coalesce(NEW.flour_ingredient_id, s.flour_ingredient_id);
    NEW.water_ingredient_id := coalesce(NEW.water_ingredient_id, s.water_ingredient_id);
    IF NEW.flour_ingredient_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(s.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.flour_ingredient_id), 'flour of the feeding');
    END IF;
    IF NEW.water_ingredient_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(s.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.water_ingredient_id), 'water of the feeding');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_starter_feedings_ingredients_before
    BEFORE INSERT ON starter_feedings
    FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_ingredients_before();

------------------------------------------------------------------------------------------------
-- Cost of one gram of the starter after a feeding, in the currency of the kitchen.
-- Flour and water are priced on the day of the feeding. The starter that was kept has the price the starter had
-- before; without one it is worth as much as the fresh mix. NULL when the cost is not known: no flour was named,
-- a named ingredient has no price on that day, or its price is in another currency.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_starter_feeding_cost(p_feeding_id uuid)
RETURNS numeric
LANGUAGE plpgsql STABLE STRICT
AS $$
DECLARE
    f          starter_feedings%ROWTYPE;
    s          starters%ROWTYPE;
    v_currency character(3);
    v_date     date;
    v_cost     numeric := 0;
    v_mass     numeric;
    v_factor   numeric;
    x          record;
    pr         record;
BEGIN
    SELECT * INTO f FROM starter_feedings WHERE id = p_feeding_id;
    IF NOT FOUND OR f.flour_ingredient_id IS NULL OR f.flour_g <= 0 THEN
        RETURN NULL;
    END IF;
    SELECT * INTO s FROM starters WHERE id = f.starter_id;
    v_currency := coalesce((SELECT ks.currency FROM kitchen_settings ks WHERE ks.user_id = s.owner_id), 'EUR');
    v_date := (f.fed_at AT TIME ZONE 'UTC')::date;

    FOR x IN
        SELECT v.ingredient_id, v.grams
        FROM (VALUES (f.flour_ingredient_id, f.flour_g), (f.water_ingredient_id, f.water_g)) AS v(ingredient_id, grams)
        WHERE v.ingredient_id IS NOT NULL AND v.grams > 0
    LOOP
        SELECT u.factor_to_base INTO v_factor
        FROM ingredients i JOIN units u ON u.code = i.unit_code
        WHERE i.id = x.ingredient_id AND u.dimension IN ('mass', 'volume');
        IF NOT FOUND THEN
            RETURN NULL;
        END IF;
        SELECT * INTO pr FROM fn_price_on_date(x.ingredient_id, v_date);
        IF NOT FOUND OR pr.currency <> v_currency THEN
            RETURN NULL;
        END IF;
        v_cost := v_cost + x.grams / v_factor * pr.price / pr.per_quantity;
    END LOOP;

    v_mass := f.flour_g + f.water_g;
    IF coalesce(f.starter_kept_g, 0) > 0 THEN
        SELECT u.factor_to_base INTO v_factor
        FROM ingredients i JOIN units u ON u.code = i.unit_code
        WHERE i.id = s.ingredient_id AND u.dimension = 'mass';
        SELECT * INTO pr FROM fn_price_on_date(s.ingredient_id, v_date);
        IF FOUND AND v_factor IS NOT NULL AND pr.currency = v_currency THEN
            v_cost := v_cost + f.starter_kept_g / v_factor * pr.price / pr.per_quantity;
            v_mass := v_mass + f.starter_kept_g;
        END IF;
    END IF;
    RETURN v_cost / v_mass;
END;
$$;

COMMENT ON FUNCTION fn_starter_feeding_cost(uuid) IS
    'Cost of one gram of a starter after a feeding: flour and water at their prices of that day plus the kept starter at its previous price; NULL when a price is missing.';

------------------------------------------------------------------------------------------------
-- A feeding and the stock (as before, with the flour and water of the feeding) and the price of the starter.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_starter_feedings_stock()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    s        starters%ROWTYPE;
    v_factor numeric;
    v_track  boolean;
    v_mass   boolean;
    v_latest boolean;
    v_left   numeric;
    v_ripe   numeric;
    v_at     timestamptz;
    v_cost   numeric;
BEGIN
    SELECT * INTO s FROM starters WHERE id = NEW.starter_id;

    INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
    SELECT i.id, 'starter_feeding', -round(x.grams / u.factor_to_base, 3), (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id
    FROM (VALUES (NEW.flour_ingredient_id, NEW.flour_g), (NEW.water_ingredient_id, NEW.water_g)) AS x(ingredient_id, grams)
    JOIN ingredients i ON i.id = x.ingredient_id
    JOIN units u ON u.code = i.unit_code
    WHERE i.track_stock
      AND u.dimension IN ('mass', 'volume')
      AND round(x.grams / u.factor_to_base, 3) > 0;

    SELECT u.factor_to_base, i.track_stock, u.dimension = 'mass' INTO v_factor, v_track, v_mass
    FROM ingredients i JOIN units u ON u.code = i.unit_code
    WHERE i.id = s.ingredient_id;

    -- Only the latest feeding moves the starter stock and sets its price: a feeding logged afterwards for an
    -- earlier time does not.
    v_latest := NEW.fed_at >= (SELECT max(fed_at) FROM starter_feedings WHERE starter_id = NEW.starter_id);

    IF v_latest AND v_mass THEN
        v_cost := fn_starter_feeding_cost(NEW.id);
        IF v_cost IS NOT NULL THEN
            INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
            VALUES (s.ingredient_id, round(v_cost * 1000, 4), round(1000 / v_factor, 3),
                    coalesce((SELECT ks.currency FROM kitchen_settings ks WHERE ks.user_id = s.owner_id), 'EUR'),
                    (NEW.fed_at AT TIME ZONE 'UTC')::date)
            ON CONFLICT (ingredient_id, effective_from)
            DO UPDATE SET price = EXCLUDED.price, per_quantity = EXCLUDED.per_quantity, currency = EXCLUDED.currency;
        END IF;
    END IF;

    IF v_latest AND v_mass AND v_track THEN
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

COMMENT ON FUNCTION trg_starter_feedings_ingredients_before() IS
    'Trigger: a feeding uses the usual flour and water of its starter unless others are named; they belong to the owner of the starter.';
COMMENT ON FUNCTION trg_starter_feedings_stock() IS
    'Trigger: a feeding takes its flour and water from the stock, sets the price of the starter to what it was fed, returns the ripe starter that is left and adds the usable share at the expected peak.';

GRANT EXECUTE ON FUNCTION fn_starter_feeding_cost(uuid) TO app_read;

-- Starters fed before this migration get their price from their latest feeding, where a price is not set for that day.
INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
SELECT s.ingredient_id, round(c.cost * 1000, 4), round(1000 / u.factor_to_base, 3),
       coalesce((SELECT ks.currency FROM kitchen_settings ks WHERE ks.user_id = s.owner_id), 'EUR'),
       (f.fed_at AT TIME ZONE 'UTC')::date
FROM starters s
JOIN ingredients i ON i.id = s.ingredient_id
JOIN units u ON u.code = i.unit_code AND u.dimension = 'mass'
JOIN LATERAL (SELECT * FROM starter_feedings x WHERE x.starter_id = s.id ORDER BY x.fed_at DESC LIMIT 1) f ON true
CROSS JOIN LATERAL (SELECT fn_starter_feeding_cost(f.id) AS cost) c
WHERE c.cost IS NOT NULL
ON CONFLICT (ingredient_id, effective_from) DO NOTHING;
