-- Volume data for checking indexes and query plans (not needed for normal use).
-- Adds 300 bakers with a year of activity: about 22 000 prices, 6 000 batches, 24 000 stages, 6 000 reminders.
-- Run as app_admin after the test data (the owner may run ANALYZE):
--   psql -h <host> -U app_admin -d oneloaf -v ON_ERROR_STOP=1 -f db/seed/volume_data.sql

\set ON_ERROR_STOP on
BEGIN;

-- bakers (a valid PBKDF2 hash of a random password; nobody can log in)
INSERT INTO users (id, email, normalized_email, password_hash, display_name, role)
SELECT ('00000000-0000-4000-c000-' || lpad(to_hex(g), 12, '0'))::uuid,
       'load.baker' || g || '@example.com', 'LOAD.BAKER' || g || '@EXAMPLE.COM',
       'AQAAAAIAAYagAAAAEBpmbyGT7gMbTssTJ7JTAjfzlJxTY0zLZQQEI1/kdVqIzu4IbM+rwo9m+maHp19a6A==',
       'Load Baker ' || g, 'user'
FROM generate_series(1, 300) g;

INSERT INTO kitchen_settings (user_id, oven_power_kw, oven_capacity_loaves, mixer_bowl_capacity_kg, electricity_tariff_per_kwh,
                              packaging_cost_per_loaf, loss_rate_percent, labor_rate_per_hour, currency, time_zone)
SELECT id, 2 + random() * 3, 2 + (random() * 6)::int, 3 + random() * 5, 0.15 + random() * 0.15, 0.10, 5 + random() * 10, NULL, 'EUR', 'Europe/Vilnius'
FROM users WHERE email LIKE 'load.baker%';

-- 6 ingredients per baker
INSERT INTO ingredients (owner_id, name, category_code, unit_code)
SELECT u.id, i.name, i.cat, i.unit
FROM users u
CROSS JOIN (VALUES ('Flour', 'flour', 'g'), ('Rye flour', 'flour', 'g'), ('Water', 'water', 'ml'),
                   ('Salt', 'salt', 'g'), ('Starter', 'starter', 'g'), ('Seeds', 'add_in', 'g')) AS i(name, cat, unit)
WHERE u.email LIKE 'load.baker%';

-- a price per month for a year
INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
SELECT i.id, round((0.5 + random() * 2)::numeric, 2), 1000, 'EUR', (date '2025-12-01' + make_interval(months => m))::date
FROM ingredients i
JOIN users u ON u.id = i.owner_id AND u.email LIKE 'load.baker%'
CROSS JOIN generate_series(0, 11) m;

-- 3 recipes per baker with 4 lines and 4 stages
INSERT INTO recipes (owner_id, title, yield_loaves, loaf_weight_g, is_archived)
SELECT u.id, 'Loaf ' || r, 1, 900, false
FROM users u CROSS JOIN generate_series(1, 3) r
WHERE u.email LIKE 'load.baker%';

INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, is_optional, sort_order, is_removed)
SELECT r.id, i.id, q.qty, false, q.ord, false
FROM recipes r
JOIN users u ON u.id = r.owner_id AND u.email LIKE 'load.baker%'
CROSS JOIN (VALUES ('Flour', 500, 1), ('Water', 350, 2), ('Salt', 10, 3), ('Starter', 100, 4)) AS q(name, qty, ord)
JOIN ingredients i ON i.owner_id = r.owner_id AND i.name = q.name;

INSERT INTO recipe_stages (recipe_id, sort_order, name, stage_type_code, min_duration_minutes, max_duration_minutes,
                           active_minutes, uses_oven, uses_mixer, is_optional, is_removed)
SELECT r.id, s.ord, s.name, s.code, s.mn, s.mx, s.act, s.code = 'bake', s.code = 'mix', false, false
FROM recipes r
JOIN users u ON u.id = r.owner_id AND u.email LIKE 'load.baker%'
CROSS JOIN (VALUES (1, 'Mix', 'mix', 15, 20, 15), (2, 'Bulk', 'bulk_fermentation', 180, 240, 10),
                   (3, 'Proof', 'proof', 60, 90, 0), (4, 'Bake', 'bake', 40, 50, 5)) AS s(ord, name, code, mn, mx, act);

-- 20 batches per baker over a year: one item and four stages each
CREATE TEMP TABLE v_batches ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, u.id AS owner_id, g AS n,
       timestamptz '2025-12-01 08:00+02' + make_interval(days => (g * 18 + (random() * 5)::int)) AS start_at
FROM users u CROSS JOIN generate_series(1, 20) g
WHERE u.email LIKE 'load.baker%';

INSERT INTO batches (id, owner_id, name, status, source, planned_start_at, planned_ready_from, planned_ready_to,
                     price_date, total_cost, currency)
SELECT id, owner_id, 'Batch ' || n, 'planned', 'manual', start_at,
       start_at + interval '295 minutes', start_at + interval '400 minutes', start_at::date, round((5 + random() * 20)::numeric, 2), 'EUR'
FROM v_batches;

INSERT INTO batch_items (batch_id, recipe_id, loaves, sort_order)
SELECT b.id, (SELECT r.id FROM recipes r WHERE r.owner_id = b.owner_id ORDER BY r.title LIMIT 1 OFFSET (b.n % 3)),
       1 + (random() * 5)::int, 1
FROM v_batches b;

INSERT INTO batch_stages (batch_item_id, sort_order, name, stage_type_code, planned_min_minutes, planned_max_minutes,
                          planned_start_at, planned_end_min_at, planned_end_max_at, status, extended_minutes, is_overridden)
SELECT bi.id, s.ord, s.name, s.code, s.mn, s.mx,
       b.start_at + make_interval(mins => s.off), b.start_at + make_interval(mins => s.off + s.mn),
       b.start_at + make_interval(mins => s.off + s.mx), 'pending', 0, false
FROM v_batches b
JOIN batch_items bi ON bi.batch_id = b.id
CROSS JOIN (VALUES (1, 'Mix', 'mix', 15, 20, 0), (2, 'Bulk', 'bulk_fermentation', 180, 240, 15),
                   (3, 'Proof', 'proof', 60, 90, 195), (4, 'Bake', 'bake', 40, 50, 255)) AS s(ord, name, code, mn, mx, off);

-- reminders: past ones sent, future ones scheduled (the queue of the Notification service)
INSERT INTO reminders (owner_id, kind, batch_item_id, due_at, channel, status, message, sent_at, attempts)
SELECT b.owner_id, 'before_kneading', bi.id, b.start_at - interval '1 hour', 'push',
       CASE WHEN b.start_at - interval '1 hour' < now() THEN 'sent' ELSE 'scheduled' END,
       'Prepare the starter',
       CASE WHEN b.start_at - interval '1 hour' < now() THEN b.start_at - interval '1 hour' END,
       CASE WHEN b.start_at - interval '1 hour' < now() THEN 1 ELSE 0 END
FROM v_batches b JOIN batch_items bi ON bi.batch_id = b.id;

COMMIT;

ANALYZE users, kitchen_settings, ingredients, ingredient_prices, recipes, recipe_ingredients, recipe_stages,
        batches, batch_items, batch_stages, reminders;

SELECT relname AS table_name, n_live_tup AS rows
FROM pg_stat_user_tables
WHERE relname IN ('users', 'ingredient_prices', 'recipes', 'batches', 'batch_stages', 'reminders')
ORDER BY relname;
