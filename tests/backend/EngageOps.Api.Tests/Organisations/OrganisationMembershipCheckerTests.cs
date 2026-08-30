using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationMembershipCheckerTests
{
    [Fact]
    public async Task IsMemberAsyncReturnsTrueOnlyForExistingMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        await using var context = new EngageOpsDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);

        var organisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var otherUser = new ApplicationUser { UserName = "owner@summit.example" };
        context.AddRange(
            organisation,
            otherOrganisation,
            user,
            otherUser,
            OrganisationMembership.Create(organisation.Id, user.Id),
            OrganisationMembership.Create(otherOrganisation.Id, otherUser.Id));
        await context.SaveChangesAsync(cancellationToken);

        var checker = new OrganisationMembershipChecker(context);

        Assert.True(await checker.IsMemberAsync(user.Id, organisation.Id, cancellationToken));
        Assert.False(await checker.IsMemberAsync(user.Id, otherOrganisation.Id, cancellationToken));
        Assert.False(await checker.IsMemberAsync(
            user.Id,
            Guid.CreateVersion7(),
            cancellationToken));
    }
}
