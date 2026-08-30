using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationProvisionerTests
{
    [Fact]
    public async Task ProvisionAsyncCreatesOrganisationAndInitialMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var user = new ApplicationUser { UserName = "owner@northstar.example" };

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);

            var provisioner = scope.ServiceProvider.GetRequiredService<OrganisationProvisioner>();
            var organisation = await provisioner.ProvisionAsync(
                user.Id,
                "  Northstar Workforce  ",
                cancellationToken);

            Assert.NotNull(organisation);
            Assert.Equal("Northstar Workforce", organisation.Name);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persistedOrganisation = await verificationContext.Organisations
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        var persistedMembership = await verificationContext.OrganisationMemberships
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal(persistedOrganisation.Id, persistedMembership.OrganisationId);
        Assert.Equal(user.Id, persistedMembership.UserId);
    }

    [Fact]
    public async Task ProvisionAsyncReturnsNullAndCreatesNothingWhenUserDoesNotExist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        await using var context = new EngageOpsDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
        var provisioner = new OrganisationProvisioner(context);

        var organisation = await provisioner.ProvisionAsync(
            Guid.CreateVersion7(),
            "Northstar Workforce",
            cancellationToken);

        Assert.Null(organisation);
        Assert.Empty(await context.Organisations.ToListAsync(cancellationToken));
        Assert.Empty(await context.OrganisationMemberships.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task ProvisionAsyncUsesOrganisationNameValidation()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;
        await using var context = new EngageOpsDbContext(options);
        var provisioner = new OrganisationProvisioner(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provisioner.ProvisionAsync(
                Guid.CreateVersion7(),
                " ",
                TestContext.Current.CancellationToken));

        Assert.Equal("name", exception.ParamName);
    }
}
