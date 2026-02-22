using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiveOrders.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIdentityWithCognitoUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CognitoUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Groups = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CognitoUsername",
                table: "Users",
                column: "CognitoUsername");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.Sql(@"
                INSERT INTO ""Users"" (""Id"", ""CognitoUsername"", ""Email"", ""Company"", ""TenantId"", ""Groups"", ""CreatedAt"", ""UpdatedAt"")
                SELECT
                    COALESCE(""CognitoSub"", ""Id""),
                    ""UserName"",
                    COALESCE(""Email"", ""UserName""),
                    ""Company"",
                    ""TenantId"",
                    COALESCE((
                        SELECT string_agg(
                            CASE r.""Name""
                                WHEN 'Admin' THEN 'Admins'
                                WHEN 'Manager' THEN 'Managers'
                                WHEN 'User' THEN 'Users'
                                ELSE r.""Name""
                            END, ',')
                        FROM ""AspNetUserRoles"" ur
                        JOIN ""AspNetRoles"" r ON ur.""RoleId"" = r.""Id""
                        WHERE ur.""UserId"" = ""AspNetUsers"".""Id""
                    ), 'Users'),
                    COALESCE(""LockoutEnd"", NOW() AT TIME ZONE 'UTC')::timestamptz,
                    NOW() AT TIME ZONE 'UTC'
                FROM ""AspNetUsers""
            ");

            migrationBuilder.Sql(@"
                UPDATE ""OrderRounds"" SET ""CreatedByUserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""OrderRounds"".""CreatedByUserId"");
                UPDATE ""OrderItems"" SET ""UserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""OrderItems"".""UserId"");
                UPDATE ""Payments"" SET ""UserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""Payments"".""UserId"");
                UPDATE ""RecurringOrderTemplates"" SET ""CreatedByUserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""RecurringOrderTemplates"".""CreatedByUserId"");
                UPDATE ""Notifications"" SET ""UserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""Notifications"".""UserId"");
                UPDATE ""PushSubscriptions"" SET ""UserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""PushSubscriptions"".""UserId"");
                UPDATE ""BotUserConnections"" SET ""UserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""BotUserConnections"".""UserId"");
                UPDATE ""BotLinkCode"" SET ""UserId"" = (SELECT COALESCE(""CognitoSub"", ""Id"") FROM ""AspNetUsers"" WHERE ""Id"" = ""BotLinkCode"".""UserId"");
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Tenants_TenantId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderRounds_AspNetUsers_CreatedByUserId",
                table: "OrderRounds");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_AspNetUsers_UserId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_AspNetUsers_UserId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringOrderTemplates_AspNetUsers_CreatedByUserId",
                table: "RecurringOrderTemplates");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderRounds_Users_CreatedByUserId",
                table: "OrderRounds",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Users_UserId",
                table: "OrderItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringOrderTemplates_Users_CreatedByUserId",
                table: "RecurringOrderTemplates",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderRounds_Users_CreatedByUserId",
                table: "OrderRounds");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Users_UserId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringOrderTemplates_Users_CreatedByUserId",
                table: "RecurringOrderTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "Users");

            throw new NotSupportedException("Down migration not supported - reverting to Identity requires manual restoration.");
        }
    }
}
