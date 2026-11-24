using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Stock of ingredients: the stock_movements ledger, the stock switch and reorder level of an ingredient,
    /// the ingredients a starter is fed with and the ingredient scale of a batch. SQL: Sql/IngredientStock/*.sql.
    /// </summary>
    public partial class IngredientStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "flour_ingredient_id",
                table: "starters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "water_ingredient_id",
                table: "starters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reorder_level",
                table: "ingredients",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "track_stock",
                table: "ingredients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ingredient_scale",
                table: "batches",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    feeding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pack_count = table.Column<int>(type: "integer", nullable: true),
                    pack_size = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_adjustment_note", "movement_type <> 'adjustment' OR length(btrim(note)) > 0");
                    table.CheckConstraint("ck_stock_movements_movement_type", "movement_type IN ('purchase', 'batch_consumption', 'batch_return', 'starter_feeding', 'adjustment')");
                    table.CheckConstraint("ck_stock_movements_purchase_fields", "movement_type <> 'purchase' OR (pack_count > 0 AND pack_size > 0 AND total_cost >= 0 AND currency IS NOT NULL)");
                    table.CheckConstraint("ck_stock_movements_quantity_not_zero", "quantity <> 0");
                    table.CheckConstraint("ck_stock_movements_quantity_sign", "(movement_type IN ('purchase', 'batch_return') AND quantity > 0) OR (movement_type IN ('batch_consumption', 'starter_feeding') AND quantity < 0) OR movement_type = 'adjustment'");
                    table.ForeignKey(
                        name: "fk_stock_movements_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_movements_currencies_currency",
                        column: x => x.currency,
                        principalTable: "currencies",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_stock_movements_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stock_movements_starter_feedings_feeding_id",
                        column: x => x.feeding_id,
                        principalTable: "starter_feedings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "E3. Stock ledger of ingredients: purchases, batch consumption and returns, starter feedings, corrections. The stock is the sum of quantity.");

            migrationBuilder.CreateIndex(
                name: "ix_starters_flour_ingredient_id",
                table: "starters",
                column: "flour_ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_starters_water_ingredient_id",
                table: "starters",
                column: "water_ingredient_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ingredients_reorder_level",
                table: "ingredients",
                sql: "reorder_level >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_batches_ingredient_scale",
                table: "batches",
                sql: "ingredient_scale > 0 AND ingredient_scale <= 1");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_batch_id",
                table: "stock_movements",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_currency",
                table: "stock_movements",
                column: "currency");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_feeding_id",
                table: "stock_movements",
                column: "feeding_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_ingredient_id_occurred_on",
                table: "stock_movements",
                columns: new[] { "ingredient_id", "occurred_on" });

            migrationBuilder.AddForeignKey(
                name: "fk_starters_ingredients_flour_ingredient_id",
                table: "starters",
                column: "flour_ingredient_id",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_starters_ingredients_water_ingredient_id",
                table: "starters",
                column: "water_ingredient_id",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.SqlScripts("IngredientStock", "01_stock.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS v_low_stock;
DROP TRIGGER IF EXISTS trg_starter_feedings_stock ON starter_feedings;
DROP FUNCTION IF EXISTS trg_starter_feedings_stock();
DROP TRIGGER IF EXISTS trg_starters_stock_links_validate ON starters;
DROP FUNCTION IF EXISTS trg_starters_stock_links_validate();
DROP TRIGGER IF EXISTS trg_stock_movements_validate ON stock_movements;
DROP FUNCTION IF EXISTS trg_stock_movements_validate();
DROP PROCEDURE IF EXISTS sp_return_batch_stock(uuid);
DROP PROCEDURE IF EXISTS sp_apply_batch_stock(uuid);
DROP FUNCTION IF EXISTS fn_batch_stock_check(uuid);
DROP FUNCTION IF EXISTS fn_batch_ingredient_needs(uuid);
DROP FUNCTION IF EXISTS fn_stock_balance(uuid);
");
            // The batch plan without the ingredient scale, as created by the BusinessProcedures migration.
            migrationBuilder.SqlScripts("BusinessProcedures", "01_procedures.sql");

            migrationBuilder.DropForeignKey(
                name: "fk_starters_ingredients_flour_ingredient_id",
                table: "starters");

            migrationBuilder.DropForeignKey(
                name: "fk_starters_ingredients_water_ingredient_id",
                table: "starters");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_starters_flour_ingredient_id",
                table: "starters");

            migrationBuilder.DropIndex(
                name: "ix_starters_water_ingredient_id",
                table: "starters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ingredients_reorder_level",
                table: "ingredients");

            migrationBuilder.DropCheckConstraint(
                name: "ck_batches_ingredient_scale",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "flour_ingredient_id",
                table: "starters");

            migrationBuilder.DropColumn(
                name: "water_ingredient_id",
                table: "starters");

            migrationBuilder.DropColumn(
                name: "reorder_level",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "track_stock",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "ingredient_scale",
                table: "batches");
        }
    }
}
