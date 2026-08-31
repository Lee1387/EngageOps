using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngageOps.Api.Tests;

internal sealed class EngageOpsApiFactory : WebApplicationFactory<Program>
{
    private const string DefaultDatabaseConnectionString =
        "Host=localhost;Database=engageops_tests;Username=unused;Password=unused";

    private readonly string databaseConnectionString;
    private readonly bool applyMigrationsOnStartup;

    public EngageOpsApiFactory(
        string? databaseConnectionString = null,
        bool applyMigrationsOnStartup = false)
    {
        this.databaseConnectionString = databaseConnectionString ?? DefaultDatabaseConnectionString;
        this.applyMigrationsOnStartup = applyMigrationsOnStartup;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", databaseConnectionString);
        builder.UseSetting(
            "Database:ApplyMigrationsOnStartup",
            applyMigrationsOnStartup.ToString());
    }
}
