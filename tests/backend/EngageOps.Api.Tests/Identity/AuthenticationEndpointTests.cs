using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Identity;

public class AuthenticationEndpointTests
{
    private const string Email = "owner@northstar.example";
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task SessionLifecycleUsesSecureCookieAndRequiresAntiforgery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var user = await CreateUserAsync(factory, cancellationToken);
        using var client = CreateSecureClient(factory);

        using (var unauthenticatedSession = await client.GetAsync(
            "/api/auth/session",
            cancellationToken))
        {
            await AssertProblemAsync(
                unauthenticatedSession,
                HttpStatusCode.Unauthorized,
                "Authentication is required.",
                cancellationToken);
        }

        using (var missingAntiforgery = await client.PostAsJsonAsync(
            "/api/auth/sign-in",
            new { Email, Password },
            cancellationToken))
        {
            await AssertProblemAsync(
                missingAntiforgery,
                HttpStatusCode.BadRequest,
                "The antiforgery token is invalid.",
                cancellationToken);
        }

        var anonymousAntiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);
        using (var signIn = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email = Email.ToUpperInvariant(), Password },
            anonymousAntiforgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

            var authenticationCookie = signIn.Headers.GetValues("Set-Cookie")
                .Single(value => value.StartsWith("EngageOps.Authentication=", StringComparison.Ordinal));

            Assert.Contains("httponly", authenticationCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", authenticationCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", authenticationCookie, StringComparison.OrdinalIgnoreCase);
        }

        using (var session = await client.GetAsync("/api/auth/session", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            var response = await session.Content.ReadFromJsonAsync<SessionResponse>(cancellationToken);

            Assert.NotNull(response);
            Assert.Equal(user.Id, response.UserId);
            Assert.Equal(Email, response.Email);
        }

        using (var missingAntiforgery = await client.PostAsync(
            "/api/auth/sign-out",
            content: null,
            cancellationToken))
        {
            await AssertProblemAsync(
                missingAntiforgery,
                HttpStatusCode.BadRequest,
                "The antiforgery token is invalid.",
                cancellationToken);
        }

        var authenticatedAntiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);
        using (var signOut = await PostWithAntiforgeryAsync(
            client,
            "/api/auth/sign-out",
            authenticatedAntiforgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        }

        using var signedOutSession = await client.GetAsync("/api/auth/session", cancellationToken);
        await AssertProblemAsync(
            signedOutSession,
            HttpStatusCode.Unauthorized,
            "Authentication is required.",
            cancellationToken);
    }

    [Fact]
    public async Task SignInValidatesInputBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new EngageOpsApiFactory();
        using var client = CreateSecureClient(factory);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);

        using (var response = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email = " ", Password = "" },
            antiforgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(cancellationToken);

            Assert.NotNull(problem?.Errors);
            Assert.Contains("email", problem.Errors);
            Assert.Contains("password", problem.Errors);
        }

        using var oversizedResponse = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email = new string('a', 257), Password = new string('a', 257) },
            antiforgeryToken,
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedResponse.StatusCode);
        var oversizedProblem = await oversizedResponse.Content
            .ReadFromJsonAsync<ProblemResponse>(cancellationToken);

        Assert.NotNull(oversizedProblem?.Errors);
        Assert.Contains("email", oversizedProblem.Errors);
        Assert.Contains("password", oversizedProblem.Errors);

        using var invalidEmailResponse = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email = "owner\0@northstar.example", Password },
            antiforgeryToken,
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmailResponse.StatusCode);
        var invalidEmailProblem = await invalidEmailResponse.Content
            .ReadFromJsonAsync<ProblemResponse>(cancellationToken);

        Assert.NotNull(invalidEmailProblem?.Errors);
        Assert.Contains("email", invalidEmailProblem.Errors);
    }

    [Fact]
    public async Task SignInReturnsSafeProblemDetailsForMalformedJson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new EngageOpsApiFactory();
        using var client = CreateSecureClient(factory);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = new StringContent("{\"email\":", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(AuthenticationEndpoints.AntiforgeryHeaderName, antiforgeryToken);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var problem = JsonDocument.Parse(body);

        Assert.Equal((int)HttpStatusCode.BadRequest, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Bad Request", problem.RootElement.GetProperty("title").GetString());
        Assert.False(problem.RootElement.TryGetProperty("exception", out _));
        Assert.False(problem.RootElement.TryGetProperty("headers", out _));
    }

    [Fact]
    public async Task SignInReturnsSameFailureForUnknownUserAndWrongPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        await CreateUserAsync(factory, cancellationToken);
        using var client = CreateSecureClient(factory);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);

        using var wrongPassword = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email, Password = "WrongPassword1!" },
            antiforgeryToken,
            cancellationToken);
        using var unknownUser = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email = "unknown@northstar.example", Password },
            antiforgeryToken,
            cancellationToken);

        var wrongPasswordProblem = await AssertProblemAsync(
            wrongPassword,
            HttpStatusCode.Unauthorized,
            "Invalid email or password.",
            cancellationToken);
        var unknownUserProblem = await AssertProblemAsync(
            unknownUser,
            HttpStatusCode.Unauthorized,
            "Invalid email or password.",
            cancellationToken);

        Assert.Equal(wrongPasswordProblem.Status, unknownUserProblem.Status);
        Assert.Equal(wrongPasswordProblem.Title, unknownUserProblem.Title);
    }

    [Fact]
    public async Task RepeatedFailedSignInsLockTheAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var user = await CreateUserAsync(factory, cancellationToken);
        using var client = CreateSecureClient(factory);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, cancellationToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await PostJsonWithAntiforgeryAsync(
                client,
                "/api/auth/sign-in",
                new { Email, Password = "WrongPassword1!" },
                antiforgeryToken,
                cancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using (var correctPassword = await PostJsonWithAntiforgeryAsync(
            client,
            "/api/auth/sign-in",
            new { Email, Password },
            antiforgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, correctPassword.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var persistedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.NotNull(persistedUser);
        Assert.True(await userManager.IsLockedOutAsync(persistedUser));
        Assert.True(persistedUser.LockoutEnd > DateTimeOffset.UtcNow);
    }

    private static HttpClient CreateSecureClient(EngageOpsApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

    private static async Task<ApplicationUser> CreateUserAsync(
        EngageOpsApiFactory factory,
        CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var user = new ApplicationUser { UserName = Email, Email = Email };
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(user, Password);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));

        return user;
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

    private static async Task<HttpResponseMessage> PostWithAntiforgeryAsync(
        HttpClient client,
        string path,
        string antiforgeryToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
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

    private sealed record SessionResponse(Guid UserId, string? Email);

    private sealed record ProblemResponse(
        int Status,
        string Title,
        Dictionary<string, string[]>? Errors = null);
}
