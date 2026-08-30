using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngageOps.Api.Tests;

internal sealed class EngageOpsApiFactory : WebApplicationFactory<Program>
{
    private const string DefaultDatabaseConnectionString =
        "Host=localhost;Database=engageops_tests;Username=unused;Password=unused";

    private readonly string databaseConnectionString;

    public EngageOpsApiFactory(string? databaseConnectionString = null)
    {
        this.databaseConnectionString = databaseConnectionString ?? DefaultDatabaseConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", databaseConnectionString);
    }
}
