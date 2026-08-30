using System.Net;
using System.Net.Http.Json;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationEndpointTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task GetOrganisationsRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = CreateSecureClient(factory);

        using var response = await client.GetAsync(
            "/api/organisations",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.Unauthorized, problem.Status);
        Assert.Equal("Authentication is required.", problem.Title);
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

        using var client = CreateSecureClient(factory);
        await SignInAsync(client, "owner@northstar.example", cancellationToken);

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

        using var client = CreateSecureClient(factory);
        await SignInAsync(client, "owner@northstar.example", cancellationToken);

        using var response = await client.GetAsync("/api/organisations", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organisations = await response.Content
            .ReadFromJsonAsync<List<OrganisationSummaryResponse>>(cancellationToken);

        Assert.NotNull(organisations);
        Assert.Empty(organisations);
    }

    private static HttpClient CreateSecureClient(EngageOpsApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

    private static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        string email)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(user, Password);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));

        return user;
    }

    private static async Task SignInAsync(
        HttpClient client,
        string email,
        CancellationToken cancellationToken)
    {
        using var antiforgeryResponse = await client.GetAsync("/api/auth/csrf", cancellationToken);
        antiforgeryResponse.EnsureSuccessStatusCode();
        var antiforgery = await antiforgeryResponse.Content
            .ReadFromJsonAsync<AntiforgeryTokenResponse>(cancellationToken);
        Assert.NotNull(antiforgery);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { Email = email, Password }),
        };
        request.Headers.Add(AuthenticationEndpoints.AntiforgeryHeaderName, antiforgery.Token);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private sealed record AntiforgeryTokenResponse(string Token);

    private sealed record OrganisationSummaryResponse(Guid Id, string Name);

    private sealed record ProblemResponse(int Status, string Title);
}
