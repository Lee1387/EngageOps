using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Tests.Persistence;

public class OrganisationMembershipPersistenceTests
{
    [Fact]
    public async Task MigrationPersistsMultiOrganisationMembershipAndRejectsDuplicate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        var firstOrganisation = Organisation.Create("Northstar Workforce");
        var secondOrganisation = Organisation.Create("Summit Staffing");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };

        await using (var context = new EngageOpsDbContext(options))
        {
            await context.Database.MigrateAsync(cancellationToken);

            context.AddRange(
                firstOrganisation,
                secondOrganisation,
                user,
                OrganisationMembership.Create(firstOrganisation.Id, user.Id),
                OrganisationMembership.Create(secondOrganisation.Id, user.Id));
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var verificationContext = new EngageOpsDbContext(options))
        {
            var memberships = await verificationContext.OrganisationMemberships
                .AsNoTracking()
                .Where(membership => membership.UserId == user.Id)
                .ToListAsync(cancellationToken);

            Assert.Equal(2, memberships.Count);
            Assert.Contains(memberships, membership =>
                membership.OrganisationId == firstOrganisation.Id);
            Assert.Contains(memberships, membership =>
                membership.OrganisationId == secondOrganisation.Id);
        }

        await using var duplicateContext = new EngageOpsDbContext(options);
        duplicateContext.OrganisationMemberships.Add(
            OrganisationMembership.Create(firstOrganisation.Id, user.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            duplicateContext.SaveChangesAsync(cancellationToken));
    }
}
