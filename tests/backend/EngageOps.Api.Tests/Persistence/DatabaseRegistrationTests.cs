using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Identity;
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
}
