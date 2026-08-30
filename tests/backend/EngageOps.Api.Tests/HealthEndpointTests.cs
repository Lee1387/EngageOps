using System.Net;

namespace EngageOps.Api.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task GetHealthReturnsOk()
    {
        using var factory = new EngageOpsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
