-- Stored procedures: multi-step business operations that must be applied completely or not at all.
-- A CALL is executed atomically: if any statement fails, every change made by the procedure is rolled back.

------------------------------------------------------------------------------------------------
-- Build or rebuild the plan of a planned batch (US-16, US-17, US-18, US-20, US-29).
-- For every item: stage timeline from the recipe, kneading cycles, starter amount and the reminder
-- before kneading; for the batch: ready window and total cost with prices on batches.price_date.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE sp_build_batch_plan(p_batch_id uuid)
LANGUAGE plpgsql
AS $$
DECLARE
    b          batches%ROWTYPE;
    item       record;
    v_total    numeric := 0;
    v_complete boolean := true;
    v_cost     record;
    v_first    timestamptz;
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

    FOR item IN SELECT * FROM batch_items WHERE batch_id = p_batch_id ORDER BY sort_order FOR UPDATE LOOP
        DELETE FROM reminders WHERE batch_item_id = item.id AND status = 'scheduled';
        DELETE FROM batch_stages WHERE batch_item_id = item.id;

        INSERT INTO batch_stages (batch_item_id, recipe_stage_id, sort_order, name, stage_type_code,
                                  planned_min_minutes, planned_max_minutes, planned_start_at, planned_end_min_at, planned_end_max_at, status, extended_minutes, is_overridden)
        SELECT item.id, t.recipe_stage_id, t.sort_order, t.name, t.stage_type_code,
               t.min_minutes, t.max_minutes, t.planned_start_at, t.planned_end_min_at, t.planned_end_max_at, 'pending', 0, false
        FROM fn_recipe_timeline(item.recipe_id, b.planned_start_at) t;

        UPDATE batch_items
        SET kneading_cycles  = fn_kneading_cycles(item.recipe_id, item.loaves),
            starter_amount_g = nullif(fn_starter_amount_g(item.recipe_id, item.loaves), 0)
        WHERE id = item.id;

        SELECT * INTO v_cost FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) WHERE line_type = 'total';
        IF v_cost.is_complete AND v_cost.currency = b.currency THEN
            v_total := v_total + v_cost.amount;
        ELSE
            v_complete := false;
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

COMMENT ON PROCEDURE sp_build_batch_plan(uuid) IS
    'Builds the stage plan, kneading cycles, starter amount, reminders, ready window and total cost of a planned batch (US-16-18, US-20, US-29).';

------------------------------------------------------------------------------------------------
-- Extend a pending or running stage by N minutes and shift the following stages (US-24).
------------------------------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE sp_extend_batch_stage(p_stage_id uuid, p_minutes integer DEFAULT 30)
LANGUAGE plpgsql
AS $$
DECLARE
    st       batch_stages%ROWTYPE;
    v_status text;
    v_batch  uuid;
    v_shift  interval;
BEGIN
    IF p_minutes IS NULL OR p_minutes <= 0 OR p_minutes > 24 * 60 THEN
        RAISE EXCEPTION 'Extension must be between 1 and 1440 minutes, got %', p_minutes USING ERRCODE = '22023';
    END IF;

    SELECT * INTO st FROM batch_stages WHERE id = p_stage_id FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Stage % not found', p_stage_id USING ERRCODE = 'P0002';
    END IF;
    SELECT b.status, b.id INTO v_status, v_batch
    FROM batch_items bi JOIN batches b ON b.id = bi.batch_id WHERE bi.id = st.batch_item_id;
    IF v_status NOT IN ('planned', 'in_progress') OR st.status NOT IN ('pending', 'running') THEN
        RAISE EXCEPTION 'Only a pending or running stage of an active batch can be extended' USING ERRCODE = '55000';
    END IF;

    v_shift := make_interval(mins => p_minutes);

    UPDATE batch_stages
    SET planned_end_min_at = planned_end_min_at + v_shift,
        planned_end_max_at = planned_end_max_at + v_shift,
        extended_minutes   = extended_minutes + p_minutes
    WHERE id = p_stage_id;

    UPDATE batch_stages
    SET planned_start_at   = planned_start_at + v_shift,
        planned_end_min_at = planned_end_min_at + v_shift,
        planned_end_max_at = planned_end_max_at + v_shift
    WHERE batch_item_id = st.batch_item_id AND sort_order > st.sort_order AND status = 'pending';

    UPDATE reminders
    SET due_at = due_at + v_shift
    WHERE status = 'scheduled'
      AND batch_stage_id IN (SELECT id FROM batch_stages
                             WHERE batch_item_id = st.batch_item_id AND sort_order > st.sort_order);

    UPDATE batches
    SET planned_ready_from = s.ready_from,
        planned_ready_to   = s.ready_to
    FROM (SELECT max(bs.planned_end_min_at) AS ready_from, max(bs.planned_end_max_at) AS ready_to
          FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
          WHERE bi.batch_id = v_batch) s
    WHERE id = v_batch;
END;
$$;

COMMENT ON PROCEDURE sp_extend_batch_stage(uuid, integer) IS
    'Extends a stage by N minutes (button "+30 min") and shifts the following stages, reminders and the ready window (US-24).';

------------------------------------------------------------------------------------------------
-- Publish a recipe to the shared library (US-39): an immutable snapshot of the effective recipe.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE sp_publish_recipe(p_recipe_id uuid, INOUT p_published_recipe_id uuid DEFAULT NULL)
LANGUAGE plpgsql
AS $$
DECLARE
    h          record;
    v_snapshot jsonb;
BEGIN
    SELECT * INTO h FROM fn_recipe_effective_header(p_recipe_id);
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Recipe % not found', p_recipe_id USING ERRCODE = 'P0002';
    END IF;

    v_snapshot := jsonb_build_object(
        'schemaVersion', 1,
        'yieldLoaves', h.yield_loaves,
        'loafWeightG', h.loaf_weight_g,
        'ingredients', coalesce((
            SELECT jsonb_agg(jsonb_build_object(
                       'name', i.name, 'category', i.category_code, 'unit', i.unit_code,
                       'quantity', e.quantity, 'isOptional', e.is_optional)
                   ORDER BY e.sort_order, e.line_id)
            FROM fn_recipe_effective_ingredients(p_recipe_id) e
            JOIN ingredients i ON i.id = e.ingredient_id), '[]'::jsonb),
        'stages', coalesce((
            SELECT jsonb_agg(jsonb_build_object(
                       'name', s.name, 'stageType', s.stage_type_code,
                       'minMinutes', s.min_duration_minutes, 'maxMinutes', s.max_duration_minutes,
                       'temperatureC', s.temperature_c, 'proofMode', s.proof_mode, 'isOptional', s.is_optional)
                   ORDER BY s.sort_order, s.line_id)
            FROM fn_recipe_effective_stages(p_recipe_id) s), '[]'::jsonb));

    INSERT INTO published_recipes (author_id, source_recipe_id, title, description, snapshot, status, published_at)
    VALUES (h.owner_id, p_recipe_id, h.title, h.description, v_snapshot, 'published', now())
    RETURNING id INTO p_published_recipe_id;
END;
$$;

COMMENT ON PROCEDURE sp_publish_recipe(uuid, uuid) IS
    'Publishes an immutable JSON snapshot of the effective recipe to the shared library and returns its id (US-39).';

------------------------------------------------------------------------------------------------
-- Anonymize a user (GDPR, LR-03): personal data and the private workspace are removed,
-- public contributions (published recipes, comments, reactions) stay, signed "Anonymous baker".
-- Full deletion of the account is a plain DELETE FROM users (cascade, US-04).
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_random_disabled_password_hash()
RETURNS text
LANGUAGE sql VOLATILE
AS $$
    -- A valid v3 hash (HMAC-SHA512, 100000 iterations) of random bytes: nobody knows a matching password.
    -- header: 01 | PRF 00000002 (HMAC-SHA512) | iterations 000186a0 (100000) | salt length 00000010 (16)
    SELECT replace(encode('\x0100000002000186a000000010'::bytea
                          || uuid_send(gen_random_uuid())
                          || uuid_send(gen_random_uuid()) || uuid_send(gen_random_uuid()), 'base64'), E'\n', '');
$$;

CREATE OR REPLACE PROCEDURE sp_anonymize_user(p_user_id uuid)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM users WHERE id = p_user_id) THEN
        RAISE EXCEPTION 'User % not found', p_user_id USING ERRCODE = 'P0002';
    END IF;

    DELETE FROM push_subscriptions WHERE user_id = p_user_id;
    DELETE FROM chat_conversations WHERE owner_id = p_user_id;
    DELETE FROM reminders          WHERE owner_id = p_user_id;
    DELETE FROM bake_records       WHERE owner_id = p_user_id;
    DELETE FROM batches            WHERE owner_id = p_user_id;
    DELETE FROM starters           WHERE owner_id = p_user_id;
    DELETE FROM recipes            WHERE owner_id = p_user_id;
    DELETE FROM ingredients        WHERE owner_id = p_user_id;
    DELETE FROM flours             WHERE owner_id = p_user_id;
    DELETE FROM kitchen_settings   WHERE user_id  = p_user_id;

    UPDATE users
    SET email            = 'anonymized-' || id || '@anonymized.invalid',
        normalized_email = upper('anonymized-' || id || '@anonymized.invalid'),
        display_name     = 'Anonymous baker',
        password_hash    = fn_random_disabled_password_hash(),
        role             = 'user',
        last_login_at    = NULL
    WHERE id = p_user_id;
END;
$$;

COMMENT ON PROCEDURE sp_anonymize_user(uuid) IS
    'Removes personal data and the private workspace of a user, keeps public contributions as "Anonymous baker" (GDPR).';

COMMENT ON FUNCTION fn_random_disabled_password_hash() IS 'Valid PBKDF2 hash of random bytes: disables login of an anonymized account.';
