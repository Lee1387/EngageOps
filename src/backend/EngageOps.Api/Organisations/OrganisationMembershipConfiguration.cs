using EngageOps.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngageOps.Api.Organisations;

internal sealed class OrganisationMembershipConfiguration
    : IEntityTypeConfiguration<OrganisationMembership>
{
    public void Configure(EntityTypeBuilder<OrganisationMembership> builder)
    {
        builder.ToTable("organisation_memberships");

        builder.HasKey(membership => new { membership.OrganisationId, membership.UserId });

        builder.Property(membership => membership.OrganisationId)
            .HasColumnName("organisation_id")
            .ValueGeneratedNever();

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(membership => membership.UserId);
    }
}
