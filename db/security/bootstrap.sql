-- OneLoaf: one-time cluster setup. Run as a PostgreSQL superuser BEFORE the migrations.
-- Creates the roles and the database; all objects inside the database are then created by
-- migrations under app_admin, and no service ever connects as a superuser.
--
--   psql -h <host> -U postgres -d postgres -v ON_ERROR_STOP=1 \
--        -v db_name=oneloaf \
--        -v admin_password="$ONELOAF_ADMIN_PASSWORD" \
--        -v app_password="$ONELOAF_APP_PASSWORD" \
--        -v reader_password="$ONELOAF_READER_PASSWORD" \
--        -f db/security/bootstrap.sql
--
-- Idempotent: re-running updates the passwords and attributes and changes nothing else.
--
-- Roles:
--   app_admin       LOGIN  owner of the database and of every object; used only by the migrator
--   app_write       group  read/write access for the application services
--   app_read        group  read-only access without personal data
--   oneloaf_app     LOGIN  member of app_write; connection user of the services
--   oneloaf_reader  LOGIN  member of app_read; connection user for reporting

\set ON_ERROR_STOP on

-- Role passwords are stored by PostgreSQL as SCRAM-SHA-256 verifiers, never in clear text or MD5.
SET password_encryption = 'scram-sha-256';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin')      THEN CREATE ROLE app_admin;      END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_write')      THEN CREATE ROLE app_write;      END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_read')       THEN CREATE ROLE app_read;       END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'oneloaf_app')    THEN CREATE ROLE oneloaf_app;    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'oneloaf_reader') THEN CREATE ROLE oneloaf_reader; END IF;
END;
$$;

ALTER ROLE app_admin      WITH LOGIN   NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT
                               CONNECTION LIMIT 5  PASSWORD :'admin_password';
ALTER ROLE app_write      WITH NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
ALTER ROLE app_read       WITH NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
ALTER ROLE oneloaf_app    WITH LOGIN   NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT
                               CONNECTION LIMIT 50 PASSWORD :'app_password';
ALTER ROLE oneloaf_reader WITH LOGIN   NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT
                               CONNECTION LIMIT 10 PASSWORD :'reader_password';

GRANT app_write TO oneloaf_app;
GRANT app_read  TO oneloaf_reader;

-- Statement timeout protects the database from runaway queries of the services
ALTER ROLE oneloaf_app    SET statement_timeout = '30s';
ALTER ROLE oneloaf_reader SET statement_timeout = '60s';

SELECT format('CREATE DATABASE %I OWNER app_admin ENCODING ''UTF8'' TEMPLATE template0', :'db_name')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db_name') \gexec

SELECT format('ALTER DATABASE %I OWNER TO app_admin', :'db_name') \gexec
SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', :'db_name') \gexec
SELECT format('GRANT CONNECT, TEMPORARY ON DATABASE %I TO app_write, app_read', :'db_name') \gexec

\connect :db_name

ALTER SCHEMA public OWNER TO app_admin;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO app_write, app_read;
