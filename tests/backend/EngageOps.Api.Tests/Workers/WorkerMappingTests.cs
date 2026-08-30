using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EngageOps.Api.Tests.Workers;

public class WorkerMappingTests
{
    [Fact]
    public void ModelMapsWorkerWithRequiredTenantRelationshipAndLookupIndex()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;

        using var context = new EngageOpsDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Worker));
        var table = StoreObjectIdentifier.Table("workers", schema: null);

        Assert.NotNull(entity);
        Assert.Equal("workers", entity.GetTableName());

        var id = entity.FindProperty(nameof(Worker.Id))!;
        Assert.Equal("id", id.GetColumnName(table));
        Assert.Equal("uuid", id.GetColumnType());
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.Contains(id, entity.FindPrimaryKey()!.Properties);

        var organisationId = entity.FindProperty(nameof(Worker.OrganisationId))!;
        Assert.Equal("organisation_id", organisationId.GetColumnName(table));
        Assert.Equal("uuid", organisationId.GetColumnType());
        Assert.False(organisationId.IsNullable);
        Assert.Equal(ValueGenerated.Never, organisationId.ValueGenerated);

        var name = entity.FindProperty(nameof(Worker.Name))!;
        Assert.Equal("name", name.GetColumnName(table));
        Assert.Equal("character varying(200)", name.GetColumnType());
        Assert.Equal(Worker.MaxNameLength, name.GetMaxLength());
        Assert.False(name.IsNullable);

        var organisationForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Organisation));

        Assert.Equal(new[] { organisationId }, organisationForeignKey.Properties);
        Assert.Equal(DeleteBehavior.Restrict, organisationForeignKey.DeleteBehavior);
        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Count == 1 && index.Properties[0] == organisationId);
    }
}
