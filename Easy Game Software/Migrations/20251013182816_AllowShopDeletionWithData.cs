using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Easy_Games_Software.Migrations
{
    /// <inheritdoc />
    public partial class AllowShopDeletionWithData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Shops_ShopId",
                table: "Transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Shops_ShopId",
                table: "Transactions",
                column: "ShopId",
                principalTable: "Shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Shops_ShopId",
                table: "Transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Shops_ShopId",
                table: "Transactions",
                column: "ShopId",
                principalTable: "Shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
