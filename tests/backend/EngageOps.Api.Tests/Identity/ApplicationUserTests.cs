using EngageOps.Api.Identity;

namespace EngageOps.Api.Tests.Identity;

public class ApplicationUserTests
{
    [Fact]
    public void ConstructorSetsVersionSevenIdentityAndSecurityStamp()
    {
        var user = new ApplicationUser();

        Assert.Equal(7, user.Id.Version);
        Assert.NotNull(user.SecurityStamp);
    }
}
