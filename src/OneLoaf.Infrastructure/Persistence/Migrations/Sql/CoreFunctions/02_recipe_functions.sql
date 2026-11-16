-- Recipe knowledge base: effective content of variants (US-07, US-08).
-- A variant stores only its differences; these functions assemble the full recipe
-- by walking up the parent chain. Multi-level variants (variant of a variant) are supported.

-- Effective ingredient lines of a recipe.
-- line_id is the id of the row that finally defines the line (the parent's row or the variant's replacement),
-- origin_recipe_id tells in which recipe of the chain that row lives.
CREATE OR REPLACE FUNCTION fn_recipe_effective_ingredients(p_recipe_id uuid)
RETURNS TABLE (
    line_id          uuid,
    origin_recipe_id uuid,
    ingredient_id    uuid,
    quantity         numeric,
    is_optional      boolean,
    sort_order       integer,
    note             text
)
LANGUAGE plpgsql STABLE STRICT
AS $$
#variable_conflict use_column
DECLARE
    v_parent_id uuid;
BEGIN
    SELECT r.parent_recipe_id INTO v_parent_id FROM recipes r WHERE r.id = p_recipe_id;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_parent_id IS NOT NULL THEN
        RETURN QUERY
        SELECT e.line_id, e.origin_recipe_id, e.ingredient_id, e.quantity, e.is_optional, e.sort_order, e.note
        FROM fn_recipe_effective_ingredients(v_parent_id) e
        WHERE NOT EXISTS (SELECT 1 FROM recipe_ingredients own
                          WHERE own.recipe_id = p_recipe_id AND own.replaces_item_id = e.line_id);
    END IF;

    RETURN QUERY
    SELECT ri.id, ri.recipe_id, ri.ingredient_id, ri.quantity, ri.is_optional, ri.sort_order, ri.note::text
    FROM recipe_ingredients ri
    WHERE ri.recipe_id = p_recipe_id AND NOT ri.is_removed;
END;
$$;

COMMENT ON FUNCTION fn_recipe_effective_ingredients(uuid) IS
    'Full ingredient list of a recipe or variant: inherited lines not replaced/removed by the variant plus its own lines (US-07).';

-- Effective stages of a recipe, same rules as for ingredients.
CREATE OR REPLACE FUNCTION fn_recipe_effective_stages(p_recipe_id uuid)
RETURNS TABLE (
    line_id              uuid,
    origin_recipe_id     uuid,
    sort_order           integer,
    name                 text,
    stage_type_code      text,
    min_duration_minutes integer,
    max_duration_minutes integer,
    active_minutes       integer,
    proof_mode           text,
    temperature_c        numeric,
    uses_oven            boolean,
    uses_mixer           boolean,
    is_optional          boolean
)
LANGUAGE plpgsql STABLE STRICT
AS $$
#variable_conflict use_column
DECLARE
    v_parent_id uuid;
BEGIN
    SELECT r.parent_recipe_id INTO v_parent_id FROM recipes r WHERE r.id = p_recipe_id;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_parent_id IS NOT NULL THEN
        RETURN QUERY
        SELECT e.*
        FROM fn_recipe_effective_stages(v_parent_id) e
        WHERE NOT EXISTS (SELECT 1 FROM recipe_stages own
                          WHERE own.recipe_id = p_recipe_id AND own.replaces_stage_id = e.line_id);
    END IF;

    RETURN QUERY
    SELECT rs.id, rs.recipe_id, rs.sort_order, rs.name::text, rs.stage_type_code::text,
           rs.min_duration_minutes, rs.max_duration_minutes, rs.active_minutes, rs.proof_mode::text,
           rs.temperature_c, rs.uses_oven, rs.uses_mixer, rs.is_optional
    FROM recipe_stages rs
    WHERE rs.recipe_id = p_recipe_id AND NOT rs.is_removed;
END;
$$;

COMMENT ON FUNCTION fn_recipe_effective_stages(uuid) IS
    'Full ordered stage list of a recipe or variant (US-07).';

-- Effective scalar fields of a recipe: own values for a base recipe;
-- parent values with variant_overrides applied for a variant.
CREATE OR REPLACE FUNCTION fn_recipe_effective_header(p_recipe_id uuid)
RETURNS TABLE (
    recipe_id     uuid,
    owner_id      uuid,
    title         text,
    yield_loaves  integer,
    description   text,
    loaf_weight_g numeric,
    notes         text
)
LANGUAGE plpgsql STABLE STRICT
AS $$
#variable_conflict use_column
DECLARE
    r recipes%ROWTYPE;
    p record;
BEGIN
    SELECT * INTO r FROM recipes WHERE id = p_recipe_id;
    IF NOT FOUND THEN
        RETURN;
    END IF;
    IF r.parent_recipe_id IS NULL THEN
        RETURN QUERY SELECT r.id, r.owner_id, r.title::text, r.yield_loaves, r.description::text, r.loaf_weight_g, r.notes::text;
        RETURN;
    END IF;

    SELECT * INTO p FROM fn_recipe_effective_header(r.parent_recipe_id);
    RETURN QUERY SELECT
        r.id, r.owner_id, r.title::text, r.yield_loaves,
        CASE WHEN r.variant_overrides ? 'description' THEN r.variant_overrides ->> 'description' ELSE p.description END,
        CASE WHEN r.variant_overrides ? 'loafWeightG' THEN (r.variant_overrides ->> 'loafWeightG')::numeric ELSE p.loaf_weight_g END,
        CASE WHEN r.variant_overrides ? 'notes' THEN r.variant_overrides ->> 'notes' ELSE p.notes END;
END;
$$;

COMMENT ON FUNCTION fn_recipe_effective_header(uuid) IS
    'Title, yield, description, loaf weight and notes of a recipe with variant overrides applied.';
