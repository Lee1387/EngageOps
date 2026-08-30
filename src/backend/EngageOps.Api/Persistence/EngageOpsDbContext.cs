using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Persistence;

public sealed class EngageOpsDbContext(DbContextOptions<EngageOpsDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<OrganisationMembership> OrganisationMemberships => Set<OrganisationMembership>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new OrganisationConfiguration());
        builder.ApplyConfiguration(new OrganisationMembershipConfiguration());
    }
}
