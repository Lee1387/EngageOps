using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngageOps.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "assignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Existing assignments were committed records before lifecycle status was introduced.
            migrationBuilder.Sql("""
                UPDATE assignments
                SET status = 'Confirmed'
                """);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "assignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_assignments_status",
                table: "assignments",
                sql: "status IN ('Confirmed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_assignments_status",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "assignments");
        }
    }
}
