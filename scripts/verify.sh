#!/usr/bin/env bash
# End-to-end verification of the OneLoaf database on a clean PostgreSQL server.
# Used by CI (.github/workflows/database.yml) and can be run locally.
#
# Steps:
#   1. bootstrap roles and two empty databases (superuser, the only superuser step)
#   2. apply the EF Core migrations with the migrator (as app_admin)
#   3. check that db/schema.sql matches the migrations and that the model has no pending changes
#   4. create the second database from db/schema.sql and compare both schemas;
#      roll it back to empty with the Down methods, re-apply the migrations and compare again
#   5. load the test data (as oneloaf_app)
#   6. run the tests: constraints and business logic (oneloaf_app), security (each role)
#   7. print the data quality report (as oneloaf_reader)
#   8. check that the generated documentation matches the schema
#   9. optionally load the volume data and show query plans (VOLUME=1)
#
# Environment:
#   PGHOST, PGPORT                       server (default localhost:5432)
#   PG_SUPERUSER, PG_SUPERUSER_PASSWORD  superuser for step 1 (default postgres)
#   ONELOAF_ADMIN_PASSWORD, ONELOAF_APP_PASSWORD, ONELOAF_READER_PASSWORD   passwords of the roles
#   VERIFY_DB                            database name (default oneloaf_verify); VERIFY_DB and VERIFY_DB_sql
#                                        are DROPPED and recreated
#   VOLUME=1                             also run db/seed/volume_data.sql and db/queries/explain_plans.sql

set -euo pipefail

: "${ONELOAF_ADMIN_PASSWORD:?set ONELOAF_ADMIN_PASSWORD}"
: "${ONELOAF_APP_PASSWORD:?set ONELOAF_APP_PASSWORD}"
: "${ONELOAF_READER_PASSWORD:?set ONELOAF_READER_PASSWORD}"
export PGHOST="${PGHOST:-localhost}" PGPORT="${PGPORT:-5432}"
SU="${PG_SUPERUSER:-postgres}"
DB="${VERIFY_DB:-oneloaf_verify}"
DB_SQL="${DB}_sql"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

step() { printf '\n=== %s\n' "$*"; }
as_su()     { PGPASSWORD="${PG_SUPERUSER_PASSWORD:-}" psql -X -q -v ON_ERROR_STOP=1 -U "$SU" "$@"; }
as_admin()  { PGPASSWORD="$ONELOAF_ADMIN_PASSWORD"  psql -X -q -v ON_ERROR_STOP=1 -U app_admin      "$@"; }
as_app()    { PGPASSWORD="$ONELOAF_APP_PASSWORD"    psql -X -q -v ON_ERROR_STOP=1 -U oneloaf_app    "$@"; }
as_reader() { PGPASSWORD="$ONELOAF_READER_PASSWORD" psql -X -q -v ON_ERROR_STOP=1 -U oneloaf_reader "$@"; }
schema_dump() { PGPASSWORD="$ONELOAF_ADMIN_PASSWORD" pg_dump -U app_admin --schema-only "$1" \
                | grep -v -E '^--|^$|^\\(un)?restrict' | sed 's/^[[:space:]]*//'; }

step "1. Bootstrap roles and databases ($DB, $DB_SQL)"
as_su -d postgres -c "DROP DATABASE IF EXISTS $DB WITH (FORCE)" -c "DROP DATABASE IF EXISTS $DB_SQL WITH (FORCE)"
for d in "$DB" "$DB_SQL"; do
  as_su -d postgres -v db_name="$d" -v admin_password="$ONELOAF_ADMIN_PASSWORD" \
        -v app_password="$ONELOAF_APP_PASSWORD" -v reader_password="$ONELOAF_READER_PASSWORD" \
        -f db/security/bootstrap.sql 2>&1 | grep -v 'already been granted' || true
done

step "2. Apply migrations (DbMigrator as app_admin)"
ConnectionStrings__OneLoaf="Host=$PGHOST;Port=$PGPORT;Database=$DB;Username=app_admin;Password=$ONELOAF_ADMIN_PASSWORD" \
  dotnet run -c Release --project src/OneLoaf.DbMigrator

step "3. db/schema.sql matches the migrations; no pending model changes"
dotnet tool restore >/dev/null
dotnet ef migrations script --idempotent --no-build --configuration Release \
  --project src/OneLoaf.Infrastructure --startup-project src/OneLoaf.Infrastructure -o /tmp/oneloaf_schema.sql >/dev/null
diff <(sed '1s/^\xEF\xBB\xBF//' db/schema.sql) <(sed '1s/^\xEF\xBB\xBF//' /tmp/oneloaf_schema.sql)
dotnet ef migrations has-pending-model-changes --no-build --configuration Release \
  --project src/OneLoaf.Infrastructure --startup-project src/OneLoaf.Infrastructure

step "4. db/schema.sql creates the same schema"
as_admin -d "$DB_SQL" -f db/schema.sql
diff <(schema_dump "$DB") <(schema_dump "$DB_SQL")
echo "schemas are identical"
CS_SQL="Host=$PGHOST;Port=$PGPORT;Database=$DB_SQL;Username=app_admin;Password=$ONELOAF_ADMIN_PASSWORD"
ConnectionStrings__OneLoaf="$CS_SQL" dotnet ef database update 0 --no-build --configuration Release \
  --project src/OneLoaf.Infrastructure --startup-project src/OneLoaf.Infrastructure >/dev/null
left=$(as_admin -d "$DB_SQL" -tAc "SELECT count(*) FROM pg_class WHERE relnamespace = 'public'::regnamespace AND relkind IN ('r','v','m') AND relname <> '__ef_migrations_history'")
[[ "$left" == "0" ]] || { echo "rollback left $left relations"; exit 1; }
ConnectionStrings__OneLoaf="$CS_SQL" dotnet run -c Release --no-build --project src/OneLoaf.DbMigrator >/dev/null
diff <(schema_dump "$DB") <(schema_dump "$DB_SQL")
echo "rollback of all migrations and re-apply: OK"

step "5. Test data (as oneloaf_app)"
as_app -d "$DB" -f db/seed/test_data.sql >/dev/null

step "6. Tests"
as_app    -d "$DB" -P pager=off -f db/tests/constraint_tests.sql | tail -n 4
as_app    -d "$DB" -P pager=off -f db/tests/business_tests.sql   | tail -n 4
as_admin  -d "$DB" -P pager=off -f db/tests/security_audit.sql   | tail -n 4
as_app    -d "$DB" -P pager=off -f db/tests/security_app.sql     | tail -n 4
as_reader -d "$DB" -P pager=off -f db/tests/security_reader.sql  | tail -n 4
as_app    -d "$DB" -f db/queries/examples.sql >/dev/null && echo "example queries run"

step "7. Data quality report (as oneloaf_reader)"
as_reader -d "$DB" -P pager=off -c "SELECT issue_code, count(*) FROM v_data_quality_issues GROUP BY 1 ORDER BY 1"

step "8. Documentation matches the schema"
DOCS_TMP="$(mktemp -d)"
export PGUSER=app_admin PGDATABASE="$DB" PGPASSWORD="$ONELOAF_ADMIN_PASSWORD"
python3 tools/generate_data_dictionary.py > "$DOCS_TMP/data-dictionary.md"
DOCS_OUT="$DOCS_TMP" python3 tools/generate_logical_schema.py
unset PGUSER PGDATABASE PGPASSWORD
for f in data-dictionary.md logical-schema.md logical-schema.dot; do
  diff -q "docs/db/$f" "$DOCS_TMP/$f" >/dev/null || { echo "docs/db/$f is outdated: regenerate it (see README)"; diff "docs/db/$f" "$DOCS_TMP/$f" | head -20; exit 1; }
done
echo "documentation is up to date"

if [[ "${VOLUME:-0}" == "1" ]]; then
  step "9. Volume data and query plans"
  as_admin -d "$DB" -f db/seed/volume_data.sql
  as_app -d "$DB" -P pager=off -f db/queries/explain_plans.sql
fi

step "All checks passed"
