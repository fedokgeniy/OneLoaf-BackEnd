-- Business rules of the production workflow (E5, E6, E7, E9) enforced by triggers.

------------------------------------------------------------------------------------------------
-- Batch life cycle (US-20, US-23, US-31)
--   planned -> in_progress -> done
--   planned | in_progress -> cancelled
--   done and cancelled are final. A batch is always created as planned.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_batches_status_before()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.status <> 'planned' THEN
            RAISE EXCEPTION 'A batch is created with status planned, got %', NEW.status USING ERRCODE = '23514';
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.status = OLD.status THEN
        RETURN NEW;
    END IF;
    IF NOT ((OLD.status = 'planned'     AND NEW.status IN ('in_progress', 'cancelled'))
         OR (OLD.status = 'in_progress' AND NEW.status IN ('done', 'cancelled'))) THEN
        RAISE EXCEPTION 'Batch status cannot change from % to %', OLD.status, NEW.status USING ERRCODE = '23514';
    END IF;
    IF NEW.status = 'in_progress' THEN
        NEW.actual_start_at := coalesce(NEW.actual_start_at, now());
    ELSIF NEW.status = 'done' THEN
        NEW.completed_at := coalesce(NEW.completed_at, now());
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_batches_status_before
    BEFORE INSERT OR UPDATE OF status ON batches
    FOR EACH ROW EXECUTE FUNCTION trg_batches_status_before();

-- History record for a finished batch (US-31): date, items, loaves and the cost on the bake date.
CREATE OR REPLACE FUNCTION fn_create_bake_record(p_batch_id uuid)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    b         batches%ROWTYPE;
    v_id      uuid;
    v_date    date;
BEGIN
    SELECT * INTO b FROM batches WHERE id = p_batch_id;
    v_date := coalesce(b.completed_at, now())::date;

    INSERT INTO bake_records (owner_id, batch_id, baked_on, total_loaves, total_cost, currency)
    VALUES (b.owner_id, b.id, v_date, 0, NULL, b.currency)
    ON CONFLICT (batch_id) DO NOTHING
    RETURNING id INTO v_id;
    IF v_id IS NULL THEN
        RETURN (SELECT id FROM bake_records WHERE batch_id = p_batch_id);
    END IF;

    INSERT INTO bake_record_items (bake_record_id, recipe_id, recipe_title, loaves, cost)
    SELECT v_id, bi.recipe_id, h.title, bi.loaves,
           (SELECT c.amount FROM fn_recipe_cost(bi.recipe_id, bi.loaves, v_date) c
            WHERE c.line_type = 'total' AND c.is_complete AND c.currency = b.currency)
    FROM batch_items bi
    CROSS JOIN LATERAL fn_recipe_effective_header(bi.recipe_id) h
    WHERE bi.batch_id = p_batch_id
    ORDER BY bi.sort_order;

    -- total_loaves and total_cost are a snapshot of the items (see docs/db/design.md, denormalization)
    UPDATE bake_records r
    SET total_loaves = s.loaves,
        total_cost   = CASE WHEN s.missing = 0 THEN s.cost END
    FROM (SELECT coalesce(sum(loaves), 0) AS loaves, sum(cost) AS cost, count(*) FILTER (WHERE cost IS NULL) AS missing
          FROM bake_record_items WHERE bake_record_id = v_id) s
    WHERE r.id = v_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION fn_create_bake_record(uuid) IS
    'Creates the baking history record of a finished batch with a cost snapshot on the bake date (US-31). Idempotent.';

CREATE OR REPLACE FUNCTION trg_batches_status_after()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.status = 'done' THEN
        PERFORM fn_create_bake_record(NEW.id);
    END IF;
    IF NEW.status IN ('done', 'cancelled') THEN
        -- reminders of a finished or cancelled batch are no longer needed
        UPDATE reminders r
        SET status = 'cancelled'
        WHERE r.status = 'scheduled'
          AND (r.batch_item_id IN (SELECT id FROM batch_items WHERE batch_id = NEW.id)
               OR r.batch_stage_id IN (SELECT bs.id FROM batch_stages bs
                                       JOIN batch_items bi ON bi.id = bs.batch_item_id
                                       WHERE bi.batch_id = NEW.id));
    END IF;
    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_batches_status_after
    AFTER UPDATE OF status ON batches
    FOR EACH ROW WHEN (NEW.status IS DISTINCT FROM OLD.status)
    EXECUTE FUNCTION trg_batches_status_after();

------------------------------------------------------------------------------------------------
-- Stage execution (US-23): status follows the recorded times; the first started stage starts the batch.
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_batch_stages_progress_before()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.status = 'skipped' THEN
        RETURN NEW;
    END IF;
    IF NEW.actual_end_at IS NOT NULL THEN
        NEW.status := 'done';
    ELSIF NEW.actual_start_at IS NOT NULL THEN
        NEW.status := 'running';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_batch_stages_progress_before
    BEFORE INSERT OR UPDATE OF actual_start_at, actual_end_at, status ON batch_stages
    FOR EACH ROW EXECUTE FUNCTION trg_batch_stages_progress_before();

CREATE OR REPLACE FUNCTION trg_batch_stages_progress_after()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE batches b
    SET status = 'in_progress',
        actual_start_at = coalesce(b.actual_start_at, NEW.actual_start_at)
    FROM batch_items bi
    WHERE bi.id = NEW.batch_item_id AND b.id = bi.batch_id AND b.status = 'planned';
    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_batch_stages_progress_after
    AFTER UPDATE OF actual_start_at ON batch_stages
    FOR EACH ROW WHEN (OLD.actual_start_at IS NULL AND NEW.actual_start_at IS NOT NULL)
    EXECUTE FUNCTION trg_batch_stages_progress_after();

------------------------------------------------------------------------------------------------
-- Starter feeding schedule (US-28, US-29, US-30)
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_starter_feedings_schedule_before()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    s starters%ROWTYPE;
BEGIN
    SELECT * INTO s FROM starters WHERE id = NEW.starter_id;
    NEW.next_feeding_at := coalesce(NEW.next_feeding_at, NEW.fed_at + make_interval(hours => s.feeding_interval_hours));
    IF s.rise_hours IS NOT NULL THEN
        NEW.expected_peak_at := coalesce(NEW.expected_peak_at, NEW.fed_at + make_interval(secs => s.rise_hours * 3600));
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_starter_feedings_schedule_before
    BEFORE INSERT ON starter_feedings
    FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_schedule_before();

-- After a feeding the previous feeding reminder is cancelled and the next one is scheduled.
CREATE OR REPLACE FUNCTION trg_starter_feedings_schedule_after()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    s starters%ROWTYPE;
BEGIN
    SELECT * INTO s FROM starters WHERE id = NEW.starter_id;

    UPDATE reminders
    SET status = 'cancelled'
    WHERE starter_id = NEW.starter_id AND kind = 'starter_feeding' AND status = 'scheduled';

    IF s.is_active AND NEW.next_feeding_at > now()
       AND NEW.fed_at = (SELECT max(fed_at) FROM starter_feedings WHERE starter_id = NEW.starter_id) THEN
        INSERT INTO reminders (owner_id, kind, starter_id, due_at, channel, status, message, attempts)
        VALUES (s.owner_id, 'starter_feeding', s.id, NEW.next_feeding_at, 'push', 'scheduled',
                format('Time to feed starter "%s"', s.name), 0);
    END IF;
    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_starter_feedings_schedule_after
    AFTER INSERT ON starter_feedings
    FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_schedule_after();

------------------------------------------------------------------------------------------------
-- Shared library (US-39, US-41)
------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_published_recipes_status()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.status = 'unpublished' AND OLD.status <> 'unpublished' THEN
        NEW.unpublished_at := coalesce(NEW.unpublished_at, now());
    ELSIF NEW.status = 'published' AND OLD.status <> 'published' THEN
        IF OLD.status = 'removed' THEN
            RAISE EXCEPTION 'A publication removed by a moderator cannot be published again' USING ERRCODE = '23514';
        END IF;
        NEW.unpublished_at := NULL;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_published_recipes_status
    BEFORE UPDATE OF status ON published_recipes
    FOR EACH ROW EXECUTE FUNCTION trg_published_recipes_status();

CREATE OR REPLACE FUNCTION trg_recipe_comments_edited()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.body IS DISTINCT FROM OLD.body THEN
        NEW.edited_at := now();
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_recipe_comments_edited
    BEFORE UPDATE OF body ON recipe_comments
    FOR EACH ROW EXECUTE FUNCTION trg_recipe_comments_edited();

COMMENT ON FUNCTION trg_batches_status_before() IS 'Trigger: batch life cycle planned -> in_progress -> done | cancelled; sets actual_start_at and completed_at.';
COMMENT ON FUNCTION trg_batches_status_after() IS 'Trigger: on done creates the history record; on done/cancelled cancels scheduled reminders.';
COMMENT ON FUNCTION trg_batch_stages_progress_before() IS 'Trigger: stage status follows the recorded actual times (US-23).';
COMMENT ON FUNCTION trg_batch_stages_progress_after() IS 'Trigger: the first started stage moves the batch to in_progress.';
COMMENT ON FUNCTION trg_starter_feedings_schedule_before() IS 'Trigger: computes the next feeding time and the expected peak (US-28, US-30).';
COMMENT ON FUNCTION trg_starter_feedings_schedule_after() IS 'Trigger: replaces the scheduled feeding reminder of the starter (US-29).';
COMMENT ON FUNCTION trg_published_recipes_status() IS 'Trigger: publication status rules; sets unpublished_at; removed publications cannot be republished.';
COMMENT ON FUNCTION trg_recipe_comments_edited() IS 'Trigger: sets edited_at when the comment text changes.';
