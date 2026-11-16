-- Security audit of roles and privileges. Run as app_admin (or a superuser):
--   psql -h <host> -U app_admin -d oneloaf -v ON_ERROR_STOP=1 -f db/tests/security_audit.sql
-- Checks the privilege matrix of every table and view against the policy in docs/db/security.md.
-- A new table without an explicit decision for app_read fails this audit.

\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE test_results (no serial, name text, passed boolean, detail text) ON COMMIT DROP;
CREATE FUNCTION pg_temp.check(p_name text, p_ok boolean, p_detail text DEFAULT NULL) RETURNS void
LANGUAGE sql AS $$ INSERT INTO test_results (name, passed, detail) VALUES (p_name, coalesce(p_ok, false), p_detail) $$;

-- Policy: what each role may do with each relation.
--   W = SELECT/INSERT/UPDATE/DELETE, R = SELECT only, C = column-level SELECT, - = nothing
CREATE TEMP TABLE policy (relname text PRIMARY KEY, app_write char(1), app_read char(1)) ON COMMIT DROP;
INSERT INTO policy VALUES
 ('units','R','R'), ('currencies','R','R'), ('stage_types','R','R'), ('ingredient_categories','R','R'), ('flour_types','R','R'),
 ('users','W','-'), ('push_subscriptions','W','-'), ('kitchen_settings','W','R'),
 ('flours','W','R'), ('ingredients','W','R'), ('ingredient_prices','W','R'),
 ('recipes','W','R'), ('recipe_ingredients','W','R'), ('recipe_stages','W','R'),
 ('batches','W','R'), ('batch_items','W','R'), ('batch_stages','W','R'),
 ('starters','W','R'), ('starter_feedings','W','R'), ('reminders','W','-'),
 ('bake_records','W','C'), ('bake_record_items','W','R'), ('bake_record_photos','W','-'),
 ('chat_conversations','W','-'), ('chat_messages','W','-'),
 ('published_recipes','W','R'), ('recipe_comments','W','-'), ('recipe_reactions','W','R'), ('content_reports','W','-'),
 ('__ef_migrations_history','-','-'),
 ('v_users_masked','R','R'), ('v_ingredient_current_prices','R','R'), ('v_recipe_effective_ingredients','R','R'),
 ('v_batch_stage_log','R','R'), ('v_batch_log_completeness','R','R'), ('v_public_catalog','R','R'),
 ('v_data_quality_issues','R','R'), ('mv_bake_statistics','R','R');

-- every table and view has a policy decision
SELECT pg_temp.check('every relation is covered by the policy',
    NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace AND c.relkind IN ('r', 'v', 'm')
                AND c.relname NOT IN (SELECT relname FROM policy)),
    (SELECT string_agg(relname, ', ') FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace AND c.relkind IN ('r', 'v', 'm')
     AND c.relname NOT IN (SELECT relname FROM policy)));

-- actual privileges match the policy
SELECT pg_temp.check(format('%s: app_write=%s app_read=%s', p.relname, p.app_write, p.app_read),
    (CASE p.app_write
        WHEN 'W' THEN has_table_privilege('app_write', p.relname, 'SELECT,INSERT,UPDATE,DELETE')
        WHEN 'R' THEN has_table_privilege('app_write', p.relname, 'SELECT')
                      AND NOT has_table_privilege('app_write', p.relname, 'INSERT')
                      AND NOT has_table_privilege('app_write', p.relname, 'UPDATE')
                      AND NOT has_table_privilege('app_write', p.relname, 'DELETE')
        ELSE NOT has_table_privilege('app_write', p.relname, 'SELECT,INSERT,UPDATE,DELETE') END)
    AND
    (CASE p.app_read
        WHEN 'R' THEN has_table_privilege('app_read', p.relname, 'SELECT')
                      AND NOT has_table_privilege('app_read', p.relname, 'INSERT,UPDATE,DELETE')
        WHEN 'C' THEN NOT has_table_privilege('app_read', p.relname, 'SELECT')
                      AND has_any_column_privilege('app_read', p.relname, 'SELECT')
                      AND NOT has_table_privilege('app_read', p.relname, 'INSERT,UPDATE,DELETE')
        ELSE NOT has_any_column_privilege('app_read', p.relname, 'SELECT,INSERT,UPDATE') END)
    AND NOT has_table_privilege('app_read', p.relname, 'TRUNCATE')
    AND NOT has_table_privilege('app_write', p.relname, 'TRUNCATE'))
FROM policy p
WHERE to_regclass(p.relname) IS NOT NULL;

SELECT pg_temp.check('app_read cannot see free-text columns of bake_records',
    NOT has_column_privilege('app_read', 'bake_records', 'comment', 'SELECT')
    AND NOT has_column_privilege('app_read', 'bake_records', 'assistant_analysis', 'SELECT'));

-- roles: nobody used by the services is privileged
SELECT pg_temp.check(format('role %s has no superuser/createrole/createdb/bypassrls', r.rolname),
    NOT (r.rolsuper OR r.rolcreaterole OR r.rolcreatedb OR r.rolbypassrls OR r.rolreplication))
FROM pg_roles r WHERE r.rolname IN ('app_admin', 'app_write', 'app_read', 'oneloaf_app', 'oneloaf_reader');

SELECT pg_temp.check('group roles cannot log in',
    NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname IN ('app_write', 'app_read') AND rolcanlogin));
SELECT pg_temp.check('oneloaf_app is a member of app_write only',
    pg_has_role('oneloaf_app', 'app_write', 'MEMBER') AND NOT pg_has_role('oneloaf_app', 'app_read', 'MEMBER')
    AND NOT pg_has_role('oneloaf_app', 'app_admin', 'MEMBER'));
SELECT pg_temp.check('oneloaf_reader is a member of app_read only',
    pg_has_role('oneloaf_reader', 'app_read', 'MEMBER') AND NOT pg_has_role('oneloaf_reader', 'app_write', 'MEMBER'));
SELECT pg_temp.check('role passwords are stored as SCRAM-SHA-256 (not MD5)',
    current_setting('password_encryption') = 'scram-sha-256');

-- objects and schema
SELECT pg_temp.check('all objects are owned by app_admin',
    NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace
                AND c.relowner <> 'app_admin'::regrole),
    (SELECT string_agg(relname, ', ') FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace AND c.relowner <> 'app_admin'::regrole));
SELECT pg_temp.check('application roles cannot create objects in the schema',
    NOT has_schema_privilege('app_write', 'public', 'CREATE') AND NOT has_schema_privilege('app_read', 'public', 'CREATE'));
SELECT pg_temp.check('PUBLIC has no privileges on the schema',
    NOT EXISTS (SELECT 1 FROM pg_namespace n, aclexplode(n.nspacl) a WHERE n.nspname = 'public' AND a.grantee = 0));
SELECT pg_temp.check('PUBLIC cannot execute any function',
    NOT EXISTS (SELECT 1 FROM pg_proc p, aclexplode(coalesce(p.proacl, acldefault('f', p.proowner))) a
                WHERE p.pronamespace = 'public'::regnamespace AND a.grantee = 0),
    (SELECT string_agg(DISTINCT p.proname, ', ') FROM pg_proc p, aclexplode(coalesce(p.proacl, acldefault('f', p.proowner))) a
     WHERE p.pronamespace = 'public'::regnamespace AND a.grantee = 0));
SELECT pg_temp.check('app_read cannot run procedures that change data',
    NOT has_function_privilege('app_read', 'sp_build_batch_plan(uuid)', 'EXECUTE')
    AND NOT has_function_privilege('app_read', 'sp_anonymize_user(uuid)', 'EXECUTE')
    AND NOT has_function_privilege('app_read', 'fn_refresh_bake_statistics()', 'EXECUTE'));
SELECT pg_temp.check('SECURITY DEFINER functions have a fixed search_path',
    NOT EXISTS (SELECT 1 FROM pg_proc p WHERE p.pronamespace = 'public'::regnamespace AND p.prosecdef
                AND NOT EXISTS (SELECT 1 FROM unnest(p.proconfig) c WHERE c LIKE 'search_path=%')));

-- default privileges for future objects
SELECT pg_temp.check('default privileges: new tables are granted to app_write',
    EXISTS (SELECT 1 FROM pg_default_acl d, aclexplode(d.defaclacl) a
            WHERE d.defaclrole = 'app_admin'::regrole AND d.defaclobjtype = 'r' AND a.grantee = 'app_write'::regrole));
SELECT pg_temp.check('default privileges: new functions are not executable by PUBLIC',
    EXISTS (SELECT 1 FROM pg_default_acl d WHERE d.defaclrole = 'app_admin'::regrole AND d.defaclobjtype = 'f'
            AND d.defaclnamespace = 0
            AND NOT EXISTS (SELECT 1 FROM aclexplode(d.defaclacl) a WHERE a.grantee = 0)));

SELECT no, name, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS result, detail FROM test_results ORDER BY no;
SELECT count(*) FILTER (WHERE passed) AS passed, count(*) AS total FROM test_results;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM test_results WHERE NOT passed) THEN
        RAISE EXCEPTION 'Security audit failed: %', (SELECT string_agg(name, '; ') FROM test_results WHERE NOT passed);
    END IF;
END;
$$;
ROLLBACK;
