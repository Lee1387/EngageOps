using EngageOps.Api.Clients;
using EngageOps.Api.Organisations;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngageOps.Api.Assignments;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable(
            "assignments",
            table => table.HasCheckConstraint(
                "CK_assignments_date_range",
                "end_date IS NULL OR end_date >= start_date"));

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.OrganisationId)
            .HasColumnName("organisation_id")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.ClientId)
            .HasColumnName("client_id")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.WorkerId)
            .HasColumnName("worker_id")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.StartDate)
            .HasColumnName("start_date");

        builder.Property(assignment => assignment.EndDate)
            .HasColumnName("end_date");

        // Assignments are operational records, so deleting any related record must be an explicit decision.
        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(assignment => assignment.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.OrganisationId, assignment.ClientId })
            .HasPrincipalKey(client => new { client.OrganisationId, client.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Worker>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.OrganisationId, assignment.WorkerId })
            .HasPrincipalKey(worker => new { worker.OrganisationId, worker.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => new
        {
            assignment.OrganisationId,
            assignment.StartDate,
            assignment.Id,
        })
            .IsDescending(false, true, false);
    }
}
