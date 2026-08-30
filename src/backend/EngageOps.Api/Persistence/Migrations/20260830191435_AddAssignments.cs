using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngageOps.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignments : Migration
    {
        // EF scaffolding emits inline arrays that the repository's CA1861 analyzer gate rejects.
        private static readonly string[] OrganisationAndIdColumns = ["organisation_id", "id"];
        private static readonly string[] OrganisationAndClientIdColumns = ["organisation_id", "client_id"];
        private static readonly string[] OrganisationAndWorkerIdColumns = ["organisation_id", "worker_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_workers_organisation_id_id",
                table: "workers",
                columns: OrganisationAndIdColumns);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_clients_organisation_id_id",
                table: "clients",
                columns: OrganisationAndIdColumns);

            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.id);
                    table.CheckConstraint("CK_assignments_date_range", "end_date IS NULL OR end_date >= start_date");
                    table.ForeignKey(
                        name: "FK_assignments_clients_organisation_id_client_id",
                        columns: x => new { x.organisation_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: OrganisationAndIdColumns,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignments_organisations_organisation_id",
                        column: x => x.organisation_id,
                        principalTable: "organisations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignments_workers_organisation_id_worker_id",
                        columns: x => new { x.organisation_id, x.worker_id },
                        principalTable: "workers",
                        principalColumns: OrganisationAndIdColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_organisation_id_client_id",
                table: "assignments",
                columns: OrganisationAndClientIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_assignments_organisation_id_worker_id",
                table: "assignments",
                columns: OrganisationAndWorkerIdColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_workers_organisation_id_id",
                table: "workers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_clients_organisation_id_id",
                table: "clients");
        }
    }
}
