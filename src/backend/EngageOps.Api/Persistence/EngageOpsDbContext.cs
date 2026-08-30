using EngageOps.Api.Organisations;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Persistence;

public sealed class EngageOpsDbContext(DbContextOptions<EngageOpsDbContext> options)
    : DbContext(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OrganisationConfiguration());
    }
}
