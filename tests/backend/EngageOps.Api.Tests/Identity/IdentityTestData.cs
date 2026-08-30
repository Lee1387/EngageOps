using EngageOps.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Identity;

internal static class IdentityTestData
{
    public const string ValidPassword = "ValidPassword1!";

    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        string email,
        string password = ValidPassword)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(user, password);

        Assert.True(
            result.Succeeded,
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return user;
    }
}
