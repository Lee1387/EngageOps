using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EngageOps.Api.Tests.Persistence;

public class AssignmentPersistenceTests
{
    [Fact]
    public async Task MigrationPersistsAssignment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        var organisation = Organisation.Create("Northstar Workforce");
        var client = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");
        var assignment = Assignment.Create(
            organisation.Id,
            client.Id,
            worker.Id,
            new DateOnly(2026, 9, 1),
            new DateOnly(2027, 2, 28));

        await using (var context = new EngageOpsDbContext(options))
        {
            await context.Database.MigrateAsync(cancellationToken);

            context.AddRange(organisation, client, worker, assignment);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var verificationContext = new EngageOpsDbContext(options);
        var persistedAssignment = await verificationContext.Assignments
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == assignment.Id, cancellationToken);

        Assert.Equal(assignment.Id, persistedAssignment.Id);
        Assert.Equal(organisation.Id, persistedAssignment.OrganisationId);
        Assert.Equal(client.Id, persistedAssignment.ClientId);
        Assert.Equal(worker.Id, persistedAssignment.WorkerId);
        Assert.Equal(assignment.StartDate, persistedAssignment.StartDate);
        Assert.Equal(assignment.EndDate, persistedAssignment.EndDate);
    }

    [Fact]
    public async Task DatabaseRejectsRelationshipsAcrossOrganisationBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        var firstOrganisation = Organisation.Create("Northstar Workforce");
        var secondOrganisation = Organisation.Create("Harbour Staffing");
        var firstClient = Client.Create(firstOrganisation.Id, "Northstar Logistics");
        var secondClient = Client.Create(secondOrganisation.Id, "Harbour Consulting");
        var firstWorker = Worker.Create(firstOrganisation.Id, "Alex Morgan");
        var secondWorker = Worker.Create(secondOrganisation.Id, "Sam Taylor");

        await using (var setupContext = new EngageOpsDbContext(options))
        {
            await setupContext.Database.MigrateAsync(cancellationToken);
            setupContext.AddRange(
                firstOrganisation,
                secondOrganisation,
                firstClient,
                secondClient,
                firstWorker,
                secondWorker);
            await setupContext.SaveChangesAsync(cancellationToken);
        }

        await using (var clientContext = new EngageOpsDbContext(options))
        {
            clientContext.Assignments.Add(
                Assignment.Create(
                    firstOrganisation.Id,
                    secondClient.Id,
                    firstWorker.Id,
                    new DateOnly(2026, 9, 1)));

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                clientContext.SaveChangesAsync(cancellationToken));

            var databaseException = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, databaseException.SqlState);
            Assert.Equal(
                "FK_assignments_clients_organisation_id_client_id",
                databaseException.ConstraintName);
        }

        await using var workerContext = new EngageOpsDbContext(options);
        workerContext.Assignments.Add(
            Assignment.Create(
                firstOrganisation.Id,
                firstClient.Id,
                secondWorker.Id,
                new DateOnly(2026, 9, 1)));

        var workerException = await Assert.ThrowsAsync<DbUpdateException>(() =>
            workerContext.SaveChangesAsync(cancellationToken));

        var workerDatabaseException = Assert.IsType<PostgresException>(workerException.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, workerDatabaseException.SqlState);
        Assert.Equal(
            "FK_assignments_workers_organisation_id_worker_id",
            workerDatabaseException.ConstraintName);
    }

    [Fact]
    public async Task DatabaseRejectsEndDateBeforeStartDate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        var organisation = Organisation.Create("Northstar Workforce");
        var client = Client.Create(organisation.Id, "Northstar Logistics");
        var worker = Worker.Create(organisation.Id, "Alex Morgan");

        await using var context = new EngageOpsDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
        context.AddRange(organisation, client, worker);
        await context.SaveChangesAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO assignments (id, organisation_id, client_id, worker_id, start_date, end_date)
                VALUES (
                    {Guid.CreateVersion7()},
                    {organisation.Id},
                    {client.Id},
                    {worker.Id},
                    {new DateOnly(2026, 9, 1)},
                    {new DateOnly(2026, 8, 31)})
                """, cancellationToken));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("CK_assignments_date_range", exception.ConstraintName);
    }
}
