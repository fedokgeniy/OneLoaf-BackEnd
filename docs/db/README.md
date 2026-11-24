# OneLoaf - database

Database of the bachelor project **"AI-assisted web application for managing recipes and assisting with homemade baking"
(OneLoaf)**: recipes with variants, price history and cost, planning and stage log of production batches, sourdough starters
and reminders, baking history, AI assistant, shared recipe library.

Type: relational transactional database (OLTP), **PostgreSQL 16**. The structure is described with EF Core (.NET 10,
Code First) and created **only by scripts**: a chain of migrations or the DDL script [`db/schema.sql`](../../db/schema.sql)
generated from it.

**Contents:** 31 tables (5 of them reference tables), 63 foreign keys, 106 CHECK constraints, 110 indexes, 37 triggers,
25 functions, 28 trigger functions, 6 procedures, 8 views, 1 materialized view, 3 access roles; 250 automated checks.

## Documentation

| Document | Contents |
|---|---|
| [Data dictionary](data-dictionary.md) | Every table and column: types, NULL, defaults, keys, indexes, CHECK constraints, triggers, data quality expectations; reference values; views; functions and procedures |
| [Logical schema](logical-schema.md) | Colour-coded diagram by functional block ([SVG](logical-schema.svg)), relationship types, ER diagram of every block |
| [Design](design.md) | Choice of the DBMS, data layers, normalization to 3NF and deliberate denormalization, variant model, reference tables, JSON columns, index justification with query plans, logic in the database, versioning |
| [Integrity and transactions](integrity-and-transactions.md) | Levels of integrity, delete policy, rules in triggers, life cycles, atomic operations, isolation level, error codes |
| [Security](security.md) | Roles and access matrix, privileges on new objects, storage of passwords and secrets, masking and anonymization of personal data |

## Requirements coverage

**Documentation**

| Requirement | Where |
|---|---|
| Data dictionary: tables, columns, types, keys, data quality expectations | [data-dictionary.md](data-dictionary.md); generated from the schema by `tools/generate_data_dictionary.py` |
| Data layers | [design.md - Data layers](design.md#data-layers) |
| Data integrity and transactions | [integrity-and-transactions.md](integrity-and-transactions.md) |
| Logical schema with highlighted blocks and colour coding, ER diagram | [logical-schema.md](logical-schema.md), [logical-schema.svg](logical-schema.svg) |
| DDL script | [`db/schema.sql`](../../db/schema.sql) |
| Versioning of the structure | [design.md - Versioning](design.md#versioning-of-the-structure) |

**Design**

| Requirement | Where |
|---|---|
| Modern relational DBMS | PostgreSQL 16; justification in [design.md - Choice of the DBMS](design.md#choice-of-the-dbms) |
| Normalization to 3NF | [design.md - Normalization](design.md#normalization) |
| Enough tables and different relationship types | 30 tables; 1:1, 1:N, M:N, recursive, reference - [logical-schema.md - Relationship types](logical-schema.md#relationship-types) |
| PK, FK, NOT NULL, UNIQUE, CHECK, meaningful data types | Every table; `numeric` for money and quantities, `timestamptz`/`date` for time - [data dictionary](data-dictionary.md) |
| JSON only with a designed model | Three columns with a documented structure checked by CHECK functions - [design.md - JSON columns](design.md#json-columns) |
| Indexes with justification | [design.md - Indexes](design.md#indexes), plans in [`db/queries/explain_plans.sql`](../../db/queries/explain_plans.sql) |
| Different DBMS types, raw - staging - mart layers | Not required by the task; justification in [design.md](design.md#choice-of-the-dbms) |

**Deployment and data**

| Requirement | Where |
|---|---|
| Structure created by scripts or migrations, not manually | EF Core migrations and `db/schema.sql`; see [Getting started](#getting-started) |
| Scripts and migrations in Git | [`src/OneLoaf.Infrastructure/Persistence/Migrations/`](../../src/OneLoaf.Infrastructure/Persistence/Migrations) (11 migrations and their SQL scripts), [`db/`](../../db) |
| Scripts for reference tables | `InitialSchema` migration (EF Core `HasData`, [`ReferenceData.cs`](../../src/OneLoaf.Infrastructure/Persistence/ReferenceData.cs)); values in the [dictionary](data-dictionary.md#reference-values) |
| Test data covering edge cases | [`db/seed/test_data.sql`](../../db/seed/test_data.sql) (5 users, every status; the edge cases are listed in the file header); volume data in [`db/seed/volume_data.sql`](../../db/seed/volume_data.sql) |

**Usage and security**

| Requirement | Where |
|---|---|
| Roles `app_read`, `app_write`, admin | `app_read`, `app_write`, `app_admin` - [security.md](security.md); created by [`db/security/bootstrap.sql`](../../db/security/bootstrap.sql), privileges in the `AccessPrivileges` migration |
| The application does not run as a superuser | Migrator - `app_admin`, services - `oneloaf_app`, reports - `oneloaf_reader`; a superuser is used only for the one-time bootstrap |
| Privileges checked when objects are added | Default privileges and an access policy audit in CI - [security.md - Privileges on new objects](security.md#3-privileges-on-new-objects) |
| Passwords only in encrypted form, no outdated algorithms | PBKDF2-HMAC-SHA512 with at least 100000 iterations; a CHECK rejects plain text, MD5 and SHA-1; role passwords are SCRAM-SHA-256 |
| Integrity through constraints and transactions | [integrity-and-transactions.md](integrity-and-transactions.md) |
| Triggers, procedures and functions with business logic | [design.md - Logic in the database](design.md#logic-in-the-database) |
| Views and materialized views | 8 views and `mv_bake_statistics` - [dictionary - Views](data-dictionary.md#views) |
| Masking and anonymization of personal data | `v_users_masked`, `fn_mask_email`, `fn_mask_name`, `sp_anonymize_user` - [security.md - Personal data](security.md#5-personal-data) |

## Getting started

Passwords are provided through the environment; the repository contains none. For Docker they are kept in the file `.env`,
which is excluded from Git. All commands below are run from the repository root.

### Option 1. Docker

```bash
cp .env.example .env                # set the passwords
docker compose up -d db             # PostgreSQL 16; the first start creates the roles and the database (bootstrap.sql)
docker compose run --rm migrator    # migrations as app_admin
```

Test data and all tests (one command, works in cmd, PowerShell and bash; runs inside the container under
the matching roles, no passwords needed):

```bash
docker compose exec db sh /oneloaf/db/tests/run_in_docker.sh
```

Every test ends with a line `passed | total` where both numbers are equal (71, 64, 57, 17, 16), and the script finishes with
`All tests passed`. It stops with an error at the first failed test.

### Option 2. Your own PostgreSQL and .NET SDK 10

```bash
# 1. Once per server: roles and database (superuser)
psql -h localhost -U postgres -d postgres -v ON_ERROR_STOP=1 \
     -v db_name=oneloaf \
     -v admin_password="$ONELOAF_ADMIN_PASSWORD" \
     -v app_password="$ONELOAF_APP_PASSWORD" \
     -v reader_password="$ONELOAF_READER_PASSWORD" \
     -f db/security/bootstrap.sql

# 2. Migrations as app_admin
export ConnectionStrings__OneLoaf="Host=localhost;Database=oneloaf;Username=app_admin;Password=$ONELOAF_ADMIN_PASSWORD"
dotnet run --project src/OneLoaf.DbMigrator
#    or: dotnet tool restore && dotnet ef database update --project src/OneLoaf.Infrastructure --startup-project src/OneLoaf.Infrastructure

# 3. Test data as the application role
PGPASSWORD="$ONELOAF_APP_PASSWORD" psql -h localhost -U oneloaf_app -d oneloaf -v ON_ERROR_STOP=1 -f db/seed/test_data.sql
```

For local development the connection string can be kept in `dotnet user-secrets`:
`dotnet user-secrets set "ConnectionStrings:OneLoaf" "<connection string>" --project src/OneLoaf.DbMigrator`.

### Option 3. SQL only

After step 1 of option 2:

```bash
PGPASSWORD="$ONELOAF_ADMIN_PASSWORD" psql -h localhost -U app_admin -d oneloaf -v ON_ERROR_STOP=1 -f db/schema.sql
```

`schema.sql` is idempotent: already applied migrations are skipped (tracked in `__ef_migrations_history`).

### Full verification

```bash
export PG_SUPERUSER_PASSWORD=... ONELOAF_ADMIN_PASSWORD=... ONELOAF_APP_PASSWORD=... ONELOAF_READER_PASSWORD=...
VOLUME=1 ./scripts/verify.sh
```

On a clean server the script creates the roles and two databases, applies the migrations, checks that `schema.sql` is up to
date and that the model has no changes without a migration, compares the database built from the migrations with the one built
from `schema.sql`, rolls all migrations back to an empty database and applies them again, loads the test data as the
application role, runs all tests under the matching roles, prints the data quality report, checks that the data dictionary and
the logical schema match the database, loads the volume data and shows the query plans. The same script runs in GitHub Actions
([`.github/workflows/database.yml`](../../.github/workflows/database.yml)).

## Tests

| File | Role | What it checks | Checks |
|---|---|---|---|
| [`db/tests/constraint_tests.sql`](../../db/tests/constraint_tests.sql) | `oneloaf_app` | The database rejects invalid data with an integrity error: formats, ranges, reference data, user isolation, variant rules, status transitions, JSON structure, weak password hashes; cascading account deletion | 71 |
| [`db/tests/business_tests.sql`](../../db/tests/business_tests.sql) | `oneloaf_app` | Functions, procedures, triggers and views return the values of the acceptance criteria US-06 - US-52 and of the ingredient stock and starter ripening | 83 |
| [`db/tests/security_audit.sql`](../../db/tests/security_audit.sql) | `app_admin` | Privileges of every table and view match the policy; role attributes; default privileges; no function is executable by `PUBLIC` | 60 |
| [`db/tests/security_app.sql`](../../db/tests/security_app.sql) | `oneloaf_app` | The application role writes data but cannot change the structure, reference data, migration history or its own privileges | 17 |
| [`db/tests/security_reader.sql`](../../db/tests/security_reader.sql) | `oneloaf_reader` | The reporting role sees personal data only masked and cannot change anything | 16 |

The tests run in a transaction that is rolled back and fail if any check fails.

## Repository structure

```
OneLoaf.slnx                          .NET 10 solution
src/
  OneLoaf.Domain/                     entities and enums
  OneLoaf.Infrastructure/
    Persistence/
      OneLoafDbContext.cs             DbContext
      ModelConventions.cs             snake_case, enum as text + CHECK, defaults
      ReferenceData.cs                reference data
      Configurations/                 keys, indexes, CHECK, ON DELETE (Fluent API)
      Migrations/                     11 migrations
        Sql/<Migration>/*.sql         functions, triggers, procedures, views, privileges
  OneLoaf.DbMigrator/                 console application that applies the migrations
db/
  schema.sql                          DDL of all migrations (idempotent)
  security/bootstrap.sql              roles and database (once, superuser)
  security/docker-init.sh             runs the bootstrap in the container
  seed/test_data.sql                  test data
  seed/volume_data.sql                volume data for checking indexes
  queries/examples.sql                typical queries of the services
  queries/explain_plans.sql           query plans of the key queries
  tests/                              integrity, logic and security tests
docs/db/                              documentation
tools/                                generators of the data dictionary and the logical schema
scripts/verify.sh                     end-to-end verification (local and CI)
docker-compose.yml, .env.example      local PostgreSQL and migrator
```

## Changing the schema

1. Change an entity in `src/OneLoaf.Domain` and/or a configuration in `Persistence/Configurations`; for functions, triggers,
   views and privileges add an SQL script to `Persistence/Migrations/Sql/<NewMigration>/`.
2. Create the migration and update the DDL script:
   ```bash
   dotnet ef migrations add <NewMigration> --project src/OneLoaf.Infrastructure --startup-project src/OneLoaf.Infrastructure --output-dir Persistence/Migrations
   dotnet ef migrations script --idempotent --project src/OneLoaf.Infrastructure --startup-project src/OneLoaf.Infrastructure -o db/schema.sql
   ```
3. Extend the access policy in `db/tests/security_audit.sql` if a table or view was added.
4. Regenerate the documentation from the schema:
   ```bash
   export PGHOST=localhost PGUSER=app_admin PGDATABASE=oneloaf PGPASSWORD=...
   python3 tools/generate_data_dictionary.py > docs/db/data-dictionary.md
   python3 tools/generate_logical_schema.py
   ```
5. Run `./scripts/verify.sh`.

## Technology stack

PostgreSQL 16, .NET 10, EF Core 10.0.15, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, Docker, GitHub Actions,
Graphviz (schema diagram)
