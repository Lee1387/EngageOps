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

public class AssignmentCancellerTests
{
    [Fact]
    public async Task CancelAsyncPersistsCancellationAndReportsRepeatedCancellation()
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
            new DateOnly(2026, 9, 1));

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

            var canceller = scope.ServiceProvider.GetRequiredService<AssignmentCanceller>();
            var result = await canceller.CancelAsync(
                user.Id,
                organisation.Id,
                assignment.Id,
                cancellationToken);

            Assert.IsType<AssignmentCancellationResult.Cancelled>(result);
        }

        using (var repeatedScope = factory.Services.CreateScope())
        {
            var canceller = repeatedScope.ServiceProvider.GetRequiredService<AssignmentCanceller>();
            var result = await canceller.CancelAsync(
                user.Id,
                organisation.Id,
                assignment.Id,
                cancellationToken);

            Assert.IsType<AssignmentCancellationResult.AlreadyCancelled>(result);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persistedAssignment = await verificationContext.Assignments
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal(AssignmentStatus.Cancelled, persistedAssignment.Status);
    }

    [Fact]
    public async Task CancelAsyncHidesInaccessibleOrganisationsAndAssignments()
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
            new DateOnly(2026, 9, 1));
        context.AddRange(
            organisation,
            otherOrganisation,
            user,
            OrganisationMembership.Create(organisation.Id, user.Id),
            otherClient,
            otherWorker,
            otherAssignment);
        await context.SaveChangesAsync(cancellationToken);

        var canceller = new AssignmentCanceller(
            context,
            new OrganisationMembershipChecker(context));
        var inaccessibleOrganisationResult = await canceller.CancelAsync(
            user.Id,
            otherOrganisation.Id,
            otherAssignment.Id,
            cancellationToken);
        var missingOrganisationResult = await canceller.CancelAsync(
            user.Id,
            Guid.CreateVersion7(),
            otherAssignment.Id,
            cancellationToken);
        var missingAssignmentResult = await canceller.CancelAsync(
            user.Id,
            organisation.Id,
            Guid.CreateVersion7(),
            cancellationToken);
        var crossTenantAssignmentResult = await canceller.CancelAsync(
            user.Id,
            organisation.Id,
            otherAssignment.Id,
            cancellationToken);

        Assert.IsType<AssignmentCancellationResult.OrganisationNotFound>(
            inaccessibleOrganisationResult);
        Assert.IsType<AssignmentCancellationResult.OrganisationNotFound>(
            missingOrganisationResult);
        Assert.IsType<AssignmentCancellationResult.AssignmentNotFound>(missingAssignmentResult);
        Assert.IsType<AssignmentCancellationResult.AssignmentNotFound>(
            crossTenantAssignmentResult);
        Assert.Equal(AssignmentStatus.Confirmed, otherAssignment.Status);
    }
}
