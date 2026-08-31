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

public class AssignmentCancellationEndpointTests
{
    [Fact]
    public async Task CancelAssignmentRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = ApiTestClient.Create(factory);

        using var response = await client.PostAsync(
            $"/api/organisations/{Guid.CreateVersion7()}/assignments/{Guid.CreateVersion7()}/cancel",
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CancelAssignmentRequiresAntiforgeryAndIsIdempotent()
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
            new DateOnly(2026, 9, 1));

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
        var path =
            $"/api/organisations/{organisation.Id}/assignments/{assignment.Id}/cancel";

        using (var missingAntiforgery = await client.PostAsync(path, cancellationToken))
        {
            await AssertProblemAsync(
                missingAntiforgery,
                HttpStatusCode.BadRequest,
                "The antiforgery token is invalid.",
                cancellationToken);
        }

        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);

        using (var cancelled = await client.PostWithAntiforgeryAsync(
            path,
            antiforgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
            Assert.Equal(true, cancelled.Headers.CacheControl?.NoStore);
        }

        using (var repeated = await client.PostWithAntiforgeryAsync(
            path,
            antiforgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        }

        using var detailResponse = await client.GetAsync(
            $"/api/organisations/{organisation.Id}/assignments/{assignment.Id}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var returned = await detailResponse.Content
            .ReadFromJsonAsync<AssignmentStatusResponse>(cancellationToken);

        Assert.NotNull(returned);
        Assert.Equal("Cancelled", returned.Status);
    }

    [Fact]
    public async Task CancelAssignmentHidesInaccessibleOrganisationsAndAssignments()
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
        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);

        foreach (var organisationId in new[]
        {
            otherOrganisation.Id,
            Guid.CreateVersion7(),
            Guid.Empty,
        })
        {
            using var response = await client.PostWithAntiforgeryAsync(
                $"/api/organisations/{organisationId}/assignments/{otherAssignment.Id}/cancel",
                antiforgeryToken,
                cancellationToken);

            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "Organisation was not found.",
                cancellationToken);
        }

        foreach (var assignmentId in new[]
        {
            otherAssignment.Id,
            Guid.CreateVersion7(),
            Guid.Empty,
        })
        {
            using var response = await client.PostWithAntiforgeryAsync(
                $"/api/organisations/{organisation.Id}/assignments/{assignmentId}/cancel",
                antiforgeryToken,
                cancellationToken);

            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "Assignment was not found.",
                cancellationToken);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persistedAssignment = await verificationContext.Assignments
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal(AssignmentStatus.Confirmed, persistedAssignment.Status);
    }

    private sealed record AssignmentStatusResponse(string Status);
}
