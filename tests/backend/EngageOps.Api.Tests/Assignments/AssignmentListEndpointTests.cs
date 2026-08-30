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

public class AssignmentListEndpointTests
{
    [Fact]
    public async Task GetAssignmentsRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = ApiTestClient.Create(factory);

        using var response = await client.GetAsync(
            $"/api/organisations/{Guid.CreateVersion7()}/assignments",
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAssignmentsReturnsTenantAssignmentsAndEmptyPages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var emptyOrganisation = Organisation.Create("Northstar Consulting");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var clientRecord = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var otherClient = Client.Create(otherOrganisation.Id, "Summit Distribution");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Taylor Reed");
        var earlierAssignment = Assignment.Create(
            organisation.Id,
            clientRecord.Id,
            worker.Id,
            new DateOnly(2026, 9, 1));
        var laterAssignment = Assignment.Create(
            organisation.Id,
            clientRecord.Id,
            worker.Id,
            new DateOnly(2026, 10, 1),
            new DateOnly(2027, 3, 31));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            context.AddRange(
                organisation,
                emptyOrganisation,
                otherOrganisation,
                clientRecord,
                worker,
                otherClient,
                otherWorker,
                OrganisationMembership.Create(organisation.Id, user.Id),
                OrganisationMembership.Create(emptyOrganisation.Id, user.Id),
                earlierAssignment,
                laterAssignment,
                Assignment.Create(
                    otherOrganisation.Id,
                    otherClient.Id,
                    otherWorker.Id,
                    new DateOnly(2026, 11, 1)));
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);

        using (var response = await client.GetAsync(
            $"/api/organisations/{organisation.Id}/assignments",
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(true, response.Headers.CacheControl?.NoStore);
            var page = await response.Content.ReadFromJsonAsync<AssignmentPageResponse>(
                cancellationToken);

            Assert.NotNull(page);
            Assert.Equal(1, page.Page);
            Assert.Equal(50, page.PageSize);
            Assert.Equal(2, page.TotalCount);
            Assert.Collection(
                page.Items,
                item => AssertItem(
                    item,
                    laterAssignment,
                    clientRecord.Name,
                    worker.Name),
                item => AssertItem(
                    item,
                    earlierAssignment,
                    clientRecord.Name,
                    worker.Name));
        }

        using var emptyResponse = await client.GetAsync(
            $"/api/organisations/{emptyOrganisation.Id}/assignments",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        var emptyPage = await emptyResponse.Content.ReadFromJsonAsync<AssignmentPageResponse>(
            cancellationToken);

        Assert.NotNull(emptyPage);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(0, emptyPage.TotalCount);
    }

    [Fact]
    public async Task GetAssignmentsReturnsSameNotFoundOutsideOrganisationMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var otherClient = Client.Create(otherOrganisation.Id, "Summit Distribution");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Taylor Reed");

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
                Assignment.Create(
                    otherOrganisation.Id,
                    otherClient.Id,
                    otherWorker.Id,
                    new DateOnly(2026, 9, 1)));
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);

        foreach (var organisationId in new[]
        {
            otherOrganisation.Id,
            Guid.CreateVersion7(),
            Guid.Empty,
        })
        {
            using var response = await client.GetAsync(
                $"/api/organisations/{organisationId}/assignments",
                cancellationToken);

            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "Organisation was not found.",
                cancellationToken);
        }
    }

    [Fact]
    public async Task GetAssignmentsPaginatesAndValidatesPaginationBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var clientRecord = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var assignments = new[]
        {
            Assignment.Create(
                organisation.Id,
                clientRecord.Id,
                worker.Id,
                new DateOnly(2026, 11, 1)),
            Assignment.Create(
                organisation.Id,
                clientRecord.Id,
                worker.Id,
                new DateOnly(2026, 10, 1)),
            Assignment.Create(
                organisation.Id,
                clientRecord.Id,
                worker.Id,
                new DateOnly(2026, 9, 1)),
        };

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            context.AddRange(
                organisation,
                clientRecord,
                worker,
                OrganisationMembership.Create(organisation.Id, user.Id));
            context.Assignments.AddRange(assignments);
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);
        var path = $"/api/organisations/{organisation.Id}/assignments";

        using (var response = await client.GetAsync($"{path}?page=2&pageSize=2", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<AssignmentPageResponse>(
                cancellationToken);

            Assert.NotNull(page);
            Assert.Equal(2, page.Page);
            Assert.Equal(2, page.PageSize);
            Assert.Equal(3, page.TotalCount);
            Assert.Equal([assignments[2].Id], page.Items.Select(item => item.Id));
        }

        using (var invalid = await client.GetAsync($"{path}?page=0&pageSize=101", cancellationToken))
        {
            var problem = await AssertProblemAsync(
                invalid,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);

            Assert.NotNull(problem.Errors);
            Assert.Equal(["Page must be at least 1."], problem.Errors["page"]);
            Assert.Equal(
                ["Page size must be between 1 and 100."],
                problem.Errors["pageSize"]);
        }

        using var excessivePage = await client.GetAsync(
            $"{path}?page={int.MaxValue}&pageSize=2",
            cancellationToken);
        var excessivePageProblem = await AssertProblemAsync(
            excessivePage,
            HttpStatusCode.BadRequest,
            "One or more validation errors occurred.",
            cancellationToken);

        Assert.NotNull(excessivePageProblem.Errors);
        Assert.Equal(["Page is too large."], excessivePageProblem.Errors["page"]);
    }

    private static void AssertItem(
        AssignmentListItemResponse item,
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

    private sealed record AssignmentListItemResponse(
        Guid Id,
        Guid OrganisationId,
        Guid ClientId,
        string ClientName,
        Guid WorkerId,
        string WorkerName,
        DateOnly StartDate,
        DateOnly? EndDate);

    private sealed record AssignmentPageResponse(
        IReadOnlyList<AssignmentListItemResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
