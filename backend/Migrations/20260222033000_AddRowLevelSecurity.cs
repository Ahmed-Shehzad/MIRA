using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiveOrders.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OrderRounds" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Payments" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "RecurringOrderTemplates" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Notifications" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "BotUserConnections" ENABLE ROW LEVEL SECURITY;

                CREATE POLICY "OrderRounds_tenant" ON "OrderRounds"
                    USING ("TenantId" = COALESCE(NULLIF(current_setting('app.tenant_id', true), '')::int, "TenantId"));

                CREATE POLICY "Payments_tenant" ON "Payments"
                    USING ("TenantId" = COALESCE(NULLIF(current_setting('app.tenant_id', true), '')::int, "TenantId"));

                CREATE POLICY "RecurringOrderTemplates_tenant" ON "RecurringOrderTemplates"
                    USING ("TenantId" = COALESCE(NULLIF(current_setting('app.tenant_id', true), '')::int, "TenantId"));

                CREATE POLICY "Notifications_tenant" ON "Notifications"
                    USING ("TenantId" = COALESCE(NULLIF(current_setting('app.tenant_id', true), '')::int, "TenantId"));

                CREATE POLICY "BotUserConnections_tenant" ON "BotUserConnections"
                    USING ("TenantId" = COALESCE(NULLIF(current_setting('app.tenant_id', true), '')::int, "TenantId"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS "OrderRounds_tenant" ON "OrderRounds";
                DROP POLICY IF EXISTS "Payments_tenant" ON "Payments";
                DROP POLICY IF EXISTS "RecurringOrderTemplates_tenant" ON "RecurringOrderTemplates";
                DROP POLICY IF EXISTS "Notifications_tenant" ON "Notifications";
                DROP POLICY IF EXISTS "BotUserConnections_tenant" ON "BotUserConnections";

                ALTER TABLE "OrderRounds" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "Payments" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "RecurringOrderTemplates" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "Notifications" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "BotUserConnections" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
