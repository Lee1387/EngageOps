using EngageOps.Api.Clients;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Tests.Persistence;

public class ClientPersistenceTests
{
    [Fact]
    public async Task MigrationPersistsClientAndRejectsMissingOrganisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        var organisation = Organisation.Create("Northstar Workforce");
        var client = Client.Create(organisation.Id, "Northstar Logistics");

        await using (var context = new EngageOpsDbContext(options))
        {
            await context.Database.MigrateAsync(cancellationToken);

            context.AddRange(organisation, client);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var verificationContext = new EngageOpsDbContext(options))
        {
            var persistedClient = await verificationContext.Clients
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == client.Id, cancellationToken);

            Assert.Equal(client.Id, persistedClient.Id);
            Assert.Equal(organisation.Id, persistedClient.OrganisationId);
            Assert.Equal(client.Name, persistedClient.Name);
        }

        await using var orphanContext = new EngageOpsDbContext(options);
        orphanContext.Clients.Add(
            Client.Create(Guid.CreateVersion7(), "Orphaned Client"));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            orphanContext.SaveChangesAsync(cancellationToken));
    }
}
