using EngageOps.Api.Clients;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EngageOps.Api.Tests.Clients;

public class ClientMappingTests
{
    [Fact]
    public void ModelMapsClientWithRequiredTenantRelationshipAndLookupIndex()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;

        using var context = new EngageOpsDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Client));
        var table = StoreObjectIdentifier.Table("clients", schema: null);

        Assert.NotNull(entity);
        Assert.Equal("clients", entity.GetTableName());

        var id = entity.FindProperty(nameof(Client.Id))!;
        Assert.Equal("id", id.GetColumnName(table));
        Assert.Equal("uuid", id.GetColumnType());
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.Contains(id, entity.FindPrimaryKey()!.Properties);

        var organisationId = entity.FindProperty(nameof(Client.OrganisationId))!;
        Assert.Equal("organisation_id", organisationId.GetColumnName(table));
        Assert.Equal("uuid", organisationId.GetColumnType());
        Assert.False(organisationId.IsNullable);
        Assert.Equal(ValueGenerated.Never, organisationId.ValueGenerated);

        var name = entity.FindProperty(nameof(Client.Name))!;
        Assert.Equal("name", name.GetColumnName(table));
        Assert.Equal("character varying(200)", name.GetColumnType());
        Assert.Equal(Client.MaxNameLength, name.GetMaxLength());
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
