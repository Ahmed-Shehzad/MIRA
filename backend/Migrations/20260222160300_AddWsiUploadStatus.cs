using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiveOrders.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWsiUploadStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "WsiUploads",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Ready");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "WsiUploads");
        }
    }
}
