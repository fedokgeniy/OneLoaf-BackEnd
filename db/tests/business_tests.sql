-- Business logic tests: functions, procedures, triggers and views give the values required by the
-- acceptance criteria of the user stories. Run after db/seed/test_data.sql as the application user:
--   psql -h <host> -U oneloaf_app -d oneloaf -v ON_ERROR_STOP=1 -f db/tests/business_tests.sql
-- Runs in a transaction that is rolled back; psql exits with an error if any test fails.

\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE test_results (no serial, name text, passed boolean, detail text) ON COMMIT DROP;

CREATE FUNCTION pg_temp.check_equal(p_name text, p_actual anyelement, p_expected anyelement) RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO test_results (name, passed, detail)
    VALUES (p_name, p_actual IS NOT DISTINCT FROM p_expected,
            format('expected %s, got %s', coalesce(p_expected::text, 'NULL'), coalesce(p_actual::text, 'NULL')));
END;
$$;

-- ids from the test data
\set anna    '''00000000-0000-4000-a001-0000000000a1'''
\set boris   '''00000000-0000-4000-a001-0000000000a2'''
\set r1      '''00000000-0000-4000-2001-000000000001'''
\set r2      '''00000000-0000-4000-2001-000000000002'''
\set r3      '''00000000-0000-4000-2001-000000000003'''
\set r5      '''00000000-0000-4000-2001-000000000005'''
\set flour   '''00000000-0000-4000-1001-000000000001'''
\set b1      '''00000000-0000-4000-6001-000000000001'''
\set b2      '''00000000-0000-4000-6001-000000000002'''

-- US-15: price on a date
SELECT pg_temp.check_equal('US-15 price as of 15 Sep = 1.20', (SELECT price FROM fn_price_on_date(:flour, '2026-09-15')), 1.20::numeric);
SELECT pg_temp.check_equal('US-15 price as of 1 Oct = 1.35',  (SELECT price FROM fn_price_on_date(:flour, '2026-10-01')), 1.35::numeric);
SELECT pg_temp.check_equal('US-12 old prices are kept (3 rows)', (SELECT count(*) FROM ingredient_prices WHERE ingredient_id = :flour), 3::bigint);
SELECT pg_temp.check_equal('no price before the first one', (SELECT count(*) FROM fn_price_on_date(:flour, '2026-01-01')), 0::bigint);

-- US-07: effective content of variants
SELECT pg_temp.check_equal('US-07 variant R2 lines',
    (SELECT string_agg(i.name || '=' || trim(to_char(e.quantity, 'FM99990.000')), ', ' ORDER BY e.sort_order)
     FROM fn_recipe_effective_ingredients(:r2) e JOIN ingredients i ON i.id = e.ingredient_id),
    'Bread flour=400.000, Water=350.000, Salt=10.000, Rye starter=100.000, Rye flour=100.000, Sunflower seeds=0.050');
SELECT pg_temp.check_equal('US-07 variant of variant R3 removes inherited salt',
    (SELECT count(*) FROM fn_recipe_effective_ingredients(:r3) e JOIN ingredients i ON i.id = e.ingredient_id WHERE i.name = 'Salt'), 0::bigint);
SELECT pg_temp.check_equal('US-07 R3 keeps lines of R2 and R1', (SELECT count(*) FROM fn_recipe_effective_ingredients(:r3)), 5::bigint);
SELECT pg_temp.check_equal('US-07 R3 replaces bulk stage (240-300)',
    (SELECT min_duration_minutes || '-' || max_duration_minutes FROM fn_recipe_effective_stages(:r3) WHERE stage_type_code = 'bulk_fermentation'),
    '240-300');
SELECT pg_temp.check_equal('US-07 overrides: R2 loaf weight 920', (SELECT loaf_weight_g FROM fn_recipe_effective_header(:r2)), 920::numeric);
SELECT pg_temp.check_equal('US-07 R3 inherits R2 weight and overrides description',
    (SELECT loaf_weight_g || ' / ' || description FROM fn_recipe_effective_header(:r3)), '920 / Low-sodium version for a customer');
SELECT pg_temp.check_equal('US-08 view marks inherited lines',
    (SELECT count(*) FROM v_recipe_effective_ingredients WHERE recipe_id = :r2 AND is_inherited), 3::bigint);

-- US-06, US-18: scaling and starter amount
SELECT pg_temp.check_equal('US-06 flour for 3 loaves = 1500 g',
    (SELECT e.quantity * 3 / h.yield_loaves FROM fn_recipe_effective_ingredients(:r1) e, fn_recipe_effective_header(:r1) h
     WHERE e.ingredient_id = :flour), 1500::numeric);
SELECT pg_temp.check_equal('US-18 starter for 3 loaves = 300 g', fn_starter_amount_g(:r1, 3), 300::numeric);

-- US-16, US-52: timeline 14-16 h
SELECT pg_temp.check_equal('US-16 ready window from 12:00',
    (SELECT to_char(ready_from AT TIME ZONE 'UTC', 'HH24:MI') || '-' || to_char(ready_to AT TIME ZONE 'UTC', 'HH24:MI')
     FROM fn_recipe_ready_window(:r1, '2026-11-20 12:00+00')), '02:00-04:00');
SELECT pg_temp.check_equal('US-52 start window for ready at 08:00',
    (SELECT to_char(start_from AT TIME ZONE 'UTC', 'DD HH24:MI') || '-' || to_char(start_to AT TIME ZONE 'UTC', 'DD HH24:MI')
     FROM fn_recipe_start_window(:r1, '2026-11-20 08:00+00')), '19 16:00-19 18:00');
SELECT pg_temp.check_equal('timeline: 5 stages, last ends at +14h..+16h',
    (SELECT count(*) || ' / ' || to_char(max(planned_end_min_at) AT TIME ZONE 'UTC', 'HH24:MI') || '-' || to_char(max(planned_end_max_at) AT TIME ZONE 'UTC', 'HH24:MI')
     FROM fn_recipe_timeline(:r1, '2026-11-20 12:00+00')), '5 / 02:00-04:00');

-- US-17: kneading cycles; a temporary recipe with 7.2 kg of dough and Anna's 4 kg bowl
INSERT INTO recipes (id, owner_id, title, yield_loaves, is_archived)
VALUES ('00000000-0000-4000-2001-0000000000ff', :anna, 'Test 7.2 kg', 1, false);
INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, is_optional, sort_order, is_removed)
VALUES ('00000000-0000-4000-2001-0000000000ff', :flour, 7200, false, 1, false);
SELECT pg_temp.check_equal('US-17 7.2 kg dough, 4 kg bowl -> 2 cycles', fn_kneading_cycles('00000000-0000-4000-2001-0000000000ff', 1), 2);
SELECT pg_temp.check_equal('US-17 4 loaves of R1 (3.84 kg) -> 1 cycle', fn_kneading_cycles(:r1, 4), 1);
SELECT pg_temp.check_equal('US-17 no bowl configured -> NULL', fn_kneading_cycles(:r5, 1), NULL::integer);

-- US-14, US-47, US-48, US-49: cost of 3 loaves
SELECT pg_temp.check_equal('US-14 total = sum of the lines',
    (SELECT sum(amount) FILTER (WHERE line_type <> 'total') = max(amount) FILTER (WHERE line_type = 'total')
     FROM fn_recipe_cost(:r1, 3, '2026-11-15')), true);
SELECT pg_temp.check_equal('US-14 total for 3 loaves today = 11.22',
    (SELECT amount FROM fn_recipe_cost(:r1, 3, '2026-11-15') WHERE line_type = 'total'), 11.22::numeric);
SELECT pg_temp.check_equal('US-14 lines: 4 ingredients + electricity + packaging + labor + loss + total',
    (SELECT string_agg(line_type, ',' ORDER BY line_no) FROM fn_recipe_cost(:r1, 3, '2026-11-15')),
    'ingredient,ingredient,ingredient,ingredient,electricity,packaging,labor,loss,total');
SELECT pg_temp.check_equal('US-15 flour line as of 15 Sep = 1.80',
    (SELECT amount FROM fn_recipe_cost(:r1, 3, '2026-09-15') WHERE ingredient_id = :flour), 1.80::numeric);
SELECT pg_temp.check_equal('US-47 packaging 0.15 x 3 = 0.45',
    (SELECT amount FROM fn_recipe_cost(:r1, 3, '2026-11-15') WHERE line_type = 'packaging'), 0.45::numeric);
SELECT pg_temp.check_equal('US-48 loss 10 % of subtotal 10.20 = 1.02',
    (SELECT amount FROM fn_recipe_cost(:r1, 3, '2026-11-15') WHERE line_type = 'loss'), 1.02::numeric);
SELECT pg_temp.check_equal('US-49 labor 35 min x 12/h = 7.00',
    (SELECT amount FROM fn_recipe_cost(:r1, 3, '2026-11-15') WHERE line_type = 'labor'), 7.00::numeric);
SELECT pg_temp.check_equal('US-13 electricity 2.5 kW x 50 min x 0.25 = 0.52',
    (SELECT amount FROM fn_recipe_cost(:r1, 3, '2026-11-15') WHERE line_type = 'electricity'), 0.52::numeric);
SELECT pg_temp.check_equal('electricity: 5 loaves need 2 oven loads',
    (SELECT description FROM fn_recipe_cost(:r1, 5, '2026-11-15') WHERE line_type = 'electricity'), 'Electricity: 2 oven load(s)');
SELECT pg_temp.check_equal('optional seeds are not in the cost',
    (SELECT count(*) FROM fn_recipe_cost(:r2, 1, '2026-11-15') WHERE description = 'Sunflower seeds'), 0::bigint);
SELECT pg_temp.check_equal('UC-01 A4 missing price -> incomplete total',
    (SELECT is_complete FROM fn_recipe_cost(:r5, 1, '2026-11-15') WHERE line_type = 'total'), false);
SELECT pg_temp.check_equal('UC-01 A4 missing price line has NULL amount',
    (SELECT count(*) FROM fn_recipe_cost(:r5, 1, '2026-11-15') WHERE line_type = 'ingredient' AND amount IS NULL), 1::bigint);
SELECT pg_temp.check_equal('no oven settings -> electricity incomplete',
    (SELECT is_complete FROM fn_recipe_cost(:r5, 1, '2026-11-15') WHERE line_type = 'electricity'), false);

-- US-20, US-29: plan built by sp_build_batch_plan
SELECT pg_temp.check_equal('US-20 planned batch B1 has 5 stages and cost 11.22',
    (SELECT count(*) || ' / ' || max(b.total_cost) FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
     JOIN batches b ON b.id = bi.batch_id WHERE b.id = :b1), '5 / 11.22');
SELECT pg_temp.check_equal('US-17/18 plan stores kneads and starter',
    (SELECT kneading_cycles || ' / ' || starter_amount_g FROM batch_items WHERE batch_id = :b1), '1 / 300.0');
SELECT pg_temp.check_equal('US-29 reminder 60 min before kneading',
    (SELECT r.due_at = bs.planned_start_at - interval '60 minutes'
     FROM reminders r JOIN batch_items bi ON bi.id = r.batch_item_id
     JOIN batch_stages bs ON bs.batch_item_id = bi.id AND bs.stage_type_code = 'mix'
     WHERE bi.batch_id = :b1 AND r.kind = 'before_kneading'), true);

-- US-24: extending a stage shifts the following ones
SELECT pg_temp.check_equal('US-24 extended bulk stage of B2 (+30)',
    (SELECT extended_minutes FROM batch_stages WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 2), 30);
DO $$
DECLARE
    v_stage uuid;
    v_before timestamptz;
    v_ready_before timestamptz;
BEGIN
    SELECT bs.id INTO v_stage FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
    WHERE bi.batch_id = '00000000-0000-4000-6001-000000000001' AND bs.sort_order = 2;
    SELECT planned_start_at INTO v_before FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
    WHERE bi.batch_id = '00000000-0000-4000-6001-000000000001' AND bs.sort_order = 3;
    SELECT planned_ready_to INTO v_ready_before FROM batches WHERE id = '00000000-0000-4000-6001-000000000001';
    CALL sp_extend_batch_stage(v_stage, 30);
    PERFORM pg_temp.check_equal('US-24 next stage moved by 30 min',
        (SELECT planned_start_at - v_before FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
         WHERE bi.batch_id = '00000000-0000-4000-6001-000000000001' AND bs.sort_order = 3), interval '30 minutes');
    PERFORM pg_temp.check_equal('US-24 ready window moved by 30 min',
        (SELECT planned_ready_to - v_ready_before FROM batches WHERE id = '00000000-0000-4000-6001-000000000001'), interval '30 minutes');
END;
$$;

-- US-23: stage times drive statuses; the first started stage starts the batch
DO $$
BEGIN
    UPDATE batch_stages SET actual_start_at = now()
    WHERE batch_item_id = '00000000-0000-4000-b001-000000000001' AND sort_order = 1;
    PERFORM pg_temp.check_equal('US-23 start -> stage running',
        (SELECT status FROM batch_stages WHERE batch_item_id = '00000000-0000-4000-b001-000000000001' AND sort_order = 1)::text, 'running');
    PERFORM pg_temp.check_equal('US-23 first start -> batch in_progress',
        (SELECT status FROM batches WHERE id = '00000000-0000-4000-6001-000000000001')::text, 'in_progress');
    UPDATE batch_stages SET actual_end_at = now() + interval '20 minutes'
    WHERE batch_item_id = '00000000-0000-4000-b001-000000000001' AND sort_order = 1;
    PERFORM pg_temp.check_equal('US-23 complete -> stage done',
        (SELECT status FROM batch_stages WHERE batch_item_id = '00000000-0000-4000-b001-000000000001' AND sort_order = 1)::text, 'done');
END;
$$;

-- US-31: history record created by the trigger with the cost on the bake date
SELECT pg_temp.check_equal('US-31 history of B2: 5 loaves, 2 items',
    (SELECT r.total_loaves || ' / ' || count(i.id) FROM bake_records r JOIN bake_record_items i ON i.bake_record_id = r.id
     WHERE r.batch_id = :b2 GROUP BY r.total_loaves), '5 / 2');
SELECT pg_temp.check_equal('US-31 history cost uses prices of the bake date (21.40), plan used 15 Sep (21.01)',
    (SELECT r.total_cost || ' / ' || b.total_cost FROM bake_records r JOIN batches b ON b.id = r.batch_id WHERE b.id = :b2), '21.40 / 21.01');
SELECT pg_temp.check_equal('US-31 incomplete cost -> history total NULL',
    (SELECT total_cost FROM bake_records WHERE owner_id = :boris), NULL::numeric);
SELECT pg_temp.check_equal('reminders of finished and cancelled batches are cancelled',
    (SELECT count(*) FROM reminders WHERE kind = 'before_kneading' AND status = 'cancelled'), 2::bigint);
SELECT pg_temp.check_equal('KPI-1 B2 has a complete stage log',
    (SELECT is_complete FROM v_batch_log_completeness WHERE batch_id = :b2), true);
SELECT pg_temp.check_equal('US-36 deviation: Boris bulk 345 min vs max 300 -> +45',
    (SELECT deviation_minutes FROM v_batch_stage_log WHERE owner_id = :boris AND stage_type_code = 'bulk_fermentation'), 45);
SELECT pg_temp.check_equal('US-24 extended stage within its range -> 0',
    (SELECT deviation_minutes FROM v_batch_stage_log WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 2), 0);

-- US-28, US-30: feeding schedule
SELECT pg_temp.check_equal('US-30 fed 08:00, rise 12 h -> peak 20:00',
    (SELECT to_char(expected_peak_at AT TIME ZONE 'Europe/Vilnius', 'HH24:MI') FROM starter_feedings WHERE fed_at = '2026-11-09 08:00+02'), '20:00');
SELECT pg_temp.check_equal('US-28 next feeding = fed + 24 h',
    (SELECT next_feeding_at - fed_at FROM starter_feedings WHERE fed_at = '2026-11-09 08:00+02'), interval '24 hours');
SELECT pg_temp.check_equal('US-29 one scheduled feeding reminder per starter',
    (SELECT count(*) FROM reminders WHERE starter_id = '00000000-0000-4000-5001-000000000001' AND kind = 'starter_feeding' AND status = 'scheduled'), 1::bigint);

-- US-39: publication snapshot
SELECT pg_temp.check_equal('US-39 snapshot of R2 has 6 ingredients and 5 stages',
    (SELECT jsonb_array_length(snapshot -> 'ingredients') || ' / ' || jsonb_array_length(snapshot -> 'stages')
     FROM published_recipes WHERE source_recipe_id = :r2), '6 / 5');
UPDATE recipe_ingredients SET quantity = 999 WHERE id = '00000000-0000-4000-3001-000000000002';
SELECT pg_temp.check_equal('US-39 editing the working copy does not change the snapshot',
    (SELECT (snapshot -> 'ingredients' -> 1 ->> 'quantity')::numeric FROM published_recipes WHERE source_recipe_id = :r1), 350::numeric);
SELECT pg_temp.check_equal('US-40 public catalog shows only published recipes',
    (SELECT string_agg(title, ', ' ORDER BY title) FROM v_public_catalog), 'White homemade loaf, White loaf with rye');
SELECT pg_temp.check_equal('US-41 removed comments are not counted',
    (SELECT comment_count FROM v_public_catalog WHERE title = 'White loaf with rye'), 0::bigint);

-- Masking
SELECT pg_temp.check_equal('mask e-mail', fn_mask_email('anna.baker@example.com'), 'a***@e***.com');
SELECT pg_temp.check_equal('mask name', fn_mask_name('Anna Baker'), 'A*** B***');
SELECT pg_temp.check_equal('masked view has no clear e-mail',
    (SELECT count(*) FROM v_users_masked WHERE email_masked LIKE '%example.com' AND email_masked NOT LIKE '%***%'), 0::bigint);

-- Statistics
SELECT pg_temp.check_equal('statistics: White homemade loaf avg cost per loaf 3.74',
    (SELECT avg_cost_per_loaf FROM mv_bake_statistics WHERE recipe_title = 'White homemade loaf'), 3.74::numeric);

-- Data quality view finds the prepared problems
SELECT pg_temp.check_equal('data quality issue codes',
    (SELECT string_agg(DISTINCT issue_code, ',' ORDER BY issue_code) FROM v_data_quality_issues),
    'incomplete_stage_log,missing_price,overdue_planned_batch,price_currency_mismatch,recipe_without_ingredients,recipe_without_stages,starter_feeding_overdue');

-- Stock of ingredients: purchases add, batches and feedings take, a cancelled batch gives back.
-- The block removes what it wrote, so the checks after it see the unchanged test data.
\set salt    '''00000000-0000-4000-1001-000000000004'''
\set stockb  '''00000000-0000-4000-6001-0000000000f1'''

SELECT pg_temp.check_equal('stock of an ingredient without movements = 0', fn_stock_balance(:flour), 0::numeric);

INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, pack_count, pack_size, total_cost, currency)
VALUES (:flour, 'purchase', 2000, current_date, 2, 1000, 2.70, 'EUR');
UPDATE ingredients SET track_stock = true, reorder_level = 1500 WHERE id = :flour;
SELECT pg_temp.check_equal('purchase of 2 x 1000 g gives a stock of 2000 g', fn_stock_balance(:flour), 2000::numeric);

INSERT INTO batches (id, owner_id, name, status, source, planned_start_at, price_date, currency)
VALUES (:stockb, :anna, 'Stock test', 'planned', 'manual', now() + interval '1 day', current_date, 'EUR');
INSERT INTO batch_items (batch_id, recipe_id, loaves, sort_order) VALUES (:stockb, :r1, 3, 1);
CALL sp_build_batch_plan(:stockb);

SELECT pg_temp.check_equal('batch needs: 3 loaves of R1 need 1500 g of flour',
    (SELECT quantity FROM fn_batch_ingredient_needs(:stockb) WHERE ingredient_id = :flour), 1500::numeric);
SELECT pg_temp.check_equal('stock check lists only ingredients with kept stock',
    (SELECT string_agg(name, ', ') FROM fn_batch_stock_check(:stockb)), 'Bread flour');

CALL sp_apply_batch_stock(:stockb);
SELECT pg_temp.check_equal('batch takes its flour from the stock', fn_stock_balance(:flour), 500::numeric);
SELECT pg_temp.check_equal('salt without kept stock is not consumed',
    (SELECT count(*) FROM stock_movements WHERE ingredient_id = :salt), 0::bigint);
SELECT pg_temp.check_equal('below the reorder level the ingredient is in v_low_stock',
    (SELECT stock FROM v_low_stock WHERE ingredient_id = :flour), 500::numeric);

UPDATE batch_items SET loaves = 2 WHERE batch_id = :stockb;
CALL sp_build_batch_plan(:stockb);
CALL sp_apply_batch_stock(:stockb);
SELECT pg_temp.check_equal('re-planned batch changes the stock by the difference', fn_stock_balance(:flour), 1000::numeric);

UPDATE batches SET ingredient_scale = 0.5 WHERE id = :stockb;
CALL sp_build_batch_plan(:stockb);
CALL sp_apply_batch_stock(:stockb);
SELECT pg_temp.check_equal('half of the dough takes half of the flour', fn_stock_balance(:flour), 1500::numeric);
SELECT pg_temp.check_equal('half of the dough needs half of the starter',
    (SELECT starter_amount_g FROM batch_items WHERE batch_id = :stockb), 100::numeric);

CALL sp_return_batch_stock(:stockb);
SELECT pg_temp.check_equal('cancelled batch returns what it took', fn_stock_balance(:flour), 2000::numeric);
CALL sp_return_batch_stock(:stockb);
SELECT pg_temp.check_equal('nothing is returned twice', fn_stock_balance(:flour), 2000::numeric);

DELETE FROM batches WHERE id = :stockb;
SELECT pg_temp.check_equal('ledger rows stay after the batch is deleted',
    (SELECT count(*) FROM stock_movements WHERE ingredient_id = :flour AND batch_id IS NULL AND movement_type LIKE 'batch%'), 2::bigint);

UPDATE starters SET flour_ingredient_id = :flour WHERE owner_id = :anna;
INSERT INTO starter_feedings (starter_id, fed_at, flour_g, water_g, starter_kept_g)
SELECT id, now(), 50, 50, 20 FROM starters WHERE owner_id = :anna ORDER BY name LIMIT 1;
SELECT pg_temp.check_equal('feeding takes its flour from the linked ingredient', fn_stock_balance(:flour), 1950::numeric);

-- A starter is an ingredient of its own; two thirds of the fed starter reach the stock when it is ripe.
\set mother '''00000000-0000-4000-5001-000000000001'''
SELECT pg_temp.check_equal('a starter has an ingredient of the category starter with its name',
    (SELECT i.category_code || ':' || i.name FROM starters s JOIN ingredients i ON i.id = s.ingredient_id WHERE s.id = :mother), 'starter:Rye mother');
INSERT INTO starter_feedings (starter_id, fed_at, flour_g, water_g, starter_kept_g) VALUES (:mother, now() + interval '1 minute', 50, 50, 20);
SELECT pg_temp.check_equal('a fed starter is not stock before it is ripe',
    (SELECT fn_stock_balance(ingredient_id) FROM starters WHERE id = :mother), 0::numeric);
SELECT pg_temp.check_equal('the usable share is stock after the peak',
    (SELECT fn_stock_balance_at(ingredient_id, now() + interval '13 hours') FROM starters WHERE id = :mother), 80.04::numeric);
INSERT INTO starter_feedings (starter_id, fed_at, flour_g, water_g, starter_kept_g) VALUES (:mother, now() + interval '14 hours', 30, 30, 60);
SELECT pg_temp.check_equal('the next feeding returns the ripe starter to the jar and ripens anew',
    (SELECT string_agg(movement_type || ' ' || trim_scale(quantity), ', ' ORDER BY created_at, quantity)
     FROM stock_movements WHERE ingredient_id = (SELECT ingredient_id FROM starters WHERE id = :mother)),
    'starter_refed -80.04, starter_ripened 80.04, starter_ripened 80.04');
SELECT pg_temp.check_equal('a feeding uses the usual flour of its starter unless another is named',
    (SELECT count(*) FROM starter_feedings WHERE starter_id = :mother AND flour_ingredient_id = :flour), 3::bigint);
SELECT pg_temp.check_equal('a starter costs what it was fed: equal parts of flour and free water are half the flour price',
    (SELECT round(sp.price / sp.per_quantity / (fp.price / fp.per_quantity / u.factor_to_base), 3)
     FROM starters s
     CROSS JOIN LATERAL fn_price_on_date(s.ingredient_id, (now() + interval '14 hours')::date) sp
     CROSS JOIN LATERAL fn_price_on_date(:flour, current_date) fp
     JOIN ingredients i ON i.id = :flour JOIN units u ON u.code = i.unit_code
     WHERE s.id = :mother), 0.5::numeric);
UPDATE starters SET name = 'Rye mother renamed' WHERE id = :mother;
SELECT pg_temp.check_equal('renaming a starter renames its ingredient',
    (SELECT i.name FROM starters s JOIN ingredients i ON i.id = s.ingredient_id WHERE s.id = :mother), 'Rye mother renamed');
UPDATE starters SET name = 'Rye mother' WHERE id = :mother;

DELETE FROM starter_feedings WHERE id IN (SELECT feeding_id FROM stock_movements WHERE feeding_id IS NOT NULL);
DELETE FROM ingredient_prices WHERE ingredient_id IN (SELECT ingredient_id FROM starters WHERE owner_id = :anna);
DELETE FROM stock_movements WHERE ingredient_id = :flour OR ingredient_id IN (SELECT ingredient_id FROM starters WHERE owner_id = :anna);
UPDATE starters SET flour_ingredient_id = NULL WHERE owner_id = :anna;
UPDATE ingredients SET track_stock = false, reorder_level = NULL WHERE id = :flour;

-- Anonymization (GDPR): personal data and workspace removed, public contributions kept
CALL sp_anonymize_user(:anna);
SELECT pg_temp.check_equal('anonymize: e-mail replaced',
    (SELECT email LIKE 'anonymized-%@anonymized.invalid' AND display_name = 'Anonymous baker' FROM users WHERE id = :anna), true);
SELECT pg_temp.check_equal('anonymize: new password hash is strong and different',
    (SELECT fn_is_strong_password_hash(password_hash) AND password_hash NOT LIKE 'AQAAAAIAAYagAAAAEGbt%' FROM users WHERE id = :anna), true);
SELECT pg_temp.check_equal('anonymize: workspace removed',
    (SELECT count(*) FROM recipes WHERE owner_id = :anna) + (SELECT count(*) FROM batches WHERE owner_id = :anna)
  + (SELECT count(*) FROM chat_conversations WHERE owner_id = :anna), 0::bigint);
SELECT pg_temp.check_equal('anonymize: publications stay, author shown as Anonymous baker',
    (SELECT count(*) FROM v_public_catalog WHERE author_name = 'Anonymous baker'), 2::bigint);
SET CONSTRAINTS ALL IMMEDIATE;  -- deferred keys are consistent after the anonymization

-- Report
SELECT no, name, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS result, detail FROM test_results ORDER BY no;
SELECT count(*) FILTER (WHERE passed) AS passed, count(*) AS total FROM test_results;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM test_results WHERE NOT passed) THEN
        RAISE EXCEPTION 'Business tests failed: %', (SELECT string_agg(name, '; ') FROM test_results WHERE NOT passed);
    END IF;
END;
$$;

ROLLBACK;
