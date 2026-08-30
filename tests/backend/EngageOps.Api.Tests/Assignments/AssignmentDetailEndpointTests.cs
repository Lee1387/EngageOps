using System.Net;
using System.Net.Http.Json;
using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Http;
using EngageOps.Api.Tests.Persistence;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static EngageOps.Api.Tests.Http.ApiResponseAssertions;
using static EngageOps.Api.Tests.Identity.IdentityTestData;

namespace EngageOps.Api.Tests.Assignments;

public class AssignmentDetailEndpointTests
{
    [Fact]
    public async Task GetAssignmentRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = ApiTestClient.Create(factory);

        using var response = await client.GetAsync(
            $"/api/organisations/{Guid.CreateVersion7()}/assignments/{Guid.CreateVersion7()}",
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAssignmentReturnsTenantAssignment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var clientRecord = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var assignment = Assignment.Create(
            organisation.Id,
            clientRecord.Id,
            worker.Id,
            new DateOnly(2026, 9, 1),
            new DateOnly(2027, 3, 31));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            context.AddRange(
                organisation,
                clientRecord,
                worker,
                OrganisationMembership.Create(organisation.Id, user.Id),
                assignment);
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);

        using var response = await client.GetAsync(
            $"/api/organisations/{organisation.Id}/assignments/{assignment.Id}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(true, response.Headers.CacheControl?.NoStore);
        var returned = await response.Content.ReadFromJsonAsync<AssignmentResponse>(
            cancellationToken);

        Assert.NotNull(returned);
        Assert.Equal(assignment.Id, returned.Id);
        Assert.Equal(organisation.Id, returned.OrganisationId);
        Assert.Equal(clientRecord.Id, returned.ClientId);
        Assert.Equal(clientRecord.Name, returned.ClientName);
        Assert.Equal(worker.Id, returned.WorkerId);
        Assert.Equal(worker.Name, returned.WorkerName);
        Assert.Equal(assignment.StartDate, returned.StartDate);
        Assert.Equal(assignment.EndDate, returned.EndDate);
    }

    [Fact]
    public async Task GetAssignmentDoesNotExposeOtherTenantsOrMissingResources()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var otherClient = Client.Create(otherOrganisation.Id, "Summit Distribution");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Taylor Reed");
        var otherAssignment = Assignment.Create(
            otherOrganisation.Id,
            otherClient.Id,
            otherWorker.Id,
            new DateOnly(2026, 9, 1));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            context.AddRange(
                organisation,
                otherOrganisation,
                otherClient,
                otherWorker,
                OrganisationMembership.Create(organisation.Id, user.Id),
                otherAssignment);
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);

        foreach (var assignmentId in new[]
        {
            Guid.CreateVersion7(),
            otherAssignment.Id,
            Guid.Empty,
        })
        {
            using var response = await client.GetAsync(
                $"/api/organisations/{organisation.Id}/assignments/{assignmentId}",
                cancellationToken);

            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "Assignment was not found.",
                cancellationToken);
        }

        foreach (var organisationId in new[]
        {
            otherOrganisation.Id,
            Guid.CreateVersion7(),
            Guid.Empty,
        })
        {
            using var response = await client.GetAsync(
                $"/api/organisations/{organisationId}/assignments/{otherAssignment.Id}",
                cancellationToken);

            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "Organisation was not found.",
                cancellationToken);
        }
    }

    private sealed record AssignmentResponse(
        Guid Id,
        Guid OrganisationId,
        Guid ClientId,
        string ClientName,
        Guid WorkerId,
        string WorkerName,
        DateOnly StartDate,
        DateOnly? EndDate);
}
