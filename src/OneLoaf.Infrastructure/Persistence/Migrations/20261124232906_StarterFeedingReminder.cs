using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The reminder of a batch item with a starter tells the baker to feed it, the time to peak before kneading.
    /// SQL: Sql/StarterFeedingReminder/*.sql.
    /// </summary>
    public partial class StarterFeedingReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("StarterFeedingReminder", "01_feeding_reminder.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("StarterFeedingReminder", "02_down_restore_plan.sql");
        }
    }
}
