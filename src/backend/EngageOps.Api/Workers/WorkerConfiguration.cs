using EngageOps.Api.Organisations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngageOps.Api.Workers;

internal sealed class WorkerConfiguration : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> builder)
    {
        builder.ToTable("workers");

        builder.HasKey(worker => worker.Id);

        builder.Property(worker => worker.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(worker => worker.OrganisationId)
            .HasColumnName("organisation_id")
            .ValueGeneratedNever();

        builder.Property(worker => worker.Name)
            .HasColumnName("name")
            .HasMaxLength(Worker.MaxNameLength)
            .IsRequired();

        // Organisation deletion must be explicit; cascading worker records would hide operational data loss.
        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(worker => worker.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(worker => worker.OrganisationId);
    }
}
