using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Tests.Persistence;

public class WorkerPersistenceTests
{
    [Fact]
    public async Task MigrationPersistsWorkerAndRejectsMissingOrganisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        var organisation = Organisation.Create("Northstar Workforce");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");

        await using (var context = new EngageOpsDbContext(options))
        {
            await context.Database.MigrateAsync(cancellationToken);

            context.AddRange(organisation, worker);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var verificationContext = new EngageOpsDbContext(options))
        {
            var persistedWorker = await verificationContext.Workers
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == worker.Id, cancellationToken);

            Assert.Equal(worker.Id, persistedWorker.Id);
            Assert.Equal(organisation.Id, persistedWorker.OrganisationId);
            Assert.Equal(worker.Name, persistedWorker.Name);
        }

        await using var orphanContext = new EngageOpsDbContext(options);
        orphanContext.Workers.Add(
            Worker.Create(Guid.CreateVersion7(), "Orphaned Worker"));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            orphanContext.SaveChangesAsync(cancellationToken));
    }
}
