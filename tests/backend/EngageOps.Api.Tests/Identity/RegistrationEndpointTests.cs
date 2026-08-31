using System.Net;
using System.Net.Http.Json;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Http;
using EngageOps.Api.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static EngageOps.Api.Tests.Http.ApiResponseAssertions;

namespace EngageOps.Api.Tests.Identity;

public class RegistrationEndpointTests
{
    [Fact]
    public async Task RegisterRequiresAntiforgeryAndValidatesInputBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new EngageOpsApiFactory();
        using var client = ApiTestClient.Create(factory);

        using (var missingAntiforgery = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                Email = "owner@northstar.example",
                Password = IdentityTestData.ValidPassword,
                OrganisationName = "Northstar Workforce",
            },
            cancellationToken))
        {
            await AssertProblemAsync(
                missingAntiforgery,
                HttpStatusCode.BadRequest,
                "The antiforgery token is invalid.",
                cancellationToken);
        }

        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);
        using var invalid = await client.PostJsonWithAntiforgeryAsync(
            "/api/auth/register",
            new
            {
                Email = " ",
                Password = "",
                OrganisationName = "Northstar\0Workforce",
            },
            antiforgeryToken,
            cancellationToken);
        var problem = await AssertProblemAsync(
            invalid,
            HttpStatusCode.BadRequest,
            "One or more validation errors occurred.",
            cancellationToken);

        Assert.NotNull(problem.Errors);
        Assert.Equal(["Email is required."], problem.Errors["email"]);
        Assert.Equal(["Password is required."], problem.Errors["password"]);
        Assert.Equal(
            ["Organisation name must not contain control characters."],
            problem.Errors["organisationName"]);
    }

    [Fact]
    public async Task RegisterCreatesFoundingAccountAndAuthenticatedSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        using (var migrationScope = factory.Services.CreateScope())
        {
            var context = migrationScope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);
        using var response = await client.PostJsonWithAntiforgeryAsync(
            "/api/auth/register",
            new
            {
                Email = "  owner@northstar.example  ",
                Password = IdentityTestData.ValidPassword,
                OrganisationName = "  Northstar Workforce  ",
            },
            antiforgeryToken,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(true, response.Headers.CacheControl?.NoStore);
        var authenticationCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("EngageOps.Authentication=", StringComparison.Ordinal));
        Assert.Contains("httponly", authenticationCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", authenticationCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", authenticationCookie, StringComparison.OrdinalIgnoreCase);

        var registration = await response.Content
            .ReadFromJsonAsync<RegistrationResponse>(cancellationToken);
        Assert.NotNull(registration);
        Assert.NotEqual(Guid.Empty, registration.UserId);
        Assert.Equal("owner@northstar.example", registration.Email);
        Assert.NotEqual(Guid.Empty, registration.OrganisationId);
        Assert.Equal("Northstar Workforce", registration.OrganisationName);

        using var sessionResponse = await client.GetAsync("/api/auth/session", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(
            cancellationToken);
        Assert.NotNull(session);
        Assert.Equal((registration.UserId, registration.Email), (session.UserId, session.Email));

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var membership = await verificationContext.OrganisationMemberships
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        Assert.Equal(
            (registration.OrganisationId, registration.UserId),
            (membership.OrganisationId, membership.UserId));
    }

    [Fact]
    public async Task RegisterReturnsIdentityValidationAndDuplicateErrorsWithoutPartialData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        using (var migrationScope = factory.Services.CreateScope())
        {
            var context = migrationScope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
        }

        using var client = ApiTestClient.Create(factory);
        var antiforgeryToken = await client.GetAntiforgeryTokenAsync(cancellationToken);
        using (var weakPassword = await client.PostJsonWithAntiforgeryAsync(
            "/api/auth/register",
            new
            {
                Email = "owner@northstar.example",
                Password = "password",
                OrganisationName = "Northstar Workforce",
            },
            antiforgeryToken,
            cancellationToken))
        {
            var problem = await AssertProblemAsync(
                weakPassword,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);
            Assert.NotNull(problem.Errors);
            Assert.Contains("password", problem.Errors);
            Assert.DoesNotContain("email", problem.Errors);
        }

        using (var created = await client.PostJsonWithAntiforgeryAsync(
            "/api/auth/register",
            new
            {
                Email = "owner@northstar.example",
                Password = IdentityTestData.ValidPassword,
                OrganisationName = "Northstar Workforce",
            },
            antiforgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var authenticatedAntiforgeryToken = await client.GetAntiforgeryTokenAsync(
            cancellationToken);
        using (var duplicate = await client.PostJsonWithAntiforgeryAsync(
            "/api/auth/register",
            new
            {
                Email = "OWNER@NORTHSTAR.EXAMPLE",
                Password = IdentityTestData.ValidPassword,
                OrganisationName = "Duplicate Organisation",
            },
            authenticatedAntiforgeryToken,
            cancellationToken))
        {
            var problem = await AssertProblemAsync(
                duplicate,
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                cancellationToken);
            Assert.NotNull(problem.Errors);
            Assert.Equal(
                ["Registration could not be completed with the supplied email address."],
                problem.Errors["email"]);
            Assert.DoesNotContain("password", problem.Errors);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        Assert.Equal(1, await verificationContext.Users.CountAsync(cancellationToken));
        Assert.Equal(1, await verificationContext.Organisations.CountAsync(cancellationToken));
        Assert.Equal(
            1,
            await verificationContext.OrganisationMemberships.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task ConcurrentDuplicateRegistrationReturnsValidationWithoutPartialData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var registrationBarrier = new ConcurrentRegistrationBarrier();
        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        // Appending this validator pauses both requests after Identity's default uniqueness check.
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IUserValidator<ApplicationUser>>(registrationBarrier)));
        using (var migrationScope = configuredFactory.Services.CreateScope())
        {
            var context = migrationScope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
        }

        using var firstClient = ApiTestClient.Create(configuredFactory);
        using var secondClient = ApiTestClient.Create(configuredFactory);
        var firstAntiforgeryToken = await firstClient.GetAntiforgeryTokenAsync(cancellationToken);
        var secondAntiforgeryToken = await secondClient.GetAntiforgeryTokenAsync(cancellationToken);
        var firstRegistration = firstClient.PostJsonWithAntiforgeryAsync(
            "/api/auth/register",
            new
            {
                Email = "owner@northstar.example",
                Password = IdentityTestData.ValidPassword,
                OrganisationName = "Northstar Workforce",
            },
            firstAntiforgeryToken,
            cancellationToken);
        var secondRegistration = secondClient.PostJsonWithAntiforgeryAsync(
            "/api/auth/register",
            new
            {
                Email = "OWNER@NORTHSTAR.EXAMPLE",
                Password = IdentityTestData.ValidPassword,
                OrganisationName = "Duplicate Organisation",
            },
            secondAntiforgeryToken,
            cancellationToken);

        var responses = await Task.WhenAll(firstRegistration, secondRegistration);
        using var created = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Created);
        using var duplicate = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.BadRequest);
        var problem = await AssertProblemAsync(
            duplicate,
            HttpStatusCode.BadRequest,
            "One or more validation errors occurred.",
            cancellationToken);

        Assert.NotNull(problem.Errors);
        Assert.Equal(
            ["Registration could not be completed with the supplied email address."],
            problem.Errors["email"]);

        using var verificationScope = configuredFactory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        Assert.Equal(1, await verificationContext.Users.CountAsync(cancellationToken));
        Assert.Equal(1, await verificationContext.Organisations.CountAsync(cancellationToken));
        Assert.Equal(
            1,
            await verificationContext.OrganisationMemberships.CountAsync(cancellationToken));
    }

    private sealed record RegistrationResponse(
        Guid UserId,
        string Email,
        Guid OrganisationId,
        string OrganisationName);

    private sealed record SessionResponse(Guid UserId, string? Email);

    private sealed class ConcurrentRegistrationBarrier : IUserValidator<ApplicationUser>
    {
        private readonly TaskCompletionSource registrationsReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int registrationCount;

        public async Task<IdentityResult> ValidateAsync(
            UserManager<ApplicationUser> manager,
            ApplicationUser user)
        {
            if (Interlocked.Increment(ref registrationCount) == 2)
            {
                registrationsReady.SetResult();
            }

            await registrationsReady.Task.WaitAsync(TimeSpan.FromSeconds(10));

            return IdentityResult.Success;
        }
    }
}
