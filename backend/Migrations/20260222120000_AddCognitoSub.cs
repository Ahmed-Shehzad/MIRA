using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiveOrders.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitoSub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CognitoSub",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CognitoSub",
                table: "AspNetUsers",
                column: "CognitoSub",
                filter: "\"CognitoSub\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CognitoSub",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CognitoSub",
                table: "AspNetUsers");
        }
    }
}
