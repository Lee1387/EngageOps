using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngageOps.Api.Tests;

internal sealed class EngageOpsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:Database",
            "Host=localhost;Database=engageops_tests;Username=unused;Password=unused");
    }
}
