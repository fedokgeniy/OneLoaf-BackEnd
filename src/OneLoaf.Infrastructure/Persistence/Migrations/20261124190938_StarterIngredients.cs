using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A starter becomes a kind of ingredient (starters.ingredient_id, usable share), ripened starter reaches the stock at
    /// its peak (stock_movements.available_at and two movement types), ingredients and recipes get a photo.
    /// SQL: Sql/StarterIngredients/*.sql.
    /// </summary>
    public partial class StarterIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_movement_type",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_quantity_sign",
                table: "stock_movements");

            migrationBuilder.AddColumn<DateTime>(
                name: "available_at",
                table: "stock_movements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ingredient_id",
                table: "starters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "usable_share",
                table: "starters",
                type: "numeric(4,3)",
                precision: 4,
                scale: 3,
                nullable: false,
                defaultValue: 0.667m);

            migrationBuilder.AddColumn<string>(
                name: "photo_content_type",
                table: "recipes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_key",
                table: "recipes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_content_type",
                table: "ingredients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_key",
                table: "ingredients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_movement_type",
                table: "stock_movements",
                sql: "movement_type IN ('purchase', 'batch_consumption', 'batch_return', 'starter_feeding', 'adjustment', 'starter_ripened', 'starter_refed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_quantity_sign",
                table: "stock_movements",
                sql: "(movement_type IN ('purchase', 'batch_return', 'starter_ripened') AND quantity > 0) OR (movement_type IN ('batch_consumption', 'starter_feeding', 'starter_refed') AND quantity < 0) OR movement_type = 'adjustment'");

            // Functions and triggers; the script also gives every existing starter its ingredient.
            migrationBuilder.SqlScripts("StarterIngredients", "01_starter_ingredients.sql");

            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                table: "starters",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_starters_ingredient_id",
                table: "starters",
                column: "ingredient_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_starters_usable_share",
                table: "starters",
                sql: "usable_share > 0 AND usable_share <= 1");

            migrationBuilder.AddForeignKey(
                name: "fk_starters_ingredients_ingredient_id",
                table: "starters",
                column: "ingredient_id",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_starters_ingredient_after_delete ON starters;
DROP TRIGGER IF EXISTS trg_ingredients_starter_name_sync ON ingredients;
DROP TRIGGER IF EXISTS trg_starters_name_sync ON starters;
DROP TRIGGER IF EXISTS trg_starters_ingredient_before ON starters;
DROP FUNCTION IF EXISTS trg_starters_ingredient_after_delete();
DROP FUNCTION IF EXISTS trg_ingredients_starter_name_sync();
DROP FUNCTION IF EXISTS trg_starters_name_sync();
DROP FUNCTION IF EXISTS trg_starters_ingredient_before();
DELETE FROM stock_movements WHERE movement_type IN ('starter_ripened', 'starter_refed');
");
            // The stock functions, the feeding trigger function and v_low_stock as the IngredientStock migration created them.
            migrationBuilder.SqlScripts("StarterIngredients", "02_down_restore_stock.sql");

            migrationBuilder.DropForeignKey(
                name: "fk_starters_ingredients_ingredient_id",
                table: "starters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_movement_type",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_quantity_sign",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ux_starters_ingredient_id",
                table: "starters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_starters_usable_share",
                table: "starters");

            migrationBuilder.DropColumn(
                name: "available_at",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "ingredient_id",
                table: "starters");

            migrationBuilder.DropColumn(
                name: "usable_share",
                table: "starters");

            migrationBuilder.DropColumn(
                name: "photo_content_type",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "photo_key",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "photo_content_type",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "photo_key",
                table: "ingredients");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_movement_type",
                table: "stock_movements",
                sql: "movement_type IN ('purchase', 'batch_consumption', 'batch_return', 'starter_feeding', 'adjustment')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_quantity_sign",
                table: "stock_movements",
                sql: "(movement_type IN ('purchase', 'batch_return') AND quantity > 0) OR (movement_type IN ('batch_consumption', 'starter_feeding') AND quantity < 0) OR movement_type = 'adjustment'");
        }
    }
}
