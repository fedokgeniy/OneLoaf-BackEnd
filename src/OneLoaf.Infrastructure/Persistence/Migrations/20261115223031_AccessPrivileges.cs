using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Privileges of the roles app_admin (owner), app_write (services) and app_read (reporting, no personal data),
    /// and default privileges for objects created by later migrations. The roles themselves are created by
    /// db/security/bootstrap.sql. SQL: Sql/AccessPrivileges/*.sql.
    /// </summary>
    public partial class AccessPrivileges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("AccessPrivileges", "01_privileges.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE EXECUTE ON FUNCTIONS FROM app_write;
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM app_write;
ALTER DEFAULT PRIVILEGES GRANT EXECUTE ON FUNCTIONS TO PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM app_write, app_read;
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM app_write, app_read;
REVOKE EXECUTE ON ALL PROCEDURES IN SCHEMA public FROM app_write;
REVOKE USAGE ON SCHEMA public FROM app_write, app_read;
");
        }
    }
}
