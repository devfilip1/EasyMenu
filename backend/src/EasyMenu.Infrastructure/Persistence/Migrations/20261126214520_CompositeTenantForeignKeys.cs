using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompositeTenantForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_menu_items_categories_category_id",
                table: "menu_items");

            migrationBuilder.DropIndex(
                name: "ix_menu_items_category_id",
                table: "menu_items");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_categories_establishment_id_id",
                table: "categories",
                columns: new[] { "establishment_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_menu_items_categories_establishment_id_category_id",
                table: "menu_items",
                columns: new[] { "establishment_id", "category_id" },
                principalTable: "categories",
                principalColumns: new[] { "establishment_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_menu_items_categories_establishment_id_category_id",
                table: "menu_items");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_categories_establishment_id_id",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "ix_menu_items_category_id",
                table: "menu_items",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "fk_menu_items_categories_category_id",
                table: "menu_items",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
