using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngageOps.Api.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task GetHealthReturnsOk()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
