using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngageOps.Api.Organisations;

internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("organisations");

        builder.HasKey(organisation => organisation.Id);

        builder.Property(organisation => organisation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(organisation => organisation.Name)
            .HasColumnName("name")
            .HasMaxLength(Organisation.MaxNameLength)
            .IsRequired();
    }
}
