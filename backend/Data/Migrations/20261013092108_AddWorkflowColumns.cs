using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsDistributionCenter.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "disposition",
                table: "return_orders",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "processed_at",
                table: "return_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "processed_by",
                table: "return_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "return_orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "qty",
                table: "return_orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "restock_location_id",
                table: "return_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "inventories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_product_id",
                table: "return_orders",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_restock_location_id",
                table: "return_orders",
                column: "restock_location_id");

            migrationBuilder.AddForeignKey(
                name: "FK_return_orders_locations_restock_location_id",
                table: "return_orders",
                column: "restock_location_id",
                principalTable: "locations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_return_orders_products_product_id",
                table: "return_orders",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_return_orders_locations_restock_location_id",
                table: "return_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_return_orders_products_product_id",
                table: "return_orders");

            migrationBuilder.DropIndex(
                name: "IX_return_orders_product_id",
                table: "return_orders");

            migrationBuilder.DropIndex(
                name: "IX_return_orders_restock_location_id",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "processed_at",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "processed_by",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "qty",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "restock_location_id",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "version",
                table: "inventories");

            migrationBuilder.AlterColumn<int>(
                name: "disposition",
                table: "return_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
