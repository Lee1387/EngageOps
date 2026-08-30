using System.Net;
using System.Net.Http.Json;
using EngageOps.Api.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngageOps.Api.Tests.Http;

internal sealed class ApiTestClient : IDisposable
{
    public const string ValidPassword = "ValidPassword1!";

    private readonly HttpClient client;

    private ApiTestClient(HttpClient client)
    {
        this.client = client;
    }

    public static ApiTestClient Create(EngageOpsApiFactory factory) =>
        new(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));

    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken) =>
        client.GetAsync(path, cancellationToken);

    public Task<HttpResponseMessage> PostAsJsonAsync<TRequest>(
        string path,
        TRequest body,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(path, body, cancellationToken);

    public async Task SignInAsync(string email, CancellationToken cancellationToken)
    {
        var antiforgeryToken = await GetAntiforgeryTokenAsync(cancellationToken);
        using var response = await PostJsonWithAntiforgeryAsync(
            "/api/auth/sign-in",
            new { Email = email, Password = ValidPassword },
            antiforgeryToken,
            cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/auth/csrf", cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content
            .ReadFromJsonAsync<AntiforgeryTokenResponse>(cancellationToken);

        Assert.NotNull(token);

        return token.Token;
    }

    public async Task<HttpResponseMessage> PostJsonWithAntiforgeryAsync<TRequest>(
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

    public void Dispose() => client.Dispose();

    private sealed record AntiforgeryTokenResponse(string Token);
}
