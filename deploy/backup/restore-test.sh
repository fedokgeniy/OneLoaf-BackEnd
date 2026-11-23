#!/bin/sh
# Backup and restore test (NFR-12). Runs inside the PostgreSQL container:
#
#   docker compose exec -T -e PGPASSWORD="$ONELOAF_ADMIN_PASSWORD" db sh -s < deploy/backup/restore-test.sh
#   (production, in the deploy folder: docker compose -f docker-compose.prod.yml exec -T -e PGPASSWORD=... db sh -s < backup/restore-test.sh)
#
# 1. dumps the database exactly like the db-backup service (custom format, as app_admin),
# 2. restores the dump into a scratch database as the superuser,
# 3. compares the row count of every table and checks that the functions work in the copy,
# 4. drops the scratch database. Exit code 0 only when everything matches.
set -eu

DB="${ONELOAF_DB:-oneloaf}"
COPY="${DB}_restore_test"
DUMP="/tmp/${DB}-restore-test.dump"

echo "Dumping ${DB}..."
pg_dump -h 127.0.0.1 -U app_admin -d "$DB" -Fc -f "$DUMP"
echo "Dump size: $(du -h "$DUMP" | cut -f1)"

echo "Restoring into ${COPY}..."
psql -q -U postgres -d postgres -c "DROP DATABASE IF EXISTS \"${COPY}\";" -c "CREATE DATABASE \"${COPY}\";"
pg_restore -U postgres -d "$COPY" --exit-on-error "$DUMP"

COUNTS="SELECT string_agg(format('%s=%s', table_name,
          (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM public.%I', table_name), false, true, '')))[1]::text),
          ',' ORDER BY table_name)
        FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'"

ORIGINAL=$(psql -tA -U postgres -d "$DB" -c "$COUNTS")
RESTORED=$(psql -tA -U postgres -d "$COPY" -c "$COUNTS")
TABLES=$(echo "$ORIGINAL" | tr ',' '\n' | wc -l)

STATUS=0
if [ "$ORIGINAL" = "$RESTORED" ]; then
    echo "Row counts match in all ${TABLES} tables."
else
    echo "Row counts differ:"
    echo "$ORIGINAL" | tr ',' '\n' > /tmp/original.txt
    echo "$RESTORED" | tr ',' '\n' > /tmp/restored.txt
    diff /tmp/original.txt /tmp/restored.txt || true
    STATUS=1
fi

FUNCTIONS=$(psql -tA -U postgres -d "$COPY" -c "SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                                                 WHERE n.nspname = 'public' AND (p.proname LIKE 'fn\_%' OR p.proname LIKE 'sp\_%')")
echo "Functions and procedures in the copy: ${FUNCTIONS}"
RECIPE=$(psql -tA -U postgres -d "$COPY" -c "SELECT id FROM recipes ORDER BY created_at LIMIT 1")
if [ -n "$RECIPE" ]; then
    LINES=$(psql -tA -U postgres -d "$COPY" -c "SELECT count(*) FROM fn_recipe_cost('${RECIPE}', 1, current_date)")
    echo "fn_recipe_cost on the copy returned ${LINES} lines."
    [ "$LINES" -gt 0 ] || STATUS=1
fi

psql -q -U postgres -d postgres -c "DROP DATABASE \"${COPY}\";"
rm -f "$DUMP" /tmp/original.txt /tmp/restored.txt
[ "$STATUS" -eq 0 ] && echo "Backup and restore test passed." || echo "Backup and restore test FAILED."
exit "$STATUS"
