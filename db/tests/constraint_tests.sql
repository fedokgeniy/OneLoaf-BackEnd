-- Integrity tests: every statement below must be REJECTED by the database with an integrity error
-- (SQLSTATE class 23 - integrity constraint violation), not by a permission or syntax error.
-- Run after db/seed/test_data.sql as the application user:
--   psql -h <host> -U oneloaf_app -d oneloaf -v ON_ERROR_STOP=1 -f db/tests/constraint_tests.sql
-- Runs in a transaction that is rolled back; psql exits with an error if any test fails.

\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE test_results (no serial, name text, passed boolean, detail text) ON COMMIT DROP;

-- Executes the statement, forces deferred constraints to be checked and expects an error of class 23.
CREATE FUNCTION pg_temp.expect_reject(p_name text, p_sql text) RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE p_sql;
        SET CONSTRAINTS ALL IMMEDIATE;
        SET CONSTRAINTS ALL DEFERRED;
        INSERT INTO test_results (name, passed, detail) VALUES (p_name, false, 'statement was accepted');
    EXCEPTION WHEN others THEN
        INSERT INTO test_results (name, passed, detail)
        VALUES (p_name, SQLSTATE LIKE '23%', SQLSTATE || ' ' || SQLERRM);
    END;
END;
$$;

-- Users and passwords (NFR-04)
SELECT pg_temp.expect_reject('duplicate e-mail (case-insensitive, US-01)',
  $q$INSERT INTO users (email, normalized_email, password_hash, display_name, role)
     SELECT 'Anna.Baker@example.com', 'ANNA.BAKER@EXAMPLE.COM', password_hash, 'X', 'user' FROM users LIMIT 1$q$);
SELECT pg_temp.expect_reject('normalized e-mail must equal upper(email)',
  $q$UPDATE users SET normalized_email = 'anna.baker@example.com' WHERE email = 'anna.baker@example.com'$q$);
SELECT pg_temp.expect_reject('invalid e-mail format',
  $q$UPDATE users SET email = 'not-an-email', normalized_email = 'NOT-AN-EMAIL' WHERE email = 'anna.baker@example.com'$q$);
SELECT pg_temp.expect_reject('password stored in plain text',
  $q$UPDATE users SET password_hash = 'Secret123!' WHERE email = 'anna.baker@example.com'$q$);
SELECT pg_temp.expect_reject('password as MD5 hex',
  $q$UPDATE users SET password_hash = '5f4dcc3b5aa765d61d8327deb882cf99' WHERE email = 'anna.baker@example.com'$q$);
SELECT pg_temp.expect_reject('PBKDF2 with HMAC-SHA1 (outdated PRF)',
  $q$UPDATE users SET password_hash = replace(encode('\x0100000000000186a000000010'::bytea || decode(repeat('ab', 48), 'hex'), 'base64'), E'\n', '')
     WHERE email = 'anna.baker@example.com'$q$);
SELECT pg_temp.expect_reject('PBKDF2 with too few iterations (1000)',
  $q$UPDATE users SET password_hash = replace(encode('\x0100000002000003e800000010'::bytea || decode(repeat('ab', 48), 'hex'), 'base64'), E'\n', '')
     WHERE email = 'anna.baker@example.com'$q$);
SELECT pg_temp.expect_reject('unknown role',
  $q$UPDATE users SET role = 'superuser' WHERE email = 'anna.baker@example.com'$q$);

-- Reference data
SELECT pg_temp.expect_reject('unknown unit code',
  $q$UPDATE ingredients SET unit_code = 'cup' WHERE name = 'Salt' AND unit_code = 'g'$q$);
SELECT pg_temp.expect_reject('unknown currency',
  $q$UPDATE kitchen_settings SET currency = 'XYZ'$q$);
SELECT pg_temp.expect_reject('unknown stage type',
  $q$UPDATE recipe_stages SET stage_type_code = 'freeze' WHERE name = 'Mix'$q$);

-- Kitchen and prices (US-12, US-13, US-48)
SELECT pg_temp.expect_reject('loss rate over 100 %',
  $q$UPDATE kitchen_settings SET loss_rate_percent = 150$q$);
SELECT pg_temp.expect_reject('oven power 0',
  $q$UPDATE kitchen_settings SET oven_power_kw = 0 WHERE oven_power_kw IS NOT NULL$q$);
SELECT pg_temp.expect_reject('two prices of one ingredient on the same date',
  $q$INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
     VALUES ('00000000-0000-4000-1001-000000000001', 9.99, 1000, 'EUR', '2026-10-01')$q$);
SELECT pg_temp.expect_reject('negative price',
  $q$INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
     VALUES ('00000000-0000-4000-1001-000000000001', -1, 1000, 'EUR', '2026-12-01')$q$);
SELECT pg_temp.expect_reject('price for zero quantity',
  $q$INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
     VALUES ('00000000-0000-4000-1001-000000000001', 1, 0, 'EUR', '2026-12-01')$q$);

-- Workspace isolation (US-03): references across users are rejected by the triggers
SELECT pg_temp.expect_reject('recipe line with an ingredient of another user',
  $q$INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, is_optional, sort_order, is_removed)
     VALUES ('00000000-0000-4000-2001-000000000001', '00000000-0000-4000-1001-000000000011', 10, false, 9, false)$q$);
SELECT pg_temp.expect_reject('variant of a recipe of another user',
  $q$INSERT INTO recipes (owner_id, parent_recipe_id, title, yield_loaves, is_archived)
     VALUES ('00000000-0000-4000-a001-0000000000a2', '00000000-0000-4000-2001-000000000001', 'Stolen', 1, false)$q$);
SELECT pg_temp.expect_reject('batch item with a recipe of another user',
  $q$INSERT INTO batch_items (batch_id, recipe_id, loaves, sort_order)
     VALUES ('00000000-0000-4000-6001-000000000001', '00000000-0000-4000-2001-000000000005', 1, 9)$q$);
SELECT pg_temp.expect_reject('batch item with a starter of another user',
  $q$UPDATE batch_items SET starter_id = '00000000-0000-4000-5001-000000000003' WHERE id = '00000000-0000-4000-b001-000000000001'$q$);
SELECT pg_temp.expect_reject('ingredient linked to a flour of another user',
  $q$UPDATE ingredients SET flour_id = '00000000-0000-4000-f001-000000000003' WHERE id = '00000000-0000-4000-1001-000000000001'$q$);
SELECT pg_temp.expect_reject('reminder for a starter of another user',
  $q$INSERT INTO reminders (owner_id, kind, starter_id, due_at, channel, status, message, attempts)
     VALUES ('00000000-0000-4000-a001-0000000000a1', 'starter_feeding', '00000000-0000-4000-5001-000000000003', now() + interval '1 day', 'push', 'scheduled', 'x', 0)$q$);
SELECT pg_temp.expect_reject('publication of a recipe of another user',
  $q$INSERT INTO published_recipes (author_id, source_recipe_id, title, snapshot, status, published_at)
     SELECT '00000000-0000-4000-a001-0000000000a2', '00000000-0000-4000-2001-000000000001', title, snapshot, 'published', now()
     FROM published_recipes LIMIT 1$q$);
SELECT pg_temp.expect_reject('moving a recipe to another workspace',
  $q$UPDATE recipes SET owner_id = '00000000-0000-4000-a001-0000000000a2' WHERE id = '00000000-0000-4000-2001-000000000004'$q$);

-- Recipes and variants (US-05, US-07, US-09, US-16)
SELECT pg_temp.expect_reject('stage max < min',
  $q$UPDATE recipe_stages SET max_duration_minutes = 1 WHERE name = 'Bulk fermentation'$q$);
SELECT pg_temp.expect_reject('active minutes longer than the stage',
  $q$UPDATE recipe_stages SET active_minutes = 999 WHERE name = 'Mix'$q$);
SELECT pg_temp.expect_reject('proof mode on a non-proof stage',
  $q$UPDATE recipe_stages SET proof_mode = 'cold' WHERE name = 'Mix'$q$);
SELECT pg_temp.expect_reject('recipe is its own parent',
  $q$UPDATE recipes SET parent_recipe_id = id WHERE id = '00000000-0000-4000-2001-000000000001'$q$);
SELECT pg_temp.expect_reject('cycle of variants (R1 -> R3 -> R2 -> R1)',
  $q$UPDATE recipes SET parent_recipe_id = '00000000-0000-4000-2001-000000000003' WHERE id = '00000000-0000-4000-2001-000000000001'$q$);
SELECT pg_temp.expect_reject('variant with another yield than its parent',
  $q$UPDATE recipes SET yield_loaves = 2 WHERE id = '00000000-0000-4000-2001-000000000002'$q$);
SELECT pg_temp.expect_reject('changing the yield of a recipe that has variants',
  $q$UPDATE recipes SET yield_loaves = 2 WHERE id = '00000000-0000-4000-2001-000000000001'$q$);
SELECT pg_temp.expect_reject('variant stores its own description instead of overrides',
  $q$UPDATE recipes SET description = 'x' WHERE id = '00000000-0000-4000-2001-000000000002'$q$);
SELECT pg_temp.expect_reject('overrides on a base recipe',
  $q$UPDATE recipes SET variant_overrides = '{"notes": "x"}' WHERE id = '00000000-0000-4000-2001-000000000001'$q$);
SELECT pg_temp.expect_reject('override with an unknown key',
  $q$UPDATE recipes SET variant_overrides = '{"title": "x"}' WHERE id = '00000000-0000-4000-2001-000000000002'$q$);
SELECT pg_temp.expect_reject('override with a wrong type',
  $q$UPDATE recipes SET variant_overrides = '{"loafWeightG": "heavy"}' WHERE id = '00000000-0000-4000-2001-000000000002'$q$);
SELECT pg_temp.expect_reject('base recipe cannot replace lines',
  $q$INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, is_optional, sort_order, replaces_item_id, is_removed)
     VALUES ('00000000-0000-4000-2001-000000000001', '00000000-0000-4000-1001-000000000001', 1, false, 9, '00000000-0000-4000-3001-000000000002', false)$q$);
SELECT pg_temp.expect_reject('variant replaces a line that is not in its parent',
  $q$INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, is_optional, sort_order, replaces_item_id, is_removed)
     VALUES ('00000000-0000-4000-2001-000000000002', '00000000-0000-4000-1001-000000000001', 1, false, 9, '00000000-0000-4000-3001-000000000051', false)$q$);
SELECT pg_temp.expect_reject('variant changes the same parent line twice',
  $q$INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, is_optional, sort_order, replaces_item_id, is_removed)
     VALUES ('00000000-0000-4000-2001-000000000002', '00000000-0000-4000-1001-000000000001', 450, false, 1, '00000000-0000-4000-3001-000000000001', false)$q$);
SELECT pg_temp.expect_reject('removal without a target line',
  $q$INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, is_optional, sort_order, is_removed)
     VALUES ('00000000-0000-4000-2001-000000000002', '00000000-0000-4000-1001-000000000003', 0, false, 9, true)$q$);
SELECT pg_temp.expect_reject('delete a parent recipe that has variants',
  $q$DELETE FROM recipes WHERE id = '00000000-0000-4000-2001-000000000002'$q$);
SELECT pg_temp.expect_reject('delete an ingredient used in a recipe',
  $q$DELETE FROM ingredients WHERE id = '00000000-0000-4000-1001-000000000003'$q$);
SELECT pg_temp.expect_reject('delete a recipe used in a batch',
  $q$DELETE FROM recipes WHERE id = '00000000-0000-4000-2001-000000000007'$q$);

-- Batches and stages (US-20, US-23)
SELECT pg_temp.expect_reject('batch created directly as done',
  $q$INSERT INTO batches (owner_id, name, status, source, planned_start_at, price_date, currency)
     VALUES ('00000000-0000-4000-a001-0000000000a1', 'x', 'done', 'manual', now(), current_date, 'EUR')$q$);
SELECT pg_temp.expect_reject('status done -> planned',
  $q$UPDATE batches SET status = 'planned' WHERE id = '00000000-0000-4000-6001-000000000002'$q$);
SELECT pg_temp.expect_reject('status planned -> done (skipping in_progress)',
  $q$UPDATE batches SET status = 'done' WHERE id = '00000000-0000-4000-6001-000000000001'$q$);
SELECT pg_temp.expect_reject('cancelled batch cannot be restarted',
  $q$UPDATE batches SET status = 'in_progress' WHERE id = '00000000-0000-4000-6001-000000000003'$q$);
SELECT pg_temp.expect_reject('zero loaves',
  $q$UPDATE batch_items SET loaves = 0 WHERE id = '00000000-0000-4000-b001-000000000001'$q$);
SELECT pg_temp.expect_reject('actual end before actual start',
  $q$UPDATE batch_stages SET actual_end_at = actual_start_at - interval '1 minute'
     WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 1$q$);
SELECT pg_temp.expect_reject('actual end without actual start',
  $q$UPDATE batch_stages SET actual_end_at = now()
     WHERE batch_item_id = '00000000-0000-4000-b001-000000000001' AND sort_order = 5$q$);
SELECT pg_temp.expect_reject('planned end before planned start',
  $q$UPDATE batch_stages SET planned_end_min_at = planned_start_at - interval '1 minute'
     WHERE batch_item_id = '00000000-0000-4000-b001-000000000001' AND sort_order = 1$q$);
SELECT pg_temp.expect_reject('batch stage from a recipe that is not the item recipe',
  $q$UPDATE batch_stages SET recipe_stage_id = '00000000-0000-4000-4001-000000000051'
     WHERE batch_item_id = '00000000-0000-4000-b001-000000000001' AND sort_order = 1$q$);

-- Starters and reminders (US-27-30)
SELECT pg_temp.expect_reject('hydration 0',
  $q$UPDATE starters SET hydration_percent = 0 WHERE name = 'Rye mother'$q$);
SELECT pg_temp.expect_reject('two starters with the same name in one workspace',
  $q$INSERT INTO starters (owner_id, name, hydration_percent, feeding_mode, feeding_interval_hours, is_active)
     VALUES ('00000000-0000-4000-a001-0000000000a1', 'Rye mother', 100, 'regular', 24, true)$q$);
SELECT pg_temp.expect_reject('second scheduled feeding reminder of a starter',
  $q$INSERT INTO reminders (owner_id, kind, starter_id, due_at, channel, status, message, attempts)
     VALUES ('00000000-0000-4000-a001-0000000000a1', 'starter_feeding', '00000000-0000-4000-5001-000000000001', now() + interval '2 days', 'push', 'scheduled', 'x', 0)$q$);
SELECT pg_temp.expect_reject('reminder without a target',
  $q$INSERT INTO reminders (owner_id, kind, due_at, channel, status, message, attempts)
     VALUES ('00000000-0000-4000-a001-0000000000a1', 'stage_start', now(), 'push', 'scheduled', 'x', 0)$q$);
SELECT pg_temp.expect_reject('sent reminder without sent_at',
  $q$UPDATE reminders SET status = 'sent' WHERE kind = 'before_kneading' AND status = 'scheduled'$q$);

-- History (US-31, US-32)
SELECT pg_temp.expect_reject('rating 6',
  $q$UPDATE bake_records SET rating = 6$q$);
SELECT pg_temp.expect_reject('second history record of one batch',
  $q$INSERT INTO bake_records (owner_id, batch_id, baked_on, total_loaves, currency)
     VALUES ('00000000-0000-4000-a001-0000000000a1', '00000000-0000-4000-6001-000000000002', '2026-11-18', 1, 'EUR')$q$);

-- Assistant (US-35)
SELECT pg_temp.expect_reject('tool message without a tool name',
  $q$INSERT INTO chat_messages (conversation_id, role, content) VALUES ('00000000-0000-4000-8001-000000000001', 'tool', 'x')$q$);
SELECT pg_temp.expect_reject('tool payload that is not a JSON object',
  $q$INSERT INTO chat_messages (conversation_id, role, content, tool_name, tool_payload)
     VALUES ('00000000-0000-4000-8001-000000000001', 'tool', 'x', 'calculate_cost', '[1, 2]')$q$);

-- Shared library and moderation (US-39-41, LR-11)
SELECT pg_temp.expect_reject('snapshot without ingredients',
  $q$INSERT INTO published_recipes (author_id, title, snapshot, status, published_at)
     VALUES ('00000000-0000-4000-a001-0000000000a1', 'x', '{"schemaVersion": 1, "yieldLoaves": 1, "ingredients": [], "stages": []}', 'published', now())$q$);
SELECT pg_temp.expect_reject('snapshot with an unknown key',
  $q$INSERT INTO published_recipes (author_id, title, snapshot, status, published_at)
     SELECT '00000000-0000-4000-a001-0000000000a1', 'x', snapshot || '{"price": 5}', 'published', now() FROM published_recipes LIMIT 1$q$);
SELECT pg_temp.expect_reject('published snapshot is immutable',
  $q$UPDATE published_recipes SET snapshot = jsonb_set(snapshot, '{yieldLoaves}', '2') WHERE status = 'published'$q$);
SELECT pg_temp.expect_reject('removed publication without a reason',
  $q$UPDATE published_recipes SET status = 'removed' WHERE status = 'published'$q$);
SELECT pg_temp.expect_reject('publication removed by a moderator cannot be republished',
  $q$UPDATE published_recipes SET status = 'published' WHERE status = 'removed'$q$);
SELECT pg_temp.expect_reject('second reaction of the same user',
  $q$INSERT INTO recipe_reactions (published_recipe_id, user_id, reaction)
     SELECT published_recipe_id, user_id, 'love' FROM recipe_reactions LIMIT 1$q$);
SELECT pg_temp.expect_reject('empty comment',
  $q$INSERT INTO recipe_comments (published_recipe_id, author_id, body, is_removed)
     SELECT id, '00000000-0000-4000-a001-0000000000a4', '', false FROM published_recipes LIMIT 1$q$);
SELECT pg_temp.expect_reject('report with two targets',
  $q$INSERT INTO content_reports (reporter_id, published_recipe_id, comment_id, reason, status)
     SELECT '00000000-0000-4000-a001-0000000000a4', c.published_recipe_id, c.id, 'x', 'open' FROM recipe_comments c LIMIT 1$q$);
SELECT pg_temp.expect_reject('report resolved by a non-administrator',
  $q$UPDATE content_reports SET status = 'resolved', resolved_by_id = '00000000-0000-4000-a001-0000000000a1' WHERE status = 'open'$q$);

-- Account deletion (US-04, LR-06): the whole workspace of the user disappears, other users are untouched
DO $$
DECLARE
    v_left   bigint;
    v_others bigint;
BEGIN
    SELECT count(*) INTO v_others FROM recipes WHERE owner_id <> '00000000-0000-4000-a001-0000000000a1';
    DELETE FROM users WHERE id = '00000000-0000-4000-a001-0000000000a1';
    SET CONSTRAINTS ALL IMMEDIATE;
    SELECT (SELECT count(*) FROM recipes            WHERE owner_id = '00000000-0000-4000-a001-0000000000a1')
         + (SELECT count(*) FROM ingredients        WHERE owner_id = '00000000-0000-4000-a001-0000000000a1')
         + (SELECT count(*) FROM batches            WHERE owner_id = '00000000-0000-4000-a001-0000000000a1')
         + (SELECT count(*) FROM starters           WHERE owner_id = '00000000-0000-4000-a001-0000000000a1')
         + (SELECT count(*) FROM bake_records       WHERE owner_id = '00000000-0000-4000-a001-0000000000a1')
         + (SELECT count(*) FROM chat_conversations WHERE owner_id = '00000000-0000-4000-a001-0000000000a1')
         + (SELECT count(*) FROM published_recipes  WHERE author_id = '00000000-0000-4000-a001-0000000000a1')
         + (SELECT count(*) FROM ingredient_prices p WHERE p.ingredient_id = '00000000-0000-4000-1001-000000000001')
    INTO v_left;
    INSERT INTO test_results (name, passed, detail)
    VALUES ('account deletion removes the whole workspace (US-04)', v_left = 0, v_left || ' rows left');
    INSERT INTO test_results (name, passed, detail)
    VALUES ('account deletion keeps other workspaces', (SELECT count(*) FROM recipes) = v_others,
            (SELECT count(*) FROM recipes) || ' of ' || v_others || ' recipes of other users');
END;
$$;

SELECT no, name, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS result, left(detail, 110) AS detail FROM test_results ORDER BY no;
SELECT count(*) FILTER (WHERE passed) AS passed, count(*) AS total FROM test_results;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM test_results WHERE NOT passed) THEN
        RAISE EXCEPTION 'Constraint tests failed: %', (SELECT string_agg(name, '; ') FROM test_results WHERE NOT passed);
    END IF;
END;
$$;

ROLLBACK;
