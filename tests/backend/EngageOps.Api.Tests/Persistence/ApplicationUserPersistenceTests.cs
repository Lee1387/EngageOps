using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Persistence;

public class ApplicationUserPersistenceTests
{
    [Fact]
    public async Task IdentityStorePersistsAndReloadsApplicationUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var user = new ApplicationUser
        {
            UserName = "owner@northstar.example",
            Email = "owner@northstar.example",
        };

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await userManager.CreateAsync(user);

            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationUserManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var persistedUser = await verificationUserManager.FindByIdAsync(user.Id.ToString());

        Assert.NotNull(persistedUser);
        Assert.Equal(user.Id, persistedUser.Id);
        Assert.Equal(user.UserName, persistedUser.UserName);
        Assert.Equal(user.Email, persistedUser.Email);
        Assert.Equal(user.SecurityStamp, persistedUser.SecurityStamp);
    }
}
