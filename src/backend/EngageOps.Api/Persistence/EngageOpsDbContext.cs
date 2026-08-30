using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Workers;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Persistence;

public sealed class EngageOpsDbContext(DbContextOptions<EngageOpsDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<OrganisationMembership> OrganisationMemberships => Set<OrganisationMembership>();

    public DbSet<Worker> Workers => Set<Worker>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new AssignmentConfiguration());
        builder.ApplyConfiguration(new ClientConfiguration());
        builder.ApplyConfiguration(new OrganisationConfiguration());
        builder.ApplyConfiguration(new OrganisationMembershipConfiguration());
        builder.ApplyConfiguration(new WorkerConfiguration());
    }
}
