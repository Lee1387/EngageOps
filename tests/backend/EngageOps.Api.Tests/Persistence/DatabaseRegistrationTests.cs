using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Persistence;

public class DatabaseRegistrationTests
{
    [Fact]
    public void ApplicationRegistersNpgsqlDbContext()
    {
        using var factory = new EngageOpsApiFactory();
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }

    [Fact]
    public void ApplicationRegistersIdentityCoreWithEntityFrameworkStore()
    {
        using var factory = new EngageOpsApiFactory();
        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();

        Assert.NotNull(userManager);
        Assert.NotNull(userStore);
    }

    [Fact]
    public async Task ApplicationAppliesMigrationsOnStartupWhenConfigured()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(
            postgreSql.GetConnectionString(),
            applyMigrationsOnStartup: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health", cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);

        Assert.Empty(pendingMigrations);
    }
}
