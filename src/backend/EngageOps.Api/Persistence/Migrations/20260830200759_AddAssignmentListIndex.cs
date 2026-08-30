using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngageOps.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentListIndex : Migration
    {
        // EF scaffolding emits inline arrays that the repository's CA1861 analyzer gate rejects.
        private static readonly string[] AssignmentListColumns =
            ["organisation_id", "start_date", "id"];

        private static readonly bool[] AssignmentListSortDirections = [false, true, false];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_assignments_organisation_id_start_date_id",
                table: "assignments",
                columns: AssignmentListColumns,
                descending: AssignmentListSortDirections);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_assignments_organisation_id_start_date_id",
                table: "assignments");
        }
    }
}
