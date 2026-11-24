using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A feeding names the flour and the water it used, and the price of a starter is what it was fed.
    /// SQL: Sql/StarterFeedingIngredients/*.sql.
    /// </summary>
    public partial class StarterFeedingIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "flour_ingredient_id",
                table: "starter_feedings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "water_ingredient_id",
                table: "starter_feedings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_starter_feedings_flour_ingredient_id",
                table: "starter_feedings",
                column: "flour_ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_starter_feedings_water_ingredient_id",
                table: "starter_feedings",
                column: "water_ingredient_id");

            migrationBuilder.AddForeignKey(
                name: "fk_starter_feedings_ingredients_flour_ingredient_id",
                table: "starter_feedings",
                column: "flour_ingredient_id",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_starter_feedings_ingredients_water_ingredient_id",
                table: "starter_feedings",
                column: "water_ingredient_id",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.SqlScripts("StarterFeedingIngredients", "01_feeding_ingredients.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("StarterFeedingIngredients", "02_down_restore_feeding.sql");

            migrationBuilder.DropForeignKey(
                name: "fk_starter_feedings_ingredients_flour_ingredient_id",
                table: "starter_feedings");

            migrationBuilder.DropForeignKey(
                name: "fk_starter_feedings_ingredients_water_ingredient_id",
                table: "starter_feedings");

            migrationBuilder.DropIndex(
                name: "ix_starter_feedings_flour_ingredient_id",
                table: "starter_feedings");

            migrationBuilder.DropIndex(
                name: "ix_starter_feedings_water_ingredient_id",
                table: "starter_feedings");

            migrationBuilder.DropColumn(
                name: "flour_ingredient_id",
                table: "starter_feedings");

            migrationBuilder.DropColumn(
                name: "water_ingredient_id",
                table: "starter_feedings");
        }
    }
}
