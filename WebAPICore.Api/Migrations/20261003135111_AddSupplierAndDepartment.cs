using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAPICore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierAndDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "總務課");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "purchase_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "總務課");

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_Code",
                table: "suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_suppliers_SupplierId",
                table: "purchase_orders",
                column: "SupplierId",
                principalTable: "suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_suppliers_SupplierId",
                table: "purchase_orders");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_SupplierId",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "purchase_orders");
        }
    }
}
