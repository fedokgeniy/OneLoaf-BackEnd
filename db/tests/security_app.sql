-- What the application user (oneloaf_app, role app_write) can and cannot do. Run as oneloaf_app:
--   psql -h <host> -U oneloaf_app -d oneloaf -v ON_ERROR_STOP=1 -f db/tests/security_app.sql

\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE test_results (no serial, name text, passed boolean, detail text) ON COMMIT DROP;

CREATE FUNCTION pg_temp.expect_denied(p_name text, p_sql text) RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE p_sql;
        INSERT INTO test_results (name, passed, detail) VALUES (p_name, false, 'statement was allowed');
    EXCEPTION WHEN insufficient_privilege THEN
        INSERT INTO test_results (name, passed, detail) VALUES (p_name, true, SQLSTATE || ' ' || SQLERRM);
    WHEN others THEN
        INSERT INTO test_results (name, passed, detail) VALUES (p_name, false, 'unexpected ' || SQLSTATE || ' ' || SQLERRM);
    END;
END;
$$;

CREATE FUNCTION pg_temp.expect_allowed(p_name text, p_sql text) RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE p_sql;
        INSERT INTO test_results (name, passed, detail) VALUES (p_name, true, 'allowed');
    EXCEPTION WHEN others THEN
        INSERT INTO test_results (name, passed, detail) VALUES (p_name, false, SQLSTATE || ' ' || SQLERRM);
    END;
END;
$$;

SELECT pg_temp.expect_allowed('read and write workspace data',
  $q$UPDATE recipes SET notes = 'checked' WHERE id = '00000000-0000-4000-2001-000000000001'$q$);
SELECT pg_temp.expect_allowed('call a procedure',
  $q$CALL sp_build_batch_plan('00000000-0000-4000-6001-000000000001')$q$);
SELECT pg_temp.expect_allowed('refresh statistics through the SECURITY DEFINER function',
  $q$SELECT fn_refresh_bake_statistics()$q$);
SELECT pg_temp.expect_allowed('read reference data',
  $q$SELECT count(*) FROM units$q$);

SELECT pg_temp.expect_denied('create a table',            $q$CREATE TABLE public.hack (id int)$q$);
SELECT pg_temp.expect_denied('drop a table',              $q$DROP TABLE recipes$q$);
SELECT pg_temp.expect_denied('alter a table',             $q$ALTER TABLE users ADD COLUMN x int$q$);
SELECT pg_temp.expect_denied('truncate a table',          $q$TRUNCATE chat_messages$q$);
SELECT pg_temp.expect_denied('change reference data',     $q$INSERT INTO units (code, name, dimension, factor_to_base, sort_order) VALUES ('cup', 'cup', 'volume', 240, 9)$q$);
SELECT pg_temp.expect_denied('delete reference data',     $q$DELETE FROM currencies WHERE code = 'PLN'$q$);
SELECT pg_temp.expect_denied('read the migrations history', $q$SELECT * FROM __ef_migrations_history$q$);
SELECT pg_temp.expect_denied('rewrite the migrations history', $q$DELETE FROM __ef_migrations_history$q$);
SELECT pg_temp.expect_denied('refresh the materialized view directly', $q$REFRESH MATERIALIZED VIEW mv_bake_statistics$q$);
SELECT pg_temp.expect_denied('replace a function',        $q$CREATE OR REPLACE FUNCTION fn_mask_email(p_email text) RETURNS text LANGUAGE sql AS 'SELECT p_email'$q$);
SELECT pg_temp.expect_denied('give itself more rights',   $q$ALTER ROLE oneloaf_app CREATEROLE$q$);
SELECT pg_temp.expect_denied('become another role',       $q$SET ROLE app_admin$q$);

-- GRANT by a non-owner only raises a warning; check that nothing was granted
SET LOCAL client_min_messages = error;
DO $$ BEGIN GRANT SELECT ON users TO app_read; EXCEPTION WHEN others THEN NULL; END $$;
RESET client_min_messages;
INSERT INTO test_results (name, passed, detail)
VALUES ('cannot grant privileges on users to another role', NOT has_table_privilege('app_read', 'users', 'SELECT'), NULL);

SELECT no, name, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS result, left(detail, 90) AS detail FROM test_results ORDER BY no;
SELECT count(*) FILTER (WHERE passed) AS passed, count(*) AS total FROM test_results;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM test_results WHERE NOT passed) THEN
        RAISE EXCEPTION 'Application security tests failed: %', (SELECT string_agg(name, '; ') FROM test_results WHERE NOT passed);
    END IF;
END;
$$;
ROLLBACK;
