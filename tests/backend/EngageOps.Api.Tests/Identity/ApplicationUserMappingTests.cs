using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EngageOps.Api.Tests.Identity;

public class ApplicationUserMappingTests
{
    [Fact]
    public void ModelMapsRoleFreeIdentityUserWithApplicationGeneratedIdentity()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;

        using var context = new EngageOpsDbContext(options);
        var user = context.Model.FindEntityType(typeof(ApplicationUser));

        Assert.NotNull(user);
        Assert.Equal("AspNetUsers", user.GetTableName());
        Assert.Equal(ValueGenerated.Never, user.FindProperty(nameof(ApplicationUser.Id))!.ValueGenerated);
        Assert.Null(context.Model.FindEntityType(typeof(IdentityRole<Guid>)));
    }
}
