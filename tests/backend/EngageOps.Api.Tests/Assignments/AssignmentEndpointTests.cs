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

public class AssignmentEndpointTests
{
    [Fact]
    public async Task CreateAssignmentRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = ApiTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/organisations/{Guid.CreateVersion7()}/assignments",
            new
            {
                ClientId = Guid.CreateVersion7(),
                WorkerId = Guid.CreateVersion7(),
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = (DateOnly?)null,
            },
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAssignmentValidatesRequestAndPersistsForOrganisationMember()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var clientRecord = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");

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
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);
        var path = $"/api/organisations/{organisation.Id}/assignments";
        var validRequest = new
        {
            ClientId = clientRecord.Id,
            WorkerId = worker.Id,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = (DateOnly?)null,
        };

        using (var missingAntiforgery = await client.PostAsJsonAsync(
            path,
            validRequest,
            cancellationToken))
        {
            await AssertProblemAsync(
                missingAntiforgery,
                HttpStatusCode.BadRequest,
                "The antiforgery token is invalid.",
                cancellationToken);
        }

        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);

        using (var missingFields = await client.PostJsonWithAntiforgeryAsync(
            path,
            new
            {
                ClientId = (Guid?)null,
                WorkerId = Guid.Empty,
                StartDate = (DateOnly?)null,
                EndDate = (DateOnly?)null,
            },
            antiforgeryToken,
            cancellationToken))
        {
            var problem = await AssertProblemAsync(
                missingFields,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);

            Assert.NotNull(problem.Errors);
            Assert.Equal(["Client identifier is required."], problem.Errors["clientId"]);
            Assert.Equal(["Worker identifier is required."], problem.Errors["workerId"]);
            Assert.Equal(
                ["Assignment start date is required."],
                problem.Errors["startDate"]);
        }

        using (var reversedDates = await client.PostJsonWithAntiforgeryAsync(
            path,
            new
            {
                ClientId = clientRecord.Id,
                WorkerId = worker.Id,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 8, 31),
            },
            antiforgeryToken,
            cancellationToken))
        {
            var problem = await AssertProblemAsync(
                reversedDates,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);

            Assert.NotNull(problem.Errors);
            Assert.Equal(
                ["Assignment end date cannot be before its start date."],
                problem.Errors["endDate"]);
        }

        using var response = await client.PostJsonWithAntiforgeryAsync(
            path,
            validRequest,
            antiforgeryToken,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(true, response.Headers.CacheControl?.NoStore);
        var created = await response.Content
            .ReadFromJsonAsync<AssignmentResponse>(cancellationToken);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(organisation.Id, created.OrganisationId);
        Assert.Equal(clientRecord.Id, created.ClientId);
        Assert.Equal(worker.Id, created.WorkerId);
        Assert.Equal(validRequest.StartDate, created.StartDate);
        Assert.Equal(validRequest.EndDate, created.EndDate);
        Assert.Equal("Confirmed", created.Status);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(
            $"/api/organisations/{organisation.Id}/assignments/{created.Id}",
            response.Headers.Location.AbsolutePath);

        using var detailResponse = await client.GetAsync(
            response.Headers.Location.ToString(),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persisted = await verificationContext.Assignments
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal(
            (created.Id, created.OrganisationId, created.ClientId, created.WorkerId),
            (persisted.Id, persisted.OrganisationId, persisted.ClientId, persisted.WorkerId));
        Assert.Equal(created.StartDate, persisted.StartDate);
        Assert.Equal(created.EndDate, persisted.EndDate);
        Assert.Equal(AssignmentStatus.Confirmed, persisted.Status);
    }

    [Fact]
    public async Task CreateAssignmentReturnsSameNotFoundOutsideOrganisationMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var currentUsersOrganisation = Organisation.Create("Northstar Workforce");
        var otherUsersOrganisation = Organisation.Create("Other Tenant");
        var otherClient = Client.Create(otherUsersOrganisation.Id, "Other Client");
        var otherWorker = Worker.Create(otherUsersOrganisation.Id, "Other Worker");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var currentUser = await CreateUserAsync(
                scope.ServiceProvider,
                "owner@northstar.example");
            var otherUser = await CreateUserAsync(
                scope.ServiceProvider,
                "owner@other.example");

            context.AddRange(
                currentUsersOrganisation,
                otherUsersOrganisation,
                otherClient,
                otherWorker,
                OrganisationMembership.Create(currentUsersOrganisation.Id, currentUser.Id),
                OrganisationMembership.Create(otherUsersOrganisation.Id, otherUser.Id));
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);
        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);
        var request = new
        {
            ClientId = otherClient.Id,
            WorkerId = otherWorker.Id,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = (DateOnly?)null,
        };

        foreach (var organisationId in new[]
        {
            otherUsersOrganisation.Id,
            Guid.CreateVersion7(),
            Guid.Empty,
        })
        {
            using var response = await client.PostJsonWithAntiforgeryAsync(
                $"/api/organisations/{organisationId}/assignments",
                request,
                antiforgeryToken,
                cancellationToken);

            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "Organisation was not found.",
                cancellationToken);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        Assert.False(await verificationContext.Assignments.AnyAsync(cancellationToken));
    }

    [Fact]
    public async Task CreateAssignmentValidatesMissingAndCrossTenantRelationships()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Other Tenant");
        var clientRecord = Client.Create(organisation.Id, "Northstar Logistics");
        var otherClient = Client.Create(otherOrganisation.Id, "Other Client");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var otherWorker = Worker.Create(otherOrganisation.Id, "Other Worker");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            context.AddRange(
                organisation,
                otherOrganisation,
                clientRecord,
                otherClient,
                worker,
                otherWorker,
                OrganisationMembership.Create(organisation.Id, user.Id));
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);
        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);
        var path = $"/api/organisations/{organisation.Id}/assignments";
        var cases = new[]
        {
            new RelationshipCase(
                Guid.CreateVersion7(),
                worker.Id,
                "clientId",
                "Client was not found in this organisation."),
            new RelationshipCase(
                otherClient.Id,
                worker.Id,
                "clientId",
                "Client was not found in this organisation."),
            new RelationshipCase(
                clientRecord.Id,
                Guid.CreateVersion7(),
                "workerId",
                "Worker was not found in this organisation."),
            new RelationshipCase(
                clientRecord.Id,
                otherWorker.Id,
                "workerId",
                "Worker was not found in this organisation."),
        };

        foreach (var testCase in cases)
        {
            using var response = await client.PostJsonWithAntiforgeryAsync(
                path,
                new
                {
                    testCase.ClientId,
                    testCase.WorkerId,
                    StartDate = new DateOnly(2026, 9, 1),
                    EndDate = (DateOnly?)null,
                },
                antiforgeryToken,
                cancellationToken);
            var problem = await AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);

            Assert.NotNull(problem.Errors);
            Assert.Equal([testCase.Error], problem.Errors[testCase.ErrorKey]);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        Assert.False(await verificationContext.Assignments.AnyAsync(cancellationToken));
    }

    private sealed record RelationshipCase(
        Guid ClientId,
        Guid WorkerId,
        string ErrorKey,
        string Error);

    private sealed record AssignmentResponse(
        Guid Id,
        Guid OrganisationId,
        Guid ClientId,
        Guid WorkerId,
        DateOnly StartDate,
        DateOnly? EndDate,
        string Status);
}
