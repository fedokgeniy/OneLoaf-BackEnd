#!/bin/sh
# Loads the test data and runs all database tests inside the PostgreSQL container.
# Works the same from cmd, PowerShell or bash on the host:
#   docker compose exec db sh /oneloaf/db/tests/run_in_docker.sh
# Inside the container psql connects through the local socket, so no password has to be typed;
# every script still runs under its own role (oneloaf_app, app_admin, oneloaf_reader).
# Stops with a non-zero exit code at the first failed test.
set -eu

DB="${ONELOAF_DB:-oneloaf}"
DIR=/oneloaf/db
OUT=/tmp/oneloaf_test_output.txt

run() {
    role="$1"; file="$2"
    echo ""
    echo "=== $file (as $role)"
    if psql -X -q -v ON_ERROR_STOP=1 -P pager=off -U "$role" -d "$DB" -f "$DIR/$file" > "$OUT" 2>&1; then
        tail -n 4 "$OUT"
    else
        cat "$OUT"
        echo ""
        echo "=== FAILED: $file"
        exit 1
    fi
}

users=$(psql -X -tA -v ON_ERROR_STOP=1 -U app_admin -d "$DB" -c "SELECT count(*) FROM users")
if [ "$users" = "0" ]; then
    echo "=== seed/test_data.sql (as oneloaf_app)"
    psql -X -q -v ON_ERROR_STOP=1 -U oneloaf_app -d "$DB" -f "$DIR/seed/test_data.sql" > /dev/null
    echo "test data loaded"
else
    echo "=== test data already present ($users users), skipping seed/test_data.sql"
fi

run oneloaf_app    tests/constraint_tests.sql
run oneloaf_app    tests/business_tests.sql
run app_admin      tests/security_audit.sql
run oneloaf_app    tests/security_app.sql
run oneloaf_reader tests/security_reader.sql

echo ""
echo "=== All tests passed"
