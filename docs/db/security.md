# Database security

Roles and privileges, storage of passwords and secrets, protection of personal data.
Covered by the tests `db/tests/security_audit.sql` (57 checks), `security_app.sql` (17) and `security_reader.sql` (16).

## 1. Roles

| Role | Type | Used by | Privileges |
|---|---|---|---|
| `app_admin` | LOGIN | The migrator (`OneLoaf.DbMigrator`, `dotnet ef`) | Owner of the database, the schema and all objects: DDL, changes of reference data, granting privileges. Not a superuser; cannot create roles or databases |
| `app_write` | group (NOLOGIN) | Application services: Core API, AI Assistant, Notification | `SELECT, INSERT, UPDATE, DELETE` on workspace and library data; read-only reference data; execution of functions and procedures; reading views. No DDL, no `TRUNCATE`, no access to the migration history |
| `app_read` | group (NOLOGIN) | Reports, support, analytics | Read-only access to tables without personal data and free text, to views and statistics; execution of calculation functions. Users only through the masking view |
| `oneloaf_app` | LOGIN, member of `app_write` | Connection string of the services | Same as `app_write`; `statement_timeout = 30s`, at most 50 connections |
| `oneloaf_reader` | LOGIN, member of `app_read` | Connection string of reports | Same as `app_read`; `statement_timeout = 60s`, at most 10 connections |

The requirement "app_read, app_write, admin" is implemented by the roles `app_read`, `app_write` and `app_admin`; the
administrative role is called `app_admin` so that it is not confused with the administrator of the application
(`users.role = 'admin'`). Privileges are granted to group roles, and the services connect with separate logins: the password of
a service can be changed or revoked without touching the privileges.

**The application never uses a superuser.** A superuser is needed once to run `db/security/bootstrap.sql`, which creates the
roles and the database (roles are cluster objects and cannot be created from a migration without `CREATEROLE`). After that all
objects are created by `app_admin` through migrations, and the services work as `oneloaf_app`. The audit checks that none of
these roles has `SUPERUSER`, `CREATEROLE`, `CREATEDB`, `REPLICATION` or `BYPASSRLS`.

## 2. Access matrix

W - `SELECT/INSERT/UPDATE/DELETE`, R - `SELECT` only, C - `SELECT` on some columns, "-" - no access.

| Objects | app_write | app_read |
|---|---|---|
| Reference tables: `units`, `currencies`, `stage_types`, `ingredient_categories`, `flour_types` | R | R |
| `users`, `push_subscriptions` | W | - |
| `kitchen_settings`, `flours`, `ingredients`, `ingredient_prices` | W | R |
| `stock_movements` | W | C (without `note`) |
| `recipes`, `recipe_ingredients`, `recipe_stages` | W | R |
| `batches`, `batch_items`, `batch_stages`, `starters`, `starter_feedings` | W | R |
| `reminders`, `bake_record_photos` | W | - |
| `bake_records` | W | C (without `comment` and `assistant_analysis`) |
| `bake_record_items`, `published_recipes`, `recipe_reactions` | W | R |
| `chat_conversations`, `chat_messages`, `recipe_comments`, `content_reports` (free text) | W | - |
| `__ef_migrations_history` | - | - |
| Views and `mv_bake_statistics` | R | R |
| `v_users_masked` | R | R |
| Procedures, `fn_refresh_bake_statistics` | execute | - |
| Calculation functions, masking functions | execute | execute |
| `PUBLIC` | nothing: privileges on the schema, tables and functions are revoked | |

`app_read` gets tables without personal data: numbers, recipes, schedules. Tables with free text written by users (chat,
comments, reports, bake feedback) are not granted because the text may contain personal data.

## 3. Privileges on new objects

1. **Default privileges** (`ALTER DEFAULT PRIVILEGES` for `app_admin`): every new table is automatically readable and writable
   by `app_write`, every new function is executable by it; `PUBLIC` cannot execute new functions.
2. **`app_read` gets nothing by default.** A new object is reviewed for personal data, and privileges are granted explicitly
   in the same migration.
3. **Audit in CI.** `db/tests/security_audit.sql` contains the access policy of every table and view and compares it with the
   actual privileges (`has_table_privilege`, `has_column_privilege`). A table without a policy entry or with privileges that
   differ from the policy fails the build. Therefore privileges are always checked and updated when an object is added.
4. A new reference table additionally requires `REVOKE INSERT, UPDATE, DELETE ... FROM app_write`.

## 4. Passwords

**Passwords of application users** are stored in `users.password_hash` only as an ASP.NET Core Identity v3 hash:
PBKDF2 with HMAC-SHA512 (or SHA256), at least 100000 iterations, a random 16-byte salt, a 32-byte key.
The database **does not accept** any other format: the CHECK `ck_users_password_hash_strong` calls
`fn_is_strong_password_hash`, which parses the hash header and rejects plain text, MD5, SHA-1, PBKDF2 with HMAC-SHA1 and too few
iterations (covered by tests). The users of the test data have valid hashes of random passwords that are not stored anywhere.

**Passwords of database roles** are stored by PostgreSQL as SCRAM-SHA-256 (`password_encryption = scram-sha-256` in the
bootstrap), not MD5.

**No secrets are stored in the repository.**

- The connection string is passed in the environment variable `ConnectionStrings__OneLoaf` or through `dotnet user-secrets`.
- Role passwords for Docker are in the file `.env`, which is excluded by `.gitignore`; the repository contains only the empty
  `.env.example`.
- `DesignTimeDbContextFactory` uses a connection string without a password to generate migrations: generation does not connect
  to the database.
- In CI the role passwords are generated for every run (`openssl rand`) and masked in the logs; the PostgreSQL container exists
  only during the check.

## 5. Personal data

| Data | Where | Protection |
|---|---|---|
| E-mail, name | `users` | No access for `app_read`; reports use `v_users_masked` (`fn_mask_email`: `anna.baker@example.com` becomes `a***@e***.com`, `fn_mask_name`: `Anna Baker` becomes `A*** B***`, dates without time) |
| Password | `users.password_hash` | Strong hash only (section 4) |
| Free text | chat, comments, reports, feedback | No access for `app_read`; column privileges on `bake_records` |
| Device subscriptions | `push_subscriptions` | No access for `app_read`; deleted together with the account |

**Right to erasure (GDPR, US-04).** Deleting a user deletes all their data by cascade, including the chat and subscriptions.

**Anonymization.** `CALL sp_anonymize_user(id)` is an alternative to deletion when public contributions have to stay: the
workspace, chat, subscriptions and reminders are deleted, the e-mail is replaced with `anonymized-<id>@anonymized.invalid`, the
name with "Anonymous baker", and the password hash with a hash of random bytes (login is impossible). Publications, comments and
reactions stay without personal data.

## 6. Protection of functions

- Functions run with the privileges of the caller (`SECURITY INVOKER`), so they do not extend the privileges of a role.
- The only `SECURITY DEFINER` function is `fn_refresh_bake_statistics` (only the owner can refresh a materialized view). Its
  `search_path` is fixed to `public, pg_temp`, and only `app_write` can execute it; the audit checks that every
  `SECURITY DEFINER` function has a fixed `search_path`.
- `EXECUTE` is revoked from `PUBLIC` for all current and future functions and procedures.
