using System.Net;
using System.Net.Http.Json;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Http;
using EngageOps.Api.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static EngageOps.Api.Tests.Http.ApiResponseAssertions;
using static EngageOps.Api.Tests.Identity.IdentityTestData;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationEndpointTests
{
    [Fact]
    public async Task GetOrganisationsRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = ApiTestClient.Create(factory);

        using var response = await client.GetAsync(
            "/api/organisations",
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOrganisationsReturnsOnlyAuthenticatedUsersMembershipsInNameOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var alpha = Organisation.Create("Alpha Staffing");
        var zeta = Organisation.Create("Zeta Workforce");
        var otherUsersOrganisation = Organisation.Create("Other Tenant");

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
                alpha,
                zeta,
                otherUsersOrganisation,
                OrganisationMembership.Create(alpha.Id, currentUser.Id),
                OrganisationMembership.Create(zeta.Id, currentUser.Id),
                OrganisationMembership.Create(otherUsersOrganisation.Id, otherUser.Id));
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);

        using var response = await client.GetAsync("/api/organisations", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(true, response.Headers.CacheControl?.NoStore);
        var organisations = await response.Content
            .ReadFromJsonAsync<List<OrganisationSummaryResponse>>(cancellationToken);

        Assert.NotNull(organisations);
        Assert.Collection(
            organisations,
            organisation => Assert.Equal((alpha.Id, alpha.Name), (organisation.Id, organisation.Name)),
            organisation => Assert.Equal((zeta.Id, zeta.Name), (organisation.Id, organisation.Name)));
    }

    [Fact]
    public async Task GetOrganisationsReturnsEmptyCollectionWithoutMemberships()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");
        }

        using var client = ApiTestClient.Create(factory);
        await client.SignInAsync("owner@northstar.example", cancellationToken);

        using var response = await client.GetAsync("/api/organisations", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organisations = await response.Content
            .ReadFromJsonAsync<List<OrganisationSummaryResponse>>(cancellationToken);

        Assert.NotNull(organisations);
        Assert.Empty(organisations);
    }

    private sealed record OrganisationSummaryResponse(Guid Id, string Name);
}
