using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiveOrders.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWsiRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "WsiUploads" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "WsiJobs" ENABLE ROW LEVEL SECURITY;

                CREATE POLICY "WsiUploads_tenant" ON "WsiUploads"
                    USING ("TenantId" = COALESCE(NULLIF(current_setting('app.tenant_id', true), '')::int, "TenantId"));

                CREATE POLICY "WsiJobs_tenant" ON "WsiJobs"
                    USING ("TenantId" = COALESCE(NULLIF(current_setting('app.tenant_id', true), '')::int, "TenantId"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS "WsiUploads_tenant" ON "WsiUploads";
                DROP POLICY IF EXISTS "WsiJobs_tenant" ON "WsiJobs";

                ALTER TABLE "WsiUploads" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "WsiJobs" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
