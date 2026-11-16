-- Workspace isolation at the database level (US-03, NFR-03).
-- A foreign key only proves that the referenced row exists. These triggers also prove that it belongs
-- to the same user, so a row of one workspace can never point to a row of another workspace,
-- even if the application has a bug. Violations raise SQLSTATE 23000 (integrity_constraint_violation).

CREATE OR REPLACE FUNCTION fn_assert_same_owner(p_expected uuid, p_actual uuid, p_what text)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_actual IS DISTINCT FROM p_expected THEN
        RAISE EXCEPTION 'Workspace isolation: % belongs to another user', p_what
            USING ERRCODE = '23000';
    END IF;
END;
$$;

-- recipes: parent in the same workspace, no cycles, variants share the scaling base of the parent.
CREATE OR REPLACE FUNCTION trg_recipes_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_parent  recipes%ROWTYPE;
    v_cursor  uuid;
    v_depth   integer := 0;
BEGIN
    IF NEW.parent_recipe_id IS NOT NULL THEN
        SELECT * INTO v_parent FROM recipes WHERE id = NEW.parent_recipe_id;
        PERFORM fn_assert_same_owner(NEW.owner_id, v_parent.owner_id, 'parent recipe');
        IF NEW.yield_loaves <> v_parent.yield_loaves THEN
            RAISE EXCEPTION 'A variant must have the same yield_loaves as its parent (% <> %)', NEW.yield_loaves, v_parent.yield_loaves
                USING ERRCODE = '23514';
        END IF;
        -- walk up the chain: the new parent must not be the recipe itself or one of its variants
        v_cursor := NEW.parent_recipe_id;
        WHILE v_cursor IS NOT NULL LOOP
            IF v_cursor = NEW.id THEN
                RAISE EXCEPTION 'Recipe variants must not form a cycle' USING ERRCODE = '23514';
            END IF;
            v_depth := v_depth + 1;
            IF v_depth > 10 THEN
                RAISE EXCEPTION 'Variant chain is deeper than 10 levels' USING ERRCODE = '23514';
            END IF;
            SELECT parent_recipe_id INTO v_cursor FROM recipes WHERE id = v_cursor;
        END LOOP;
    END IF;

    IF TG_OP = 'UPDATE' AND NEW.yield_loaves <> OLD.yield_loaves
       AND EXISTS (SELECT 1 FROM recipes WHERE parent_recipe_id = NEW.id) THEN
        RAISE EXCEPTION 'yield_loaves of a recipe with variants cannot be changed' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'UPDATE' AND NEW.owner_id <> OLD.owner_id THEN
        RAISE EXCEPTION 'A recipe cannot be moved to another workspace' USING ERRCODE = '23000';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_recipes_validate
    BEFORE INSERT OR UPDATE OF parent_recipe_id, owner_id, yield_loaves ON recipes
    FOR EACH ROW EXECUTE FUNCTION trg_recipes_validate();

-- ingredients / starters: the linked flour is from the same catalog.
CREATE OR REPLACE FUNCTION trg_flour_link_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.flour_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM flours WHERE id = NEW.flour_id), 'flour');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_ingredients_validate
    BEFORE INSERT OR UPDATE OF flour_id, owner_id ON ingredients
    FOR EACH ROW EXECUTE FUNCTION trg_flour_link_validate();

CREATE TRIGGER trg_starters_validate
    BEFORE INSERT OR UPDATE OF flour_id, owner_id ON starters
    FOR EACH ROW EXECUTE FUNCTION trg_flour_link_validate();

-- recipe_ingredients: ingredient from the same workspace; a replacing/removing line exists only in a variant
-- and targets a line that is effective in the parent.
CREATE OR REPLACE FUNCTION trg_recipe_ingredients_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_recipe recipes%ROWTYPE;
BEGIN
    SELECT * INTO v_recipe FROM recipes WHERE id = NEW.recipe_id;
    PERFORM fn_assert_same_owner(v_recipe.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.ingredient_id), 'ingredient');

    IF NEW.replaces_item_id IS NOT NULL THEN
        IF v_recipe.parent_recipe_id IS NULL THEN
            RAISE EXCEPTION 'Only a variant can replace or remove ingredient lines' USING ERRCODE = '23514';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM fn_recipe_effective_ingredients(v_recipe.parent_recipe_id) e
                       WHERE e.line_id = NEW.replaces_item_id) THEN
            RAISE EXCEPTION 'replaces_item_id must reference an ingredient line of the parent recipe' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_recipe_ingredients_validate
    BEFORE INSERT OR UPDATE ON recipe_ingredients
    FOR EACH ROW EXECUTE FUNCTION trg_recipe_ingredients_validate();

CREATE OR REPLACE FUNCTION trg_recipe_stages_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_parent_id uuid;
BEGIN
    IF NEW.replaces_stage_id IS NOT NULL THEN
        SELECT parent_recipe_id INTO v_parent_id FROM recipes WHERE id = NEW.recipe_id;
        IF v_parent_id IS NULL THEN
            RAISE EXCEPTION 'Only a variant can replace or remove stages' USING ERRCODE = '23514';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM fn_recipe_effective_stages(v_parent_id) e WHERE e.line_id = NEW.replaces_stage_id) THEN
            RAISE EXCEPTION 'replaces_stage_id must reference a stage of the parent recipe' USING ERRCODE = '23514';
        END IF;
    END IF;
    IF NEW.proof_mode IS NOT NULL AND NEW.stage_type_code <> 'proof' THEN
        RAISE EXCEPTION 'proof_mode is allowed only for proof stages' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_recipe_stages_validate
    BEFORE INSERT OR UPDATE ON recipe_stages
    FOR EACH ROW EXECUTE FUNCTION trg_recipe_stages_validate();

-- batch_items: recipe and starter from the batch owner's workspace.
CREATE OR REPLACE FUNCTION trg_batch_items_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_owner uuid;
BEGIN
    SELECT owner_id INTO v_owner FROM batches WHERE id = NEW.batch_id;
    PERFORM fn_assert_same_owner(v_owner, (SELECT owner_id FROM recipes WHERE id = NEW.recipe_id), 'recipe');
    IF NEW.starter_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(v_owner, (SELECT owner_id FROM starters WHERE id = NEW.starter_id), 'starter');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_batch_items_validate
    BEFORE INSERT OR UPDATE OF batch_id, recipe_id, starter_id ON batch_items
    FOR EACH ROW EXECUTE FUNCTION trg_batch_items_validate();

-- batch_stages: the source stage belongs to the effective stages of the item's recipe.
CREATE OR REPLACE FUNCTION trg_batch_stages_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.recipe_stage_id IS NOT NULL AND NOT EXISTS (
        SELECT 1
        FROM batch_items bi
        CROSS JOIN LATERAL fn_recipe_effective_stages(bi.recipe_id) e
        WHERE bi.id = NEW.batch_item_id AND e.line_id = NEW.recipe_stage_id) THEN
        RAISE EXCEPTION 'recipe_stage_id must be a stage of the recipe of this batch item' USING ERRCODE = '23000';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_batch_stages_validate
    BEFORE INSERT OR UPDATE OF batch_item_id, recipe_stage_id ON batch_stages
    FOR EACH ROW EXECUTE FUNCTION trg_batch_stages_validate();

-- reminders: target objects belong to the reminder owner.
CREATE OR REPLACE FUNCTION trg_reminders_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.starter_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM starters WHERE id = NEW.starter_id), 'starter');
    END IF;
    IF NEW.batch_item_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(NEW.owner_id,
            (SELECT b.owner_id FROM batch_items bi JOIN batches b ON b.id = bi.batch_id WHERE bi.id = NEW.batch_item_id), 'batch item');
    END IF;
    IF NEW.batch_stage_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(NEW.owner_id,
            (SELECT b.owner_id FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
                                                    JOIN batches b ON b.id = bi.batch_id
             WHERE bs.id = NEW.batch_stage_id), 'batch stage');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_reminders_validate
    BEFORE INSERT OR UPDATE OF owner_id, starter_id, batch_item_id, batch_stage_id ON reminders
    FOR EACH ROW EXECUTE FUNCTION trg_reminders_validate();

-- bake_records / bake_record_items: batch and recipe from the same workspace.
CREATE OR REPLACE FUNCTION trg_bake_records_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.batch_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM batches WHERE id = NEW.batch_id), 'batch');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_bake_records_validate
    BEFORE INSERT OR UPDATE OF owner_id, batch_id ON bake_records
    FOR EACH ROW EXECUTE FUNCTION trg_bake_records_validate();

CREATE OR REPLACE FUNCTION trg_bake_record_items_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.recipe_id IS NOT NULL THEN
        PERFORM fn_assert_same_owner((SELECT owner_id FROM bake_records WHERE id = NEW.bake_record_id),
                                     (SELECT owner_id FROM recipes WHERE id = NEW.recipe_id), 'recipe');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_bake_record_items_validate
    BEFORE INSERT OR UPDATE OF bake_record_id, recipe_id ON bake_record_items
    FOR EACH ROW EXECUTE FUNCTION trg_bake_record_items_validate();

-- published_recipes: the source recipe belongs to the author; the snapshot is immutable (US-39).
CREATE OR REPLACE FUNCTION trg_published_recipes_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.source_recipe_id IS NOT NULL
       AND (TG_OP = 'INSERT' OR NEW.source_recipe_id IS DISTINCT FROM OLD.source_recipe_id) THEN
        PERFORM fn_assert_same_owner(NEW.author_id, (SELECT owner_id FROM recipes WHERE id = NEW.source_recipe_id), 'source recipe');
    END IF;
    IF TG_OP = 'UPDATE' THEN
        IF NEW.snapshot IS DISTINCT FROM OLD.snapshot THEN
            RAISE EXCEPTION 'A published snapshot cannot be changed; publish a new version instead' USING ERRCODE = '23514';
        END IF;
        IF NEW.author_id <> OLD.author_id THEN
            RAISE EXCEPTION 'The author of a publication cannot be changed' USING ERRCODE = '23000';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_published_recipes_validate
    BEFORE INSERT OR UPDATE ON published_recipes
    FOR EACH ROW EXECUTE FUNCTION trg_published_recipes_validate();

-- content_reports: only an administrator resolves reports (LR-11).
CREATE OR REPLACE FUNCTION trg_content_reports_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.resolved_by_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM users WHERE id = NEW.resolved_by_id AND role = 'admin') THEN
        RAISE EXCEPTION 'Reports can be resolved only by an administrator' USING ERRCODE = '23514';
    END IF;
    IF NEW.status <> 'open' AND (TG_OP = 'INSERT' OR NEW.status <> OLD.status) THEN
        NEW.resolved_at := coalesce(NEW.resolved_at, now());
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_content_reports_validate
    BEFORE INSERT OR UPDATE ON content_reports
    FOR EACH ROW EXECUTE FUNCTION trg_content_reports_validate();

COMMENT ON FUNCTION fn_assert_same_owner(uuid, uuid, text) IS 'Raises 23000 when a referenced row belongs to another workspace (US-03).';
COMMENT ON FUNCTION trg_recipes_validate() IS 'Trigger: parent recipe in the same workspace, no cycles, depth <= 10, variant yield equals parent yield, no move between workspaces.';
COMMENT ON FUNCTION trg_flour_link_validate() IS 'Trigger: flour linked to an ingredient or starter belongs to the same workspace.';
COMMENT ON FUNCTION trg_recipe_ingredients_validate() IS 'Trigger: ingredient from the same workspace; replacing/removing lines only in variants and only for lines of the parent.';
COMMENT ON FUNCTION trg_recipe_stages_validate() IS 'Trigger: replacing/removing stages only in variants and only for stages of the parent; proof_mode only for proof stages.';
COMMENT ON FUNCTION trg_batch_items_validate() IS 'Trigger: recipe and starter of a batch item belong to the batch owner.';
COMMENT ON FUNCTION trg_batch_stages_validate() IS 'Trigger: the source recipe stage is a stage of the item recipe.';
COMMENT ON FUNCTION trg_reminders_validate() IS 'Trigger: the starter, batch item or stage of a reminder belongs to its owner.';
COMMENT ON FUNCTION trg_bake_records_validate() IS 'Trigger: the batch of a history record belongs to its owner.';
COMMENT ON FUNCTION trg_bake_record_items_validate() IS 'Trigger: the recipe of a history item belongs to the owner of the record.';
COMMENT ON FUNCTION trg_published_recipes_validate() IS 'Trigger: the source recipe belongs to the author; snapshot and author are immutable.';
COMMENT ON FUNCTION trg_content_reports_validate() IS 'Trigger: only an administrator resolves reports; sets resolved_at.';
