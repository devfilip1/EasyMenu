using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_token = table.Column<string>(type: "character(43)", fixedLength: true, maxLength: 43, nullable: false),
                    pickup_code = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_cents = table.Column<long>(type: "bigint", nullable: false),
                    payment_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    picked_up_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_orders_establishments_establishment_id",
                        column: x => x.establishment_id,
                        principalTable: "establishments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit_price_cents = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    station = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subtotal_cents = table.Column<long>(type: "bigint", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    station = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_parts", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_parts_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_parts_order_id_station",
                table: "order_parts",
                columns: new[] { "order_id", "station" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_establishment_id_pickup_code",
                table: "orders",
                columns: new[] { "establishment_id", "pickup_code" },
                unique: true,
                filter: "status <> 'picked_up'");

            migrationBuilder.CreateIndex(
                name: "ix_orders_establishment_id_status_created_at",
                table: "orders",
                columns: new[] { "establishment_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_orders_public_token",
                table: "orders",
                column: "public_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "order_parts");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
