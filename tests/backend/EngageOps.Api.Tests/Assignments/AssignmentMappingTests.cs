using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EngageOps.Api.Tests.Assignments;

public class AssignmentMappingTests
{
    [Fact]
    public void ModelMapsAssignmentWithTenantSafeRelationshipsAndDateConstraint()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;

        using var context = new EngageOpsDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(Assignment));
        var table = StoreObjectIdentifier.Table("assignments", schema: null);

        Assert.NotNull(entity);
        Assert.Equal("assignments", entity.GetTableName());

        var id = entity.FindProperty(nameof(Assignment.Id))!;
        Assert.Equal("id", id.GetColumnName(table));
        Assert.Equal("uuid", id.GetColumnType());
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.Contains(id, entity.FindPrimaryKey()!.Properties);

        var organisationId = entity.FindProperty(nameof(Assignment.OrganisationId))!;
        Assert.Equal("organisation_id", organisationId.GetColumnName(table));
        Assert.Equal("uuid", organisationId.GetColumnType());
        Assert.False(organisationId.IsNullable);
        Assert.Equal(ValueGenerated.Never, organisationId.ValueGenerated);

        var clientId = entity.FindProperty(nameof(Assignment.ClientId))!;
        Assert.Equal("client_id", clientId.GetColumnName(table));
        Assert.Equal("uuid", clientId.GetColumnType());
        Assert.False(clientId.IsNullable);
        Assert.Equal(ValueGenerated.Never, clientId.ValueGenerated);

        var workerId = entity.FindProperty(nameof(Assignment.WorkerId))!;
        Assert.Equal("worker_id", workerId.GetColumnName(table));
        Assert.Equal("uuid", workerId.GetColumnType());
        Assert.False(workerId.IsNullable);
        Assert.Equal(ValueGenerated.Never, workerId.ValueGenerated);

        var startDate = entity.FindProperty(nameof(Assignment.StartDate))!;
        Assert.Equal("start_date", startDate.GetColumnName(table));
        Assert.Equal("date", startDate.GetColumnType());
        Assert.False(startDate.IsNullable);

        var endDate = entity.FindProperty(nameof(Assignment.EndDate))!;
        Assert.Equal("end_date", endDate.GetColumnName(table));
        Assert.Equal("date", endDate.GetColumnType());
        Assert.True(endDate.IsNullable);

        var organisationForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Organisation));
        Assert.Equal(new[] { organisationId }, organisationForeignKey.Properties);
        Assert.Equal(DeleteBehavior.Restrict, organisationForeignKey.DeleteBehavior);

        var clientForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Client));
        Assert.Equal(new[] { organisationId, clientId }, clientForeignKey.Properties);
        Assert.Equal(
            new[] { nameof(Client.OrganisationId), nameof(Client.Id) },
            clientForeignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, clientForeignKey.DeleteBehavior);

        var workerForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Worker));
        Assert.Equal(new[] { organisationId, workerId }, workerForeignKey.Properties);
        Assert.Equal(
            new[] { nameof(Worker.OrganisationId), nameof(Worker.Id) },
            workerForeignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, workerForeignKey.DeleteBehavior);

        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.SequenceEqual([organisationId, clientId]));
        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.SequenceEqual([organisationId, workerId]));

        var dateConstraint = Assert.Single(entity.GetCheckConstraints());
        Assert.Equal("CK_assignments_date_range", dateConstraint.Name);
        Assert.Equal("end_date IS NULL OR end_date >= start_date", dateConstraint.Sql);
    }
}
