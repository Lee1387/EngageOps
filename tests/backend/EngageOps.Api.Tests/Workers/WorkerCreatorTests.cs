using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Workers;

public class WorkerCreatorTests
{
    [Fact]
    public async Task CreateAsyncPersistsWorkerForOrganisationMember()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            context.AddRange(
                organisation,
                user,
                OrganisationMembership.Create(organisation.Id, user.Id));
            await context.SaveChangesAsync(cancellationToken);

            var creator = scope.ServiceProvider.GetRequiredService<WorkerCreator>();
            var worker = await creator.CreateAsync(
                user.Id,
                organisation.Id,
                "  Alex Morgan  ",
                cancellationToken);

            Assert.NotNull(worker);
            Assert.Equal("Alex Morgan", worker.Name);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persistedWorker = await verificationContext.Workers
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal(organisation.Id, persistedWorker.OrganisationId);
        Assert.Equal("Alex Morgan", persistedWorker.Name);
    }

    [Fact]
    public async Task CreateAsyncReturnsNullAndCreatesNothingWithoutOrganisationMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        await using var context = new EngageOpsDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);

        var usersOrganisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var otherUser = new ApplicationUser { UserName = "owner@summit.example" };
        context.AddRange(
            usersOrganisation,
            otherOrganisation,
            user,
            otherUser,
            OrganisationMembership.Create(usersOrganisation.Id, user.Id),
            OrganisationMembership.Create(otherOrganisation.Id, otherUser.Id));
        await context.SaveChangesAsync(cancellationToken);

        var creator = new WorkerCreator(
            context,
            new OrganisationMembershipChecker(context));
        var otherOrganisationsWorker = await creator.CreateAsync(
            user.Id,
            otherOrganisation.Id,
            "Taylor Reed",
            cancellationToken);
        var missingOrganisationsWorker = await creator.CreateAsync(
            user.Id,
            Guid.CreateVersion7(),
            "Jordan Blake",
            cancellationToken);

        Assert.Null(otherOrganisationsWorker);
        Assert.Null(missingOrganisationsWorker);
        Assert.Empty(await context.Workers.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task CreateAsyncUsesWorkerNameValidation()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;
        await using var context = new EngageOpsDbContext(options);
        var creator = new WorkerCreator(
            context,
            new OrganisationMembershipChecker(context));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            creator.CreateAsync(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                " ",
                TestContext.Current.CancellationToken));

        Assert.Equal("name", exception.ParamName);
    }
}
