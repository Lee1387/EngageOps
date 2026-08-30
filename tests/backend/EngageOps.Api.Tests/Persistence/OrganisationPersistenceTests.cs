using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Tests.Persistence;

public class OrganisationPersistenceTests
{
    [Fact]
    public async Task MigrationPersistsAndReloadsOrganisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        var organisation = Organisation.Create("Northstar Workforce");

        await using (var context = new EngageOpsDbContext(options))
        {
            Assert.NotEmpty(await context.Database.GetPendingMigrationsAsync(cancellationToken));

            await context.Database.MigrateAsync(cancellationToken);

            Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));

            context.Organisations.Add(organisation);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var verificationContext = new EngageOpsDbContext(options);
        var persistedOrganisation = await verificationContext.Organisations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == organisation.Id, cancellationToken);

        Assert.Equal(organisation.Id, persistedOrganisation.Id);
        Assert.Equal(organisation.Name, persistedOrganisation.Name);
    }
}
