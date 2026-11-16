using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Stored procedures for multi-step operations: building a batch plan, extending a stage,
    /// publishing a recipe snapshot, anonymizing a user. SQL: Sql/BusinessProcedures/*.sql.
    /// </summary>
    public partial class BusinessProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("BusinessProcedures", "01_procedures.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS sp_anonymize_user(uuid);
DROP FUNCTION IF EXISTS fn_random_disabled_password_hash();
DROP PROCEDURE IF EXISTS sp_publish_recipe(uuid, uuid);
DROP PROCEDURE IF EXISTS sp_extend_batch_stage(uuid, integer);
DROP PROCEDURE IF EXISTS sp_build_batch_plan(uuid);
");
        }
    }
}
