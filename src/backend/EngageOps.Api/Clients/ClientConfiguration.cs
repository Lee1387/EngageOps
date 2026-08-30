using EngageOps.Api.Organisations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngageOps.Api.Clients;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(client => client.Id);

        // This composite key lets tenant-owned dependants enforce that the client belongs to their organisation.
        builder.HasAlternateKey(client => new { client.OrganisationId, client.Id });

        builder.Property(client => client.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(client => client.OrganisationId)
            .HasColumnName("organisation_id")
            .ValueGeneratedNever();

        builder.Property(client => client.Name)
            .HasColumnName("name")
            .HasMaxLength(Client.MaxNameLength)
            .IsRequired();

        // Organisation deletion must be explicit; cascading operational client records would hide data loss.
        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(client => client.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(client => client.OrganisationId);
    }
}
