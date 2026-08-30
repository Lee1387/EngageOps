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
using ClientEntity = EngageOps.Api.Clients.Client;

namespace EngageOps.Api.Tests.Clients;

public class ClientEndpointTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task CreateClientRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = CreateSecureClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/organisations/{Guid.CreateVersion7()}/clients",
            new { Name = "Northstar Logistics" },
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateClientValidatesRequestAndPersistsForOrganisationMember()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            context.AddRange(
                organisation,
                OrganisationMembership.Create(organisation.Id, user.Id));
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = CreateSecureClient(factory);
        await SignInAsync(client, "owner@northstar.example", cancellationToken);
        var path = $"/api/organisations/{organisation.Id}/clients";

        using (var missingAntiforgery = await client.PostAsJsonAsync(
            path,
            new { Name = "Northstar Logistics" },
            cancellationToken))
        {
            await AssertProblemAsync(
                missingAntiforgery,
                HttpStatusCode.BadRequest,
                "The antiforgery token is invalid.",
                cancellationToken);
        }

        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);

        using (var emptyName = await PostJsonWithAntiforgeryAsync(
            client,
            path,
            new { Name = " " },
            antiforgeryToken,
            cancellationToken))
        {
            var problem = await AssertProblemAsync(
                emptyName,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);
            Assert.NotNull(problem.Errors);
            Assert.Equal(["Client name is required."], problem.Errors["name"]);
        }

        using (var oversizedName = await PostJsonWithAntiforgeryAsync(
            client,
            path,
            new { Name = new string('a', ClientEntity.MaxNameLength + 1) },
            antiforgeryToken,
            cancellationToken))
        {
            var problem = await AssertProblemAsync(
                oversizedName,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);
            Assert.NotNull(problem.Errors);
            Assert.Equal(
                [$"Client name must not exceed {ClientEntity.MaxNameLength} characters."],
                problem.Errors["name"]);
        }

        using (var controlCharacter = await PostJsonWithAntiforgeryAsync(
            client,
            path,
            new { Name = "Northstar\0Logistics" },
            antiforgeryToken,
            cancellationToken))
        {
            var problem = await AssertProblemAsync(
                controlCharacter,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);
            Assert.NotNull(problem.Errors);
            Assert.Equal(
                ["Client name must not contain control characters."],
                problem.Errors["name"]);
        }

        using var response = await PostJsonWithAntiforgeryAsync(
            client,
            path,
            new { Name = "  Northstar Logistics  " },
            antiforgeryToken,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(true, response.Headers.CacheControl?.NoStore);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(cancellationToken);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(organisation.Id, created.OrganisationId);
        Assert.Equal("Northstar Logistics", created.Name);

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persisted = await verificationContext.Clients
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal((created.Id, created.OrganisationId, created.Name),
            (persisted.Id, persisted.OrganisationId, persisted.Name));
    }

    [Fact]
    public async Task CreateClientReturnsSameNotFoundOutsideOrganisationMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var currentUsersOrganisation = Organisation.Create("Northstar Workforce");
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
                currentUsersOrganisation,
                otherUsersOrganisation,
                OrganisationMembership.Create(currentUsersOrganisation.Id, currentUser.Id),
                OrganisationMembership.Create(otherUsersOrganisation.Id, otherUser.Id));
            await context.SaveChangesAsync(cancellationToken);
        }

        using var client = CreateSecureClient(factory);
        await SignInAsync(client, "owner@northstar.example", cancellationToken);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);

        foreach (var organisationId in new[]
        {
            otherUsersOrganisation.Id,
            Guid.CreateVersion7(),
            Guid.Empty,
        })
        {
            using var response = await PostJsonWithAntiforgeryAsync(
                client,
                $"/api/organisations/{organisationId}/clients",
                new { Name = "Northstar Logistics" },
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
        Assert.False(await verificationContext.Clients.AnyAsync(cancellationToken));
    }

    [Fact]
    public async Task GetClientsRequiresAuthentication()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = CreateSecureClient(factory);

        using var response = await client.GetAsync(
            $"/api/organisations/{Guid.CreateVersion7()}/clients",
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetClientsReturnsOnlyRequestedOrganisationsClientsInNameOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Other Tenant");
        var alpha = ClientEntity.Create(organisation.Id, "Alpha Distribution");
        var zeta = ClientEntity.Create(organisation.Id, "Zeta Logistics");
        var otherOrganisationsClient = ClientEntity.Create(
            otherOrganisation.Id,
            "Other Client");

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            database.AddRange(
                organisation,
                otherOrganisation,
                alpha,
                zeta,
                otherOrganisationsClient,
                OrganisationMembership.Create(organisation.Id, user.Id));
            await database.SaveChangesAsync(cancellationToken);
        }

        using var client = CreateSecureClient(factory);
        await SignInAsync(client, "owner@northstar.example", cancellationToken);

        using var response = await client.GetAsync(
            $"/api/organisations/{organisation.Id}/clients",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(true, response.Headers.CacheControl?.NoStore);
        var page = await response.Content
            .ReadFromJsonAsync<ClientPageResponse>(cancellationToken);

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(2, page.TotalCount);
        Assert.Collection(
            page.Items,
            client => Assert.Equal(
                (alpha.Id, alpha.OrganisationId, alpha.Name),
                (client.Id, client.OrganisationId, client.Name)),
            client => Assert.Equal(
                (zeta.Id, zeta.OrganisationId, zeta.Name),
                (client.Id, client.OrganisationId, client.Name)));
    }

    [Fact]
    public async Task GetClientsReturnsEmptyCollectionAndSameNotFoundOutsideMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var emptyOrganisation = Organisation.Create("Northstar Workforce");
        var otherUsersOrganisation = Organisation.Create("Other Tenant");

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
            var currentUser = await CreateUserAsync(
                scope.ServiceProvider,
                "owner@northstar.example");
            var otherUser = await CreateUserAsync(
                scope.ServiceProvider,
                "owner@other.example");

            database.AddRange(
                emptyOrganisation,
                otherUsersOrganisation,
                OrganisationMembership.Create(emptyOrganisation.Id, currentUser.Id),
                OrganisationMembership.Create(otherUsersOrganisation.Id, otherUser.Id));
            await database.SaveChangesAsync(cancellationToken);
        }

        using var client = CreateSecureClient(factory);
        await SignInAsync(client, "owner@northstar.example", cancellationToken);

        using (var emptyResponse = await client.GetAsync(
            $"/api/organisations/{emptyOrganisation.Id}/clients",
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
            var page = await emptyResponse.Content
                .ReadFromJsonAsync<ClientPageResponse>(cancellationToken);

            Assert.NotNull(page);
            Assert.Empty(page.Items);
            Assert.Equal(0, page.TotalCount);
        }

        foreach (var organisationId in new[]
        {
            otherUsersOrganisation.Id,
            Guid.CreateVersion7(),
            Guid.Empty,
        })
        {
            using var response = await client.GetAsync(
                $"/api/organisations/{organisationId}/clients",
                cancellationToken);

            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "Organisation was not found.",
                cancellationToken);
        }
    }

    [Fact]
    public async Task GetClientsPaginatesAndValidatesPaginationBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var alpha = ClientEntity.Create(organisation.Id, "Alpha Distribution");
        var beta = ClientEntity.Create(organisation.Id, "Beta Logistics");
        var gamma = ClientEntity.Create(organisation.Id, "Gamma Consulting");

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, "owner@northstar.example");

            database.AddRange(
                organisation,
                alpha,
                beta,
                gamma,
                OrganisationMembership.Create(organisation.Id, user.Id));
            await database.SaveChangesAsync(cancellationToken);
        }

        using var client = CreateSecureClient(factory);
        await SignInAsync(client, "owner@northstar.example", cancellationToken);
        var path = $"/api/organisations/{organisation.Id}/clients";

        using (var response = await client.GetAsync(
            $"{path}?page=2&pageSize=2",
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content
                .ReadFromJsonAsync<ClientPageResponse>(cancellationToken);

            Assert.NotNull(page);
            Assert.Equal(2, page.Page);
            Assert.Equal(2, page.PageSize);
            Assert.Equal(3, page.TotalCount);
            var item = Assert.Single(page.Items);
            Assert.Equal(
                (gamma.Id, gamma.OrganisationId, gamma.Name),
                (item.Id, item.OrganisationId, item.Name));
        }

        using (var maximumPageSize = await client.GetAsync(
            $"{path}?pageSize=100",
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, maximumPageSize.StatusCode);
            var page = await maximumPageSize.Content
                .ReadFromJsonAsync<ClientPageResponse>(cancellationToken);

            Assert.NotNull(page);
            Assert.Equal(100, page.PageSize);
            Assert.Equal(3, page.Items.Count);
        }

        using (var distantPage = await client.GetAsync(
            $"{path}?page={int.MaxValue}&pageSize=1",
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, distantPage.StatusCode);
            var page = await distantPage.Content
                .ReadFromJsonAsync<ClientPageResponse>(cancellationToken);

            Assert.NotNull(page);
            Assert.Equal(int.MaxValue, page.Page);
            Assert.Empty(page.Items);
            Assert.Equal(3, page.TotalCount);
        }

        foreach (var (query, errorKey) in new[]
        {
            ("?page=0", "page"),
            ("?pageSize=0", "pageSize"),
            ("?pageSize=101", "pageSize"),
            ($"?page={int.MaxValue}&pageSize=100", "page"),
        })
        {
            using var response = await client.GetAsync($"{path}{query}", cancellationToken);
            var problem = await AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);

            Assert.NotNull(problem.Errors);
            Assert.Contains(errorKey, problem.Errors);
        }
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
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);
        using var response = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email = email, Password },
            antiforgeryToken,
            cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/auth/csrf", cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>(cancellationToken);

        Assert.NotNull(token);

        return token.Token;
    }

    private static async Task<HttpResponseMessage> PostJsonWithAntiforgeryAsync<TRequest>(
        HttpClient client,
        string path,
        TRequest body,
        string antiforgeryToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add(AuthenticationEndpoints.AntiforgeryHeaderName, antiforgeryToken);

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<ProblemResponse> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);

        return problem;
    }

    private sealed record AntiforgeryTokenResponse(string Token);

    private sealed record ClientResponse(Guid Id, Guid OrganisationId, string Name);

    private sealed record ClientPageResponse(
        IReadOnlyList<ClientResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);

    private sealed record ProblemResponse(
        int Status,
        string Title,
        Dictionary<string, string[]>? Errors = null);
}
