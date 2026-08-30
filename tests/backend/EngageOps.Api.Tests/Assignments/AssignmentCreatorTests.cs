using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Assignments;

public class AssignmentCreatorTests
{
    [Fact]
    public async Task CreateAsyncPersistsAssignmentForOrganisationMember()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var client = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var startDate = new DateOnly(2026, 9, 1);
        var endDate = new DateOnly(2027, 2, 28);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            context.AddRange(
                organisation,
                user,
                OrganisationMembership.Create(organisation.Id, user.Id),
                client,
                worker);
            await context.SaveChangesAsync(cancellationToken);

            var creator = scope.ServiceProvider.GetRequiredService<AssignmentCreator>();
            var result = await creator.CreateAsync(
                user.Id,
                organisation.Id,
                client.Id,
                worker.Id,
                startDate,
                endDate,
                cancellationToken);

            var created = Assert.IsType<AssignmentCreationResult.Created>(result);
            Assert.Equal(organisation.Id, created.Assignment.OrganisationId);
            Assert.Equal(client.Id, created.Assignment.ClientId);
            Assert.Equal(worker.Id, created.Assignment.WorkerId);
            Assert.Equal(startDate, created.Assignment.StartDate);
            Assert.Equal(endDate, created.Assignment.EndDate);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persistedAssignment = await verificationContext.Assignments
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal(organisation.Id, persistedAssignment.OrganisationId);
        Assert.Equal(client.Id, persistedAssignment.ClientId);
        Assert.Equal(worker.Id, persistedAssignment.WorkerId);
        Assert.Equal(startDate, persistedAssignment.StartDate);
        Assert.Equal(endDate, persistedAssignment.EndDate);
    }

    [Fact]
    public async Task CreateAsyncHidesMissingAndInaccessibleOrganisations()
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

        var creator = new AssignmentCreator(
            context,
            new OrganisationMembershipChecker(context));
        var inaccessibleResult = await creator.CreateAsync(
            user.Id,
            otherOrganisation.Id,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 1),
            endDate: null,
            cancellationToken);
        var missingResult = await creator.CreateAsync(
            user.Id,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 1),
            endDate: null,
            cancellationToken);

        Assert.IsType<AssignmentCreationResult.OrganisationNotFound>(inaccessibleResult);
        Assert.IsType<AssignmentCreationResult.OrganisationNotFound>(missingResult);
        Assert.Empty(await context.Assignments.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task CreateAsyncRejectsMissingAndCrossTenantRelationships()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        await using var context = new EngageOpsDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);

        var organisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var client = Client.Create(organisation.Id, "Northstar Logistics");
        var otherClient = Client.Create(otherOrganisation.Id, "Summit Distribution");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Taylor Reed");
        context.AddRange(
            organisation,
            otherOrganisation,
            user,
            OrganisationMembership.Create(organisation.Id, user.Id),
            client,
            otherClient,
            worker,
            otherWorker);
        await context.SaveChangesAsync(cancellationToken);

        var creator = new AssignmentCreator(
            context,
            new OrganisationMembershipChecker(context));
        var missingClientResult = await creator.CreateAsync(
            user.Id,
            organisation.Id,
            Guid.CreateVersion7(),
            worker.Id,
            new DateOnly(2026, 9, 1),
            endDate: null,
            cancellationToken);
        var crossTenantClientResult = await creator.CreateAsync(
            user.Id,
            organisation.Id,
            otherClient.Id,
            worker.Id,
            new DateOnly(2026, 9, 1),
            endDate: null,
            cancellationToken);
        var missingWorkerResult = await creator.CreateAsync(
            user.Id,
            organisation.Id,
            client.Id,
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 1),
            endDate: null,
            cancellationToken);
        var crossTenantWorkerResult = await creator.CreateAsync(
            user.Id,
            organisation.Id,
            client.Id,
            otherWorker.Id,
            new DateOnly(2026, 9, 1),
            endDate: null,
            cancellationToken);

        Assert.IsType<AssignmentCreationResult.ClientNotFound>(missingClientResult);
        Assert.IsType<AssignmentCreationResult.ClientNotFound>(crossTenantClientResult);
        Assert.IsType<AssignmentCreationResult.WorkerNotFound>(missingWorkerResult);
        Assert.IsType<AssignmentCreationResult.WorkerNotFound>(crossTenantWorkerResult);
        Assert.Empty(await context.Assignments.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task CreateAsyncUsesAssignmentDateValidation()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;
        await using var context = new EngageOpsDbContext(options);
        var creator = new AssignmentCreator(
            context,
            new OrganisationMembershipChecker(context));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            creator.CreateAsync(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 8, 31),
                TestContext.Current.CancellationToken));

        Assert.Equal("endDate", exception.ParamName);
    }
}
