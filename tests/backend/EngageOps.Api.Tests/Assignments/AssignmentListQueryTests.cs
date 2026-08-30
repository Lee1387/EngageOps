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

public class AssignmentListQueryTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsTenantAssignmentsWithRelatedNamesInStableOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var client = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var otherClient = Client.Create(otherOrganisation.Id, "Summit Distribution");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Taylor Reed");
        var earlierAssignment = Assignment.Create(
            organisation.Id,
            client.Id,
            worker.Id,
            new DateOnly(2026, 9, 1));
        var laterAssignment = Assignment.Create(
            organisation.Id,
            client.Id,
            worker.Id,
            new DateOnly(2026, 10, 1),
            new DateOnly(2027, 3, 31));
        var otherAssignment = Assignment.Create(
            otherOrganisation.Id,
            otherClient.Id,
            otherWorker.Id,
            new DateOnly(2026, 11, 1));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            context.AddRange(
                organisation,
                otherOrganisation,
                user,
                OrganisationMembership.Create(organisation.Id, user.Id),
                client,
                worker,
                otherClient,
                otherWorker,
                earlierAssignment,
                laterAssignment,
                otherAssignment);
            await context.SaveChangesAsync(cancellationToken);
        }

        using var queryScope = factory.Services.CreateScope();
        var query = queryScope.ServiceProvider.GetRequiredService<AssignmentListQuery>();
        var result = await query.ExecuteAsync(
            user.Id,
            organisation.Id,
            offset: 0,
            pageSize: 50,
            cancellationToken);

        var found = Assert.IsType<AssignmentListResult.Found>(result);
        Assert.Equal(2, found.TotalCount);
        Assert.Collection(
            found.Items,
            item => AssertItem(item, laterAssignment, client.Name, worker.Name),
            item => AssertItem(item, earlierAssignment, client.Name, worker.Name));
    }

    [Fact]
    public async Task ExecuteAsyncHidesMissingAndInaccessibleOrganisations()
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
        var otherClient = Client.Create(otherOrganisation.Id, "Summit Distribution");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Taylor Reed");
        context.AddRange(
            organisation,
            otherOrganisation,
            user,
            OrganisationMembership.Create(organisation.Id, user.Id),
            otherClient,
            otherWorker,
            Assignment.Create(
                otherOrganisation.Id,
                otherClient.Id,
                otherWorker.Id,
                new DateOnly(2026, 9, 1)));
        await context.SaveChangesAsync(cancellationToken);

        var query = new AssignmentListQuery(
            context,
            new OrganisationMembershipChecker(context));
        var inaccessibleResult = await query.ExecuteAsync(
            user.Id,
            otherOrganisation.Id,
            offset: 0,
            pageSize: 50,
            cancellationToken);
        var missingResult = await query.ExecuteAsync(
            user.Id,
            Guid.CreateVersion7(),
            offset: 0,
            pageSize: 50,
            cancellationToken);

        Assert.IsType<AssignmentListResult.OrganisationNotFound>(inaccessibleResult);
        Assert.IsType<AssignmentListResult.OrganisationNotFound>(missingResult);
    }

    [Fact]
    public async Task ExecuteAsyncReturnsTheRequestedPageAndTotalCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        await using var context = new EngageOpsDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);

        var organisation = Organisation.Create("Northstar Workforce");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var client = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var assignments = new[]
        {
            Assignment.Create(
                organisation.Id,
                client.Id,
                worker.Id,
                new DateOnly(2026, 12, 1)),
            Assignment.Create(
                organisation.Id,
                client.Id,
                worker.Id,
                new DateOnly(2026, 11, 1)),
            Assignment.Create(
                organisation.Id,
                client.Id,
                worker.Id,
                new DateOnly(2026, 10, 1)),
            Assignment.Create(
                organisation.Id,
                client.Id,
                worker.Id,
                new DateOnly(2026, 9, 1)),
        };
        context.AddRange(
            organisation,
            user,
            OrganisationMembership.Create(organisation.Id, user.Id),
            client,
            worker);
        context.Assignments.AddRange(assignments);
        await context.SaveChangesAsync(cancellationToken);

        var query = new AssignmentListQuery(
            context,
            new OrganisationMembershipChecker(context));
        var result = await query.ExecuteAsync(
            user.Id,
            organisation.Id,
            offset: 1,
            pageSize: 2,
            cancellationToken);
        var emptyResult = await query.ExecuteAsync(
            user.Id,
            organisation.Id,
            offset: assignments.Length,
            pageSize: 2,
            cancellationToken);

        var found = Assert.IsType<AssignmentListResult.Found>(result);
        Assert.Equal(assignments.Length, found.TotalCount);
        Assert.Equal([assignments[1].Id, assignments[2].Id], found.Items.Select(item => item.Id));

        var emptyPage = Assert.IsType<AssignmentListResult.Found>(emptyResult);
        Assert.Equal(assignments.Length, emptyPage.TotalCount);
        Assert.Empty(emptyPage.Items);
    }

    private static void AssertItem(
        AssignmentListItem item,
        Assignment assignment,
        string clientName,
        string workerName)
    {
        Assert.Equal(assignment.Id, item.Id);
        Assert.Equal(assignment.OrganisationId, item.OrganisationId);
        Assert.Equal(assignment.ClientId, item.ClientId);
        Assert.Equal(clientName, item.ClientName);
        Assert.Equal(assignment.WorkerId, item.WorkerId);
        Assert.Equal(workerName, item.WorkerName);
        Assert.Equal(assignment.StartDate, item.StartDate);
        Assert.Equal(assignment.EndDate, item.EndDate);
    }
}
