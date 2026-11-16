-- What the reporting user (oneloaf_reader, role app_read) can and cannot do. Run as oneloaf_reader:
--   psql -h <host> -U oneloaf_reader -d oneloaf -v ON_ERROR_STOP=1 -f db/tests/security_reader.sql

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

CREATE FUNCTION pg_temp.check(p_name text, p_ok boolean, p_detail text DEFAULT NULL) RETURNS void
LANGUAGE sql AS $$ INSERT INTO test_results (name, passed, detail) VALUES (p_name, coalesce(p_ok, false), p_detail) $$;

-- allowed: masked personal data, numbers, reports
SELECT pg_temp.check('masked users are readable, e-mail is masked',
    (SELECT bool_and(email_masked LIKE '%***%') FROM v_users_masked), (SELECT string_agg(email_masked, ', ') FROM v_users_masked));
SELECT pg_temp.check('baking statistics are readable', (SELECT count(*) > 0 FROM mv_bake_statistics));
SELECT pg_temp.check('data quality report is readable', (SELECT count(*) > 0 FROM v_data_quality_issues));
SELECT pg_temp.check('cost calculation is allowed',
    (SELECT amount FROM fn_recipe_cost('00000000-0000-4000-2001-000000000001', 3, '2026-11-15') WHERE line_type = 'total') = 11.22);
SELECT pg_temp.check('numbers of bake_records are readable',
    (SELECT count(*) > 0 FROM (SELECT id, rating, total_cost FROM bake_records) s));

-- denied: personal data in clear text, free text, any change
SELECT pg_temp.expect_denied('read users (e-mail in clear text)',  $q$SELECT email FROM users$q$);
SELECT pg_temp.expect_denied('read chat messages',                 $q$SELECT content FROM chat_messages$q$);
SELECT pg_temp.expect_denied('read comments',                      $q$SELECT body FROM recipe_comments$q$);
SELECT pg_temp.expect_denied('read push subscriptions',            $q$SELECT endpoint FROM push_subscriptions$q$);
SELECT pg_temp.expect_denied('read the free-text comment of a bake', $q$SELECT comment FROM bake_records$q$);
SELECT pg_temp.expect_denied('insert data',                        $q$INSERT INTO flours (owner_id, name, flour_type_code) SELECT id, 'x', 'wheat' FROM v_users_masked LIMIT 1$q$);
SELECT pg_temp.expect_denied('update data',                        $q$UPDATE recipes SET title = 'x'$q$);
SELECT pg_temp.expect_denied('delete data',                        $q$DELETE FROM batches$q$);
SELECT pg_temp.expect_denied('run a procedure',                    $q$CALL sp_anonymize_user('00000000-0000-4000-a001-0000000000a1')$q$);
SELECT pg_temp.expect_denied('refresh statistics',                 $q$SELECT fn_refresh_bake_statistics()$q$);
SELECT pg_temp.expect_denied('create a table',                     $q$CREATE TABLE public.r (id int)$q$);

SELECT no, name, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS result, left(detail, 90) AS detail FROM test_results ORDER BY no;
SELECT count(*) FILTER (WHERE passed) AS passed, count(*) AS total FROM test_results;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM test_results WHERE NOT passed) THEN
        RAISE EXCEPTION 'Reader security tests failed: %', (SELECT string_agg(name, '; ') FROM test_results WHERE NOT passed);
    END IF;
END;
$$;
ROLLBACK;
