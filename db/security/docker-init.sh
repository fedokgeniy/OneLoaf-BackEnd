#!/bin/sh
# Runs once when the PostgreSQL container initializes an empty data directory
# (mounted into /docker-entrypoint-initdb.d). Creates the OneLoaf roles and database
# with passwords from the environment (.env), see db/security/bootstrap.sql.
set -eu

: "${ONELOAF_ADMIN_PASSWORD:?ONELOAF_ADMIN_PASSWORD is not set}"
: "${ONELOAF_APP_PASSWORD:?ONELOAF_APP_PASSWORD is not set}"
: "${ONELOAF_READER_PASSWORD:?ONELOAF_READER_PASSWORD is not set}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
     -v db_name="${ONELOAF_DB:-oneloaf}" \
     -v admin_password="$ONELOAF_ADMIN_PASSWORD" \
     -v app_password="$ONELOAF_APP_PASSWORD" \
     -v reader_password="$ONELOAF_READER_PASSWORD" \
     -f /oneloaf/bootstrap.sql
