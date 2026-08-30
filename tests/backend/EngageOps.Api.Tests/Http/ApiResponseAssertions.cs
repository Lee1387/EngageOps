using System.Net;
using System.Net.Http.Json;

namespace EngageOps.Api.Tests.Http;

internal static class ApiResponseAssertions
{
    public static async Task<ProblemResponse> AssertProblemAsync(
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
}

internal sealed record ProblemResponse(
    int Status,
    string Title,
    Dictionary<string, string[]>? Errors = null);
