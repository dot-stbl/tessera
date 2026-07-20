using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tessera.Modules.Preferences.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tessera");

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: static table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    tenant_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                schema: "tessera",
                constraints: static table => table.PrimaryKey("PK_user_preferences", static x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_user_preferences_deleted_at",
                table: "user_preferences",
                column: "deleted_at",
                schema: "tessera");

            migrationBuilder.CreateIndex(
                name: "ix_user_preferences_tenant_id",
                table: "user_preferences",
                column: "tenant_id",
                schema: "tessera");

            migrationBuilder.CreateIndex(
                name: "ux_user_preferences_user_key",
                table: "user_preferences",
                columns: ["user_id", "key"],
                schema: "tessera",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_preferences",
                schema: "tessera");
        }
    }
}
