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

public class AssignmentDetailQueryTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsAssignmentWithRelatedNamesForOrganisationMember()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var client = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var assignment = Assignment.Create(
            organisation.Id,
            client.Id,
            worker.Id,
            new DateOnly(2026, 9, 1),
            new DateOnly(2027, 3, 31));
        Assert.True(assignment.TryCancel());

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            context.AddRange(
                organisation,
                user,
                OrganisationMembership.Create(organisation.Id, user.Id),
                client,
                worker,
                assignment);
            await context.SaveChangesAsync(cancellationToken);
        }

        using var queryScope = factory.Services.CreateScope();
        var query = queryScope.ServiceProvider.GetRequiredService<AssignmentDetailQuery>();
        var result = await query.ExecuteAsync(
            user.Id,
            organisation.Id,
            assignment.Id,
            cancellationToken);

        var found = Assert.IsType<AssignmentDetailResult.Found>(result);
        Assert.Equal(assignment.Id, found.Assignment.Id);
        Assert.Equal(assignment.OrganisationId, found.Assignment.OrganisationId);
        Assert.Equal(assignment.ClientId, found.Assignment.ClientId);
        Assert.Equal(client.Name, found.Assignment.ClientName);
        Assert.Equal(assignment.WorkerId, found.Assignment.WorkerId);
        Assert.Equal(worker.Name, found.Assignment.WorkerName);
        Assert.Equal(assignment.StartDate, found.Assignment.StartDate);
        Assert.Equal(assignment.EndDate, found.Assignment.EndDate);
        Assert.Equal(AssignmentStatus.Cancelled, found.Assignment.Status);
    }

    [Fact]
    public async Task ExecuteAsyncHidesMissingAndCrossTenantAssignments()
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
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var otherClient = Client.Create(otherOrganisation.Id, "Summit Distribution");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Taylor Reed");
        var assignment = Assignment.Create(
            organisation.Id,
            client.Id,
            worker.Id,
            new DateOnly(2026, 9, 1));
        var otherAssignment = Assignment.Create(
            otherOrganisation.Id,
            otherClient.Id,
            otherWorker.Id,
            new DateOnly(2026, 10, 1));
        context.AddRange(
            organisation,
            otherOrganisation,
            user,
            OrganisationMembership.Create(organisation.Id, user.Id),
            client,
            worker,
            otherClient,
            otherWorker,
            assignment,
            otherAssignment);
        await context.SaveChangesAsync(cancellationToken);

        var query = new AssignmentDetailQuery(
            context,
            new OrganisationMembershipChecker(context));
        var missingResult = await query.ExecuteAsync(
            user.Id,
            organisation.Id,
            Guid.CreateVersion7(),
            cancellationToken);
        var crossTenantResult = await query.ExecuteAsync(
            user.Id,
            organisation.Id,
            otherAssignment.Id,
            cancellationToken);

        Assert.IsType<AssignmentDetailResult.AssignmentNotFound>(missingResult);
        Assert.IsType<AssignmentDetailResult.AssignmentNotFound>(crossTenantResult);
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
        var otherAssignment = Assignment.Create(
            otherOrganisation.Id,
            otherClient.Id,
            otherWorker.Id,
            new DateOnly(2026, 10, 1));
        context.AddRange(
            organisation,
            otherOrganisation,
            user,
            OrganisationMembership.Create(organisation.Id, user.Id),
            otherClient,
            otherWorker,
            otherAssignment);
        await context.SaveChangesAsync(cancellationToken);

        var query = new AssignmentDetailQuery(
            context,
            new OrganisationMembershipChecker(context));
        var inaccessibleResult = await query.ExecuteAsync(
            user.Id,
            otherOrganisation.Id,
            otherAssignment.Id,
            cancellationToken);
        var missingResult = await query.ExecuteAsync(
            user.Id,
            Guid.CreateVersion7(),
            otherAssignment.Id,
            cancellationToken);

        Assert.IsType<AssignmentDetailResult.OrganisationNotFound>(inaccessibleResult);
        Assert.IsType<AssignmentDetailResult.OrganisationNotFound>(missingResult);
    }
}
