-- OneLoaf test data.
-- Run after the migrations, as the APPLICATION user (oneloaf_app), to show that the application role is enough:
--   psql -h <host> -U oneloaf_app -d oneloaf -v ON_ERROR_STOP=1 -f db/seed/test_data.sql
-- The script is one transaction: either everything is loaded or nothing.
--
-- Users (passwords are valid PBKDF2 hashes of random passwords that are not stored anywhere):
--   Anna   (a1) experienced home baker preparing to sell: full kitchen settings, recipe with two levels of variants,
--               batches in every status, starter with a schedule, published recipe, chat with the assistant
--   Boris  (a2) beginner: no oven settings, ingredient without a price, empty recipe, overdue starter,
--               finished batch with an incomplete stage log
--   Clara  (a3) home-bakery owner: labor rate, price in another currency, overdue planned batch, unpublished recipe
--   Dmitry (a4) library visitor: comments and reactions only
--   Admin  (a5) administrator: resolves content reports
--
-- Edge cases covered (checked by db/tests/business_tests.sql and visible in v_data_quality_issues):
--   price history with a change on 1 October (cost as of 15 September vs today), zero price (water),
--   price per kilogram for an ingredient in kg, optional ingredient, cold proof, variant of a variant that removes
--   an inherited line, archived recipe, recipe without ingredients/stages, missing price -> incomplete cost,
--   price in USD while the kitchen uses EUR, no oven settings -> incomplete electricity, extended stage (+30 min),
--   batch across midnight, cancelled batch, overdue planned batch, finished batch with incomplete stage log,
--   starter never fed, publication unpublished and removed by a moderator, open and resolved reports.

BEGIN;

------------------------------------------------------------------------------------------------
-- Users and kitchen settings
------------------------------------------------------------------------------------------------
INSERT INTO users (id, email, normalized_email, password_hash, display_name, role) VALUES
 ('00000000-0000-4000-a001-0000000000a1', 'anna.baker@example.com',     'ANNA.BAKER@EXAMPLE.COM',
  'AQAAAAIAAYagAAAAEGbtM3QoMFczM8gGj8Q4FiuhXaveCAu1smLsKub4cdR+CumKloilcuZmdMPsf734AA==', 'Anna Baker', 'user'),
 ('00000000-0000-4000-a001-0000000000a2', 'boris.beginner@example.com', 'BORIS.BEGINNER@EXAMPLE.COM',
  'AQAAAAIAAYagAAAAEGdRJ/fg0BdTU35V9OhqSUxYF3qrhPha/fuqPQ/y3C+4YpT+3QHZ7dN1Ngk7N7oWGA==', 'Boris Novak', 'user'),
 ('00000000-0000-4000-a001-0000000000a3', 'clara.bakery@example.com',   'CLARA.BAKERY@EXAMPLE.COM',
  'AQAAAAIAAYagAAAAEKR4NXC9yUaPvZyiRNc17SISj/w4UGS2uMKejnSZU9cSYXEQw+DK9iIQ7P7VtxWdJQ==', 'Clara Home Bakery', 'user'),
 ('00000000-0000-4000-a001-0000000000a4', 'dmitry.reader@example.com',  'DMITRY.READER@EXAMPLE.COM',
  'AQAAAAIAAYagAAAAEKNOTnfyq6U7tO1QnZ1/QgJzTQ5Iq+p45uuOsNNr7MCPomBzdeA1Q+deM+16afaGqQ==', 'Dmitry', 'user'),
 ('00000000-0000-4000-a001-0000000000a5', 'admin@oneloaf.example',      'ADMIN@ONELOAF.EXAMPLE',
  'AQAAAAIAAYagAAAAEC9zX21J0ktzIC1rN5akAPI2JKTyCE0xIo8cyR6mqJkNg0tcUhB1/LkRsss05UZ68g==', 'OneLoaf Admin', 'admin');

-- Anna: 2.5 kW oven for 4 loaves, 4 kg bowl, tariff 0.25, packaging 0.15, loss 10 %, labor 12/h (US-13, US-47-49)
-- Boris: no oven data (electricity cannot be calculated); Clara: big oven, labor rate, loss 5 %
INSERT INTO kitchen_settings (user_id, oven_power_kw, oven_capacity_loaves, mixer_bowl_capacity_kg, electricity_tariff_per_kwh,
                              packaging_cost_per_loaf, loss_rate_percent, labor_rate_per_hour, currency, time_zone) VALUES
 ('00000000-0000-4000-a001-0000000000a1', 2.50, 4,  4.00, 0.2500, 0.15, 10.00, 12.00, 'EUR', 'Europe/Vilnius'),
 ('00000000-0000-4000-a001-0000000000a2', NULL, NULL, NULL, NULL,   0.00,  0.00, NULL,  'EUR', 'Europe/Vilnius'),
 ('00000000-0000-4000-a001-0000000000a3', 6.00, 12, 20.00, 0.2200, 0.25,  5.00, 15.00, 'EUR', 'Europe/Vilnius');

------------------------------------------------------------------------------------------------
-- Catalog: flours, ingredients, price history
------------------------------------------------------------------------------------------------
INSERT INTO flours (id, owner_id, name, flour_type_code, protein_percent, grind, notes) VALUES
 ('00000000-0000-4000-f001-000000000001', '00000000-0000-4000-a001-0000000000a1', 'Bread flour T550', 'wheat', 12.5, 'fine', NULL),
 ('00000000-0000-4000-f001-000000000002', '00000000-0000-4000-a001-0000000000a1', 'Rye flour T1150',  'rye',    9.0, 'medium', 'For the rye starter'),
 ('00000000-0000-4000-f001-000000000003', '00000000-0000-4000-a001-0000000000a2', 'All-purpose flour', 'wheat', 10.5, 'fine', NULL),
 ('00000000-0000-4000-f001-000000000004', '00000000-0000-4000-a001-0000000000a3', 'Spelt flour',       'spelt', 14.0, 'fine', NULL);

INSERT INTO ingredients (id, owner_id, name, category_code, unit_code, flour_id) VALUES
 -- Anna
 ('00000000-0000-4000-1001-000000000001', '00000000-0000-4000-a001-0000000000a1', 'Bread flour',   'flour',   'g',  '00000000-0000-4000-f001-000000000001'),
 ('00000000-0000-4000-1001-000000000002', '00000000-0000-4000-a001-0000000000a1', 'Rye flour',     'flour',   'g',  '00000000-0000-4000-f001-000000000002'),
 ('00000000-0000-4000-1001-000000000003', '00000000-0000-4000-a001-0000000000a1', 'Water',         'water',   'ml', NULL),
 ('00000000-0000-4000-1001-000000000004', '00000000-0000-4000-a001-0000000000a1', 'Salt',          'salt',    'g',  NULL),
 ('00000000-0000-4000-1001-000000000005', '00000000-0000-4000-a001-0000000000a1', 'Rye starter',   'starter', 'g',  NULL),
 ('00000000-0000-4000-1001-000000000006', '00000000-0000-4000-a001-0000000000a1', 'Sunflower seeds','add_in', 'kg', NULL),
 -- Boris (salt has no price: cost is incomplete)
 ('00000000-0000-4000-1001-000000000011', '00000000-0000-4000-a001-0000000000a2', 'Flour',         'flour',   'g',  '00000000-0000-4000-f001-000000000003'),
 ('00000000-0000-4000-1001-000000000012', '00000000-0000-4000-a001-0000000000a2', 'Water',         'water',   'ml', NULL),
 ('00000000-0000-4000-1001-000000000013', '00000000-0000-4000-a001-0000000000a2', 'Salt',          'salt',    'g',  NULL),
 ('00000000-0000-4000-1001-000000000014', '00000000-0000-4000-a001-0000000000a2', 'Starter',       'starter', 'g',  NULL),
 -- Clara (spelt priced in USD although the kitchen uses EUR)
 ('00000000-0000-4000-1001-000000000021', '00000000-0000-4000-a001-0000000000a3', 'Spelt flour',   'flour',   'g',  '00000000-0000-4000-f001-000000000004'),
 ('00000000-0000-4000-1001-000000000022', '00000000-0000-4000-a001-0000000000a3', 'Milk',          'dairy',   'ml', NULL),
 ('00000000-0000-4000-1001-000000000023', '00000000-0000-4000-a001-0000000000a3', 'Butter',        'fat',     'g',  NULL),
 ('00000000-0000-4000-1001-000000000024', '00000000-0000-4000-a001-0000000000a3', 'Eggs',          'other',   'pcs',NULL),
 ('00000000-0000-4000-1001-000000000025', '00000000-0000-4000-a001-0000000000a3', 'Sugar',         'sweetener','g', NULL);

-- US-12 / US-15: bread flour 1.20 per kg, changed to 1.35 on 1 October; the old price is kept
INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from) VALUES
 ('00000000-0000-4000-1001-000000000001', 1.10, 1000, 'EUR', '2026-03-01'),
 ('00000000-0000-4000-1001-000000000001', 1.20, 1000, 'EUR', '2026-06-01'),
 ('00000000-0000-4000-1001-000000000001', 1.35, 1000, 'EUR', '2026-10-01'),
 ('00000000-0000-4000-1001-000000000002', 1.10, 1000, 'EUR', '2026-06-01'),
 ('00000000-0000-4000-1001-000000000003', 0.00, 1000, 'EUR', '2026-06-01'),
 ('00000000-0000-4000-1001-000000000004', 0.50, 1000, 'EUR', '2026-06-01'),
 ('00000000-0000-4000-1001-000000000005', 0.60, 1000, 'EUR', '2026-06-01'),
 ('00000000-0000-4000-1001-000000000006', 4.50,    1, 'EUR', '2026-06-01'),
 ('00000000-0000-4000-1001-000000000011', 0.90, 1000, 'EUR', '2026-09-01'),
 ('00000000-0000-4000-1001-000000000012', 0.00, 1000, 'EUR', '2026-09-01'),
 ('00000000-0000-4000-1001-000000000014', 0.50, 1000, 'EUR', '2026-09-01'),
 ('00000000-0000-4000-1001-000000000021', 2.40, 1000, 'USD', '2026-09-01'),
 ('00000000-0000-4000-1001-000000000022', 1.10, 1000, 'EUR', '2026-09-01'),
 ('00000000-0000-4000-1001-000000000023', 9.80, 1000, 'EUR', '2026-09-01'),
 ('00000000-0000-4000-1001-000000000024', 0.30,    1, 'EUR', '2026-09-01'),
 ('00000000-0000-4000-1001-000000000025', 1.00, 1000, 'EUR', '2026-09-01');

------------------------------------------------------------------------------------------------
-- Recipes
------------------------------------------------------------------------------------------------
-- R1 White homemade loaf: 1 loaf, 960 g of dough, stages 14-16 h in total (US-05, US-06, US-16)
INSERT INTO recipes (id, owner_id, title, description, yield_loaves, loaf_weight_g, notes, is_archived) VALUES
 ('00000000-0000-4000-2001-000000000001', '00000000-0000-4000-a001-0000000000a1', 'White homemade loaf',
  'Basic white sourdough loaf with an overnight cold proof', 1, 900, 'Bake in a Dutch oven', false);

INSERT INTO recipe_ingredients (id, recipe_id, ingredient_id, quantity, is_optional, sort_order, note, is_removed) VALUES
 ('00000000-0000-4000-3001-000000000001', '00000000-0000-4000-2001-000000000001', '00000000-0000-4000-1001-000000000001', 500, false, 1, NULL, false),
 ('00000000-0000-4000-3001-000000000002', '00000000-0000-4000-2001-000000000001', '00000000-0000-4000-1001-000000000003', 350, false, 2, 'Water at 28 C', false),
 ('00000000-0000-4000-3001-000000000003', '00000000-0000-4000-2001-000000000001', '00000000-0000-4000-1001-000000000004',  10, false, 3, NULL, false),
 ('00000000-0000-4000-3001-000000000004', '00000000-0000-4000-2001-000000000001', '00000000-0000-4000-1001-000000000005', 100, false, 4, 'Active, 4-6 h after feeding', false);

INSERT INTO recipe_stages (id, recipe_id, sort_order, name, stage_type_code, min_duration_minutes, max_duration_minutes,
                           active_minutes, proof_mode, temperature_c, uses_oven, uses_mixer, is_optional, instructions, is_removed) VALUES
 ('00000000-0000-4000-4001-000000000001', '00000000-0000-4000-2001-000000000001', 1, 'Mix',               'mix',                15,  20, 15, NULL,   NULL, false, true,  false, 'Mix until the dough is smooth', false),
 ('00000000-0000-4000-4001-000000000002', '00000000-0000-4000-2001-000000000001', 2, 'Bulk fermentation', 'bulk_fermentation', 180, 240, 10, NULL,   24,   false, false, false, 'Two sets of folds', false),
 ('00000000-0000-4000-4001-000000000003', '00000000-0000-4000-2001-000000000001', 3, 'Shape',             'shape',               5,  10,  5, NULL,   NULL, false, false, false, NULL, false),
 ('00000000-0000-4000-4001-000000000004', '00000000-0000-4000-2001-000000000001', 4, 'Cold proof',        'proof',             600, 640,  0, 'cold', 4,    false, false, false, 'In the fridge', false),
 ('00000000-0000-4000-4001-000000000005', '00000000-0000-4000-2001-000000000001', 5, 'Bake',              'bake',               40,  50,  5, NULL,   240,  true,  false, false, '20 min lid on, then lid off', false);

-- R2 variant: 400 g bread flour instead of 500, plus 100 g rye flour and optional seeds; heavier loaf (US-07, US-09)
INSERT INTO recipes (id, owner_id, parent_recipe_id, title, yield_loaves, variant_overrides, is_archived) VALUES
 ('00000000-0000-4000-2001-000000000002', '00000000-0000-4000-a001-0000000000a1', '00000000-0000-4000-2001-000000000001',
  'White loaf with rye', 1, '{"loafWeightG": 920, "notes": "Rye makes the crumb denser"}', false);

INSERT INTO recipe_ingredients (id, recipe_id, ingredient_id, quantity, is_optional, sort_order, replaces_item_id, is_removed) VALUES
 ('00000000-0000-4000-3001-000000000011', '00000000-0000-4000-2001-000000000002', '00000000-0000-4000-1001-000000000001', 400,   false, 1, '00000000-0000-4000-3001-000000000001', false),
 ('00000000-0000-4000-3001-000000000012', '00000000-0000-4000-2001-000000000002', '00000000-0000-4000-1001-000000000002', 100,   false, 5, NULL, false),
 ('00000000-0000-4000-3001-000000000013', '00000000-0000-4000-2001-000000000002', '00000000-0000-4000-1001-000000000006', 0.050, true,  6, NULL, false);

-- R3 variant of the variant: removes the inherited salt line and makes the bulk fermentation longer
INSERT INTO recipes (id, owner_id, parent_recipe_id, title, yield_loaves, variant_overrides, is_archived) VALUES
 ('00000000-0000-4000-2001-000000000003', '00000000-0000-4000-a001-0000000000a1', '00000000-0000-4000-2001-000000000002',
  'Salt-free rye loaf, long bulk', 1, '{"description": "Low-sodium version for a customer"}', false);

INSERT INTO recipe_ingredients (id, recipe_id, ingredient_id, quantity, is_optional, sort_order, replaces_item_id, is_removed) VALUES
 ('00000000-0000-4000-3001-000000000021', '00000000-0000-4000-2001-000000000003', '00000000-0000-4000-1001-000000000004', 0, false, 3, '00000000-0000-4000-3001-000000000003', true);

INSERT INTO recipe_stages (id, recipe_id, sort_order, name, stage_type_code, min_duration_minutes, max_duration_minutes,
                           active_minutes, proof_mode, temperature_c, uses_oven, uses_mixer, is_optional, replaces_stage_id, is_removed) VALUES
 ('00000000-0000-4000-4001-000000000021', '00000000-0000-4000-2001-000000000003', 2, 'Long bulk fermentation', 'bulk_fermentation', 240, 300, 10, NULL, 22, false, false, false,
  '00000000-0000-4000-4001-000000000002', false);

-- R4 archived recipe (kept for history, hidden from lists and quality checks)
INSERT INTO recipes (id, owner_id, title, yield_loaves, loaf_weight_g, is_archived) VALUES
 ('00000000-0000-4000-2001-000000000004', '00000000-0000-4000-a001-0000000000a1', 'Old focaccia', 2, 600, true);

-- R5 Boris: beginner loaf, salt without a price
INSERT INTO recipes (id, owner_id, title, yield_loaves, loaf_weight_g, is_archived) VALUES
 ('00000000-0000-4000-2001-000000000005', '00000000-0000-4000-a001-0000000000a2', 'My first sourdough', 1, 800, false),
 ('00000000-0000-4000-2001-000000000006', '00000000-0000-4000-a001-0000000000a2', 'Draft: rye bread', 1, NULL, false);

INSERT INTO recipe_ingredients (id, recipe_id, ingredient_id, quantity, is_optional, sort_order, is_removed) VALUES
 ('00000000-0000-4000-3001-000000000051', '00000000-0000-4000-2001-000000000005', '00000000-0000-4000-1001-000000000011', 450, false, 1, false),
 ('00000000-0000-4000-3001-000000000052', '00000000-0000-4000-2001-000000000005', '00000000-0000-4000-1001-000000000012', 330, false, 2, false),
 ('00000000-0000-4000-3001-000000000053', '00000000-0000-4000-2001-000000000005', '00000000-0000-4000-1001-000000000013',   9, false, 3, false),
 ('00000000-0000-4000-3001-000000000054', '00000000-0000-4000-2001-000000000005', '00000000-0000-4000-1001-000000000014',  90, false, 4, false);

INSERT INTO recipe_stages (id, recipe_id, sort_order, name, stage_type_code, min_duration_minutes, max_duration_minutes,
                           active_minutes, proof_mode, temperature_c, uses_oven, uses_mixer, is_optional, is_removed) VALUES
 ('00000000-0000-4000-4001-000000000051', '00000000-0000-4000-2001-000000000005', 1, 'Mix by hand',  'mix',               20,  30, 20, NULL,   NULL, false, false, false, false),
 ('00000000-0000-4000-4001-000000000052', '00000000-0000-4000-2001-000000000005', 2, 'Bulk',         'bulk_fermentation', 240, 300, 15, NULL,   23,   false, false, false, false),
 ('00000000-0000-4000-4001-000000000053', '00000000-0000-4000-2001-000000000005', 3, 'Warm proof',   'proof',             60,  90,  0, 'warm', 26,   false, false, false, false),
 ('00000000-0000-4000-4001-000000000054', '00000000-0000-4000-2001-000000000005', 4, 'Bake',         'bake',              45,  50,  5, NULL,   230,  true,  false, false, false);

-- R7 Clara: brioche with pieces (eggs) and an optional egg wash stage
INSERT INTO recipes (id, owner_id, title, description, yield_loaves, loaf_weight_g, is_archived) VALUES
 ('00000000-0000-4000-2001-000000000007', '00000000-0000-4000-a001-0000000000a3', 'Spelt brioche', 'Enriched dough for markets', 4, 500, false);

INSERT INTO recipe_ingredients (id, recipe_id, ingredient_id, quantity, is_optional, sort_order, is_removed) VALUES
 ('00000000-0000-4000-3001-000000000071', '00000000-0000-4000-2001-000000000007', '00000000-0000-4000-1001-000000000021', 1000, false, 1, false),
 ('00000000-0000-4000-3001-000000000072', '00000000-0000-4000-2001-000000000007', '00000000-0000-4000-1001-000000000022',  300, false, 2, false),
 ('00000000-0000-4000-3001-000000000073', '00000000-0000-4000-2001-000000000007', '00000000-0000-4000-1001-000000000023',  250, false, 3, false),
 ('00000000-0000-4000-3001-000000000074', '00000000-0000-4000-2001-000000000007', '00000000-0000-4000-1001-000000000024',    6, false, 4, false),
 ('00000000-0000-4000-3001-000000000075', '00000000-0000-4000-2001-000000000007', '00000000-0000-4000-1001-000000000025',  120, false, 5, false);

INSERT INTO recipe_stages (id, recipe_id, sort_order, name, stage_type_code, min_duration_minutes, max_duration_minutes,
                           active_minutes, proof_mode, temperature_c, uses_oven, uses_mixer, is_optional, is_removed) VALUES
 ('00000000-0000-4000-4001-000000000071', '00000000-0000-4000-2001-000000000007', 1, 'Mix',        'mix',               20,  25, 20, NULL,   NULL, false, true,  false, false),
 ('00000000-0000-4000-4001-000000000072', '00000000-0000-4000-2001-000000000007', 2, 'Proof',      'proof',            120, 180,  0, 'warm', 27,   false, false, false, false),
 ('00000000-0000-4000-4001-000000000073', '00000000-0000-4000-2001-000000000007', 3, 'Egg wash',   'other',              5,  10,  5, NULL,   NULL, false, false, true,  false),
 ('00000000-0000-4000-4001-000000000074', '00000000-0000-4000-2001-000000000007', 4, 'Bake',       'bake',              30,  35,  5, NULL,   180,  true,  false, false, false);

------------------------------------------------------------------------------------------------
-- Starters and feedings (the trigger computes the next feeding and schedules the reminder)
------------------------------------------------------------------------------------------------
INSERT INTO starters (id, owner_id, name, flour_id, hydration_percent, feeding_mode, feeding_interval_hours, rise_hours, is_active, notes) VALUES
 ('00000000-0000-4000-5001-000000000001', '00000000-0000-4000-a001-0000000000a1', 'Rye mother',   '00000000-0000-4000-f001-000000000002', 100, 'regular', 24,  12, true,  NULL),
 ('00000000-0000-4000-5001-000000000002', '00000000-0000-4000-a001-0000000000a1', 'Wheat levain', '00000000-0000-4000-f001-000000000001',  80, 'fridge',  168, 10, false, 'Paused for the summer'),
 ('00000000-0000-4000-5001-000000000003', '00000000-0000-4000-a001-0000000000a2', 'Bubbles',      '00000000-0000-4000-f001-000000000003', 100, 'regular', 24,   8, true,  NULL),
 ('00000000-0000-4000-5001-000000000004', '00000000-0000-4000-a001-0000000000a3', 'Main levain',  '00000000-0000-4000-f001-000000000004', 100, 'regular', 12,   6, true,  NULL),
 ('00000000-0000-4000-5001-000000000005', '00000000-0000-4000-a001-0000000000a3', 'New rye starter', NULL,                               120, 'regular', 24, NULL, true, 'Created, not fed yet');

-- Anna: US-30 example (fed 08:00, rise 12 h -> peak 20:00) and a recent feeding that schedules the next reminder
INSERT INTO starter_feedings (starter_id, fed_at, flour_g, water_g, starter_kept_g, temperature_c, note) VALUES
 ('00000000-0000-4000-5001-000000000001', '2026-11-09 08:00+02', 50, 50, 50, 23, NULL),
 ('00000000-0000-4000-5001-000000000001', now() - interval '2 hours', 50, 50, 50, 22, 'Before the white loaves'),
-- Boris: last feeding four days ago -> overdue (two intervals = 48 h)
 ('00000000-0000-4000-5001-000000000003', now() - interval '4 days', 40, 40, 20, 21, NULL),
-- Clara: fed this morning
 ('00000000-0000-4000-5001-000000000004', now() - interval '3 hours', 200, 200, 100, 24, NULL);

------------------------------------------------------------------------------------------------
-- Batches. Each batch is created as planned and planned by sp_build_batch_plan (timeline, kneads,
-- starter amount, reminders, cost); progress is recorded through stage times like the application does.
------------------------------------------------------------------------------------------------
INSERT INTO batches (id, owner_id, name, status, source, planned_start_at, price_date, currency, notes) VALUES
 -- B1 Anna: 3 white loaves tomorrow at 12:00 (US-34 example); stays planned
 ('00000000-0000-4000-6001-000000000001', '00000000-0000-4000-a001-0000000000a1', '3 white loaves for tomorrow', 'planned', 'assistant',
  date_trunc('day', now()) + interval '1 day 12 hours', current_date, 'EUR', NULL),
 -- B2 Anna: market batch 10-11 November, two recipes, prices as of 15 September; finished
 ('00000000-0000-4000-6001-000000000002', '00000000-0000-4000-a001-0000000000a1', 'Saturday market', 'planned', 'manual',
  '2026-11-10 12:00+02', '2026-09-15', 'EUR', 'Overnight cold proof'),
 -- B3 Anna: cancelled
 ('00000000-0000-4000-6001-000000000003', '00000000-0000-4000-a001-0000000000a1', 'Order cancelled by customer', 'planned', 'manual',
  date_trunc('day', now()) + interval '3 days 9 hours', current_date, 'EUR', NULL),
 -- B4 Anna: running now
 ('00000000-0000-4000-6001-000000000004', '00000000-0000-4000-a001-0000000000a1', 'Rye loaves in progress', 'planned', 'manual',
  now() - interval '3 hours', current_date, 'EUR', NULL),
 -- B5 Boris: finished, but only two stages were logged
 ('00000000-0000-4000-6001-000000000005', '00000000-0000-4000-a001-0000000000a2', 'Weekend loaf', 'planned', 'manual',
  '2026-11-08 10:00+02', '2026-11-08', 'EUR', NULL),
 -- B6 Clara: planned three days ago, never started (overdue)
 ('00000000-0000-4000-6001-000000000006', '00000000-0000-4000-a001-0000000000a3', 'Brioche for the cafe', 'planned', 'manual',
  now() - interval '3 days', current_date, 'EUR', NULL);

INSERT INTO batch_items (id, batch_id, recipe_id, starter_id, loaves, starter_reminder_minutes, sort_order) VALUES
 ('00000000-0000-4000-b001-000000000001', '00000000-0000-4000-6001-000000000001', '00000000-0000-4000-2001-000000000001', '00000000-0000-4000-5001-000000000001', 3, 60,   1),
 ('00000000-0000-4000-b001-000000000002', '00000000-0000-4000-6001-000000000002', '00000000-0000-4000-2001-000000000001', '00000000-0000-4000-5001-000000000001', 3, 60,   1),
 ('00000000-0000-4000-b001-000000000003', '00000000-0000-4000-6001-000000000002', '00000000-0000-4000-2001-000000000002', '00000000-0000-4000-5001-000000000001', 2, NULL, 2),
 ('00000000-0000-4000-b001-000000000004', '00000000-0000-4000-6001-000000000003', '00000000-0000-4000-2001-000000000001', NULL,                                   6, NULL, 1),
 ('00000000-0000-4000-b001-000000000005', '00000000-0000-4000-6001-000000000004', '00000000-0000-4000-2001-000000000003', '00000000-0000-4000-5001-000000000001', 2, NULL, 1),
 ('00000000-0000-4000-b001-000000000006', '00000000-0000-4000-6001-000000000005', '00000000-0000-4000-2001-000000000005', '00000000-0000-4000-5001-000000000003', 1, 30,   1),
 ('00000000-0000-4000-b001-000000000007', '00000000-0000-4000-6001-000000000006', '00000000-0000-4000-2001-000000000007', '00000000-0000-4000-5001-000000000004', 8, NULL, 1);

CALL sp_build_batch_plan('00000000-0000-4000-6001-000000000001');
CALL sp_build_batch_plan('00000000-0000-4000-6001-000000000002');
CALL sp_build_batch_plan('00000000-0000-4000-6001-000000000003');
CALL sp_build_batch_plan('00000000-0000-4000-6001-000000000004');
CALL sp_build_batch_plan('00000000-0000-4000-6001-000000000005');
CALL sp_build_batch_plan('00000000-0000-4000-6001-000000000006');

-- B2: full stage log of both items, bulk fermentation of item 1 extended by 30 min (US-23, US-24), across midnight
UPDATE batch_stages SET actual_start_at = '2026-11-10 12:05+02', actual_end_at = '2026-11-10 12:25+02', actual_temperature_c = 25
WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 1;
UPDATE batch_stages SET actual_start_at = '2026-11-10 12:25+02', actual_temperature_c = 22
WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 2;
DO $$
DECLARE
    v_stage uuid;
BEGIN
    SELECT id INTO v_stage FROM batch_stages
    WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 2;
    CALL sp_extend_batch_stage(v_stage, 30);
END;
$$;
UPDATE batch_stages SET actual_end_at = '2026-11-10 16:55+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 2;
UPDATE batch_stages SET actual_start_at = '2026-11-10 17:00+02', actual_end_at = '2026-11-10 17:10+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 3;
UPDATE batch_stages SET actual_start_at = '2026-11-10 17:15+02', actual_end_at = '2026-11-11 03:30+02', actual_temperature_c = 4
WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 4;
UPDATE batch_stages SET actual_start_at = '2026-11-11 03:40+02', actual_end_at = '2026-11-11 04:30+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000002' AND sort_order = 5;

UPDATE batch_stages SET actual_start_at = '2026-11-10 12:30+02', actual_end_at = '2026-11-10 12:50+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000003' AND sort_order = 1;
UPDATE batch_stages SET actual_start_at = '2026-11-10 12:50+02', actual_end_at = '2026-11-10 16:20+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000003' AND sort_order = 2;
UPDATE batch_stages SET actual_start_at = '2026-11-10 17:10+02', actual_end_at = '2026-11-10 17:20+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000003' AND sort_order = 3;
UPDATE batch_stages SET actual_start_at = '2026-11-10 17:25+02', actual_end_at = '2026-11-11 03:30+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000003' AND sort_order = 4;
UPDATE batch_stages SET actual_start_at = '2026-11-11 04:30+02', actual_end_at = '2026-11-11 05:20+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000003' AND sort_order = 5;

-- finishing the batch creates the history record automatically (US-31)
UPDATE batches SET status = 'done', completed_at = '2026-11-11 05:30+02' WHERE id = '00000000-0000-4000-6001-000000000002';
UPDATE bake_records SET rating = 4, comment = 'Good oven spring; the rye loaves were a bit dense'
WHERE batch_id = '00000000-0000-4000-6001-000000000002';

-- B3: cancelled before the start (its reminders are cancelled by the trigger)
UPDATE batches SET status = 'cancelled' WHERE id = '00000000-0000-4000-6001-000000000003';

-- B4: first stage done, second running
UPDATE batch_stages SET actual_start_at = now() - interval '3 hours', actual_end_at = now() - interval '2 hours 40 minutes'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000005' AND sort_order = 1;
UPDATE batch_stages SET actual_start_at = now() - interval '2 hours 40 minutes'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000005' AND sort_order = 2;

-- B5: Boris logged only two stages, then marked the batch as done; one stage skipped
UPDATE batch_stages SET actual_start_at = '2026-11-08 10:10+02', actual_end_at = '2026-11-08 10:45+02'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000006' AND sort_order = 1;
UPDATE batch_stages SET actual_start_at = '2026-11-08 10:45+02', actual_end_at = '2026-11-08 16:30+02', actual_temperature_c = 26,
                        note = 'Dough rose too fast'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000006' AND sort_order = 2;
UPDATE batch_stages SET status = 'skipped'
WHERE batch_item_id = '00000000-0000-4000-b001-000000000006' AND sort_order = 3;
UPDATE batches SET status = 'done', completed_at = '2026-11-08 18:00+02' WHERE id = '00000000-0000-4000-6001-000000000005';
UPDATE bake_records SET rating = 2, comment = 'Flat loaf, dense crumb, sour smell'
WHERE batch_id = '00000000-0000-4000-6001-000000000005';

------------------------------------------------------------------------------------------------
-- Assistant chat (US-34, US-36)
------------------------------------------------------------------------------------------------
INSERT INTO chat_conversations (id, owner_id, title, created_at, updated_at) VALUES
 ('00000000-0000-4000-8001-000000000001', '00000000-0000-4000-a001-0000000000a1', 'Batch for tomorrow', now() - interval '1 hour', now() - interval '1 hour'),
 ('00000000-0000-4000-8001-000000000002', '00000000-0000-4000-a001-0000000000a2', 'Why is my loaf flat?', '2026-11-08 19:00+02', '2026-11-08 19:05+02');

INSERT INTO chat_messages (conversation_id, role, content, tool_name, tool_payload, input_tokens, output_tokens, created_at) VALUES
 ('00000000-0000-4000-8001-000000000001', 'user', 'Create a batch of 3 white homemade loaves for tomorrow at 12', NULL, NULL, NULL, NULL, now() - interval '60 minutes'),
 ('00000000-0000-4000-8001-000000000001', 'assistant', 'Calculating the plan.', 'calculate_cost',
  '{"recipeId": "00000000-0000-4000-2001-000000000001", "loaves": 3, "asOf": "today"}', 1450, 60, now() - interval '59 minutes'),
 ('00000000-0000-4000-8001-000000000001', 'tool', 'calculate_cost result', 'calculate_cost',
  '{"total": 11.22, "currency": "EUR", "isComplete": true}', NULL, NULL, now() - interval '59 minutes'),
 ('00000000-0000-4000-8001-000000000001', 'assistant', 'Plan: 3 loaves, ready 02:00-04:00, cost 11.22 EUR, prepare 300 g of starter. Save the batch?', NULL, NULL, 1600, 120, now() - interval '58 minutes'),
 ('00000000-0000-4000-8001-000000000002', 'user', 'My loaf is flat and dense. What went wrong?', NULL, NULL, NULL, NULL, '2026-11-08 19:00+02'),
 ('00000000-0000-4000-8001-000000000002', 'tool', 'analyze_bake result', 'analyze_bake',
  '{"batchId": "00000000-0000-4000-6001-000000000005", "deviations": [{"stage": "Bulk", "deviationMinutes": 45, "temperatureC": 26}]}',
  NULL, NULL, '2026-11-08 19:01+02'),
 ('00000000-0000-4000-8001-000000000002', 'assistant', 'Bulk fermentation ran 45 min longer than planned at 26 C: the dough was probably over-fermented.', NULL, NULL, 2100, 180, '2026-11-08 19:02+02');

UPDATE bake_records
SET assistant_analysis = 'Bulk fermentation exceeded the planned maximum by 45 minutes at 26 C; over-fermentation is the most probable cause.',
    analysis_at = '2026-11-08 19:02+02'
WHERE batch_id = '00000000-0000-4000-6001-000000000005';

------------------------------------------------------------------------------------------------
-- Shared library (US-39-41) and moderation (LR-11)
------------------------------------------------------------------------------------------------
CALL sp_publish_recipe('00000000-0000-4000-2001-000000000001', NULL);  -- Anna: white loaf
CALL sp_publish_recipe('00000000-0000-4000-2001-000000000002', NULL);  -- Anna: variant with rye
CALL sp_publish_recipe('00000000-0000-4000-2001-000000000007', NULL);  -- Clara: brioche
CALL sp_publish_recipe('00000000-0000-4000-2001-000000000005', NULL);  -- Boris: first sourdough

-- Clara unpublishes her brioche; the snapshot stays for comments history
UPDATE published_recipes SET status = 'unpublished' WHERE source_recipe_id = '00000000-0000-4000-2001-000000000007';

INSERT INTO recipe_comments (published_recipe_id, author_id, body, is_removed)
SELECT id, '00000000-0000-4000-a001-0000000000a4', 'Baked it on Sunday, great oven spring!', false
FROM published_recipes WHERE source_recipe_id = '00000000-0000-4000-2001-000000000001';
INSERT INTO recipe_comments (published_recipe_id, author_id, body, is_removed)
SELECT id, '00000000-0000-4000-a001-0000000000a3', 'Try 30 g less water if your flour is weaker.', false
FROM published_recipes WHERE source_recipe_id = '00000000-0000-4000-2001-000000000001';
INSERT INTO recipe_comments (published_recipe_id, author_id, body, is_removed)
SELECT id, '00000000-0000-4000-a001-0000000000a4', 'Buy cheap flour at www.spam.example', false
FROM published_recipes WHERE source_recipe_id = '00000000-0000-4000-2001-000000000002';

INSERT INTO recipe_reactions (published_recipe_id, user_id, reaction)
SELECT id, u.user_id, u.reaction
FROM published_recipes
CROSS JOIN (VALUES ('00000000-0000-4000-a001-0000000000a4'::uuid, 'like'),
                   ('00000000-0000-4000-a001-0000000000a3'::uuid, 'want_to_bake'),
                   ('00000000-0000-4000-a001-0000000000a2'::uuid, 'love')) AS u(user_id, reaction)
WHERE source_recipe_id = '00000000-0000-4000-2001-000000000001';

-- A reported spam comment: removed by the administrator, report resolved
INSERT INTO content_reports (reporter_id, comment_id, reason, status)
SELECT '00000000-0000-4000-a001-0000000000a1', c.id, 'Advertising', 'open'
FROM recipe_comments c WHERE c.body LIKE 'Buy cheap flour%';
UPDATE recipe_comments SET is_removed = true WHERE body LIKE 'Buy cheap flour%';
UPDATE content_reports SET status = 'resolved', resolved_by_id = '00000000-0000-4000-a001-0000000000a5'
WHERE reason = 'Advertising';

-- Boris's publication is reported and removed by the administrator
INSERT INTO content_reports (reporter_id, published_recipe_id, reason, status)
SELECT '00000000-0000-4000-a001-0000000000a3', id, 'Copied from a book without attribution', 'open'
FROM published_recipes WHERE source_recipe_id = '00000000-0000-4000-2001-000000000005';
UPDATE published_recipes SET status = 'removed', removed_reason = 'Copyright complaint upheld'
WHERE source_recipe_id = '00000000-0000-4000-2001-000000000005';
UPDATE content_reports SET status = 'resolved', resolved_by_id = '00000000-0000-4000-a001-0000000000a5'
WHERE reason = 'Copied from a book without attribution';

-- An open report waiting for moderation
INSERT INTO content_reports (reporter_id, published_recipe_id, reason, status)
SELECT '00000000-0000-4000-a001-0000000000a4', id, 'Quantities look wrong', 'open'
FROM published_recipes WHERE source_recipe_id = '00000000-0000-4000-2001-000000000002';

-- Dmitry copies the white loaf into his workspace (US-42, attribution)
INSERT INTO recipes (id, owner_id, copied_from_published_recipe_id, title, yield_loaves, loaf_weight_g, is_archived)
SELECT '00000000-0000-4000-2001-000000000008', '00000000-0000-4000-a001-0000000000a4', id, 'White loaf (copy)', 1, 900, false
FROM published_recipes WHERE source_recipe_id = '00000000-0000-4000-2001-000000000001';

-- Push subscription of Anna's phone (Web Push)
INSERT INTO push_subscriptions (user_id, endpoint, p256dh, auth) VALUES
 ('00000000-0000-4000-a001-0000000000a1', 'https://push.example.com/send/anna-phone-1',
  'BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM', 'tBHItJI5svbpez7KI4CCXg');

COMMIT;

-- Statistics are refreshed after the history changed
SELECT fn_refresh_bake_statistics();
