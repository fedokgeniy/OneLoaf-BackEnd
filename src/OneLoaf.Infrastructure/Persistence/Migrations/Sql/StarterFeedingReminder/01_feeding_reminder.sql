-- The reminder of a batch item with a starter is a reminder to feed it: it is due the time to peak of the starter
-- before the first kneading stage (the Core API stores that lead in batch_items.starter_reminder_minutes), so the
-- starter is ripe when kneading starts. Only the text of the reminder changes here.

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
                    format('Feed the starter%s now: it needs %s to be ripe when kneading starts',
                           coalesce(' "' || (SELECT s.name FROM starters s WHERE s.id = item.starter_id) || '"', ''),
                           CASE WHEN item.starter_reminder_minutes >= 60
                                THEN trim_scale(round(item.starter_reminder_minutes / 60.0, 1))::text || ' h'
                                ELSE item.starter_reminder_minutes::text || ' min' END), 0);
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
